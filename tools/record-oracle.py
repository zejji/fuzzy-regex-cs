#!/usr/bin/env python
"""Records upstream's *matching* results for a generated wave of rows.

The analogue of ``tools/record-compile-corpus.py`` for the engine: that one records what upstream's
compiler produces, this one records what upstream's matcher answers. Same shape - recorder in
Python, consumer in C# - with one deliberate difference. The corpus is committed, because it is
derived from upstream's own finite test suite; a wave is *generated from a seed* and thrown away,
because the space of (pattern, subject) pairs is not finite and pinning one sample of it would
turn a generative tester into a fixture. Only minimised divergences become permanent tests.

Written by S14. Design spec amendment 10 has the reasoning for having it at all.

Index translation happens here
------------------------------
Python indexes a ``str`` by codepoint; .NET indexes a ``string`` by UTF-16 code unit. The
conversion is done in this recorder, against the subject it has in hand, so the file holds the
``(Index, Length)`` values our public API must return and the C# consumer compares them verbatim.
This is one of exactly two places the span convention is enforced (DECISIONS 2026-08-31); the
other is ``Match``/``Group``'s accessors. A slip at either end shows up as a divergence rather
than as a silent agreement. Each match row also carries ``codepointSpan`` - Python's own answer,
untranslated - which is what makes the translation visible in the file rather than merely trusted.

Minimisation workflow
---------------------
A divergence is not a finding until it is one row long, and it is not fixed until it is a test:

1. The C# consumer writes ``TestResults/oracle/report.txt``, one block per divergence, quoting the
   pattern, the flags, the operation, the subject and both sides' answers.
2. Copy that row into a ``.jsonl`` file - one JSON object per line, keys ``pattern``, ``flags``,
   ``namedLists``, ``subject``, ``operation`` - and shrink it by hand: drop pattern elements, then
   subject characters, re-recording with ``--rows FILE`` after each cut and re-running the
   consumer. Stop at the smallest pair that still diverges.
3. Pin *that* pair as an ordinary test in ``tests/FuzzyRegex.Tests/Gaps/``, with the recorded
   upstream answer quoted in a comment. VERIFICATION.md rule 7 requires this; a divergence that
   only lives in a wave disappears the next time the seed changes.

Usage::

    python tools/record-oracle.py [--generator literals,literal-dot,anchors] [--seed N] [--count N]
    python tools/record-oracle.py --rows candidate.jsonl
    python tools/record-oracle.py --verify-determinism

The output is JSONL: one header object, then one row object per line. ASCII-only, LF endings, and
no timestamps anywhere, so two runs of the same seed are byte-identical and can be diffed.
"""

from __future__ import annotations

import argparse
import filecmp
import json
import os
import random
import re
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_OUTPUT = REPO_ROOT / "TestResults" / "oracle" / "wave.jsonl"

# Upstream never published the pinned release to PyPI, so a dev machine's `pip install regex`
# gives the previous one. The changelog delta between them is the single line "Support Python
# 3.15." (upstream/changelog.txt, verified 2026-08-31) - behaviourally empty, so a local run
# against it is sound. Anything *else* is version drift masquerading as a port defect, and costs
# a session to chase down, so it fails the run. DECISIONS 2026-08-31, owner-approved.
PYPI_FALLBACK_VERSION = "2026.7.19"

OPERATIONS = ("search", "match", "fullmatch")

# regex.compile takes named lists as **kwargs (signature verified 2026-08-31:
# `(pattern, flags=0, ignore_unused=False, cache_pattern=None, **kwargs)`), so a list whose name is
# one of its own parameters cannot be passed at all - the call fails with "compile() got multiple
# values for argument 'flags'". Our own API takes named lists in a separate dictionary and can
# express such a row, but there is no ground truth to compare it against, and recording the
# TypeError would file a failure of the *call* as upstream's answer about the *pattern*.
RESERVED_NAMES = ("pattern", "flags", "ignore_unused", "cache_pattern")

# Failures of the interpreter rather than judgements about the pattern: a larger recursion limit or
# more memory would change the answer, so "upstream rejected this input" is not true of them and a
# port that accepts the pattern is not thereby wrong. Recorded as an outcome they would be compared
# against whatever this port did, and any rejection at all would score as agreement.
ENVIRONMENT_FAILURES = ("RecursionError", "MemoryError", "OverflowError")

# Two alphabets, alternating row by row: one plain ASCII, one mixing BMP and astral characters so
# the UTF-16 translation is exercised from the first wave rather than from the first bug. No
# metacharacter is in either, so a generated pattern needs no escaping and the generators stay
# what they claim to be - literals. U+1F600 GRINNING FACE and U+1D518 MATHEMATICAL FRAKTUR
# CAPITAL U are both astral, so each contributes two UTF-16 units and one codepoint.
ALPHABETS = ("abcde", "ab\U0001f600\U0001d518c")

MAX_SUBJECT_LENGTH = 8
MAX_PATTERN_LENGTH = 4
DOT_PROBABILITY = 0.3
SUBSTRING_PROBABILITY = 0.7


# --------------------------------------------------------------------------------------------
# The version policy
# --------------------------------------------------------------------------------------------


def _pinned_version() -> str:
    """The version ``upstream/`` is pinned at, read from its own pyproject."""
    text = (REPO_ROOT / "upstream" / "pyproject.toml").read_text(encoding="utf-8")
    match = re.search(r'^version = "(.+)"', text, re.M)
    if not match:
        raise SystemExit("cannot find the pinned version in upstream/pyproject.toml")
    return match.group(1)


def _check_version(installed: str) -> str:
    pinned = _pinned_version()
    if installed == pinned:
        return "pinned"
    if installed == PYPI_FALLBACK_VERSION:
        return "pypi"
    raise SystemExit(
        f"the oracle is regex {installed}, which is neither the pinned {pinned} (CI builds it "
        f"from upstream/) nor the PyPI fallback {PYPI_FALLBACK_VERSION}. Divergences recorded "
        "against any other version are version drift, not port defects."
    )


def _upstream_commit() -> str:
    return subprocess.run(
        ["git", "-C", str(REPO_ROOT / "upstream"), "rev-parse", "HEAD"],
        capture_output=True, text=True, check=True,
    ).stdout.strip()


# --------------------------------------------------------------------------------------------
# Codepoint to UTF-16
# --------------------------------------------------------------------------------------------


def _utf16_offsets(subject: str) -> list[int]:
    """``offsets[i]`` is the UTF-16 index of codepoint ``i``, for ``i`` in ``0..len(subject)``.

    One entry past the end, because a span's end is an exclusive index and may be ``len``. Built
    per subject rather than per span so a row with many groups stays linear.
    """
    offsets = [0] * (len(subject) + 1)
    units = 0
    for i, char in enumerate(subject):
        offsets[i] = units
        units += 2 if ord(char) > 0xFFFF else 1
    offsets[len(subject)] = units
    return offsets


def _to_index_length(offsets: list[int], span: tuple[int, int]) -> list[int]:
    """A Python codepoint ``(start, end)`` as the UTF-16 ``[Index, Length]`` our API returns."""
    start, end = span
    index = offsets[start]
    return [index, offsets[end] - index]


# --------------------------------------------------------------------------------------------
# Running one row through the oracle
# --------------------------------------------------------------------------------------------


def _record_row(regex, row: dict) -> dict:
    """Runs one row against upstream and returns it with its recorded outcome attached."""
    pattern = row["pattern"]
    flags = int(row.get("flags", 0))
    named_lists = _canonical_named_lists(row.get("namedLists") or {})
    subject = row["subject"]
    operation = row["operation"]
    if operation not in OPERATIONS:
        raise SystemExit(f"unknown operation {operation!r}; expected one of {OPERATIONS}")

    reserved = sorted(set(named_lists) & set(RESERVED_NAMES))
    if reserved:
        raise SystemExit(
            f"named list {reserved[0]!r} collides with a parameter of regex.compile, so upstream "
            f"cannot be asked this row and there is no ground truth for it (pattern {pattern!r}). "
            "If a slice ever needs these names, record them through regex._main._compile instead "
            "of the public API."
        )

    recorded = {
        "generator": row.get("generator", "rows"),
        "pattern": pattern,
        "flags": flags,
        "namedLists": named_lists,
        "subject": subject,
        "operation": operation,
    }

    try:
        compiled = regex.compile(pattern, flags, **named_lists)
        match = getattr(compiled, operation)(subject)
    except Exception as e:  # noqa: BLE001 - the exception *is* the recorded answer
        if type(e).__name__ in ENVIRONMENT_FAILURES:
            raise SystemExit(
                f"upstream raised {type(e).__name__} on pattern {pattern!r} against subject "
                f"{subject!r}. That is a limit of the interpreter, not a judgement about the "
                "pattern, so it is not comparable and is not recorded."
            ) from e
        recorded["codepointSpan"] = None
        recorded["outcome"] = {
            "kind": "error",
            "exception": type(e).__name__,
            # regex.error's str() appends the position; .msg is the text our
            # FuzzyRegexParseException.Message carries, which is what the corpus compares too.
            "message": e.msg if isinstance(e, regex.error) else str(e),
        }
        return recorded

    if match is None:
        recorded["codepointSpan"] = None
        recorded["outcome"] = {"kind": "nomatch"}
        return recorded

    offsets = _utf16_offsets(subject)
    groups = []
    for number in range(compiled.groups + 1):
        span = match.span(number)
        success = span != (-1, -1)
        index, length = _to_index_length(offsets, span) if success else (0, 0)
        groups.append({
            "number": number,
            "success": success,
            "index": index,
            "length": length,
            # Every capture the group made, oldest first - upstream keeps the full list for any
            # group, not just for one inside a repeat, and Group.Captures has to match.
            "captures": [_to_index_length(offsets, s) for s in match.spans(number)],
        })

    recorded["codepointSpan"] = list(match.span(0))
    recorded["outcome"] = {
        "kind": "match",
        "groups": groups,
        # Neither is derivable from the groups: 'lastindex' is the group that *closed* last, so
        # regex.match('((a))', 'a').lastindex is 1 even though groups 1 and 2 both succeed with the
        # same span, and 'lastgroup' names the last *named* group even when an unnamed one succeeded
        # later. None becomes -1 and null, which is what Match.LastGroupNumber and
        # Match.LastGroupName report.
        "lastIndex": -1 if match.lastindex is None else match.lastindex,
        "lastGroup": match.lastgroup,
    }
    return recorded


def _canonical_named_lists(named_lists: dict) -> dict:
    """Each named list sorted, and the names too.

    Not cosmetic. ``StringSet.__init__`` sorts its branches by length only - a *stable* sort - so
    the caller's container order leaks into the bytecode and therefore into what matches. A
    harness that hands upstream a different order from the one it hands the port is comparing two
    different questions: 58 of the 62 divergences in S13's wave were exactly that
    (DECISIONS 2026-08-31). Sorting both sides' input here is what makes the comparison honest.
    """
    return {name: sorted(named_lists[name]) for name in sorted(named_lists)}


# --------------------------------------------------------------------------------------------
# The generators
# --------------------------------------------------------------------------------------------


GENERATORS = ("literals", "literal-dot", "anchors", "classes", "groups", "quantifiers")

# The zero-width assertions the S16 spine implements, as (prefix, suffix) pairs wrapped round a
# literal. Every one is a plain anchor: word and grapheme boundaries are S20 and would only produce
# unsupported rows. `\G` is upstream's SEARCH_ANCHOR, which is only interesting when the operation
# is a search, so it is paired with an empty suffix rather than combined.
ANCHOR_AFFIXES = (
    ("^", ""),
    ("", "$"),
    ("^", "$"),
    (r"\A", ""),
    ("", r"\Z"),
    (r"\A", r"\Z"),
    (r"\G", ""),
    ("", ""),
)

# Recorded both with and without MULTILINE, because that is exactly what changes '^' and '$' from
# START_OF_STRING/END_OF_STRING_LINE into START_OF_LINE/END_OF_LINE - four separate opcodes and four
# separate try_match predicates in the port. regex.MULTILINE is 8.
MULTILINE = 8

# Line separators, mixed into the anchor generator's subjects so '^' and '$' have somewhere to match
# other than the two ends. CR/LF is in the list as one item because upstream refuses to break inside
# it (ascii_at_line_start, upstream/src/_regex.c:899) and a generator that never emits the pair would
# never test that.
LINE_BREAKS = ("\n", "\r", "\r\n", " ", "")


# regex.ASCII and regex.VERSION1 (upstream/regex/_regex_core.py lines 74 and 88).
ASCII = 0x80
VERSION1 = 0x100

# The class atoms the S17 engine can match: RANGE, PROPERTY and the four SET_* operators, positive
# and negated, plus a bare literal so a generated pattern is not all classes. Every one is a single
# node that consumes exactly one character - no quantifier, no group, no alternation - because a
# generator that emits an opcode no slice has ported produces `unsupported` rows and tells nobody
# anything. The '&&', '--', '||' and '~~' forms need V1, so they are drawn separately below.
CLASS_ATOMS = (
    "[a]",
    "[^a]",
    "[abz]",
    "[^abz]",
    "[a-f]",
    "[^a-f]",
    "[a-fA-F0-9]",
    "[0-9a-f_]",
    r"\d",
    r"\D",
    r"\w",
    r"\W",
    r"\s",
    r"\S",
    r"[\d]",
    r"[^\d]",
    r"[\w\s]",
    r"[^\w\s]",
    r"[a\d]",
    r"[^a\d]",
    "[[:alpha:]]",
    "[[:^alpha:]]",
    "[[:digit:]]",
    "[[:punct:]]",
    "[[:xdigit:]]",
    r"\p{L}",
    r"\P{L}",
    r"\p{Lu}",
    r"\p{Nd}",
    r"\p{^Nd}",
    r"\p{Greek}",
    r"\p{Cyrillic}",
    r"\p{ASCII}",
    r"\p{Alnum}",
    r"[\p{L}\p{N}]",
    r"[^\p{L}]",
    "a",
    "Z",
    "0",
)

# The four V1 set operators, nested one level, so 'in_set_diff', 'in_set_inter', 'in_set_sym_diff'
# and 'in_set_union' are each reached with a member list longer than one and with a nested set as a
# member - which is the only way 'matches_member' recurses.
V1_CLASS_ATOMS = (
    r"[\p{ASCII}&&\p{L}]",
    r"[\p{L}&&\p{ASCII}&&[a-z]]",
    r"[\p{ASCII}--\p{L}]",
    r"[[a-z]--[aei]]",
    r"[\w--[0-9]]",
    r"[\p{L}||\p{N}]",
    r"[[a-c]||[x-z]]",
    r"[\p{Alnum}~~\p{L}]",
    r"[[a-f]~~[d-k]]",
    r"[^[\p{L}--[a-z]]]",
)

# Four bands, cycled row by row so no wave is all-ASCII: ASCII, Latin-1 (the first place the ASCII
# flag changes an answer), the BMP above it, and astral - where a codepoint is two UTF-16 code units,
# so a property looked up by 'char' rather than by codepoint would answer differently.
CLASS_SUBJECT_ALPHABETS = (
    "aZ0_ -.\t",
    "éÅµß· ",
    "ΓγЖж中٠ ",
    "\U0001f600\U0001d518\U0001d7ee\U00010400\U0001f4a9",
)

# How many atoms a generated class pattern holds, and how often. Weighted towards one, because
# every extra atom multiplies the chance that the row simply does not match, and a wave that is
# nearly all 'nomatch' exercises the failure path and almost nothing else. Measured over 400 rows of
# seed 1: a flat 1-3 draw gave 45 matching rows, this weighting gives 91.
CLASS_PATTERN_ATOMS = (1, 2, 3)
CLASS_PATTERN_ATOM_WEIGHTS = (6, 3, 1)

# Shorter subjects than the literal generators use. A third of the rows are 'fullmatch', which
# against an eight-character subject needs an eight-atom pattern to have any chance at all: over the
# same 400 rows a maximum of 8 gave 1 matching fullmatch row, and a maximum of 4 gives 10.
MAX_CLASS_SUBJECT_LENGTH = 4


def _generate_classes(rng: random.Random, count: int):
    """S17's generator: one to three class atoms in a row, over four bands of codepoint.

    No substring trick here, unlike the literal generators: a class already matches a whole band of
    characters, so drawing atoms independently of the subject still gives a healthy mix of matching
    and non-matching rows. The subject is never empty, because an empty one makes every
    one-character class fail for the same uninteresting reason.
    """
    for i in range(count):
        alphabet = CLASS_SUBJECT_ALPHABETS[i % len(CLASS_SUBJECT_ALPHABETS)]
        subject = "".join(rng.choice(alphabet) for _ in range(rng.randrange(1, MAX_CLASS_SUBJECT_LENGTH + 1)))

        # The set operators are a V1-only syntax, so half the rows opt in and only those may draw
        # from the operator pool. Drawn from the seeded stream rather than from 'i', for the reason
        # recorded against the anchor generator below: indexing several tables by 'i' aliases them.
        version1 = rng.random() < 0.5
        pool = CLASS_ATOMS + V1_CLASS_ATOMS if version1 else CLASS_ATOMS

        flags = 0
        if version1:
            flags |= VERSION1
        if rng.random() < 0.5:
            # The whole of the ASCII flag's matching story is that the property table swaps, so it is
            # only observable on a subject above U+007F - which is three of the four bands.
            flags |= ASCII

        atoms = rng.choices(CLASS_PATTERN_ATOMS, weights=CLASS_PATTERN_ATOM_WEIGHTS)[0]

        yield {
            "generator": "classes",
            "pattern": "".join(rng.choice(pool) for _ in range(atoms)),
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }


# --------------------------------------------------------------------------------------------
# S18's generator: alternation, capture groups and the whole group surface
# --------------------------------------------------------------------------------------------

# The atoms a generated group pattern is built from. Every one consumes exactly one character or
# nothing at all: no quantifier (S19), no backreference (S21), no lookaround (Phase 4), because a
# generator that emits an opcode no slice has ported produces `unsupported` rows and tells nobody
# anything. Kept deliberately narrow - one letter, one class, one dot - so that what the row is
# really testing is the group and branch structure wrapped round them.
GROUP_ATOMS = ("a", "b", "c", "x", ".", "[ab]", "[^a]", r"\w", r"\d")

# The subjects. Short, because a pattern of n atoms can only match n characters without a
# quantifier, and a subject much longer than the pattern makes every 'fullmatch' row fail for the
# same uninteresting reason. The astral alphabet is here for the same reason as in the literal
# generators: a group span reported in codepoints rather than UTF-16 code units has to show up as a
# divergence from the first wave.
GROUP_SUBJECT_ALPHABETS = ("abcx", "abx1_", "ab\U0001f600\U0001d518c")

MAX_GROUP_SUBJECT_LENGTH = 5

# How deep a generated pattern nests a group inside a group. Two is enough to reach every case that
# matters - an inner group of an unmatched alternative, a group closing before its parent, the
# 'lastindex' rule that reports the group which closed last rather than the highest-numbered one -
# and three multiplies the chance the row simply does not match.
MAX_GROUP_DEPTH = 2

# How often each shape is drawn. Weighted towards the plain group and the two-way branch: those are
# the two opcodes this generator exists to exercise, and the empty alternative is rare enough in
# real patterns that giving it an equal share would crowd them out.
GROUP_SHAPES = ("atom", "group", "named", "noncapture", "branch", "branch3", "empty-branch")
GROUP_SHAPE_WEIGHTS = (30, 20, 8, 8, 20, 8, 6)


def _group_pattern(rng: random.Random, depth: int, namer) -> str:
    """One generated pattern fragment, recursively.

    ``namer`` hands out distinct group names, because a repeated one is legal upstream but means
    something quite different (two groups sharing a name), and mixing that in here would make a
    divergence ambiguous between the branch machinery and the duplicate-name machinery.
    """
    shape = rng.choices(GROUP_SHAPES, weights=GROUP_SHAPE_WEIGHTS)[0]
    if depth >= MAX_GROUP_DEPTH:
        shape = "atom"

    if shape == "atom":
        return rng.choice(GROUP_ATOMS)

    inner = "".join(_group_pattern(rng, depth + 1, namer) for _ in range(rng.randrange(1, 3)))

    if shape == "group":
        return f"({inner})"
    if shape == "named":
        return f"(?P<{namer()}>{inner})"
    if shape == "noncapture":
        return f"(?:{inner})"

    other = "".join(_group_pattern(rng, depth + 1, namer) for _ in range(rng.randrange(1, 3)))
    if shape == "branch":
        return f"({inner}|{other})"
    if shape == "branch3":
        third = _group_pattern(rng, depth + 1, namer)
        return f"({inner}|{other}|{third})"

    # 'empty-branch': an alternative that matches nothing at all, which is the only way to reach a
    # group whose span is (pos, pos) rather than absent - the distinction same_span_as_group draws
    # (upstream/src/_regex.c:11639).
    return f"({inner}|)"


def _generate_groups(rng: random.Random, count: int):
    """S18's generator: nested and named capture groups round alternations.

    The subject is drawn independently of the pattern rather than sliced out of it, unlike the
    literal generators: a branch over single-character atoms already matches a wide range of
    subjects, so an independent draw still gives a mix of matching and non-matching rows without
    the substring trick. Measured over 300 rows of seed 1: 92 match, 208 do not, 77 with an astral
    subject. That is the best of the shapes tried - with up to three top-level fragments instead of
    two it was 70/230, and shortening the subject to four characters gave 91/209 - and it is in line
    with the `classes` generator, which sits at about the same fraction.
    """
    for i in range(count):
        alphabet = GROUP_SUBJECT_ALPHABETS[i % len(GROUP_SUBJECT_ALPHABETS)]
        subject = "".join(
            rng.choice(alphabet) for _ in range(rng.randrange(1, MAX_GROUP_SUBJECT_LENGTH + 1))
        )

        counter = [0]

        def namer(counter=counter) -> str:
            counter[0] += 1
            return f"g{counter[0]}"

        pattern = "".join(_group_pattern(rng, 0, namer) for _ in range(rng.randrange(1, 3)))

        yield {
            "generator": "groups",
            "pattern": pattern,
            "flags": 0,
            "namedLists": {},
            "subject": subject,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }


# --------------------------------------------------------------------------------------------
# S19's generator: greedy and lazy quantifiers, the ONE fast paths, and the repeat guards
# --------------------------------------------------------------------------------------------

# What a quantifier is put on. Every one consumes exactly one character, so the count a repeat
# reports is a character count and the span it reports is in UTF-16 code units - the whole reason
# S19 needed 'StepBy' and 'CountBetween'. A quantified single-character atom compiles to
# GREEDY_REPEAT_ONE / LAZY_REPEAT_ONE, which is the fast path with its own backtrack sub-switch, so
# these reach different code from the group shapes below.
QUANT_ATOMS = ("a", "b", "x", ".", "[ab]", "[^a]", r"\w", r"\d", r"\s", "[a-c]")

# The quantifiers, as (suffix, weight). '{m,n}' forms are drawn separately so m and n vary. Weighted
# towards '*' and '+' because those are what real patterns hold, and because a wave that is mostly
# '{7,9}' spends its rows on counts no subject this short can reach.
QUANTIFIERS = ("*", "+", "?", "{m}", "{m,}", "{m,n}")
QUANTIFIER_WEIGHTS = (25, 25, 15, 10, 10, 15)

# The bounds a '{m,n}' draws from. Kept at or below the subject length, so a bounded repeat has a
# real chance of both reaching and exceeding its minimum - which is where an off-by-one at either
# boundary shows up.
QUANT_BOUNDS = (0, 1, 2, 3)

# How a quantified fragment is wrapped. 'group' is the one that makes the captures observable: a
# repeated capture group keeps one capture per iteration, and 'Group.Captures' has to agree with
# 'match.spans(n)' row for row. 'nested' is a quantifier on a group that itself holds a quantifier,
# which is the only way to reach RE_STATUS_INNER and the BODY_END / MATCH_BODY / MATCH_TAIL markers
# with a repeat inside a repeat. 'empty-body' is the '(a?)*' shape whose body can match nothing at
# all, where the position guards are the only thing that terminates the match.
QUANT_SHAPES = ("atom", "group", "noncapture", "nested", "empty-body")
QUANT_SHAPE_WEIGHTS = (34, 26, 12, 18, 10)

# Short, because a quantifier already explores many lengths at each start position and a long subject
# multiplies that by the number of start positions a search tries. The astral alphabet is here so a
# repeat count reported in codepoints rather than UTF-16 code units diverges from the first wave.
QUANT_SUBJECT_ALPHABETS = ("ab", "abx", "ab \t", "ab\U0001f600\U0001d518")

MAX_QUANT_SUBJECT_LENGTH = 6
MAX_QUANT_FRAGMENTS = 2

# How deep a generated pattern nests one quantified group inside another. Two reaches everything that
# matters - a repeat inside a repeat, so RE_STATUS_INNER and the BODY_END / MATCH_BODY / MATCH_TAIL
# markers, and a capture group closing inside an outer iteration - and the cap is a hard one because
# a quantifier nested four deep is how a generator accidentally emits a catastrophic pattern and
# stalls the wave.
MAX_QUANT_DEPTH = 2


def _quantifier(rng: random.Random) -> str:
    """One quantifier suffix, greedy or lazy.

    Never possessive ('*+', Phase 4) and never doubled ('a*?*', which upstream rejects outright with
    "multiple repeat"), because a generator that emits either produces rows that say nothing about
    this slice: the first is `unsupported`, the second is a parse error both sides agree on.
    """
    form = rng.choices(QUANTIFIERS, weights=QUANTIFIER_WEIGHTS)[0]

    if form == "{m}":
        form = "{%d}" % rng.choice(QUANT_BOUNDS)
    elif form == "{m,}":
        form = "{%d,}" % rng.choice(QUANT_BOUNDS)
    elif form == "{m,n}":
        low = rng.choice(QUANT_BOUNDS)
        form = "{%d,%d}" % (low, low + rng.randrange(3))

    return form + ("?" if rng.random() < 0.35 else "")


def _quant_fragment(rng: random.Random, depth: int) -> str:
    shape = rng.choices(QUANT_SHAPES, weights=QUANT_SHAPE_WEIGHTS)[0]
    if depth >= MAX_QUANT_DEPTH:
        shape = "atom"
    elif depth > 0 and shape in ("nested", "empty-body"):
        shape = "atom"

    if shape == "atom":
        return rng.choice(QUANT_ATOMS) + _quantifier(rng)

    if shape == "empty-body":
        # '(a?)*': the body can match nothing, so the guards are what stops the repeat re-entering
        # its own body at the same position for ever. Upstream reports the empty final iteration as a
        # capture of its own, which is what makes this observable rather than merely terminating.
        return "(%s?)%s" % (rng.choice(QUANT_ATOMS), rng.choice(("*", "*?", "+", "+?")))

    inner = "".join(_quant_fragment(rng, depth + 1) for _ in range(rng.randrange(1, 3)))

    if shape == "group":
        return "(%s)%s" % (inner, _quantifier(rng))
    if shape == "noncapture":
        return "(?:%s)%s" % (inner, _quantifier(rng))

    # 'nested': a quantifier on a group holding a quantifier.
    return "(%s%s)%s" % (rng.choice(QUANT_ATOMS), _quantifier(rng), _quantifier(rng))


def _generate_quantifiers(rng: random.Random, count: int):
    """S19's generator: greedy and lazy repeats over atoms, groups and nested repeats.

    The subject is drawn independently of the pattern rather than sliced out of it: a quantifier
    already matches a range of lengths, so an independent draw gives a healthy mix without the
    substring trick the literal generators use. Measured over 300 rows of seed 1: 174 match, 126 do
    not, 58 with an astral subject, no parse errors at all, and the slowest row took 0.05ms upstream.
    """
    for i in range(count):
        alphabet = QUANT_SUBJECT_ALPHABETS[i % len(QUANT_SUBJECT_ALPHABETS)]
        subject = "".join(
            rng.choice(alphabet) for _ in range(rng.randrange(MAX_QUANT_SUBJECT_LENGTH + 1))
        )

        pattern = "".join(
            _quant_fragment(rng, 0) for _ in range(rng.randrange(1, MAX_QUANT_FRAGMENTS + 1))
        )

        yield {
            "generator": "quantifiers",
            "pattern": pattern,
            "flags": 0,
            "namedLists": {},
            "subject": subject,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }


def _generate(name: str, rng: random.Random, count: int):
    """Yields ``count`` unrecorded rows from the named generator.

    Deliberately simple: S16 - literals, plain anchors and the engine spine - is their first
    customer, and a generator that emits constructs no slice has ported yet produces a wave that is
    all ``unsupported`` and tells nobody anything. Later slices add their own.
    """
    if name not in GENERATORS:
        raise SystemExit(f"unknown generator {name!r}; expected one of {', '.join(GENERATORS)}")

    if name == "classes":
        yield from _generate_classes(rng, count)
        return

    if name == "groups":
        yield from _generate_groups(rng, count)
        return

    if name == "quantifiers":
        yield from _generate_quantifiers(rng, count)
        return

    dotted = name == "literal-dot"
    anchored = name == "anchors"

    for i in range(count):
        alphabet = ALPHABETS[i % len(ALPHABETS)]
        subject = "".join(rng.choice(alphabet) for _ in range(rng.randrange(MAX_SUBJECT_LENGTH + 1)))

        if anchored:
            # Put line breaks in, so '^' and '$' under MULTILINE have interior positions to match at
            # and the CR/LF rule is reachable. Inserted rather than drawn from the alphabet so a row
            # can hold several.
            for _ in range(rng.randrange(3)):
                at = rng.randrange(len(subject) + 1)
                subject = subject[:at] + rng.choice(LINE_BREAKS) + subject[at:]

        if subject and rng.random() < SUBSTRING_PROBABILITY:
            # A substring of the subject, so most rows match. Sliced by codepoint, so an astral
            # character is never cut in half and the pattern holds no lone surrogate.
            start = rng.randrange(len(subject))
            end = rng.randrange(start, min(len(subject), start + MAX_PATTERN_LENGTH) + 1)
            pattern = subject[start:end]
        else:
            # An independent string, so some rows genuinely do not match. Without these the wave
            # would only ever exercise the success path.
            pattern = "".join(rng.choice(alphabet) for _ in range(rng.randrange(MAX_PATTERN_LENGTH + 1)))

        if dotted:
            pattern = "".join("." if rng.random() < DOT_PROBABILITY else c for c in pattern)

        flags = 0
        if anchored:
            # A line break inside the literal would make the pattern's own text the thing under
            # test rather than the anchor, so escape whatever the substring slice picked up.
            pattern = re.sub(r"[\n\r ]", lambda m: "\\" + m.group(), pattern)
            # Drawn from the seeded stream, not from `i`. Indexing the affix table, the alphabet
            # table and the flag all by `i` aliased them: ANCHOR_AFFIXES has an even length, so
            # '^' only ever landed on an even index and was therefore never recorded with
            # MULTILINE, which left START_OF_LINE and START_OF_LINE_U - two of the four opcodes
            # this generator exists to cover - untested, and put every astral subject on the
            # MULTILINE side. Found by the S16 blind review; measured before the fix over 500 rows
            # at 0 of 126 '^' rows with MULTILINE and 0 of 166 astral rows without it.
            prefix, suffix = rng.choice(ANCHOR_AFFIXES)
            pattern = prefix + pattern + suffix
            flags = MULTILINE if rng.random() < 0.5 else 0

        yield {
            "generator": name,
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }


def _read_rows(path: Path):
    """Reads explicit rows from a JSONL file - the minimisation path."""
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        line = line.strip()
        if not line or line.startswith("//"):
            continue
        try:
            row = json.loads(line)
        except json.JSONDecodeError as e:
            raise SystemExit(f"{path}:{number}: not valid JSON: {e}") from e
        if row.get("kind") == "header":
            # So a recorded wave file can be fed straight back in after editing.
            continue
        row.setdefault("generator", "rows")
        row.setdefault("flags", 0)
        row.setdefault("namedLists", {})
        row.setdefault("operation", "search")
        yield row


# --------------------------------------------------------------------------------------------
# Writing the wave
# --------------------------------------------------------------------------------------------


def write(header: dict, rows: list[dict], path: Path) -> None:
    """One JSON object per line: the header, then the rows.

    ASCII only, so an astral subject is written as its surrogate pair escape and the bytes are the
    same on every platform; ``newline=''`` so Windows does not turn the file into CRLF and break a
    byte-for-byte comparison between two runs.
    """
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="ascii", newline="") as f:
        f.write(json.dumps(header, sort_keys=False) + "\n")
        for row in rows:
            f.write(json.dumps(row, sort_keys=False) + "\n")


def record(generators: list[str], seed: int, count: int, rows_path: Path | None) -> tuple[dict, list[dict]]:
    import regex

    version_source = _check_version(regex.__version__)

    if rows_path is not None:
        waves = [{"rows": rows_path.name}]
        unrecorded = list(_read_rows(rows_path))
    else:
        waves = [{"generator": name, "seed": seed, "count": count} for name in generators]
        # A generator's own stream, keyed by the seed *and* its name, not one stream shared by all
        # of them. The header records a seed per generator, so that seed has to reproduce that
        # generator's rows on its own - with a shared stream it only reproduced them when the whole
        # run was repeated with the same generators in the same order, which is not what a row
        # being minimised from a report gets re-recorded with.
        unrecorded = [
            row
            for name in generators
            for row in _generate(name, random.Random(f"{seed}:{name}"), count)
        ]

    recorded = [_record_row(regex, row) for row in unrecorded]

    header = {
        "kind": "header",
        "comment": "GENERATED - a differential oracle wave. Written by tools/record-oracle.py.",
        "regexVersion": regex.__version__,
        "versionSource": version_source,
        "pinnedVersion": _pinned_version(),
        "upstreamCommit": _upstream_commit(),
        # Upstream's DEFAULT_VERSION at record time. The consumer checks it against the port's
        # own default, because a pattern with no explicit V0/V1 resolves against it.
        "defaultVersion": regex.DEFAULT_VERSION,
        "waves": waves,
        "rowCount": len(recorded),
    }
    return header, recorded


# --------------------------------------------------------------------------------------------
# Determinism
# --------------------------------------------------------------------------------------------


_DETERMINISM_SEED = 20260831


def _verify_determinism() -> int:
    """Records the same seed twice in fresh interpreters under different hash seeds.

    The corpus recorder needs four runs because the order it is defending against leaks out of
    set iteration over parse nodes, which no seed perturbs. This recorder has no such exposure -
    every row comes from an explicitly seeded ``random.Random`` and every named list is sorted -
    so two runs are enough to catch the mistake this check is actually for: a timestamp, a path,
    a dict built from an unsorted set, or anything else non-reproducible reaching the file.
    """
    with tempfile.TemporaryDirectory() as tmp:
        outputs = []
        for run in (0, 1):
            env = dict(os.environ, PYTHONHASHSEED=str(run * 7919 + 1), PYTHONDONTWRITEBYTECODE="1")
            out = Path(tmp) / f"wave-{run}.jsonl"
            subprocess.run(
                [sys.executable, str(Path(__file__).resolve()),
                 "--seed", str(_DETERMINISM_SEED), "--count", "40", "--output", str(out)],
                env=env, check=True, stdout=subprocess.DEVNULL)
            outputs.append(out)

        if not filecmp.cmp(outputs[0], outputs[1], shallow=False):
            print("the recorder is not deterministic: two runs of the same seed differed",
                  file=sys.stderr)
            return 1

    print(f"deterministic: two runs of seed {_DETERMINISM_SEED} produced byte-identical waves")
    return 0


def _self_check() -> int:
    """Requires the recorder's four guards to fire, all four raised by the S14 blind review.

    Each is a silent wrong answer rather than a crash if it regresses, which is why they are
    checked rather than trusted: the first two would record a failure of the harness as upstream's
    answer about a pattern, the third would print a seed in the header that does not reproduce the
    rows it is printed beside, and the fourth would record every astral row at the wrong index.
    """
    failures = []

    def row(**overrides):
        base = {"pattern": "a", "flags": 0, "namedLists": {}, "subject": "a", "operation": "search"}
        return {**base, **overrides}

    import regex

    for name, unrecordable, why in (
        ("a named list colliding with regex.compile's own parameters",
         row(pattern="\\L<flags>", namedLists={"flags": ["ab"]}), "collides with a parameter"),
        # 2000 nested groups against CPython's own recursion limit. Not a pattern any generator
        # emits today; the guard is for the generators later slices add.
        ("an interpreter limit rather than a judgement about the pattern",
         row(pattern="(" * 2000 + "a" + ")" * 2000), "limit of the interpreter"),
    ):
        try:
            _record_row(regex, unrecordable)
        except SystemExit as e:
            if why in str(e):
                continue
            failures.append(f"{name}: rejected with the wrong message: {e}")
        except Exception as e:  # noqa: BLE001 - any other exception is itself the failure
            failures.append(f"{name}: raised {type(e).__name__} instead of refusing the row")
        else:
            failures.append(f"{name}: was recorded as if it were upstream's answer")

    # The seed the header prints beside a generator must reproduce that generator's rows on its
    # own, whatever else was recorded alongside it. Recorded with a *second* generator present,
    # because that is the only arrangement in which a shared stream is observable: with one
    # generator, a stream keyed on the run and a stream keyed on the generator are the same stream,
    # so the single-generator form of this check passed a deliberately shared stream.
    alone = [r["pattern"] for r in _generate("literal-dot", random.Random("7:literal-dot"), 20)]
    together = [
        r["pattern"]
        for r in record(["literals", "literal-dot"], 7, 20, None)[1]
        if r["generator"] == "literal-dot"
    ]
    if alone != together:
        failures.append("a generator's rows depend on which other generators ran with it")

    # The index translation, which nothing else checks: while the engine is unported every real row
    # is `unsupported`, so a recorder that passed Python's codepoint indices straight through would
    # record wrong answers silently rather than crash. U+1D518 is one codepoint and two UTF-16 code
    # units, so 'a<frak-U>b' is 3 codepoints and 4 code units, and the last offset - the exclusive
    # end of a span that reaches the end of the subject - has to be the total, not zero.
    subject = "a\U0001d518b"
    offsets = _utf16_offsets(subject)
    if offsets != [0, 1, 3, 4]:
        failures.append(f"_utf16_offsets({subject!r}) is {offsets}, expected [0, 1, 3, 4]")
    else:
        for span, expected in (((0, 3), [0, 4]), ((1, 2), [1, 2]), ((2, 3), [3, 1]), ((3, 3), [4, 0])):
            got = _to_index_length(offsets, span)
            if got != expected:
                failures.append(f"codepoint span {span} translates to {got}, expected {expected}")

    for failure in failures:
        print("self-check: " + failure, file=sys.stderr)
    if failures:
        return 1

    print(
        "self-check: the reserved-name, interpreter-limit, per-generator-seed and index-translation "
        "guards all fire"
    )
    return 0


# --------------------------------------------------------------------------------------------


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--generator", default=",".join(GENERATORS),
                        help=f"comma-separated generator names (default: {','.join(GENERATORS)})")
    parser.add_argument("--seed", type=int, default=None,
                        help="the generator seed; a random one is chosen and recorded if omitted")
    parser.add_argument("--count", type=int, default=300, help="rows per generator (default: 300)")
    parser.add_argument("--rows", type=Path, default=None,
                        help="record these explicit rows (JSONL) instead of generating any")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--verify-determinism", action="store_true",
                        help="record one seed twice in fresh interpreters and require identical bytes")
    parser.add_argument("--self-check", action="store_true",
                        help="require the recorder's four guards to fire")
    args = parser.parse_args(argv)

    if args.verify_determinism:
        return _verify_determinism()

    if args.self_check:
        return _self_check()

    # Random by default and recorded in the header: the point of a generative tester is that
    # today's run is not yesterday's, and the header is what makes a divergence reproducible.
    seed = args.seed if args.seed is not None else random.randrange(1 << 30)
    generators = [name.strip() for name in args.generator.split(",") if name.strip()]

    header, rows = record(generators, seed, args.count, args.rows)
    write(header, rows, args.output)

    kinds: dict[str, int] = {}
    for row in rows:
        kind = row["outcome"]["kind"]
        kinds[kind] = kinds.get(kind, 0) + 1
    astral = sum(1 for row in rows if any(ord(c) > 0xFFFF for c in row["subject"]))

    print(f"wrote {args.output}: {len(rows)} rows, seed {seed}, "
          f"regex {header['regexVersion']} ({header['versionSource']})")
    print("  " + ", ".join(f"{count} {kind}" for kind, count in sorted(kinds.items()))
          + f", {astral} with an astral subject")
    return 0


if __name__ == "__main__":
    os.environ.setdefault("PYTHONDONTWRITEBYTECODE", "1")
    sys.exit(main())
