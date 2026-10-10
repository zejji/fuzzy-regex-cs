#!/usr/bin/env python
"""Records one judged oracle row in tests/FuzzyRegex.OracleTests/JudgedDivergences.jsonl.

Why this exists
---------------
A scheduled oracle sweep draws fresh rows, and some of them are new rows of an upstream bug that
has already been judged. A key that recognises every such row automatically would also claim a
port bug of the same shape (three attempts failed that way, 2026-10-09), so the judgement stays
with a person or an agent and is written down as data, one line per row. The `judged-row` entry in
ExpectedDivergences.cs then accounts for the row only while both engines still give the answers
recorded here, so any change to either answer turns the sweep red again.

What it reads
-------------
The row's question and upstream's answer come from the sweep's wave, line N of
TestResults/oracle/sweep-S/wave.jsonl (line 0 is the header, so line N is row N). Both answers'
one-line descriptions come from the same sweep's report.txt, whose DIVERGE block prints upstream's
answer and this port's, each as its Describe() text. The row must be a DIVERGE or FAULT block: a
row a key already claims needs no judgement.

A FAULT row is one upstream crashed on. This port must never reproduce an upstream crash: it
answers correctly or refuses the pattern at compile time. The block's port line is pinned as for a
DIVERGE row, upstream's answer is written as "<upstream crashed>", and `--crash` must give the
evidence, naming the recorded exit code. A port line that is an error raised while matching is
refused: that is a port bug to fix, not an answer to judge.

Usage
-----
    python tools/judge-row.py --seed 4242 --row 34781 --cause "ledger 2" \\
        --witness "ba??x over 'baa', match, partial: upstream (0,3)P, port no match"
    python tools/judge-row.py --seed 187242955 --row 25286 --cause "ledger 11" --witness "..." \\
        --crash "exit 3221225477 (access violation), reproduced alone; ..."

`--cause` is one or more of `ledger N` (an entry of docs/plan/upstream-reports/LEDGER.md), `DNN`
(a row of docs/KNOWN-DEFECTS.md) or a pending candidate named in the data file's header, joined
with ` + ` when two mechanisms stack on one row. `--dry-run` prints the line and writes nothing.
"""

import argparse
import datetime
import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_FILE = REPO_ROOT / "tests" / "FuzzyRegex.OracleTests" / "JudgedDivergences.jsonl"
DEFAULT_SWEEPS = REPO_ROOT / "TestResults" / "oracle"
LEDGER = REPO_ROOT / "docs" / "plan" / "upstream-reports" / "LEDGER.md"
KNOWN_DEFECTS = REPO_ROOT / "docs" / "KNOWN-DEFECTS.md"

# The fields a question is made of, matching JudgedDivergences.QuestionOf in the consumer.
QUESTION_KEYS = ("pattern", "flags", "namedLists", "subject", "operation", "template", "count",
                 "partial", "pos", "endpos")

# A crash row's upstream answer, matching JudgedDivergences.CrashedMarker in the consumer.
CRASHED_MARKER = "<upstream crashed>"

CODEPOINTS_SUFFIX = re.compile(r"   \[python codepoints -?\d+,-?\d+\]$")


def pending_candidates(text: str) -> set[str]:
    """The names the data file's header allows as a cause before they have a ledger entry."""
    for line in text.splitlines():
        m = re.match(r"^// Pending candidates:\s*(.*)$", line)
        if m:
            return {name.strip() for name in m.group(1).split(",") if name.strip()}
    raise SystemExit("the data file has no '// Pending candidates:' header line")


def cause_problems(cause: str, pending: set[str]) -> list[str]:
    """Every part of a cause that names nothing the repository records."""
    ledger = LEDGER.read_text(encoding="utf-8")
    defects = KNOWN_DEFECTS.read_text(encoding="utf-8")
    problems = []
    for part in cause.split(" + "):
        if m := re.fullmatch(r"ledger (\d+)", part):
            if not re.search(rf"^## {m.group(1)}\. ", ledger, re.M):
                problems.append(f"'{part}': LEDGER.md has no entry {m.group(1)}")
        elif re.fullmatch(r"D\d+", part):
            if not re.search(rf"\b{part}\b", defects):
                problems.append(f"'{part}': KNOWN-DEFECTS.md never names it")
        elif part not in pending:
            problems.append(f"'{part}': not 'ledger N', 'DNN' or a pending candidate ({', '.join(sorted(pending))})")
    return problems


def report_answers(report: str, row: int) -> tuple[str, str, bool]:
    """Upstream's and this port's Describe() text from the row's DIVERGE or FAULT block, and
    whether it is a FAULT (upstream crashed). A crash's upstream text is CRASHED_MARKER."""
    text = report.replace("\r\n", "\n")
    m = re.search(rf"^(DIVERGE|FAULT|EXPECTED [^\n]*?) row {row} \(.*?(?=\n\n|\Z)", text, re.M | re.S)
    if not m:
        raise SystemExit(f"report has no block for row {row}")
    if m.group(1).startswith("EXPECTED"):
        raise SystemExit(f"row {row} is already accounted for ({m.group(1)}); there is nothing to judge")
    block = m.group(0)
    upstream = re.search(r"^  upstream (.*)$", block, re.M).group(1)
    port = re.search(r"^  port     (.*)$", block, re.M)
    if m.group(1) == "DIVERGE":
        return CODEPOINTS_SUFFIX.sub("", upstream), port.group(1), False
    if not upstream.startswith("upstream crashed (exit "):
        raise SystemExit(f"row {row} is a FAULT the MSan screen attributed, not a plain crash; judge it with the screen")
    if not port:
        raise SystemExit(f"row {row}'s FAULT block has no port line; re-run the sweep with a consumer that asks the port")
    if port.group(1).startswith("error while matching "):
        raise SystemExit(f"row {row}: this port raised an error while matching ({port.group(1)}); "
                         "that is a port bug to fix, never a judged answer")
    return CRASHED_MARKER, port.group(1), True


def question_of(row: dict) -> str:
    return json.dumps({key: row.get(key) for key in QUESTION_KEYS}, sort_keys=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--seed", required=True, help="the sweep's seed: reads <sweeps>/sweep-SEED/")
    parser.add_argument("--row", type=int, required=True, help="the row number in that sweep's report")
    parser.add_argument("--cause", required=True)
    parser.add_argument("--witness", required=True,
                        help="the mechanism's minimal pattern and subject with both engines' answers")
    parser.add_argument("--crash", default=None,
                        help="FAULT rows only: the crash evidence - the exit code, where upstream crashes "
                             "if known, and how it was reproduced")
    parser.add_argument("--judged", default=datetime.date.today().isoformat())
    parser.add_argument("--sweeps", type=Path, default=DEFAULT_SWEEPS)
    parser.add_argument("--file", type=Path, default=DEFAULT_FILE)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if not args.witness.strip():
        raise SystemExit("--witness is empty")
    data = args.file.read_text(encoding="utf-8")
    problems = cause_problems(args.cause, pending_candidates(data))
    if problems:
        raise SystemExit("unknown cause: " + "; ".join(problems))

    sweep = args.sweeps / f"sweep-{args.seed}"
    with open(sweep / "wave.jsonl", encoding="utf-8") as wave:
        for number, line in enumerate(wave):
            if number == 0 and json.loads(line).get("defaultVersion") != 8192:
                # The consumer parses these rows as recorded under VERSION0, as every sweep is today.
                raise SystemExit("the sweep was not recorded under DEFAULT_VERSION VERSION0 (8192)")
            if number == args.row:
                raw = line.rstrip("\r\n")
                break
        else:
            raise SystemExit(f"{sweep / 'wave.jsonl'} has no row {args.row}")
    row = json.loads(raw)
    upstream, port, crashed = report_answers((sweep / "report.txt").read_text(encoding="utf-8"), args.row)
    if crashed != (args.crash is not None):
        raise SystemExit("--crash is required for a FAULT row (upstream crashed) and allowed only there")
    if crashed and f"exit {row['outcome']['exitCode']}" not in args.crash:
        raise SystemExit(f"--crash must name the recorded exit code: 'exit {row['outcome']['exitCode']}'")

    asked = question_of(row)
    for existing in data.splitlines():
        if existing.strip() and not existing.startswith("//") and question_of(json.loads(existing)["row"]) == asked:
            raise SystemExit(f"this question is already judged: {json.loads(existing)['source']}")

    # The row is written byte for byte as the recorder wrote it, so record-oracle.py --recheck can
    # re-ask upstream exactly the question the sweep asked.
    head = {"cause": args.cause, "upstream": upstream, "port": port, "witness": args.witness,
            "judged": args.judged, "source": f"{args.seed}/{args.row}"}
    if crashed:
        head["crash"] = args.crash
    entry = json.dumps(head, ensure_ascii=True)[:-1] + ', "row": ' + raw + "}"
    if args.dry_run:
        print(entry)
        return 0
    with open(args.file, "a", encoding="utf-8", newline="\n") as out:
        if data and not data.endswith("\n"):
            out.write("\n")
        out.write(entry + "\n")
    print(f"judged {args.seed}/{args.row} as {args.cause}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
