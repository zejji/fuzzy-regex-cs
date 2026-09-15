"""The negative control for the four `*-long` generators: is the padding-SIDE rule load-bearing?

`tools/record-oracle.py`'s `_generate_long` puts the filler on the side the pattern does not run
off - a forward row is padded on the LEFT so the scan has text to walk before it can match, and a
reversed one is padded on the RIGHT because it reads right to left and runs out at the left end.
Its docstring says padding the wrong side makes a `partial-long` row "just a `literals` row with a
long prefix". This probe restores that fault and measures what actually moves.

THE CONTROL: rewrite the padding line so every row is padded on the left regardless of direction,
record the same seed both ways, and compare the reversed rows - the only ones the rule can reach.
The file is restored in a `finally`, so an interrupted run does not leave the recorder mutated.

WHAT IT MEASURES, and why the obvious column is the wrong one. Sitting 4 of S52 predicted the
control would collapse `partial-long`'s `walked` column. It does not, and the aggregate table in
`generator-long-subject-reach.py` barely moves at all, because two of the four generators draw NO
reversed row (0 of 200 each at seed 7, against 90 of 200 for `partial-long` and 31 for
`fuzzy-long`) and averaging a rule in with the rows it cannot reach hides it. Split by direction:

  seed 7                        baseline -> control
  fuzzy-long   reversed  walked      21 -> 1     spans starting at text 0  17 -> 1
  partial-long reversed  walked      27 -> 24    spans starting at text 0  29 -> 26

  seed 31337                    baseline -> control
  fuzzy-long   reversed  walked      22 -> 3     spans starting at text 0  16 -> 3
  partial-long reversed  walked      19 -> 10    spans starting at text 0  14 -> 13

So the side rule IS load-bearing, and `fuzzy-long` is where it shows: padded on the wrong side its
reversed rows match at once in the original subject now sitting at the right-hand end, and the
`spans ending at the text end` column rises 4 -> 20 and 9 -> 23 to say exactly that.

It is NOT load-bearing for reversed `partial-long` rows, and that is a property of the rows rather
than a weak generator: a reversed PARTIAL runs off the left end by construction, so it walks the
whole filler whichever side the filler is on. Their spans still start at text 0 under the control
(29 -> 26, 14 -> 13). Judge the control on the `fuzzy-long` lines.

Run:  python tools/probes/long-subject-padding-side-control.py [SEED]
"""

import json
import pathlib
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
RECORDER = REPO_ROOT / "tools" / "record-oracle.py"

# The two lines as `_generate_long` reads today. An exact match, so that a later edit to the
# padding rule fails this probe loudly instead of silently controlling nothing.
TRUE_LINE = (
    '        reverse = "(?r" in row["pattern"] or bool(row["flags"] & REVERSE)\n'
    '        row["subject"] = row["subject"] + filler if reverse else filler + row["subject"]'
)
CONTROL_LINE = '        row["subject"] = filler + row["subject"]  # CONTROL: pad the same side regardless'

SEED = sys.argv[1] if len(sys.argv) > 1 else "7"
COUNT = "200"
WALKED = 100


def record(out: pathlib.Path) -> list:
    subprocess.run(
        [sys.executable, str(RECORDER), "--generator", "partial-long,fuzzy-long",
         "--count", COUNT, "--seed", SEED, "--out", str(out)],
        check=True, capture_output=True, cwd=REPO_ROOT,
    )
    return [
        json.loads(line)
        for line in out.read_text(encoding="utf-8").splitlines()
        if line.strip() and '"kind": "header"' not in line
    ]


def is_reversed(row) -> bool:
    return "(?r" in row["pattern"] or bool(row["flags"] & 0x400)


def summarise(rows, generator) -> str:
    walked = at_zero = at_end = matched = 0
    for row in rows:
        if row.get("generator") != generator or not is_reversed(row):
            continue
        span = row.get("codepointSpan")
        if not span:
            continue
        matched += 1
        size = len(row["subject"])
        walked += size - span[1] >= WALKED
        at_zero += span[0] == 0
        at_end += span[1] == size
    return (f"matched {matched:3d}  walked {walked:3d}  "
            f"spans starting at text 0: {at_zero:3d}  spans ending at the text end: {at_end:3d}")


def main() -> int:
    scratch = REPO_ROOT / ".scratch"
    scratch.mkdir(exist_ok=True)
    original = RECORDER.read_text(encoding="utf-8")
    if TRUE_LINE not in original:
        print("the padding lines in _generate_long have moved - this probe controls nothing; fix it")
        return 1
    try:
        baseline = record(scratch / "padding-side-baseline.jsonl")
        # newline="" - on Windows Python rewrites \n as \r\n, and .editorconfig says LF.
        RECORDER.write_text(original.replace(TRUE_LINE, CONTROL_LINE), encoding="utf-8", newline="")
        control = record(scratch / "padding-side-control.jsonl")
    finally:
        RECORDER.write_text(original, encoding="utf-8", newline="")

    print(f"seed {SEED}, {COUNT} rows a generator, REVERSED rows only\n")
    for generator in ("fuzzy-long", "partial-long"):
        print(f"== {generator}")
        print(f"   baseline  {summarise(baseline, generator)}")
        print(f"   control   {summarise(control, generator)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
