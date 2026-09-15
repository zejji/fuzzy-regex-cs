"""Does a `*-long` row actually make the engine walk its long subject?

The four long-subject generators exist to reach three paths a subject of eight characters cannot:
the position-by-position scan for a start (upstream's `search_start` family), the repeat guards
that only bite after many iterations, and a fuzzy insert budget spent far from where the match
began. All three need the ANSWER to be far from the near end - a row whose filler happens to match
at offset 0 is a short row wearing a long subject, and the generator would be measuring nothing.

`tools/record-oracle.py` biases against that (`LONG_FILLER_ALPHABET` holds characters no base
generator puts in a literal) but cannot guarantee it: `.`, `\\w`, `[^a]` and any fuzzy substitution
match the filler happily. So this probe measures how far the bias carried, rather than asserting
it, and the number it prints is the evidence the slice's closing notes quote.

TWO metrics, because the three paths do not share one. `distance` is how many characters the scan
stepped over before the match began - the match offset for a forward row, and the distance back
from the right-hand end for a reversed one, since a reversed row is padded on the right and scans
leftwards. That is the metric for a distant start and for a far-from-the-start fuzzy budget.
`length` is how long the match itself is, and that is the metric for a repeat guard: a guard is
reached by ITERATIONS, so a `quantifiers-long` row earns its long subject by consuming the text
rather than by stepping over it. Judge each generator on the column its path lives in.

Run:  python tools/probes/generator-long-subject-reach.py [--count N] [--seed S]
"""

from __future__ import annotations

import argparse
import json
import statistics
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
RECORDER = REPO_ROOT / "tools" / "record-oracle.py"
LONG_GENERATORS = ("literals-long", "quantifiers-long", "partial-long", "fuzzy-long")

# What counts as having walked the text. Far below the 1,000-character floor on purpose: the claim
# under test is "the engine stepped over a run of text", and a hundred positions is already two
# orders of magnitude past what every wave before this one asked for.
WALKED = 100


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--count", type=int, default=200)
    parser.add_argument("--seed", type=int, default=7)
    args = parser.parse_args(argv)

    out = REPO_ROOT / ".scratch" / "long-subject-reach.jsonl"
    out.parent.mkdir(exist_ok=True)
    subprocess.run(
        [
            sys.executable,
            str(RECORDER),
            "--generator",
            ",".join(LONG_GENERATORS),
            "--count",
            str(args.count),
            "--seed",
            str(args.seed),
            "--out",
            str(out),
        ],
        check=True,
        cwd=REPO_ROOT,
    )

    rows = [json.loads(line) for line in out.read_text(encoding="utf-8").splitlines() if line.strip()]
    rows = [row for row in rows if row.get("generator") in LONG_GENERATORS]

    print(f"\nseed {args.seed}, {args.count} rows a generator\n")
    header = ("generator", "rows", "match", "walked", "dist", "len", "long", "subject")
    print(f"{header[0]:<18}{header[1]:>6}{header[2]:>7}{header[3]:>8}{header[4]:>8}{header[5]:>7}{header[6]:>6}{header[7]:>9}")
    for name in LONG_GENERATORS:
        mine = [row for row in rows if row["generator"] == name]
        subjects = [len(row["subject"]) for row in mine]
        distances, lengths = [], []
        for row in mine:
            span = row.get("codepointSpan")
            if not span:
                continue
            reverse = "(?r)" in row["pattern"]
            distances.append(len(row["subject"]) - span[1] if reverse else span[0])
            lengths.append(span[1] - span[0])
        median = lambda xs: round(statistics.median(xs)) if xs else 0  # noqa: E731
        print(
            f"{name:<18}{len(mine):>6}{len(distances):>7}"
            f"{sum(1 for d in distances if d >= WALKED):>8}{median(distances):>8}"
            f"{median(lengths):>7}{sum(1 for n in lengths if n >= WALKED):>6}"
            f"{median(subjects):>9}"
        )

    every = [row for row in rows if row.get("codepointSpan")]
    print(f"\n`walked` counts matches beginning {WALKED}+ characters into the scan and `long` counts")
    print(f"matches {WALKED}+ characters long; `dist`, `len` and `subject` are medians.")
    print(f"{len(every)} of {len(rows)} rows answered a match.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
