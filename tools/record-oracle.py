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

# S24's operations. A substitution row carries two more fields - `template` and `count` - and its
# answer is a (string, count) pair rather than a span, so it is a second row shape rather than a
# fourth entry in OPERATIONS: everything that reads a row has to know which shape it is holding.
# `count` is upstream's own convention, where 0 means "no limit"; the consumer translates it.
SUB_OPERATIONS = ("sub", "subf")

# S25's operations, and a third row shape: the answer is a *sequence*, so comparing one match is not
# enough - a scan that finds the right matches in the wrong order, or stops one match early, agrees
# on every individual match. `split` carries a limit in the same field and the same convention as a
# substitution's `count`, where 0 means "no limit"; the consumer translates it.
ITER_OPERATIONS = ("finditer", "finditer-overlapped", "split")
LIMIT_OPERATIONS = SUB_OPERATIONS + ("split",)
ALL_OPERATIONS = OPERATIONS + SUB_OPERATIONS + ITER_OPERATIONS

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
    if operation not in ALL_OPERATIONS:
        raise SystemExit(f"unknown operation {operation!r}; expected one of {ALL_OPERATIONS}")

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
    if operation in SUB_OPERATIONS:
        if "template" not in row:
            raise SystemExit(
                f"operation {operation!r} needs a 'template' and this row has none (pattern "
                f"{pattern!r}). A substitution row's answer is the replaced string, so recording "
                "one without a template would file the untouched subject as upstream's answer."
            )
        recorded["template"] = row["template"]

    if operation in LIMIT_OPERATIONS:
        recorded["count"] = int(row.get("count", 0))

    def failed(e: Exception, while_matching: bool) -> dict:
        """The recorded answer when upstream raised.

        ``whileMatching`` is recorded, not inferred, because upstream answers the *same* bad
        template two different ways depending on when it notices: `regex.sub('x', r'\\g<bad', 'z')`
        raises while compiling the template and `regex.sub('x', r'\\1', 'x')` raises while
        substituting, and both are the port's answer too. Without the flag the consumer's rule
        that an exception raised while matching cannot agree with a compile-time rejection - which
        exists to stop an index slip in our own engine being filed as parity - made every
        invalid-group-reference row a false divergence. Measured 2026-09-01.
        """
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
            "whileMatching": while_matching,
        }
        return recorded

    try:
        compiled = regex.compile(pattern, flags, **named_lists)
    except Exception as e:  # noqa: BLE001 - the exception *is* the recorded answer
        return failed(e, while_matching=False)

    if operation in SUB_OPERATIONS:
        method = compiled.subn if operation == "sub" else compiled.subfn
        try:
            text, made = method(recorded["template"], subject, count=recorded["count"])
        except Exception as e:  # noqa: BLE001
            return failed(e, while_matching=True)

        recorded["codepointSpan"] = None
        recorded["outcome"] = {"kind": "sub", "text": text, "count": made}
        return recorded

    if operation in ITER_OPERATIONS:
        try:
            if operation == "split":
                parts = compiled.split(subject, maxsplit=recorded["count"])
            else:
                overlapped = operation == "finditer-overlapped"
                found = list(compiled.finditer(subject, overlapped=overlapped))
        except Exception as e:  # noqa: BLE001
            return failed(e, while_matching=True)

        recorded["codepointSpan"] = None
        if operation == "split":
            # None stays None: upstream puts it where a capturing group took no part in a match,
            # which our `string?[]` spells as null. Losing the distinction between that and an
            # empty string is exactly the mistake Regex.Split makes.
            recorded["outcome"] = {"kind": "split", "parts": parts}
        else:
            offsets = _utf16_offsets(subject)
            recorded["outcome"] = {
                "kind": "matches",
                "matches": [
                    # The untranslated codepoint span per match, for the same reason the top-level
                    # one exists: it makes the recorder's index translation visible in the file
                    # rather than merely trusted. The consumer never reads it.
                    dict(_describe_match(compiled, m, offsets), codepointSpan=list(m.span(0)))
                    for m in found
                ],
            }
        return recorded

    try:
        match = getattr(compiled, operation)(subject)
    except Exception as e:  # noqa: BLE001
        return failed(e, while_matching=True)

    if match is None:
        recorded["codepointSpan"] = None
        recorded["outcome"] = {"kind": "nomatch"}
        return recorded

    recorded["codepointSpan"] = list(match.span(0))
    recorded["outcome"] = {"kind": "match", **_describe_match(compiled, match, _utf16_offsets(subject))}
    return recorded


def _describe_match(compiled, match, offsets: list[int]) -> dict:
    """One match's groups and its two last-group fields, in UTF-16 ``[Index, Length]``.

    Shared by the single-match operations and by ``finditer``, whose answer is a list of these, so
    the two shapes cannot drift apart.
    """
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

    return {
        "groups": groups,
        # Neither is derivable from the groups: 'lastindex' is the group that *closed* last, so
        # regex.match('((a))', 'a').lastindex is 1 even though groups 1 and 2 both succeed with the
        # same span, and 'lastgroup' names the last *named* group even when an unnamed one succeeded
        # later. None becomes -1 and null, which is what Match.LastGroupNumber and
        # Match.LastGroupName report.
        "lastIndex": -1 if match.lastindex is None else match.lastindex,
        "lastGroup": match.lastgroup,
    }


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


GENERATORS = (
    "literals",
    "literal-dot",
    "anchors",
    "classes",
    "groups",
    "quantifiers",
    "boundaries",
    "backrefs",
    "case-folding",
    "reverse",
    "substitution",
    "iteration",
)

# The zero-width assertions the S16 spine implements, as (prefix, suffix) pairs wrapped round a
# literal. Every one is a plain anchor: the word and grapheme boundaries S20 added have their own
# generator below, because they need subjects chosen for their word-break classes rather than for
# their line breaks. `\G` is upstream's SEARCH_ANCHOR, which is only interesting when the operation
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


# S20's generator.
#
# The zero-width predicates that ask about the characters either side of a position rather than
# about the character at it: `\b` / `\B` (BOUNDARY), `\m` / `\M` (START_OF_WORD / END_OF_WORD),
# their `(?w)` forms (DEFAULT_BOUNDARY, DEFAULT_START_OF_WORD, DEFAULT_END_OF_WORD), `\X`
# (GRAPHEME_BOUNDARY inside an atomic group) and `\K` (KEEP).
BOUNDARY_AFFIXES = (
    (r"\b", ""),
    ("", r"\b"),
    (r"\b", r"\b"),
    (r"\B", ""),
    ("", r"\B"),
    (r"\B", r"\B"),
    (r"\m", ""),
    ("", r"\M"),
    (r"\m", r"\M"),
    ("", ""),
)

# The three encodings the word predicates dispatch on. `(?w)` is the only one that reaches
# `unicode_at_default_boundary` and its WB rules at all; `(?a)` is the only one that reaches
# `ascii_word_left` / `ascii_word_right`, whose whole difference is that everything above U+007F is
# answered as unassigned. Written inline rather than as a flags integer so both sides of the oracle
# read the identical row - FuzzyRegexOptions has no WORD or ASCII member (S24 territory).
BOUNDARY_FLAG_PREFIXES = ("", "(?a)", "(?w)", "(?V1)", "(?V1w)", "(?aw)")

# The literal an affix pair is wrapped round. Kept to single characters and short runs so the row is
# about the predicate rather than about whether the literal happened to be present.
BOUNDARY_LITERALS = ("a", "z", "0", "'", "b", "ab", "a0", "א", "カ", "क", "é")

# Subject material, cycled band by band so no wave is all-ASCII. Each band is chosen for the
# word-break or grapheme-cluster classes it contains, because those classes are what the rules
# switch on:
#   0. ASCII letters, digits and the MidLetter / MidNumLet / MidNum punctuation of WB6-WB12.
#   1. Latin-1, a combining mark (GB9, and Extend for WB4), the dotted and dotless I of WB5a.
#   2. Hebrew_Letter (WB7a-WB7c), Katakana (WB13), Devanagari with a virama (GB9c), an Arabic-Indic
#      digit, ExtendNumLet (WB13a/WB13b) and WSegSpace (WB3d).
#   3. Astral: regional indicators (WB15/WB16 and GB12/GB13), a ZWJ, extended pictographics
#      (WB3c, GB11) and an emoji modifier - every one of which is two UTF-16 code units, so a rule
#      that counted code units rather than characters answers differently here.
BOUNDARY_SUBJECT_ALPHABETS = (
    "abz09 '.,-_",
    "éÀàİı '",
    "אב\"'カタक्ष٠_　",
    "\U0001f1ec\U0001f1e7\U0001f600‍\U0001f469\U0001f3fb\U0001d518",
)

# Line breaks, inserted rather than drawn, so a row can hold several and so CR/LF lands as a pair -
# which is WB3 and GB3, the one place both rule sets refuse to break between two characters they
# would each break around on their own.
BOUNDARY_LINE_BREAKS = ("\n", "\r", "\r\n", "", " ")

# Whole clusters, inserted the same way, because the rules that join two characters need those two
# characters side by side and a per-character draw rarely produces one. A negative control measured
# this: seeding a fault into the regional-indicator count of WB15/GB13 - the one rule whose port had
# to count characters rather than subtract UTF-16 indices - diverged on 0 rows of 600 without these
# and on 2 with them (seed 7). Each entry is something upstream refuses to break inside, or a rule's
# own worked example.
BOUNDARY_CLUSTERS = (
    "\U0001f1ec\U0001f1e7",  # A regional-indicator pair: GB12/GB13 and WB15/WB16.
    "\U0001f1ec\U0001f1e7\U0001f1eb\U0001f1f7",  # Two flags, so the run length is what decides.
    "\U0001f469‍\U0001f466",  # An emoji ZWJ sequence: GB11 and WB3c.
    "à",  # A base letter and a combining mark: GB9, and Extend for WB4.
    "क्ष",  # Devanagari consonant, virama, consonant: GB9c.
    "can't",  # WB6/WB7 across an apostrophe, which is what `(?w)` is for.
    "3.2",  # WB11/WB12 across a decimal point.
)

BOUNDARY_CLUSTER_PROBABILITY = 0.4

MAX_BOUNDARY_SUBJECT_LENGTH = 7

# How a row's pattern is shaped, and how often.
#   'affix':    a literal wrapped in one of BOUNDARY_AFFIXES - the word predicates.
#   'infix':    an assertion *between* two literals, so the position under test is interior.
#   'keep':     '\K' between two literals, which moves the reported match start.
#   'grapheme': '\X', repeated or quantified - GRAPHEME_BOUNDARY inside an atomic group.
#
# 'infix' exists because of a negative control that did not fire. WB1/WB2 answers "there is a
# boundary here" for the two ends of the subject before any other rule is consulted, and an
# affix-shaped pattern searched forwards mostly matches at position 0, so a fault seeded into WB5
# was invisible over 600 rows: 48 of them held `(?w)` and `\b`, and not one of them turned on a rule
# past WB2. With 'infix' at this weight the same fault diverges on 7 rows of 600 (seed 7).
BOUNDARY_SHAPES = ("affix", "infix", "keep", "grapheme")
BOUNDARY_SHAPE_WEIGHTS = (4, 4, 2, 3)

# The zero-width assertions worth putting between two literals. `\K` is not here - it has its own
# shape, and it is not an assertion but a marker.
BOUNDARY_INFIXES = (r"\b", r"\B", r"\m", r"\M")

# The last three put the `\K` inside an alternative or an optional group, so a branch that fails
# after the marker has to backtrack past it and put the reported match start back. That branch was
# also found by a control that did not fire: removing the restore left the first five shapes
# agreeing on all 600 rows, because none of them can fail after the `\K`.
BOUNDARY_KEEP_SHAPES = (
    r"%s\K%s",
    r"(%s\K%s)",
    r"%s\K(%s)",
    r"(?:%s)\K%s",
    r"%s+\K%s",
    r"%s\K%s%s|%s%s",
    r"(?:%s\K%s|%s)%s",
    r"(%s\K%s)?%s%s",
)
BOUNDARY_GRAPHEME_SHAPES = (r"\X", r"\X\X", r"\X+", r"\X*", r"\X+?", r"\X{2}", r"\X%s", r"%s\X")


def _generate_boundaries(rng: random.Random, count: int):
    """S20's generator: word, default-word and grapheme boundaries, and the keep marker.

    The subject is drawn independently of the pattern, as the class and quantifier generators do: a
    boundary assertion is zero-width, so slicing the whole pattern out of the subject would say
    nothing extra. Measured over 200 rows of seed 1: 59 match, 141 do not, 49 with an astral
    subject, no parse errors, and every one of `\\b`, `\\B`, `\\m`, `\\M`, `\\K`, `\\X`, `(?a)` and
    `(?w)` recorded at least 17 times.
    """
    for i in range(count):
        alphabet = BOUNDARY_SUBJECT_ALPHABETS[i % len(BOUNDARY_SUBJECT_ALPHABETS)]
        subject = "".join(
            rng.choice(alphabet) for _ in range(rng.randrange(1, MAX_BOUNDARY_SUBJECT_LENGTH + 1))
        )

        for _ in range(rng.randrange(3)):
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + rng.choice(BOUNDARY_LINE_BREAKS) + subject[at:]

        if rng.random() < BOUNDARY_CLUSTER_PROBABILITY:
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + rng.choice(BOUNDARY_CLUSTERS) + subject[at:]

        # A literal the subject actually contains most of the time, so the row turns on where the
        # boundary is rather than on whether the literal was there at all. Measured over 200 rows of
        # seed 1: drawing every literal from the table gave 46 matching rows, this gives 59.
        def literal():
            candidates = [c for c in subject if c not in "\r\n"]
            if candidates and rng.random() < SUBSTRING_PROBABILITY:
                return re.escape(rng.choice(candidates))
            return rng.choice(BOUNDARY_LITERALS)

        shape = rng.choices(BOUNDARY_SHAPES, weights=BOUNDARY_SHAPE_WEIGHTS)[0]

        if shape == "affix":
            prefix, suffix = rng.choice(BOUNDARY_AFFIXES)
            pattern = prefix + literal() + suffix
        elif shape == "infix":
            # Two characters the subject holds side by side, so the assertion is asked about a
            # position the pattern can actually reach. Drawn independently the pattern usually
            # cannot match at all, and a row that never reaches the assertion tests nothing.
            pairs = [
                subject[at : at + 2]
                for at in range(len(subject) - 1)
                if "\r" not in subject[at : at + 2] and "\n" not in subject[at : at + 2]
            ]
            if pairs and rng.random() < SUBSTRING_PROBABILITY:
                pair = rng.choice(pairs)
                left, right = re.escape(pair[0]), re.escape(pair[1])
            else:
                left, right = literal(), literal()

            pattern = left + rng.choice(BOUNDARY_INFIXES) + right
        elif shape == "keep":
            form = rng.choice(BOUNDARY_KEEP_SHAPES)
            pattern = form % tuple(literal() for _ in range(form.count("%s")))
        else:
            form = rng.choice(BOUNDARY_GRAPHEME_SHAPES)
            pattern = form % literal() if "%s" in form else form

        # `\m` and `\M` are mrab-regex extensions with no V0/V1 difference, and `(?w)` only changes
        # which opcode `\b` compiles to, so every prefix is legal in front of every shape.
        pattern = rng.choice(BOUNDARY_FLAG_PREFIXES) + pattern

        yield {
            "generator": "boundaries",
            "pattern": pattern,
            "flags": 0,
            "namedLists": {},
            "subject": subject,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }


# --------------------------------------------------------------------------------------------
# S21's generator: backreferences and group-existence conditionals
# --------------------------------------------------------------------------------------------

# What a group captures, and what a conditional's branches hold. One character each, as in the
# `groups` generator: the row is about the reference, not about the atom it reads back.
BACKREF_ATOMS = ("a", "b", "c", "x", ".", "[ab]", r"\w")

# The subjects. Small alphabets, because a backreference can only match when the subject repeats
# something, and 'abcde' at length 6 almost never repeats a two-character run. The astral alphabet
# is here for the reason it is in every other generator: a span reported in codepoints rather than
# UTF-16 code units has to show up as a divergence, and a reference is the one construct that walks
# the *subject* twice, so a stepping bug on the second walk shows up here and nowhere else.
BACKREF_SUBJECT_ALPHABETS = ("ab", "abc", "aabbx", "ab\U0001f600\U0001d518")

MAX_BACKREF_SUBJECT_LENGTH = 8

# How a group gets defined. Every shape holds exactly one capture group, which is what makes the
# numbering below a simple counter. 'optional' is how a reference reaches a group that did not
# match at all (upstream: regex.search(r'(a)?\1', 'a') is None); 'empty-alt' is how it reaches one
# that matched empty, which succeeds; 'repeated' changes the span the reference reads on every
# iteration.
BACKREF_DEFINE_SHAPES = ("plain", "named", "optional", "empty-alt", "alt", "repeated")
BACKREF_DEFINE_WEIGHTS = (24, 16, 16, 14, 16, 14)

# How a reference is spelled. All four compile to the same REF_GROUP node, so this is really a test
# of the parser's four spellings reaching the same opcode - cheap to carry, and it is what the
# ported suite spends five of its rows on.
BACKREF_REF_FORMS = ("number", "g-number", "g-named", "p-named")
BACKREF_REF_WEIGHTS = (72, 28, 0, 0)
BACKREF_REF_NAMED_WEIGHTS = (20, 10, 35, 35)

# What is wrapped round a reference. 'repeat' and 'count' are the shapes where the reference is
# visited more than once, so 'stringPos' has to be back at -1 by the time it is; 'in-group' puts it
# inside a capture of its own, which is the shape the ported suite's '^(\|)?([^()]+)\1$' rows use.
BACKREF_REF_WRAPS = ("bare", "optional", "repeat", "count", "in-group")
BACKREF_REF_WRAP_WEIGHTS = (44, 16, 12, 12, 16)

# The conditional forms. 'yes-only' and 'empty-yes' are the two half-empty shapes: upstream builds
# both from the same GROUP_EXISTS with one exit pointing straight at the join
# (NodeCompiler.BuildGroupExists), so they are where an exit wired to the wrong branch shows up.
BACKREF_COND_SHAPES = ("both", "yes-only", "empty-yes", "in-group")
BACKREF_COND_WEIGHTS = (40, 24, 16, 20)

BACKREF_PIECE_KINDS = ("define", "ref", "cond", "atom", "ref-in-repeat", "repeated-then-ref")
BACKREF_PIECE_WEIGHTS = (28, 24, 18, 8, 12, 10)

# Two or three pieces. The match rate falls off sharply as this rises - 28% at two pieces, 23% at
# three, 20% at four, over 600 rows of `random.Random("1:backrefs")`, which is how `_record_wave`
# seeds a generator - because every extra piece is another thing an eight-character subject has to
# satisfy at once. Three keeps the structural variety (a definition, a reference and a conditional
# in one pattern) at a match rate between the `groups` generator's 30% and the `classes`
# generator's 23% on the same measurement.
MAX_BACKREF_PIECES = 3

# How often a row is prefixed with a conditional on a group defined *later* in the pattern. Legal
# upstream (regex.search(r'(?(1)a)(b)', 'b') spans (0, 1)) and the sharpest test of GROUP_EXISTS
# there is: the group does exist in the pattern and does match, but not yet, so the condition has to
# be false. An implementation that consulted the pattern's group table rather than the attempt's
# state takes the other branch on every one of these rows.
BACKREF_FORWARD_COND_PROBABILITY = 0.18

# How often the subject is built from doubled characters rather than drawn one at a time. See
# _generate_backrefs for what it is worth.
BACKREF_DOUBLED_SUBJECT_PROBABILITY = 0.5


def _backref_pattern(rng: random.Random) -> str:
    """One generated pattern: group definitions, references back to them, and conditionals.

    Built strictly left to right so the group numbers are known as they are handed out. Self
    references are deliberately absent: `(\\1xx|){3}` is "cannot refer to an open group" under V0
    (probed 2026-08-31), and it needs the V1 flag, whose own semantics are not ported yet.
    """
    counter = [0]
    # (number, name or None) for every capture group emitted so far.
    defined: list[tuple[int, str | None]] = []

    def atom() -> str:
        return rng.choice(BACKREF_ATOMS)

    def define() -> str:
        shape = rng.choices(BACKREF_DEFINE_SHAPES, weights=BACKREF_DEFINE_WEIGHTS)[0]
        counter[0] += 1
        number = counter[0]
        name = f"g{number}" if shape == "named" else None
        defined.append((number, name))

        if shape == "named":
            return f"(?P<{name}>{atom()})"
        if shape == "optional":
            return f"({atom()})?"
        if shape == "empty-alt":
            return f"({atom()}|)"
        if shape == "alt":
            return f"({atom()}|{atom()})"
        if shape == "repeated":
            return f"({atom()})+"
        return f"({atom()})"

    def reference() -> str:
        # A named group is only 16% of definitions, so a uniform draw referenced one rarely and the
        # two named spellings landed on 4 and 3 rows of 300. Preferring a named target when there is
        # one, and then weighting the spellings only a named target can use, takes them to 9 and 5.
        #
        # That is where the tuning stopped, deliberately. All four spellings compile to the same
        # REF_GROUP node, and the compile-parity corpus already proves that bit-exactly over 1,547
        # patterns, so matching cannot diverge by spelling - only parsing could, and parsing has its
        # own oracle. Buying the remaining coverage would mean distorting the mix of definitions to
        # serve a comparison that is already made elsewhere.
        named = [g for g in defined if g[1] is not None]
        number, name = rng.choice(named if named and rng.random() < 0.5 else defined)
        weights = BACKREF_REF_NAMED_WEIGHTS if name is not None else BACKREF_REF_WEIGHTS
        form = rng.choices(BACKREF_REF_FORMS, weights=weights)[0]

        if form == "g-number":
            ref = f"\\g<{number}>"
        elif form == "g-named":
            ref = f"\\g<{name}>"
        elif form == "p-named":
            ref = f"(?P={name})"
        else:
            ref = f"\\{number}"

        wrap = rng.choices(BACKREF_REF_WRAPS, weights=BACKREF_REF_WRAP_WEIGHTS)[0]
        if wrap == "optional":
            return f"(?:{ref})?"
        if wrap == "repeat":
            return f"(?:{ref})+"
        if wrap == "count":
            return f"(?:{ref}){{{rng.randrange(2, 4)}}}"
        if wrap == "in-group":
            counter[0] += 1
            defined.append((counter[0], None))
            return f"({ref})"
        return ref

    def conditional(number: int, name: str | None, *, may_capture: bool = True) -> str:
        shape = rng.choices(BACKREF_COND_SHAPES, weights=BACKREF_COND_WEIGHTS)[0]
        # 'in-group' takes the next group number, which is only right for a piece that is
        # *appended*. The forward conditional below is prepended, so its wrapper would really open
        # as group 1 and shift every number already handed out: the S21 blind review found 8 rows of
        # 300 where the condition and every numeric reference in the row pointed one group off, which
        # silently defeated the two repeat shapes. The wave stayed sound - both sides get the same
        # pattern - but the rows no longer tested what they were generated to test.
        if shape == "in-group" and not may_capture:
            shape = "both"
        # A named group can be tested by either spelling; an unnamed one only by number.
        condition = name if name is not None and rng.random() < 0.5 else str(number)

        if shape == "yes-only":
            return f"(?({condition}){atom()})"
        if shape == "empty-yes":
            return f"(?({condition})|{atom()})"
        if shape == "in-group":
            counter[0] += 1
            defined.append((counter[0], None))
            return f"((?({condition}){atom()}|{atom()}))"
        return f"(?({condition}){atom()}|{atom()})"

    pieces = []
    for _ in range(rng.randrange(2, MAX_BACKREF_PIECES + 1)):
        kind = rng.choices(BACKREF_PIECE_KINDS, weights=BACKREF_PIECE_WEIGHTS)[0]
        # Nothing to point at yet, so the first piece is always a definition.
        if not defined and kind in ("ref", "cond", "atom"):
            kind = "define"

        if kind == "define":
            pieces.append(define())
        elif kind == "ref":
            pieces.append(reference())
        elif kind == "cond":
            pieces.append(conditional(*rng.choice(defined)))
        elif kind == "ref-in-repeat":
            # Defined and referenced inside the same repeat body, so each iteration's reference
            # reads the span that iteration just captured, not the previous one's. This is the
            # shape that catches a reference reading the pre-iteration span.
            counter[0] += 1
            defined.append((counter[0], None))
            pieces.append(f"(?:({atom()})\\{counter[0]}){rng.choice(('+', '*', '{2}'))}")
        elif kind == "repeated-then-ref":
            # '(.)+\\1': the group captures once per iteration and the reference then reads the
            # last of them. Emitted as a unit because it is the only shape that tells the current
            # capture apart from the first one. Leaving it to chance was worth 6 divergences of 600
            # on the negative control for exactly that fault - read 'Captures[0]' instead of
            # 'Captures[Current]' in Matcher's REF_GROUP case - and adding it took that to 17. See
            # the S21 closing notes for how to re-run it. The atom is drawn from the wide ones, since
            # 'a' captures the same character every iteration and proves nothing here.
            counter[0] += 1
            defined.append((counter[0], None))
            wide = rng.choice((".", "[ab]", r"\w"))
            pieces.append(f"({wide}){rng.choice(('+', '{2,3}'))}\\{counter[0]}")
        else:
            pieces.append(atom())

    pattern = "".join(pieces)

    if rng.random() < BACKREF_FORWARD_COND_PROBABILITY:
        number, name = rng.choice(defined)
        pattern = conditional(number, name, may_capture=False) + pattern

    return pattern


def _generate_backrefs(rng: random.Random, count: int):
    """S21's generator: backreferences and group-existence conditionals.

    The subject is drawn independently of the pattern, as the `groups` generator does.

    Measured by `python tools/record-oracle.py --generator backrefs --count 300 --seed 1`: 76 match,
    224 do not, 63 with an astral subject, no parse errors. Grepping that wave, 119 rows hold a
    conditional, 75 an optional definition and 45 an empty alternative; matching the two repeat
    shapes needs a real regex rather than a substring, and gives 79 rows with a reference inside a
    repeat and 64 with a reference after a repeated definition. 13 rows hold a named reference,
    which the comment in `reference` explains and defends.
    """
    for i in range(count):
        alphabet = BACKREF_SUBJECT_ALPHABETS[i % len(BACKREF_SUBJECT_ALPHABETS)]
        length = rng.randrange(1, MAX_BACKREF_SUBJECT_LENGTH + 1)

        if rng.random() < BACKREF_DOUBLED_SUBJECT_PROBABILITY:
            # Built from doubled characters, so a reference has something to match. Worth a little
            # overall - 138 matching rows of 600 against 127 with this set to 0.0, seed 1 - and it
            # buys that where it is worth most: 'match' goes from 45 to 49 and 'fullmatch' from 7 to
            # 15, the two operations a reference constrains hardest, because the whole pattern has to
            # line up from position 0.
            subject = ""
            while len(subject) < length:
                subject += rng.choice(alphabet) * 2
            subject = subject[:length]
        else:
            subject = "".join(rng.choice(alphabet) for _ in range(length))

        yield {
            "generator": "backrefs",
            "pattern": _backref_pattern(rng),
            "flags": 0,
            "namedLists": {},
            "subject": subject,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }


# --------------------------------------------------------------------------------------------
# S22's generator: case-insensitive and full-casefold matching
# --------------------------------------------------------------------------------------------

# regex.IGNORECASE and regex.FULLCASE (verified 2026-08-31: `int(regex.I)` is 2 and `int(regex.F)`
# is 16384). regex.ASCII is 128, and selects upstream's other casing table - ascii_all_cases only
# knows A-Z and a-z, so an ASCII row is where a fold that should not happen would show up.
IGNORECASE = 2
FULLCASE = 16384
ASCII_FLAG = 128


def _expanding_characters() -> str:
    """Every character whose full case folding is longer than itself.

    Computed rather than listed, so it cannot rot against a Unicode bump: a hand-copied inventory
    would silently stop covering the characters a new Unicode version adds. ``str.casefold`` is
    CPython's full case folding, which is the same CaseFolding.txt data upstream's
    ``re_get_full_case_folding`` table is generated from; the two are not asserted to agree here -
    that is what the wave is for - it is only being used to *choose* interesting characters.

    Measured 2026-08-31 on CPython 3.14: 104 characters, which is the figure the S22 slice file
    quotes. The sharp-s family and the ff/fi/fl/ffi/ffl/long-s-t/st ligatures are all in it.
    """
    return "".join(chr(cp) for cp in range(0x110000) if len(chr(cp).casefold()) > 1)


# Folds that do not expand but still catch a simple-folding bug. Turkic dotted/dotless I, whose
# four variants upstream refuses to fold into each other at all (unicode_simple_case_fold, :1997);
# Cherokee, whose lowercase letters fold *upward* into the U+13A0 block rather than downward; and
# the singletons every case-folding implementation gets wrong at least once - long s, Kelvin sign,
# Angstrom sign, final sigma.
FOLD_TURKIC = "Iiİı"
FOLD_CHEROKEE = "ᎠᏰᏵꭰꮎᏸ"
FOLD_SINGLETONS = "ſKÅσςΣßẞ"

# Cased *astral* characters - Deseret, Osage, Vithkuqi, Warang Citi, Medefaidrin and Adlam - each
# an upper/lower pair that folds into the other. Every one is one codepoint and two UTF-16 code
# units, which is the split the whole port has to keep straight: an IGN comparison that advanced a
# code unit where upstream advances a codepoint agrees everywhere in the BMP and diverges here.
# Verified 2026-08-31 with str.casefold: each pair folds to its lowercase member.
FOLD_ASTRAL = (
    "\U00010400\U00010428"
    "\U000104B0\U000104D8"
    "\U00010570\U00010597"
    "\U000118A0\U000118C0"
    "\U00016E40\U00016E60"
    "\U0001E900\U0001E922"
)

# Plain ASCII, so the wave is not all exotica: an ordinary '(?i)abc' against 'ABC' is the common
# case and has to keep working. Kept short so the alphabet stays small enough that a random
# subject and a random pattern collide often.
FOLD_ASCII = "aAbBkKsS"

# The alphabets a row's subject is drawn from, rotated by row index so every wave covers all of
# them rather than whichever one the seed favoured. Each exotic set is spliced with ASCII because a
# subject of nothing but ligatures matches almost nothing, and a wave of non-matches says little.
#
# The expanding set appears twice in the rotation, so a third of rows draw from it rather than a
# fifth. It is the only one that reaches STRING_FLD and REF_GROUP_FLD's differing advance rates at
# all, and those need several things to line up at once - FULLCASE set, the right shape, the
# character repeated - so a fifth of the wave was not enough to reach them reliably.
FOLD_ALPHABETS = (
    FOLD_ASCII,
    FOLD_ASCII + FOLD_TURKIC,
    FOLD_ASCII + _expanding_characters(),
    FOLD_ASCII + FOLD_CHEROKEE + FOLD_SINGLETONS,
    FOLD_ASCII + FOLD_ASTRAL,
    FOLD_ASCII + _expanding_characters(),
)

MAX_FOLD_SUBJECT_LENGTH = 6
MAX_FOLD_PATTERN_LENGTH = 3

# How often the pattern's literal is cut out of the subject rather than drawn independently. Lower
# than the literals generator's 0.7 because a case-insensitive pattern matches far more than the
# substring it was cut from, so the wave would otherwise be almost all matches.
FOLD_SUBSTRING_PROBABILITY = 0.55

# How often the subject is built from doubled characters rather than drawn one at a time - the
# trick the backrefs generator uses, and for the same reason: a reference needs the subject to
# repeat itself before it can match at all. It matters more here, because REF_GROUP_FLD only
# behaves differently from REF_GROUP_IGN when the *captured* text expands on folding, and a
# reference that never matches never reaches that. With REF_GROUP_FLD comparing the captured
# character raw instead of folded (S22 control D), 600 rows gave 0 divergences at seed 7 and 0 at
# seed 4242 without this, and 8 and 6 with it (measured 2026-08-31).
FOLD_DOUBLED_SUBJECT_PROBABILITY = 0.4

# The pattern shapes, and how often each is chosen. Every one puts the folded literal somewhere a
# different opcode family handles it: bare is CHARACTER_IGN / STRING_IGN / STRING_FLD, 'set' is
# SET_UNION_IGN, 'range' is RANGE_IGN, 'property' is PROPERTY_IGN, 'repeat' is the *_REPEAT_ONE
# counting path, and 'backref' is REF_GROUP_IGN / REF_GROUP_FLD.
FOLD_SHAPES = (
    "bare",
    "folded-literal",
    "set",
    "negated-set",
    "range",
    "property",
    "repeat",
    "backref",
    "folded-backref",
)
FOLD_SHAPE_WEIGHTS = (16, 12, 11, 7, 10, 7, 10, 11, 16)

# The properties the 'property' shape draws from. \p{Lu}, \p{Ll} and \p{Lt} are the three whose
# meaning matches_PROPERTY_IGN deliberately changes under IGNORECASE - they collapse into "is it a
# cased letter" - and \p{Upper} and \p{Lower} are the two that collapse into \p{Cased}. \p{L} is
# the control: case-insensitive already, so it must answer the same either way.
FOLD_PROPERTIES = (r"\p{Lu}", r"\p{Ll}", r"\p{Lt}", r"\p{Upper}", r"\p{Lower}", r"\p{L}", r"\p{Alpha}")

FOLD_QUANTIFIERS = ("+", "*", "?", "{1,3}", "{2}", "+?", "*?")

# Ranges that hold one case of a letter and not the other, so a character inside one has no second
# case inside it and 'in_range_ign' has to test the character itself as well as its other cases.
# The Cherokee pair is the same trick above the BMP's Latin block, where the small letters fold
# upward out of their own range; the Deseret pair is astral.
FOLD_ONE_CASE_RANGES = (
    "[a-z]",
    "[A-Z]",
    "[a-m]",
    "[N-Z]",
    "[Ꭰ-Ᏽ]",
    "[ꭰ-ꮿ]",
    "[\U00010400-\U00010427]",
    "[\U00010428-\U0001044f]",
)


def _fold_pattern(
    rng: random.Random,
    literal: str,
    subject: str,
    shapes: tuple[str, ...] = FOLD_SHAPES,
    weights: tuple[int, ...] = FOLD_SHAPE_WEIGHTS,
) -> tuple[str, str]:
    """Wraps a literal drawn from the folding inventory in one of the shapes above.

    Returns the pattern and the shape that produced it; the caller needs the shape to decide
    whether the ASCII flag is safe on this row. The subject is needed only by the two backreference
    shapes, which pick a character the subject already repeats.

    ``shapes``/``weights`` are overridable so a caller aiming at one opcode family can narrow the
    draw to the shapes that reach it; S23's ``_generate_reverse_fold`` is the only such caller.
    """
    shape = rng.choices(shapes, weights=weights, k=1)[0]

    if shape == "property":
        quantified = rng.choice(FOLD_QUANTIFIERS) if rng.random() < 0.4 else ""
        return rng.choice(FOLD_PROPERTIES) + quantified, shape

    if not literal:
        # Nothing to wrap; an empty pattern is a legal row and matches everywhere.
        return "", shape

    if shape == "folded-literal":
        # The spelled-out folding of the literal, so under (?f) the pattern is longer than the
        # subject text it has to match and STRING_FLD's two sides advance at different rates. That
        # is the one thing a bare literal cannot test: with pattern and subject the same length,
        # advancing the subject once per pattern character and advancing it only when the subject's
        # folding runs out are the same walk. With STRING_FLD advancing unconditionally (S22
        # control C), 600 rows at seed 7 gave 0 divergences without this shape and 5 with it, and
        # at seed 4242 1 and 5 (measured 2026-08-31).
        return literal.casefold(), shape

    if shape == "set":
        return "[" + literal + "]", shape

    if shape == "negated-set":
        return "[^" + literal + "]", shape

    if shape == "range":
        # Half the time a range whose bounds hold one case and not the other, so a subject
        # character inside it has no *other* case inside it. A range built from the subject's own
        # characters almost always contains both cases of everything it contains, which makes it
        # nearly blind to a folding bug: with 'in_range_ign' skipping the character itself
        # (S22 control B), 600 rows at seed 7 gave 3 divergences without these and 7 with them,
        # and at seed 4242 3 and 8 (measured 2026-08-31).
        if rng.random() < 0.5:
            return rng.choice(FOLD_ONE_CASE_RANGES), shape

        # Sorted, because '[z-a]' is a parse error rather than a matching question. A range of one
        # character is legal and still exercises in_range_ign, so a duplicate pair is kept.
        lower, upper = sorted((rng.choice(literal), rng.choice(literal)))
        return "[" + lower + "-" + upper + "]", shape

    if shape == "repeat":
        # One character repeated, so the parser builds a *_REPEAT_ONE and the count runs through
        # count_one's bulk-stepper path rather than through the dispatch switch.
        return rng.choice(literal) + rng.choice(FOLD_QUANTIFIERS), shape

    if shape in ("backref", "folded-backref"):
        # The reference is what REF_GROUP_IGN and REF_GROUP_FLD execute; under (?f) the two sides
        # can fold to different lengths, which is the whole difficulty of REF_GROUP_FLD - and a
        # reference that does not match never reaches the folding at all. So the group is a single
        # character the subject already repeats where there is one: drawn from the literal instead,
        # only 23 of 152 backreference rows matched (600 rows, seed 7, 2026-08-31), and only 2 of
        # those had a subject character that expands on folding.
        doubled = [subject[i] for i in range(len(subject) - 1) if subject[i] == subject[i + 1]]
        group = rng.choice(doubled) if doubled else rng.choice(literal)

        # 'folded-backref' writes the group as the spelled-out folding of that character, so
        # against a subject holding the unfolded form the group *captures* a character that
        # expands, and REF_GROUP_FLD has to fold the captured text as well as the subject. Nothing
        # else here produces that: a group written as the character itself captures the character
        # itself, and both sides then fold the same way.
        if shape == "folded-backref":
            group = group.casefold()

        return "(" + group + r")\1", shape

    return literal, shape


def _generate_casefolding(rng: random.Random, count: int):
    """S22's generator: every literal, set, range, repeat and backreference under (?i) and (?fi).

    Measured by `python tools/record-oracle.py --generator case-folding --count 600 --seed 1`,
    after the last change to this generator: 223 match, 377 do not, no parse errors and no empty
    patterns; 85 rows have an astral subject and 248 a subject holding a character that expands on
    folding; 304 rows carry FULLCASE and 127 carry ASCII. Classifying that wave's patterns by shape
    - which cannot separate 'bare' from 'folded-literal', nor the two backreference shapes - gives
    181 bare-or-folded, 77 set, 45 negated-set, 66 range, 32 property, 55 repeat and 144
    backreference rows.
    """
    for i in range(count):
        alphabet = FOLD_ALPHABETS[i % len(FOLD_ALPHABETS)]
        length = rng.randrange(1, MAX_FOLD_SUBJECT_LENGTH + 1)

        if rng.random() < FOLD_DOUBLED_SUBJECT_PROBABILITY:
            subject = ""
            while len(subject) < length:
                subject += rng.choice(alphabet) * 2
            subject = subject[:length]
        else:
            subject = "".join(rng.choice(alphabet) for _ in range(length))

        # At least one character, on both paths. Drawn the way the literals generator draws it -
        # 'randrange(start, ...)' and 'randrange(MAX + 1)' - an empty literal came up on 173 rows
        # of 600 at seed 1, and every shape below collapses to the empty pattern when handed one,
        # so nearly a third of the wave was asking upstream what '' matches. The '?' and '*'
        # quantifiers still give the wave patterns that *can* match empty.
        if rng.random() < FOLD_SUBSTRING_PROBABILITY:
            start = rng.randrange(len(subject))
            end = rng.randrange(start + 1, min(len(subject), start + MAX_FOLD_PATTERN_LENGTH) + 1)
            literal = subject[start:end]
        else:
            literal = "".join(rng.choice(alphabet) for _ in range(rng.randrange(1, MAX_FOLD_PATTERN_LENGTH + 1)))

        pattern, shape = _fold_pattern(rng, literal, subject)
        operation = OPERATIONS[i % len(OPERATIONS)]

        # IGNORECASE on every row - a case-folding generator with a case-sensitive row in it is
        # just the literals generator. FULLCASE on half, because (?i) and (?fi) reach different
        # opcodes for the same pattern: STRING_IGN against STRING_FLD, REF_GROUP_IGN against
        # REF_GROUP_FLD. ASCII on a quarter, to reach the other casing table.
        flags = IGNORECASE
        if rng.random() < 0.5:
            flags |= FULLCASE
        if rng.random() < 0.25:
            flags |= ASCII_FLAG

        # ... but never ASCII on a property. Upstream does not agree with *itself* about a cased
        # property under the ASCII encoding, and no single predicate reproduces all of its answers.
        # Measured against regex 2026.7.19 on 2026-08-31, subject 'KsKK', operation match:
        #
        #     pattern        (?ai)      (?ui)
        #     \p{Ll}         (0, 1)     (0, 1)
        #     \p{Ll}{2}      (0, 2)     (0, 2)
        #     \p{Ll}{4}      (0, 4)     (0, 4)
        #     \p{Ll}+        (0, 2)     (0, 4)
        #     \p{Ll}*        (0, 0)     (0, 4)
        #     (\p{Ll})+      (0, 4)     (0, 4)
        #
        # A greedy '*' that consumes nothing where '+' consumes two is not a rule any predicate
        # states; it is three code paths - the dispatch switch, count_one's bulk stepper and
        # search_start's screen - reaching for three different functions, only one of which does
        # the cased-category collapse. `regex.search(r'(?ai)\p{Ll}', 'A')` being None while
        # `regex.match` of the same pair spans (0, 1) is the same fault seen from the search side.
        # This port answers all three consistently, so every such row records as a divergence.
        # The rows we can pin are pinned in Gaps/Engine/CaseInsensitiveMatchingTests.cs, and
        # DECISIONS 2026-08-31 carries the finding. Revisit if upstream settles it.
        if shape == "property":
            flags &= ~ASCII_FLAG

        yield {
            "generator": "case-folding",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": operation,
        }


# Which generators the reverse generator draws from: every earlier one, one share each.
REVERSE_SOURCES = (
    "literals",
    "literal-dot",
    "anchors",
    "classes",
    "groups",
    "quantifiers",
    "boundaries",
    "backrefs",
    "case-folding",
)

# ... plus this many shares of the expanding-fold emphasis below. Upstream fixed exactly the
# reverse-plus-full-casefolding interaction in 2026.5.9 ("Reverse matching with full unicode
# casefolding could lead to out-of-range string indexes", upstream/changelog.txt), so that is the
# cell this slice was told to probe hardest - and a plain 'case-folding' share barely reaches it,
# because STRING_FLD_REV and REF_GROUP_FLD_REV only behave differently from their _IGN_REV twins
# when a *folding expands*, and the shares, the FULLCASE coin, the shape draw and the alphabet
# rotation multiply out to almost nothing. With STRING_FLD_REV reading folded[0] instead of
# folded[folded_pos - 1] (S23 control D), 600 rows of a wave whose fold shares came from
# 'case-folding' gave 1 divergence at seed 7; with these two shares in their place, 21 at seed 7
# and 20 at seed 4242 (measured 2026-09-01).
REVERSE_FOLD_BATCHES = 2

# The shapes the expanding-fold emphasis draws from: the four that reach a *_FLD_REV opcode.
# 'bare' and 'folded-literal' are STRING_FLD_REV, the two backreference shapes REF_GROUP_FLD_REV.
REVERSE_FOLD_SHAPES = ("bare", "folded-literal", "backref", "folded-backref")
REVERSE_FOLD_SHAPE_WEIGHTS = (1, 1, 1, 1)


def _generate_reverse_fold(rng: random.Random, count: int):
    """Rows aimed squarely at STRING_FLD_REV and REF_GROUP_FLD_REV.

    ``_generate_casefolding`` with three things forced rather than drawn: FULLCASE on every row,
    an alphabet that always carries expanding characters (half of them astral as well), and a
    subject of at least two characters. Everything else - the doubling trick, the substring cut,
    the shape wrapper - is shared with it, because the point is the same rows aimed at a narrower
    target, not a different kind of row.

    ASCII is never set. It cannot be: the flag turns full casefolding off, which is the thing
    being probed.
    """
    alphabets = (
        FOLD_ASCII + _expanding_characters(),
        FOLD_ASCII + FOLD_ASTRAL + _expanding_characters(),
    )

    for i in range(count):
        alphabet = alphabets[i % len(alphabets)]
        length = rng.randrange(2, MAX_FOLD_SUBJECT_LENGTH + 1)

        if rng.random() < FOLD_DOUBLED_SUBJECT_PROBABILITY:
            subject = ""
            while len(subject) < length:
                subject += rng.choice(alphabet) * 2
            subject = subject[:length]
        else:
            subject = "".join(rng.choice(alphabet) for _ in range(length))

        start = rng.randrange(len(subject))
        end = rng.randrange(start + 1, min(len(subject), start + MAX_FOLD_PATTERN_LENGTH) + 1)
        literal = subject[start:end]

        pattern, _ = _fold_pattern(rng, literal, subject, REVERSE_FOLD_SHAPES, REVERSE_FOLD_SHAPE_WEIGHTS)

        yield {
            "generator": "case-folding",
            "pattern": pattern,
            "flags": IGNORECASE | FULLCASE,
            "namedLists": {},
            "subject": subject,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }


def _generate_reverse(rng: random.Random, count: int):
    """S23's generator: every earlier generator's rows, searched right to left.

    Not a new shape of pattern - the point of ``(?r)`` is that it changes the *direction* every
    other construct runs in, so the honest wave is the ones already written with ``(?r)`` in front.
    Prefixed as inline pattern text rather than passed as a flag because that is what a caller
    writes and what the ported tests use, and because a global flag has to be at the start of the
    pattern anyway.

    Drawn in whole batches per source rather than one row at a time: every source generator picks
    its alphabet by the row index it is on, so asking each of them for one row repeatedly would
    hand out index 0 every time and the astral alphabet would never appear - which is the half of
    the subject space this slice most needs.

    Measured by `python tools/record-oracle.py --generator reverse --count 1100 --seed 1`, after
    the last change to this generator: 358 match, 742 do not, no parse errors; 279 rows have an
    astral subject and 246 carry FULLCASE.
    """
    total = len(REVERSE_SOURCES) + REVERSE_FOLD_BATCHES
    per, extra = divmod(count, total)
    sizes = [per + (1 if k < extra else 0) for k in range(total)]

    batches = [list(_generate(name, rng, sizes[k])) for k, name in enumerate(REVERSE_SOURCES)]
    for k in range(REVERSE_FOLD_BATCHES):
        batches.append(list(_generate_reverse_fold(rng, sizes[len(REVERSE_SOURCES) + k])))

    for k in range(max((len(b) for b in batches), default=0)):
        for batch in batches:
            if k >= len(batch):
                continue
            row = dict(batch[k])
            row["generator"] = "reverse"
            row["pattern"] = "(?r)" + row["pattern"]
            yield row


# --------------------------------------------------------------------------------------------
# S24's generator: sub, subn, subf and subfn
# --------------------------------------------------------------------------------------------

# What a capture group is wrapped round. Every one is a single character or a class matching one,
# so a group's span is predictable and a `*` on it can match empty - which is where the empty-match
# advance policy, and therefore most substitution bugs, live.
SUB_ATOMS = ("a", "b", "x", ".", "[ab]", "[^a]", r"\w", r"\d")

# The shapes a pattern fragment takes, and how many capture groups each opens. Weighted towards the
# plain and starred group: the first is the ordinary case a template references, and the second is
# the zero-width one. 'optional' exists to produce a group that takes no part in a match, which is
# the case where upstream expands a reference to the empty string rather than dropping it.
# 'plus' is the only shape that makes a group capture more than once, which is what a format
# template's `{n[i]}` subscript exists to reach: without it every capture list was one entry long,
# the `[-1]` subscript agreed with `[0]` by accident, so the rule that reads it could not be got
# wrong observably. With this shape the control that breaks that rule fires on 16 rows of 600 at
# seed 7 and 14 at seed 55 (measured 2026-09-01 against this generator, S24 closing notes).
SUB_SHAPES = ("literal", "group", "named", "star", "lazy", "optional", "branch", "plus")
SUB_SHAPE_WEIGHTS = (12, 20, 10, 15, 7, 12, 9, 15)
SUB_SHAPE_GROUPS = {
    "literal": 0,
    "group": 1,
    "named": 1,
    "star": 1,
    "lazy": 1,
    "optional": 1,
    "branch": 1,
    "plus": 1,
}

# How often the whole pattern becomes `A|B`, where B's atoms are drawn from characters the subject
# alphabets do not hold. B's groups are then reachable by number from the template and never take
# part in a match, which is upstream's `sub('(test1)|(test2)', r'matched: \1\2', ...)` case.
SUB_ALTERNATION_PROBABILITY = 0.3
SUB_DEAD_ATOMS = ("q", "z", "Q", "7")

# How often a template reaches for a group the pattern has not got. Both directions are wanted and
# they fail at different times: `\g<n>` out of range is rejected while the template compiles, and a
# bare `\n` out of range only when it is expanded against a match (measured 2026-09-01).
SUB_BAD_REFERENCE_PROBABILITY = 0.08

# The replacement counts, in upstream's convention where 0 is "no limit" and a negative is "no
# replacements at all" - which is this surface's -1 and 0 respectively, so the wave exercises both
# ends of the translation. Weighted towards no limit so most rows exercise the whole scan, with the
# rest stopping the loop early enough to catch a `count` that counts scans instead of replacements.
SUB_COUNTS = (0, 0, 0, 1, 2, 3, -1)

# Literal runs a template is built from. `<`, `>`, `[`, `]` and `{`/`}` are in there because they
# are the delimiters of the two template languages and a literal one must survive unchanged.
SUB_LITERALS = ("-", "|", "<", ">", "[", "]", "ab", "", " ", "\U0001f600")

MAX_SUB_TEMPLATE_PARTS = 4

# One row in this many forces the too-short-subject shortcut's cell. `pattern_subx` compares
# `min_width` - a CODEPOINT count - against the subject, and returns before the template is even
# compiled, so the only way the comparison is observable at all is a subject whose codepoint and
# UTF-16 code-unit counts differ, a pattern whose width falls between the two, and a template the
# template compiler rejects. All three at once: without this arm the ordinary rows reached the cell
# on no seed tried and the control that breaks the comparison fired on 0 rows of 600; with it, on
# 21 at seed 7 and 21 at seed 55 (measured 2026-09-01, S24 closing notes).
SUB_NARROW_EVERY = 12
SUB_NARROW_ASTRAL = "\U0001f600"



def _sub_pattern(rng: random.Random) -> tuple[str, int, list[str]]:
    """A pattern, its capture group count and its group names.

    The inventory is returned rather than recovered from the pattern because the template has to
    reference groups by number, and a number the pattern has not got is a *different* test - one
    this generator makes deliberately and rarely (SUB_BAD_REFERENCE_PROBABILITY) rather than by
    accident on most rows.
    """

    def branch(atoms: tuple[str, ...], names: list[str], start: int) -> tuple[str, int]:
        parts = []
        opened = 0
        for _ in range(rng.randrange(1, 4)):
            shape = rng.choices(SUB_SHAPES, weights=SUB_SHAPE_WEIGHTS)[0]
            atom = rng.choice(atoms)
            if shape == "literal":
                parts.append(atom)
            elif shape == "group":
                parts.append(f"({atom})")
            elif shape == "named":
                name = f"g{start + opened + 1}"
                names.append(name)
                parts.append(f"(?P<{name}>{atom})")
            elif shape == "star":
                parts.append(f"({atom}*)")
            elif shape == "lazy":
                parts.append(f"({atom}*?)")
            elif shape == "optional":
                parts.append(f"({atom})?")
            elif shape == "plus":
                # Repeated *inside* the group's own repeat, so the group captures once per
                # iteration and its capture list has more than one entry to subscript.
                parts.append(f"({atom})+")
            else:
                parts.append(f"({atom}|{rng.choice(atoms)})")
            opened += SUB_SHAPE_GROUPS[shape]
        return "".join(parts), opened

    names: list[str] = []
    left, opened = branch(SUB_ATOMS, names, 0)
    if rng.random() >= SUB_ALTERNATION_PROBABILITY:
        return left, opened, names

    right, dead = branch(SUB_DEAD_ATOMS, names, opened)
    return f"{left}|{right}", opened + dead, names


def _sub_template(rng: random.Random, groups: int, names: list[str]) -> str:
    """A `sub` replacement template over a pattern with that group inventory."""
    parts = []
    for _ in range(rng.randrange(1, MAX_SUB_TEMPLATE_PARTS + 1)):
        choice = rng.randrange(7)
        if choice == 0:
            parts.append(rng.choice(SUB_LITERALS))
        elif choice == 1:
            parts.append(rng.choice((r"\n", r"\t", "\\\\", r"\x41", r"\101")))
        elif choice == 2 and rng.random() < SUB_BAD_REFERENCE_PROBABILITY:
            # Rejected while the template compiles, whether or not the pattern matches.
            parts.append(f"\\g<{groups + 1}>")
        elif choice == 3 and rng.random() < SUB_BAD_REFERENCE_PROBABILITY:
            # Rejected only when it is expanded, so a row that never matches is not rejected at all.
            parts.append(f"\\{min(groups + 1, 9)}")
        elif choice in (2, 3, 4) and groups:
            number = rng.randrange(1, groups + 1)
            parts.append(f"\\{number}" if number <= 9 else f"\\g<{number}>")
        elif choice == 5 and names:
            parts.append(f"\\g<{rng.choice(names)}>")
        else:
            # \g<0> is the whole match; a bare \0 is an octal escape for NUL, not group 0.
            parts.append(r"\g<0>" if rng.random() < 0.5 else rng.choice(SUB_LITERALS))
    return "".join(parts)


def _subf_template(rng: random.Random, groups: int, names: list[str]) -> str:
    """A `subf` format template over a pattern with that group inventory.

    Automatic field numbering is decided once for the whole template, because CPython rejects a
    template that mixes `{}` with `{0}` and a wave of rejections would say nothing about the port.
    """
    automatic = rng.random() < 0.2
    parts = []
    for _ in range(rng.randrange(1, MAX_SUB_TEMPLATE_PARTS + 1)):
        choice = rng.randrange(6)
        if choice == 0:
            parts.append(rng.choice(SUB_LITERALS).replace("{", "{{").replace("}", "}}"))
        elif choice == 1:
            parts.append(rng.choice(("{{", "}}", "{{}}")))
        elif automatic:
            parts.append("{}")
        elif choice == 2 and rng.random() < SUB_BAD_REFERENCE_PROBABILITY:
            parts.append(f"{{{groups + 1}}}")
        elif choice in (2, 3) or not names:
            # A subscript reaches the individual captures of a repeated group; a negative one counts
            # back from the last. Index 2 is out of range on most rows, which is wanted: upstream
            # raises IndexError there and so must this port.
            number = rng.randrange(groups + 1)
            # Weighted towards the negative subscripts: they are the only ones whose rule can be
            # got wrong silently, since a positive index is the same number either way. As one
            # choice in five they left the control that breaks that rule below ten rows in 600.
            subscript = rng.choice(("", "[0]", "[-1]", "[-1]", "[-2]", "[2]"))
            parts.append(f"{{{number}{subscript}}}")
        else:
            parts.append(f"{{{rng.choice(names)}}}")
    return "".join(parts)


def _narrow_row(rng: random.Random) -> dict:
    """One row aimed at the too-short-subject shortcut, over an all-astral subject.

    The pattern is a run of ``.``, so its ``min_width`` is exactly its length in codepoints, and
    the template is a group reference the pattern has not got, which the *template compiler*
    rejects - the one thing the shortcut can be seen to skip. The width is drawn across the whole
    interesting range, so the arm carries the two agreeing sides as well as the divergent middle:
    at or below the codepoint count both engines compile the template and raise, above the
    code-unit count both take the shortcut, and only in between do they differ if the comparison
    is made in the wrong unit. Verified against regex 2026.7.19 on 2026-09-01 for one, two and
    three codepoints.
    """
    codepoints = rng.randrange(1, 4)
    width = rng.randrange(1, 2 * codepoints + 2)
    return {
        "generator": "substitution",
        "pattern": "." * width,
        "flags": 0,
        "namedLists": {},
        "subject": SUB_NARROW_ASTRAL * codepoints,
        "operation": "sub",
        "template": r"\g<1>",
        "count": rng.choice(SUB_COUNTS),
    }


def _generate_substitution(rng: random.Random, count: int):
    """S24's generator: (pattern, subject, template, count) quadruples for sub and subf.

    The pattern is generated with its group inventory in hand so the template can reference the
    groups it actually has; the subject is drawn independently, so a fair share of rows match
    nothing and exercise the "return the subject untouched" path and the too-short-subject
    shortcut. Every other row is reversed, because `(?r)` reverses the join list rather than the
    matching alone and that is a substitution-specific code path.

    Measured by `python tools/record-oracle.py --generator substitution --count 600 --seed 1`,
    after the last change to this generator: 194 rows replace at least once, 327 replace nothing,
    79 are rejected by upstream, 250 are subf, and 219 have an astral subject. The `sub` share is
    the larger one because the narrow arm is always a `sub` row.

    Re-taken from scratch after every widening, and that is not ceremony: `SUB_COUNTS` gaining a
    `-1` entry shifts the whole RNG stream, so a figure measured before it describes a wave this
    generator no longer produces. The first set recorded here was wrong for exactly that reason,
    and S24's second blind pass caught it.
    """
    for i in range(count):
        if i % SUB_NARROW_EVERY == SUB_NARROW_EVERY - 1:
            yield _narrow_row(rng)
            continue

        alphabet = ALPHABETS[i % len(ALPHABETS)]
        subject = "".join(rng.choice(alphabet) for _ in range(rng.randrange(MAX_SUBJECT_LENGTH + 1)))

        pattern, groups, names = _sub_pattern(rng)
        operation = SUB_OPERATIONS[i % len(SUB_OPERATIONS)]
        template = (
            _sub_template(rng, groups, names)
            if operation == "sub"
            else _subf_template(rng, groups, names)
        )

        yield {
            "generator": "substitution",
            "pattern": ("(?r)" + pattern) if i % 2 else pattern,
            "flags": 0,
            "namedLists": {},
            "subject": subject,
            "operation": operation,
            "template": template,
            "count": rng.choice(SUB_COUNTS),
        }


# --------------------------------------------------------------------------------------------
# S25: the iteration generator
# --------------------------------------------------------------------------------------------


# The atoms a scan can go wrong on, which is a different set from what a single match cares about.
# Everything here can match at more than one position, and most of it can match nothing at all -
# which is what makes the empty-match advance, the overlapped step and a split's zero-width policy
# reachable at all. `\b` and `\B` are the only zero-width assertions available before Phase 4 brings
# lookaround, and `\G` is left out: it pins a match to the search anchor, which moves with every
# turn of a scan, so it belongs to whichever slice ports the scanner's anchor semantics.
ITER_ATOMS = (
    "a", "b", ":", ".",
    "a*", "a+", "a?", "a*?", "a+?",
    ":*", ":+", ":*?",
    "[ab]", "[ab]*", "[^a]", "[^a]*",
    "a|", "|a", "a|b", ":|a",
    r"\b", r"\B",
    "a{0,2}", "a{1,2}?", "a{2,}",
)

# The group wrappers, because a split interleaves every group's capture and findall/finditer report
# per-group spans: a group that takes no part in one match of a scan is where a null slot comes
# from, and a repeated group is where a capture list grows across a scan. `(?:...)` is in so that a
# non-capturing group is not silently treated as a capturing one.
ITER_WRAPPERS = (
    "{0}",
    "({0})",
    "(?:{0})",
    "({0})*",
    "({0})?",
    "(?P<g>{0})",
    "({0})|(x)",
    "(x)|({0})",
)

# Upstream's own limit convention, where 0 is "no limit" and a negative number is "none at all".
# Both ends are here because the two are inverted against this surface at both ends, and S24 shipped
# a correct port that the oracle called RED for translating only one of them.
ITER_LIMITS = (0, 0, 0, 1, 2, 3, -1)

# The subjects. Short, because what matters is the number of positions a scan visits rather than the
# length of any one match, and adjacency is the interesting case: ':::' gives a run of three, 'a:a'
# gives matches separated by one character, and '' gives the single zero-width position.
ITER_SUBJECT_ALPHABETS = ("ab:", "a:\U0001f600")
ITER_MAX_SUBJECT = 6


def _generate_iteration(rng: random.Random, count: int):
    """S25's generator: whole-sequence rows for finditer, overlapped finditer and split.

    Each row's answer is a sequence, so a scan that finds the right matches in the wrong order or
    stops one short diverges where a per-match comparison would agree. Every fourth row carries
    ``(?r)`` and every third ``(?V1)``, drawn from the seeded stream rather than from ``i`` so the
    two do not alias each other or the operation - the mistake the S16 blind review found in the
    anchors generator, where indexing three tables by ``i`` left two of four opcodes untested.

    Measured by `python tools/record-oracle.py --generator iteration --count 600 --seed 1`, after
    the last change to this generator: 200 finditer rows, 200 overlapped and 200 split; 435 rows
    produce an answer with more than one element, of which 226 are finditer rows with more than one
    match; 1,006 matches in all; 145 rows are reversed; 215 are V1; 182 have an astral subject; and
    111 split rows carry a limit other than "no limit". No row is rejected by upstream - every
    pattern this grammar can build compiles, and rejection coverage belongs to the other generators.

    Re-taken from scratch after every widening, and that is not ceremony: adding one entry to any of
    the tables above shifts the whole RNG stream, so a figure measured before it describes a wave
    this generator no longer produces. S24 quoted five such figures and four were wrong.
    """
    for i in range(count):
        alphabet = ITER_SUBJECT_ALPHABETS[i % len(ITER_SUBJECT_ALPHABETS)]
        subject = "".join(rng.choice(alphabet) for _ in range(rng.randrange(ITER_MAX_SUBJECT + 1)))

        body = "".join(rng.choice(ITER_ATOMS) for _ in range(rng.randrange(1, 3)))
        pattern = rng.choice(ITER_WRAPPERS).format(body)

        # Order matters: (?r) and (?V1) are both global flags and upstream accepts them in either
        # order, but only at the start of the pattern.
        if rng.random() < 0.25:
            pattern = "(?r)" + pattern
        if rng.random() < 0.35:
            pattern = "(?V1)" + pattern

        yield {
            "generator": "iteration",
            "pattern": pattern,
            "flags": 0,
            "namedLists": {},
            "subject": subject,
            "operation": ITER_OPERATIONS[i % len(ITER_OPERATIONS)],
            "count": rng.choice(ITER_LIMITS),
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

    if name == "boundaries":
        yield from _generate_boundaries(rng, count)
        return

    if name == "backrefs":
        yield from _generate_backrefs(rng, count)
        return

    if name == "case-folding":
        yield from _generate_casefolding(rng, count)
        return

    if name == "reverse":
        yield from _generate_reverse(rng, count)
        return

    if name == "substitution":
        yield from _generate_substitution(rng, count)
        return

    if name == "iteration":
        yield from _generate_iteration(rng, count)
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
        # Added in S24. Without it a hand-written minimisation row that forgot its template would
        # record the untouched subject as upstream's answer, which every port trivially agrees with.
        ("a substitution row with no template",
         row(operation="sub"), "needs a 'template'"),
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
