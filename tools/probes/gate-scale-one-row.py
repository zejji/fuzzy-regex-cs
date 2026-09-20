"""Scale one benchmark row in a copy of a BenchmarkDotNet artifacts folder, to see what
tools/compare-benchmarks.ps1 does with it. A negative control for the gate, on real numbers.

    python tools/probes/gate-scale-one-row.py <substring> <factor> <srcResults> <dstResults>

Scales that row's Median and BytesAllocatedPerOperation by <factor>; every other row is copied
unchanged, so the only difference the comparison can see is the one row.

S58 used it 2026-09-19 to show that a floor below -Threshold changes no verdict:

    python tools/probes/gate-scale-one-row.py SplitLong 1.20 \\
        artifacts/bench/2026-09-19-S58-noise-A/results .scratch/doctored-1p2/results
    pwsh -File tools/compare-benchmarks.ps1 -UseExisting -ArtifactsPath .scratch/doctored-1p2 \\
        -BaselinePath bench/baselines/<machine-id>/2026-09-19-S58-noise-A.json -Job medium

which printed `1.20x 1.20x` on that row and GREEN overall; re-run with `-Threshold 1.05` and it is
RED at `-AllocationNoiseFloor 1.0001` and GREEN at 1.25, which is the floor excusing a ratio.

NOTE: `artifacts/` is gitignored, so that exact run needs a benchmark run to have produced the
folder. The same behaviour is pinned from committed files alone by
tools/tests/CompareBenchmarks.Tests.ps1, which fabricates its own report.
"""

import json
import pathlib
import shutil
import sys

needle, factor, src, dst = sys.argv[1], float(sys.argv[2]), pathlib.Path(sys.argv[3]), pathlib.Path(sys.argv[4])
if dst.exists():
    shutil.rmtree(dst)
dst.mkdir(parents=True)

hits = 0
for path in src.glob("*-report-full-compressed.json"):
    d = json.loads(path.read_text(encoding="utf-8-sig"))
    for b in d["Benchmarks"]:
        if needle in b["FullName"]:
            b["Statistics"]["Median"] *= factor
            if b.get("Memory") and b["Memory"].get("BytesAllocatedPerOperation"):
                b["Memory"]["BytesAllocatedPerOperation"] = round(
                    b["Memory"]["BytesAllocatedPerOperation"] * factor
                )
            hits += 1
    (dst / path.name).write_text(json.dumps(d), encoding="utf-8")
print(f"scaled {hits} row(s) matching {needle!r} by {factor} into {dst}")
