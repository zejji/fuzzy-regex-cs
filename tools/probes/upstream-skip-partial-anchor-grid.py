r"""The three `(*SKIP)`-plus-partial rows S60 sitting 2 left for judgement, asked the UNCAPPED way.

`gate-divergence-doors.py` opens every family's door on them, but its anchor sweep stops after
three hits, which finds the leftmost `pos` on a forward row and NOT the highest `endpos` on a
reversed one - and on a reversed row the highest is the anchor upstream's own search must try
first, so a capped sweep can invert the argument (`upstream-partial-anchor-reachability.py`, S52
sitting 18, row 24). These three rows include a reversed one, so they are asked here instead.

THE ROWS, and nothing is transcribed - each is read out of `TestResults/oracle/wave-<seed>.jsonl`
by row number, the same file the gate scored:

  seed 20260920 row 5014  `partial`         forward
  seed 31337    row 3633  `interactions`    REVERSED
  seed 31337    row 5200  `partial-sliced`  forward, and carries a slice

WHAT IS ASKED, per row:

  as drawn / (*SKIP)->(*PRUNE) / verb deleted
      `(*PRUNE)` prunes the backtracking `(*SKIP)` prunes and moves NO slice bound
      (upstream/src/_regex.c:14553 reversed, :14555 forwards), so a difference between the first two
      lines is about the bound and not about what the pattern means. The verb-free line is printed
      and is NOT a control: deleting a verb prunes nothing, so it may reach a match neither pruned
      spelling can.

  uncapped anchor sweep
      Every anchor in the searched region, printed in the order upstream's own search tries them -
      ascending `pos` forwards, DESCENDING `endpos` reversed, because a reversed match is anchored
      by its end. The answer a search owes is the first line of this list.

  reachability grid
      Every span upstream's own matcher produces over any (pos, endpos) pair in the region, so
      "upstream answered a span its own matcher cannot make" is settled over the whole region
      rather than at the one point `record-oracle.py`'s `searchOnlyPartial` field asks about.

Run it. The rows and the two RECORDED columns are read out of `TestResults/oracle/`, which is
gitignored, so on a tree that has never run the gate those two seeds are regenerated first - both
in one command, because seed 20260920 is only a default on the day it names:

    pwsh -File tools/run-oracle.ps1 -Seeds 20260920,31337
    python tools/probes/upstream-skip-partial-anchor-grid.py

Written by S60 sitting 3 against regex 2026.9.10.
"""

import json
import re
import sys
from pathlib import Path

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

REPO_ROOT = Path(__file__).resolve().parents[2]
REPORTS = REPO_ROOT / "TestResults" / "oracle"

# (seed, row number as the gate report names it).
WANTED = ((20260920, 5014), (31337, 3633), (31337, 5200))

REVERSE = 0x400
CALL_TIMEOUT = 5.0

# `verbs` and `partial-sliced` are recorded with upstream's required-string prefilter neutralised
# (PREFILTER_FREE_GENERATORS, tools/record-oracle.py:325), so a probe has to ask the same way or it
# is not reproducing the row at all.
PREFILTER_FREE = ("verbs", "partial-sliced")
_REQ_OFFSET_ARG, _REQ_CHARS_ARG = 7, 8
_inner = regex._regex.compile


def _without_required_string(*args):
    args = list(args)
    args[_REQ_OFFSET_ARG] = -1
    args[_REQ_CHARS_ARG] = None
    return _inner(*args)


def compile_row(row: dict, pattern: str | None = None):
    pattern = row["pattern"] if pattern is None else pattern
    lists = row.get("namedLists") or {}
    if row.get("generator", "rows") not in PREFILTER_FREE:
        return regex.compile(pattern, row.get("flags", 0), cache_pattern=False, **lists)
    regex._regex.compile = _without_required_string
    try:
        return regex.compile(pattern, row.get("flags", 0), cache_pattern=False, **lists)
    finally:
        regex._regex.compile = _inner


def describe(m) -> str:
    """One answer in CODEPOINTS, which is the unit upstream counts in."""
    if m is None:
        return "None"
    bits = [str(m.span())]
    for n in range(1, m.re.groups + 1):
        bits.append(f"g{n}={m.span(n)}" if m.span(n) != (-1, -1) else f"g{n}=unset")
    bits.append("PARTIAL" if m.partial else "not-partial")
    if any(m.fuzzy_counts):
        bits.append(f"fuzzy={m.fuzzy_counts}")
    return " ".join(bits)


def slice_of(row: dict) -> tuple[int, int]:
    """The row's own `pos`/`endpos` IN CODEPOINTS, which is the unit upstream counts in."""
    sliced = row.get("codepointSlice")
    if sliced:
        return tuple(sliced)
    pos, endpos = row.get("pos"), row.get("endpos")
    if pos is None and endpos is None:
        return (0, len(row["subject"]))
    return (0 if pos is None else pos, len(row["subject"]) if endpos is None else endpos)


def is_reversed(row: dict) -> bool:
    return "(?r" in row["pattern"] or bool(row.get("flags", 0) & REVERSE)


def search_answer(row: dict, pattern: str | None = None) -> str:
    compiled = compile_row(row, pattern)
    lo, hi = slice_of(row)
    return describe(compiled.search(row["subject"], lo, hi, partial=True, timeout=CALL_TIMEOUT))


def prune_spelling(pattern: str) -> str:
    return pattern.replace("(*SKIP)", "(*PRUNE)")


def verb_free(pattern: str) -> str:
    return pattern.replace("(*SKIP)", "")


def uncapped_sweep(row: dict) -> list[str]:
    """Every anchor that answers, in the order upstream's own search tries them."""
    compiled = compile_row(row)
    subject = row["subject"]
    lo, hi = slice_of(row)
    reverse = is_reversed(row)
    anchors = range(hi, lo - 1, -1) if reverse else range(lo, hi + 1)
    lines = []
    for p in anchors:
        m = (compiled.match(subject, lo, p, partial=True, timeout=CALL_TIMEOUT) if reverse
             else compiled.match(subject, p, hi, partial=True, timeout=CALL_TIMEOUT))
        if m is not None:
            lines.append(f"{'endpos' if reverse else 'pos'}={p} {describe(m)}")
    return lines or ["never answers at any anchor"]


def reachability_grid(row: dict) -> list[str]:
    """Every span upstream's own matcher makes over any (pos, endpos) pair in the region."""
    compiled = compile_row(row)
    subject = row["subject"]
    lo, hi = slice_of(row)
    seen: dict[tuple, list[str]] = {}
    for start in range(lo, hi + 1):
        for end in range(start, hi + 1):
            m = compiled.match(subject, start, end, partial=True, timeout=CALL_TIMEOUT)
            if m is not None:
                seen.setdefault((m.span(), m.partial), []).append(f"({start},{end})")
    return [f"{span} {'PARTIAL' if partial else 'not-partial'}  from {', '.join(anchors)}"
            for (span, partial), anchors in sorted(seen.items(), key=lambda kv: kv[0][0])]


def dollar_positions(row: dict, spelling: str = "$") -> list[int]:
    """Where upstream's own `$` is true, asked one anchored position at a time.

    `end-of-line-reads-a-skip-moved-slice`'s whole tell: `try_match_END_OF_LINE`
    (upstream/src/_regex.c:7110) reads `slice_end` where its seven sibling edge predicates read a
    text bound, and `RE_OP_SKIP` under `(?r)` writes that very field (:14553), so an answer that
    ENDS where upstream's own `$` is false is a bound the verb moved being read as a line end.

    THE REVERSE BIT IS MASKED OFF, because a reversed `match` is anchored by its `endpos` and not
    by its `pos`, so asking position by position with `endpos=len(subject)` would answer at EVERY
    position and say nothing. Measured 2026-09-20 on regex 2026.9.10, 'ab<LF>cd' under MULTILINE:
    `[2, 5]`, and under MULTILINE|REVERSE `[0, 1, 2, 3, 4, 5]`
    (`tools/probes/upstream-dollar-positions-for-moved-slice-rows.py`, which prints both). The mask
    changes no number this probe has already printed, because none of the three rows carries REVERSE
    in its FLAGS - 0x400a, 0x108 and 0x8, and 0x400 is clear in all three. Row 3633 is reversed, but
    it spells `(?r)` in its PATTERN, and what is compiled here is the spelling (`$` or `(?w)$`) with
    the row's flags, never the row's pattern, so the inline verb cannot reach this call. The mask is
    defensive: no generator sets the flag today either - 0 of the 6,380 rows of `wave-31337.jsonl`
    have 0x400 set (counted 2026-09-20) - and it is here so that a hand-written row, or a generator
    that starts passing the flag, cannot turn this sweep into an all-positions answer unnoticed.
    """
    compiled = regex.compile(spelling, row.get("flags", 0) & ~REVERSE, cache_pattern=False)
    subject = row["subject"]
    return [p for p in range(len(subject) + 1)
            if compiled.match(subject, p, len(subject), timeout=CALL_TIMEOUT) is not None]


def read_report(seed: int) -> dict[int, tuple[str, str]]:
    """Each judged row's recorded upstream and port answer, out of the gate's own report.

    Both prefixes, because a row changes prefix the moment it is pinned: an unjudged row heads
    its block `DIVERGE row 3633 ...`, and once `ExpectedDivergences` claims it the same block
    reads `EXPECTED end-of-line-reads-a-skip-moved-slice row 3633 ...`. Reading only `DIVERGE`
    made this probe print NOT RECORDED for every row the slice had already pinned - which is
    every row it is here to explain. Measured 2026-09-20 on `report-31337.txt`, written by that
    morning's gate run: `grep -n 3633` finds the EXPECTED heading at line 23 and no DIVERGE one.
    The pinned form carries the ledger entry's name between the prefix and `row`, which is why
    the number is matched rather than read from a fixed field.
    """
    report = REPORTS / f"report-{seed}.txt"
    answers: dict[int, tuple[str, str]] = {}
    if not report.exists():
        return answers
    lines = report.read_text(encoding="utf-8", errors="replace").splitlines()
    for i, line in enumerate(lines):
        heading = re.match(r"(?:DIVERGE|EXPECTED) (?:\S+ )?row (\d+) ", line)
        if heading is None:
            continue
        number = int(heading.group(1))
        block = lines[i:i + 8]

        def first(prefix: str) -> str:
            return next((b.strip()[len(prefix):].strip() for b in block
                         if b.strip().startswith(prefix)), "?")

        answers[number] = (first("upstream"), first("port"))
    return answers


def read_wave_row(seed: int, number: int) -> dict | None:
    """One row of `wave-<seed>.jsonl`. The wave carries a HEADER line, so row N is line N + 1."""
    wave = REPORTS / f"wave-{seed}.jsonl"
    if not wave.exists():
        return None
    with wave.open(encoding="utf-8") as handle:
        for index, line in enumerate(handle):
            if index == number:
                return json.loads(line)
    return None


def main() -> int:
    print(f"regex {regex.__version__}")
    for seed, number in WANTED:
        row = read_wave_row(seed, number)
        if row is None:
            print(f"\n--- seed {seed} row {number}: no wave-{seed}.jsonl - regenerate it with "
                  f"`pwsh -File tools/run-oracle.ps1 -Seeds {seed}`")
            continue
        missing = f"NOT RECORDED - `pwsh -File tools/run-oracle.ps1 -Seeds {seed}` writes it"
        recorded = read_report(seed).get(number, (missing, missing))
        lo, hi = slice_of(row)
        print(f"\n--- seed {seed} row {number}  {row.get('generator')}  {row['operation']}"
              f"  flags=0x{row.get('flags', 0):x}{'  REVERSED' if is_reversed(row) else ''}")
        print(f"    pattern            {row['pattern']!r}")
        print(f"    subject            {row['subject']!r}   region (codepoints) ({lo}, {hi})")
        print(f"    searchOnlyPartial  {row.get('searchOnlyPartial')}")
        print(f"    UPSTREAM (recorded)  {recorded[0]}")
        print(f"    PORT     (recorded)  {recorded[1]}")
        print(f"    as drawn           {search_answer(row)!r}")
        print(f"    (*SKIP)->(*PRUNE)  {search_answer(row, prune_spelling(row['pattern']))!r}")
        print(f"    verb deleted       {search_answer(row, verb_free(row['pattern']))!r}")
        print("    uncapped sweep, in the order its own search tries them:")
        for line in uncapped_sweep(row):
            print(f"        {line}")
        print("    reachability grid:")
        for line in reachability_grid(row):
            print(f"        {line}")
        if "$" in row["pattern"]:
            # The `$` controls, for a row whose answer might END on a bound the verb moved rather
            # than on a line end. `(?w)$` is the twin that reads `text_end`
            # (regex/_regex_core.py:506-510); the written-out spelling is `$`'s own definition with
            # no bound in it at all.
            written_out = row["pattern"].replace("$", r"(?:(?=\n)|(?!\n|.))")
            twin = "(?w)" + row["pattern"]
            print(f"    `$` true at        {dollar_positions(row)}"
                  f"   (?w)$ at {dollar_positions(row, '(?w)$')}")
            print(f"    `$` written out    {search_answer(row, written_out)!r}")
            print(f"    `$` as (?w)$       {search_answer(row, twin)!r}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
