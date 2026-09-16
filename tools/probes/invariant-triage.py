"""Reach and firings of the metamorphic invariants over one or more recorded waves.

S52c scope item 3. `tools/record-oracle.py` writes an invariant's id into a row's
`selfContradiction` when the row's own answers contradict each other; the wave summary counts the
firings. THIS PRINTS THE OTHER HALF, WHICH IS THE HALF THAT MAKES A ZERO READABLE: how many rows
each invariant was ELIGIBLE on.

An invariant that fired on 0 of 40,000 eligible rows is cheap evidence of consistency. An invariant
that fired on 0 of 0 is a check that is not running, and the two print identically in the wave
summary. Every zero in ORACLE-INVARIANTS.md's calibration column has to be told apart from a hole
before it is worth anything, and that is what this file is for.

Run:
    python tools/probes/invariant-triage.py TestResults/oracle/wave-inv-*.jsonl
"""

import importlib.util
import json
import sys
from pathlib import Path

_RECORDER = Path(__file__).resolve().parent.parent / "record-oracle.py"
_spec = importlib.util.spec_from_file_location("record_oracle", _RECORDER)
recorder = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(recorder)


def _eligibility(row: dict) -> set[str]:
    """Which invariants had something to decide on this row.

    Deliberately re-derived here from the row rather than exported by the recorder, so a checker
    that silently stops looking at a family shows up as its eligibility going to zero rather than
    as its firing count staying at zero.
    """
    eligible = set()
    outcome = row["outcome"]
    kind = outcome["kind"]
    matches = recorder._matches_of(outcome)

    for match in matches:
        if match.get("fuzzyCounts") is not None and match.get("fuzzyChanges") is not None:
            eligible.add("fuzzy-counts-match-changes")
        if match.get("lastIndex", -1) != -1:
            eligible.add("lastindex-participated")
        if len(match.get("groups") or []) > 1 and not recorder._SPAN_ESCAPES_THE_MATCH.search(
            row["pattern"]
        ):
            eligible.add("group-spans-inside-match")

    # Every recorded match passed through the captures read, whether or not it had a group.
    if matches:
        eligible.add("captures-are-the-texts-of-spans")

    # The denominator here is EVERY ROW THAT FAULTED, not every row that faulted beside an answering
    # twin - the latter is the violation condition itself, and an eligibility that equals the
    # violation prints 7 of 7 and says nothing. What this number answers is "how often does upstream
    # fall over at all, and in what share of those does a twin of the same row answer normally".
    if kind in ("timeout", "resource") or (kind == "error" and outcome.get("whileMatching")):
        eligible.add("no-fault-where-a-twin-answers")
    if "bestmatchFreeOutcome" in row:
        eligible.add("bestmatch-no-worse")
    if "posixFreeOutcome" in row:
        eligible.add("posix-chooses-among-flagless-answers")
    return eligible


def _describe(outcome: dict | None) -> str:
    """The fields of one outcome that decide which family a violation belongs to."""
    if outcome is None:
        return "-"
    kind = outcome["kind"]
    if kind == "error":
        return f"error {outcome['exception']} whileMatching={outcome.get('whileMatching')}"
    if kind in ("timeout", "resource"):
        return f"{kind} {outcome.get('exception', outcome.get('seconds'))}"
    matches = recorder._matches_of(outcome)
    if not matches:
        return kind
    parts = []
    for match in matches[:3]:
        groups = match.get("groups") or []
        span = f"({groups[0]['index']},{groups[0]['length']})" if groups else "?"
        counts = match.get("fuzzyCounts")
        changes = match.get("fuzzyChanges")
        tally = (
            None
            if changes is None
            else [len(changes["substitutions"]), len(changes["insertions"]), len(changes["deletions"])]
        )
        parts.append(
            span
            + ("" if counts is None else f" counts={counts}")
            + ("" if tally is None else f" tallied={tally}")
            + (" partial" if match.get("partial") else "")
        )
    more = "" if len(matches) <= 3 else f" ... {len(matches)} matches"
    return f"{kind} " + " | ".join(parts) + more


def _detail(path: str, number: int, row: dict) -> None:
    """Everything needed to classify one firing row, so the family is READ rather than re-derived."""
    print()
    print(f"--- {Path(path).name}:{number}  {row['generator']} {row['operation']}  "
          f"{','.join(row['selfContradiction'])}")
    print(f"    pattern {ascii(row['pattern'])}")
    print(f"    subject {ascii(row['subject'])}"
          + (f"  slice {row['codepointSlice']}" if row.get("codepointSlice") else "")
          + ("  partial" if row.get("partial") else ""))
    if "template" in row:
        # Part of the question on a substitution row, and the first thing a triage has to rule out
        # when upstream raises: `subf` renders the template with str.format, so a template naming a
        # group that does not exist raises an IndexError that is upstream's ANSWER rather than a
        # fault. Without this line that check meant opening the wave by hand.
        print(f"    template {ascii(row['template'])}  count={row.get('count')}")
    if row.get("namedLists"):
        print(f"    namedLists {ascii(row['namedLists'])}")
    print(f"    upstream   {_describe(row['outcome'])}")
    for key, _ in recorder._CONTROLS:
        if key in row:
            # WHETHER THE TWIN BREAKS THE SAME INVARIANT IS THE DISCRIMINATOR, and printing it is
            # what saves opening the wave by hand. A row whose `(*PRUNE)` twin breaks
            # `group-spans-inside-match` in the same place did not break it BECAUSE of the
            # `(*SKIP)`, so the verb family is not the mechanism; a twin that is clean says it is.
            twin = recorder._structural_violations({**row, "outcome": row[key]})
            also = f"   [twin also breaks {','.join(sorted(set(twin)))}]" if twin else "   [twin clean]"
            print(f"    {key:10} {_describe(row[key])}{also}")

    # The offending group, for a span violation: naming it is the whole of the minimisation.
    if "group-spans-inside-match" in row["selfContradiction"]:
        for match in recorder._matches_of(row["outcome"]):
            groups = match.get("groups") or []
            if not groups:
                continue
            start = groups[0]["index"]
            end = start + groups[0]["length"]
            for group in groups[1:]:
                if not group["success"]:
                    continue
                for index, length in [[group["index"], group["length"]], *group["captures"]]:
                    if index < start or index + length > end:
                        print(f"    outside    group {group['number']} span ({index},{length}) "
                              f"is not inside match ({start},{end - start})")


def main(paths: list[str]) -> int:
    detail = "--detail" in paths
    write_firing = None
    if "--write-firing" in paths:
        write_firing = paths[paths.index("--write-firing") + 1]
        paths = [path for path in paths if path != write_firing]
    paths = [path for path in paths if not path.startswith("--")]
    if not paths:
        print(__doc__.strip().splitlines()[-1], file=sys.stderr)
        return 2

    firing: list[tuple[str, int, dict]] = []
    eligible: dict[str, int] = {}
    fired: dict[str, int] = {}
    by_generator: dict[tuple[str, str], int] = {}
    examples: dict[str, list[str]] = {}
    rows_read = 0

    for path in paths:
        for number, line in enumerate(Path(path).read_text(encoding="ascii").splitlines()):
            row = json.loads(line)
            if row.get("kind") == "header":
                continue
            rows_read += 1
            if row.get("selfContradiction"):
                firing.append((path, number, row))
            for invariant in _eligibility(row):
                eligible[invariant] = eligible.get(invariant, 0) + 1
            for invariant in row.get("selfContradiction", ()):
                fired[invariant] = fired.get(invariant, 0) + 1
                by_generator[(invariant, row["generator"])] = (
                    by_generator.get((invariant, row["generator"]), 0) + 1
                )
                # `ascii()` rather than `!r`, because a Windows console is cp1252 and a repr that
                # keeps a printable astral character kills this script with a UnicodeEncodeError
                # after it has printed half its table. The waves themselves are ASCII for the same
                # reason (`write` in tools/record-oracle.py).
                examples.setdefault(invariant, []).append(
                    f"{Path(path).name}:{number} {row['generator']} {row['operation']} "
                    f"{ascii(row['pattern'])} on {ascii(row['subject'])}"
                )

    print(f"{rows_read} rows from {len(paths)} wave(s)")
    print()
    print(f"{'invariant':44} {'eligible':>9} {'fired':>7}")
    for invariant in sorted(set(eligible) | set(fired)):
        print(f"{invariant:44} {eligible.get(invariant, 0):>9} {fired.get(invariant, 0):>7}")

    if by_generator:
        print()
        print("firings by generator")
        for (invariant, generator), count in sorted(by_generator.items()):
            print(f"  {invariant:44} {generator:18} {count}")

    for invariant, rows in sorted(examples.items()):
        print()
        print(f"{invariant} - {len(rows)} rows")
        for example in rows[:12]:
            print(f"  {example}")
        if len(rows) > 12:
            print(f"  ... and {len(rows) - 12} more")

    if detail:
        print()
        print(f"===== {len(firing)} firing rows in full =====")
        for path, number, row in firing:
            _detail(path, number, row)

    # The firing rows as a rows file, which is what feeds the second engine:
    #   python tools/probes/invariant-triage.py --write-firing .scratch/firing.jsonl <waves>
    #   python tools/probes/tre-fuzzy-check.py .scratch/firing.jsonl
    # Scope item 5 asks TRE about every fuzzy-core violation this slice triages, and handing it the
    # whole 126,240-row wave to find ten rows in is the wrong shape.
    if write_firing:
        Path(write_firing).write_text(
            "".join(json.dumps(row) + "\n" for _, _, row in firing), encoding="ascii"
        )
        print()
        print(f"wrote {len(firing)} firing rows to {write_firing}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
