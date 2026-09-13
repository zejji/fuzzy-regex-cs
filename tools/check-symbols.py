#!/usr/bin/env python
"""Cross-checks the port's two symbol ledgers: how many functions upstream defines, and how many
of them PORTMAP.md accounts for.

S26 (2026-09-01) counted upstream/src/_regex.c's function definitions two independent ways and
cross-checked docs/PORTMAP.md's coverage of them; S36 (2026-09-12) re-ran the same check at the
Phase 4 close. Both scripts lived in session scratch and were lost - "the same unreproducible-
evidence problem the slice skill records for negative controls" (S36's own words) - so this is the
third build of it, and the first one committed, per S43's slice file.

The count is mechanical, and getting it right is the whole point: S26's first attempt matched
function declarations with a regex and reported 458, undercounting by 109, because upstream puts
the opening brace at the end of the declarator, wraps long parameter lists over several lines and
hides the return type behind a macro (`Py_LOCAL_INLINE(Py_ssize_t) foo(...) {`), so a regex over
the *opening* line misses it. The fix - and this file's primary method - is brace-depth tracking
over the whole file: a `{` at depth 0 immediately preceded by a function-call-shaped `)` opens a
function body, and its name is the identifier just before that `)`'s matching `(`. Comments and
string/char literals are blanked first (there is exactly one `'{'` char literal in this file, which
would otherwise desync the depth for everything after it - measured 2026-09-13). The independent
cross-check is `grep -c '^}$'`: in this file a function body is the only construct that closes with
a bare `}` at column 0 (a struct or array initialiser closes with `};`), so the two methods either
agree or one of them is wrong. Both give 567 against the upstream commit this port has checked out
(re-measured 2026-09-13, matching S26 and S36's own 567).

Naming coverage in PORTMAP.md is checked two ways, both approximations PORTMAP's own accounting
section admits it needs: a direct name search (a lower bound - "the family in the `x_left`/`_right`
shorthand a name search cannot see" is real and this script cannot see it either), widened by the
line-range citations (`` `:1234-5678` ``) that the matcher-and-later sections of PORTMAP.md use to
cover a family without naming every member.

WHAT IS LEFT OVER IS PINNED, NOT IGNORED, and the pin is what gives the exit code a meaning. A first
build of this script exited non-zero on 38 names and would have done so on every run for ever, which
is a gate nobody can act on. Those 38 are all covered by PORTMAP family rows that name a few
representative members and take the rest in PROSE - "every `*_dealloc` and `*_deepcopy`", "the
`scanner_*`/`splitter_*` iterator protocol", "`re_alloc` ... `release_state_lock`" - so they are
accounted for by a human and invisible to a script by construction. They are listed in
PROSE_ACCOUNTED below with the row that covers each, and the run fails if the set CHANGES IN EITHER
DIRECTION: a new name means upstream gained a function nothing accounts for, and a name leaving
means PORTMAP now lists it individually and the pin has gone stale. That is the same contract as
tests/parity-baseline.json and ExpectedDivergences.cs, and it is chosen for the same reason - a
list that only ever grows is the rot those two exist to avoid.

Also reported: how many times `Seam.For` appears under `src/`, a plain recursive count and the
matcher's own tally of what it has not yet dispatched to a real opcode.

Usage::

    python tools/check-symbols.py
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
UPSTREAM_C = REPO / "upstream" / "src" / "_regex.c"
PORTMAP = REPO / "docs" / "PORTMAP.md"
SRC = REPO / "src"

ACCOUNTING_HEADING = re.compile(r"^### Every `_regex\.c` function, accounted for", re.MULTILINE)
NODE_COMPILER_HEADING = re.compile(r"^### The node compiler", re.MULTILINE)
NEXT_HEADING = re.compile(r"^#{1,6} ", re.MULTILINE)

# Only a bare `:1234-5678` or an explicit `_regex.c:1234-5678` - never `_regex_core.py:...` or
# `_main.py:...`, which use the same bare-colon shorthand for a DIFFERENT file earlier in the
# document (the parser's own symbol map, before the node-compiler heading). Restricting the sweep
# to that heading onward (below) is what keeps the two conventions apart. Two spellings of a range
# are both in use: one backtick pair (`` `:1234-5678` ``) and two, hyphen outside either
# (`` `:1234`-`:5678` ``).
RANGE_REF = re.compile(r"`(?:_regex\.c)?:(\d+)`?-`?(?:_regex\.c)?:?(\d+)`")

# The names PORTMAP.md accounts for in PROSE rather than by naming them, grouped by the family row
# that covers each. Re-derived and checked against those rows on 2026-09-13; see the module
# docstring for why this is a pinned set rather than a permanent failure.
PROSE_ACCOUNTED = {
    # "Python methods, properties and protocol", 46: the accessors are argument shuffling over
    # `match_get_*_by_index`, the protocol members have no C# counterpart, and the deallocs free
    # memory a garbage-collected heap frees itself.
    "make_pattern_copy", "match_allcaptures", "match_allspans", "match_captures",
    "match_capturesdict", "match_copy", "match_dealloc", "match_deepcopy", "match_end",
    "match_ends", "match_get_group", "match_get_group_index", "match_get_group_slice",
    "match_getitem", "match_group", "match_length", "match_regs", "match_spans", "match_starts",
    "match_string", "pattern_copy", "pattern_dealloc", "pattern_deepcopy", "pattern_subf",
    "pattern_subfn", "safe_dealloc", "scanner_iter", "scanner_match",
    "scanner_search",
    # `scanner_iternext` CAME OUT on 2026-09-13 (S44), and this is the pin working in the
    # direction the module docstring says it must. It is now named outright, in the sync log's
    # row for PRs 615-618 - which lists every function those PRs touch, `scanner_iternext`
    # included, and says why none of them needs porting. Named is stronger than covered by a
    # family row, so it no longer belongs here. The set is 37 from this date.
    # "GIL, locks and allocation (`re_alloc` ... `release_state_lock`)", 13: .NET has no GIL.
    "acquire_GIL", "release_GIL",
    # "Bytes support (`bytes1_char_at` ... `join_strings`)", 18: the three-by-three width-selected
    # accessors this port replaces with one `MatchState.CharAt` over a UTF-16 string.
    "bytes4_point_to",
    # `_REV` members taken in the `try_match_*` and `search_start_*` family rows' own shorthand,
    # which those rows say a name search cannot see.
    "search_start_STRING_REV", "try_match_STRING_REV",
    # Two more that are NOT in those two families, and a blind review caught them being filed there:
    # `match_many_SET_REV` belongs to the `match_many_*` row, and `partial_string_match`
    # (`_regex.c:11655`) sits outside both cited line ranges - PORTMAP names only its `_ign` half at
    # `:326`, and the row that accounts for the base function is the `GREEDY_REPEAT_ONE` retreat
    # sub-switch at `:305` (`:15907-16270`).
    "match_many_SET_REV", "partial_string_match",
    # Taken by the "Word and line predicates" row's `unicode_` twins shorthand, and named outright
    # at PORTMAP's `ascii_at_line_start`/`_end`, `unicode_at_line_start`/`_end` row - the slash
    # shorthand is what hides it from a text search. NOT the `locale_*` row: it is defined at
    # `_regex.c:1963`, outside that row's `:1025`-`:1311`, and there is no `locale_at_line_end` at
    # all (the locale table points its line slots at `ascii_at_line_end`). A blind review caught it
    # filed under `locale_*` here.
    "unicode_at_line_end",
}


def strip_comments_and_literals(text: str) -> str:
    """Blanks comments and string/char literal bodies so a brace inside one (there is exactly one,
    a `'{'` char literal) cannot desync the depth count. Length and newline positions are kept."""
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == "/" and text[i : i + 2] == "/*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
        elif c == "/" and text[i : i + 2] == "//":
            j = text.find("\n", i)
            j = n if j < 0 else j
        elif c in "\"'":
            j = i + 1
            while j < n and text[j] != c:
                j += 2 if text[j] == "\\" else 1
            j = min(j + 1, n)
        else:
            out.append(c)
            i += 1
            continue
        out.append("".join(ch if ch == "\n" else " " for ch in text[i:j]))
        i = j
    return "".join(out)


def function_name_before(s: str, close_paren_pos: int) -> str | None:
    """The identifier just before the `(` matching the `)` at close_paren_pos."""
    depth = 1
    i = close_paren_pos - 1
    while i >= 0 and depth > 0:
        if s[i] == ")":
            depth += 1
        elif s[i] == "(":
            depth -= 1
        i -= 1
    j = i
    while j >= 0 and s[j].isspace():
        j -= 1
    end = j + 1
    while j >= 0 and (s[j].isalnum() or s[j] == "_"):
        j -= 1
    start = j + 1
    return s[start:end] if start < end else None


def find_function_defs(stripped: str) -> list[tuple[str, int]]:
    """Primary method: brace-depth tracking. Returns (name, 1-based line number) per definition."""
    depth = 0
    last_nonspace = ""
    last_nonspace_pos = -1
    line = 1
    defs = []
    for i, c in enumerate(stripped):
        if c == "{":
            if depth == 0 and last_nonspace == ")":
                name = function_name_before(stripped, last_nonspace_pos)
                if name:
                    defs.append((name, line))
            depth += 1
        elif c == "}":
            depth -= 1
        if c == "\n":
            line += 1
        elif not c.isspace():
            last_nonspace = c
            last_nonspace_pos = i
    return defs


def accounting_section_span(portmap_text: str) -> tuple[int, int]:
    """Start/end offsets of "Every `_regex.c` function, accounted for", so a name search over the
    rest of the document does not count that section naming itself."""
    m = ACCOUNTING_HEADING.search(portmap_text)
    if not m:
        raise SystemExit("PORTMAP.md: accounting section heading not found")
    end_m = NEXT_HEADING.search(portmap_text, m.end())
    return m.start(), (end_m.start() if end_m else len(portmap_text))


def line_ranges_covering(portmap_text: str) -> list[tuple[int, int]]:
    """Every `_regex.c` line-range citation from "The node compiler" heading onward - the point
    past which every bare `:N-M` citation in this document means this file, not `_regex_core.py`
    or `_main.py`, which use the same shorthand for themselves earlier in the document."""
    m = NODE_COMPILER_HEADING.search(portmap_text)
    start = m.start() if m else 0
    return [(int(a), int(b)) for a, b in RANGE_REF.findall(portmap_text[start:])]


def main() -> int:
    c_text = UPSTREAM_C.read_text(encoding="utf-8")
    stripped = strip_comments_and_literals(c_text)
    defs = find_function_defs(stripped)
    primary_count = len(defs)
    cross_check = len(re.findall(r"^}$", c_text, re.MULTILINE))

    portmap_text = PORTMAP.read_text(encoding="utf-8")
    acc_start, acc_end = accounting_section_span(portmap_text)
    outside_text = portmap_text[:acc_start] + portmap_text[acc_end:]
    ranges = line_ranges_covering(portmap_text)

    named, not_named, unaccounted = [], [], []
    for name, line in defs:
        pattern = rf"\b{re.escape(name)}\b"
        if re.search(pattern, outside_text):
            named.append(name)
        else:
            not_named.append(name)
        # Unaccounted is a different question from "not individually named outside the section":
        # the accounting section's own family rows (e.g. "Python methods, properties and
        # protocol") name many members by hand right there, which the "outside" search above
        # deliberately cannot see (that is the whole reason the section is excluded from it).
        # So a name found ANYWHERE in the document, or covered by one of its line-range
        # citations, is accounted for; only neither is unaccounted.
        if not re.search(pattern, portmap_text) and not any(a <= line <= b for a, b in ranges):
            unaccounted.append(name)

    # bin/ and obj/ hold build output (a concurrent build writes them while this runs), not source -
    # `FuzzyRegex.xml`'s doc comments alone add 8 spurious hits across four copies if these aren't
    # skipped (measured 2026-09-13).
    seam_count = 0
    for path in SRC.rglob("*"):
        if path.is_file() and not {"bin", "obj"} & set(path.relative_to(SRC).parts):
            seam_count += path.read_text(encoding="utf-8", errors="replace").count("Seam.For")

    found = set(unaccounted)
    appeared = sorted(found - PROSE_ACCOUNTED)
    vanished = sorted(PROSE_ACCOUNTED - found)

    lines = [
        f"upstream/src/_regex.c function definitions: primary (brace-depth) {primary_count}, "
        f"cross-check (bare '}}' count) {cross_check}",
        f"named in docs/PORTMAP.md outside its accounting section: {len(named)}",
        f"not individually named there: {len(not_named)}",
        f"named nowhere and in no cited line range, accounted for in PROSE by a family row: "
        f"{len(found)} (pinned: {len(PROSE_ACCOUNTED)})",
        f"Seam.For occurrences under src/: {seam_count}",
    ]
    if appeared:
        lines.append(
            "UNACCOUNTED: nothing in PORTMAP.md names or covers these, and they are not pinned: "
            + ", ".join(appeared)
        )
    if vanished:
        lines.append(
            "STALE PIN: PORTMAP.md now accounts for these by name or line range, so they must come "
            "out of PROSE_ACCOUNTED: " + ", ".join(vanished)
        )
    if primary_count != cross_check:
        lines.append(f"MISMATCH: the two counting methods disagree ({primary_count} vs {cross_check})")

    report = "\n".join(lines)
    sys.stdout.buffer.write(report.encode("utf-8", "backslashreplace") + b"\n")

    return 1 if (appeared or vanished or primary_count != cross_check) else 0


if __name__ == "__main__":
    sys.exit(main())
