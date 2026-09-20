r"""One door, put to every reversed partial row asked over a non-zero slice: does this port's
answer over the SLICE equal upstream's own answer over the CUT SUBJECT, offset back?

WHY THIS IS THE DOOR. The owner ruled on 2026-09-15 (ledger 24, Option B, slice S52d) that for a
reversed match asked with `partial=True` the text has run out when the match reaches `pos`. The
whole argument for that ruling is that a slice start behaves like a string start, so the ruling has
a falsifiable consequence: the slice `[pos, endpos)` of a subject must answer exactly what the same
pattern answers over `subject[pos:endpos]` as a subject in its own right, shifted by `pos`. This
probe asks that of every row, and prints SAME or DIFFERENT. It renders no verdict.

Upstream cannot answer the sliced call that way, because `init_match` sets `text_start` to 0
(`upstream/src/_regex.c:18442`) and every node handler reads it, so upstream's own answer over a
non-zero slice comes from a later door: its search retreats until `search_start` (`:8400-8405`)
reports a partial positioned at `slice_start`, and `:18185-18190` then overwrites the match's text
position with `slice_start`. So upstream's span is `(slice_start, match_pos)` of whichever attempt
happened to be current, and the port's is the one the ruling names.

Run it after a gate run::

    pwsh -File tools/run-oracle.ps1 -Count 6000        # writes report-<seed>.txt and wave-<seed>.jsonl
    python tools/probes/s57b-cut-subject-door.py       # the default seeds
    python tools/probes/s57b-cut-subject-door.py 7 4242 20260920

Nothing is transcribed: the row comes out of `wave-<seed>.jsonl` and this port's answer out of
`report-<seed>.txt`, exactly as `gate-divergence-doors.py` does it. DIVERGE rows are read, and so
are the rows the two slice-start entries already account for, because the point of the door is to
say whether ONE rule covers both.

Written by S57b sitting 2 against regex 2026.9.10.
"""

import importlib.util
import re
import sys
from pathlib import Path

# The sibling probe owns the compile-with-the-prefilter-off dance and the report and wave readers,
# and reusing them is how this probe asks upstream the same question the recorder asked. Its name
# carries hyphens, so it is loaded by path rather than imported.
_spec = importlib.util.spec_from_file_location(
    "gate_divergence_doors", Path(__file__).resolve().parent / "gate-divergence-doors.py"
)
_doors = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_doors)
REPORTS, compile_row = _doors.REPORTS, _doors.compile_row
default_seeds, read_wave_rows, slice_of = _doors.default_seeds, _doors.read_wave_rows, _doors.slice_of

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

PINNED = frozenset(
    {"reversed-partial-runs-out-at-the-slice-start", "reversed-partial-answers-the-cut-subject"}
)
HEAD = re.compile(r"^(DIVERGE|EXPECTED) (?:(\S+) )?row (\d+) \((\S+)\) (\S+) flags=")
GROUP = re.compile(r"(\d+):(?:\((-?\d+),(-?\d+)\)|unset)")


def utf16_offsets(subject: str) -> list[int]:
    """`offsets[i]` is the UTF-16 index of codepoint `i`; the list runs one past the end."""
    offsets, at = [], 0
    for ch in subject:
        offsets.append(at)
        at += 2 if ord(ch) > 0xFFFF else 1
    offsets.append(at)
    return offsets


def port_answer(port_line: str) -> tuple[dict[int, tuple[int, int]] | None, bool]:
    """This port's groups as `{number: (start, end)}` in UTF-16, and its partial flag.

    The report writes a group as `n:(index,length)` and an unset one as `n:unset`, so the spans have
    to be rebuilt from index and LENGTH. `None` means the port answered no match.
    """
    if port_line.startswith("no match"):
        return None, False
    body = port_line.split("last=")[0]
    spans: dict[int, tuple[int, int]] = {}
    for number, index, length in GROUP.findall(body):
        if index:
            spans[int(number)] = (int(index), int(index) + int(length))
    return spans, " partial" in port_line


def cut_answer(row: dict) -> tuple[dict[int, tuple[int, int]] | None, bool, str]:
    """Upstream's answer over `subject[pos:endpos]` as its own subject, in the FULL subject's UTF-16.

    The shift is done in codepoints, which is upstream's unit, and converted afterwards, because a
    slice that cuts astral characters out makes the two subjects' UTF-16 offsets differ.
    """
    lo, hi = slice_of(row)
    compiled = compile_row(row)
    match = getattr(compiled, row["operation"])(
        row["subject"][lo:hi], 0, hi - lo, partial=True, timeout=5.0
    )
    if match is None:
        return None, False, "None"
    offsets = utf16_offsets(row["subject"])
    spans, shown = {}, []
    for number in range(0, (compiled.groups or 0) + 1):
        start, end = match.span(number)
        if start < 0:
            shown.append(f"{number}:unset")
            continue
        spans[number] = (offsets[start + lo], offsets[end + lo])
        shown.append(f"{number}:({start + lo},{end + lo})")
    tail = " partial" if match.partial else ""
    return spans, match.partial, " ".join(shown) + tail


def main(argv: list[str] | None = None) -> int:
    seeds = [int(a) for a in (argv or sys.argv[1:])] or default_seeds()
    totals = {"SAME": 0, "DIFFERENT": 0, "COULD NOT ASK": 0}

    for seed in seeds:
        report = REPORTS / f"report-{seed}.txt"
        if not report.exists():
            print(f"\n=== seed {seed}: no report on disk")
            continue

        lines = report.read_text(encoding="utf-8", errors="replace").splitlines()
        starts = [i for i, line in enumerate(lines) if HEAD.match(line)]
        wanted: list[tuple[int, str, str, str]] = []
        for n, start in enumerate(starts):
            head = HEAD.match(lines[start])
            kind, entry = head.group(1), head.group(2) or ""
            if kind == "EXPECTED" and entry not in PINNED:
                continue
            block = lines[start + 1 : starts[n + 1] if n + 1 < len(starts) else len(lines)]
            port = next(
                (x.strip()[len("port ") :].strip() for x in block if x.strip().startswith("port ")),
                "",
            )
            wanted.append((int(head.group(3)), kind, entry, port))

        rows = read_wave_rows(seed, [n for n, _, _, _ in wanted])
        print(f"\n=== seed {seed}: {len(wanted)} reversed-partial candidates in the report")
        for number, kind, entry, port in wanted:
            row = rows.get(number)
            if row is None:
                continue
            lo, _ = slice_of(row)
            if not (row.get("partial") and lo > 0 and "(?r" in row["pattern"]):
                continue

            ours, ours_partial = port_answer(port)
            try:
                theirs, theirs_partial, shown = cut_answer(row)
            except Exception as e:  # noqa: BLE001 - an exception IS upstream's answer on some rows
                totals["COULD NOT ASK"] += 1
                print(f"  row {number:6}  COULD NOT ASK  {type(e).__name__}: {e}")
                continue

            same = ours == theirs and ours_partial == theirs_partial
            totals["SAME" if same else "DIFFERENT"] += 1
            label = f"{kind}{(' ' + entry) if entry else ''}"
            print(f"  row {number:6}  {'SAME     ' if same else 'DIFFERENT'}  {label}")
            if not same:
                print(f"           port        {port}")
                print(f"           cut subject {shown}")

    print(
        f"\n{totals['SAME']} SAME, {totals['DIFFERENT']} DIFFERENT, "
        f"{totals['COULD NOT ASK']} COULD NOT ASK"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
