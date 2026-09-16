r"""Every unjudged sweep row, every single-construct ablation, in one table.

S52's sittings 15 to 18 judged the seed sweep's diverging rows a few at a time, and twice recorded
the same lesson: **reasoning from a row's shape was wrong and the ablation was right** (sitting 16
on group A, sitting 17 on control B, sitting 18 on group D). The owner's instruction of 2026-09-16
is to stop doing that one row at a time - run the ablations for every remaining row in ONE pass and
classify from the table rather than from the shape.

WHAT AN ABLATION IS HERE, and what it is not. An ablation removes ONE construct from the row and
asks the SAME question of both engines: a flag bit (and its inline spelling), a fuzzy constraint,
a backtracking verb, an atomic cut, upstream's required-string prefilter. A DOOR asks a DIFFERENT
question of the same pattern - a single `search` where the row scans, a non-overlapped scan where
the row overlaps, the call without `partial=True`. Both are emitted, and they are labelled apart,
because an ablation that makes the two engines agree names the construct upstream mishandles,
where a door that answers differently is upstream contradicting ITSELF and is the other half of
amendment 16's standard.

HOW TO RUN IT, three commands, no seed anywhere - these are explicit rows::

    python tools/probes/sweep-ablation-matrix.py --emit .scratch/ablations.jsonl
    pwsh -File tools/run-oracle.ps1 -Rows .scratch/ablations.jsonl
    python tools/probes/sweep-ablation-matrix.py --table TestResults/oracle/wave.jsonl TestResults/oracle/report.txt

The middle command is the whole point: `run-oracle.ps1 -Rows` re-records every variant against
upstream AND runs this port over the identical rows, so one run answers both halves of the
question. It exits non-zero while any row diverges, which every unjudged row does by construction;
the tally is the claim, not the exit code.

WHAT THE TABLE'S COLUMNS MEAN.

  ``upstream``  whether upstream's answer to the variant differs from its answer to the row as
                drawn. Read out of the recorded wave, not inferred.
  ``verdict``   AGREE when the two engines answered the same thing, DIVERGE when they did not and
                no entry claims it, and otherwise the id of the `ExpectedDivergences` entry that
                does - all three from the consumer's own report over the identical rows.

**A variant that AGREES tells you the port's answer too**, because agreement is equality - so the
two columns together say which ablation moves upstream onto this port's answer, which is the
classification question. There is no third column claiming the port moved independently: the
consumer prints a port line for a REPORTED row only, and inventing one for an agreeing row by
re-rendering upstream's JSON would be this file duplicating the consumer's renderer. The report
itself is where both engines' answers are, and the table points at it.

Measured against regex 2026.9.10 and the pin this oracle records.
"""
import argparse
import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

REPO_ROOT = Path(__file__).resolve().parents[2]
ROWS_FILE = REPO_ROOT / "tools" / "probes" / "sweep-divergence-rows.jsonl"

# The flag letters, with the module's own bit values read at run time rather than copied here: a
# constant that drifts from upstream's header is exactly the mistake this repo keeps finding.
FLAG_NAMES = (
    "BESTMATCH", "ENHANCEMATCH", "POSIX", "REVERSE", "IGNORECASE", "FULLCASE",
    "WORD", "DOTALL", "MULTILINE", "VERBOSE", "ASCII", "UNICODE", "VERSION0", "VERSION1",
)
INLINE = {
    "BESTMATCH": "b", "ENHANCEMATCH": "e", "POSIX": "p", "REVERSE": "r", "IGNORECASE": "i",
    "FULLCASE": "f", "WORD": "w", "DOTALL": "s", "MULTILINE": "m", "VERBOSE": "x",
    "ASCII": "a", "UNICODE": "u", "VERSION0": "V0", "VERSION1": "V1",
}

# The generator tags the recorder compiles prefilter-free, copied from its PREFILTER_FREE_GENERATORS
# (tools/record-oracle.py:325) because that is the switch, and a probe that guesses it is asking a
# different question from the wave.
PREFILTER_FREE = ("verbs", "partial-sliced")

# A fuzzy constraint, and nothing that is merely a repeat: every fuzzy spelling upstream accepts
# carries a `<=` or a `+`-joined cost list, and `{2,3}` and `{0}` carry neither.
FUZZY = re.compile(r"\{[^{}]*(?:<=|[a-z]\+)[^{}]*\}")

# The two codepoints `CaseFolding.txt` marks `T`, and their three stand-ins: each folds to more
# than one character, as U+0130 does, and none of the three has a `T` row of its own.
TURKIC = "İı"
TURKIC_STAND_INS = {"00df": "ß", "fb00": "ﬀ", "01f0": "ǰ"}


def swap_turkic(text: str, stand_in: str) -> str:
    for letter in TURKIC:
        text = text.replace(letter, stand_in)
    return text


# An inline flag group, as `_regex_core.py` writes one: letters, optionally a `V0`/`V1`, optionally
# negated after a `-`. Only the positive half is ablated; a `(?-i)` is already an absence.
INLINE_GROUP = re.compile(r"\(\?([aefiLmpsuwxrbV0-9]+)\)")


def flag_bits():
    import regex
    return {name: getattr(regex, name) for name in FLAG_NAMES if hasattr(regex, name)}


def remove_inline(pattern: str, letter: str) -> str:
    """The pattern with one inline flag letter gone from every group that sets it."""
    def strip(match: re.Match) -> str:
        body = match.group(1)
        if letter not in body:
            return match.group(0)
        # `V0`/`V1` is two characters and a plain letter is one, so the token is removed rather
        # than the character - dropping the `0` of `V0` would leave a `(?V)` upstream rejects.
        body = body.replace(letter, "", 1)
        return f"(?{body})" if body else ""
    return INLINE_GROUP.sub(strip, pattern)


def ablations(row: dict, bits: dict) -> list[tuple[str, str, dict]]:
    """Every (label, kind, row) variant this row can carry, as oracle rows ready to record."""
    pattern, flags = row["pattern"], row.get("flags", 0)
    out: list[tuple[str, str, dict]] = []

    def variant(label: str, kind: str, **changes) -> None:
        made = {k: v for k, v in row.items()
                if k in ("generator", "pattern", "flags", "namedLists", "subject", "operation",
                         "oracle", "template", "count", "partial", "pos", "endpos",
                         "codepointSlice")}
        made.update(changes)
        out.append((label, kind, made))

    variant("as-drawn", "base")

    for name, bit in bits.items():
        letter = INLINE[name]
        gone = remove_inline(pattern, letter)
        set_by_flag = bool(flags & bit)
        if not set_by_flag and gone == pattern:
            continue
        variant(f"no-{letter}", "ablation", pattern=gone, flags=flags & ~bit)

    if FUZZY.search(pattern):
        variant("no-fuzzy", "ablation", pattern=FUZZY.sub("", pattern))
    if "(*SKIP)" in pattern:
        variant("skip->prune", "ablation", pattern=pattern.replace("(*SKIP)", "(*PRUNE)"))
        variant("verb-deleted", "ablation", pattern=pattern.replace("(*SKIP)", ""))
    if "(*PRUNE)" in pattern and "(*SKIP)" not in pattern:
        variant("prune-deleted", "ablation", pattern=pattern.replace("(*PRUNE)", ""))
    if "(?>" in pattern:
        variant("atomic-free", "ablation", pattern=pattern.replace("(?>", "(?:"))
    if "(?<" in pattern or "(?=" in pattern or "(?!" in pattern:
        # Not removed - a lookaround's body cannot be deleted without changing what the rest of the
        # pattern consumes - but its FUZZINESS can be, which is the ablation entry 11's doors want.
        pass

    # THE PREFILTER IS SWITCHED BY THE `generator` TAG, not by the `oracle` field. The recorder
    # neutralises upstream's required string for `PREFILTER_FREE_GENERATORS` alone
    # (tools/record-oracle.py:325 and :522) and WRITES `oracle` back as an output; nothing reads it
    # in. A first version of this ablation set `oracle` and produced 22 variants that were
    # byte-identical duplicates of their base rows - an artefact reported as a measurement, which
    # the second blind pass of 2026-09-16 caught. Flipping the tag is what actually asks the
    # question, and `generator` drives nothing else about a `--rows` row (its other uses at :2592,
    # :5755 and :6073 are generation and a determinism check).
    # THE TURKIC CONTROL, and it is a control rather than an ablation: the `T` letters cannot be
    # deleted, so they are SWAPPED for letters that fold to more than one character and carry no
    # `T` row of their own. "Take the U+0130 away" would not isolate anything - swapping it for `h`
    # also shortens the fold from two characters to one - which is why S52's second sitting chose
    # exactly these three (CaseFolding.txt: 00DF, FB00 and 01F0 are all F rows, none of them T).
    # `ensure_ascii=False`, or a named list's U+0130 is spelled `İ` here and the control the
    # row most needs is silently not generated. Row 34 of the sweep is exactly that row.
    lists = json.dumps(row.get("namedLists") or {}, ensure_ascii=False)
    if any(letter in pattern + row["subject"] + lists for letter in TURKIC):
        for name, stand_in in TURKIC_STAND_INS.items():
            variant(f"turkic-swap:{name}", "control",
                    pattern=swap_turkic(pattern, stand_in),
                    subject=swap_turkic(row["subject"], stand_in),
                    namedLists={k: [swap_turkic(w, stand_in) for w in v]
                                for k, v in (row.get("namedLists") or {}).items()})

    if row.get("generator") in PREFILTER_FREE:
        # "rows" is the tag a hand-written rows file gets, and it is not prefilter-free, so this
        # asks the row WITH upstream's required-string prefilter in place.
        variant("prefilter-on", "ablation", generator="rows", oracle=None)
    else:
        variant("prefilter-off", "ablation", generator="verbs", oracle=None)

    operation = row.get("operation", "search")
    if operation == "finditer-overlapped":
        variant("door:not-overlapped", "door", operation="finditer")
        variant("door:single-search", "door", operation="search")
    elif operation == "finditer":
        variant("door:single-search", "door", operation="search")
        variant("door:overlapped", "door", operation="finditer-overlapped")
    elif operation in ("split", "sub", "subf"):
        variant("door:scan-instead", "door", operation="finditer")
    elif operation == "search":
        variant("door:anchored-match", "door", operation="match")
    elif operation in ("match", "fullmatch"):
        variant("door:search-instead", "door", operation="search")

    if row.get("partial"):
        variant("door:no-partial", "door", partial=False)

    return out


def emit(path: Path, wanted: list[int]) -> int:
    bits = flag_bits()
    rows = read_rows(ROWS_FILE)
    written = 0
    with open(path, "w", encoding="ascii", newline="") as f:
        for number in wanted:
            row = rows[number - 1]
            for label, kind, made in ablations(row, bits):
                made = {k: v for k, v in made.items() if v is not None}
                made["comment"] = f"sweep-row {number} | {kind} | {label}"
                f.write(json.dumps(made, sort_keys=False) + "\n")
                written += 1
    print(f"wrote {path}: {written} variants over {len(wanted)} rows")
    print(f"rows: {','.join(str(n) for n in wanted)}")
    return 0


def read_rows(path: Path) -> list[dict]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()
            if line.strip() and not line.startswith("//")
            and json.loads(line).get("kind") != "header"]


def diverging_rows(report: Path) -> list[int]:
    """The sweep rows a baseline replay of the committed rows file reports as still diverging."""
    numbers = []
    for line in report.read_text(encoding="utf-8", errors="replace").splitlines():
        found = re.match(r"DIVERGE row (\d+) ", line)
        if found:
            numbers.append(int(found.group(1)))
    return numbers


def outcome_of(row: dict) -> str:
    """One variant's recorded upstream answer, canonically, for comparing two variants."""
    return json.dumps(row.get("outcome"), sort_keys=True)


# A report block's header, in the two shapes the consumer writes them: `DIVERGE row 12 (...)` for a
# row no entry claims, and `EXPECTED <entry-id> row 12 (...)` for one an entry does.
_HEADER = re.compile(r"^(?:DIVERGE|EXPECTED (?P<id>\S+)) row (?P<row>\d+) ")


def port_lines(report: Path) -> dict[int, str]:
    """The `port` line of every reported block, by the report's own row number."""
    lines = report.read_text(encoding="utf-8", errors="replace").splitlines()
    out: dict[int, str] = {}
    current = None
    for line in lines:
        found = _HEADER.match(line)
        if found:
            current = int(found.group("row"))
        elif current is not None and line.strip().startswith("port "):
            out[current] = line.strip()[5:].strip()
            current = None
    return out


def verdicts(report: Path) -> dict[int, str]:
    """AGREE is the absence of a block; anything reported is DIVERGE or the entry that claims it."""
    out: dict[int, str] = {}
    for line in report.read_text(encoding="utf-8", errors="replace").splitlines():
        found = _HEADER.match(line)
        if found:
            out[int(found.group("row"))] = found.group("id") or "DIVERGE"
    return out


def table(wave: Path, report: Path, wanted: list[int]) -> int:
    """The matrix, labels and all.

    The RECORDER DROPS a row's `comment`, so the labels cannot be read back out of the wave: it
    rewrites each row from the question it was asked. What survives is the ORDER, so the variants
    are regenerated here from the same committed rows file and the same row list that `--emit`
    used, and every one is checked against the wave row it is paired with, on every field of the
    question the wave carries. A mismatch is a hard error rather than a mislabelled table.
    """
    recorded = read_rows(wave)
    told = verdicts(report)
    ports = port_lines(report)
    bits = flag_bits()
    rows = read_rows(ROWS_FILE)

    labelled = [(number, label, kind, made)
                for number in wanted
                for label, kind, made in ablations(rows[number - 1], bits)]

    if len(labelled) != len(recorded):
        raise SystemExit(f"{len(labelled)} variants regenerated against {len(recorded)} in the "
                         f"wave - the row list does not match the run")

    print(f"{'sweep':>5} {'variant':<22} {'kind':<8} {'upstream':<9} verdict")
    current = None
    base_by_sweep: dict[int, str] = {}

    for index, ((number, label, kind, made), row) in enumerate(zip(labelled, recorded), start=1):
        # EVERY field that identifies the question, not just the visible three: a `no-<letter>`
        # variant of a flag carried in `flags` differs from its base row in nothing else, and so do
        # `prefilter-on`/`prefilter-off` (`oracle`), `door:no-partial` (`partial`) and row 34's
        # turkic swaps (`namedLists`). The first version of this guard read pattern, subject and
        # operation alone, which left 94 of 211 variants able to pair with the wrong wave row in
        # silence - and the column this table is read for is exactly the one a mis-pairing inverts.
        # Found by the blind review of 2026-09-16.
        # THE FIELDS THE RECORDER ECHOES, and only those. `oracle` is not one it reads: the recorder
        # DERIVES it from `generator` and writes it back, so comparing `generator` - which IS in the
        # list - covers every variant `oracle` could have distinguished. `template` and `count` it
        # keeps or drops according to the operation, so a `door:scan-instead` variant of a `split`
        # row legitimately loses the `count` it was emitted with, and comparing either would fire on
        # a correct pairing. With `generator` in the list no two variants of one row are
        # indistinguishable, so the residue an earlier repair had to admit is closed.
        # `partial` is compared as a BOOLEAN because the recorder writes the key only when it is
        # true, so a `door:no-partial` variant emitted with `False` comes back with the key absent.
        for key in ("pattern", "subject", "operation", "flags", "namedLists", "partial",
                    "pos", "endpos", "generator"):
            mine, theirs = made.get(key), row.get(key)
            if key == "partial":
                mine, theirs = bool(mine), bool(theirs)
            if mine != theirs:
                raise SystemExit(f"variant {index} ({number} {label}) is not the wave's row "
                                 f"{index}: {key} differs ({made.get(key)!r} against "
                                 f"{row.get(key)!r})")
        if kind == "base":
            base_by_sweep[number] = outcome_of(row)
        if number != current:
            print()
            current = number
        moved = "MOVED" if outcome_of(row) != base_by_sweep.get(number) else "same"
        verdict = told.get(index, "AGREE")
        print(f"{number:>5} {label:<22} {kind:<8} {moved:<9} {verdict}")

    agreeing = sum(1 for index in range(1, len(recorded) + 1) if index not in told)
    print(f"\n{len(recorded)} variants, {agreeing} agreeing, {len(told)} reported")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--emit", type=Path, help="write the ablation rows file")
    parser.add_argument("--baseline", type=Path,
                        default=REPO_ROOT / "TestResults" / "oracle" / "report.txt",
                        help="a replay report of the committed rows file; its DIVERGE rows are "
                             "the ones ablated (default: TestResults/oracle/report.txt)")
    parser.add_argument("--rows", help="explicit sweep row numbers instead, comma separated")
    parser.add_argument("--table", nargs=2, type=Path, metavar=("WAVE", "REPORT"),
                        help="print the matrix from a recorded wave and its report")
    args = parser.parse_args()

    if not args.emit and not args.table:
        parser.error("one of --emit or --table is required")

    if args.rows:
        wanted = [int(n) for n in args.rows.split(",") if n.strip()]
    elif args.table:
        # The ablation run's own report has overwritten the baseline replay's by the time the
        # table is printed, so `--rows` is how a table is asked for the same set twice.
        parser.error("--table needs --rows: the row list the emit used, comma separated")
    elif args.baseline.exists():
        wanted = diverging_rows(args.baseline)
    else:
        parser.error(f"no baseline report at {args.baseline}; run the replay first or pass --rows")

    if args.table:
        return table(*args.table, wanted)
    return emit(args.emit, wanted)


if __name__ == "__main__":
    sys.exit(main())
