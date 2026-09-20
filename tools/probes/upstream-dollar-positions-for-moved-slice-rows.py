r"""Where upstream's own `$` is true on every row of `end-of-line-reads-a-skip-moved-slice`.

That entry's whole tell is that upstream's answer ENDS where its own `$` is false, and its sharpest
control is the `(?w)` twin: `(?w)` compiles `$` to `END_OF_LINE_U` (regex/_regex_core.py:506-510),
which reads `text_end` rather than the `slice_end` a reversed `(*SKIP)` writes
(upstream/src/_regex.c:14553, read at :7110). The control can only RUN on a row where the phantom
end - the position upstream's answer ends at - is NOT also a `(?w)$` position, because otherwise a
`(?w)` run that stops answering cannot tell a stale bound from a line end the twin created.

Six rows now carry that judgement and the counts were stated row by row as each landed, so this
asks all six in one run instead. Nothing is transcribed: the rows are read out of the committed
`_endOfLineReadsMovedSliceRows` literal in
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`, so no wave file and no gate run is needed.

    python tools/probes/upstream-dollar-positions-for-moved-slice-rows.py

It also pins the one defect this probe was written to settle, in `REVERSE_DEMONSTRATION` below:
asking `$` with the REVERSE bit in the flags answers at EVERY position, because a reversed `match`
is anchored by its `endpos` and not by its `pos`. `upstream-skip-partial-anchor-grid.py`'s
`dollar_positions` masks that bit off for this reason.

Written by S60 sitting 4 against regex 2026.9.10.
"""

import json
import re
import sys
from pathlib import Path

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

REPO_ROOT = Path(__file__).resolve().parents[2]
LEDGER = REPO_ROOT / "tests" / "FuzzyRegex.OracleTests" / "ExpectedDivergences.cs"
LITERAL = "_endOfLineReadsMovedSliceRows"

REVERSE = 0x400
MULTILINE = 0x8
CALL_TIMEOUT = 5.0


def ledger_rows() -> list[dict]:
    """The entry's own rows, out of the committed raw-string literal."""
    text = LEDGER.read_text(encoding="utf-8")
    body = re.search(rf'{LITERAL} = """(.*?)""";', text, re.S)
    if body is None:
        raise SystemExit(f"{LITERAL} not found in {LEDGER}")
    return [json.loads(line) for line in body.group(1).splitlines() if line.strip()]


def codepoints(text: str) -> str:
    """C# writes UTF-16, so a JSON row carries surrogate PAIRS; Python counts codepoints."""
    return text.encode("utf-16", "surrogatepass").decode("utf-16")


def dollar_positions(subject: str, flags: int, spelling: str = "$") -> list[int]:
    """Every position where upstream's own `$` is true, asked one anchored position at a time."""
    compiled = regex.compile(spelling, flags & ~REVERSE, cache_pattern=False)
    return [p for p in range(len(subject) + 1)
            if compiled.match(subject, p, len(subject), timeout=CALL_TIMEOUT) is not None]


def reverse_demonstration() -> list[str]:
    """Two runs differing in ONE parameter: the REVERSE bit, and nothing else."""
    subject = "ab\ncd"
    lines = []
    for label, flags in (("MULTILINE", MULTILINE), ("MULTILINE|REVERSE", MULTILINE | REVERSE)):
        compiled = regex.compile("$", flags, cache_pattern=False)
        answered = [p for p in range(len(subject) + 1)
                    if compiled.match(subject, p, len(subject), timeout=CALL_TIMEOUT) is not None]
        lines.append(f"    {label:<18} {subject!r} answers at {answered}")
    return lines


def main() -> int:
    print(f"regex {regex.__version__}")
    for number, row in enumerate(ledger_rows(), start=1):
        subject = codepoints(row["subject"])
        flags = row.get("flags", 0)
        print(f"\n--- row {number}  {row.get('generator')}  {row['operation']}  flags=0x{flags:x}")
        print(f"    pattern      {row['pattern']!r}")
        print(f"    subject      {subject!r}  ({len(subject)} codepoints)")
        print(f"    `$` true at  {dollar_positions(subject, flags)}"
              f"   `(?w)$` true at {dollar_positions(subject, flags, '(?w)$')}")
    print("\n--- the REVERSE bit, asked both ways (this probe's own control):")
    for line in reverse_demonstration():
        print(line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
