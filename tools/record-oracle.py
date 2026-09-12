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
    pos = row.get("pos")
    endpos = row.get("endpos")
    if pos is not None or endpos is not None:
        if operation not in OPERATIONS:
            raise SystemExit(
                f"operation {operation!r} carries no pos/endpos in this recorder (pattern "
                f"{pattern!r}); only {OPERATIONS} do."
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
        compiled = _compile_upstream(regex, recorded, pattern, flags, named_lists)
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
            # OVERLAPPED AND FORWARD ONLY, and the two exclusions are not tidiness - each is a case
            # where the walk below cannot ask upstream the same question the scanner asks, so a
            # recorded answer would be a third opinion rather than a second one. See its docstring.
            if overlapped and "(*SKIP)" in pattern and (compiled.flags & _REVERSE_FLAG) == 0:
                recorded["anchoredScan"] = _anchored_scan(compiled, subject, offsets)
        return recorded

    # Pattern.match/search/fullmatch take (string, pos, endpos, concurrent, partial, timeout), so
    # the slice goes positionally and `partial` by keyword.
    kwargs = {"partial": True} if partial else {}
    args = (subject,) if pos is None else (subject, pos, endpos)

    try:
        match = getattr(compiled, operation)(*args, **kwargs)
    except Exception as e:  # noqa: BLE001
        return failed(e, while_matching=True)

    if match is None:
        recorded["codepointSpan"] = None
        recorded["outcome"] = {"kind": "nomatch"}
        return recorded

    recorded["codepointSpan"] = list(match.span(0))
    recorded["outcome"] = {"kind": "match", **_describe_match(compiled, match, _utf16_offsets(subject))}

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
            same = compiled.match(subject, start, end, partial=True)
        except Exception:  # noqa: BLE001 - an unanswerable second question is recorded as unasked
            recorded["searchOnlyPartial"] = False
        else:
            recorded["searchOnlyPartial"] = same is None or not same.partial

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
        # Not derivable from the span either: a partial match and a complete match of the same text
        # are the same span and different answers. Always present, and always False on a row that
        # did not ask for a partial, so every generator's rows now also assert "not partial".
        "partial": bool(match.partial),
    }


def _anchored_scan(compiled, subject: str, offsets: list[int]) -> list[dict]:
    """The same overlapped scan, asked of upstream one match at a time from a fresh state each step.

    A SECOND FACT ABOUT UPSTREAM, never compared against anything, and recorded only for a
    ``finditer`` row that is OVERLAPPED, FORWARD, and whose pattern contains ``(*SKIP)``. Only that
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

    WHY OVERLAPPED ONLY, AND WHY FORWARD ONLY. Both exclusions were found by S34's blind review,
    which built a row the walk got wrong and showed the list absorbing an engine mutation because of
    it. Neither is tidiness: in each case the public API cannot ask upstream the same question its
    scanner asks, so a recorded answer would be a third opinion rather than a second one.

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
    pos = 0

    while 0 <= pos <= len(subject) and len(found) < limit:
        match = compiled.search(subject, pos)
        if match is None:
            break

        # The SAME shape a `matches` outcome's entries have, groups and all, so the consumer can
        # compare the whole rendering rather than only the spans. A stale slice shows up in a
        # CAPTURE as readily as in a whole-match span: at seed 314159 upstream's overlapped scan of
        # `((?:\p{L}(*SKIP))+)` reports group 1 as (0, 4) for a match spanning (1, 4), a capture
        # outside its own match, where this walk gives (1, 4).
        found.append(dict(_describe_match(compiled, match, offsets), codepointSpan=list(match.span(0))))

        # scanner_search_or_match's own overlapped step (:20903), which MatchState.AdvancePastMatch
        # ports: one character past where the match STARTED.
        pos = match.span(0)[0] + 1

    return found


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
INTERACTION_PIECES = (
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
INTERACTION_PIECE_WEIGHTS = (24, 12, 12, 9, 5, 9, 11, 18, 12, 8)

# Wrapped round the whole pattern, so the anchors and the boundaries are asked about positions an
# interior piece has reached rather than only about position 0.
INTERACTION_AFFIXES = (("^", ""), ("", "$"), ("^", "$"), (r"\b", ""), ("", r"\b"), ("", ""), ("", ""))

INTERACTION_BOUNDARIES = (r"\b", r"\B", r"\m", r"\M")

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


def _interaction_pattern(rng: random.Random, subject: str, version1: bool) -> tuple[str, int, list[str]]:
    """One composed pattern, with the group inventory a substitution template needs.

    Built strictly left to right, as ``_backref_pattern`` is, so a group number is only handed out
    once the group that owns it has been emitted - a reference to a group defined later is legal
    upstream but is `backrefs`' own test, not this one's.
    """
    atoms = INTERACTION_CLASS_ATOMS + INTERACTION_V1_CLASS_ATOMS if version1 else INTERACTION_CLASS_ATOMS
    counter = [0]
    names: list[str] = []
    defined: list[int] = []
    pieces: list[str] = []

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
        kind = rng.choices(INTERACTION_PIECES, weights=INTERACTION_PIECE_WEIGHTS)[0]
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
            # all: measured over 600 rows of seed 1 (`.scratch/ablate.py` in the S26 session), 34 of
            # 387 reference rows produced an answer with the table alone and 50 of 390 with this. It
            # is still the cell this generator exists for - a class, in a quantified capture group,
            # referenced back - only aimed at a subject that can satisfy it.
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
    return prefix + "".join(pieces) + suffix, counter[0], names


def _generate_interactions(rng: random.Random, count: int):
    """S26's generator: the S16-S25 constructs composed, over all eight operations.

    **Widened at the Phase 4 close (S36) to compose Phase 4's six families with the S16-S25 ones.**
    Two new pieces - `called-group` and `verb-alt` - plus a lookaround-as-condition arm on `cond`,
    POSIX at the row level and `partial=True` with the subject cut short. The cell the widening
    exists for is a lookaround round a group call inside a conditional inside a repeat, which no
    per-slice wave reaches; it found two things in its first three seeds, an upstream capture recorded
    outside the subject and a recorder that raised `IndexError` rather than write it down.

    Measured by `python tools/record-oracle.py --generator interactions --count 600 --seed 1`, after
    the last change to this generator: 119 rows produce an answer - a match, a non-empty match list,
    a split with more than one part or a substitution that replaced something - 469 produce none and
    12 are rejected by upstream. 299 rows carry IGNORECASE, 142 FULLCASE, 283 MULTILINE, 230
    VERSION1, 68 ASCII and 280 are reversed; 94 are POSIX (42 by flag, 52 as `(?p)`), 71 ask for a
    partial match, and 190 have an astral subject. 379 hold a backreference, 210 a named group, 133 a
    conditional (48 of them on a lookaround), 123 a lookaround, 81 a group call, 67 a backtracking
    verb and 39 a `\\K`. Every one of the eight operations is recorded exactly 75 times, because the
    operation is cycled by row index rather than drawn.

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
            # recorded rather than assumed - measured over 600 rows of seed 1, 43 of 371 reference
            # rows produced an answer without it and 50 of 390 with it (114 answers against 111
            # overall). Kept because it is a gain on the cell this generator exists for, not because
            # it is a large one.
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
        pattern, groups, names = _interaction_pattern(rng, subject, version1)

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
        if rng.random() < INTERACTION_POSIX_PROBABILITY:
            if rng.random() < INTERACTION_POSIX_INLINE_PROBABILITY:
                pattern = "(?p)" + pattern
            else:
                flags |= POSIX

        # `(?r)` as inline pattern text, as S23 and S25 write it: it is what a caller writes, and a
        # global flag has to be at the start of the pattern anyway.
        reverse = rng.random() < INTERACTION_REVERSE_PROBABILITY
        if reverse:
            pattern = "(?r)" + pattern

        operation = ALL_OPERATIONS[i % len(ALL_OPERATIONS)]
        row = {
            "generator": "interactions",
            "pattern": pattern,
            "flags": flags,
            "namedLists": {},
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
        pattern, _groups, _names = _interaction_pattern(rng, subject, version1)

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
POSIX_SUBJECT_ALPHABETS = ("ab", "abc", "aé\U0001f600")

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

        # And a span upstream can report that is not in the subject at all - see `_utf16_index`.
        # It has to stay impossible on this side too, or the consumer is handed a plausible span
        # and agrees with a defect. Codepoint 5 is two past the end of a three-codepoint subject,
        # so its UTF-16 index is the total 4 plus 2, and (5, 3) keeps its negative length.
        for span, expected in (((5, 3), [6, -2]), ((4, 4), [5, 0])):
            got = _to_index_length(offsets, span)
            if got != expected:
                failures.append(f"out-of-subject span {span} translates to {got}, expected {expected}")

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
