r"""The six partial-search rows of the seed sweep, and the instrument that splits them 3/3.

S52 sitting 15 grouped the sweep's 37 diverging rows and put six of them in group D - "partial
`search`, `search-start-partial` shape" - with the warning that a shape is not a classification.
It is not: the six are TWO families, and which one a row belongs to is decided by a question
neither the doors probe nor the recorder's `searchOnlyPartial` field asks.

THE TWO QUESTIONS THIS PROBE ADDS.

  1. THE UNCAPPED ANCHOR SWEEP, and the anchor upstream's own search is defined to try FIRST.
     A forward search tries the lowest `pos` first, a reversed one the highest `endpos` (it is
     anchored by its end), so the answer a search OWES is the first anchor at which its own
     `match(..., partial=True)` answers at all. `gate-divergence-doors.py` stops the sweep after
     three hits, which is the leftmost for a forward row but NOT the highest for a reversed one -
     on row 24 the capped sweep's last line is the first anchor, and reading it as the third-best
     would invert the argument. This sweep runs the whole region.

  2. REACHABILITY: is upstream's own search answer producible by ANY anchored call at all?
     `searchOnlyPartial` (tools/record-oracle.py:947) asks the single question "does `match` over
     the span the search reported answer the same partial there". That is one cell of a grid. This
     probe walks every (pos, endpos) pair in the searched region and collects every span upstream's
     own matcher will produce, so "upstream answered something its matcher cannot" is established
     over the whole region rather than at one point.

WHAT THE TWO QUESTIONS SEPARATE, measured 2026-09-15 on regex 2026.9.10:

  rows 25, 29, 36 -> `search-start-partial`. Upstream's search answers a span **no anchored call
      anywhere in the region produces** - (0,5), (0,5) and (0,2) against matcher grids that do not
      contain them. Its search door and its match door disagree, which is that entry's whole
      signature, and the recorded `searchOnlyPartial` is `true` on each.

  rows 13, 22 -> `partial-retry-carried-slice-forward`, and row 24 -> `partial-retry-reversed-
      slice`. Upstream's answer here IS anchored-reachable - it is a span its own matcher makes -
      but not at the anchor its own search must try first, and the anchor it must try first answers
      exactly what this port answers. A bound skipped the anchors in between. `searchOnlyPartial`
      is `false` on all three, which is the recorded field saying the same thing at one point.

ON ALL SIX the `(*PRUNE)` spelling answers this port's answer, span, groups and partial flag.
`(*PRUNE)` prunes the backtracking `(*SKIP)` prunes and moves no slice bound
(upstream/src/_regex.c:14553 reversed, :14555 forwards), so the bound move is the cause rather
than what the pattern means - the argument all three entries rest on. The VERB-FREE spelling is
printed too and it is NOT the control: deleting the verb prunes nothing, so it may legitimately
reach a complete match the pruned spellings cannot, and on rows 13, 22 and 24 it does.

A negative result worth keeping, because the obvious reading of `search-start-partial`'s name is
wrong here: neutralising upstream's REQUIRED-STRING prefilter (the recorder's own
PREFILTER_FREE treatment, tools/record-oracle.py:325) changes not one of the six answers. Whatever
in `search_start` (:8385) answers rows 25, 29 and 36, it is not the required string.

NOTHING IS TRANSCRIBED - the rows are read out of `tools/probes/sweep-divergence-rows.jsonl` by
index, so a row cannot drift from the sweep that drew it. Each row's own `comment` names the sweep
directory and wave row it came from and is printed below.

Run it:

    python tools/probes/upstream-partial-anchor-reachability.py
"""

import json
import sys
from pathlib import Path

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

REPO_ROOT = Path(__file__).resolve().parents[2]
ROWS_FILE = REPO_ROOT / "tools" / "probes" / "sweep-divergence-rows.jsonl"

# File indices, 1-based, as the sweep file numbers them.
WANTED = (13, 22, 24, 25, 29, 36)

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


def compile_row(row: dict, pattern: str | None = None, prefilter_free: bool | None = None):
    pattern = row["pattern"] if pattern is None else pattern
    lists = row.get("namedLists") or {}
    if prefilter_free is None:
        prefilter_free = row.get("generator", "rows") in PREFILTER_FREE
    if not prefilter_free:
        return regex.compile(pattern, row.get("flags", 0), cache_pattern=False, **lists)
    regex._regex.compile = _without_required_string
    try:
        return regex.compile(pattern, row.get("flags", 0), cache_pattern=False, **lists)
    finally:
        regex._regex.compile = _inner


def describe(m) -> str:
    """One answer in CODEPOINTS, which is the unit upstream counts in.

    No `fuzzy_changes` is read anywhere here: on a POSIX match that spent an error reading it is an
    access violation rather than an exception (ledger entry 9), which killed `gate-divergence-
    doors.py` mid-run before S52 sitting 15 guarded it. None of these six carries POSIX, and the
    guard is not needed rather than not wanted - the counts are printed, which is what a fuzzy row
    here would show.
    """
    if m is None:
        return "None"
    bits = [str(m.span())]
    for n in range(1, m.re.groups + 1):
        bits.append(f"g{n}={m.span(n)}" if m.span(n) != (-1, -1) else f"g{n}=unset")
    bits.append("PARTIAL" if m.partial else "not-partial")
    if any(m.fuzzy_counts):
        bits.append(f"counts={m.fuzzy_counts}")
    return " ".join(bits)


def slice_of(row: dict) -> tuple[int, int]:
    """The row's own searched region IN CODEPOINTS.

    `partial-sliced` draws a slice and the recorder writes it twice - `pos`/`endpos` in UTF-16 for
    this port and `codepointSlice` for upstream. Row 29 is one, so asking the whole subject instead
    would be asking a different question of the one row in this set that carries a slice.
    """
    sliced = row.get("codepointSlice")
    if sliced:
        return tuple(sliced)
    return (0, len(row["subject"]))


def search_answer(row: dict, pattern: str | None = None, prefilter_free: bool | None = None) -> str:
    compiled = compile_row(row, pattern, prefilter_free)
    lo, hi = slice_of(row)
    return describe(compiled.search(row["subject"], lo, hi, partial=True, timeout=CALL_TIMEOUT))


def anchor_sweep(row: dict) -> tuple[list[tuple[int, str]], bool]:
    """Every anchor in the region at which upstream's own `match(partial=True)` answers.

    UNCAPPED, unlike `gate-divergence-doors.py`'s, and that is the whole point for a reversed row:
    a reversed match anchors at the END, so the anchor its search tries first is the HIGHEST endpos
    that answers, and a sweep that stops after three hits has only found the three lowest.
    """
    compiled = compile_row(row)
    subject = row["subject"]
    lo, hi = slice_of(row)
    reverse = "(?r" in row["pattern"] or bool(row.get("flags", 0) & REVERSE)
    hits = []
    for p in range(lo, hi + 1):
        m = (compiled.match(subject, lo, p, partial=True, timeout=CALL_TIMEOUT) if reverse
             else compiled.match(subject, p, hi, partial=True, timeout=CALL_TIMEOUT))
        if m is not None:
            hits.append((p, describe(m)))
    return hits, reverse


def reachable_spans(row: dict) -> set[tuple[int, int]]:
    """Every span upstream's own anchored matcher produces anywhere in the searched region."""
    compiled = compile_row(row)
    subject = row["subject"]
    lo, hi = slice_of(row)
    spans = set()
    for p in range(lo, hi + 1):
        for q in range(p, hi + 1):
            m = compiled.match(subject, p, q, partial=True, timeout=CALL_TIMEOUT)
            if m is not None:
                spans.add(m.span())
    return spans


def main() -> int:
    print("regex", regex.__version__)
    rows = [json.loads(line) for line in
            ROWS_FILE.read_text(encoding="utf-8").splitlines() if line.strip()]

    for number in WANTED:
        row = rows[number - 1]
        pattern, subject = row["pattern"], row["subject"]
        lo, hi = slice_of(row)
        drawn = compile_row(row).search(subject, lo, hi, partial=True, timeout=CALL_TIMEOUT)

        print(f"\n=== sweep row {number}  {row.get('generator')}  {row['operation']} partial"
              f"  flags={row.get('flags', 0):#x}  [{row.get('comment', '')}]")
        print("    pattern              " + ascii(pattern))
        print("    subject              " + ascii(subject))
        print(f"    searched region      ({lo}, {hi})  of {len(subject)} codepoints")
        print(f"    searchOnlyPartial    {row.get('searchOnlyPartial')}   <- as the recorder wrote it")
        print("    as drawn             " + describe(drawn))

        if "(*SKIP)" in pattern:
            print("    (*SKIP)->(*PRUNE)    "
                  + search_answer(row, pattern.replace("(*SKIP)", "(*PRUNE)"))
                  + "   <- the control: same pruning, no bound moved")
            print("    verb deleted         "
                  + search_answer(row, pattern.replace("(*SKIP)", ""))
                  + "   <- NOT a control: deleting the verb prunes nothing")

        # The required string is one part of `search_start` and it is not the part that answers
        # here. Printed because "prefilter" invites the assumption that it is.
        print("    required string gone " + search_answer(row, prefilter_free=True))

        hits, reverse = anchor_sweep(row)
        bound = "endpos" if reverse else "pos"
        print(f"    anchor sweep on {bound}, UNCAPPED ({len(hits)} answer):")
        for p, text in hits:
            print(f"        {bound}={p}  {text}")
        if hits:
            first = hits[-1] if reverse else hits[0]
            print(f"    first anchor a {'reversed' if reverse else 'forward'} search tries that answers"
                  f":  {bound}={first[0]}  {first[1]}")

        spans = reachable_spans(row)
        target = drawn.span()
        print(f"    anchored spans in the region  {sorted(spans)}")
        print(f"    upstream's own search answer {target} is "
              + ("REACHABLE by an anchored call - so the matcher made it, at the wrong anchor"
                 if target in spans else
                 "NOT REACHABLE by any anchored call in the region - the matcher cannot make it"))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
