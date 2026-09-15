"""Turn the three red-seed reports of an oracle gate run into one triage table.

The 6000-row three-seed gate writes `TestResults/oracle/report-<seed>.txt` for every RED seed, and
those files are gitignored - so a slice that wants to hand its divergences to the next sitting has
to either transcribe them (which rots, and mis-transcribes) or record how to regenerate them. This
is the second option:

    pwsh -File tools/run-oracle.ps1 -Count 6000          # about 8.5 minutes, writes the reports
    python tools/probes/gate-divergence-triage.py        # reads them, prints the table

It renders no judgement. It reports each diverging row's generator, operation, flags and the shape
features that decide which family to try first - whether the pattern carries a `(*SKIP)`, whether
it is reversed, whether it has a fuzzy section, and whether either engine ERRORED rather than
answered - plus totals.

ONE THING TO GET RIGHT, because the first version of this got it wrong and printed every pattern
attributed to the previous row: a DIVERGE block is NOT a fixed number of lines. A `sub` row carries
a template, a row whose port timed out carries a stack trace. Bound each block at the next header.
"""

import datetime
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

REPO_ROOT = Path(__file__).resolve().parents[2]
REPORTS = REPO_ROOT / "TestResults" / "oracle"
HEAD = re.compile(r"^DIVERGE row (\d+) \((\S+)\) (\S+) flags=(\S+)")


def default_seeds() -> list[int]:
    """The seeds `tools/run-oracle.ps1` runs by default.

    Its `$Seeds` default is `"7,4242,$(Get-Date -Format 'yyyyMMdd')"` (`:243`), so the third one is
    the RUN DATE and not a constant. Hardcoding the date this probe was written would make it stop
    reading that seed's report the next day - and print "the seed was GREEN" about a report it never
    looked for, which is the worst kind of wrong. Pass seeds explicitly to read an older run.
    """
    return [7, 4242, int(datetime.date.today().strftime("%Y%m%d"))]


def features(pattern: str, upstream: str, port: str) -> list[str]:
    tags = []
    if "(*SKIP)" in pattern:
        tags.append("SKIP")
    if "(?r)" in pattern:
        tags.append("rev")
    if re.search(r"\{[^}]*[eisd]\s*<=", pattern) or "(?b)" in pattern or "(?e)" in pattern:
        tags.append("fuzzy")
    if "Timeout" in port:
        tags.append("PORT-TIMEOUT")
    if "error while matching" in upstream:
        tags.append("UPSTREAM-ERROR")
    elif "error while matching" in port:
        tags.append("PORT-ERROR")
    return tags


def main(argv=None) -> int:
    seeds = [int(a) for a in (argv or sys.argv[1:])] or default_seeds()
    # Two dicts, not one: `fuzzy` is BOTH a generator name and a shape tag, so a single dict sums
    # the one `fuzzy` row with the nine `fuzzy`-tagged ones and reports 10 of neither.
    generators: dict[str, int] = {}
    tags: dict[str, int] = {}
    seen = 0
    for seed in seeds:
        report = REPORTS / f"report-{seed}.txt"
        if not report.exists():
            print(f"\n=== seed {seed}: no report - the seed was GREEN, or the gate has not run")
            continue
        lines = report.read_text(encoding="utf-8", errors="replace").splitlines()
        starts = [i for i, line in enumerate(lines) if HEAD.match(line)]
        print(f"\n=== seed {seed}  ({len(starts)} diverging rows)")
        for n, start in enumerate(starts):
            end = starts[n + 1] if n + 1 < len(starts) else len(lines)
            row, generator, operation, flags = HEAD.match(lines[start]).groups()
            block = lines[start + 1:end]

            def first(prefix: str) -> str:
                return next(
                    (line.strip()[len(prefix):].strip()
                     for line in block if line.strip().startswith(prefix)),
                    "",
                )

            pattern, upstream, port = first("pattern "), first("upstream "), first("port ")
            shape = features(pattern, upstream, port)
            seen += 1
            generators[generator] = generators.get(generator, 0) + 1
            for tag in shape:
                tags[tag] = tags.get(tag, 0) + 1
            print(f"  {row:>7} {generator:<15}{operation:<20}{flags:<9}{','.join(shape) or '-'}")
            print(f"          pat {pattern[:110]}")
            print(f"          up  {upstream[:70]}")
            print(f"          us  {port[:70]}")

    print(f"\n=== {seen} diverging rows over {len(seeds)} seeds")
    print("  by generator:")
    for key in sorted(generators):
        print(f"    {key:<20}{generators[key]}")
    print("  by shape:")
    for key in sorted(tags):
        print(f"    {key:<20}{tags[key]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
