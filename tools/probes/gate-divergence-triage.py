"""Turn the three red-seed reports of an oracle gate run into one triage table.

The 6000-row three-seed gate writes `TestResults/oracle/report-<seed>.txt` for every RED seed, and
those files are gitignored - so a slice that wants to hand its divergences to the next sitting has
to either transcribe them (which rots, and mis-transcribes) or record how to regenerate them. This
is the second option:

    pwsh -File tools/run-oracle.ps1 -Count 6000          # about 8.5 minutes, writes the reports
    python tools/probes/gate-divergence-triage.py        # reads them, prints the table

A `-Rows` replay writes ONE report, `report.txt`, because it runs no seed at all (`run-oracle.ps1`
gives it the pseudo-seed -1). That is the shape S52's sweep triage uses, so the same table is
reachable over an explicit rows file rather than over a seed list:

    pwsh -File tools/run-oracle.ps1 -Rows tools/probes/sweep-divergence-rows.jsonl
    python tools/probes/gate-divergence-triage.py --report TestResults/oracle/report.txt

That rows file holds the 37 rows S52's first seed sweep diverged on, each carrying a `comment` of
the `sweep-<seed>` directory and the row number it was lifted from. It exists BECAUSE those
directories are under `TestResults/`, which is gitignored: the sweep is where the rows came from
and the file is the only copy that survives a clean checkout.

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
    argv = list(argv if argv is not None else sys.argv[1:])
    # An explicit report path, for a `-Rows` replay, which has no seed to name its file after.
    reports = []
    while "--report" in argv:
        at = argv.index("--report")
        if at + 1 >= len(argv):
            print("--report needs a path, e.g. --report TestResults/oracle/report.txt")
            return 2
        reports.append((argv[at + 1], Path(argv[at + 1])))
        del argv[at:at + 2]
    if reports and argv:
        # Refusing rather than ignoring: `--report <path> 7` reads as "this report AND seed 7", and
        # silently dropping the 7 prints a table that looks like the answer to a question nobody asked.
        print(f"--report and seeds cannot be mixed; leftover arguments: {' '.join(argv)}")
        return 2
    if not reports:
        reports = [(f"seed {seed}", REPORTS / f"report-{seed}.txt")
                   for seed in ([int(a) for a in argv] or default_seeds())]
    # Two dicts, not one: `fuzzy` is BOTH a generator name and a shape tag, so a single dict sums
    # the one `fuzzy` row with the nine `fuzzy`-tagged ones and reports 10 of neither.
    generators: dict[str, int] = {}
    tags: dict[str, int] = {}
    seen = 0
    for label, report in reports:
        if not report.exists():
            print(f"\n=== {label}: no report - the seed was GREEN, or the gate has not run")
            continue
        lines = report.read_text(encoding="utf-8", errors="replace").splitlines()
        starts = [i for i, line in enumerate(lines) if HEAD.match(line)]
        print(f"\n=== {label}  ({len(starts)} diverging rows)")
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

    print(f"\n=== {seen} diverging rows over {len(reports)} report(s)")
    print("  by generator:")
    for key in sorted(generators):
        print(f"    {key:<20}{generators[key]}")
    print("  by shape:")
    for key in sorted(tags):
        print(f"    {key:<20}{tags[key]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
