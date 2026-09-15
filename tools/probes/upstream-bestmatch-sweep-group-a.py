r"""The four sweep rows that look like ledger entry 12 and are not it.

The eight-seed seed sweep drew eight rows with one signature: upstream answers `None`,
and with the `(?b)` deleted it answers what this port answers. S52 sitting 16 separated
them with a PORT-SIDE control rather than by eye - restoring upstream's doubled term in
`Matcher.cs`'s `END_FUZZY` backtrack arm (the line S46 dropped) makes this port refuse
four of the eight and leaves the other four answering, so only those four are entry 12.

This probe asks upstream about the other four: rows 4, 15, 18 and 35 of
`tools/probes/sweep-divergence-rows.jsonl`. NOTHING IS TRANSCRIBED - the rows are read
out of that committed file by index, so a row cannot drift from the sweep that drew it.

Run it:

    python tools/probes/upstream-bestmatch-sweep-group-a.py

Expected on regex 2026.9.10 (measured 2026-09-15):

  rows 18 and 35 - POSIX AND BESTMATCH TOGETHER. `None` as drawn; the SAME match back
      when either the `(?b)` or the POSIX bit is taken away, and neither alone destroys
      it. Ledger entry 16's conjunction on a plain anchored call rather than on an
      overlapped scan. A second port-side control names the same place S48b fixed here:
      deleting the two running-total lines from `RestoreBestMatch` makes this port lose
      exactly these two rows of the eight.

  rows 4 and 15 - OPEN. `(?b)` alone destroys them: clearing POSIX on row 4 leaves
      `None`, and row 15 carries no POSIX at all. Both are reversed `(?r)` partial
      searches whose flagless answer is a PARTIAL, which is ledger entry 13's shape, and
      neither port-side control moves either. Not judged.

A POSIX row's `fuzzy_changes` is not read anywhere here: reading it on the wrong POSIX
match is an access violation rather than an exception (ledger entry 9), which killed
`gate-divergence-doors.py` mid-run before S52 sitting 15 guarded it.
"""

import json
import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

ROWS_FILE = "tools/probes/sweep-divergence-rows.jsonl"

# File indices, 1-based, as the sweep file numbers them. Each row's own `comment` names
# the sweep directory and the wave row it came from, and is printed below.
WANTED = (4, 15, 18, 35)


def describe(m) -> str:
    """One answer, WITHOUT change positions - see the docstring's last paragraph."""
    if m is None:
        return "None"
    bits = [str(m.span())]
    if m.re.groups:
        bits.append(" ".join(f"g{g}={m.span(g)}" for g in range(1, m.re.groups + 1)))
    bits.append("PARTIAL" if m.partial else "complete")
    if any(m.fuzzy_counts):
        bits.append(f"counts={m.fuzzy_counts}")
    return "  ".join(bits)


def ask(label: str, pattern: str, row: dict, flags: int | None = None) -> None:
    lists = {name: sorted(v) for name, v in sorted((row["namedLists"] or {}).items())}
    kwargs = {"partial": True} if row.get("partial") else {}
    try:
        compiled = regex.compile(pattern, row["flags"] if flags is None else flags, **lists)
        answer = getattr(compiled, row["operation"])(row["subject"], **kwargs)
    except Exception as exc:  # noqa: BLE001 - a rejected ablation is data, not a failure
        print(f"    {label:<26} EXC {exc!r}")
        return
    print(f"    {label:<26} {describe(answer)}")


if __name__ == "__main__":
    print("regex", regex.__version__)

    rows = {}
    with open(ROWS_FILE, encoding="utf-8") as handle:
        for index, line in enumerate(handle, 1):
            rows[index] = json.loads(line)

    for index in WANTED:
        row = rows[index]
        pattern = row["pattern"]
        print()
        print(
            f"--- {ROWS_FILE} row {index}  {row['generator']} {row['operation']}"
            f"  flags={row['flags']:#x}  {row['comment']}"
        )
        print(f"    pattern {ascii(pattern)}")
        print(f"    subject {ascii(row['subject'])}")
        if row["namedLists"]:
            print(f"    lists   {ascii(row['namedLists'])}")

        ask("as drawn", pattern, row)
        ask("no (?b)", pattern.replace("(?b)", "", 1), row)
        if "(?e)" in pattern:
            ask("no (?e)", pattern.replace("(?e)", "", 1), row)
        if "(?p)" in pattern:
            ask("no (?p)", pattern.replace("(?p)", "", 1), row)
        if row["flags"] & regex.POSIX:
            ask("POSIX flag cleared", pattern, row, row["flags"] & ~regex.POSIX)
        if "(?r)" in pattern:
            ask("no (?r)", pattern.replace("(?r)", "", 1), row)
        if "(*SKIP)" in pattern:
            ask("(*SKIP)->(*PRUNE)", pattern.replace("(*SKIP)", "(*PRUNE)"), row)
            ask("verb deleted", pattern.replace("(*SKIP)", ""), row)
