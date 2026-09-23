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

# How long upstream gets to answer one row before the row is recorded as a timeout rather than as
# an answer. S40a, added because upstream can loop for ever on a row a generator drew: at 6000 rows
# a generator, `verbs` row 5944 at seed 4242 never returned, so the wave was never written at all -
# no output, no error, no partial file, and forty minutes lost to bisecting it by hand.
#
# `regex` takes `timeout=` on every method this recorder calls and honours it inside the loop that
# hangs (measured 2026-09-13, regex 2026.7.19, .scratch/probe-timeout.py):
#
#     >>> regex.compile('.?x(?>a(*SKIP)z)').search('xzxa', timeout=3)
#     TimeoutError: regex timed out after 3.0s
#
# Ten seconds, to match `OracleComparer.RowTimeout` - the deadline the consumer gives THIS port for
# the same row. Symmetry is the point: neither engine is allowed longer than the other to answer a
# question, so a row recorded as a timeout is one no engine was going to answer.
#
# It is also what keeps the file deterministic. The recorded outcome carries this CONSTANT and never
# an elapsed time, so two runs of the same seed stay byte-identical as long as the same rows hang -
# which is why the deadline is generous rather than tight. A row that takes nine seconds and a row
# that takes eleven would record differently from run to run, and a deadline anywhere near a
# generator's real row times would make `--verify-determinism` flap.
ROW_TIMEOUT_SECONDS = 10.0

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
# port that accepts the pattern is not thereby wrong. Recorded as an `error` they would be compared
# against whatever this port did, and any rejection at all would score as agreement.
#
# So they are recorded as `resource`, a kind of its own that the consumer skips and counts - see
# `exhausted` in `_record_row` for why that replaced aborting the run, which is what S43 found one
# MemoryError row doing to a whole six-seed wave.
ENVIRONMENT_FAILURES = ("RecursionError", "MemoryError", "OverflowError")


# --------------------------------------------------------------------------------------------
# All Unicode planes (S52)
# --------------------------------------------------------------------------------------------
#
# ONE PLACE for the astral characters every generator draws, so that "the wave reaches every plane"
# is a property of this block rather than of twenty scattered string literals that drifted apart.
#
# WHAT WAS ACTUALLY MISSING, measured before any of this was written (.scratch/s52-plane-audit.py,
# 2026-09-15, 1200 rows a generator across seeds 7, 4242 and 20260915). Astral SUBJECTS were already
# everywhere - 118 to 484 rows a generator, every one of the twenty-one. Three things were not:
#
#   * SMP DIGITS reached only `classes` (125 rows) and `reverse` (13). So `\d`, `\p{Nd}`,
#     `[[:digit:]]` and `\w` over a digit that is two UTF-16 units were barely tested at all.
#   * EMOJI MODIFIERS and ZWJ reached only `boundaries` (241) and `reverse` (21), and `\X` the same
#     two - so grapheme clusters were a `boundaries` feature rather than a wave-wide one.
#   * SEVEN GENERATORS NEVER PUT AN ASTRAL CHARACTER IN THE PATTERN at all - `classes`, `groups`,
#     `quantifiers`, `backrefs`, `substitution`, `iteration` and `fuzzy` - because each builds its
#     pattern from a fixed atom list rather than from a slice of its subject. A fuzzy edit over a
#     surrogate pair, an astral backreference and an astral repeat were therefore unreachable.
#
# Every property below is MEASURED rather than asserted (.scratch/s52-chars.py, same day):
ASTRAL_LETTER = "\U0001d518"  # MATHEMATICAL FRAKTUR CAPITAL U - Lu, \w, \p{L}, folds to itself
ASTRAL_CASED = "\U00010400"  # DESERET CAPITAL LONG I - Lu, \w, \p{L}, and it HAS a lowercase
ASTRAL_CASED_LOWER = "\U00010428"  # DESERET SMALL LONG I - the other half of that pair
ASTRAL_DIGIT = "\U0001d7ee"  # MATHEMATICAL SANS-SERIF BOLD DIGIT TWO - Nd, and \d and \w are true
ASTRAL_DIGIT_2 = "\U000104a0"  # OSMANYA DIGIT ZERO - Nd in a different block, also \d and \w
ASTRAL_SYMBOL = "\U0001f600"  # GRINNING FACE - So, and \w is FALSE, so it is not a word character
EMOJI_MODIFIER = "\U0001f3fb"  # EMOJI MODIFIER FITZPATRICK TYPE-1-2 - Sk, astral, \w false
ZWJ = "‍"  # ZERO WIDTH JOINER - Cf, BMP, and \w is TRUE, which is the surprise

# Drawn character by character like every other alphabet, so the clusters arise from adjacency
# rather than from a table of them. That is deliberate: `boundaries` has a table of whole clusters
# already (BOUNDARY_CLUSTERS), and what no generator had was the AWKWARD SINGLE CHARACTERS - a bare
# modifier with nothing to modify, a ZWJ at the end of a subject, a digit in a plane the fast paths
# do not expect. Adjacency still produces real clusters often enough to matter: U+1F600 followed by
# U+1F3FB is one `\X`, and so is anything either side of a ZWJ.
ASTRAL_ALPHABET = ASTRAL_SYMBOL + ASTRAL_LETTER + ASTRAL_DIGIT + EMOJI_MODIFIER + ZWJ

# The astral atoms a pattern can be built from, for the generators whose patterns come from a fixed
# list rather than from the subject. `\X` is here because it is the one construct whose whole job is
# a multi-codepoint cluster, and because the audit found it in two generators out of twenty-one.
ASTRAL_PATTERN_ATOMS = (ASTRAL_SYMBOL, ASTRAL_LETTER, ASTRAL_DIGIT, r"\X")

# Two alphabets, alternating row by row: one plain ASCII, one mixing BMP and astral characters so
# the UTF-16 translation is exercised from the first wave rather than from the first bug. No
# metacharacter is in either, so a generated pattern needs no escaping and the generators stay
# what they claim to be - literals. U+1F600 GRINNING FACE and U+1D518 MATHEMATICAL FRAKTUR
# CAPITAL U are both astral, so each contributes two UTF-16 units and one codepoint.
ALPHABETS = ("abcde", "ab" + ASTRAL_SYMBOL + ASTRAL_LETTER + "c" + ASTRAL_DIGIT + EMOJI_MODIFIER)

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


def _utf16_index(offsets: list[int], codepoint: int) -> int:
    """One codepoint index as a UTF-16 one, surviving an index that is not in the subject at all.

    Upstream can report one. The composed `interactions` wave S36 added draws
    ``(?P<g1>A*)(?<=(?&g1))`` over ``'A'``, and regex 2026.7.19 records g1's second capture as
    ``(2, 1)`` - a start PAST the end of a one-character subject, and an end before its own start.
    That is issue 614, the match direction not reaching a called group, and 2026.9.10 answers
    ``(0, 1)`` for it; this port has always answered ``(0, 1)``.

    Before this function existed, ``offsets[start]`` raised ``IndexError`` and the whole wave failed
    to record - so one upstream bug took out two thousand rows that had nothing to do with it. The
    index is therefore EXTENDED rather than clamped: one unit per codepoint past the end, and a
    negative index kept as it is, so the impossible span reaches the consumer still impossible and
    is reported as a divergence instead of being quietly made plausible.
    """
    if 0 <= codepoint < len(offsets):
        return offsets[codepoint]
    if codepoint < 0:
        return codepoint
    return offsets[-1] + (codepoint - (len(offsets) - 1))


def _to_index_length(offsets: list[int], span: tuple[int, int]) -> list[int]:
    """A Python codepoint ``(start, end)`` as the UTF-16 ``[Index, Length]`` our API returns."""
    start, end = span
    index = _utf16_index(offsets, start)
    return [index, _utf16_index(offsets, end) - index]


# --------------------------------------------------------------------------------------------
# Running one row through the oracle
# --------------------------------------------------------------------------------------------


# Generators recorded against an upstream whose required-string prefilter is switched off, and the
# only intentional divergence from "upstream as shipped" this recorder carries.
#
# Why. `locate_required_string` (upstream/src/_regex.c:11082) finds the pattern's required literal in
# the subject and moves the *first* attempt to `found_pos - req_offset`, so positions before that are
# never tried at all. That is invisible for every other generator - skipping a position that cannot
# match changes nothing - but a `(*SKIP)` moves `slice_start` from wherever the attempt began, so the
# attempt upstream skips is an attempt with a different answer:
#
#     >>> import regex
#     >>> regex.compile(r"(?:..(*SKIP)x|q)x").search("ab cd xx")     # None
#     >>> regex.compile(r"(?:..(*SKIP)x|q)x").match("ab cd xx", 4)   # (4, 8)
#
# Upstream's own compile call carries `req_offset=3, req_chars=(120,)` for that pattern (measured
# 2026-09-11 by intercepting `regex._regex.compile`), so `x` at 6 puts the first attempt at 3, the
# verb steps 3 -> 5, and position 4 is never tried. Perl does the identical thing - `use re "debug"`
# prints `Found floating substr "x" at offset 6 (rx_origin now 3)` - and PCRE2 documents the class
# under "Optimizations that affect backtracking verbs". It is not a bug on either side.
#
# This port has no prefilter until Phase 7, so recording plain upstream here would file a *missing
# optimisation* as a matching divergence, on rows nobody can act on until Phase 7 arrives. Switching
# the prefilter off instead compares the two matchers, which is what the generator is for: with it
# off, every case that diverged agrees (`ab cd xx` -> (4, 8); `abcdxxx` -> (2, 6), not (3, 7)).
#
# PHASE 7 MUST DELETE THIS. The moment `locate_required_string` is ported, plain upstream and this
# port agree and the `prefilter-free` tag becomes a lie: remove the generator from the tuple below,
# expect the `verbs` wave to go red until the port's own prefilter is right, and invert the two gap
# tests in tests/FuzzyRegex.Tests/Gaps/Engine/BacktrackingVerbTests.cs that pin (4, 8) and (2, 6).
# Both are marked. See docs/plan/slices/done/S29-backtracking-verbs.md and DECISIONS 2026-09-11.
#
# S33 added `partial-sliced` for the same reason on a different symptom. The prefilter does not only
# move an attempt; on a partial match it can SUPPRESS one, because `locate_required_string` returning
# -1 makes `basic_match` answer FAILURE (:11812) before any partial arm is reached, and that check
# runs for `match` and `fullmatch` too rather than only for a search. Measured 2026-09-12,
# .scratch/row719h.py, on the row the S33 wave found at seed 31:
#
#     >>> regex.compile(r"(?rimf)(x)[\p{L}\p{N}]{2,}?").match("a\na..A", 6, 6, partial=True)
#     None                                    # prefilter-free: ((6, 6), True), which is our answer
#     >>> regex.compile(r"(?rimf)[\p{L}\p{N}]{2,}?").match("a\na..A", 6, 6, partial=True)
#     ((6, 6), True)                          # the same pattern with nothing for the prefilter to find
#
# So plain upstream answers a partial or no match on the same subject according to whether the pattern
# happens to carry a required literal, which is a property of the optimiser and not of the language.
# PHASE 7 MUST DELETE THIS TOO, on the same terms as the line above.
#
# `interactions` is NOT on this list, and S36 tried putting it there and took it off again, which is
# worth recording because the a-priori argument for adding it is good. The Phase 4 widening put
# `(*SKIP)` into its patterns and `partial=True` on its rows, so it draws both of the shapes above -
# and yet recording it prefilter-free changes nothing that matters: over 6000 rows at five seeds it
# removed one diverging row and introduced another, and left all three of the `(*SKIP)`-plus-partial
# rows diverging exactly as before. Those three are `search_start`, which is NOT reachable from
# Python, rather than `locate_required_string`, which is. So the change bought nothing and would have
# altered how every `interactions` row is recorded; the slice that judges those three rows is the one
# that should decide it, with the measurement in front of it.
PREFILTER_FREE_GENERATORS = ("verbs", "partial-sliced")

# Where `req_offset` and `req_chars` sit in the positional argument list `_main.py:660` passes to
# `_regex.compile(pattern, flags, code, group_index, index_group, named_lists, named_list_indexes,
# req_offset, req_chars, req_flags, group_count)`. The C extension takes them positionally only.
_REQ_OFFSET_ARG = 7
_REQ_CHARS_ARG = 8

# Upstream's REVERSE flag bit, which is `regex.R`. Spelled out rather than read off the module so
# this file states the number the wave's `flags` field carries.
_REVERSE_FLAG = 0x400

# Upstream's POSIX flag bit, which is `regex.P`, spelled out for the same reason. Read off the
# COMPILED pattern rather than off the row's flags, because an inline `(?p)` never reaches the row's
# flags: measured on every spelling - the flag, a leading `(?p)`, one written mid-pattern and one
# inside a group - by `tools/probes/upstream-posix-flag-is-visible-on-compiled.py` (regex 2026.9.10,
# 2026-09-14), all of which set the bit on `Pattern.flags`.
_POSIX_FLAG = 0x10000

# The leading run of inline-flag groups, which is where every generator writes a pattern's flags.
# Only a construct INSIDE this run is taken away by a control below: past it, an inline group
# scopes to what encloses it, and deleting one there would ask a different question.
_INLINE_FLAG_PREFIX = re.compile(r"^(?:\(\?[a-zA-Z0-9]+\))+")

# Upstream's BESTMATCH flag bit, which is `regex.B`, spelled out for the same reason. Both
# generators that draw the flag write it as the inline `(?b)` prefix rather than setting the bit
# (`_generate_fuzzy` and `_generate_interactions`), so the prefix is the case that fires in practice
# and the bit is handled because a hand-built `--rows` file may use it.
_BESTMATCH_FLAG = 0x1000
_BESTMATCH_INLINE = "(?b)"

# The inline spelling of POSIX, and of an atomic group. Both are handled as a PREFIX-ONLY or
# whole-pattern textual edit, which is as narrow as `_BESTMATCH_INLINE`'s own `startswith` and
# narrow for the same reason: a `(?p)` written mid-pattern scopes to the group it sits in, so
# deleting it there would ask a different question rather than the same one without POSIX.
_POSIX_INLINE = "(?p)"
_ATOMIC_OPEN = "(?>"
_ATOMIC_FREE_OPEN = "(?:"

# The two backtracking verbs, for the control that swaps one for the other. Both prune the same
# backtracking; only `(*SKIP)` moves a slice bound (`upstream/src/_regex.c:14545`, `:14551`).
_SKIP_VERB = "(*SKIP)"
_PRUNE_VERB = "(*PRUNE)"

# Which outcome kinds are an ANSWER to a second question. Anything else - an error, a timeout, a
# resource blowup - says nothing about what the construct did, so the key is left off entirely.
_ANSWERED = ("match", "nomatch", "sub", "split", "matches")


def _without_bestmatch(row: dict, pattern: str, flags: int) -> dict | None:
    """The same row without BESTMATCH, or ``None`` where it carries none.

    ``BESTMATCH`` is documented as a RANKING flag - "By default, fuzzy matching searches for the
    first match that meets the given constraints ... The BESTMATCH flag will make it search for the
    best match instead" (``upstream/README.rst:592``) - so the flag chooses among the flagless
    engine's candidates and cannot invent or destroy one.
    """
    if pattern.startswith(_BESTMATCH_INLINE):
        return {**row, "pattern": pattern[len(_BESTMATCH_INLINE) :]}
    if flags & _BESTMATCH_FLAG:
        return {**row, "flags": flags & ~_BESTMATCH_FLAG}
    return None


def _without_posix(row: dict, pattern: str, flags: int) -> dict | None:
    """The same row without POSIX, or ``None`` where it carries none.

    ``POSIX`` chooses leftmost-LONGEST among the matches the ordinary engine can make
    (``upstream/README.rst``, and ``_regex.c``'s ``RE_FLAG_POSIX`` arms), so like ``BESTMATCH`` it
    is a CHOOSING flag: it may move which match is answered, and only within what the flagless
    engine can already produce. The three rows S48b's second sitting judged are where upstream
    breaks that - two by charging a span more errors than its own flagless engine needs for the
    same span, and one by choosing the SHORTER of two equal-cost matches, which is the opposite of
    leftmost-longest.

    ONLY A `(?p)` IN THE LEADING RUN of inline-flag groups is stripped, and the reason is NOT
    scoping. A `(?p)` is a GLOBAL flag wherever it is a flag group - written mid-pattern, or even
    inside a group, it sets the bit on the whole pattern exactly as a leading one does (measured
    2026-09-14 on regex 2026.9.10; the probe named in the consumer's entry carries it). The reason
    is that a textual `(?p)` is not always a flag group: `[(?p)]+` matches the literal text `(?p)`
    with POSIX OFF, and deleting those four characters leaves `[]+`, which does not compile. Telling
    the two apart needs a parser, and the leading run of flag groups is the cheap place where the
    question is certain. A pattern that carries POSIX any other way records no key at all, so its
    row is REPORTED rather than classified - the direction that cannot hide a defect. No wave has
    drawn one: zero non-leading `(?p)` across 418,005 recorded rows.
    """
    prefix = _INLINE_FLAG_PREFIX.match(pattern)
    if prefix and _POSIX_INLINE in prefix.group(0):
        return {**row, "pattern": pattern.replace(_POSIX_INLINE, "", 1)}
    if flags & _POSIX_FLAG:
        return {**row, "flags": flags & ~_POSIX_FLAG}
    return None


def _without_atomic_groups(row: dict, pattern: str, flags: int) -> dict | None:
    """The same row with every atomic group made an ordinary one, or ``None`` where there is none.

    An atomic group is the one construct that abandons a sub-attempt WITHOUT backtracking through
    it, so it is the door onto ledger entry 11's mechanism that S47's ``leakFreeFuzzy`` cannot see:
    that question re-asks upstream ANCHORED, which removes an EARLIER attempt's leak, and an atomic
    group's leak is inside ONE attempt. ``(?>`` to ``(?:`` is upstream's own control for it - the
    same body, the same alternatives, the backtracking cut gone - and it is an unambiguous
    three-character edit, so no paren matching is needed.

    Unused ``flags``, kept so every control below has one signature.
    """
    del flags
    return {**row, "pattern": pattern.replace(_ATOMIC_OPEN, _ATOMIC_FREE_OPEN)} if _ATOMIC_OPEN in pattern else None


def _with_prune_instead_of_skip(row: dict, pattern: str, flags: int) -> dict | None:
    """The same row with every ``(*SKIP)`` made a ``(*PRUNE)``, or ``None`` where there is none.

    THE ONE CONTROL THIS FILE ALREADY RELIES ON IN PROSE AND HAS NEVER RECORDED. Four entries in
    ``tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`` - ``search-start-partial``,
    ``partial-retry-reversed-slice``, ``partial-retry-carried-slice-forward`` and
    ``skip-blocks-a-repeat-retreat-partial`` - rest their judgement on it, each quoting a probe run
    by hand: ``(*PRUNE)`` prunes exactly the backtracking ``(*SKIP)`` prunes and moves NO bound
    (``upstream/src/_regex.c:14545`` and ``:14551`` are the two lines that move ``slice_start`` and
    ``slice_end``, and the ``(*PRUNE)`` arm has neither), so an answer that changes between the two
    changed because of the bound and not because of what the pattern means. Recording it makes that
    a per-row fact the consumer can read instead of a paragraph a reader has to trust.

    NOT A JUDGEMENT AND NOT A GATE, exactly like the three controls above. ``(*SKIP)`` is documented
    to do something ``(*PRUNE)`` does not - "the next attempt at a match will start at the position
    in the string where ``(*SKIP)`` was encountered" - so the two answering differently is the
    ordinary case, and on the great majority of rows both engines follow upstream's ``(*SKIP)``
    together and nothing here fires. What the key supplies is the OTHER half of a row-keyed entry:
    which of the two answers this port gave.

    GATED ON THE TEXT, like ``subMatches`` and ``anchoredScan``'s own ``"(*SKIP)" in pattern``. A
    ``(*SKIP)`` inside a character class is literal text and the edit would ask a different
    question there - but no generator writes one, and a row whose key is useless is still REPORTED
    rather than classified, because every entry that reads this is keyed on rows as well.

    Unused ``flags``, kept so every control has one signature.
    """
    del flags
    return {**row, "pattern": pattern.replace(_SKIP_VERB, _PRUNE_VERB)} if _SKIP_VERB in pattern else None


# Each control's recorded key, and how to take its construct away. Every one is A SECOND FACT
# ABOUT UPSTREAM, never compared against anything, exactly as `searchOnlyPartial` and
# `anchoredScan` are: only the consumer's `ExpectedDivergences` reads them.
#
# They exist because no predicate over the two COMPARED answers is narrow enough for these
# families. On one `bestmatch` row the two engines report the same span, the same groups and the
# same fuzzy counts and differ only over which positions are substitutions and which insertions;
# on the POSIX rows they report the same span and differ only in what it cost. Nothing on our side
# of the comparison can see that as a family rather than as a defect.
#
# NONE OF THEM IS A GATE OR NARROW BY ITSELF - see each entry's own `Reason`, which records what a
# control measured about its width and what backstops it.
_CONTROLS = (
    ("bestmatchFreeOutcome", _without_bestmatch),
    ("posixFreeOutcome", _without_posix),
    ("atomicFreeOutcome", _without_atomic_groups),
    ("pruneOutcome", _with_prune_instead_of_skip),
)


# --------------------------------------------------------------------------------------------
# Metamorphic invariants - docs/ORACLE-INVARIANTS.md, S52c scope item 2
# --------------------------------------------------------------------------------------------
#
# The oracle asks "do the two engines agree?" and is blind to a bug the port inherited line for
# line, because then they do. Every serious inherited bug in LEDGER.md was found instead by
# UPSTREAM CONTRADICTING ITSELF, by hand, when a session happened to look. These checks make that
# automatic: a row whose own recorded answers contradict each other carries the invariant's id in
# `selfContradiction`, with no port involved and no judgement applied.
#
# EVERY INVARIANT HERE COSTS ZERO EXTRA UPSTREAM CALLS, which is not only a budget decision. Ledger
# entry 9 is upstream crashing on a question asked a particular way, so a checker that asks its own
# questions can be the thing that faults and can take a whole wave with it. Four of these are read
# off the recorded row; three read the ablation twins `_CONTROLS` already records. The `+1` and `+2`
# tiers in ORACLE-INVARIANTS.md are a later sitting's.
#
# WHAT FIRES IS A CANDIDATE, NEVER A VERDICT. An invariant firing means the engine is wrong or the
# invariant is; scope item 3 triages every one and prunes this list when it is the invariant.

# The two constructs measured to put a group's span OUTSIDE the reported match span, which is what
# `group-spans-inside-match` is narrowed around. BOTH NARROWINGS ARE MEASURED, not reasoned:
# `tools/probes/upstream-free-tier-invariant-grounds.py` sections 3 and 5 (regex 2026.9.10,
# 2026-09-16).
#
#   `\K`        resets the reported match start, so a group before it lies outside the span:
#               `(a)\Kb` over 'ab' is match (1, 2) with group 1 at (0, 1).
#   lookaround  consumes nothing, so a group inside one matches text the match never covered:
#               `a(?=(b))` over 'ab' is match (0, 1) with group 1 at (1, 2), and `(?:(?=(bc))b)`
#               over 'bc' is match (0, 1) with group 1 at (0, 2).
#
# A GROUP CALL IS **NOT** NARROWED AROUND, and ORACLE-INVARIANTS.md's first draft said it was. The
# probe's section 3 measured both spellings - `(a)b(?1)` over 'aba' and `(?P<g>a)b(?&g)` over 'aba' -
# and each records group 1 at (0, 1), INSIDE the match. A call re-enters a group; it does not move
# the span that is reported for it. Excluding those rows would have been a hole for no reason. What
# the ledger actually records under a group call (entry 8, and the `group-call-direction` family in
# run-oracle.ps1) is a call inside a LOOKAROUND, which the lookaround narrowing already covers.
#
# GATED ON THE TEXT, like `_without_posix` and `subMatches`: `(?=` inside a character class is
# literal text, and a pattern written that way is skipped when it need not be. That direction loses
# a check; the other invents a violation, which is the one that wastes triage.
_SPAN_ESCAPES_THE_MATCH = re.compile(r"\\K|\(\?=|\(\?!|\(\?<[=!]")

# The two controls whose construct only ever REMOVES backtracking, so their twin cannot be cheaper
# to run than the row itself. See `no-fault-where-a-twin-answers` below, which uses these and not
# the ranking flags when the row's fault is a timeout.
_PRUNING_CONTROLS = ("atomicFreeOutcome", "pruneOutcome")


def _matches_of(outcome: dict) -> list[dict]:
    """Every described match in an outcome - one for a single-match row, the list for a scan.

    A `sub`, `split`, `nomatch`, `error`, `timeout` or `resource` outcome describes no match, so
    the structural invariants below have nothing to read and the row is silently not checked.
    """
    kind = outcome.get("kind")
    if kind == "match":
        return [outcome]
    if kind == "matches":
        return outcome.get("matches") or []
    return []


def _structural_violations(recorded: dict) -> list[str]:
    """The invariants decidable from the recorded row alone - ORACLE-INVARIANTS.md group B.

    `captures-are-the-texts-of-spans` is NOT here: the row records the capture spans and not their
    texts, so it is collected at record time by `_describe_match`, where the match object still is.
    """
    violations: list[str] = []
    spans_are_inside = not _SPAN_ESCAPES_THE_MATCH.search(recorded["pattern"])

    for match in _matches_of(recorded["outcome"]):
        # `fuzzy-counts-match-changes`. Ledger 11, the entry with seven distinct doors found so far,
        # all of one shape: a fuzzy match reporting change positions that contradict its own change
        # counts. S47 checked this by hand on the rows it minimised; this is the same property on
        # every row of every wave. A POSIX row records counts and no positions (ledger 9), so the
        # check simply has nothing to compare there and is skipped.
        counts = match.get("fuzzyCounts")
        changes = match.get("fuzzyChanges")
        if counts is not None and changes is not None:
            tally = [
                len(changes["substitutions"]),
                len(changes["insertions"]),
                len(changes["deletions"]),
            ]
            if tally != counts:
                violations.append("fuzzy-counts-match-changes")

        groups = match.get("groups") or []

        # `lastindex-participated`, PARTICIPATION LIMB ONLY. `lastindex` is documented as the last
        # group that *participated*, so a `lastindex` naming a group whose span is (-1, -1) is the
        # match object contradicting itself.
        #
        # THE SECOND LIMB ORACLE-INVARIANTS.md STATED - "and `lastgroup` names the same group" - IS
        # FALSE, and the probe's section 1 measures it: `(?P<x>a)(b)` over 'ab' answers lastindex=2
        # and lastgroup='x', and group 2 has no name at all. `lastgroup` is the last NAMED group,
        # which is what `_describe_match`'s own comment has said since S14. The limb is pruned in
        # the file with this measurement beside it.
        last = match.get("lastIndex", -1)
        if last != -1 and not (0 <= last < len(groups) and groups[last]["success"]):
            violations.append("lastindex-participated")

        # `group-spans-inside-match`, narrowed - see `_SPAN_ESCAPES_THE_MATCH`. Every capture is
        # checked and not only the group's own span, because a repeated group's earlier captures are
        # the half most likely to rot and cost nothing extra to read.
        if spans_are_inside and groups:
            start = groups[0]["index"]
            end = start + groups[0]["length"]
            for group in groups[1:]:
                if not group["success"]:
                    continue
                spans = [[group["index"], group["length"]], *group["captures"]]
                if any(index < start or index + length > end for index, length in spans):
                    violations.append("group-spans-inside-match")
                    break

    return violations


def _choosing_flag_violations(invariant: str, flagged: dict, free: dict) -> list[str]:
    """One CHOOSING flag against its own flagless twin - ORACLE-INVARIANTS.md groups D and E.

    `BESTMATCH` and `POSIX` are both documented as SELECTING among the matches the ordinary engine
    can already make, so each is checkable against that engine with no reference to any other
    implementation - and the twin is already on the row, recorded by `_CONTROLS`. Three limbs:

    existence  the flag chose nothing from a non-empty set. Ledger 12 and 13 (`BESTMATCH` loses a
               match plain fuzzy matching finds) and 23 (`POSIX` adds a zero-width match the
               flagless engine does not make - the other direction of the same limb).
    longest    POSIX only, and POSIX only because `BESTMATCH` is defined to choose the BEST and not
               the longest. Ledger 16.
    cost       the flag's answer at the same start costs more errors than the flagless engine needs.
               Ledger 9 for POSIX; `bestmatch-no-worse` limb (b) for BESTMATCH.

    WHAT IS DELIBERATELY NOT A VIOLATION: the two answering at DIFFERENT starts. A `search` reports
    only its first match, so a flagless answer at another start says nothing about whether the
    flagless engine could also match where the flag did - the invariant's own wording is "one the
    flagless engine can also make AT THE SAME START". Flagging it would report ambiguity as a
    finding, and ambiguity is what the confounder note in ORACLE-INVARIANTS.md exists to keep out.

    NEITHER IS A ROW THE FLAGGED SIDE DID NOT ANSWER, and the first three-seed wave of S52c is why
    this guard is written down rather than assumed. Without it, `_matches_of` renders a TIMEOUT as
    "no matches", the existence limb reads that as "the flag chose nothing from a non-empty set",
    and a row where upstream merely ran out of its ten seconds is filed as `BESTMATCH` losing a
    match. Three of the four `bestmatch-no-worse` firings and the single
    `posix-chooses-among-flagless-answers` firing were exactly that - upstream had no answer at all
    on the flagged side. The fault case is `no-fault-where-a-twin-answers`'s business, and it fires
    on those rows already.
    """
    violations: list[str] = []
    if flagged["kind"] not in _ANSWERED or free["kind"] not in _ANSWERED:
        return violations

    flagged_matches = _matches_of(flagged)
    free_matches = _matches_of(free)

    # Existence, in both directions and for a scan as well as a single match.
    if not flagged_matches and free_matches:
        return [invariant]
    if flagged_matches and not free_matches:
        return [invariant]
    if flagged["kind"] != "match" or free["kind"] != "match":
        return violations

    ours, theirs = flagged_matches[0], free_matches[0]
    if not ours.get("groups") or not theirs.get("groups"):
        return violations
    if ours["groups"][0]["index"] != theirs["groups"][0]["index"]:
        return violations

    same_length = ours["groups"][0]["length"] == theirs["groups"][0]["length"]
    if invariant == "posix-chooses-among-flagless-answers":
        if ours["groups"][0]["length"] < theirs["groups"][0]["length"]:
            violations.append(invariant)

        # THE COST LIMB NEEDS THE SAME SPAN, NOT MERELY THE SAME START, and reading ledger 9 as
        # "the same start" makes this invariant FALSE. Entry 9 is upstream "charging a SPAN more
        # errors than its own flagless engine needs for the same span"; POSIX leftmost-longest is
        # defined to buy LENGTH, and a longer match at the same start legitimately costs more.
        # Measured 2026-09-16, and this is a legal answer the first version of this check reported:
        #
        #     (?p)(?:abc){e<=2}  over 'abxxyc'  ->  span (0, 4), counts (1, 1, 0)
        #         (?:abc){e<=2}  over 'abxxyc'  ->  span (0, 3), counts (1, 0, 0)
        #
        # POSIX is both longer (the limb above holds) and therefore dearer. Found by the blind
        # review; the wave had not drawn one, because only 29 of the 1,122 rows that reach this
        # comparison carry fuzzy counts on either side at all.
        if not same_length:
            return violations

    # BESTMATCH takes the cost limb at the same START, deliberately, and the asymmetry above is not
    # an oversight. BESTMATCH is defined to minimise ERRORS and is free to choose any length to do
    # it, so a dearer answer at the same start is a contradiction whatever its length; POSIX is
    # defined to maximise LENGTH and buys it with errors.
    if sum(ours.get("fuzzyCounts") or []) > sum(theirs.get("fuzzyCounts") or []):
        violations.append(invariant)

    return violations


def _twin_answered(free: dict | None, kind: str) -> bool:
    """Whether an ablation twin's answer actually says the row's question is answerable.

    A control's key is only written when the twin answered (`_ANSWERED`), so the key being present
    is nearly the whole of it. THE ONE EXCEPTION IS A SUBSTITUTION THAT REPLACED NOTHING, and S52c's
    second corrected wave is what forced it, on both of the two rows the fault limb fired on.

    Both were `subf` rows raising `IndexError` while matching, each beside a twin that answered - the
    shape of ledger entry 6, and not what it actually was. Measured in section 7 of
    `tools/probes/upstream-free-tier-invariant-grounds.py` (regex 2026.9.10, 2026-09-16): upstream's
    `subf` renders the template with `str.format` over the GROUP LIST, so `{0[2]}` on a pattern with
    no group 2 is `IndexError: list index out of range` - a property of the pattern and the template,
    not of the matcher. `regex.subf('abcdefgh', '{0[2]}...', 'abcdefgh')` raises it with no verb, no
    fuzzy section and no reversal anywhere in sight.

    THE TEMPLATE IS ONLY RENDERED WHERE SOMETHING MATCHED, which is the whole mechanism: on both
    rows the twin replaced NOTHING, so it never rendered the template and never reached the question.
    Reading "the twin answered" off it filed a bad template as an engine fault. A twin that DID
    replace proves the template is fine for this pattern, and then the row's own raise is a real
    finding again - so this is a narrowing of one predicate and not an exclusion of `sub` rows.

    Applied to the `error` limb ONLY. A twin that completes a scan with nothing to replace has still
    done the searching, so it remains a perfectly good answer to "does this row hang".
    """
    if free is None:
        return False
    if kind == "error" and free.get("kind") == "sub":
        return free.get("count", 0) > 0
    return True


def _control_violations(recorded: dict) -> list[str]:
    """The invariants decidable from the ablation twins `_CONTROLS` already recorded."""
    violations: list[str] = []
    outcome = recorded["outcome"]
    kind = outcome["kind"]

    # `no-fault-where-a-twin-answers` - ORACLE-INVARIANTS.md group H, and the five ledger entries
    # that are not "two answers disagree" but "one door falls over while another answers": 9, 6, 10,
    # 14, 18. A control's key is only written when the twin ANSWERED (`_ANSWERED`, above), so the
    # key being present is the whole of the second half of this check.
    #
    # A COMPILE-TIME REJECTION IS NOT A FAULT and is excluded by `whileMatching`. Upstream deciding
    # a pattern is invalid is an answer, and a control that removes a construct can legitimately
    # make an invalid pattern valid. Only a call that got as far as matching and then raised, timed
    # out, or exhausted the interpreter is the shape the ledger entries above are.
    #
    # AND A TIMEOUT BESIDE A RANKING FLAG IS NOT A FAULT EITHER - the narrowing S52c's first
    # three-seed wave forced, where five of the seven firings were one shape: a `(?b)` row that
    # spent its whole ten seconds while the same row without `(?b)` answered. `BESTMATCH` is
    # DOCUMENTED to do more work - "By default, fuzzy matching searches for the first match that
    # meets the given constraints ... The BESTMATCH flag will make it search for the best match
    # instead" (upstream/README.rst:592) - and POSIX's leftmost-longest has to see every match at a
    # position before it can pick the longest. Taking either flag away leaves an engine that may
    # stop at the first acceptable answer, so the twin finishing where the flagged row does not is
    # a COST difference and not a contradiction.
    #
    # THE OTHER TWO CONTROLS ARE THE OPPOSITE AND KEEP THE TIMEOUT CASE, which is the whole reason
    # this is a narrowing rather than "drop timeouts". `(?>` to `(?:` and `(*SKIP)` to `(*PRUNE)`
    # both REMOVE pruning, so the twin explores at least as much as the original: a row that hangs
    # WITH the pruning construct and finishes without it cannot be explained by cost. That is
    # exactly ledger entry 10 - `(*SKIP)` inside an atomic group after an optional item loops for
    # ever - and narrowing any wider would have thrown that calibration away.
    faulted = kind in ("timeout", "resource") or (kind == "error" and outcome.get("whileMatching"))
    twins = (
        _PRUNING_CONTROLS if kind == "timeout" else tuple(key for key, _ in _CONTROLS)
    )
    if faulted and any(_twin_answered(recorded.get(key), kind) for key in twins):
        violations.append("no-fault-where-a-twin-answers")

    for key, invariant in (
        ("bestmatchFreeOutcome", "bestmatch-no-worse"),
        ("posixFreeOutcome", "posix-chooses-among-flagless-answers"),
    ):
        free = recorded.get(key)
        if free is not None:
            violations += _choosing_flag_violations(invariant, outcome, free)

    return violations


def _record_row_and_its_control_answers(regex, row: dict) -> dict:
    """Records one row, and the same row again with each construct a control takes away."""
    violations: list[str] = []
    recorded = _record_row(regex, row, violations)
    violations += _structural_violations(recorded)

    pattern = row["pattern"]
    flags = int(row.get("flags", 0))
    for key, without in _CONTROLS:
        changed = without(row, pattern, flags)
        if changed is None:
            continue

        free = _record_row(regex, changed)["outcome"]

        # An unanswerable second question is recorded as unasked, which is `searchOnlyPartial`'s
        # rule. A row upstream errors, times out or exhausts itself on WITHOUT the construct says
        # nothing about what the construct did, and leaving the key off makes the entry not apply -
        # the direction that reports a row rather than hiding it.
        if free["kind"] in _ANSWERED:
            recorded[key] = free

    violations += _control_violations(recorded)
    if violations:
        # Sorted and de-duplicated: a scan whose every match breaks one invariant is ONE candidate
        # to triage, not forty, and the field is a set of ids by contract. The detail a triage needs
        # is on the row itself - the groups, the counts, the twins - so nothing is lost by not
        # writing it twice.
        recorded["selfContradiction"] = sorted(set(violations))

    return recorded


def _compile_upstream(regex, recorded: dict, pattern: str, flags: int, named_lists: dict):
    """Compiles one row's pattern, with the required-string prefilter off where a generator wants it.

    The interception is at ``regex._regex.compile`` - the boundary between upstream's Python compiler
    and its C matcher - because that is the one place both values pass through and neither is
    reachable afterwards: ``_regex.compile`` bakes them into the pattern object. Matching then needs
    no wrapper at all.

    ``cache_pattern=False`` so neither the lookup nor the store touches upstream's own cache
    (``_main._compile``, ``cache_it``). Without it a prefilter-free pattern object could be handed
    back to another generator's row, or an ordinary cached one handed back to this one, and either
    way the row would not be recorded against what its tag says.
    """
    if recorded["generator"] not in PREFILTER_FREE_GENERATORS:
        return regex.compile(pattern, flags, **named_lists)

    recorded["oracle"] = "prefilter-free"
    inner = regex._regex.compile

    def without_required_string(*args):
        args = list(args)
        args[_REQ_OFFSET_ARG] = -1
        args[_REQ_CHARS_ARG] = None
        return inner(*args)

    regex._regex.compile = without_required_string
    try:
        return regex.compile(pattern, flags, cache_pattern=False, **named_lists)
    finally:
        regex._regex.compile = inner


def _record_row(regex, row: dict, violations: list | None = None) -> dict:
    """Runs one row against upstream and returns it with its recorded outcome attached.

    ``violations`` collects the one metamorphic invariant that cannot be read back off the recorded
    row - `captures-are-the-texts-of-spans`, which needs the live match object. Left ``None`` by the
    control twins and by ``_self_check``, whose answers are not the row being judged.
    """
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

    # The row's own deadline, and part of the QUESTION rather than of the answer - see
    # `_generate_timeout`. Absent on every generator but `timeout`, and then the blanket
    # ROW_TIMEOUT_SECONDS applies as a safety net rather than as a question.
    #
    # Threaded through the PRIMARY question only, and that is sufficient rather than sloppy: every
    # second fact below (`scanMatches`, `leakFreeFuzzy`, `searchOnlyPartial`, `anchoredScan`) is
    # reached only AFTER upstream answered, because a TimeoutError returns `timed_out()` from the
    # call itself. A row that runs out of its budget reaches none of them, so none of them can spend
    # ten seconds on a row whose budget is a quarter of one.
    deadline = row.get("timeout")
    if deadline is not None:
        deadline = float(deadline)
        recorded["timeout"] = deadline
    else:
        deadline = ROW_TIMEOUT_SECONDS

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

    # Part of the QUESTION, not of the answer: the same pattern and subject give different results
    # with it and without it, so it is recorded on the row and the consumer reads it back. Only the
    # three single-match operations take it - upstream's findall refuses it outright ("unused keyword
    # argument 'partial'", measured 2026-09-12) and this recorder has no finditer-partial row shape.
    partial = bool(row.get("partial", False))
    if partial:
        if operation not in OPERATIONS:
            raise SystemExit(
                f"operation {operation!r} cannot be asked with partial=True (pattern {pattern!r}); "
                f"only {OPERATIONS} take it."
            )
        recorded["partial"] = True

    # The slice, also part of the question. Absent means the whole subject, which is what every
    # generator before S31 asks. It matters to partial matching more than to anything else: half of
    # upstream's partial arms are bounded by `slice_start`/`slice_end` and half by
    # `text_start`/`text_end`, and the two are only distinguishable once the slice is narrower than
    # the subject. Raised by the S31 blind review, which found `(?r)a(bc)*` over `'abc'[1:1]` -
    # a partial upstream, no match here - on an axis no generator could reach.
    # A RECORDED row carries its slice TWICE - `pos`/`endpos` in UTF-16 and `codepointSlice` in
    # codepoints - and `--rows` reads codepoints. Both halves of what `--rows` is for run through
    # here and they pull opposite ways:
    #
    #   round-trip   a recorded row fed straight back is how an entry's `Example` is made, and
    #                re-reading its UTF-16 pair as codepoints records a DIFFERENT SLICE. Measured
    #                2026-09-15 on S52's seed 20260915 row 105880, whose subject leads with astral
    #                characters: the slice came back [2, 10] where the wave recorded [1, 7], and the
    #                answer with it. The three sliced `Example` rows already in
    #                `ExpectedDivergences.cs` survived only because nothing astral sits before their
    #                slices, which is luck and not a rule.
    #   minimisation cutting a row means EDITING the slice and re-recording, and a reader cuts
    #                whichever of the two fields they looked at first.
    #
    # `codepointSlice` WINS WHEREVER IT IS PRESENT, because it is right on two of the three ways a
    # row gets here and `pos`/`endpos` is right on one:
    #
    #   fed back unchanged   codepointSlice right, pos/endpos wrong (UTF-16 read as codepoints)
    #   subject cut          codepointSlice right - it indexes the NEW subject - pos/endpos now stale
    #   slice cut            whichever field the reader edited
    #
    # SO THE ONE SHARP EDGE LEFT IS A SLICE CUT MADE IN `pos`/`endpos` OF A RECORDED ROW: it is
    # silently ignored, and the row records the un-cut slice. Cut `codepointSlice` instead, or delete
    # it and give `pos`/`endpos` in CODEPOINTS, which is the unit a hand-written `--rows` row uses.
    # Both blind passes of S52 sitting 8 hit this from opposite sides - one reproduced the silent
    # discard, the other reproduced a legitimate subject cut being refused by a guard written to stop
    # it - which is what says the two cases are indistinguishable here rather than merely unhandled.
    # The real fix is for the recorder to stop echoing the UTF-16 pair under the same key names the
    # INPUT uses (`utf16Slice`, say); that is a wave-format change reaching the C# consumer, every
    # committed rows file and every wave on disk, so it is a slice of its own and not this one's.
    sliced = row.get("codepointSlice")
    pos = sliced[0] if sliced is not None else row.get("pos")
    endpos = sliced[1] if sliced is not None else row.get("endpos")
    # `sub` and `subf` joined the three single-match operations in S53b, when `Replace` and
    # `ReplaceFormat` gained a `beginning`/`length` pair. `split` still cannot take one, because
    # upstream's `pattern_split` has no pos/endpos at all - its kwlist is string, maxsplit,
    # concurrent, timeout - and neither do the `finditer` shapes, which this recorder asks through
    # `findall`-shaped calls.
    _SLICEABLE = OPERATIONS + SUB_OPERATIONS
    if pos is not None or endpos is not None:
        if operation not in _SLICEABLE:
            raise SystemExit(
                f"operation {operation!r} carries no pos/endpos in this recorder (pattern "
                f"{pattern!r}); only {_SLICEABLE} do."
            )
        pos = 0 if pos is None else int(pos)
        endpos = len(subject) if endpos is None else int(endpos)

        # A negative index is upstream's slice convention, not an error: `regex` counts it back from
        # the end, so `search('abc', 0, -1)` searches [0, 2) and `search('bca', -3, 3)` searches all
        # of it (measured 2026-09-12). Clamping one to zero recorded a slice upstream was never
        # asked about, which is a divergence invented by the recorder. Raised by the S31 second
        # blind pass. Resolved here, once, so the recorded pair and the consumer agree with what
        # Python was actually handed below.
        length = len(subject)
        pos = max(0, min(pos + length if pos < 0 else pos, length))
        endpos = max(0, min(endpos + length if endpos < 0 else endpos, length))

        # Recorded in UTF-16, like every other index in a row: upstream's pos/endpos are codepoint
        # indices, and handing the consumer those would narrow OUR slice somewhere else entirely on
        # an astral subject - a fake divergence about encoding rather than a real one about
        # partial matching. The untranslated pair goes in beside them, unread, for the same reason
        # `codepointSpan` does: it makes the translation visible in the file rather than trusted.
        slice_offsets = _utf16_offsets(subject)
        recorded["pos"] = slice_offsets[pos]
        recorded["endpos"] = slice_offsets[endpos]
        recorded["codepointSlice"] = [pos, endpos]

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
            return exhausted(type(e).__name__)
        recorded["codepointSpan"] = None
        recorded["outcome"] = {
            "kind": "error",
            "exception": type(e).__name__,
            # regex.error's str() appends the position; .msg is the text our
            # FuzzyRegexParseException.Message carries, which is what the corpus compares too.
            "message": e.msg if isinstance(e, regex.error) else str(e),
            "whileMatching": while_matching,
        }

        # S52. An exception is not a span either, and upstream raising WHILE MATCHING means it had
        # already found the match that a span-keyed entry needs to read - `subfn('(?i)I', '{1}', 'ı')`
        # matches the dotless i by a `T` row and only then discovers the template names a group that
        # does not exist. A rejection raised while COMPILING never matched anything, so it is not
        # asked. See `_needs_a_scan_to_be_classified`.
        if while_matching and _may_turn_on_a_turkic_rule(pattern, subject, flags):
            scanned = _scan_matches(compiled, subject)
            if scanned is not None:
                recorded["scanMatches"] = scanned

        return recorded

    def exhausted(exception: str) -> dict:
        """The recorded answer when upstream ran out of memory, stack or range.

        THE SAME SHAPE AS ``timed_out`` AND FOR THE SAME REASON, which is why it is a kind of its own
        rather than an ``error``. Upstream did not reject the pattern; it ran into a limit of the
        interpreter, so a larger heap would change the answer and "upstream rejected this input" is
        simply not true of the row. Filing it as a rejection would score a port that answers as
        diverging and a port that also blows up as agreeing - both halves backwards, exactly as the
        ``timeout`` docstring says. The consumer skips it the way it skips an unsupported row and
        counts it separately, so a generator that starts drawing these shows up in the summary line.

        UNTIL S43 THIS ABORTED THE WHOLE WAVE with a SystemExit, and the reasoning behind that was
        only ever about not RECORDING the row as an outcome - never about aborting being necessary.
        Aborting means one unanswerable row throws away every other row in the run, which is the same
        failure S40a fixed for the hanging row by adding the deadline.

        The composed fuzzy wave makes these routine rather than rare, and the cause is a REPEAT WHOSE
        BODY CAN MATCH EMPTY sitting beside a fuzzy section - the same no-progress mechanism
        INTERACTION_FUZZY_WRAPPERS describes for a self-recursive call. Measured 2026-09-13 on
        'bb.a\\r.':

            (?b)(?P<g1>\\p{L}*)+?(?:ab){e<=1}     MemoryError in 1.78s
            (?e)(?P<g1>\\p{L}*)+?(?:ab){e<=1}     MemoryError in 1.76s
                (?P<g1>\\p{L}*)+?(?:ab){e<=1}     MemoryError in 1.75s   <- no ranking flag at all
                (?P<g1>\\p{L}+)+?(?:ab){e<=1}     (0, 2) in 0.00s        <- body must consume
                (?P<g1>\\p{L}*)+?ab               None   in 0.00s        <- no fuzzy section

        A FIRST DRAFT OF THIS DOCSTRING BLAMED `(?b)`, and its blind review killed that with the
        middle three rows: `(?e)` and no flag at all blow up identically, so BESTMATCH is not the
        cause and the wave would have carried a false attribution into three files. It is upstream's
        551/554 resource-blowup family, which Phase 6 triages; it is not something a generator can
        reliably avoid drawing, and it is not something one row should be able to destroy a six-seed
        run over.
        """
        recorded["codepointSpan"] = None
        recorded["outcome"] = {"kind": "resource", "exception": exception}
        return recorded

    def timed_out() -> dict:
        """The recorded answer when upstream ran out of its deadline.

        A KIND OF ITS OWN, not an ``error``. Upstream has no answer to compare against - it did not
        reject the pattern, it simply never finished - so filing this as a rejection would score any
        port that *does* answer as diverging, and a port that also hangs as agreeing. The consumer
        skips the row the way it skips ``unsupported`` and counts it separately, so a generator that
        starts drawing hanging shapes is visible in the summary line rather than silent.

        Not dropped, either. A row a generator emits and nobody records is a shape that quietly
        stops being tested, which is the failure S38's and S39's controls each hit from a different
        direction.
        """
        recorded["codepointSpan"] = None
        recorded["outcome"] = {"kind": "timeout", "seconds": deadline}
        return recorded

    try:
        compiled = _compile_upstream(regex, recorded, pattern, flags, named_lists)
    except Exception as e:  # noqa: BLE001 - the exception *is* the recorded answer
        return failed(e, while_matching=False)

    if operation in SUB_OPERATIONS:
        method = compiled.subn if operation == "sub" else compiled.subfn
        # In CODEPOINTS, as every pos/endpos handed to Python is: `recorded["pos"]` is the UTF-16
        # translation for the consumer and these two locals are what upstream indexes with. Absent
        # means the whole subject, which is what `sub`'s own defaults mean.
        sub_slice = {} if pos is None else {"pos": pos, "endpos": endpos}
        try:
            text, made = method(
                recorded["template"], subject, count=recorded["count"], timeout=deadline, **sub_slice
            )
        except TimeoutError:
            return timed_out()
        except Exception as e:  # noqa: BLE001
            return failed(e, while_matching=True)

        recorded["codepointSpan"] = None
        recorded["outcome"] = {"kind": "sub", "text": text, "count": made}

        # S52's third second-fact, for the family that needs the WHOLE scan rather than the replaced
        # prefix of it: `subMatches` below is truncated to the row's count, so a row that replaces
        # nothing carries an empty list where the divergence is a match upstream made and this port
        # did not. See `_needs_a_scan_to_be_classified`.
        if _needs_a_scan_to_be_classified(operation, pattern, subject, flags):
            scanned = _scan_matches(compiled, subject)
            if scanned is not None:
                recorded["scanMatches"] = scanned

        # WHERE UPSTREAM REPLACED, for a `(*SKIP)` pattern only, and a SECOND FACT ABOUT UPSTREAM in
        # the sense `anchoredScan` above is - recorded, never compared.
        #
        # A `sub` outcome is a string and a count, so a row that diverges on one carries no match
        # positions for anything to refute. That is the whole reason seed 20260913's row 116388 could
        # not be classified with the other rows of its family (S40c's handover, S40d's scope): the
        # `$` tell `overlapped-skip-extra-match-reversed` reads needs a match END to read it at.
        # `subn` walks the same scanner `finditer` does, so asking the scan separately gives the
        # spans the substitution used - measured on that row, where upstream replaces 3 times and
        # `finditer` gives exactly 3 spans (tools/probes/upstream-reversed-skip-scan-shapes.py).
        #
        # The consumer demands that this list is exactly as long as the recorded count before it
        # reads anything from it, so a row where the two questions disagree is reported rather than
        # classified.
        if "(*SKIP)" in pattern:
            try:
                replaced_at = list(compiled.finditer(subject, timeout=ROW_TIMEOUT_SECONDS))
            except Exception:  # noqa: BLE001 - an unanswerable second question is recorded as unasked
                return recorded

            # Upstream's own limit semantics, which are not a plain slice: zero is NO LIMIT and a
            # NEGATIVE count replaces nothing at all - `regex.subn('a(*SKIP)', 'X', 'aaa', count=-1)`
            # is `('aaa', 0)` where `count=0` is `('XXX', 3)`. `SUB_COUNTS` draws -1, so slicing by
            # the raw count would record every match but the last for a row upstream never touched.
            # Found by S40d's blind review; eight rows of a 6000-row seed-7 `verbs` wave carried a
            # non-empty list for a substitution that replaced nothing.
            limit = recorded["count"]
            if limit < 0:
                replaced_at = []
            elif limit > 0:
                replaced_at = replaced_at[:limit]

            offsets = _utf16_offsets(subject)
            recorded["subMatches"] = [
                dict(_describe_match(compiled, m, offsets), codepointSpan=list(m.span(0)))
                for m in replaced_at
            ]

        return recorded

    if operation in ITER_OPERATIONS:
        try:
            if operation == "split":
                parts = compiled.split(subject, maxsplit=recorded["count"], timeout=deadline)
            else:
                overlapped = operation == "finditer-overlapped"
                found = list(compiled.finditer(subject, overlapped=overlapped, timeout=deadline))
        except TimeoutError:
            return timed_out()
        except Exception as e:  # noqa: BLE001
            return failed(e, while_matching=True)

        recorded["codepointSpan"] = None
        if operation == "split":
            # None stays None: upstream puts it where a capturing group took no part in a match,
            # which our `string?[]` spells as null. Losing the distinction between that and an
            # empty string is exactly the mistake Regex.Split makes.
            recorded["outcome"] = {"kind": "split", "parts": parts}

            # A list of strings is not a span either. See `_needs_a_scan_to_be_classified`.
            if _needs_a_scan_to_be_classified(operation, pattern, subject, flags):
                scanned = _scan_matches(compiled, subject)
                if scanned is not None:
                    recorded["scanMatches"] = scanned
        else:
            offsets = _utf16_offsets(subject)
            described = [
                # The untranslated codepoint span per match, for the same reason the top-level
                # one exists: it makes the recorder's index translation visible in the file
                # rather than merely trusted. The consumer never reads it.
                dict(
                    _describe_match(compiled, m, offsets, violations),
                    codepointSpan=list(m.span(0)),
                )
                for m in found
            ]
            recorded["outcome"] = {"kind": "matches", "matches": described}

            leak_free = _leak_free_fuzzy(compiled, subject, offsets, described, partial)
            if leak_free is not None:
                recorded["leakFreeFuzzy"] = leak_free
            # OVERLAPPED ONLY, and reversed only where the pattern reads nothing at the end of the
            # subject - each exclusion is a case where the walk below cannot ask upstream the same
            # question the scanner asks, so a recorded answer would be a third opinion rather than a
            # second one. See its docstring.
            reverse = (compiled.flags & _REVERSE_FLAG) != 0
            if (
                overlapped
                and "(*SKIP)" in pattern
                and not (reverse and _reads_the_end_of_the_subject(pattern))
            ):
                # None where a step ran out of its deadline, and then the key is left OFF the row
                # entirely rather than written as a short walk. A truncated walk is not upstream's
                # answer to the scan, and `overlapped-skip-stale-slice` demands the walk agree with
                # this port match for match - so a short one would classify a row the entry has no
                # business classifying. Absent means the entry does not apply and the row is
                # reported, which is the direction that cannot hide a defect.
                walk = _anchored_scan(compiled, subject, offsets, reverse)
                if walk is not None:
                    recorded["anchoredScan"] = walk
        return recorded

    # Pattern.match/search/fullmatch take (string, pos, endpos, concurrent, partial, timeout), so
    # the slice goes positionally and `partial` by keyword.
    kwargs = {"timeout": deadline}
    if partial:
        kwargs["partial"] = True
    args = (subject,) if pos is None else (subject, pos, endpos)

    try:
        match = getattr(compiled, operation)(*args, **kwargs)
    except TimeoutError:
        return timed_out()
    except Exception as e:  # noqa: BLE001
        return failed(e, while_matching=True)

    cut = _cut_subject_outcome(compiled, subject, operation, pos, endpos, partial)
    if cut is not None:
        recorded["cutSubjectOutcome"] = cut

    if match is None:
        recorded["codepointSpan"] = None
        recorded["outcome"] = {"kind": "nomatch"}
        return recorded

    recorded["codepointSpan"] = list(match.span(0))
    offsets = _utf16_offsets(subject)
    described = _describe_match(compiled, match, offsets, violations)
    recorded["outcome"] = {"kind": "match", **described}

    leak_free = _leak_free_fuzzy(
        compiled, subject, offsets, [dict(described, codepointSpan=list(match.span(0)))], partial
    )
    if leak_free is not None:
        recorded["leakFreeFuzzy"] = leak_free

    # One extra question, asked only of a SEARCH that answered a partial, and recorded as a fact
    # about upstream rather than compared against anything: does upstream's own `match` answer the
    # same partial over the span the search reported?
    #
    # `search_start` (upstream/src/_regex.c:8385) is consulted on a search and nowhere else, and
    # every one of its scanners can report RE_ERROR_PARTIAL of its own where the slow path has no
    # such arm. So "search says partial here, match says None here" is upstream's two doors
    # disagreeing, which is the signature of the prefilter family the consumer's
    # `search-start-partial` entry accounts for. "Both doors say partial" is the matcher's own
    # answer, and a port that misses THAT has a bug.
    #
    # Added by S33 because without it the entry could not tell the two apart, and a control proved
    # it: mutating `IsStringTestPartial`'s reversed bound back to `text_start` - the exact defect
    # S33 fixed - produced ZERO reported divergences over 2,000 rows at three seeds, because the one
    # row that caught it was being classified as the prefilter family. With this field the same
    # control fires. Measured 2026-09-12; six prefilter rows at seed 31 all answer None from `match`
    # and the real-bug row answers the same partial (.scratch/discriminator.py).
    if operation == "search" and partial and match.partial:
        start, end = match.span(0)
        try:
            same = compiled.match(subject, start, end, partial=True, timeout=ROW_TIMEOUT_SECONDS)
        except Exception:  # noqa: BLE001 - an unanswerable second question is recorded as unasked
            recorded["searchOnlyPartial"] = False
        else:
            recorded["searchOnlyPartial"] = same is None or not same.partial

    return recorded


def _describe_match(compiled, match, offsets: list[int], violations: list | None = None) -> dict:
    """One match's groups and its two last-group fields, in UTF-16 ``[Index, Length]``.

    Shared by the single-match operations and by ``finditer``, whose answer is a list of these, so
    the two shapes cannot drift apart.

    ``violations`` is where `captures-are-the-texts-of-spans` is collected, and it is collected HERE
    rather than in `_structural_violations` because it is the one FREE invariant whose evidence does
    not survive into the row: the row records a capture's SPAN and never its text. Passed by the
    primary question's two call sites only - `subMatches` and `_anchoredScan` are second facts about
    upstream rather than the answer being judged, so they leave it ``None``.
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

    # `captures-are-the-texts-of-spans` - ORACLE-INVARIANTS.md group B, and NOT the vacuous check it
    # reads as. Upstream's `match_spans` and `match_get_captures_by_index` walk the SAME
    # `group->captures[i]` array (upstream/src/_regex.c:19115 and :19174), but the captures arm
    # renders each one through `get_slice(self->substring, start - self->substring_offset, ...)`,
    # so this is the only check that exercises that offset bookkeeping at all. `spans()` cannot see
    # a wrong `substring_offset`; this can.
    #
    # NO POSIX GUARD, unlike `fuzzyChanges` below, and that is measured rather than assumed. Reading
    # `fuzzy_changes` on a POSIX fuzzy match that spent an error kills the interpreter (ledger 9),
    # and `tools/probes/upstream-posix-fuzzy-safe-attributes.py` never asked about `captures`.
    # Section 6 of `tools/probes/upstream-free-tier-invariant-grounds.py` now does, over three POSIX
    # fuzzy patterns including a repeated capturing group, and every read returns normally
    # (regex 2026.9.10, 2026-09-16).
    if violations is not None:
        for number in range(compiled.groups + 1):
            spans = match.spans(number)
            captures = match.captures(number)
            if len(captures) != len(spans) or any(
                text != match.string[start:end] for text, (start, end) in zip(captures, spans)
            ):
                violations.append("captures-are-the-texts-of-spans")
                break

    described = {
        "groups": groups,
        # Neither is derivable from the groups: 'lastindex' is the group that *closed* last, so
        # regex.match('((a))', 'a').lastindex is 1 even though groups 1 and 2 both succeed with the
        # same span, and 'lastgroup' names the last *named* group even when an unnamed one succeeded
        # later. None becomes -1 and null, which is what Match.LastGroupNumber and
        # Match.LastGroupName report.
        "lastIndex": -1 if match.lastindex is None else match.lastindex,
        "lastGroup": match.lastgroup,
        # Not derivable from the span either: a partial match and a complete match of the same text
        # are the same span and different answers. Always present, and always False on a row that
        # did not ask for a partial, so every generator's rows now also assert "not partial".
        "partial": bool(match.partial),
    }

    # The fuzzy half, appended only when the match actually used an error, so every row recorded
    # before S38 - and every row of every generator that is not fuzzy - renders exactly as it did.
    # The S31 `partial` precedent.
    substitutions, insertions, deletions = match.fuzzy_counts
    if substitutions or insertions or deletions:
        # NEVER `match.fuzzy_changes` ON A POSIX PATTERN. Reading it on a POSIX fuzzy match that
        # spent an error is an access violation (0xC0000005 on Windows, SIGSEGV under Git Bash) that
        # kills this process - not an exception, so no `except` clause below can see it, and
        # `--rows` over one such row exits 139 and writes no output file at all. Ledger entry 9.
        #
        # The counts on the same match answer correctly, and so does every other read this function
        # makes - `span(n)` and `spans(n)` over the whole group range, `lastindex`, `lastgroup` and
        # `partial` - each measured in its own child process by
        # `tools/probes/upstream-posix-fuzzy-safe-attributes.py` (regex 2026.9.10, 2026-09-14). So a
        # POSIX row records its counts and omits its positions, and the consumer drops the positions
        # from BOTH sides of the comparison rather than comparing ours against nothing. What that
        # costs is the change positions on those rows, which upstream has no answer for anyway.
        #
        # The guard is here, at the one funnel every recorded match passes through - the single-match
        # door, `finditer`, a `(*SKIP)` substitution's `subMatches` and `_anchored_scan` all call this
        # function - rather than at each of those call sites, because missing one would kill a wave
        # rather than fail a test.
        #
        # Keyed off POSIX and not off "this match spent errors", which is the faulting condition:
        # nothing readable off the pattern OR the subject predicts a spent error, since POSIX
        # leftmost-longest can stretch an apparently exact row into spending one -
        # `(?p)(?:abc){e<=1}` over 'abcd' looks exact and faults
        # (`tools/probes/upstream-posix-fuzzy-spent-error.py`).
        described["fuzzyCounts"] = [substitutions, insertions, deletions]
        if compiled.flags & _POSIX_FLAG:
            return described

        sub_positions, ins_positions, del_positions = match.fuzzy_changes
        described["fuzzyChanges"] = {
            "substitutions": [_utf16_index(offsets, p) for p in sub_positions],
            "insertions": [_utf16_index(offsets, p) for p in ins_positions],
            # A deletion's position is NOT a position in the subject: match_fuzzy_changes
            # (upstream/src/_regex.c:20555-20558) adds one per deletion recorded before it, so what comes
            # out is where the missing character would sit in a string with them all put back. Only
            # the part before that shift is a real subject position, so the shift is undone, the
            # position translated, and the shift re-applied - a deletion is one CHARACTER wide
            # whether or not the characters around it are astral, and our engine counts it the same
            # way.
            "deletions": [_utf16_index(offsets, p - i) + i for i, p in enumerate(del_positions)],
        }

    return described


def _cut_subject_outcome(compiled, subject: str, operation: str, pos, endpos, partial: bool) -> dict | None:
    """Upstream's answer to the same call over ``subject[pos:endpos]`` AS A SUBJECT IN ITS OWN RIGHT.

    A SECOND FACT ABOUT UPSTREAM, never compared against anything, exactly as ``searchOnlyPartial``
    and ``bestmatchFreeOutcome`` are. The spans are shifted back into the full subject before they
    are written, so the consumer compares them with this port's sliced answer directly.

    WHAT IT IS FOR. The owner ruled on 2026-09-15 (ledger entry 24, slice S52d) that a reversed match
    asked with ``partial=True`` has run out of text when it reaches ``pos``. The argument for that
    ruling is that a slice start behaves like a string start, and that has a consequence which can be
    checked rather than described: the slice ``[pos, endpos)`` must answer what the cut subject
    answers, shifted. This field is that answer, so the consumer's entry can pin "this port gives the
    ruling's own consequence" instead of describing a shape.

    Upstream cannot give it over the slice, which is the divergence. ``init_match`` sets
    ``text_start`` to 0 (``upstream/src/_regex.c:18442``) and every node handler reads it, so
    upstream's sliced answer comes from a later door: its search retreats until ``search_start``
    (``:8400-8405``) reports a partial positioned at ``slice_start``, and ``:18185-18190`` then
    overwrites the match position with ``slice_start``. Upstream's span is therefore
    ``(slice_start, match_pos)`` of whichever attempt was current when the retreat ran out, and this
    port's is the one the ruling names.

    THE TWO ANSWERS ARE NOT ALWAYS THE SAME, AND THAT IS THE POINT. ``\\b``, ``\\B`` and lookbehind
    still read the character before ``pos`` - Python ``re``'s rule, which the ruling left alone - so a
    pattern that looks across the slice start answers differently once the text before it is gone.
    Measured by ``tools/probes/s57b-cut-subject-door.py`` over the three-seed 6000-row gate of
    2026-09-21: of the 853 reversed partial rows with a non-zero slice that those reports NAME - the
    divergences and the rows the entry below already accounts for, not the rows that simply agreed -
    795 give this port's answer and 58 do not, and every one of the 58 is a row where upstream
    answers no match over the slice. The consumer keeps its separate limb for those.

    ASKED ONLY OF A REVERSED PARTIAL OVER A NON-ZERO SLICE. ``text_end`` IS the slice end on every
    upstream path, so the forward side never held two rules and the door would tell nothing apart.
    """
    if not (partial and pos and endpos is not None and operation in ("match", "search", "fullmatch")):
        return None
    if not compiled.flags & _REVERSE_FLAG:
        return None

    try:
        cut = getattr(compiled, operation)(
            subject[pos:endpos], 0, endpos - pos, partial=True, timeout=ROW_TIMEOUT_SECONDS
        )
    except Exception:  # noqa: BLE001 - an unanswerable second question is recorded as unasked
        return None

    if cut is None:
        return {"kind": "nomatch"}
    # `offsets[pos:]` indexes the CUT subject's codepoints and yields the FULL subject's UTF-16
    # positions, which is the shift, done in one step rather than as an add afterwards.
    return {"kind": "match", **_describe_match(compiled, cut, _utf16_offsets(subject)[pos:])}


def _leak_free_fuzzy(compiled, subject: str, offsets: list[int], matches: list[dict], partial: bool) -> list | None:
    """Upstream's own fuzzy half for each recorded match, asked again ANCHORED at the span it reported.

    A SECOND FACT ABOUT UPSTREAM, never compared against anything, exactly as ``anchoredScan``,
    ``searchOnlyPartial`` and ``bestmatchFreeOutcome`` are. One entry per recorded match, in the
    recorded order; ``None`` where upstream would not answer the question, and the key is left off the
    row entirely where there was no question to ask.

    WHAT IT IS FOR. Ledger entry 11 mechanism A: ``start_match`` clears ``state->fuzzy_counts`` and
    leaves ``state->fuzzy_changes`` alone (``upstream/src/_regex.c:11790-11792``), so an attempt that
    was abandoned without unwinding leaves its entries at the BOTTOM of the change stack, and
    ``match_fuzzy_changes`` then reports the FIRST ``sum(fuzzy_counts)`` entries (``:20522``) - the
    stale ones, DISPLACING the winning attempt's. This port clears the list beside the counts, so from
    S47 on every row where upstream leaks is a divergence in which the two engines agree on the span,
    the groups AND the fuzzy counts, and differ only over where the errors were spent. Nothing on our
    side of the comparison can tell that from a port that computed a position wrongly.

    What can tell them apart is upstream's OWN answer with the leak taken away. ``match(pos=start,
    endpos=end)`` makes the winning attempt upstream's FIRST attempt, so there is no earlier attempt
    to have left anything on the stack. Measured over the 19 rows of the three-seed 6000-row gate on
    2026-09-14 (``.scratch/anchored.py``, reproduced in the S47 closing notes): on EVERY diverging
    match upstream could be asked about this way, its leak-free answer is this port's answer exactly.

    IT CANNOT ALWAYS BE ASKED, and that is why ``None`` is a recorded value rather than a reason to
    omit the row. Anchoring at the reported span breaks three shapes: a fuzzy section inside a
    LOOKAHEAD, which has to read past ``endpos``; a ``\\K``, whose reported start is not where the
    attempt began; and the second match of a scan at a position an earlier match already used. Eight
    of the 23 diverging matches in that gate are one of those. The consumer's entry says what it does
    with them and how much weaker that arm is.

    :param compiled: The compiled pattern.
    :param subject: The subject.
    :param offsets: The subject's codepoint-to-UTF-16 table.
    :param matches: The recorded matches, each carrying its own ``codepointSpan``.
    :param partial: Whether the row asked for a partial match.
    :returns: One entry per recorded match, or ``None`` where no match had a fuzzy half to ask about.
    """
    # A POSIX row has no change positions on either side (ledger entry 9), so there is nothing here
    # for a second question to be about - and `_describe_match` would refuse to read them anyway.
    if compiled.flags & _POSIX_FLAG:
        return None

    asked: list = []
    any_question = False
    for described in matches:
        if "fuzzyChanges" not in described:
            asked.append(None)
            continue

        any_question = True
        start, end = described["codepointSpan"]
        try:
            again = compiled.match(subject, start, end, partial=partial, timeout=ROW_TIMEOUT_SECONDS)
        except Exception:  # noqa: BLE001 - an unanswerable second question is recorded as unasked
            again = None

        # A different span is a different match, so its errors say nothing about this one's.
        if again is None or list(again.span(0)) != [start, end]:
            asked.append(None)
            continue

        answer = _describe_match(compiled, again, offsets)
        asked.append(
            {
                "fuzzyCounts": answer.get("fuzzyCounts", [0, 0, 0]),
                "fuzzyChanges": answer.get(
                    "fuzzyChanges", {"substitutions": [], "insertions": [], "deletions": []}
                ),
            }
        )

    return asked if any_question else None


def _anchored_scan(compiled, subject: str, offsets: list[int], reverse: bool = False) -> list[dict] | None:
    """The same overlapped scan, asked of upstream one match at a time from a fresh state each step.

    A SECOND FACT ABOUT UPSTREAM, never compared against anything, and recorded only for a
    ``finditer`` row that is OVERLAPPED, whose pattern contains ``(*SKIP)``, and - if it is
    reversed - which reads nothing at the end of the subject. Only that
    verb moves ``slice_start``/``slice_end`` mid-attempt (``upstream/src/_regex.c:14553``), and
    nothing puts them back: ``init_match`` (``:3404``), ``do_match`` (``:18121``) and
    ``scanner_search_or_match`` (``:20874``) all leave them alone, and the only other writer is
    ``state_init`` (``:18438``), which runs once per scanner. So a scan of such a pattern carries
    whatever slice the previous match's last ``(*SKIP)`` left, and this walk is the same scan with
    that carry-over removed - every step a fresh ``search``, which is upstream's own single-shot
    door.

    What it is FOR. The consumer's ``ExpectedDivergences`` needs a discriminator, exactly as
    ``searchOnlyPartial`` is one for the partial family: without it a "``(*SKIP)`` scan whose two
    answers differ" predicate also swallows a genuine defect in this port's own scan. With it, an
    entry can demand that upstream's stateful scanner contradicts upstream's own matcher AND that
    this port agrees with the matcher.

    WHY OVERLAPPED ONLY, AND WHY A REVERSED ROW IS CONDITIONAL. Both exclusions were found by S34's
    blind review, which built a row the walk got wrong and showed the list absorbing an engine
    mutation because of it. Neither is tidiness: in each case the public API cannot ask upstream the
    same question its scanner asks, so a recorded answer would be a third opinion rather than a
    second one. S40d narrowed the second from "no reversed row" to "no reversed row whose pattern
    reads the end of the subject", because that refusal was never about reversal - see
    ``_reads_the_end_of_the_subject``.

    * **Non-overlapped needs ``must_advance``, and no Python call carries it.** After a zero-width
      match at *p* the scanner re-attempts AT *p* with ``must_advance`` set (``:20912``), which
      forbids another zero-width match there but still allows a longer one starting there. A plain
      ``search(subject, p)`` cannot express that, and ``search(subject, p + 1)`` skips the longer
      match. Measured: ``regex.compile('a??').finditer('aa')`` is
      ``(0,0) (0,1) (1,1) (1,2) (2,2)`` and a ``p + 1`` walk gives ``(0,0) (1,1) (2,2)``. The
      overlapped branch sets ``must_advance = FALSE`` and steps to ``match_pos + 1``, which
      ``search(subject, match_pos + 1)`` reproduces exactly.

    * **Reversed needs ``endpos``, and ``endpos`` truncates the subject.** A reversed scan is
      anchored by its end, so stepping it means moving ``endpos`` - and every assertion that
      reads the end of the subject changes meaning at a truncated one: the dollar anchor, the
      end-of-text escape and both word-boundary escapes. Measured: a reversed word-boundary
      pattern over 'bab' gives (3,3) (0,0) from finditer and (3,3) (2,2) (1,1) from the same
      walk, because positions 1 and 2 are boundaries only in a truncated 'bab'.

      **That is a property of the PATTERN, not of reversal, so S40d made the refusal read the
      pattern.** A reversed pattern holding no such item is walked, and the step is upstream's own:
      ``state->text_pos = state->match_pos - 1`` for a reversed overlapped scan
      (``upstream/src/_regex.c:20903``), where ``match_pos`` is the end a reversed attempt anchors
      at - so the next ``endpos`` is one before the last match's end. Measured 2026-09-13 on seven
      shapes including a zero-width one, and on four that DO read the end and differ:
      ``python tools/probes/upstream-reversed-walk-step.py``.

    WHAT WAS CHECKED AND DOES NOT MATTER, so the next reader does not re-derive it.
    ``search(subject, pos)`` sets ``slice_start`` to *pos* where the scanner leaves it at the
    row's own start, which looks like it should change an anchor's meaning and does not: the
    start-of-string and start-of-line predicates are bound by ``text_start``, not by
    ``slice_start`` (``try_match_START_OF_STRING``, ``:7373``), and so are the word-boundary escape and a
    lookbehind. Measured 2026-09-12: ``regex.compile('^a').search('ba', 1)`` and
    the same question asked with the start-of-string escape are both ``None``, with or without an
    ``endpos``.

    That is a checked list, not a proof. If a future row makes this walk disagree with the
    scanner for a reason that is not the carried slice, the direction is safe for the one entry
    that demands equality with the walk - a walk that differs for any reason makes the entry NOT
    apply, and the row is reported rather than classified.
    """
    found: list[dict] = []

    # One more step than there are positions is the most any correct walk can take, so a pattern
    # that somehow fails to advance stops here instead of hanging the recorder.
    limit = len(subject) + 2

    # Forward the walk anchors its START and sweeps it up; reversed it anchors its END and sweeps
    # that down. `pos` is whichever of the two this row moves.
    pos = len(subject) if reverse else 0

    while 0 <= pos <= len(subject) and len(found) < limit:
        try:
            match = (
                compiled.search(subject, 0, pos, timeout=ROW_TIMEOUT_SECONDS)
                if reverse
                else compiled.search(subject, pos, timeout=ROW_TIMEOUT_SECONDS)
            )
        except TimeoutError:
            # The walk is unfinishable, so there is no walk. See the caller: the key is left off
            # the row rather than written short.
            return None

        if match is None:
            break

        # The SAME shape a `matches` outcome's entries have, groups and all, so the consumer can
        # compare the whole rendering rather than only the spans. A stale slice shows up in a
        # CAPTURE as readily as in a whole-match span: at seed 314159 upstream's overlapped scan of
        # `((?:\p{L}(*SKIP))+)` reports group 1 as (0, 4) for a match spanning (1, 4), a capture
        # outside its own match, where this walk gives (1, 4).
        found.append(dict(_describe_match(compiled, match, offsets), codepointSpan=list(match.span(0))))

        # scanner_search_or_match's own overlapped step (:20903), which MatchState.AdvancePastMatch
        # ports: `match_pos + step`, one character past where the ATTEMPT ANCHORED. Forward that is
        # the match's start and the step is +1; reversed it is the match's end and the step is -1.
        pos = match.span(0)[1] - 1 if reverse else match.span(0)[0] + 1

    return found


# The two codepoints `CaseFolding.txt` marks `T` name, and so the only two a Turkic divergence can
# turn on. Kept here rather than spelled at the call site because the consumer's own copy of this
# list - `_turkicI` in ExpectedDivergences.cs - is what reads the field this gate decides to record,
# and the two have to say the same thing.
_TURKIC_I = "İı"


def _needs_a_scan_to_be_classified(operation: str, pattern: str, subject: str, flags: int) -> bool:
    """Whether this row's recorded answer will carry no spans AND a span-keyed entry may want them.

    A SECOND FACT ABOUT UPSTREAM in the sense ``subMatches`` and ``anchoredScan`` are - recorded,
    never compared - and the third of them, added by S52 for the same blind spot S40d hit from the
    substitution side.

    ``sub``, ``subf`` and ``split`` answer with a STRING (and a count, or a list of parts), and a row
    that upstream failed *while matching* answers with an exception. None of the four carries a match
    position, so a divergence on one gives a span-keyed entry in
    ``tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`` nothing to read and the row reddens the
    run however well its family is understood. Measured 2026-09-15 on the three-seed 2000-row wave of
    commit 58977bb, which was RED at all three seeds on exactly three rows - two of them
    ``turkic-default-folding`` in precisely these shapes:

        subn('(?i)I', 'X', 'ı')          upstream ('X', 1), this port ('ı', 0)
        split('(?i)(I)', 'aıb')          upstream ['a', 'ı', 'b'], this port ['aıb']
        subfn('(?i)I', '{1}', 'ı')       upstream IndexError while matching, this port ('ı', 0)

    GATED ON THE FAMILY, like ``subMatches``' own ``"(*SKIP)" in pattern``, because an extra
    ``finditer`` per row is not free and only one entry keys on spans where the answer has none.
    IGNORECASE has to be in force - without it no case data is consulted at all - and one of the two
    ``T`` codepoints has to be somewhere in the row, which is the same pair of clauses the consumer's
    predicate opens with. Widening this gate is a recorder change, not a predicate change; a row it
    refuses is reported rather than classified, which is the safe direction.
    """
    if operation not in LIMIT_OPERATIONS and operation not in SUB_OPERATIONS:
        # Every other operation either carries its own spans (`match`, `finditer`) or has none to
        # record. An error row reaches this function through its own call site below instead.
        return False

    return _may_turn_on_a_turkic_rule(pattern, subject, flags)


def _may_turn_on_a_turkic_rule(pattern: str, subject: str, flags: int) -> bool:
    """Whether IGNORECASE is in force and a `T` codepoint is anywhere in the row."""
    if not (flags & IGNORECASE or "(?i" in pattern):
        return False

    return any(c in _TURKIC_I for c in pattern) or any(c in _TURKIC_I for c in subject)


def _scan_matches(compiled, subject: str) -> list | None:
    """Upstream's own ``finditer`` over the whole subject, described the way every match is.

    ``None`` where upstream cannot answer the second question - which is recorded as the field being
    absent, never as an empty scan, so the consumer can tell "upstream found nothing" from "nobody
    asked".
    """
    try:
        found = list(compiled.finditer(subject, timeout=ROW_TIMEOUT_SECONDS))
    except Exception:  # noqa: BLE001 - an unanswerable second question is recorded as unasked
        return None

    offsets = _utf16_offsets(subject)
    return [dict(_describe_match(compiled, m, offsets), codepointSpan=list(m.span(0))) for m in found]


# The zero-width items whose meaning changes when `endpos` truncates the subject, in the spellings a
# pattern can carry them in. Upstream's POSITION_ESCAPES (upstream/regex/_regex_core.py:4635) minus
# `\A`, which is bound by `text_start` rather than by the slice and which the walk does not move
# anyway; plus `$` in all three of its opcodes; plus `\G` (`:1278`), whose anchor is the search's own
# start; plus `\X`, whose expansion ends in a GraphemeBoundary that reads the character after it
# (`:2924`); plus either lookahead, which reads past the position it sits at.
_END_SENSITIVE_ITEMS = ("$", r"\Z", r"\z", r"\b", r"\B", r"\m", r"\M", r"\K", r"\G", r"\X", "(?=", "(?!")


def _reads_the_end_of_the_subject(pattern: str) -> bool:
    """Whether a pattern holds anything whose meaning a truncated subject would change.

    The refusal `_anchored_scan` applies to a REVERSED row, and deliberately a crude textual one.
    Every way it is crude refuses MORE than it has to and so can only cost a classification, never
    buy a wrong one: `\\b` inside a character class is a backspace and `$` inside one is a literal,
    and both are refused here; a doubled backslash before one of the letters makes it a literal
    backslash and it is refused too.

    Whitespace goes first for the reason `CarriesACaptureOutsideItself` strips it in
    tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs - under `(?x)` a construct can be spelt with
    spaces inside it - and stripping can only ADD a refusal.
    """
    bare = "".join(pattern.split())
    return any(item in bare for item in _END_SENSITIVE_ITEMS)


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
    "interactions",
    "lookaround",
    "conditionals",
    "verbs",
    "recursion",
    "partial",
    "partial-sliced",
    "posix",
    "fuzzy",
    "fuzzy-anchored",
    "fuzzy-overhang",
    "literals-long",
    "quantifiers-long",
    "partial-long",
    "fuzzy-long",
    "timeout",
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
    # S52. Every atom above is ASCII or a property name, so before this the `classes` PATTERN never
    # held a character above U+FFFF at all - measured at 0 of 1200 rows. A set whose member list is
    # built by codepoint and read by UTF-16 code unit is exactly the mistake these three catch: a
    # bare astral literal, a class holding one, and a RANGE whose two ends are both surrogate pairs.
    ASTRAL_LETTER,
    "[" + ASTRAL_SYMBOL + ASTRAL_DIGIT + "]",
    "[\U0001d400-\U0001d7ff]",
    "[^\U0001d400-\U0001d7ff]",
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
    # S52: the modifier, the ZWJ and a second Nd block join the band that was already here. The
    # modifier and the ZWJ are what a class has to answer about a character that is astral-adjacent
    # rather than astral - U+200D is BMP, and `\w` is TRUE of it, which is not obvious from `Cf`.
    ASTRAL_ALPHABET + ASTRAL_CASED + ASTRAL_CASED_LOWER + ASTRAL_DIGIT_2 + "\U0001f4a9",
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
GROUP_ATOMS = ("a", "b", "c", "x", ".", "[ab]", "[^a]", r"\w", r"\d") + ASTRAL_PATTERN_ATOMS

# The subjects. Short, because a pattern of n atoms can only match n characters without a
# quantifier, and a subject much longer than the pattern makes every 'fullmatch' row fail for the
# same uninteresting reason. The astral alphabet is here for the same reason as in the literal
# generators: a group span reported in codepoints rather than UTF-16 code units has to show up as a
# divergence from the first wave.
GROUP_SUBJECT_ALPHABETS = ("abcx", "abx1_", "ab" + ASTRAL_ALPHABET + "c")

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
QUANT_ATOMS = ("a", "b", "x", ".", "[ab]", "[^a]", r"\w", r"\d", r"\s", "[a-c]") + ASTRAL_PATTERN_ATOMS

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
QUANT_SUBJECT_ALPHABETS = ("ab", "abx", "ab \t", "ab" + ASTRAL_ALPHABET)

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
# answered as unassigned.
#
# S24 wrote these inline only, because FuzzyRegexOptions had no WORD or ASCII member and a flags
# integer would have been unaskable on our side. S53b added `Ascii`, `Unicode` and `Word`, so half
# the flagged rows now ask the SAME question through the flags integer instead - which is the one
# path the corpus cannot check for WORD, upstream's own suite never passing it as a flag. The two
# spellings compile to identical bytecode (Gaps/Api/EncodingAndWordOptionTests), so a row that
# diverges one way and not the other is a finding about the option path.
BOUNDARY_FLAG_PREFIXES = ("", "(?a)", "(?w)", "(?V1)", "(?V1w)", "(?aw)")

# Each prefix as the flags integer that means the same thing: ASCII 0x80, WORD 0x800, VERSION1
# 0x100, from upstream's RegexFlag table (upstream/regex/_regex_core.py lines 73-90; re-measured
# 2026-09-16, hex(regex.ASCII) 0x80, hex(regex.WORD) 0x800).
BOUNDARY_FLAG_VALUES = {
    "": 0,
    "(?a)": 0x80,
    "(?w)": 0x800,
    "(?V1)": 0x100,
    "(?V1w)": 0x100 | 0x800,
    "(?aw)": 0x80 | 0x800,
}

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
    # S52 adds the SMP digit: a regional indicator pair, a ZWJ sequence, a modifier and an astral
    # letter were all here, and the one word-break and grapheme-break class missing was Numeric in a
    # plane where it is two code units. WB8 and WB11 are about digits specifically.
    "\U0001f1ec\U0001f1e7\U0001f600‍\U0001f469\U0001f3fb\U0001d518" + ASTRAL_DIGIT,
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

# The last five put the `\K` inside an alternative or an optional group, so a branch that fails
# after the marker has to backtrack past it and put the reported match start back. That branch was
# also found by a control that did not fire: removing the restore left the first five shapes
# agreeing on all 600 rows, because none of them can fail after the `\K`.
#
# Weighted, and widened from three failing shapes to five, by S26. With a uniform draw over eight
# shapes the same control fired on 3 rows of 600 at seed 7 and **0** at seed 20260901 - a control
# that fires at one seed and not another is a finding about the generator, so the shapes that can
# reach the restore are now three times as likely to be drawn as the ones that cannot.
BOUNDARY_KEEP_SHAPES = (
    r"%s\K%s",
    r"(%s\K%s)",
    r"%s\K(%s)",
    r"(?:%s)\K%s",
    r"%s+\K%s",
    r"%s\K%s%s|%s%s",
    r"(?:%s\K%s|%s)%s",
    r"(%s\K%s)?%s%s",
    # Two branches that both set the marker, so the second crossing has to restore before it sets
    # it again, and a repeat whose body sets it and can fail, which is the only shape here that
    # crosses the marker more than once in one attempt.
    r"(?:%s\K%s|%s\K%s)%s",
    r"(?:%s\K%s)*%s",
)
BOUNDARY_KEEP_SHAPE_WEIGHTS = (1, 1, 1, 1, 1, 3, 3, 3, 3, 3)
BOUNDARY_GRAPHEME_SHAPES = (r"\X", r"\X\X", r"\X+", r"\X*", r"\X+?", r"\X{2}", r"\X%s", r"%s\X")


def _generate_boundaries(rng: random.Random, count: int):
    """S20's generator: word, default-word and grapheme boundaries, and the keep marker.

    The subject is drawn independently of the pattern, as the class and quantifier generators do: a
    boundary assertion is zero-width, so slicing the whole pattern out of the subject would say
    nothing extra. Measured over 200 rows of seed 1, re-taken after S26 weighted the `\\K` shapes:
    40 match, 160 do not, 66 have an astral subject, none is rejected, and every one of `\\b`,
    `\\B`, `\\m`, `\\M`, `\\K`, `\\X`, `(?a)`, `(?w)` and `(?V1)` is recorded at least 28 times.
    (Before that widening the same 200 rows gave 59 matching and 49 astral; the keep shapes that can
    fail *after* the marker match less often, which is the point of them.)
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
            form = rng.choices(BOUNDARY_KEEP_SHAPES, weights=BOUNDARY_KEEP_SHAPE_WEIGHTS)[0]
            pattern = form % tuple(literal() for _ in range(form.count("%s")))
        else:
            form = rng.choice(BOUNDARY_GRAPHEME_SHAPES)
            pattern = form % literal() if "%s" in form else form

        # `\m` and `\M` are mrab-regex extensions with no V0/V1 difference, and `(?w)` only changes
        # which opcode `\b` compiles to, so every prefix is legal in front of every shape.
        prefix = rng.choice(BOUNDARY_FLAG_PREFIXES)

        # Every other flagged row asks through the flags integer instead of the inline prefix
        # (S53b). Keyed on the row index rather than on a draw of its own, deliberately: the rng
        # stream stays exactly what S24 and S26 measured their row shapes against, so this widening
        # changes which SPELLING a flagged row uses and nothing else about the wave.
        if prefix and i % 2 == 0:
            flags = BOUNDARY_FLAG_VALUES[prefix]
        else:
            pattern = prefix + pattern
            flags = 0

        yield {
            "generator": "boundaries",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }


# --------------------------------------------------------------------------------------------
# S21's generator: backreferences and group-existence conditionals
# --------------------------------------------------------------------------------------------

# What a group captures, and what a conditional's branches hold. One character each, as in the
# `groups` generator: the row is about the reference, not about the atom it reads back.
BACKREF_ATOMS = ("a", "b", "c", "x", ".", "[ab]", r"\w") + ASTRAL_PATTERN_ATOMS

# The subjects. Small alphabets, because a backreference can only match when the subject repeats
# something, and 'abcde' at length 6 almost never repeats a two-character run. The astral alphabet
# is here for the reason it is in every other generator: a span reported in codepoints rather than
# UTF-16 code units has to show up as a divergence, and a reference is the one construct that walks
# the *subject* twice, so a stepping bug on the second walk shows up here and nowhere else.
BACKREF_SUBJECT_ALPHABETS = ("ab", "abc", "aabbx", "ab" + ASTRAL_ALPHABET)

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
SUB_ATOMS = ("a", "b", "x", ".", "[ab]", "[^a]", r"\w", r"\d") + ASTRAL_PATTERN_ATOMS

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

# How often an ordinary substitution row carries a narrowed slice (S53b). A quarter rather than a
# half, because the whole-subject rows are the ones the earlier seeds measured and the slice is a
# widening rather than a replacement: what it adds is the rule that the text outside the slice is
# copied through, and a quarter of a wave is thousands of rows of it.
SUB_SLICED_PROBABILITY = 0.25



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
    after the last change to this generator (S53b's slice, 2026-09-16): 158 rows replace at least
    once, 370 replace nothing, 72 are rejected by upstream, 250 are subf, 247 have an astral
    subject, and 131 carry a narrowed slice - 96 of those starting past the front of the subject.
    The `sub` share is the larger one because the narrow arm is always a `sub` row.

    Before the slice the same command gave 194 / 327 / 79 / 250 / 219 with no sliced row at all, so
    a quarter of the wave is now asking a question the generator could not ask, and the shift in
    the other five figures is the RNG stream moving rather than the row shapes changing.

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

        row = {
            "generator": "substitution",
            "pattern": ("(?r)" + pattern) if i % 2 else pattern,
            "flags": 0,
            "namedLists": {},
            "subject": subject,
            "operation": operation,
            "template": template,
            "count": rng.choice(SUB_COUNTS),
        }

        # A NARROWED SLICE, on about a quarter of the rows (S53b, when `Replace` and
        # `ReplaceFormat` gained a `beginning`/`length` pair). It is the one argument of upstream's
        # `sub` this generator could not ask about, and it reaches a rule no other generator does:
        # a substitution keeps the text OUTSIDE its slice and replaces only inside it, so a slip
        # that dropped either end would be invisible on a whole-subject row and is a wrong string
        # here. The min-width shortcut moves with it too - `pattern_subx` compares the pattern's
        # width against the CLAMPED slice, so a narrow slice takes the shortcut on a pattern the
        # whole subject would have matched.
        #
        # Drawn by codepoint index; `_record_row` translates to UTF-16, so a slice edge never falls
        # inside a surrogate pair. Not on the narrow arm, which is about the shortcut at full width.
        if subject and rng.random() < SUB_SLICED_PROBABILITY:
            lo = rng.randrange(len(subject) + 1)
            hi = rng.randrange(lo, len(subject) + 1)
            row["pos"] = lo
            row["endpos"] = hi

        yield row


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
    # S52. A split and an overlapped scan both report positions, so an astral separator is where a
    # codepoint index and a UTF-16 one part company most visibly. `\X` is here because a scan over
    # grapheme clusters is the one iteration whose step is not one character.
    ASTRAL_SYMBOL, ASTRAL_SYMBOL + "*", ASTRAL_DIGIT, r"\X", r"\X+",
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
ITER_SUBJECT_ALPHABETS = ("ab:", "a:" + ASTRAL_ALPHABET)
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


# --------------------------------------------------------------------------------------------
# S26's generator: every family at once, rather than one family at a time
# --------------------------------------------------------------------------------------------
#
# What the per-slice generators structurally miss. Each of them holds one construct fixed and
# varies the rest - `classes` never quantifies an atom, `quantifiers` never puts a class under
# `(?i)`, `backrefs` never reverses - so the cells where two features meet are reached only by
# accident, and the interaction is exactly where a port breaks: S23's own control measured it, at 1
# divergence of 600 when the reverse wave drew its folding rows from `case-folding` and 21 when it
# drew them from rows built for the interaction.
#
# So this one composes: a class inside a quantified capture group, referenced back, under
# case-insensitivity and reverse and MULTILINE at once, over subjects that hold ASCII and
# expanding-fold and astral characters in the same string, through all eight operations.

# The class atoms, S17's inventory narrowed to what is worth quantifying. `\p{Cyrillic}` and friends
# are dropped: quantified over these subjects they match nothing, and the row is then a statement
# about the failure path rather than about the interaction.
INTERACTION_CLASS_ATOMS = (
    "[a]",
    "[^a]",
    "[abz]",
    "[a-f]",
    "[^a-f]",
    "[A-Z]",
    r"\d",
    r"\D",
    r"\w",
    r"\W",
    r"\s",
    r"\S",
    r"[\w\s]",
    r"[a\d]",
    r"[^\d]",
    "[[:alpha:]]",
    "[[:digit:]]",
    r"\p{L}",
    r"\p{Lu}",
    r"\p{Ll}",
    r"\p{Nd}",
    r"\p{ASCII}",
    r"[\p{L}\p{N}]",
    r"[^\p{L}]",
    ".",
)

# The V1-only set operators, drawn only when the row carries VERSION1.
INTERACTION_V1_CLASS_ATOMS = (
    r"[\p{ASCII}&&\p{L}]",
    r"[\p{ASCII}--\p{L}]",
    r"[[a-z]--[aei]]",
    r"[\w--[0-9]]",
    r"[\p{L}||\p{N}]",
    r"[[a-f]~~[d-k]]",
    r"[^[\p{L}--[a-z]]]",
)

# The subjects, and the point of the generator in one table. The first three bands are the ones the
# per-family generators already draw from; the fourth holds all three kinds of character in the same
# string, which is the band no other generator has - a fold that expands, a character that occupies
# two UTF-16 code units and a plain ASCII letter, so a row can reach the expanding-fold path and the
# surrogate-stepping path in one match.
INTERACTION_SUBJECT_ALPHABETS = (
    # S52, and FIRST in the tuple only because every caller picks with `rng.choice`. The composed
    # generator had astral LETTERS and the Deseret case pair, and no astral digit, no emoji modifier
    # and no ZWJ at all - so nothing it composes, a verb beside a fuzzy section or a lookaround
    # inside a recursion, ever ran over one. Five generators draw from this tuple, not one.
    "aA" + ASTRAL_ALPHABET + ASTRAL_CASED,
    "aAbB0_ .",
    "aAsSß\ufb00\ufb01\u0130\u0131",
    "aA\U0001f600\U0001d518\U00010400\U00010428",
    "aAß\ufb03\U00010400\U0001f600_",
)

# Inserted rather than drawn, so a row can hold several and so CR/LF lands as a pair - which is what
# makes MULTILINE's `^` and `$` interesting anywhere but the two ends.
INTERACTION_LINE_BREAKS = ("\n", "\r", "\r\n", "", " ")

# What a row's pattern is built from.
#   'quant-group': a class atom in a capture group with a quantifier - the cell `classes` and
#                  `quantifiers` share and neither reaches.
#   'backref':     a reference to one of those groups, so the reference reads back text that a
#                  quantified class captured, under whatever folding and direction the row carries.
#   'cond':        a group-existence conditional on one of them.
#   'class':       a bare or quantified atom, so not every piece opens a group.
#   'keep':        `\K`, which moves the reported match start - and interacts with substitution and
#                  with reverse in ways nothing else here does.
#   'boundary':    `\b` / `\B` / `\m` / `\M`, the predicates that ask about both sides of a position.
#   'literal':     a character the subject holds, escaped - so a row can actually match.
#   'group-then-ref': the headline cell, emitted as a unit: a quantified class in a capture group
#                  with the reference to that group immediately after it. Left to chance - a group
#                  from one piece and a reference from another - a reference came up on 56 rows of
#                  600 at seed 1, because a reference to a group two pieces back rarely matches.
#                  As a unit, and as the forced shape of a one-piece row, it is 390 of 600.
#   'called-group': S36's headline cell, and the reason this generator was widened at the Phase 4
#                  close: a lookaround round a GROUP CALL, inside a group-existence CONDITIONAL,
#                  inside a REPEAT. Four of Phase 4's six families in one piece, and emitted as a
#                  unit for the same reason 'group-then-ref' is - a call needs its named group to
#                  exist already, and left to chance across pieces it would almost never appear.
#   'verb-alt':    an alternation with a `(*PRUNE)` or a `(*SKIP)` in one branch, which is S29's
#                  cell. A verb only shows where something AFTER it fails and the other branch has
#                  to be tried, so it goes in as a two-branch unit rather than as a bare verb.
#   'fuzzy':       S43's headline cell, and the reason this generator was widened at the Phase 5
#                  close: a FUZZY SECTION wrapped round the constructs above, sometimes with a
#                  capture group inside it (so a substitution template reads a group an error landed
#                  in) and sometimes with a second section nested inside carrying a different
#                  constraint. Everything else about the row - `(?i)`, `(?fi)`, `(?r)`, `(?p)`,
#                  `partial=True`, the operation and the template - already varies, so one piece
#                  kind composes fuzzy with all of it.
#   'fuzzy-wrapped': the other direction - a fuzzy section INSIDE one of Phase 4's containers: a
#                  lookaround, an atomic group, a conditional branch, a verb alternation, or a
#                  self-recursive called group. Emitted as a unit for the reason 'called-group' is:
#                  left to chance across pieces the container and the section would rarely meet.
#   'fuzzy-list':  `\L<name>{e<=1}`, which DECISIONS 2026-08-30 records as a distinct code path -
#                  a named list lowers to BRANCH, so a fuzzy budget spent inside one is not the
#                  same walk as one spent over a class. This is the ONLY generator that emits a
#                  named list at all; `namedLists` was wired through the recorder and the consumer
#                  from the start and nothing had used it since S13's compile corpus.
#
# TWO TABLES, and the split is load-bearing rather than tidy. `_generate_partial` builds its
# patterns with this same function (see its docstring), and S43 must not change a single `partial`
# or `partial-sliced` row: both carry pinned divergences, and `partial-sliced` is recorded
# prefilter-free, so a reshuffled row stream would turn permanent answers red for a reason that has
# nothing to do with what changed. So `partial` keeps drawing from the ten-piece table at its
# original weights, which is byte-identical to what it drew before - `random.choices` consumes
# exactly one `random()` call whatever the list length, and the plain table's cumulative weights are
# unchanged - and `interactions` draws from the widened one.
INTERACTION_PIECES_PLAIN = (
    "quant-group",
    "class",
    "backref",
    "cond",
    "keep",
    "boundary",
    "literal",
    "group-then-ref",
    "called-group",
    "verb-alt",
)
INTERACTION_PIECE_WEIGHTS_PLAIN = (24, 12, 12, 9, 5, 9, 11, 18, 12, 8)

INTERACTION_PIECES = INTERACTION_PIECES_PLAIN + ("fuzzy", "fuzzy-wrapped", "fuzzy-list")
INTERACTION_PIECE_WEIGHTS = INTERACTION_PIECE_WEIGHTS_PLAIN + (22, 14, 6)

# Wrapped round the whole pattern, so the anchors and the boundaries are asked about positions an
# interior piece has reached rather than only about position 0.
INTERACTION_AFFIXES = (("^", ""), ("", "$"), ("^", "$"), (r"\b", ""), ("", r"\b"), ("", ""), ("", ""))

INTERACTION_BOUNDARIES = (r"\b", r"\B", r"\m", r"\M")

# --------------------------------------------------------------------------------------------
# S43: fuzzy composed into this generator
# --------------------------------------------------------------------------------------------

# The constraints a composed section may carry. BOUNDED ONLY - `{e}` is deliberately absent, for
# the reason FUZZY_BOUNDED_CONSTRAINTS spells out at length: an unbounded error budget over a
# multi-character item is upstream's 551/554 resource-blowup family, and EVERY piece this generator
# emits is multi-character (a repeat, a backreference, a called group, an alternation).
#
# Two weighted equations are in the list on purpose rather than by oversight. They exercise the cost
# machinery S42 ported, and `_has_weighted_cost` then suppresses `(?e)` and `(?b)` on that row, so
# the ranking divergence DECISIONS 2026-09-12 records is never drawn here either - the same rule
# `_generate_fuzzy` follows, applied to the same pattern text.
INTERACTION_FUZZY_CONSTRAINTS = (
    "{e<=1}",
    "{e<=2}",
    "{s<=1}",
    "{i<=1}",
    "{d<=1}",
    "{e<=2,i<=1}",
    "{e<=2,s<=1}",
    "{s<=1,i<=1,d<=1}",
    "{1<=e<=2}",
    "{1i+2d+1s<=3}",
    "{2i+1d+1s<=2}",
)

# The `{...:test}` forms, S40's FUZZY_EXT. A short list rather than FUZZY_TESTS' fifteen, because
# these have to be chosen against INTERACTION_SUBJECT_ALPHABETS rather than against `_generate_fuzzy`'s
# three bands: a test aimed at 'abx' both permits and refuses real edits there and is very nearly
# vacuous here, where a subject is as likely to be 'ß', 'ﬁ' or an astral character as a letter.
# `.` is kept for the reason FUZZY_TESTS keeps it - it compiles to ANY, has no arm in upstream's
# `fuzzy_ext_match` switch (upstream/src/_regex.c:9938) and so constrains nothing.
INTERACTION_FUZZY_TESTS = (r"\w", r"\W", r"\d", r"\s", r"\S", "[a-z]", "[^a-z]", "[A-Za-z_]", ".")

# What a fuzzy section may be wrapped in.
#
# THE RULE THIS LIST OBEYS, measured rather than assumed: a SELF-RECURSIVE call whose body is a fuzzy
# section that can match the EMPTY STRING allocates until upstream raises MemoryError, because
# nothing makes the recursion progress. A section of n atoms can be emptied by n deletions, so a
# budget that permits n of them is the dangerous one; `{s<=n}` and `{i<=n}` never delete anything,
# whatever n is.
#
# EVERY ONE of the eleven constraints this generator can draw, measured 2026-09-13 on regex 2026.7.19
# against `(?P<g1>(?:Ab){C}(?&g1)?)` over 'AbAb' and `(?P<g1>(?:Abc){C}(?&g1)?)` over 'AbcAbc':
#
#     atoms=2   MemoryError:  {e<=2}  {1<=e<=2}  {2i+1d+1s<=2}
#               ok:           {e<=1} {s<=1} {i<=1} {d<=1} {e<=2,i<=1} {e<=2,s<=1}
#                             {s<=1,i<=1,d<=1} {1i+2d+1s<=3}
#     atoms=3   ok: all eleven
#
# The rule accounts for nine of the eleven. `{e<=2}` and `{1<=e<=2}` allow two deletions of a
# two-atom section; `{2i+1d+1s<=2}` prices a deletion at 1 against a budget of 2, so it allows two
# as well, while `{1i+2d+1s<=3}` prices one at 2 and allows only one. At three atoms none of them
# reaches three deletions, which is why the same eleven are all safe there.
#
# IT DOES NOT ACCOUNT FOR `{e<=2,i<=1}` AND `{e<=2,s<=1}`, and that is stated rather than smoothed
# over: both cap the total at two and neither caps deletions, so the rule predicts two deletions and
# a blowup, and both are safe in 0.00s. Why a compound constraint behaves differently has NOT been
# established - no mechanism was measured - so the rule is a good predictor and not a proof, and the
# list below is justified by the table rather than by the rule.
#
# Whole-pattern recursion is the degenerate case - `(?:(?R)){e<=1}` is a section whose only content
# is the recursion, so it matches empty at any budget - and `(?R)` and `(?0)` are therefore NOT in
# the wrapper list and must not be. All five shapes in
# tools/probes/upstream-fuzzy-recursion-blowup.py raise MemoryError in 0.48s to 0.97s, where the
# identical recursion WITHOUT a fuzzy section answers (0, 4) in 0.00s.
#
# A SELF-RECURSIVE CALL IS BACK IN THIS LIST SINCE S47 (2026-09-14), and the history matters because
# two earlier attempts to keep it are what the 'call' arm has to beat. Forcing an atom that must
# consume OUTSIDE the section - `(?P<g1>A(?:Ab){C}(?&g1)?)` - makes the recursion provably progress
# and is safe across every constraint this generator can draw (.scratch/probe-selfcall-guard.py, all
# 0.00s), and it was still not enough on real drawn rows: a 600-row wave at seed 1 raised MemoryError
# on four of them. Progress bounds the DEPTH; it does nothing about the BRANCHING, and a fuzzy
# section offers a fresh insert/delete/substitute choice at every position of every level. So the
# shape was drawn only through the 'called-group' piece - a call to a group that has already closed,
# which is not recursion and is measured safe.
#
# What changed is the PORT, not the generator's arithmetic. S47 ported PCRE2's positional guard
# (`PCRE2_ERROR_RECURSELOOP`, "nested recursion at the same subject position",
# tools/probes/pcre2-bounds-an-unbounded-recursion.py), so a call that re-enters a group where a call
# of it is already open now fails that path in microseconds instead of filling a gigabyte. Both of
# the old objections go with it: the cost objection, because the port no longer spends a second and a
# gigabyte on a row upstream cannot answer, and the blindness objection, because the rows upstream
# CAN answer - the recursions that progress - are exactly the ones the guard must not touch, and
# drawing them is the only instrument that can show it does not. Upstream still raises MemoryError on
# the rest; the recorder writes that down as a `resource` outcome and the consumer skips it (see
# `exhausted` above), so those rows cost a wave nothing but the draw.
#
# The port's own blowups are a deliberate divergence now rather than an inherited bug, pinned in
# Gaps/Engine/FuzzyRecursionTests.cs and carried by ledger entry 14. The 1GB bound is still the
# backstop for the branching the guard cannot see.
INTERACTION_FUZZY_WRAPPERS = ("look", "atomic", "cond", "verb", "call")

# How often a composed section carries a `{...:test}`, nests a second section with a different
# constraint, or holds a capture group. The capture group is the one a substitution template can
# read, which is the cell 'a template reading a fuzzy match's groups' names.
INTERACTION_FUZZY_TEST_PROBABILITY = 0.3
INTERACTION_FUZZY_NESTING_PROBABILITY = 0.3
INTERACTION_FUZZY_GROUP_PROBABILITY = 0.35

# How often a row carries `(?e)` or `(?b)`, drawn independently so a wave holds all four
# combinations - and drawn ONLY on a row that actually has a fuzzy section, unlike the row-level
# flags above. Both are no-ops without one, so putting them on every row would spend the draw on
# rows where it cannot change an answer.
INTERACTION_ENHANCE_PROBABILITY = 0.3
INTERACTION_BESTMATCH_PROBABILITY = 0.3

# The members of a `\L<name>` list. Two- and three-character runs, because a named list lowers to
# BRANCH and a one-character member would make it a character class in all but name; drawn from the
# subject's own alphabet at the call site so a row can match.
INTERACTION_NAMED_LIST_SIZES = (2, 3, 4)
INTERACTION_NAMED_LIST_MEMBER_LENGTHS = (1, 2, 2, 3)

MAX_INTERACTION_SUBJECT_LENGTH = 7
MAX_INTERACTION_PIECES = 3

# How often each flag is set. IGNORECASE and reverse are the two that change what every other piece
# means, so they are on about half the rows each; FULLCASE only differs from IGNORECASE when a
# folding expands, which is why two of the four alphabets carry expanding characters.
INTERACTION_IGNORECASE_PROBABILITY = 0.5
INTERACTION_FULLCASE_PROBABILITY = 0.5
INTERACTION_REVERSE_PROBABILITY = 0.45
INTERACTION_MULTILINE_PROBABILITY = 0.5
INTERACTION_VERSION1_PROBABILITY = 0.35
INTERACTION_ASCII_PROBABILITY = 0.2

# How often the subject is built from doubled characters rather than drawn one at a time. See
# _generate_interactions for what it is worth.
INTERACTION_DOUBLED_SUBJECT_PROBABILITY = 0.5

# Added by S36, at the Phase 4 close, so the composed wave reaches the two things Phase 4 added that
# are properties of the CALL rather than of the pattern: POSIX leftmost-longest (S32) and a subject
# cut short under `partial=True` (S31). Both are low: they only change an answer where the rest of
# the row already produced one, and a row that cannot match tests neither.
INTERACTION_POSIX_PROBABILITY = 0.15
INTERACTION_POSIX_INLINE_PROBABILITY = 0.5
INTERACTION_PARTIAL_PROBABILITY = 0.3
INTERACTION_PARTIAL_CUT_PROBABILITY = 0.6


def _interaction_subject_class(rng: random.Random, subject: str) -> str:
    """A character class built from characters the subject holds, preferring a repeated one.

    ``re.escape`` on each member, because the alphabets hold ``.``, ``_`` and ``-``, and an
    unescaped ``-`` inside a class is a range rather than a member.
    """
    usable = [c for c in subject if c not in "\r\n"]
    if not usable:
        return "[a-z]"

    doubled = [subject[i] for i in range(len(subject) - 1) if subject[i] == subject[i + 1]]
    members = [rng.choice(doubled) if doubled else rng.choice(usable)]
    if rng.random() < 0.5:
        members.append(rng.choice(usable))

    return "[" + "".join(re.escape(c) for c in dict.fromkeys(members)) + "]"


def _interaction_fuzzy_constraint(rng: random.Random) -> str:
    """One bounded constraint, sometimes carrying a `{...:test}` - S40's FUZZY_EXT rather than FUZZY."""
    constraint = rng.choice(INTERACTION_FUZZY_CONSTRAINTS)
    if rng.random() >= INTERACTION_FUZZY_TEST_PROBABILITY:
        return constraint
    return constraint[:-1] + ":" + rng.choice(INTERACTION_FUZZY_TESTS) + "}"


def _interaction_fuzzy_body(rng: random.Random, subject: str, atoms: tuple, group) -> str:
    """What goes inside a fuzzy section: two or three atoms, sometimes one of them a capture group.

    ``group`` is ``_interaction_pattern``'s own closure, so a group opened in here is numbered in
    the same left-to-right sequence as every other group in the row and a template written
    afterwards can refer to it. That is the whole of the 'a substitution template reading a fuzzy
    match's groups' cell: the group is INSIDE the section, so an error charged against the section
    can land in the text the template goes on to read.

    A literal the subject actually holds goes in about a quarter of the time, for the reason
    ``_interaction_subject_class`` exists - a section built only from table atoms often cannot match
    the subject at all, and a fuzzy section that can never match within its budget tests the refusal
    path and nothing else.
    """
    pieces = []
    for _ in range(rng.randrange(2, 4)):
        draw = rng.random()
        if draw < 0.25:
            candidates = [c for c in subject if c not in "\r\n"]
            pieces.append(re.escape(rng.choice(candidates)) if candidates else "a")
        elif draw < 0.25 + INTERACTION_FUZZY_GROUP_PROBABILITY:
            pieces.append(group(rng.choice(atoms) + (_quantifier(rng) if rng.random() < 0.4 else "")))
        else:
            pieces.append(rng.choice(atoms) + (_quantifier(rng) if rng.random() < 0.4 else ""))

    body = "".join(pieces)

    # A SECOND SECTION NESTED INSIDE, with a constraint of its own. Without nesting the outer
    # FUZZY/END_FUZZY counts are always (0, 0, 0), which is exactly the observation `_generate_fuzzy`
    # records against its own FUZZY_NESTING_PROBABILITY: the stack traffic round the two opcodes
    # cannot change an answer until something has already been spent when the inner section is
    # entered. The inner section goes round the LAST piece rather than the first for the same reason
    # that generator draws its start from index 1 - an inner section at the start inherits nothing.
    if len(pieces) >= 2 and rng.random() < INTERACTION_FUZZY_NESTING_PROBABILITY:
        body = "".join(pieces[:-1]) + "(?:" + pieces[-1] + ")" + _interaction_fuzzy_constraint(rng)

    return body


def _interaction_named_list(rng: random.Random, subject: str, alphabet: str) -> list[str]:
    """The members of one `\\L<name>` list, biased towards runs the subject holds.

    Half the members are cut out of the subject itself and half drawn from its alphabet. A list
    drawn purely from the alphabet almost never matches a seven-character subject, and a list cut
    purely from the subject would never exercise the branch that fails.
    """
    members = []
    for _ in range(rng.choice(INTERACTION_NAMED_LIST_SIZES)):
        length = rng.choice(INTERACTION_NAMED_LIST_MEMBER_LENGTHS)
        usable = [c for c in subject if c not in "\r\n"]
        if usable and rng.random() < 0.5:
            at = rng.randrange(len(usable))
            members.append("".join(usable[at : at + length]))
        else:
            members.append("".join(rng.choice(alphabet) for _ in range(length)))

    # Never empty and never a duplicate: upstream keeps both, but an empty member makes the whole
    # list match everywhere and a duplicate is the same branch twice, and neither says anything
    # about a fuzzy budget spent inside the list.
    return list(dict.fromkeys(m for m in members if m)) or ["a"]


def _interaction_pattern(
    rng: random.Random, subject: str, version1: bool, alphabet: str = "", allow_fuzzy: bool = False
) -> tuple[str, int, list[str], dict, bool]:
    """One composed pattern, with the group inventory a substitution template needs.

    Built strictly left to right, as ``_backref_pattern`` is, so a group number is only handed out
    once the group that owns it has been emitted - a reference to a group defined later is legal
    upstream but is `backrefs`' own test, not this one's.

    Returns the pattern, the group count, the group names, the named lists any `\\L<name>` piece
    registered, and whether the row ended up with a fuzzy section - which is what decides whether
    `(?e)` and `(?b)` are worth drawing for it.

    ``allow_fuzzy`` is off by default so that ``_generate_partial``, the other caller, keeps the
    patterns it drew before S43. See INTERACTION_PIECES_PLAIN.
    """
    atoms = INTERACTION_CLASS_ATOMS + INTERACTION_V1_CLASS_ATOMS if version1 else INTERACTION_CLASS_ATOMS
    counter = [0]
    names: list[str] = []
    defined: list[int] = []
    pieces: list[str] = []
    named_lists: dict[str, list[str]] = {}
    fuzzy = False

    def group(body: str, named: bool = False) -> str:
        counter[0] += 1
        defined.append(counter[0])
        if named or rng.random() < 0.25:
            name = f"g{counter[0]}"
            names.append(name)
            return f"(?P<{name}>{body})"
        return f"({body})"

    # A one-piece row is allowed, but only as a 'group-then-ref' - which is itself an interaction of
    # four features, and is the only shape short enough that a seven-character subject can satisfy
    # the whole pattern. Every extra piece is another thing that has to match at the same time, and
    # a wave that cannot match tests the failure path and nothing else.
    wanted = rng.randrange(1, MAX_INTERACTION_PIECES + 1)
    for _ in range(wanted):
        kind = (
            rng.choices(INTERACTION_PIECES, weights=INTERACTION_PIECE_WEIGHTS)[0]
            if allow_fuzzy
            else rng.choices(INTERACTION_PIECES_PLAIN, weights=INTERACTION_PIECE_WEIGHTS_PLAIN)[0]
        )
        if wanted == 1:
            kind = "group-then-ref"
        # 'cond' is no longer in this guard: since S36 half of its conditions are a LOOKAROUND rather
        # than a group number, and that form needs no group to have been defined.
        if not defined and kind == "backref":
            kind = "quant-group"

        if kind == "group-then-ref":
            # Half the time the class is built out of characters the subject actually holds, rather
            # than drawn from the table. A reference can only match where the subject repeats what
            # the group captured, and a class drawn independently often cannot capture anything at
            # all: re-measured over 600 rows of seed 1 at S47 with the threshold on this line set to
            # 0.0, which ablates the class without moving the RNG stream, 49 of 326 reference rows
            # produced an answer with the table alone and 58 of 345 with this (135 answers against
            # 142 overall). It is still the cell this generator exists for - a class, in a quantified
            # capture group, referenced back - only aimed at a subject that can satisfy it.
            body = _interaction_subject_class(rng, subject) if rng.random() < 0.5 else rng.choice(atoms)
            body += _quantifier(rng) if rng.random() < 0.5 else ""
            opened = group(body) + (_quantifier(rng) if rng.random() < 0.4 else "")
            pieces.append(opened + f"\\{defined[-1]}")
        elif kind == "quant-group":
            # The quantifier goes outside the group as often as inside it: '(\w)+' captures once per
            # iteration and '(\w+)' captures once, and a reference reads a different span in each.
            body = rng.choice(atoms) + (_quantifier(rng) if rng.random() < 0.5 else "")
            pieces.append(group(body) + (_quantifier(rng) if rng.random() < 0.5 else ""))
        elif kind == "class":
            pieces.append(rng.choice(atoms) + (_quantifier(rng) if rng.random() < 0.6 else ""))
        elif kind == "backref":
            number = rng.choice(defined)
            pieces.append(rng.choice((f"\\{number}", f"\\g<{number}>")))
        elif kind == "cond":
            yes, no = rng.choice(atoms), rng.choice(atoms)
            if defined and rng.random() < 0.5:
                head = f"(?({rng.choice(defined)})"
            else:
                # S28's form, added here by S36: the condition is a LOOKAROUND rather than a group
                # number, so the test consumes nothing and can be a lookbehind - which makes the
                # branch chosen depend on the text on the OTHER side of the position.
                head = "(?" + rng.choice(LOOKAROUND_FORMS) + rng.choice(atoms) + ")"
            pieces.append(rng.choice((f"{head}{yes}|{no})", f"{head}{yes})")))
        elif kind == "called-group":
            # Phase 4's families composed into one piece, which is what no per-slice wave reaches:
            # the named group, then a repeat round a conditional whose yes-branch is a lookaround
            # round a call back to that group. `(?&g1)` and `(?P>g1)` only - `\g<g1>` is a call in a
            # pattern and a backreference in a template, and this generator writes both.
            body = _interaction_subject_class(rng, subject) if rng.random() < 0.5 else rng.choice(atoms)
            opened = group(body + (_quantifier(rng) if rng.random() < 0.5 else ""), named=True)
            number, name = defined[-1], names[-1]
            call = rng.choice((f"(?&{name})", f"(?P>{name})"))
            yes = rng.choice(LOOKAROUND_FORMS) + call + ")" + rng.choice(atoms)
            inner = (
                f"(?({number}){yes}|{rng.choice(atoms)})" if rng.random() < 0.5 else f"(?({number}){yes})"
            )
            pieces.append(opened + f"(?:{inner})" + (_quantifier(rng) if rng.random() < 0.6 else ""))
        elif kind == "verb-alt":
            verb = rng.choice(("(*PRUNE)", "(*SKIP)"))
            left = rng.choice(atoms) + (_quantifier(rng) if rng.random() < 0.5 else "")
            pieces.append(f"(?:{left}{verb}{rng.choice(atoms)}|{rng.choice(atoms)})")
        elif kind == "fuzzy":
            # A fuzzy section wrapped ROUND the constructs above. Everything else about the row -
            # the case flags, `(?r)`, `(?p)`, `partial=True`, the operation, the template - is drawn
            # by the caller, so this one piece composes fuzzy with all of it.
            fuzzy = True
            body = _interaction_fuzzy_body(rng, subject, atoms, group)
            pieces.append("(?:" + body + ")" + _interaction_fuzzy_constraint(rng))
        elif kind == "fuzzy-wrapped":
            # The other direction: a fuzzy section INSIDE one of Phase 4's containers. See
            # INTERACTION_FUZZY_WRAPPERS on why `(?R)` is not one of them.
            fuzzy = True
            section = "(?:" + _interaction_fuzzy_body(rng, subject, atoms, group) + ")"
            section += _interaction_fuzzy_constraint(rng)
            wrapper = rng.choice(INTERACTION_FUZZY_WRAPPERS)
            # A conditional needs a group to ask about, and on a first piece there is none yet.
            # Re-routed to the atomic arm rather than to whatever the chain falls through to, so
            # which arm absorbs the re-route is a decision here rather than an accident of ordering.
            if wrapper == "cond" and not defined:
                wrapper = "atomic"

            if wrapper == "look":
                pieces.append(rng.choice(LOOKAROUND_FORMS) + section + ")")
            elif wrapper == "atomic":
                pieces.append("(?>" + section + ")")
            elif wrapper == "call":
                # Ledger entry 14's shape: a group that calls ITSELF round a fuzzy section. The name
                # has to be known before the body that uses it is built, so the next counter value is
                # spelled out here rather than read back out of `names` afterwards - `group(...,
                # named=True)` assigns this same name.
                self_name = f"g{counter[0] + 1}"
                self_call = rng.choice((f"(?&{self_name})", f"(?P>{self_name})"))
                pieces.append(group(section + self_call + "?", named=True))
            elif wrapper == "cond":
                head = f"(?({rng.choice(defined)})"
                pieces.append(f"{head}{section}|{rng.choice(atoms)})")
            else:
                verb = rng.choice(("(*PRUNE)", "(*SKIP)"))
                pieces.append(f"(?:{section}{verb}{rng.choice(atoms)}|{rng.choice(atoms)})")
        elif kind == "fuzzy-list":
            # `\L<name>{e<=1}`: a named list lowers to BRANCH (upstream/regex/_regex_core.py:4069),
            # so a budget spent inside one is a different walk from one spent over a class.
            fuzzy = True
            name = f"w{len(named_lists) + 1}"
            named_lists[name] = _interaction_named_list(rng, subject, alphabet)
            pieces.append(f"\\L<{name}>" + _interaction_fuzzy_constraint(rng))
        elif kind == "keep":
            pieces.append(r"\K")
        elif kind == "boundary":
            pieces.append(rng.choice(INTERACTION_BOUNDARIES))
        else:
            # A character the subject holds, so the row can match at all. Escaped, because the
            # alphabets hold '.' and '_' and an unescaped '.' would be a third dot atom rather than
            # a literal.
            candidates = [c for c in subject if c not in "\r\n"]
            pieces.append(re.escape(rng.choice(candidates)) if candidates else "a")

    prefix, suffix = rng.choice(INTERACTION_AFFIXES)
    return prefix + "".join(pieces) + suffix, counter[0], names, named_lists, fuzzy


def _generate_interactions(rng: random.Random, count: int):
    """S26's generator: the S16-S25 constructs composed, over all eight operations.

    **Widened at the Phase 4 close (S36) to compose Phase 4's six families with the S16-S25 ones.**
    Two new pieces - `called-group` and `verb-alt` - plus a lookaround-as-condition arm on `cond`,
    POSIX at the row level and `partial=True` with the subject cut short. The cell the widening
    exists for is a lookaround round a group call inside a conditional inside a repeat, which no
    per-slice wave reaches; it found two things in its first three seeds, an upstream capture recorded
    outside the subject and a recorder that raised `IndexError` rather than write it down.

    **Widened again at the Phase 5 close (S43) to compose FUZZY MATCHING with all of it.** Three new
    pieces - `fuzzy`, `fuzzy-wrapped` and `fuzzy-list` - plus `(?e)` and `(?b)` at the row level.
    Everything else about a row was already drawn, so one fuzzy piece composes a section with
    `(?i)`, `(?fi)`, `(?r)`, `partial=True`, the operation and the substitution template at once.
    One shape is deliberately NOT drawn, and it is an upstream bug this generator found rather than
    an omission: a self-recursive call round a fuzzy section (see INTERACTION_FUZZY_WRAPPERS).
    **S47 lifted that exclusion on 2026-09-14**, once the port gained PCRE2's positional recursion
    guard, and the widening paid for itself on its first run: row 72179 at seed 20260914 is a drawn
    self-recursive call this port used to exhaust its backtracking stack on.
    POSIX beside a fuzzy section was the other, suppressed from S43 until S46 sitting 2 lifted the
    suppression - the crash is still real, but the recorder no longer reads the attribute that
    triggers it, so the cell is drawn again and compared on everything but the change positions.
    See the POSIX draw below and `_describe_match`.

    Measured by `python tools/record-oracle.py --generator interactions --count 600 --seed 1`, after
    the last change to this generator: 142 rows produce an answer - a match, a non-empty match list,
    a split with more than one part or a substitution that replaced something - 450 produce none, 5
    are rejected by upstream and 3 are rows upstream cannot answer at all (`resource`). 326 rows
    carry IGNORECASE, 149 FULLCASE, 310 MULTILINE, 207 VERSION1, 59 ASCII and 255 are reversed; 107
    are POSIX (46 by flag, 61 as `(?p)`), 66 ask for a partial match, and 213 have an astral subject.
    345 hold a backreference, 218 a named group, 109 a conditional, 103 a lookaround, 76 a group call
    - 14 of those a SELF-RECURSIVE one, the cell S47 lifted the exclusion on - 60 a backtracking verb
    and 21 a `\\K`. 182 hold a FUZZY SECTION - 67 of those a second section nested inside it with a
    different constraint, 58 a `{...:test}` and 34 a `\\L<name>` named list - and 46 carry `(?e)`, 50
    `(?b)`. Every one of the eight operations is recorded exactly 75 times, because the operation is
    cycled by row index rather than drawn.

    **The fuzzy sections earn their place rather than decorating the pattern, and that is measured
    too**, because a composed generator whose sections never actually spend an error would look
    identical to this one from the outside. Over 2000 rows at seed 7: 718 rows carry a section, 127
    of them produce a match, and 78 of those 127 charge at least one error - a match no exact
    engine could have returned. The remaining 49 match at zero cost, which is the row that says the
    engine does not spend an error it did not need.

    The answer rate is deliberately in line with `classes` and `backrefs` rather than higher: a
    composed pattern has more that must line up at once, and buying matches by shortening the
    pattern would spend the generator's whole point. What it must not be is *low on the interaction
    cell*, which is why the two subject tricks below carry measurements.

    Re-taken from scratch after every widening: adding one entry to any table above shifts the whole
    RNG stream, so a figure measured before it describes a wave this generator no longer produces.
    """
    for i in range(count):
        # Drawn from the seeded stream rather than from `i`: `ALL_OPERATIONS` has eight entries and
        # this table has four, so indexing both by `i` would pair each alphabet with exactly two
        # operations for ever - the aliasing the S16 blind review found in the anchors generator.
        alphabet = rng.choice(INTERACTION_SUBJECT_ALPHABETS)
        length = rng.randrange(1, MAX_INTERACTION_SUBJECT_LENGTH + 1)

        if rng.random() < INTERACTION_DOUBLED_SUBJECT_PROBABILITY:
            # Built from doubled characters, the trick `backrefs` and `case-folding` both use: a
            # reference cannot match unless the subject repeats something, and a reference is on
            # nearly two thirds of these rows. Worth less here than there, and the figure is
            # recorded rather than assumed - re-measured over 600 rows of seed 1 at S47 with
            # INTERACTION_DOUBLED_SUBJECT_PROBABILITY set to 0.0, which ablates the trick without
            # moving the RNG stream, 35 of 319 reference rows produced an answer without it and 58 of
            # 345 with it (116 answers against 142 overall). Kept because it is a gain on the cell
            # this generator exists for, and at this re-take a clear one.
            subject = ""
            while len(subject) < length:
                subject += rng.choice(alphabet) * 2
            subject = subject[:length]
        else:
            subject = "".join(rng.choice(alphabet) for _ in range(length))

        for _ in range(rng.randrange(3)):
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + rng.choice(INTERACTION_LINE_BREAKS) + subject[at:]

        version1 = rng.random() < INTERACTION_VERSION1_PROBABILITY
        pattern, groups, names, named_lists, fuzzy = _interaction_pattern(
            rng, subject, version1, alphabet, allow_fuzzy=True
        )

        flags = 0
        if version1:
            flags |= VERSION1
        if rng.random() < INTERACTION_IGNORECASE_PROBABILITY:
            flags |= IGNORECASE
            if rng.random() < INTERACTION_FULLCASE_PROBABILITY:
                flags |= FULLCASE
        if rng.random() < INTERACTION_MULTILINE_PROBABILITY:
            flags |= MULTILINE
        # Never ASCII where the pattern holds a property: upstream does not agree with *itself*
        # about a cased property under the ASCII encoding, so such a row is a guaranteed divergence
        # that says nothing about this port. The measurement and the six patterns are recorded
        # against `_generate_casefolding`, and DECISIONS 2026-08-31 carries the finding.
        if rng.random() < INTERACTION_ASCII_PROBABILITY and not re.search(r"\\[pP]\{|\[\[:", pattern):
            flags |= ASCII

        # Half as the flag and half inline, exactly as `posix` writes it, because the two reach the
        # parser by different routes and a composed row is where a mis-scoped flag would show.
        #
        # A FUZZY ROW MAY CARRY POSIX AGAIN SINCE S46 SITTING 2 (2026-09-14), and the way it is safe
        # is in `_describe_match`, not here. From S43 until then this read `if posix and not fuzzy`,
        # because reading `fuzzy_changes` on a POSIX fuzzy match that spent an error kills the
        # interpreter with an access violation (0xC0000005 on Windows, SIGSEGV under Git Bash) - not
        # an exception, so no `except` clause can see it, and the recorder read that attribute for
        # every match it recorded. `--rows` over one such row still exits 139 and writes no file.
        #
        # What changed is that the recorder no longer reads it on a POSIX row: `fuzzy_counts` is safe
        # on a faulting match and every other read `_describe_match` makes is too, so a POSIX row
        # records its counts and omits its change positions, and the consumer drops the positions
        # from both sides. The crash is still real, still upstream's, still pinned by
        # `Gaps/Engine/FuzzyPosixTests.cs` and still entered on the ledger as entry 9; what the wave
        # now compares on this cell is the span, the groups and the error COUNTS, which is everything
        # about it upstream can be asked at all.
        #
        # Minimised to four necessary conditions (2026-09-13, .scratch/minimise-crash3.py, regex
        # 2026.7.19); removing any one of them makes it safe:
        #
        #     regex.compile(r'(?p)(?:a|aa){e<=1}').match('aa').fuzzy_changes   # access violation
        #       - POSIX:      without `(?p)` it answers ((), (), ())
        #       - an alternation with a SHORTER branch before a longer one: 'a|aa' and 'a|ab' crash,
        #         'ab|a' and 'a|b' do not, so it is leftmost-longest overriding the first branch
        #       - a budget that permits an INSERTION: '{e<=1}' and '{i<=1}' crash, '{s<=1}' and
        #         '{d<=1}' do not
        #       - a subject long enough to take the longer branch: 'aa' crashes, 'a' does not
        #
        # S43 SHARPENED THAT and the sharpening is why no narrower suppression was ever possible: the
        # faulting condition is a SPENT ERROR, not a pattern shape, and POSIX leftmost-longest can
        # stretch an apparently exact row into spending one - `(?p)(?:abc){e<=1}` over 'abcd' looks
        # exact and faults (`tools/probes/upstream-posix-fuzzy-spent-error.py`, re-run on the pinned
        # 2026.9.10). Not even the subject is a safe test, which is exactly why the guard that
        # replaced this one keys off POSIX rather than off any prediction of a spent error.
        #
        # Both draws happen before the suppression, never inside it, for the reason S42 records
        # against FUZZY_BESTMATCH_PROBABILITY: a suppression that swallows a draw reshuffles the
        # whole row stream and makes two waves incomparable. Kept as written even though there is no
        # longer a suppression to swallow one, so a future narrowing cannot reintroduce the problem.
        posix = rng.random() < INTERACTION_POSIX_PROBABILITY
        posix_inline = rng.random() < INTERACTION_POSIX_INLINE_PROBABILITY
        if posix:
            if posix_inline:
                pattern = "(?p)" + pattern
            else:
                flags |= POSIX

        # `(?r)` as inline pattern text, as S23 and S25 write it: it is what a caller writes, and a
        # global flag has to be at the start of the pattern anyway.
        reverse = rng.random() < INTERACTION_REVERSE_PROBABILITY
        if reverse:
            pattern = "(?r)" + pattern

        # ENHANCEMATCH and BESTMATCH, on the rows that have a fuzzy section for them to rank. Drawn
        # independently, so a wave holds all four combinations, and `(?b)` goes in front so a row
        # with both reads `(?b)(?e)` - the order `_generate_fuzzy` writes them in, and the one place
        # a wrong dispatch (upstream/src/_regex.c:18107) is visible.
        #
        # DRAWN BEFORE THEY ARE SUPPRESSED, never inside the `if`, for the reason S42 records against
        # FUZZY_BESTMATCH_PROBABILITY: changing which rows MAY carry a flag must not reshuffle the
        # row stream, or two waves stop being comparable. `_has_weighted_cost` is the suppression -
        # this port ranks by cost and upstream by error count, so a weighted equation under `(?e)` or
        # `(?b)` is a divergence by construction (DECISIONS 2026-09-12) and teaches nothing.
        enhance = rng.random() < INTERACTION_ENHANCE_PROBABILITY
        bestmatch = rng.random() < INTERACTION_BESTMATCH_PROBABILITY

        if not fuzzy or _has_weighted_cost(pattern):
            enhance = False
            bestmatch = False

        if enhance:
            pattern = "(?e)" + pattern
        if bestmatch:
            pattern = "(?b)" + pattern

        operation = ALL_OPERATIONS[i % len(ALL_OPERATIONS)]
        row = {
            "generator": "interactions",
            "pattern": pattern,
            "flags": flags,
            "namedLists": named_lists,
            "subject": subject,
            "operation": operation,
        }

        # `partial` only on the three operations that take one - upstream raises ValueError for the
        # rest - and the subject cut short after the pattern was built from it, which is what makes
        # the partial reachable. Cut by codepoint, and from the LEFT for a reversed row, because a
        # reversed match runs out of text at the left end. Both rules are `partial`'s own.
        if operation in OPERATIONS and rng.random() < INTERACTION_PARTIAL_PROBABILITY:
            row["partial"] = True
            if subject and rng.random() < INTERACTION_PARTIAL_CUT_PROBABILITY:
                keep = rng.randrange(len(subject) + 1)
                row["subject"] = subject[len(subject) - keep :] if reverse else subject[:keep]
        if operation in SUB_OPERATIONS:
            row["template"] = (
                _sub_template(rng, groups, names)
                if operation == "sub"
                else _subf_template(rng, groups, names)
            )
        if operation in LIMIT_OPERATIONS:
            row["count"] = rng.choice(SUB_COUNTS if operation in SUB_OPERATIONS else ITER_LIMITS)

        yield row


# The four lookaround forms, as (opener, closer). The first two are lookaheads and the last two
# lookbehinds, whose body the compiler marks reversed - so the pair is a direction test as much as an
# assertion test. The negative ones are the halves an opcode-by-opcode port gets wrong: a negative
# lookaround *succeeding* means the whole assertion failed, so it leaves by the backtrack path.
LOOKAROUND_FORMS = ("(?=", "(?!", "(?<=", "(?<!")

# What goes inside a lookaround. Deliberately not just literals: the body is an ordinary subpattern,
# so a quantifier or an alternation inside it is a variable-length lookbehind - which upstream allows
# and .NET's own engine does not, and which is therefore a shape no borrowed implementation could
# have got right by accident.
#
# 'sequence' is the one that earns its place rather than rounding out a list. Every other kind is a
# single atom, and a single atom either matches or fails *without moving*: a body that never consumed
# anything before failing never exercises the text-position restore, which is the whole job of the
# LOOKAROUND backtrack arm. A two-atom body whose first atom the subject holds and whose second
# usually does not is the shape that leaves text_pos moved when the body gives up. Measured over 600
# rows at seeds 7 and 20260911: with only the single-atom kinds, control S27-A found 1 divergence and
# 2; with 'sequence' added it found 8 and 6, and at the weight below, 12 and 7.
# 'nested' is last because the depth guard below drops the final entry.
LOOKAROUND_BODY_KINDS = ("literal", "class", "quantified", "alternation", "group", "sequence", "nested")
LOOKAROUND_BODY_WEIGHTS = (14, 12, 12, 11, 11, 30, 10)

# What a row's pattern is built from.
#   'look':            a bare lookaround, sometimes quantified - '(?=abc){3}abc' is a ported test.
#   'look-then-ref':   the headline cell. A capture made inside a *positive* lookahead stays visible
#                      after it, so '(?=(a))\1' matches; inside a negative one the capture is
#                      discarded on the way out. Both halves are emitted, because the difference
#                      between them is exactly what the two END_LOOKAROUND branches do.
#   'repeat-of-look':  a lookaround inside a repeat, so the backtrack arm runs more than once per
#                      row - the arm a single-visit row never reaches.
#   'literal'/'class': plain atoms, so the lookaround has somewhere to assert *about* other than
#                      position 0.
#   'alt-with-look':   a branch that leads with a lookaround, beside a fallback branch that does
#                      not. Everything else here reaches a lookaround once per match attempt, and a
#                      construct visited once cannot show whether what it undoes is undone properly.
#                      This one is tried and *given up within the same attempt*, which is the only
#                      way into the LOOKAROUND backtrack arm, and the only way a capture made inside
#                      an assertion that then failed can be seen leaking past it. Measured over 600
#                      rows at seeds 7 and 20260911: without it control S27-B found 1 divergence and
#                      1; with it, 7 and 6.
LOOKAROUND_PIECES = ("look", "look-then-ref", "repeat-of-look", "alt-with-look", "literal", "class")
LOOKAROUND_PIECE_WEIGHTS = (18, 16, 8, 30, 16, 12)

LOOKAROUND_AFFIXES = (("^", ""), ("", "$"), ("^", "$"), (r"\b", ""), ("", ""), ("", ""))

MAX_LOOKAROUND_PIECES = 3

# The three flags S27 names. Reverse is written inline as '(?r)', which is what a caller writes.
LOOKAROUND_IGNORECASE_PROBABILITY = 0.5
LOOKAROUND_FULLCASE_PROBABILITY = 0.5
LOOKAROUND_MULTILINE_PROBABILITY = 0.5
LOOKAROUND_REVERSE_PROBABILITY = 0.4

# As in `interactions`: a doubled subject is what lets a reference to a capture made inside a
# lookahead match at all. See _generate_lookaround for what it is worth here.
LOOKAROUND_DOUBLED_SUBJECT_PROBABILITY = 0.5


def _lookaround_body(rng: random.Random, subject: str, depth: int) -> str:
    """One lookaround body: what the assertion asks about."""
    kinds = LOOKAROUND_BODY_KINDS
    weights = LOOKAROUND_BODY_WEIGHTS
    if depth >= 1:
        # One level of nesting only. Two lookarounds inside each other is the construct; three is a
        # pattern too long for a seven-character subject to satisfy, so it would only test failure.
        kinds = kinds[:-1]
        weights = weights[:-1]

    kind = rng.choices(kinds, weights=weights)[0]
    candidates = [c for c in subject if c not in "\r\n"]

    if kind == "literal":
        return re.escape(rng.choice(candidates)) if candidates else "a"
    if kind == "class":
        return _interaction_subject_class(rng, subject) if rng.random() < 0.5 else rng.choice(INTERACTION_CLASS_ATOMS)
    if kind == "quantified":
        atom = _interaction_subject_class(rng, subject) if rng.random() < 0.5 else rng.choice(INTERACTION_CLASS_ATOMS)
        return atom + _quantifier(rng)
    if kind == "alternation":
        # '(?<=a|bc)' - the two branches have different lengths, so the lookbehind cannot be compiled
        # to a fixed step back.
        left = re.escape(rng.choice(candidates)) if candidates else "a"
        right = "".join(re.escape(c) for c in (candidates[:2] or "bc"))
        return f"{left}|{right}"
    if kind == "group":
        return "(" + _lookaround_body(rng, subject, depth + 1) + ")"
    if kind == "sequence":
        # Two or three atoms, the first drawn from the subject so it usually matches and the rest
        # left to chance so the body usually fails partway through. That is the only shape here that
        # leaves the text position moved when the body gives up, and therefore the only one that can
        # tell whether the backtrack arm puts it back.
        head = re.escape(rng.choice(candidates)) if candidates else "a"
        rest = []
        for _ in range(rng.randrange(1, 3)):
            if rng.random() < 0.5 and candidates:
                rest.append(re.escape(rng.choice(candidates)))
            else:
                rest.append(rng.choice(INTERACTION_CLASS_ATOMS))
        return head + "".join(rest)

    return rng.choice(LOOKAROUND_FORMS) + _lookaround_body(rng, subject, depth + 1) + ")"


def _lookaround_pattern(rng: random.Random, subject: str) -> tuple[str, int, list[str]]:
    """One pattern, with the group inventory a substitution template needs.

    Built left to right like ``_interaction_pattern``, so a reference is only emitted once the group
    it names exists.
    """
    counter = [0]
    names: list[str] = []
    pieces: list[str] = []
    candidates = [c for c in subject if c not in "\r\n"]

    def group(body: str) -> str:
        counter[0] += 1
        if rng.random() < 0.25:
            name = f"g{counter[0]}"
            names.append(name)
            return f"(?P<{name}>{body})"
        return f"({body})"

    for _ in range(rng.randrange(1, MAX_LOOKAROUND_PIECES + 1)):
        kind = rng.choices(LOOKAROUND_PIECES, weights=LOOKAROUND_PIECE_WEIGHTS)[0]

        if kind == "look":
            piece = rng.choice(LOOKAROUND_FORMS) + _lookaround_body(rng, subject, 0) + ")"
            pieces.append(piece + (_quantifier(rng) if rng.random() < 0.2 else ""))
        elif kind == "look-then-ref":
            form = rng.choice(LOOKAROUND_FORMS)
            body = group(_lookaround_body(rng, subject, 1))
            pieces.append(form + body + ")" + f"\\{counter[0]}")
        elif kind == "repeat-of-look":
            inner = rng.choice(LOOKAROUND_FORMS) + _lookaround_body(rng, subject, 1) + ")"
            atom = re.escape(rng.choice(candidates)) if candidates else "a"
            pieces.append(f"(?:{inner}{atom})" + _quantifier(rng))
        elif kind == "alt-with-look":
            form = rng.choice(LOOKAROUND_FORMS)
            # A capture inside the assertion most of the time: the group is what makes the leak
            # visible, since a leaked capture is reported in the groups the consumer compares.
            body = _lookaround_body(rng, subject, 1)
            body = group(body) if rng.random() < 0.6 else body
            first = re.escape(rng.choice(candidates)) if candidates else "a"
            second = re.escape(rng.choice(candidates)) if candidates else "b"
            pieces.append("(?:" + form + body + ")" + first + "|" + second + ")")
        elif kind == "literal":
            pieces.append(re.escape(rng.choice(candidates)) if candidates else "a")
        else:
            pieces.append(rng.choice(INTERACTION_CLASS_ATOMS) + (_quantifier(rng) if rng.random() < 0.4 else ""))

    prefix, suffix = rng.choice(LOOKAROUND_AFFIXES)
    return prefix + "".join(pieces) + suffix, counter[0], names


def _generate_lookaround(rng: random.Random, count: int):
    """S27's generator: the four lookaround forms, over all eight operations.

    Measured by `python tools/record-oracle.py --generator lookaround --count 600 --seed 7`, after
    the last change to this generator: 125 rows produce an answer, 469 produce none and 6 are
    rejected by upstream - an answer rate in line with `interactions` (114 of 600), which is what a
    composed pattern costs. 201 rows hold a positive lookahead, 199 a negative one, 199 a positive
    lookbehind and 208 a negative one; 281 hold two or more lookarounds, 101 a lookaround inside a
    repeat, 101 a variable-length body, 178 a reference to a group defined inside a lookaround. 288
    carry IGNORECASE, 131 FULLCASE, 306 MULTILINE, 238 are reversed and 210 have an astral subject.
    Every one of the eight operations is recorded exactly 75 times, because the operation is cycled
    by row index rather than drawn.

    Re-take from scratch after any widening: adding one entry to any table above shifts the whole RNG
    stream, so a figure measured before it describes a wave this generator no longer produces.
    """
    for i in range(count):
        alphabet = rng.choice(INTERACTION_SUBJECT_ALPHABETS)
        length = rng.randrange(1, MAX_INTERACTION_SUBJECT_LENGTH + 1)

        if rng.random() < LOOKAROUND_DOUBLED_SUBJECT_PROBABILITY:
            subject = ""
            while len(subject) < length:
                subject += rng.choice(alphabet) * 2
            subject = subject[:length]
        else:
            subject = "".join(rng.choice(alphabet) for _ in range(length))

        for _ in range(rng.randrange(3)):
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + rng.choice(INTERACTION_LINE_BREAKS) + subject[at:]

        pattern, groups, names = _lookaround_pattern(rng, subject)

        flags = 0
        if rng.random() < LOOKAROUND_IGNORECASE_PROBABILITY:
            flags |= IGNORECASE
            if rng.random() < LOOKAROUND_FULLCASE_PROBABILITY:
                flags |= FULLCASE
        if rng.random() < LOOKAROUND_MULTILINE_PROBABILITY:
            flags |= MULTILINE

        if rng.random() < LOOKAROUND_REVERSE_PROBABILITY:
            pattern = "(?r)" + pattern

        operation = ALL_OPERATIONS[i % len(ALL_OPERATIONS)]
        row = {
            "generator": "lookaround",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": operation,
        }
        if operation in SUB_OPERATIONS:
            row["template"] = (
                _sub_template(rng, groups, names) if operation == "sub" else _subf_template(rng, groups, names)
            )
        if operation in LIMIT_OPERATIONS:
            row["count"] = rng.choice(SUB_COUNTS if operation in SUB_OPERATIONS else ITER_LIMITS)

        yield row


# What a conditional's branch holds. A branch can be empty - '(?(?=X))' and '(?(?=A)A|)' are both
# ported tests - and an empty branch is where an engine that restores the wrong amount of state
# shows up, because nothing after it moves the text position to cover the mistake.
CONDITIONAL_BRANCH_KINDS = ("literal", "class", "sequence", "group", "empty")
CONDITIONAL_BRANCH_WEIGHTS = (26, 20, 20, 20, 14)

# What a row's pattern is built from.
#   'cond':           a bare conditional, sometimes quantified.
#   'cond-then-ref':  a capture made *inside the condition*, referenced after the conditional. This
#                     is the headline cell, and it is what distinguishes CONDITIONAL from a
#                     lookaround followed by a branch: a positive condition that holds leaves its
#                     captures visible, and a negative one that holds pops them back. The two
#                     END_CONDITIONAL arms differ in exactly that.
#   'repeat-of-cond': a conditional inside a repeat, so the condition is re-evaluated per iteration
#                     and the backtrack arm runs more than once per row.
#   'alt-with-cond':  a branch leading with a conditional beside a fallback that does not, so the
#                     conditional is entered and then given up within one match attempt. That is the
#                     only way into the CONDITIONAL backtrack arm (S27 learned the same thing about
#                     lookaround the hard way).
#   'literal'/'class': plain atoms, so a conditional has somewhere to assert about other than
#                     position 0.
CONDITIONAL_PIECES = ("cond", "cond-then-ref", "repeat-of-cond", "alt-with-cond", "literal", "class")
CONDITIONAL_PIECE_WEIGHTS = (20, 16, 24, 22, 12, 6)

MAX_CONDITIONAL_PIECES = 3

# 'repeat-of-cond' uses these rather than `_quantifier`, which draws '?' and '{0,1}' often enough
# that most of those rows would enter the repeat body at most once - and the slice this generator was
# written for asks for a condition that is *re-evaluated per iteration*, which a body entered once is
# not. It did not buy what it was meant to buy. Measured with the count-saving mutation S28-C
# carried at the time (`stack.PushSize(0)`), over 600 rows at seeds 7 and 20260912: 4 divergences
# and 5 with `_quantifier` at a weight of 12, and 1 and 5 with these at a weight of 24. The repeat
# stack is nearly invisible to this wave for a structural reason, not a weighting one - see S28's
# closing notes - so this is kept for the construct it produces rather than for a number it
# improved. S28-C was then changed to the mutation it now carries, which reads 2 and 8.
CONDITIONAL_REPEAT_QUANTIFIERS = ("+", "*", "{1,3}", "{2,3}", "+?", "{1,3}?")

# How often a condition body is a repeat over more than one character. It has to be more than one,
# because a single-character repeat compiles to GREEDY_REPEAT_ONE, which carries no RE_RepeatData -
# and RE_RepeatData is precisely what push_repeats/pop_repeats save and restore. Without this the
# generator would exercise the repeat stack only by accident.
CONDITIONAL_REPEAT_CONDITION_PROBABILITY = 0.35

# How often the conditional has a 'no' branch at all, and how often the 'no' branch is itself a
# conditional - '(?(?<=A)|(?(?![^B])C|D))' is a ported regression.
CONDITIONAL_ELSE_PROBABILITY = 0.7
CONDITIONAL_NESTED_PROBABILITY = 0.2

CONDITIONAL_AFFIXES = (("^", ""), ("", "$"), ("^", "$"), (r"\b", ""), ("", ""), ("", ""))

CONDITIONAL_IGNORECASE_PROBABILITY = 0.5
CONDITIONAL_FULLCASE_PROBABILITY = 0.5
CONDITIONAL_MULTILINE_PROBABILITY = 0.5
CONDITIONAL_REVERSE_PROBABILITY = 0.4
CONDITIONAL_DOUBLED_SUBJECT_PROBABILITY = 0.5


def _conditional_branch(rng: random.Random, subject: str, group) -> str:
    """One branch of a conditional: the 'yes' or the 'no'."""
    candidates = [c for c in subject if c not in "\r\n"]
    kind = rng.choices(CONDITIONAL_BRANCH_KINDS, weights=CONDITIONAL_BRANCH_WEIGHTS)[0]

    if kind == "empty":
        return ""
    if kind == "literal":
        return re.escape(rng.choice(candidates)) if candidates else "a"
    if kind == "class":
        return rng.choice(INTERACTION_CLASS_ATOMS) + (_quantifier(rng) if rng.random() < 0.4 else "")
    if kind == "group":
        return group(re.escape(rng.choice(candidates)) if candidates else "a")

    # A two-atom branch, the first drawn from the subject and the second usually not: the shape that
    # is entered, moves the text position and then fails, so the row reaches the backtrack arm with
    # a branch half-matched rather than never started.
    head = re.escape(rng.choice(candidates)) if candidates else "a"
    return head + rng.choice(INTERACTION_CLASS_ATOMS)


def _conditional_condition(rng: random.Random, subject: str) -> str:
    """The condition: one of the four lookaround forms round a body."""
    form = rng.choice(LOOKAROUND_FORMS)

    if rng.random() < CONDITIONAL_REPEAT_CONDITION_PROBABILITY:
        candidates = [c for c in subject if c not in "\r\n"]
        head = re.escape(rng.choice(candidates)) if candidates else "a"
        tail = rng.choice(INTERACTION_CLASS_ATOMS)
        body = f"(?:{head}|{tail}){_quantifier(rng)}"
    else:
        body = _lookaround_body(rng, subject, 1)

    return form + body + ")"


def _conditional(rng: random.Random, subject: str, group, depth: int = 0) -> str:
    """One conditional, '(?' + condition + 'yes' + optional '|no' + ')'."""
    condition = _conditional_condition(rng, subject)
    yes = _conditional_branch(rng, subject, group)

    if depth == 0 and rng.random() < CONDITIONAL_NESTED_PROBABILITY:
        no = "|" + _conditional(rng, subject, group, depth + 1)
    elif rng.random() < CONDITIONAL_ELSE_PROBABILITY:
        no = "|" + _conditional_branch(rng, subject, group)
    else:
        no = ""

    return "(?" + condition + yes + no + ")"


def _conditional_pattern(rng: random.Random, subject: str) -> tuple[str, int, list[str]]:
    """One pattern, with the group inventory a substitution template needs.

    Built left to right like ``_lookaround_pattern``, so a reference is only emitted once the group
    it names exists.
    """
    counter = [0]
    names: list[str] = []
    pieces: list[str] = []
    candidates = [c for c in subject if c not in "\r\n"]

    def group(body: str) -> str:
        counter[0] += 1
        if rng.random() < 0.25:
            name = f"g{counter[0]}"
            names.append(name)
            return f"(?P<{name}>{body})"
        return f"({body})"

    for _ in range(rng.randrange(1, MAX_CONDITIONAL_PIECES + 1)):
        kind = rng.choices(CONDITIONAL_PIECES, weights=CONDITIONAL_PIECE_WEIGHTS)[0]

        if kind == "cond":
            pieces.append(_conditional(rng, subject, group) + (_quantifier(rng) if rng.random() < 0.2 else ""))
        elif kind == "cond-then-ref":
            form = rng.choice(LOOKAROUND_FORMS)
            condition = form + group(_lookaround_body(rng, subject, 1)) + ")"
            # '\g<1>' rather than '\1': the next piece often begins with a digit, and a bare '\1'
            # followed by '0' is read as group 10, which upstream rejects outright - so the row
            # tests the parse-error path instead of the cell this piece exists for. Verified against
            # regex 2026.7.19 on 2026-09-11: `regex.match(r'(a)\g<1>0', 'aa0')` matches, and
            # `regex.match(r'(a)\10', 'aa0')` raises "invalid group reference at position 6".
            reference = f"\\g<{counter[0]}>"
            yes = _conditional_branch(rng, subject, group)
            no = ("|" + _conditional_branch(rng, subject, group)) if rng.random() < CONDITIONAL_ELSE_PROBABILITY else ""
            pieces.append("(?" + condition + yes + no + ")" + reference)
        elif kind == "repeat-of-cond":
            atom = re.escape(rng.choice(candidates)) if candidates else "a"
            pieces.append(
                f"(?:{_conditional(rng, subject, group)}{atom})" + rng.choice(CONDITIONAL_REPEAT_QUANTIFIERS)
            )
        elif kind == "alt-with-cond":
            first = re.escape(rng.choice(candidates)) if candidates else "a"
            second = re.escape(rng.choice(candidates)) if candidates else "b"
            pieces.append("(?:" + _conditional(rng, subject, group) + first + "|" + second + ")")
        elif kind == "literal":
            pieces.append(re.escape(rng.choice(candidates)) if candidates else "a")
        else:
            pieces.append(rng.choice(INTERACTION_CLASS_ATOMS) + (_quantifier(rng) if rng.random() < 0.4 else ""))

    prefix, suffix = rng.choice(CONDITIONAL_AFFIXES)
    return prefix + "".join(pieces) + suffix, counter[0], names


def _generate_conditionals(rng: random.Random, count: int):
    """S28's generator: the lookaround-condition form, over all eight operations.

    The subject material is ``lookaround``'s, for the same reason: a conditional's condition is a
    lookaround, and the rows only say anything if the condition sometimes holds and sometimes does
    not.

    Measured by `python tools/record-oracle.py --generator conditionals --count 600 --seed 7`, after
    the last change to this generator: 30 rows answer with a single match, 195 with no match, 150
    with a match list, 145 with a substitution and 75 with a split, and 5 are rejected by upstream.
    248 rows hold a positive lookahead condition, 252 a negative one, 243 a positive lookbehind and
    235 a negative one; 368 hold two or more conditionals, 430 a conditional inside a repeat, 294 a
    condition whose body is a repeat, 13 a conditional nested in another's no-branch and 181 a
    reference to a group defined inside a condition. 295 carry IGNORECASE, 139 FULLCASE, 308
    MULTILINE, 244 are reversed and 221 have an astral subject. Every one of the eight operations is
    recorded exactly 75 times, because the operation is cycled by row index rather than drawn.

    Re-take from scratch after any widening: adding one entry to any table above shifts the whole RNG
    stream, so a figure measured before it describes a wave this generator no longer produces.
    """
    for i in range(count):
        alphabet = rng.choice(INTERACTION_SUBJECT_ALPHABETS)
        length = rng.randrange(1, MAX_INTERACTION_SUBJECT_LENGTH + 1)

        if rng.random() < CONDITIONAL_DOUBLED_SUBJECT_PROBABILITY:
            subject = ""
            while len(subject) < length:
                subject += rng.choice(alphabet) * 2
            subject = subject[:length]
        else:
            subject = "".join(rng.choice(alphabet) for _ in range(length))

        for _ in range(rng.randrange(3)):
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + rng.choice(INTERACTION_LINE_BREAKS) + subject[at:]

        pattern, groups, names = _conditional_pattern(rng, subject)

        flags = 0
        if rng.random() < CONDITIONAL_IGNORECASE_PROBABILITY:
            flags |= IGNORECASE
            if rng.random() < CONDITIONAL_FULLCASE_PROBABILITY:
                flags |= FULLCASE
        if rng.random() < CONDITIONAL_MULTILINE_PROBABILITY:
            flags |= MULTILINE

        if rng.random() < CONDITIONAL_REVERSE_PROBABILITY:
            pattern = "(?r)" + pattern

        operation = ALL_OPERATIONS[i % len(ALL_OPERATIONS)]
        row = {
            "generator": "conditionals",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": operation,
        }
        if operation in SUB_OPERATIONS:
            row["template"] = (
                _sub_template(rng, groups, names) if operation == "sub" else _subf_template(rng, groups, names)
            )
        if operation in LIMIT_OPERATIONS:
            row["count"] = rng.choice(SUB_COUNTS if operation in SUB_OPERATIONS else ITER_LIMITS)

        yield row


# The two verbs this generator exists for. '(*FAIL)' is not drawn: it is the FAILURE opcode and has
# been matching since S16, so a row carrying it would spend itself on a delivered cell.
VERBS = ("(*PRUNE)", "(*SKIP)")

# The runs a verb cuts behind. Greedy, lazy and possessive, because the three leave different
# amounts on the backtracking stack for the verb to cut: a greedy run leaves one entry per position
# it could give back, a lazy run leaves one per position it could take, and a possessive run leaves
# none at all - so a verb after a possessive run is the case where cutting too far and cutting
# correctly look identical, and the lookbehind pieces are the only thing that separates them. The
# possessive forms are also what upstream's own ported tests use ('\d++(?<=3(*PRUNE))zzd|[4d]$').
VERB_RUN_QUANTIFIERS = ("+", "*", "+?", "*?", "{1,3}", "{1,3}?", "{2,4}", "++", "*+")

# What a row's pattern is built from.
#   'run-then-verb':    a run, the verb, then an atom that may or may not match where the run
#                       stopped. This is the headline cell and upstream's own '\d+(*PRUNE)\d': with
#                       the verb the run cannot be given back, so the row answers "no match" where
#                       the same pattern without the verb matches.
#   'alt-with-verb':    the verb inside one branch of an alternation, beside a fallback branch. The
#                       two verbs differ from each other here rather than in isolation - PRUNE cuts
#                       the backtracking but leaves the search position alone, SKIP also moves the
#                       slice start, so the *next* match's start is what tells them apart, and only
#                       a multi-match operation can see it.
#   'atomic-with-verb': the verb inside an atomic group. This is upstream issue 613's shape, where a
#                       SKIP inside an atomic group leaves a stale backtrack limit behind.
#   'look-with-verb':   the verb inside a positive or negative lookaround, sometimes with a body and
#                       sometimes bare ('(?<=(*PRUNE))' is a ported test). A lookaround pushes its
#                       own pruning-stack entry, so a verb inside one must cut back only to the
#                       lookaround's entry and leave the backtracking outside it alone.
#   'repeat-of-verb':   the verb inside a repeat body, so it runs once per iteration and cuts a
#                       backtracking stack that the enclosing repeat is still adding to.
#   'literal'/'class':  plain atoms, so a verb has somewhere to prune *behind* other than position 0.
VERB_PIECES = (
    "run-then-verb",
    "alt-with-verb",
    "atomic-with-verb",
    "look-with-verb",
    "repeat-of-verb",
    "literal",
    "class",
)
VERB_PIECE_WEIGHTS = (24, 22, 12, 16, 12, 8, 6)

MAX_VERB_PIECES = 3

# A widening was tried here and reverted, and the reason is worth keeping so nobody repeats it.
# S29 weighted the piece count towards one, raised the doubled-subject probability, dropped most of
# the anchoring affixes, broadened the run atoms and raised the share of atoms drawn from the
# subject - all aimed at the one cell a moved slice start can show in, which needs a row that
# matches more than once. Measured at 4800 rows, seed 7, before and after: the overlapped rows
# carrying a '(*SKIP)' went 384 -> 319, those matching more than once 23 -> 49, and the rows where
# the moved slice start actually changes the answer 13 -> 12. So it doubled the coverage it aimed at
# and did not move the cell at all - what limits that cell is not "does the row match twice" but
# "does a '(*SKIP)' land two or more characters past the match start and the pattern still match one
# position on".
#
# It was reverted because it also drove the wave into a region where upstream's own answer is not
# well defined - '(*SKIP)' at or near the end of a match, read by an overlapped scan. See the S29
# closing notes and DECISIONS 2026-09-11: in one shape upstream returns four matches or six for the
# same call depending on whether the caller kept the previous MatchObject alive. The narrow
# generator stays until Phase 6 has the intentional-divergence allowlist that region needs.

# 'repeat-of-verb' draws from these rather than from `_quantifier`, for the reason S28 recorded
# about `CONDITIONAL_REPEAT_QUANTIFIERS`: '?' and '{0,1}' enter the body at most once, and a verb
# that runs once inside a repeat is just a verb.
VERB_REPEAT_QUANTIFIERS = ("+", "*", "{1,3}", "{2,3}", "+?", "{1,3}?")

# How often a lookaround holding a verb has a body at all, and how often the verb comes after that
# body rather than before it. Upstream's ported tests cover all three arrangements - '(?<=3(*PRUNE))',
# '(?<=(*PRUNE)3)' and '(?<=2(*PRUNE)3)' - and they are not the same row: a verb before the body
# prunes a lookaround that has not matched anything yet.
VERB_LOOKAROUND_BODY_PROBABILITY = 0.7
VERB_LOOKAROUND_VERB_LAST_PROBABILITY = 0.5

# How often a piece is wrapped in a capture group, so a substitution template has something to name
# and so END_GROUP sits between the verb and the pruning point.
VERB_GROUP_PROBABILITY = 0.3

VERB_AFFIXES = (("^", ""), ("", "$"), (r"\b", ""), ("", ""), ("", ""), ("", ""))

VERB_IGNORECASE_PROBABILITY = 0.4
VERB_FULLCASE_PROBABILITY = 0.5
VERB_MULTILINE_PROBABILITY = 0.4
VERB_REVERSE_PROBABILITY = 0.4

VERB_DOUBLED_SUBJECT_PROBABILITY = 0.5


def _verb_atom(rng: random.Random, subject: str) -> str:
    """One atom: half the time a character the subject actually holds, half the time a class.

    Both halves are needed. An atom drawn from the subject is what makes the tail after a verb
    sometimes match, and a class is what makes it sometimes not - a generator that only ever emitted
    one of them would produce rows that all answer the same way.
    """
    candidates = [c for c in subject if c not in "\r\n"]
    if candidates and rng.random() < 0.5:
        return re.escape(rng.choice(candidates))
    return rng.choice(INTERACTION_CLASS_ATOMS)


def _verb_run(rng: random.Random) -> str:
    """A quantified class: the backtracking the verb behind it cuts."""
    return rng.choice(INTERACTION_CLASS_ATOMS) + rng.choice(VERB_RUN_QUANTIFIERS)


def _verb_piece(rng: random.Random, subject: str) -> str:
    """One piece of a pattern, per the table above."""
    kind = rng.choices(VERB_PIECES, weights=VERB_PIECE_WEIGHTS)[0]
    verb = rng.choice(VERBS)

    if kind == "run-then-verb":
        return _verb_run(rng) + verb + _verb_atom(rng, subject)

    if kind == "alt-with-verb":
        return (
            "(?:"
            + _verb_run(rng)
            + verb
            + _verb_atom(rng, subject)
            + "|"
            + _verb_atom(rng, subject)
            + ")"
        )

    if kind == "atomic-with-verb":
        return "(?>" + _verb_run(rng) + verb + _verb_atom(rng, subject) + ")"

    if kind == "look-with-verb":
        form = rng.choice(LOOKAROUND_FORMS)
        body = _verb_atom(rng, subject) if rng.random() < VERB_LOOKAROUND_BODY_PROBABILITY else ""
        inside = (body + verb) if rng.random() < VERB_LOOKAROUND_VERB_LAST_PROBABILITY else (verb + body)
        return _verb_run(rng) + form + inside + ")" + _verb_atom(rng, subject)

    if kind == "repeat-of-verb":
        return (
            "(?:"
            + rng.choice(INTERACTION_CLASS_ATOMS)
            + verb
            + ")"
            + rng.choice(VERB_REPEAT_QUANTIFIERS)
        )

    if kind == "literal":
        candidates = [c for c in subject if c not in "\r\n"]
        return re.escape(rng.choice(candidates)) if candidates else "a"

    return rng.choice(INTERACTION_CLASS_ATOMS) + (_quantifier(rng) if rng.random() < 0.4 else "")


def _verb_pattern(rng: random.Random, subject: str) -> tuple[str, int, list[str]]:
    """One pattern, with the group inventory a substitution template needs."""
    counter = [0]
    names: list[str] = []
    pieces: list[str] = []

    def group(body: str) -> str:
        counter[0] += 1
        if rng.random() < 0.25:
            name = f"g{counter[0]}"
            names.append(name)
            return f"(?P<{name}>{body})"
        return f"({body})"

    for _ in range(rng.randrange(1, MAX_VERB_PIECES + 1)):
        piece = _verb_piece(rng, subject)
        if rng.random() < VERB_GROUP_PROBABILITY:
            piece = group(piece)
        pieces.append(piece)

    prefix, suffix = rng.choice(VERB_AFFIXES)
    return prefix + "".join(pieces) + suffix, counter[0], names


def _generate_verbs(rng: random.Random, count: int):
    """S29's generator: '(*PRUNE)' and '(*SKIP)', over all eight operations.

    The subject material is ``lookaround``'s and ``conditionals``', for a reason of its own: a verb
    only says anything about a row where the run in front of it could have been given back, so the
    subjects want repeated characters, and the doubling below is what supplies them.

    Measured by `python tools/record-oracle.py --generator verbs --count 600 --seed 7`, after the
    last change to this generator: 27 rows answer with a single match, 198 with no match, 150 with a
    match list, 139 with a substitution and 75 with a split, and 11 are rejected by upstream (all
    eleven on the substitution template, not on the pattern). 373 rows hold a '(*PRUNE)' and 380 a
    '(*SKIP)', 329 hold two or more verbs and 41 hold none; 130 put a verb in an atomic group, 168
    in or beside a lookaround, 187 behind a possessive run. 248 carry IGNORECASE, 120 FULLCASE, 254
    MULTILINE, 260 are reversed and 199 have an astral subject. Every one of the eight operations is
    recorded exactly 75 times, because the operation is cycled by row index rather than drawn.

    Re-take from scratch after any widening: adding one entry to any table above shifts the whole RNG
    stream, so a figure measured before it describes a wave this generator no longer produces.
    """
    for i in range(count):
        alphabet = rng.choice(INTERACTION_SUBJECT_ALPHABETS)
        length = rng.randrange(1, MAX_INTERACTION_SUBJECT_LENGTH + 1)

        if rng.random() < VERB_DOUBLED_SUBJECT_PROBABILITY:
            subject = ""
            while len(subject) < length:
                subject += rng.choice(alphabet) * 2
            subject = subject[:length]
        else:
            subject = "".join(rng.choice(alphabet) for _ in range(length))

        for _ in range(rng.randrange(3)):
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + rng.choice(INTERACTION_LINE_BREAKS) + subject[at:]

        pattern, groups, names = _verb_pattern(rng, subject)

        flags = 0
        if rng.random() < VERB_IGNORECASE_PROBABILITY:
            flags |= IGNORECASE
            if rng.random() < VERB_FULLCASE_PROBABILITY:
                flags |= FULLCASE
        if rng.random() < VERB_MULTILINE_PROBABILITY:
            flags |= MULTILINE

        if rng.random() < VERB_REVERSE_PROBABILITY:
            pattern = "(?r)" + pattern

        operation = ALL_OPERATIONS[i % len(ALL_OPERATIONS)]
        row = {
            "generator": "verbs",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": operation,
        }
        if operation in SUB_OPERATIONS:
            row["template"] = (
                _sub_template(rng, groups, names) if operation == "sub" else _subf_template(rng, groups, names)
            )
        if operation in LIMIT_OPERATIONS:
            row["count"] = rng.choice(SUB_COUNTS if operation in SUB_OPERATIONS else ITER_LIMITS)

        yield row


# --------------------------------------------------------------------------------------------
# S30's generator: recursion and group calls
# --------------------------------------------------------------------------------------------


# Subjects for the self-similar shapes, which only say anything when the brackets actually nest.
# The last two carry an astral character and an expanding fold, so a call can step over a surrogate
# pair and a called group can reach the folding path.
RECURSION_SUBJECT_ALPHABETS = (
    # S52. A balanced-bracket subject whose payload is two UTF-16 units, so a recursive call has to
    # step over a surrogate pair to find its own closing bracket.
    "()" + ASTRAL_ALPHABET,
    "()ab",
    "()()ab",
    "[]<>ab",
    "(){}a,",
    "aA\U0001f600()",
    "aAßﬃ()",
)

# Every whole-pattern shape this generator draws, each checked by hand for one property: it cannot
# reach its own recursive call without first consuming a character, in either direction. That
# matters because the generator also reverses about a third of its rows, and `(?r)` turns a trailing
# recursive call into a leading one - `\w(?R)?` is safe as written and raises MemoryError from
# upstream the moment `(?r)` is prefixed (measured 2026-09-11), because the recursion then never
# advances. Anything added here gets the same check, under both directions.
#
# Deliberately absent: a call inside a *lookbehind*, `(?<=(?&name))`. Upstream matches that shape
# only when the lookbehind is the whole pattern and fails on every longer sequence, while this port
# matches it - the divergence S30 minimised and pinned in
# tests/FuzzyRegex.Tests/Gaps/Engine/GroupCallTests.cs. Drawing it here would colour the whole wave
# with one known upstream defect and hide whatever else a run found. A lookbehind *inside* a called
# group is drawn, because the two engines agree on it.
RECURSION_SHAPES = (
    # Balanced brackets, one line per call syntax, so each of the seven forms is exercised.
    r"\((?:[^()]|(?R))*\)",
    r"\((?:[^()]|(?0))*\)",
    r"(?<b>\((?:[^()]|(?&b))*\))",
    r"(?<b>\((?:[^()]|(?P>b))*\))",
    r"(\((?:[^()]|(?1))*\))",
    r"(\((?:[^()]|(?-1))*\))",
    r"(\[(?:[^\[\]]|(?1))*\])",
    r"(<(?:[^<>]|(?1))*>)",
    # Palindrome shapes: the call sits between two copies of the same atom.
    r"(?<p>a(?&p)?a|b)",
    r"(?<p>[ab](?&p)?[ab]|[ab])",
    r"a(?R)?b",
    # A backreference to a group that was set inside the call. Upstream restores the caller's
    # spans on return but not its capture lists, so what the reference reads is the question.
    r"(?<w>(?<c>\w)(?&w)?\2)",
    r"(?<g>(\w))(?&g)\2",
    # A lookaround inside a called group, and a call inside a lookahead.
    r"(?<g>\w(?=\w))(?&g)",
    r"(?<g>\w(?<=\w))(?&g)",
    r"(?<g>[ab]+)(?=(?&g))",
    r"(?<g>[ab]+)(?!(?&g))",
    # A call inside a repeat, which is what makes GROUP_CALL push and pop the repeat state.
    r"(?<g>[ab])(?:(?&g))*",
    r"(?<g>[ab])(?:(?&g))+",
    r"(?<g>[ab])(?&g)?",
    r"(?<g>[ab]{1,3})(?:(?&g))*",
    # A repeat *inside* a called group, which is the shape GROUP_CALL's guard clearing exists for:
    # the caller's own pass guards positions in the repeat's body, and the call then walks the same
    # positions. Without these four the control that deletes that clearing barely fires - it found
    # 0 rows of 600 at one seed and 4 at another before they were added.
    r"(?<g>(?:[ab]+c)*d)(?&g)",
    r"(?<g>(?:[ab]*c)+)(?&g)?",
    r"(?<g>(?:[ab]|cd)+)(?&g)",
    r"((?:[ab]+,)*;)(?1)",
    r"(?<b>\((?:[^()]+|(?&b))*\))",
    # Plain calls: forward, backward and relative.
    r"(?+1)(?<h>[ab])",
    r"(?<g>[ab])(?<h>[ab])(?-1)",
    r"(?<g>[ab])(?<h>[ab])(?-2)",
    r"([ab])(?1)",
    # A group that is only ever reached through a call.
    r"(?(DEFINE)(?<d>[ab]+))(?&d)",
    r"(?(DEFINE)(?<d>[ab]+))(?&d)x?",
)

RECURSION_AFFIXES = (("^", ""), ("", "$"), ("^", "$"), (r"\b", ""), ("", ""), ("", ""))

RECURSION_IGNORECASE_PROBABILITY = 0.4
RECURSION_FULLCASE_PROBABILITY = 0.5
RECURSION_MULTILINE_PROBABILITY = 0.3
RECURSION_REVERSE_PROBABILITY = 0.35

# How often a shape gets an ordinary atom stuck on the end, so the call is not always the last thing
# in the pattern and the state GROUP_RETURN restores has to serve something afterwards.
RECURSION_TAIL_PROBABILITY = 0.35

MAX_RECURSION_SUBJECT_LENGTH = 10


def _recursion_pattern(rng: random.Random, subject: str) -> tuple[str, int, list[str]]:
    """One pattern, with the group inventory a substitution template needs.

    The shape carries the recursion; the optional tail and the affixes are what stop every row
    being the same pattern in a different order.
    """
    shape = rng.choice(RECURSION_SHAPES)

    tail = ""
    if rng.random() < RECURSION_TAIL_PROBABILITY:
        candidates = [c for c in subject if c not in "\r\n"]
        atom = re.escape(rng.choice(candidates)) if candidates else "a"
        tail = atom if rng.random() < 0.5 else rng.choice(INTERACTION_CLASS_ATOMS) + "?"

    prefix, suffix = rng.choice(RECURSION_AFFIXES)
    pattern = prefix + shape + tail + suffix

    # Counted rather than tracked while building, because the shapes are literals: the capture
    # groups are the '(' that is not '(?', and the names are the '(?<name>' forms.
    groups = len(re.findall(r"\((?!\?)", shape)) + len(re.findall(r"\(\?P?<[A-Za-z_]", shape))
    names = re.findall(r"\(\?P?<([A-Za-z_][A-Za-z_0-9]*)>", shape)
    return pattern, groups, names


def _generate_recursion(rng: random.Random, count: int):
    """S30's generator: '(?R)', '(?0)', '(?1)', '(?-1)', '(?+1)', '(?&name)' and '(?P>name)'.

    The subjects are bracket-heavy rather than ``lookaround``'s, because a self-similar shape only
    reaches its own call on a subject that nests - a wave over letters would record 600 rows of
    'no match' and say nothing about GROUP_CALL at all.

    Measured by `python tools/record-oracle.py --generator recursion --count 600 --seed 7`, after the
    last change to this generator: 22 rows answer with a single match, 203 with no match, 150 with a
    match list, 142 with a substitution and 75 with a split, and 8 are rejected by upstream. Every one
    of the seven call syntaxes is drawn - 350 rows hold a '(?&name)', 103 a '(?N)', 54 a '(?-N)', 30 a
    '(?R)', 22 a '(?P>name)', 21 a '(?+N)' and 20 a '(?0)'; 44 hold a '(?(DEFINE)...)' group that is
    only ever reached through a call, 59 a lookaround containing or contained by one, and 43 a
    backreference to a group the call set. 230 carry IGNORECASE, 121 FULLCASE, 171 MULTILINE, 197 are
    reversed and 79 have an astral subject. Every one of the eight operations is recorded exactly 75
    times, because the operation is cycled by row index rather than drawn.

    Re-take from scratch after any widening: adding one entry to any table above shifts the whole RNG
    stream, so a figure measured before it describes a wave this generator no longer produces.
    """
    for i in range(count):
        alphabet = rng.choice(RECURSION_SUBJECT_ALPHABETS)
        length = rng.randrange(1, MAX_RECURSION_SUBJECT_LENGTH + 1)
        subject = "".join(rng.choice(alphabet) for _ in range(length))

        for _ in range(rng.randrange(2)):
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + rng.choice(INTERACTION_LINE_BREAKS) + subject[at:]

        pattern, groups, names = _recursion_pattern(rng, subject)

        flags = 0
        if rng.random() < RECURSION_IGNORECASE_PROBABILITY:
            flags |= IGNORECASE
            if rng.random() < RECURSION_FULLCASE_PROBABILITY:
                flags |= FULLCASE
        if rng.random() < RECURSION_MULTILINE_PROBABILITY:
            flags |= MULTILINE

        if rng.random() < RECURSION_REVERSE_PROBABILITY:
            pattern = "(?r)" + pattern

        operation = ALL_OPERATIONS[i % len(ALL_OPERATIONS)]
        row = {
            "generator": "recursion",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": operation,
        }
        if operation in SUB_OPERATIONS:
            row["template"] = (
                _sub_template(rng, groups, names) if operation == "sub" else _subf_template(rng, groups, names)
            )
        if operation in LIMIT_OPERATIONS:
            row["count"] = rng.choice(SUB_COUNTS if operation in SUB_OPERATIONS else ITER_LIMITS)

        yield row


# How often a `partial` row keeps the whole subject rather than cutting it. An uncut row is where a
# COMPLETE match exists and a longer partial one would too, which is the case do_match's fallback
# has to get right: it tries a normal match first (:18140-18162) and only falls back when that
# fails, so an uncut row must never come back partial.
PARTIAL_UNCUT_PROBABILITY = 0.25

# How often a `partial` row is asked WITHOUT partial=True. The same pattern and the same cut subject
# through both doors: the flag is the only difference, so a row pair pins that it is the flag and
# not the shape that produced the answer.
PARTIAL_ASKED_PROBABILITY = 0.75


def _generate_partial(rng: random.Random, count: int, sliced: bool = False):
    """S31's generator: the S16-S25 constructs, over a subject cut short, under ``partial=True``.

    The pattern comes from ``_interaction_pattern`` rather than from a grammar of its own, because
    "every construct the engine has, with the subject cut short" is exactly `interactions`' pattern
    pool asked a different question - and a second grammar would drift from the first at every
    later slice. What this generator owns is the SUBJECT: it is built whole, the pattern is composed
    against the whole of it so the row can match at all, and only then is it cut.

    The cut is at the END for a forward pattern and at the START for a ``(?r)`` one, because that is
    where each runs out of text: ``state_init`` sets ``partial_side`` to LEFT when the pattern is
    reversed (``upstream/src/_regex.c:18627``) and ``do_match`` forces ``text_pos`` to
    ``slice_start`` rather than ``slice_end`` for it (``:18175-18180``). Cutting a reversed pattern's
    subject at the end would test the side it does not use.

    Only ``search``, ``match`` and ``fullmatch``: ``findall`` refuses ``partial`` outright and this
    recorder has no ``finditer``-with-partial row shape. The scanner's own partial behaviour is
    pinned by measurement in ``tests/FuzzyRegex.Tests/Gaps/Engine/IterationTests.cs`` instead.

    Measured by `python tools/record-oracle.py --generator partial --count 600 --seed 31`, after the
    last change to this generator: 196 rows answer with a match and 403 with none, and 1 is rejected
    by upstream. 458 rows ask for a partial and 142 do not; of the 196 matches, **119 are partial
    matches and 77 are complete ones** - the complete ones are the fallback's own test, since a row
    that could match completely must never come back partial. 286 rows are reversed, so the LEFT
    partial side is drawn on nearly half, and 91 have an empty subject after the cut. Each of the
    three operations is recorded exactly 200 times, because the operation is cycled by row index
    rather than drawn.

    Re-take from scratch after any widening: adding one entry to any table this reads shifts the
    whole RNG stream, so a figure measured before it describes a wave this generator no longer
    produces.
    """
    for i in range(count):
        alphabet = rng.choice(INTERACTION_SUBJECT_ALPHABETS)
        length = rng.randrange(1, MAX_INTERACTION_SUBJECT_LENGTH + 1)

        if rng.random() < INTERACTION_DOUBLED_SUBJECT_PROBABILITY:
            subject = ""
            while len(subject) < length:
                subject += rng.choice(alphabet) * 2
            subject = subject[:length]
        else:
            subject = "".join(rng.choice(alphabet) for _ in range(length))

        for _ in range(rng.randrange(3)):
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + rng.choice(INTERACTION_LINE_BREAKS) + subject[at:]

        version1 = rng.random() < INTERACTION_VERSION1_PROBABILITY
        # Composed against the WHOLE subject, then the subject is cut - so the pattern is one that
        # had a chance of matching the full text, which is what makes a prefix of it a candidate
        # for a partial match rather than a guaranteed miss.
        pattern, _groups, _names, _lists, _fuzzy = _interaction_pattern(rng, subject, version1)

        flags = 0
        if version1:
            flags |= VERSION1
        if rng.random() < INTERACTION_IGNORECASE_PROBABILITY:
            flags |= IGNORECASE
            if rng.random() < INTERACTION_FULLCASE_PROBABILITY:
                flags |= FULLCASE
        if rng.random() < INTERACTION_MULTILINE_PROBABILITY:
            flags |= MULTILINE
        # The same exclusion `interactions` makes: upstream does not agree with itself about a cased
        # property under ASCII, so such a row is a guaranteed divergence that says nothing here.
        if rng.random() < INTERACTION_ASCII_PROBABILITY and not re.search(r"\\[pP]\{|\[\[:", pattern):
            flags |= ASCII

        reverse = rng.random() < INTERACTION_REVERSE_PROBABILITY
        if reverse:
            pattern = "(?r)" + pattern

        # Every prefix length from 0 to full, drawn uniformly, with the uncut length also reachable
        # by its own probability so the "a complete match exists" case is not left to chance on a
        # long subject. Sliced by codepoint, so an astral character is never cut in half - a cut
        # through a surrogate pair would ask upstream and this port different questions about
        # UTF-16 rather than about partial matching.
        if rng.random() < PARTIAL_UNCUT_PROBABILITY:
            cut = subject
        else:
            keep = rng.randrange(len(subject) + 1)
            # The reversed pattern reads right to left and runs out of text at the LEFT end, so its
            # subject keeps the TAIL and loses the head.
            cut = subject[len(subject) - keep :] if reverse else subject[:keep]

        row = {
            "generator": "partial-sliced" if sliced else "partial",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": cut,
            "operation": OPERATIONS[i % len(OPERATIONS)],
        }
        if rng.random() < PARTIAL_ASKED_PROBABILITY:
            row["partial"] = True

        # `partial-sliced` only: a narrowed slice, which is the OTHER way a partial match can run
        # out of text. Half of upstream's partial arms are bounded by slice_start/slice_end and
        # half by text_start/text_end - compare try_match_STRING (:7396, slice_end) with the STRING
        # opcode's own arm in basic_match (text_end) - and the two agree exactly as long as the
        # slice is the whole subject. Raised by the S31 blind review, and left as its own generator
        # because this port diverges on it: see the Generator note in tools/run-oracle.ps1.
        #
        # Drawn by codepoint index; `_record_row` translates to UTF-16, so a slice edge never falls
        # inside a surrogate pair.
        if sliced and cut:
            lo = rng.randrange(len(cut) + 1)
            hi = rng.randrange(lo, len(cut) + 1)
            row["pos"] = lo
            row["endpos"] = hi

        yield row


# regex.POSIX (upstream/regex/_regex_core.py), and also FuzzyRegexOptions.Posix - the oracle hands
# the same integer to both sides.
POSIX = 0x10000

# The alphabets a `posix` row draws from, cycled row by row. Deliberately tiny: leftmost-longest
# only answers differently from leftmost-first when a shorter alternative is a prefix of a longer
# one, and a wide alphabet makes that collision rare. The third band holds a BMP and an astral
# character so `check_posix_match`'s length comparison is asked about a surrogate pair - it counts
# UTF-16 code units in this port and codepoints upstream, and the two must order the same way.
POSIX_SUBJECT_ALPHABETS = ("ab", "abc", "aé\U0001f600", "ab" + ASTRAL_ALPHABET)

MAX_POSIX_SUBJECT_LENGTH = 6

# How often a subject gets a prefix of itself glued on the end. A chain like `a|ab|abc` only reaches
# its longer alternatives on a subject that repeats, and an independently drawn one rarely does.
POSIX_DOUBLED_SUBJECT_PROBABILITY = 0.5

# How often the alternatives of a chain are shuffled out of shortest-first order. Shortest-first is
# the interesting order - it is where leftmost-first takes the short branch and POSIX does not - so
# it is the default, and the shuffle exists so that "the alternation order does not matter under
# POSIX" is recorded too.
POSIX_SHUFFLE_PROBABILITY = 0.25

# How often a row actually asks for POSIX. The rest are the same grammar through the ordinary door,
# which is what pins that the FLAG and not the shape produced the answer - the same pairing
# `partial` makes with PARTIAL_ASKED_PROBABILITY.
POSIX_APPLIED_PROBABILITY = 0.8

# Of the rows that do ask, how often through the inline `(?p)` rather than the compile-time flag.
# Both doors reach the same `RE_FLAG_POSIX` bit, and the ported tests only use the inline one.
POSIX_INLINE_PROBABILITY = 0.5

POSIX_REVERSE_PROBABILITY = 0.3
POSIX_IGNORECASE_PROBABILITY = 0.2
POSIX_FULLCASE_PROBABILITY = 0.4
POSIX_MULTILINE_PROBABILITY = 0.2
POSIX_NAMED_GROUP_PROBABILITY = 0.2

# Quantifiers hung on a piece. The lazy ones are here because `(?p)a*(.*?)` - upstream's own Hg
# issue 180 - is the case where POSIX and laziness pull opposite ways: the group wants to match
# nothing and the overall match wants to be as long as possible.
POSIX_QUANTIFIERS = ("", "", "", "?", "*", "+", "{1,2}", "{1,3}", "*?", "+?", "??")

# The shapes one piece can take, and how often. `chain` and `captured` carry the slice's own
# subject - an alternation whose first alternative is a prefix of a later one - so they are drawn
# most; `nested` is what makes a chain appear inside another one.
POSIX_PIECE_KINDS = ("chain", "captured", "optional", "tail", "nested")
POSIX_PIECE_WEIGHTS = (5, 4, 3, 2, 2)

MAX_POSIX_PIECE_DEPTH = 2

# Affixes, so a row is not always unanchored. `\b` is in the list because a boundary either side of
# a chain changes which alternative can win rather than merely which position is tried.
POSIX_AFFIXES = (
    ("", ""),
    ("", ""),
    ("^", ""),
    ("", "$"),
    ("^", "$"),
    (r"\b", ""),
    ("", r"\b"),
    (r"\A", r"\Z"),
)


def _posix_run(rng: random.Random, subject: str) -> str:
    """A short run of characters drawn from the subject, two or three long where it can be.

    Two is the floor because a one-character run makes a chain with a single alternative, which is
    not an alternation at all and cannot tell POSIX from leftmost-first. Measured over 600 rows of
    seed 7, against the rest of this generator as it stands: allowing runs of one left 49 of the 463
    rows that ask for POSIX actually depending on the flag, and this floor gives 57 of 488.
    """
    plain = [c for c in subject if c not in "\r\n"]
    if not plain:
        return "ab"

    # The start is held back from the end so that two characters are still available; a subject of
    # one character is the only case that can still give a run of one.
    start = rng.randrange(max(0, len(plain) - 2) + 1)
    end = min(len(plain), start + rng.choice((2, 2, 3)))
    return "".join(plain[start:end])


def _posix_group(rng: random.Random, body: str, namer) -> str:
    """Wraps a body in a capture group, named some of the time."""
    if rng.random() < POSIX_NAMED_GROUP_PROBABILITY:
        return f"(?<{namer()}>{body})"

    return f"({body})"


def _posix_chain(rng: random.Random, run: str) -> str:
    """``a|ab|abc``: every prefix of the run, as alternatives."""
    parts = [re.escape(run[:n]) for n in range(1, len(run) + 1)]
    if rng.random() < POSIX_SHUFFLE_PROBABILITY:
        rng.shuffle(parts)

    return "|".join(parts)


def _posix_captured_chain(rng: random.Random, run: str, namer) -> str:
    """``(a)|(ab)``: the same chain with each alternative in its own group.

    This is the shape that catches a restore which puts the overall span back but leaves the
    captures of the first, shorter match behind: the short branch's group must end up unset.
    """
    parts = [_posix_group(rng, re.escape(run[:n]), namer) for n in range(1, len(run) + 1)]
    if rng.random() < POSIX_SHUFFLE_PROBABILITY:
        rng.shuffle(parts)

    return "|".join(parts)


def _posix_optional(rng: random.Random, run: str, namer) -> str:
    """``one(self)?(selfsufficient)?``: optional suffixes that overlap.

    Upstream's own test #162, generalised. Leftmost-first takes the first optional group and then
    cannot take the second; POSIX keeps looking and finds the pairing that spans more text.
    """
    head = re.escape(run[:1])
    rest = run[1:] or run
    tails = [re.escape(rest[:n]) for n in range(1, len(rest) + 1)]
    return head + "".join(_posix_group(rng, t, namer) + "?" for t in tails)


def _posix_tail(rng: random.Random, run: str, namer) -> str:
    """``a*(.*?)`` and ``a*(.*)``: upstream's Hg issue 180, both ways round."""
    head = re.escape(run[:1])
    inner = ".*?" if rng.random() < 0.5 else ".*"
    return head + "*" + _posix_group(rng, inner, namer)


def _posix_piece(rng: random.Random, subject: str, namer, depth: int = 0) -> str:
    """One piece of a `posix` pattern, quantified only where that is safe.

    **A quantifier goes on a piece only if the piece holds none already**, which is what keeps this
    generator out of catastrophic backtracking. `chain` and `captured` are alternations of plain
    literals and take one; `optional`, `tail` and `nested` already carry `?`, `*` or a quantified
    child and take none.

    That rule was written against a measurement rather than a principle. Without it, seed 31 row 1407
    of 2000 was

        (?:(?:(?:e*(.*?))+|(?:X|Xe)+)*?|((e)|(?<g0>ea)|(ea e))??){1,3}(?:a|ae){1,3}

    - three quantifiers deep over a five-character subject - and POSIX, which explores every path
    instead of stopping at the first, turned a 15ms row into 17.9s in a Debug build and red the wave
    on the 10s row timeout. It was never a divergence: the port and upstream both answer four
    matches, in 1.1s and 0.69s respectively in their optimised builds. An exponential pattern is a
    property of the pattern, so a generator that emits one is measuring the build configuration
    rather than the port.
    """
    kind = rng.choices(POSIX_PIECE_KINDS, weights=POSIX_PIECE_WEIGHTS)[0]
    if kind == "nested" and depth >= MAX_POSIX_PIECE_DEPTH:
        kind = "chain"

    run = _posix_run(rng, subject)

    if kind == "chain":
        body = _posix_chain(rng, run)
    elif kind == "captured":
        body = _posix_captured_chain(rng, run, namer)
    elif kind == "optional":
        body = _posix_optional(rng, run, namer)
    elif kind == "tail":
        body = _posix_tail(rng, run, namer)
    else:
        left = _posix_piece(rng, subject, namer, depth + 1)
        right = _posix_piece(rng, subject, namer, depth + 1)
        body = f"{left}|{right}"

    quantifier = rng.choice(POSIX_QUANTIFIERS) if kind in ("chain", "captured") else ""

    # An alternation has to be bracketed before anything can be hung on it, and a quantifier has to
    # be hung on a bracket rather than on a bare chain - otherwise `a|ab+` quantifies the 'b'.
    if quantifier or "|" in body:
        body = _posix_group(rng, body, namer) if rng.random() < 0.5 else f"(?:{body})"

    return body + quantifier


def _posix_pattern(rng: random.Random, subject: str) -> tuple[str, int, list[str]]:
    """One pattern, with the group inventory a substitution template needs."""
    names: list[str] = []

    def namer() -> str:
        name = f"g{len(names)}"
        names.append(name)
        return name

    pieces = [_posix_piece(rng, subject, namer) for _ in range(rng.randrange(1, 3))]
    prefix, suffix = rng.choice(POSIX_AFFIXES)
    pattern = prefix + "".join(pieces) + suffix

    # Counted the same way `_recursion_pattern` counts: an unnamed group is a '(' not followed by
    # '?', a named one is a '(?<name>'.
    groups = len(re.findall(r"\((?!\?)", pattern)) + len(names)
    return pattern, groups, names


def _generate_posix(rng: random.Random, count: int):
    """S32's generator: leftmost-longest matching, over alternations that make it visible.

    Every shape here exists so that leftmost-first and leftmost-longest can disagree: a chain whose
    alternatives are prefixes of one another, overlapping optional suffixes, and a lazy group under
    a greedy overall match. A generator that emitted ordinary patterns would record a wave in which
    POSIX changed nothing and no arm of `check_posix_match` was ever reached twice.

    One row in five is recorded WITHOUT POSIX, through the same grammar, so the wave also says what
    these patterns do through the ordinary door - the flag is then the only difference between the
    two populations.

    Measured by `python tools/record-oracle.py --generator posix --count 600 --seed 7`, after the
    last change to this generator: 141 rows answer with a single match, 84 with no match, 150 with a
    match list, 121 with a substitution and 75 with a split, and 29 are rejected by upstream. Not one
    of the 29 is a rejected pattern: 22 are the `{N[2]}` capture subscript `_subf_template` emits on
    purpose, 5 an out-of-range `\\g<N>` and 2 an out-of-range positional field. 488 rows ask for
    POSIX, 267 through the inline `(?p)` and 221 through the compile-time flag, and 112 do not ask at
    all. 191 rows are reversed, 197 hold a named group, 120 carry IGNORECASE, 43 FULLCASE, 119
    MULTILINE, 121 have a newline in the subject and 139 an astral one. Every one of the eight
    operations is recorded exactly 75 times, because the operation is cycled by row index rather than
    drawn.

    **What the flag is actually worth here: POSIX changes upstream's own answer on 57 of the 488 rows
    that ask for it**, and of the 176 single-match rows among those, 14 move a group span and 5 unset
    a group that leftmost-first had set. The other 431 asked rows still drive every line of
    `save_best_match`, `check_posix_match` and `restore_best_match` - they are the ones that say
    POSIX must *not* change the answer when nothing longer exists, which is what a restore that
    dropped the captures would break.

    Re-take from scratch after any widening: adding one entry to any table above shifts the whole RNG
    stream, so a figure measured before it describes a wave this generator no longer produces.
    """
    for i in range(count):
        alphabet = POSIX_SUBJECT_ALPHABETS[i % len(POSIX_SUBJECT_ALPHABETS)]
        length = rng.randrange(1, MAX_POSIX_SUBJECT_LENGTH + 1)
        subject = "".join(rng.choice(alphabet) for _ in range(length))

        if rng.random() < POSIX_DOUBLED_SUBJECT_PROBABILITY:
            # A prefix of itself glued on the end, so a chain reaches past its first alternative.
            subject += subject[: rng.randrange(1, len(subject) + 1)]

        if rng.random() < POSIX_MULTILINE_PROBABILITY:
            at = rng.randrange(len(subject) + 1)
            subject = subject[:at] + "\n" + subject[at:]

        pattern, groups, names = _posix_pattern(rng, subject)

        flags = 0
        if rng.random() < POSIX_IGNORECASE_PROBABILITY:
            flags |= IGNORECASE
            if rng.random() < POSIX_FULLCASE_PROBABILITY:
                flags |= FULLCASE
        if rng.random() < POSIX_MULTILINE_PROBABILITY:
            flags |= MULTILINE

        if rng.random() < POSIX_APPLIED_PROBABILITY:
            if rng.random() < POSIX_INLINE_PROBABILITY:
                pattern = "(?p)" + pattern
            else:
                flags |= POSIX

        if rng.random() < POSIX_REVERSE_PROBABILITY:
            pattern = "(?r)" + pattern

        operation = ALL_OPERATIONS[i % len(ALL_OPERATIONS)]
        row = {
            "generator": "posix",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": operation,
        }
        if operation in SUB_OPERATIONS:
            row["template"] = (
                _sub_template(rng, groups, names) if operation == "sub" else _subf_template(rng, groups, names)
            )
        if operation in LIMIT_OPERATIONS:
            row["count"] = rng.choice(SUB_COUNTS if operation in SUB_OPERATIONS else ITER_LIMITS)

        yield row


# --------------------------------------------------------------------------------------------
# S38's generator: fuzzy matching over one-character and zero-width items
# --------------------------------------------------------------------------------------------

# The atoms a fuzzy section is built from. Every one is a single node that consumes exactly one
# character - the S16-S20 one-character constructs - because S38 delivers only those: a fuzzy
# STRING, backreference or *_REPEAT_ONE is S39's, and a generator that emits one produces
# `unsupported` rows and tells nobody anything.
#
# NO TWO LITERALS ARE EVER ADJACENT in a generated pattern, and that is the whole reason these are
# drawn one at a time with a separator rule below. `Sequence.pack_characters`
# (upstream/regex/_regex_core.py:3526) packs a run of two or more `Character` items into one STRING
# node, so `ab` inside a fuzzy section is a fuzzy string and not two fuzzy characters.
FUZZY_ATOMS = (
    "a",
    "b",
    "x",
    "0",
    ".",
    "[ab]",
    "[^a]",
    "[a-f]",
    "[^a-f]",
    r"\w",
    r"\W",
    r"\d",
    r"\s",
    r"\p{L}",
    r"\p{Nd}",
    # S52. The `fuzzy` PATTERN held nothing above U+FFFF at all - 0 of 1200 rows - so the question
    # this generator exists to ask had never been asked about a surrogate pair: does ONE edit buy a
    # whole astral character, or does it buy one code unit and leave half a pair behind? A literal
    # and a class, so both the CHARACTER and the SET arms of the fuzzy matcher get one.
    ASTRAL_SYMBOL,
    ASTRAL_DIGIT,
    "[" + ASTRAL_SYMBOL + ASTRAL_LETTER + "]",
)

# Which of those are single literal characters. S38 refused to put two in a row so that no STRING
# node could be built; S39 delivers the fuzzy STRING arms, so the rule is gone and the set is kept
# only to say what an atom's exact subject text is.
FUZZY_LITERAL_ATOMS = frozenset({"a", "b", "x", "0", ASTRAL_SYMBOL, ASTRAL_DIGIT})

# S39's widening, part one: multi-character literals, which `Sequence.pack_characters`
# (upstream/regex/_regex_core.py:3526) packs into a STRING node. These reach `fuzzy_match_string`
# (upstream/src/_regex.c:10431) and, once the whole string has matched, `fuzzy_insert` (:10346) -
# the one place an insertion can be charged next to a string.
FUZZY_STRING_ATOMS = (
    "ab", "ba", "abx", "fo", "oba", "a0", "x0b",
    # S52. A STRING node holding a surrogate pair, which is where a length counted in code units
    # rather than in characters would show up as a miscounted edit rather than as a crash.
    "a" + ASTRAL_SYMBOL, ASTRAL_SYMBOL + "b", ASTRAL_SYMBOL + ASTRAL_DIGIT,
)

# S39's widening, part two: repeat bodies. These do NOT reach the fuzzy *_REPEAT_ONE loops - a
# repeat inside a fuzzy section is always a GREEDY_REPEAT, never a GREEDY_REPEAT_ONE, because
# `sequence_matches_one` (:24056) refuses a fuzzy body. They are here because a repeat next to a
# fuzzy string is where the backtracking into `retry_fuzzy_match_string` actually happens.
FUZZY_REPEAT_ATOMS = {
    "a+": ("a", 1),
    "a*?": ("a", 0),
    "b+?": ("b", 1),
    "[ab]+": ("ab", 1),
    "[ab]*?": ("ab", 0),
    r"\d+": ("0123456789", 1),
}

# S39's widening, part three: literals whose full case folding is longer than one character, which
# is the only way STRING_FLD's and REF_GROUP_FLD's fuzzy arms are reached. Drawn only under `(?fi)`,
# because simple case folding does not make 'ss' match U+00DF.
#
# U+0130 is deliberately absent: it never reaches its full fold upstream either (ledger entry 7,
# Phase 6's fix list), so drawing it would fill the wave with rows about a known inherited bug.
FUZZY_FOLD_ATOMS = ("straße", "ﬆx", "ßa", "maße", "ﬀo")

# How a section's case sensitivity is chosen. `plain` is STRING and REF_GROUP, `ign` is their _IGN
# forms, `fold` their _FLD forms.
FUZZY_CASE_MODES = ("plain", "ign", "fold")
FUZZY_CASE_MODE_WEIGHTS = (5, 2, 3)

# How often a section refers back to a capture group outside it, which is the only way the
# REF_GROUP* fuzzy arms are reached at all.
#
# The fold weight above and this share one reason. `REF_GROUP_FLD` needs both at once, and at the
# first weights tried - (6, 2, 2) and 0.18 - control S39-E, which moves the wrong side's folding on
# a deletion, fired on 2 rows of 600 at one seed and 1 at another. A rule a generator reaches once
# in six hundred is not measured, it is noticed.
FUZZY_BACKREF_PROBABILITY = 0.25
FUZZY_BACKREF_ATOM = r"(?:\1)"

# What the referenced group holds when the row is in `fold` mode, drawn instead of a plain string
# atom about half the time. S40 added these, and the reason is a control that measured zero.
#
# `fuzzy_ext_match_group_fld` (upstream/src/_regex.c:10033) asks the character INSIDE the subject
# character's folding, at `folded_pos`. That index is only ever non-zero when a subject character
# folds to more than one - and the two sides have to run at different speeds for the comparison to
# stop partway through a folding at all. With the group text drawn from FUZZY_STRING_ATOMS, which is
# ASCII, both sides folded one-to-one, `folded_pos` was always 0, and S40's control E - which
# replaces `folded_pos` with a literal 0 - found nothing at either of two seeds over 2000 rows.
# With these, it fires. A control that a generator cannot make fail is not evidence of anything.
FUZZY_BACKREF_FOLD_ATOMS = ("ßa", "ﬆx", "aß", "ﬀo", "ßß")
FUZZY_BACKREF_FOLD_PROBABILITY = 0.5

# S85's 'fuzzy-overhang' generator: a full-folded reference whose group ends HALF-WAY THROUGH a
# subject character's folding, the one shape S84's and S85's repairs decide. The group is plain
# letters and the reference's share of the subject swaps its last letters (its first, reversed) for
# a character whose folding starts (ends) with them and runs on: group 's' against 'ß', which folds
# to ss, or 'ff' against 'ﬃ'. The default wave reaches S84's leftovers defect on no row, and its
# retry defect on at most four rows of 6680 per seed (S84's closing notes). 'ab' has no such
# character, so its rows keep the plain share and stand as the comparison.
FUZZY_OVERHANG_GROUPS = ("s", "as", "sa", "f", "ff", "fa", "af", "fi", "st", "t", "ab")
FUZZY_OVERHANG_CHARACTERS = "ßẞﬀﬁﬂﬃﬄﬅﬆ"

# The zero-width assertions a fuzzy section may contain. They matter more here than anywhere else:
# a zero-width item passes a step of 0 to `fuzzy_match_item` (upstream/src/_regex.c:10185), which
# rules out deletion and substitution outright, so an insertion is the only error that can get past
# a failing assertion - and it moves the position rather than the node.
FUZZY_ZERO_WIDTH = ("^", "$", r"\b", r"\B", r"\A", r"\Z")

# A zero-width assertion in FRONT of the whole pattern, which is a different shape from the one
# above and reaches a rule nothing else here can see. It is what the 'fuzzy-anchored' generator
# adds, and it has a generator of its own rather than a share of 'fuzzy' for a measured reason:
# drawing it inside `_generate_fuzzy` reshuffles that generator's whole row stream, and the first
# 2,000-row trial of it did exactly that and reported a divergence in an unrelated family - a
# `(?b)(?fi)` backreference row with no assertion anywhere in it (seed 7, row 331, 2026-09-21).
# A generator has its own `random.Random(f"{seed}:{name}")`, so a new one adds rows without moving
# anybody else's.
#
# WHY THE SHAPE NEEDS REACHING. S57c fixed upstream issues 563 and 564 by permitting a fuzzy
# insertion at the search anchor where a LEADING assertion holds at the anchor and fails one
# character on (`docs/DIVERGENCES.md`). Its negative control - removing the one-step-on half of
# that test - reddens four rows of the ported suite and, before this generator, not one row of a
# 12,000-row wave: the generators drew the assertion and the fuzzy section independently and
# never together in front.
#
# WHAT IT FOUND FIRST, and it was not S57c's rule: seed 1234567, row 1982 of 2000,
# `(?b)(?r)\m(?:.fo){e<=2}` over 'x fx', where both engines answer the same span at the same error
# counts and only the recorded insertion position differs. S57e judged it ledger entry 12 - the
# doubled term in upstream's `END_FUZZY` backtrack arm, which this port dropped in S46 - and pinned
# it as row 24 of `bestmatch-loses-a-candidate`. The generator went onto the default list with that
# judgement, and the 6000-row gate drew three more rows of the same entry at once - rows 25 to 27,
# two of them upstream keeping a three-error match where this port fits the same span in two. A
# fourth, row 28, came out of the slice's own negative control at seed 8675309.
#
# The assertions are the ones that can FAIL one step along from an anchor: `\b` and `\m` at the
# start of a subject whose first two characters are both word characters, `\M` and `$` at the end
# under `(?r)`, where the anchor is the subject's end, and `\B` for the reverse of all of them.
# `^` and `\A` are deliberately absent: upstream turns a start-anchored search into an anchored
# match and escapes the rule entirely, so those rows would be about the unported search prefilters
# instead of about this.
FUZZY_ANCHOR_GUARDS = (r"\b", r"\m", r"\M", "$", r"\B")

# The constraints. Every shape upstream's `build_FUZZY` reads: a bare budget, a per-kind budget, a
# mixture, a cost equation, and a minimum. `(?e)` and `(?b)` are NOT here - they are whole-pattern
# flags rather than constraints, and are prepended at the end of `_generate_fuzzy` (S41 and S42).
FUZZY_CONSTRAINTS = (
    "{e<=1}",
    "{e<=2}",
    "{e<=3}",
    "{s<=1}",
    "{i<=1}",
    "{d<=1}",
    "{s<=2}",
    "{i<=2}",
    "{d<=2}",
    "{e<=2,i<=1}",
    "{e<=2,s<=1}",
    "{s<=1,i<=1,d<=1}",
    "{1i+2d+1s<=4}",
    "{2i+1d+1s<=2}",
    "{1i+1d+1s<=1}",
    "{e<=3,1i+1d+2s<=3}",
    "{1<=e<=2}",
    "{e}",
)

# The same list without the unbounded budget, used by any section holding a MULTI-CHARACTER item.
# `{e}` puts no ceiling on the number of errors, so the search space is bounded only by the subject
# - which is fine over S38's one-character atoms, where it was measured green at 6000 rows, and is
# not fine once a string, a fold, a repeat or a backreference is in the section. Two measured on
# regex 2026.7.19, 2026-09-13, both raising MemoryError and so killing the whole wave rather than
# producing a comparable row:
#   regex.sub(r'(abx)(?:\W[a-f](?:[ab]+\1){e}){s<=2}', '<>', 'abx.dabx')
#   regex.split(r'(?fi)(?:straße\d\A){e}', 'sTrasßE0')
# That is the same family as upstream issues 551 and 554 (resource blowups, already on Phase 6's
# triage list), so the generator stays off the shape rather than the recorder learning to survive it.
FUZZY_BOUNDED_CONSTRAINTS = tuple(c for c in FUZZY_CONSTRAINTS if c != "{e}")

# Tests that SPLIT A FOLDING, and that is the whole of why they are a list of their own.
#
# `fuzzy_ext_match_group_fld` asks `folded_char_at(text_pos, folded_pos)` - the character INSIDE the
# subject character's folding. A test can only tell that apart from `folded_char_at(text_pos, 0)` if
# it answers differently for two characters of one folding. The foldings this generator produces are
# 'ß' -> "ss", 'ﬀ' -> "ff", 'ﬆ' -> "st" and 'ﬁ' -> "fi", so "ss" and "ff" cannot be split at all and
# the ones that can need a boundary between 's' and 't', or between 'f' and 'i'.
#
# Measured on 2026-09-13, and the numbers are why this list exists rather than two more entries in
# FUZZY_TESTS. S40's control E replaces `folded_pos` with a literal 0 and read 0 divergences of 2000
# at both seeds three times running: with the group side ASCII, with the group side folding, and
# with these tests merely added to the pool. The wave held 81 rows folding on both sides and exactly
# ONE of them drew a splitting test, because a uniform draw gives such a test 2 chances in 17.
# Biasing the draw for that one shape - a fold-mode row with a backreference - is what gave the
# control teeth.
FUZZY_SPLIT_TESTS = ("[a-s]", "[a-f]", "s", "f", "[^t]", "[^i]")

# How often a fold-mode row with a backreference draws from FUZZY_SPLIT_TESTS instead. Only that
# shape: biasing every row would cost the CHARACTER, RANGE, PROPERTY and SET arms the spread they
# have, to buy coverage those arms do not need.
FUZZY_SPLIT_TEST_PROBABILITY = 0.6

# The `{...:test}` constraint, S40: which subject characters an error is allowed to touch. Every
# constraint shape above accepts one - measured, all 18 x these 15 parse - so the test is appended
# to whichever shape the row drew rather than being a shape of its own.
#
# The list is one entry per arm of upstream's `fuzzy_ext_match` switch (upstream/src/_regex.c:9938),
# plus the two shapes that have no arm:
#   CHARACTER  'x', '0'            and negated, '[^x]', '[^0]' - a one-character class optimises to
#                                  CHARACTER with node->match FALSE rather than to a set
#   RANGE      '[a-z]', '[0-9]'
#   PROPERTY   '\d', '\w', '\s', '\S'
#   SET_UNION  '[0-9x]', '[abx]', '[^abx]', '[a-cx-z]' - disjoint pieces, so they cannot collapse
#   (no arm)   '.', which compiles to ANY and therefore constrains nothing
# SET_DIFF, SET_INTER and SET_SYM_DIFF are NOT here: the set-operator syntax needs `(?V1)`, which
# this generator does not emit, so `{e<=1:[[a-z]--[aeiou]]}` is `error: expected }` in a V0 pattern.
# Gaps/Engine/FuzzyTestConstraintTests.cs covers those three directly instead.
# `(?i)x` is not here either: the grammar takes only a character set, so it is a parse error.
#
# The entries are chosen against FUZZY_SUBJECT_ALPHABETS ('abx', 'ab0 x', 'abf<astral>'), so a test
# both permits and refuses real edits rather than being vacuous either way.
FUZZY_TESTS = (
    "x",
    "0",
    "[^x]",
    "[^0]",
    "[a-z]",
    "[0-9]",
    r"\d",
    r"\w",
    r"\s",
    r"\S",
    "[0-9x]",
    "[abx]",
    "[^abx]",
    "[a-cx-z]",
    ".",
) + FUZZY_SPLIT_TESTS

# How often a constraint carries a test. A third: high enough that a 2000-row wave holds several
# hundred FUZZY_EXT rows in every body shape, low enough that the plain FUZZY spine S38 and S39
# ported keeps the coverage it had.
FUZZY_TEST_PROBABILITY = 0.35

# The alphabet a subject is drawn from. The astral band is here for the same reason it is in the
# group generator: a change position reported in codepoints rather than UTF-16 code units has to
# show up as a divergence from the first wave.
#
# S42 TRIED AND WITHDREW AN ALL-ASTRAL FOURTH BAND, and what it found is worth more than the band
# would have been. The mixed band is three BMP characters to two astral ones, so a position the
# engine steps by one code unit instead of one character only lands inside a surrogate pair some of
# the time: S42's control C - do_best_fuzzy_match's second pass stepping '+= step' - fired on 1 row
# of 2000 at seed 7 and on none at 4242 or 20260913. A fourth band of nothing but astral characters
# lifted that to 1, 0 and 2, and to 3 of 5 seeds across 7, 4242, 20260913, 777 and 31; seed 4242
# stayed green even at 6000 rows, so the limit is the shape rather than the rate - the stepped
# position must land inside a pair AND an anchored match must succeed there and beat the candidate.
#
# It was withdrawn because it turned the wave red on a row that has NOTHING TO DO WITH S42:
# '(?e)(?:\d\wba){1i+2d+1s<=4}', where upstream keeps 2 deletions costing 4 and this port keeps 3
# substitutions costing 3, at a DIFFERENT SPAN. That is the deliberate cost-ranking divergence
# (DECISIONS 2026-09-12) in the shape 'enhancematch-ranks-by-cost' reports rather than classifies,
# and it needs no astral character at all - 'XX8QbaY' does it. Deciding how the divergence list
# should hold that family is a ranking question and belongs beside the rest of them; changing which
# subject a row draws would only move the day it fires. See STATE.md.
FUZZY_SUBJECT_ALPHABETS = ("abx", "ab0 x", "abf" + ASTRAL_ALPHABET)

# How many atoms a fuzzy section holds. Three is the sweet spot: one atom cannot show an error in
# the middle, and a long section against a short subject makes every row fail for the same
# uninteresting reason.
FUZZY_SECTION_ATOMS = (1, 2, 3, 4)
FUZZY_SECTION_ATOM_WEIGHTS = (2, 6, 8, 4)

# How many edits a subject is mutated by, starting from a string the section matches exactly. Zero
# is in the list on purpose: an exact subject is the row that says the engine does not spend an
# error it did not need.
FUZZY_EDIT_COUNTS = (0, 1, 2, 3)
FUZZY_EDIT_COUNT_WEIGHTS = (3, 6, 4, 2)

# How often a section holds a second fuzzy section inside it. See the call site: without nesting,
# the FUZZY/END_FUZZY stack traffic is unobservable, because the outer counts are always zero.
FUZZY_NESTING_PROBABILITY = 0.2

# How often a row carries `(?e)`, which routes it through do_enhanced_fuzzy_match's improvement loop
# instead of do_simple_fuzzy_match. A third, for the reason FUZZY_TEST_PROBABILITY is a third: high
# enough that every body shape draws it several hundred times in a 2000-row wave, low enough that
# the plain spine S38 and S39 ported keeps the coverage it had. S42 adds `(?b)` beside it.
FUZZY_ENHANCE_PROBABILITY = 0.35

# How often a row carries `(?b)`, which routes it through do_best_fuzzy_match instead - the two-pass
# search for the match with the fewest errors rather than the first that fits. Drawn INDEPENDENTLY of
# `(?e)`, so a wave holds all four combinations and about an eighth of it is `(?b)(?e)` together,
# which is a third mode again: do_best_fuzzy_match wins the dispatch (:18107) and the improvement
# loop never runs, so a row carrying both is the one place a wrong dispatch order is visible.
FUZZY_BESTMATCH_PROBABILITY = 0.35

# Matches one cost equation inside a constraint: the `<n>i+<n>d+<n>s` term list, in any order.
_FUZZY_COST_TERM = re.compile(r"(\d+)([ids])")
_FUZZY_EQUATION = re.compile(r"\{[^{}]*?((?:\d+[ids]\+)*\d+[ids])\s*<=?\s*\d+")


def _has_weighted_cost(pattern: str) -> bool:
    """Whether any cost equation in `pattern` prices the three error kinds differently.

    THIS IS WHAT DECIDES WHETHER A ROW MAY CARRY `(?e)` OR `(?b)`, and the reason is that a
    differential oracle cannot judge a comparison the two engines are DEFINED to answer differently.
    This port ranks fuzzy matches by cost and upstream ranks them by error count (owner decision,
    DECISIONS 2026-09-12, upstream's open issue 470). With unit coefficients the two rules are the
    same rule, so such a row is real ground truth and is drawn as before. With weighted ones every
    row where the rules can disagree IS a divergence by construction, and reporting it teaches
    nothing while turning the wave permanently red - measured 2026-09-13, 4 rows of 2000 at each of
    seeds 7, 4242 and 20260913, every one of the twelve a weighted equation under `(?b)` and every
    one of them this port answering more cheaply than upstream under the pattern's own equation.

    Classifying them instead was considered and cannot be made strict. `sub`, `subf` and `split`
    rows record a STRING and no per-match counts (see the row shape written below), so for those
    there is no cost to compare and the only available predicate is "the spans differ" - which
    DECISIONS 2026-09-13 refuses, because a different span is also exactly what a real engine defect
    looks like and the list must never hide one.

    What replaces the coverage: `tools/probes/enhancematch-cost-rows.py`, which draws this family on
    purpose and is read for the invariant the port actually guarantees - its answer is never DEARER
    than upstream's - plus the pinned rows in `Gaps/Engine/FuzzyBestMatchTests.cs`,
    `Gaps/Engine/FuzzyEnhanceMatchTests.cs` and `Ported/Fuzzy/FuzzyBestMatchTests.cs`. Note that the
    new code is still exercised here on every unit-cost row: `DoBestFuzzyMatch`'s cost walk runs for
    those too and has to agree with upstream's single walk, so it is the OUTCOME that is not drawn,
    not the code path.
    """
    for match in _FUZZY_EQUATION.finditer(pattern):
        costs = {kind: int(value) for value, kind in _FUZZY_COST_TERM.findall(match.group(1))}

        # A TERM THE EQUATION OMITS COSTS NOTHING, not one, so `2d+1s<4` prices insertions at zero
        # and is weighted. Upstream's own `test_fuzzy#44` is that shape and this port answers it
        # differently for exactly that reason - see Ported/Fuzzy/FuzzyBestMatchTests.cs.
        if len({costs.get(kind, 0) for kind in "ids"}) > 1:
            return True

    return False


def _fuzzy_constraint(rng: random.Random, constraints: tuple[str, ...], split: bool) -> str:
    """One constraint, sometimes carrying a `{...:test}` - S40's FUZZY_EXT rather than FUZZY.

    `split` says the row folds on both sides, so a test that splits a folding is worth more than a
    uniform draw from the whole pool. See FUZZY_SPLIT_TESTS for the measurement behind that.
    """
    constraint = rng.choice(constraints)

    if rng.random() >= FUZZY_TEST_PROBABILITY:
        return constraint

    if split and rng.random() < FUZZY_SPLIT_TEST_PROBABILITY:
        return constraint[:-1] + ":" + rng.choice(FUZZY_SPLIT_TESTS) + "}"

    return constraint[:-1] + ":" + rng.choice(FUZZY_TESTS) + "}"


def _fuzzy_atom(rng: random.Random, mode: str, allow_fold: bool) -> str:
    """One atom. S39 no longer refuses two literals in a row - see FUZZY_LITERAL_ATOMS on why."""
    pool = FUZZY_ATOMS + FUZZY_STRING_ATOMS + tuple(FUZZY_REPEAT_ATOMS)
    if mode == "fold" and allow_fold:
        pool += FUZZY_FOLD_ATOMS
    return rng.choice(pool)


def _fuzzy_atom_text(rng: random.Random, atom: str, alphabet: str) -> str:
    """The text one atom matches exactly, for the atoms S39 added."""
    if atom in FUZZY_REPEAT_ATOMS:
        characters, minimum = FUZZY_REPEAT_ATOMS[atom]
        return "".join(rng.choice(characters) for _ in range(rng.randint(minimum, minimum + 2)))
    if atom in FUZZY_FOLD_ATOMS:
        # The subject side carries the EXPANDED folding, which is what makes one subject character
        # answer for several pattern characters (or the other way round for a ligature).
        return atom.casefold()
    if atom in FUZZY_STRING_ATOMS:
        return atom
    return _fuzzy_one_char_text(rng, atom, alphabet)


def _fuzzy_overhang(rng: random.Random, group: str, reverse: bool) -> str:
    """The group's text with its trailing letters (leading, reversed) folded into one character.

    The character's folding starts with those letters and runs on, so a reference to the group
    matches part of it and runs out. The group is plain letters, so its folding is itself. Where no
    character fits, the text comes back unchanged.
    """
    options = []
    for character in FUZZY_OVERHANG_CHARACTERS:
        folding = character.casefold()
        for k in range(1, min(len(group), len(folding) - 1) + 1):
            if reverse and folding.endswith(group[:k]):
                options.append(character + group[k:])
            elif not reverse and folding.startswith(group[-k:]):
                options.append(group[:-k] + character)
    return rng.choice(options) if options else group


def _fuzzy_recase(rng: random.Random, text: str) -> str:
    """The same text with some characters upper-cased, for a section compiled under (?i) or (?fi)."""
    return "".join(c.upper() if rng.random() < 0.5 else c for c in text)


def _fuzzy_exact_subject(rng: random.Random, atoms: list[str], alphabet: str, mode: str) -> str:
    """A subject the section matches exactly, so a mutation of it needs a known number of errors."""
    text = "".join(_fuzzy_atom_text(rng, atom, alphabet) for atom in atoms)
    return _fuzzy_recase(rng, text) if mode != "plain" else text


def _fuzzy_one_char_text(rng: random.Random, atom: str, alphabet: str) -> str:
    """S38's atoms: the one character each of them matches."""
    if atom in FUZZY_LITERAL_ATOMS:
        return atom
    if atom == ".":
        return rng.choice("abx")
    if atom == "[ab]":
        return rng.choice("ab")
    if atom == "[^a]":
        return rng.choice("bx0")
    if atom == "[a-f]":
        return rng.choice("abcdef")
    if atom == "[^a-f]":
        return rng.choice("xyz0")
    if atom in (r"\w", r"\p{L}"):
        return rng.choice("abxQ")
    if atom == r"\W":
        return rng.choice(" -.")
    if atom in (r"\d", r"\p{Nd}"):
        # S52 puts the two SMP digits in the pool: both are `Nd` and both satisfy `\d`, measured
        # 2026-09-15, so a `\d` inside a fuzzy section now sometimes matches two UTF-16 code units.
        return rng.choice("0123456789" + ASTRAL_DIGIT + ASTRAL_DIGIT_2)
    if atom == r"\s":
        return " "
    if atom == "[" + ASTRAL_SYMBOL + ASTRAL_LETTER + "]":
        return rng.choice((ASTRAL_SYMBOL, ASTRAL_LETTER))
    return rng.choice(alphabet)


def _fuzzy_mutate(rng: random.Random, subject: str, edits: int, alphabet: str) -> str:
    """The subject with ``edits`` substitutions, insertions and deletions applied at random."""
    for _ in range(edits):
        kind = rng.choice(("sub", "ins", "del"))
        if not subject:
            kind = "ins"
        if kind == "ins":
            # One insertion in three goes on the END, because that is the only place `fuzzy_insert`
            # (upstream/src/_regex.c:10346) can be charged: it runs after a string has matched in
            # full, so a spare character anywhere earlier is a substitution or an inner insertion
            # instead. At a uniform position control S39-A fired on 3 rows of 600 and then 1.
            position = len(subject) if rng.random() < 0.33 else rng.randrange(len(subject) + 1)
        else:
            position = rng.randrange(len(subject))
        if kind == "sub":
            subject = subject[:position] + rng.choice(alphabet) + subject[position + 1 :]
        elif kind == "ins":
            subject = subject[:position] + rng.choice(alphabet) + subject[position:]
        else:
            subject = subject[:position] + subject[position + 1 :]
    return subject


def _generate_fuzzy(rng: random.Random, count: int, guarded: bool = False, overhang: bool = False):
    """One fuzzy section, S38's one-character and zero-width items plus S39's multi-character ones.

    The subject is built to match the section exactly and then mutated by zero to three edits, so a
    matching row is the normal case rather than a lucky one - a wave that is nearly all `nomatch`
    exercises the failure path and almost nothing else. The mutation is random rather than budgeted,
    so plenty of rows overshoot their constraint and check the refusal too.

    S39 widens it four ways, one per arm family the slice ports: multi-character literals (STRING),
    `(?i)` and `(?fi)` (STRING_IGN and STRING_FLD), a backreference (the REF_GROUP family), and
    repeat bodies next to a string, which is where a retry into `retry_fuzzy_match_string` comes
    from.

    S40 widens it once more: about a third of the constraints - inner section as well as outer -
    carry a `{...:test}`, so the row compiles to FUZZY_EXT and the error has to pass
    `fuzzy_ext_match` before it is spent. See FUZZY_TESTS for which tests and why those.

    S41 widens it once more: about a third of the rows carry `(?e)`, so the row goes through
    `do_enhanced_fuzzy_match` (upstream/src/_regex.c:17862) - the improvement loop - rather than
    `do_simple_fuzzy_match`. The draw is independent of everything else here, so every body shape
    the generator makes gets it.

    S42 adds `(?b)` on the same terms, drawn independently again, so a wave holds all four
    combinations of the two flags and every body shape draws each of them. See
    FUZZY_BESTMATCH_PROBABILITY on why both together is worth its own eighth of the wave.

    S57c reuses the whole of it for a second generator, 'fuzzy-anchored', by passing `guarded`:
    every row then opens with a zero-width assertion in front of everything else, which is the one
    shape the S57c anchor-pin rule decides. See FUZZY_ANCHOR_GUARDS for why it is a generator of
    its own rather than a probability inside this one.

    S85 reuses it for a third, 'fuzzy-overhang', by passing `overhang`: every row is `(?fi)` with a
    backreference as the section's last atom (first, reversed), and the reference's share of the
    subject ends inside a folding. See FUZZY_OVERHANG_GROUPS. Each change short-circuits a draw
    only when `overhang` is set, so 'fuzzy' and 'fuzzy-anchored' rows are unchanged at every seed.
    """
    for i in range(count):
        # A TRAP FOR WHOEVER ADDS A FOURTH BAND, left as a comment because S42 walked into it and
        # backed out. The operation below is `ALL_OPERATIONS[i % 8]`, so an alphabet picked by
        # `i % len(FUZZY_SUBJECT_ALPHABETS)` pairs with the operation only as well as the two lengths
        # are coprime. Three bands and eight operations are coprime, so every pair occurs - by luck,
        # not design. A fourth band makes gcd(4, 8) = 4 and LOCKS each operation to exactly one
        # alphabet: `search`, `match`, `subf` and `finditer` then draw no astral subject at all, over
        # any number of rows (measured 2026-09-13, seed 7, 2000 rows; found by S42's blind review).
        # The fix is one character - index by `i // len(ALL_OPERATIONS)` instead, which makes the two
        # round-robins exhaustive rather than accidental - but it reshuffles every fuzzy row, so it
        # belongs with a band change and not on its own. See STATE.md: applying it uncovered a real
        # ENHANCEMATCH defect that S42 was not the slice to fix.
        alphabet = FUZZY_SUBJECT_ALPHABETS[i % len(FUZZY_SUBJECT_ALPHABETS)]
        mode = "fold" if overhang else rng.choices(FUZZY_CASE_MODES, weights=FUZZY_CASE_MODE_WEIGHTS)[0]

        atom_count = rng.choices(FUZZY_SECTION_ATOMS, weights=FUZZY_SECTION_ATOM_WEIGHTS)[0]

        # AT MOST ONE expanding fold per section, and that cap is a finding rather than caution.
        # Two of them in one literal run is a COMPILE-time divergence this port already owns and
        # S35 already decided: `Sequence._fix_full_casefold` (upstream/regex/_regex_core.py:3637)
        # finds its chunks in the folded text and slices the unfolded run with those offsets, so the
        # second expanding character loses its FULLIGNORECASE flag - `(?fi)ßaß` stops
        # matching 'ssass' upstream, and this port matches it. Drawing the shape here would fill the
        # wave with rows about a compiler difference both sides' own tests already pin, and would
        # hide any real defect in the matcher arms this slice ports. Measured 2026-09-13: without
        # the cap, 22 divergences over three 2000-row seeds and every one of them this family.
        atoms = []
        for _ in range(atom_count):
            atom = _fuzzy_atom(rng, mode, allow_fold=not any(a in FUZZY_FOLD_ATOMS for a in atoms))
            atoms.append(atom)

        # A backreference to a group OUTSIDE the section: the capture has to be made before the
        # section can refer to it, so the group goes in front of the section - or behind it under
        # `(?r)`, where matching runs the other way. Its text is one of the string atoms, so what
        # the reference has to match is a multi-character run and the REF_GROUP arms get real work.
        reverse = rng.random() < 0.2
        backref_text = ""
        if overhang:
            backref_text = rng.choice(FUZZY_OVERHANG_GROUPS)
            atoms[0 if reverse else len(atoms) - 1] = FUZZY_BACKREF_ATOM
        elif rng.random() < FUZZY_BACKREF_PROBABILITY:
            if mode == "fold" and rng.random() < FUZZY_BACKREF_FOLD_PROBABILITY:
                backref_text = rng.choice(FUZZY_BACKREF_FOLD_ATOMS)
            else:
                backref_text = rng.choice(FUZZY_STRING_ATOMS)
            # Wrapped, because a bare `\1` next to a literal digit parses as `\10` - group ten,
            # which does not exist, so the row is an `error` on both sides and tests nothing. Nine
            # rows of a 2000-row wave were exactly that before the wrapper went in.
            atoms[rng.randrange(len(atoms))] = FUZZY_BACKREF_ATOM

        # `\1` stands for the captured text, so that is what its share of an exact subject is.
        if overhang:
            # The share is not recased: ß upper-cases to SS, which would fold one letter at a time.
            share = _fuzzy_overhang(rng, backref_text, reverse)
            exact = "".join(
                share if atom == FUZZY_BACKREF_ATOM else _fuzzy_recase(rng, _fuzzy_atom_text(rng, atom, alphabet))
                for atom in atoms
            )
        else:
            exact = _fuzzy_exact_subject(
                rng,
                [backref_text if atom == FUZZY_BACKREF_ATOM else atom for atom in atoms],
                alphabet,
                mode,
            )

        # An edit under (?fi) has to be able to land a multi-character fold as well as take one
        # away, so the characters it draws from include the ones whose folding is longer than one.
        mutated = _fuzzy_mutate(
            rng,
            exact,
            rng.choices(FUZZY_EDIT_COUNTS, weights=FUZZY_EDIT_COUNT_WEIGHTS)[0],
            alphabet + "STßﬆ" if mode == "fold" else alphabet,
        )
        subject = mutated + backref_text if reverse else backref_text + mutated

        # A zero-width assertion goes in a fifth of the sections, at one end or in the middle, which
        # is the only way the step-of-0 arms of fuzzy_match_item are reached at all.
        section = list(atoms)
        if rng.random() < 0.2:
            section.insert(rng.randrange(len(section) + 1), rng.choice(FUZZY_ZERO_WIDTH))

        # A NESTED fuzzy section in a fifth of the rows, which is the only way the stack traffic
        # round FUZZY and END_FUZZY is observable at all: with one section the outer counts are
        # always (0, 0, 0) and the outer node is always null, so pushing and popping them cannot
        # change an answer. Measured, not assumed - S38's control E, which drops the restore on the
        # FUZZY backtrack arm, found nothing at either seed until this went in.
        # The inner section starts at index 1 or later WHEREVER THERE IS ROOM, and that is the whole
        # of why this control has teeth. An inner section at the start is entered before the outer
        # one has spent anything, so the outer counts are (0, 0, 0) there and inheriting them is the
        # same as clearing them. Measured: with the start drawn from 0, control E found nothing at
        # either seed; hand-built rows whose first atom must be substituted diverged on 21 of 24.
        # An unbounded budget is safe over one-character atoms and is not safe over S39's - see
        # FUZZY_BOUNDED_CONSTRAINTS.
        heavy = bool(backref_text) or any(
            atom in FUZZY_REPEAT_ATOMS or atom in FUZZY_STRING_ATOMS or atom in FUZZY_FOLD_ATOMS
            for atom in atoms
        )
        constraints = FUZZY_BOUNDED_CONSTRAINTS if heavy else FUZZY_CONSTRAINTS

        # Both sides of the group reference fold to more than one character, which is the only shape
        # where WHICH character of a folding the test asks is observable at all.
        split = mode == "fold" and bool(backref_text)

        if len(section) >= 2 and rng.random() < FUZZY_NESTING_PROBABILITY:
            first = 1 if len(section) >= 3 else 0
            start = rng.randrange(first, len(section) - 1)
            end = rng.randrange(start + 1, len(section))
            inner = "(?:" + "".join(section[start : end + 1]) + ")" + _fuzzy_constraint(rng, constraints, split)
            section = section[:start] + [inner] + section[end + 1 :]

        pattern = "(?:" + "".join(section) + ")" + _fuzzy_constraint(rng, constraints, split)

        if backref_text:
            group = "(" + backref_text + ")"
            pattern = pattern + group if reverse else group + pattern

        # A LEADING assertion, in front of everything the row has built so far, so that the S57c
        # anchor-pin rule is reachable at all. See FUZZY_ANCHOR_GUARDS. It goes on after the
        # backreference group rather than before it, because the rule reads the assertions the
        # pattern passes before anything else happens and a group in front of one hides it. The
        # `if` short-circuits for 'fuzzy' itself, so that generator draws nothing new and its rows
        # are unchanged at every seed.
        if guarded:
            pattern = rng.choice(FUZZY_ANCHOR_GUARDS) + pattern

        flags = 0
        if reverse:
            # Reverse, which flips the step and so the position record_fuzzy writes: a change is
            # recorded one character back ALONG THE DIRECTION OF TRAVEL, which forwards is before the
            # character and backwards is after it.
            pattern = "(?r)" + pattern

        if mode == "ign":
            # STRING_IGN and REF_GROUP_IGN: simple case folding, one character to one character.
            pattern = "(?i)" + pattern
        elif mode == "fold":
            # STRING_FLD and REF_GROUP_FLD: full case folding, where one character on one side can
            # answer for up to three on the other and an error can land INSIDE a folding.
            pattern = "(?fi)" + pattern

        # ENHANCEMATCH: find a match, then re-run inside its own span with a tighter budget until the
        # fit stops improving. BESTMATCH: search the whole slice for the match with the fewest errors
        # rather than taking the first that fits, then re-examine the equal-best candidates. Drawn
        # independently, and `(?b)` goes in front, so a row that has both reads `(?b)(?e)`; both are
        # whole-pattern flags, so the order is presentation and the dispatch decides.
        #
        # BOTH ARE DRAWN BEFORE THEY ARE SUPPRESSED, never inside the `if`, so that changing which
        # rows may carry a flag does not reshuffle the whole row stream and make two waves
        # incomparable. See `_has_weighted_cost` for what the suppression is and why it is not a
        # classification instead.
        enhance = rng.random() < FUZZY_ENHANCE_PROBABILITY
        bestmatch = rng.random() < FUZZY_BESTMATCH_PROBABILITY

        if _has_weighted_cost(pattern):
            enhance = False
            bestmatch = False

        if enhance:
            pattern = "(?e)" + pattern

        if bestmatch:
            pattern = "(?b)" + pattern

        row = {
            "generator": "fuzzy-anchored" if guarded else "fuzzy-overhang" if overhang else "fuzzy",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
            "subject": subject,
            "operation": ALL_OPERATIONS[i % len(ALL_OPERATIONS)],
        }
        if row["operation"] in SUB_OPERATIONS:
            row["template"] = "<>"
        if row["operation"] in LIMIT_OPERATIONS:
            row["count"] = rng.choice(SUB_COUNTS if row["operation"] in SUB_OPERATIONS else ITER_LIMITS)
        if row["operation"] in OPERATIONS and rng.random() < 0.25:
            # Partial matching, which is the only way check_fuzzy_partial (:9751) is reached.
            row["partial"] = True

        yield row


# --------------------------------------------------------------------------------------------
# The long-subject variants
# --------------------------------------------------------------------------------------------
#
# Every generator above draws a subject of at most MAX_SUBJECT_LENGTH (8) characters, so no wave
# this project has ever run has asked either engine to walk a text. Three paths only a long subject
# reaches: the scan that steps position by position looking for a start (upstream's `search_start`
# family, and this port's equivalent loop), the repeat guards that only bite after many iterations,
# and a fuzzy insert budget spent far from where the match began.
#
# These are a WRAPPER rather than four new grammars, which is the whole design: the pattern a long
# row carries is drawn by the base generator unchanged, so a long row and a short row differ in the
# one variable under test. Writing four long-subject grammars would have made every divergence a
# question about which grammar found it.

LONG_BASES = {
    "literals-long": "literals",
    "quantifiers-long": "quantifiers",
    "partial-long": "partial",
    "fuzzy-long": "fuzzy",
}

# The span the filler brings the subject up to. The floor is well past any buffer or unrolled-loop
# size either engine uses, and the ceiling is what one `search` answers inside the 10s row timeout.
MIN_LONG_SUBJECT = 1000
MAX_LONG_SUBJECT = 20000

# Drawn from characters no generator above puts in a pattern's literals - the base alphabets are
# `abcde` plus the astral letter/digit/symbol set, and the anchor generator's line breaks. It is
# NOT a guarantee that the filler cannot match: `.`, `\w`, `[^a]` and a fuzzy substitution all
# reach it, and a fuzzy pattern can match anywhere. It is a bias, and the closing notes measure how
# far the bias actually carried rather than asserting it.
LONG_FILLER_ALPHABET = "qQ§"

# `quantifiers-long` takes its filler from the BASE ALPHABET instead, and the reason is that its
# path is a different one. The other three want the match to begin far from where the scan started,
# so they want filler the pattern steps over; a repeat guard is reached by ITERATIONS, and a repeat
# cannot iterate over text it cannot consume - so the column that matters for this generator is
# `long` (matches 100+ characters long), not `walked` (matches beginning 100+ characters in).
#
# RE-MEASURED 2026-09-15, and the margin is much smaller than S52 sitting 4 recorded. Setting
# `LONG_REPEAT_FILLER_GENERATORS = ()` and running
# `tools/probes/generator-long-subject-reach.py --count 200 --seed 7` both ways gives:
#
#   unmatchable filler   match 148   walked 9   dist 0   len 0   long 28
#   base alphabet        match 155   walked 5   dist 0   len 1   long 32
#
# So the base alphabet buys 4 rows on `long` (28 -> 32) and COSTS 4 on `walked` (9 -> 5), and it
# moves the median match length from 0 to 1. Sitting 4's notes claimed it took `long` "from 0 to
# 32" and put the median length at 1 with the unmatchable filler; both were misreadings of the
# table's columns, caught by the blind review re-running it. The line stays because 28 -> 32 is
# still the right direction on the right column and changing it would invalidate every recorded
# long wave - but it rests on a thin margin, and whether `quantifiers-long` needs a filler rule of
# its own at all is worth re-deciding rather than inheriting.
LONG_REPEAT_FILLER_GENERATORS = ("quantifiers-long",)

# regex.REVERSE, the flag form of `(?r)`. Which side the filler goes on depends on it.
REVERSE = 0x400


# --------------------------------------------------------------------------------------------
# Timeout rows (S52)
# --------------------------------------------------------------------------------------------
#
# The one generator whose rows are MEANT to run out, and the only one whose `timeout` outcome the
# consumer compares instead of skipping. What makes that legitimate is the margin, and the margin is
# measured rather than assumed: `tools/probes/timeout-row-margin.py` and its port half put every
# shape below to both engines and report how far past the budget each one is still running.
#
# WHY A CURATED TABLE RATHER THAN A GRAMMAR. A timeout row is only evidence if BOTH engines
# certainly blow through the budget; a generator that composed catastrophic shapes at random would
# draw rows near the knee, where the verdict turns on the machine rather than on the engine, and
# those rows would flap. Every shape here was measured, and so was the length the table draws above.
#
# WHAT IS NOT IN THE TABLE IS THE INTERESTING HALF (measured 2026-09-15, regex 2026.9.10 and this
# port at Release, .scratch/s14-catastrophic.py and s14-family.py). Upstream is NOT vulnerable to
# the textbook shapes: `(a+)+$`, `(a*)*$`, `(.*,)*z`, `(a+)+\1$` and `(?=(a+)+$)a` all answer in
# milliseconds at every length tried, because a nested repeat over a single-character body collapses.
# What does blow up is an AMBIGUOUS ALTERNATION under a repeat - two branches that can match the same
# text - and the ambiguity has to survive compilation: `(?:ab|a)+$` and `(?:[ab]|a)+$` are both fast.
# So is `(?i)(a|A)+$`, and so is the reversed `(?r)(a|a)+^`. This family is narrower than it looks,
# which is why widening it means re-running the probe rather than editing the table.
TIMEOUT_SHAPES = (
    ("alt-same", r"(a|a)+$"),
    ("alt-same-star", r"(a|a)*$"),
    ("alt-prefix", r"(?:a|aa)+$"),
    ("alt-prefix-3", r"(?:a|aa|aaa)+$"),
    ("alt-groups", r"((a)|(a))+$"),
    ("alt-nested", r"(?:(?:a|a)+)+$"),
    ("alt-same-word", r"(a|a)+\b\d"),
    ("alt-same-lookahead", r"(?=(a|a)+$)a"),
    ("alt-same-atomic-free", r"(a|a)+x"),
    ("alt-backref", r"(a|a)+(\1)$"),
)

# A quarter of a second, against shapes still running after five on both engines - a margin over
# twenty times. Small because every row of this generator spends its whole budget by construction:
# at 2000 rows that is eight minutes of wall clock on each side, and at ten seconds it would be five
# hours. Raising it buys nothing - the shapes do not finish at five seconds either.
TIMEOUT_ROW_BUDGET_SECONDS = 0.25

# Above every shape's measured knee, with room to spare. `timeout-row-margin.py --knee` reports the
# shortest subject each shape is still running 5 seconds on, and they range from 24 to 36 - the floor
# is set by the slowest-to-blow-up shape, `(?:a|aa)+$` at 36, and not by the average. Longer is free
# rather than costly: the row stops at its budget either way, so the only thing length buys is how
# certain the timeout is.
MIN_TIMEOUT_REPEATS = 40
MAX_TIMEOUT_REPEATS = 56


def _generate_timeout(rng: random.Random, count: int):
    """Yields ``count`` rows drawn from shapes measured catastrophic on BOTH engines.

    The question a row asks is not what the pattern matches - neither engine will ever find out -
    but whether the engine HONOURS A DEADLINE on the path this operation takes. That is why the
    operation is drawn across all of `ALL_OPERATIONS` rather than fixed: `Replace`, `Split`,
    `Matches` and `Match` each hand the budget to a different loop in this port, and a loop that
    never polls it is a hang the ported suite cannot see. Measured 2026-09-15: all ten shapes
    against all eight operations are still running after five seconds on upstream, 80 of 80 cells
    (.scratch/s14-ops.py, reproduced by the committed probe).

    NO FLAGS, and that is a measured constraint rather than a simplification - `(?i)` makes
    `(a|A)+$` fast, and `(?r)` makes `(a|a)+^` fast. A generator that composed this family with the
    flag alphabet every other generator uses would draw rows that answer, and a timeout row that
    answers is a divergence. See `TIMEOUT_SHAPES` for what else drops out of the family.

    **`count` IS A CEILING HERE, NOT A TARGET, and this is the one generator where that is right.**
    Its question space is FINITE - ten shapes against eight operations is eighty questions, and the
    subject length moves nothing but how certain the timeout is - so the grid is enumerated and
    shuffled rather than sampled. Any `count` of eighty or more therefore draws every cell exactly
    once, which is both cheaper and STRONGER than sampling: a random draw of eighty from eighty
    cells with replacement misses about a third of them, and the cells are the point. It matters
    because every row of this generator spends its whole budget by construction, on both engines:
    at the default 300 a seed the recording alone took 75 seconds (measured 2026-09-15), against
    20 for the capped grid, and a 2000-row sweep would have spent eight minutes a seed re-asking
    eighty questions twenty-five times each.
    """
    grid = [(pattern, operation) for _, pattern in TIMEOUT_SHAPES for operation in ALL_OPERATIONS]
    rng.shuffle(grid)
    for pattern, operation in grid[:count]:
        subject = "a" * rng.randrange(MIN_TIMEOUT_REPEATS, MAX_TIMEOUT_REPEATS + 1) + "!"
        row = {
            # Every generator sets this, and without it `_record_row` falls back to "rows" - the tag
            # reserved for a hand-written `--rows` file. It is not cosmetic: it is what a divergence
            # block names in the report, and what `_compile_upstream` keys PREFILTER_FREE_GENERATORS
            # off.
            "generator": "timeout",
            "pattern": pattern,
            "flags": 0,
            "subject": subject,
            "operation": operation,
            "timeout": TIMEOUT_ROW_BUDGET_SECONDS,
        }
        if operation in SUB_OPERATIONS:
            # Never reached - the scan runs out first - but a substitution row without one is
            # refused by `_record_row` before upstream is asked at all.
            row["template"] = "z"
        if operation in LIMIT_OPERATIONS:
            row["count"] = 0
        yield row


def _generate_long(name: str, rng: random.Random, count: int):
    """Yields ``count`` rows from the base generator with each subject padded into a long text.

    Two rules, and both are about not destroying the question the base row asked.

    **The filler goes on the side the pattern does not run off.** A forward pattern is padded on
    the LEFT, so the scan has a text to walk before it can match and the subject's own right-hand
    edge - which is where a `partial` row runs out of text - is still the edge. A reversed one
    reads right to left and runs out at the LEFT end, so it is padded on the RIGHT instead. Pad the
    wrong side and a `partial-long` row is just a `literals` row with a long prefix.

    **The operation is forced to `search`.** Every path this variant exists to reach is a scan;
    `match` and `fullmatch` are anchored, so on a padded subject they answer at position 0 or not
    at all and reach nothing new, and `fullmatch` cannot match at all. It also bounds the cost: a
    `finditer`, `split` or `sub` over 20,000 characters is one row that can spend the whole 10s
    timeout, and `fuzzy` cycles ALL_OPERATIONS. `partial` is left exactly as the base row set it,
    because a partial `search` over a long text IS the interesting row here.
    """
    base = LONG_BASES[name]
    alphabet = ALPHABETS[0] if name in LONG_REPEAT_FILLER_GENERATORS else LONG_FILLER_ALPHABET
    for row in _generate(base, rng, count):
        filler = "".join(
            rng.choice(alphabet)
            for _ in range(rng.randrange(MIN_LONG_SUBJECT, MAX_LONG_SUBJECT + 1))
        )
        # `(?r` rather than `(?r)`, and the REVERSE flag too: `(?ri)` is reversed and so is a row
        # whose flags carry 0x400, and either spelling padded as though it were forward is the
        # "just a `literals` row with a long prefix" failure the docstring warns about. Measured
        # 2026-09-15 over 2,400 long rows at seeds 7 and 20260915: NO row is reversed by either of
        # those spellings today, so this widening changes no recorded wave. It is here so that a
        # later generator emitting one cannot silently pad it on the wrong side.
        reverse = "(?r" in row["pattern"] or bool(row["flags"] & REVERSE)
        row["subject"] = row["subject"] + filler if reverse else filler + row["subject"]
        row["generator"] = name
        row["operation"] = "search"
        # Only OPERATIONS rows carry these, and `search` is one; a row whose operation was a
        # substitution or an iteration brought its template and limit with it, and they are not
        # fields a `search` row has.
        row.pop("template", None)
        row.pop("count", None)
        yield row


def _generate(name: str, rng: random.Random, count: int):
    """Yields ``count`` unrecorded rows from the named generator.

    Deliberately simple: S16 - literals, plain anchors and the engine spine - is their first
    customer, and a generator that emits constructs no slice has ported yet produces a wave that is
    all ``unsupported`` and tells nobody anything. Later slices add their own.
    """
    if name not in GENERATORS:
        raise SystemExit(f"unknown generator {name!r}; expected one of {', '.join(GENERATORS)}")

    if name in LONG_BASES:
        yield from _generate_long(name, rng, count)
        return

    if name == "timeout":
        yield from _generate_timeout(rng, count)
        return

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

    if name == "interactions":
        yield from _generate_interactions(rng, count)
        return

    if name == "lookaround":
        yield from _generate_lookaround(rng, count)
        return

    if name == "conditionals":
        yield from _generate_conditionals(rng, count)
        return

    if name == "verbs":
        yield from _generate_verbs(rng, count)
        return

    if name == "recursion":
        yield from _generate_recursion(rng, count)
        return

    if name == "partial":
        yield from _generate_partial(rng, count)
        return

    if name == "partial-sliced":
        yield from _generate_partial(rng, count, sliced=True)
        return

    if name == "fuzzy":
        yield from _generate_fuzzy(rng, count)
        return

    if name == "fuzzy-anchored":
        yield from _generate_fuzzy(rng, count, guarded=True)
        return

    if name == "fuzzy-overhang":
        yield from _generate_fuzzy(rng, count, overhang=True)
        return

    if name == "posix":
        yield from _generate_posix(rng, count)
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

    recorded = [_record_row_and_its_control_answers(regex, row) for row in unrecorded]

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

        # And a span upstream can report that is not in the subject at all - see `_utf16_index`.
        # It has to stay impossible on this side too, or the consumer is handed a plausible span
        # and agrees with a defect. Codepoint 5 is two past the end of a three-codepoint subject,
        # so its UTF-16 index is the total 4 plus 2, and (5, 3) keeps its negative length.
        for span, expected in (((5, 3), [6, -2]), ((4, 4), [5, 0])):
            got = _to_index_length(offsets, span)
            if got != expected:
                failures.append(f"out-of-subject span {span} translates to {got}, expected {expected}")

    # A RECORDED ROW FED BACK IN MUST RECORD THE SAME ROW. `--rows` is how an entry's `Example` is
    # made and how a divergence is minimised one cut at a time, and both copy a row out of a wave.
    # A recorded row's `pos`/`endpos` are UTF-16 while the pair `--rows` reads is in CODEPOINTS, so
    # without `codepointSlice` winning, a sliced row with anything astral before its slice comes
    # back as a DIFFERENT QUESTION - silently, and with a plausible answer. Found 2026-09-15 on
    # S52's seed 20260915 row 105880, whose slice came back [2, 10] where the wave recorded [1, 7].
    sliced = row(pattern="(\\D)\\1", subject="\U00010400ab\U0001d518c", operation="search",
                 pos=1, endpos=4)
    once = _record_row(regex, sliced)
    twice = _record_row(regex, dict(once))
    for key in ("pos", "endpos", "codepointSlice", "codepointSpan", "outcome"):
        if once.get(key) != twice.get(key):
            failures.append(
                f"a recorded row fed back through --rows changes its {key}: "
                f"{once.get(key)!r} became {twice.get(key)!r}"
            )

    # AND A SUBJECT CUT, the other half of the minimisation loop and the case that rules out
    # guarding this with "the two slices must agree": cutting a character out of the subject leaves
    # the recorded UTF-16 `pos`/`endpos` stale while `codepointSlice` still indexes the new subject.
    # Such a row has to RECORD, and it has to record the codepoint slice it was given. The second
    # blind pass of S52 sitting 8 reproduced exactly this being refused by such a guard.
    # Cut from the END, past the slice, so the slice it was given still FITS: a cut that removes
    # text the slice covered is clamped, and clamping is correct rather than a regression.
    shorter = dict(once)
    shorter["subject"] = once["subject"][:-1]
    try:
        cut = _record_row(regex, shorter)
    except SystemExit as e:
        failures.append(f"cutting a character from a recorded row's subject made it unrecordable: {e}")
    else:
        if cut.get("codepointSlice") != once.get("codepointSlice"):
            failures.append(
                "cutting a character past the slice of a recorded row's subject changed its "
                f"codepointSlice from {once.get('codepointSlice')!r} to "
                f"{cut.get('codepointSlice')!r}, so the cut row no longer asks the slice it was given"
            )

    # THE METAMORPHIC CHECKER MUST FIRE ON A ROW THAT CONTRADICTS ITSELF, and must not fire on one
    # that does not. It is the only thing in this file whose failure mode is SILENCE: a checker that
    # stops firing records a clean wave and nobody can tell it from a wave with nothing wrong in it,
    # which is the exact failure S52c exists to remove. Each case is a hand-corrupted recorded row,
    # so no upstream call is needed and the guard cannot itself flake.
    contradictory = {
        "fuzzy-counts-match-changes": {
            "pattern": "(?:ab){e<=1}", "outcome": {"kind": "match", "groups": [
                {"number": 0, "success": True, "index": 0, "length": 2, "captures": [[0, 2]]}],
                "lastIndex": -1, "fuzzyCounts": [1, 0, 0],
                "fuzzyChanges": {"substitutions": [], "insertions": [], "deletions": []}}},
        "lastindex-participated": {
            "pattern": "(a)?b", "outcome": {"kind": "match", "groups": [
                {"number": 0, "success": True, "index": 0, "length": 1, "captures": [[0, 1]]},
                {"number": 1, "success": False, "index": 0, "length": 0, "captures": []}],
                "lastIndex": 1}},
        "group-spans-inside-match": {
            "pattern": "a(b)", "outcome": {"kind": "match", "groups": [
                {"number": 0, "success": True, "index": 0, "length": 1, "captures": [[0, 1]]},
                {"number": 1, "success": True, "index": 1, "length": 1, "captures": [[1, 1]]}],
                "lastIndex": 1}},
    }
    for invariant, corrupt in contradictory.items():
        got = _structural_violations(corrupt)
        if invariant not in got:
            failures.append(f"the checker did not fire {invariant} on a row that breaks it: {got}")

    # And the narrowing: the SAME group-span row with a `\K` or a lookaround in its pattern is
    # legitimate, measured in tools/probes/upstream-free-tier-invariant-grounds.py sections 3 and 5.
    for pattern in ("a\\K(b)", "a(?=(b))"):
        narrowed = {**contradictory["group-spans-inside-match"], "pattern": pattern}
        if "group-spans-inside-match" in _structural_violations(narrowed):
            failures.append(f"the checker fires group-spans-inside-match on {pattern!r}, which is legitimate")

    # A CHOOSING flag that chose nothing from a non-empty set, and one that did not.
    lost = _choosing_flag_violations("bestmatch-no-worse", {"kind": "nomatch"},
                                     {"kind": "match", "groups": [], "lastIndex": -1})
    if "bestmatch-no-worse" not in lost:
        failures.append("the checker did not fire bestmatch-no-worse where the flagless twin matched")
    kept = _choosing_flag_violations("bestmatch-no-worse", {"kind": "nomatch"}, {"kind": "nomatch"})
    if kept:
        failures.append(f"the checker fires bestmatch-no-worse where neither answer matched: {kept}")

    # THE TWO NARROWINGS S52c's FIRST THREE-SEED WAVE FORCED, both of which turned a row upstream
    # simply did not answer into a reported contradiction. Guarded because each is one predicate
    # deep and would go back to over-reporting on a careless edit, silently and only on a wave.
    unanswered = _choosing_flag_violations(
        "bestmatch-no-worse",
        {"kind": "timeout", "seconds": 10.0},
        {"kind": "match", "groups": [], "lastIndex": -1},
    )
    if unanswered:
        failures.append(
            f"the checker reads a row upstream timed out on as BESTMATCH losing a match: {unanswered}"
        )

    ranked = _control_violations({
        "outcome": {"kind": "timeout", "seconds": 10.0},
        "bestmatchFreeOutcome": {"kind": "match"},
        "posixFreeOutcome": {"kind": "match"},
    })
    if ranked:
        failures.append(f"the checker calls a timeout beside a RANKING flag's twin a fault: {ranked}")
    pruned = _control_violations({
        "outcome": {"kind": "timeout", "seconds": 10.0},
        "pruneOutcome": {"kind": "sub", "text": "x", "count": 0},
    })
    if "no-fault-where-a-twin-answers" not in pruned:
        failures.append("the checker no longer fires where a row hangs and its (*PRUNE) twin answers")

    # A twin that REPLACED NOTHING never rendered the template, so it is no answer to a row whose
    # error came out of one - see `_twin_answered`. Both rows the fault limb fired on in S52c's
    # second corrected wave were exactly this.
    empty_twin = _control_violations({
        "outcome": {"kind": "error", "exception": "IndexError", "message": "", "whileMatching": True},
        "pruneOutcome": {"kind": "sub", "text": "unchanged", "count": 0},
    })
    if empty_twin:
        failures.append(f"a substitution twin that replaced nothing counts as an answer: {empty_twin}")
    real_twin = _control_violations({
        "outcome": {"kind": "error", "exception": "IndexError", "message": "", "whileMatching": True},
        "pruneOutcome": {"kind": "sub", "text": "replaced", "count": 2},
    })
    if "no-fault-where-a-twin-answers" not in real_twin:
        failures.append("a substitution twin that DID replace no longer counts as an answer")

    # POSIX BUYS LENGTH WITH ERRORS, so its cost limb needs the same SPAN and not merely the same
    # start - `(?p)(?:abc){e<=2}` over 'abxxyc' is (0, 4) at a cost of 2 where the flagless engine's
    # (0, 3) costs 1, and that is leftmost-longest working. Raised by S52c's blind review on a shape
    # no wave had drawn. BESTMATCH keeps the same-start form, because it minimises errors rather
    # than length, and both directions are guarded so a later edit cannot quietly swap them.
    longer = {"kind": "match", "lastIndex": -1, "fuzzyCounts": [1, 1, 0],
              "groups": [{"number": 0, "success": True, "index": 0, "length": 4, "captures": []}]}
    shorter = {"kind": "match", "lastIndex": -1, "fuzzyCounts": [1, 0, 0],
               "groups": [{"number": 0, "success": True, "index": 0, "length": 3, "captures": []}]}
    if _choosing_flag_violations("posix-chooses-among-flagless-answers", longer, shorter):
        failures.append("POSIX buying length with errors is reported as a contradiction")
    if "bestmatch-no-worse" not in _choosing_flag_violations("bestmatch-no-worse", longer, shorter):
        failures.append("BESTMATCH answering at a greater cost is no longer a contradiction")

    # `captures-are-the-texts-of-spans` IS THE ONE INVARIANT `_describe_match` COLLECTS RATHER THAN
    # reading off a row, so it is the one a guard over `_structural_violations` cannot reach - and
    # the blind review found it was therefore the only id with no guard at all, in a file whose
    # stated failure mode is silence. Dropping the check was a one-token edit that nothing noticed.
    #
    # A stub stands in for the match object because the point is a match whose `captures` and
    # `spans` DISAGREE, which upstream will not produce to order.
    class _Stub:
        """A match object whose captures do not match its spans, and a pattern with one group."""

        groups = 1
        flags = 0
        string = "abc"
        lastindex = None
        lastgroup = None
        partial = False
        fuzzy_counts = (0, 0, 0)

        def __init__(self, captures):
            self._captures = captures

        def span(self, number):
            return (0, 3) if number == 0 else (1, 2)

        def spans(self, number):
            return [(0, 3)] if number == 0 else [(1, 2)]

        def captures(self, number):
            return ["abc"] if number == 0 else self._captures

    honest, lying = [], []
    _describe_match(_Stub(["b"]), _Stub(["b"]), [0, 1, 2, 3], honest)
    _describe_match(_Stub(["X"]), _Stub(["X"]), [0, 1, 2, 3], lying)
    if honest:
        failures.append(f"the captures check fires on a match whose captures ARE its spans: {honest}")
    if "captures-are-the-texts-of-spans" not in lying:
        failures.append("the captures check no longer fires on a capture text that is not its span")

    for failure in failures:
        print("self-check: " + failure, file=sys.stderr)
    if failures:
        return 1

    print(
        "self-check: the reserved-name, interpreter-limit, per-generator-seed, index-translation, "
        "slice-round-trip and metamorphic-checker guards all fire"
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

    # The metamorphic invariants, counted per id. A ROW IS COUNTED ONCE PER ID, not once per
    # violating match, so this line is the number of candidates a triage has to judge. Printed even
    # at zero, because "no invariant fired" and "the checker did not run" are different facts and a
    # missing line cannot tell them apart.
    contradictions: dict[str, int] = {}
    for row in rows:
        for invariant in row.get("selfContradiction", ()):
            contradictions[invariant] = contradictions.get(invariant, 0) + 1
    rows_with = sum(1 for row in rows if row.get("selfContradiction"))
    if contradictions:
        print(f"  {rows_with} rows contradict themselves: "
              + ", ".join(f"{count} {name}" for name, count in sorted(contradictions.items())))
    else:
        print("  no row contradicts itself")
    return 0


if __name__ == "__main__":
    os.environ.setdefault("PYTHONDONTWRITEBYTECODE", "1")
    sys.exit(main())
