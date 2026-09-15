r"""The controls that judge the four GENERATOR-DRAWN `(*SKIP)` rows of S52's 6000-row gate.

`gate-divergence-doors.py` puts every family's door to every diverging row of a gate RUN, and it is
the batch instrument: it needs `TestResults/oracle/wave-<seed>.jsonl`, which is gitignored and which
a later run overwrites. This probe is the permanent form for the four rows S52's eleventh sitting
measured - it reads its rows from the committed `gate-drawn-skip-rows.jsonl` beside it, so every
figure the entries and the ledger quote for these rows is re-runnable from the committed tree with
no gate run at all::

    python tools/probes/upstream-gate-drawn-skip-rows.py

It renders no verdict; it prints what upstream answers to each control. Three rows are judged
(`ExpectedDivergences.end-of-line-reads-a-skip-moved-slice` and
`.bestmatch-walk-truncated-by-a-skip`); the fourth, seed 7 row 76160, is deliberately NOT judged and
is here because the measurement that keeps it off the family is as much evidence as the ones that
put rows on it.

THE CONTROLS, and what each is for:

  (*SKIP)->(*PRUNE) / verb deleted
      `(*PRUNE)` is `(*SKIP)`'s opcode body but for the two lines that move a slice bound
      (`upstream/src/_regex.c:14553` reversed, `:14555` forwards), so a difference between the two
      spellings is about the bound and not about what the pattern means. Never a judgement on its
      own: on a scan the two are documented to differ.

  `$` spelled out, and where `$` is true
      `end-of-line-reads-a-skip-moved-slice`'s own control. `try_match_END_OF_LINE` (`:7110`) is the
      one edge predicate of eight that reads `slice_end` rather than a text bound, so a span ending
      where upstream's own `$` is FALSE is the entry's signature. `(?w)$` compiles to the twin that
      reads `text_end` (`regex/_regex_core.py:506-510`) and is the natural second control - but it
      also moves which positions ARE line ends, so it can only isolate a row whose phantom position
      it does not itself create. On row 74413 it does (3 is a `(?w)$` position), which is why the
      spelled-out definition is the control that runs here.

  the `(?b)`-free pair
      `bestmatch-walk-truncated-by-a-skip`'s door, and the line that classifies a row into it:
      with `(?b)` deleted the two verb spellings answer IDENTICALLY, so the pruning `(*SKIP)` shares
      with `(*PRUNE)` is innocent; with `(?b)` present they differ. A verb that only matters while a
      `(?b)` walk is running is a verb acting on the walk, which is that entry's mechanism
      (`do_best_fuzzy_match`'s loop guard, `:17625`).

  the anchored door (row 76160 only)
      What stands in for the `(*PRUNE)` line on a row whose `(*PRUNE)` spelling does not terminate.
      On the family's own row 1, upstream's anchored `match` at the candidate its walk never reached
      finds a BETTER match than the one it answered. Here it finds a worse one, which is why 76160 is
      not in the family.

Written by S52 sitting 13 against regex 2026.9.10, out of the scratch controls sitting 11 ran and
did not commit.
"""

import importlib.util
import json
import sys
from pathlib import Path

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

HERE = Path(__file__).resolve().parent
ROWS_FILE = HERE / "gate-drawn-skip-rows.jsonl"

# The doors probe owns `compile_row`/`answer`/`describe`; its filename is not an identifier, so it is
# loaded by path rather than imported. Reusing it is the point - a second implementation of "ask
# upstream this row's own operation" is a second thing to get wrong.
_spec = importlib.util.spec_from_file_location("doors", HERE / "gate-divergence-doors.py")
DOORS = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(DOORS)

# The label per row, in the file's order. Kept here rather than in the `.jsonl` so that file stays a
# valid `--rows` input for `tools/record-oracle.py`.
LABELS = (
    "seed 7 row 74413      end-of-line-reads-a-skip-moved-slice, row 3",
    "seed 7 row 76160      NOT JUDGED - the family's own door rules it out",
    "seed 4242 row 76778   bestmatch-walk-truncated-by-a-skip, row 6",
    "seed 4242 row 77119   bestmatch-walk-truncated-by-a-skip, row 7",
)

# `$` as what `$` is defined to be: end of the text, or before a line break at the end of a line.
SPELLED_OUT = r"(?:(?=\n)|(?!\n|.))"


def show(tag: str, produce) -> None:
    print(f"    {tag:<26}{ascii(produce())}")


def controls(row: dict, label: str, prune_timeout: float) -> None:
    pattern, flags = row["pattern"], row.get("flags", 0)
    print(f"\n== {label}")
    print("   pattern  " + ascii(pattern))
    print("   subject  " + ascii(row["subject"]) + f"   {row['operation']}  flags={flags:#x}")
    if row.get("template") is not None:
        print("   template " + ascii(row["template"]) + f"  count={row.get('count', 0)}")

    show("as drawn", lambda: DOORS.answer(row))

    # The `(*PRUNE)` line of row 76160 does not terminate - 5 seconds at sitting 8, 300 at sitting 11
    # and 300 again at sitting 13. It is run under a bounded timeout so the OTHER rows still get
    # measured; a hung
    # ablation reports nothing about anything.
    previous, DOORS.CALL_TIMEOUT = DOORS.CALL_TIMEOUT, prune_timeout
    try:
        show(f"(*SKIP)->(*PRUNE) [{prune_timeout:g}s]",
             lambda: DOORS.answer(row, pattern.replace("(*SKIP)", "(*PRUNE)")))
    finally:
        DOORS.CALL_TIMEOUT = previous
    show("verb deleted", lambda: DOORS.answer(row, pattern.replace("(*SKIP)", "")))

    if "(?b)" in pattern:
        without_b = pattern.replace("(?b)", "", 1)
        show("no (?b), (*SKIP)", lambda: DOORS.answer(row, without_b))
        show("no (?b), (*PRUNE)", lambda: DOORS.answer(row, without_b.replace("(*SKIP)", "(*PRUNE)")))

    if "$" in pattern:
        show("$ spelled out", lambda: DOORS.answer(row, pattern.replace("$", SPELLED_OUT)))
        show("(?w)$", lambda: DOORS.answer(row, pattern.replace("$", "(?w)$")))
        print(f"    upstream's own `$` true at {DOORS.dollar_positions(row['subject'], flags)}"
              f"   (?w)$ at {DOORS.dollar_positions(row['subject'], flags, True)}")


def single_search(row: dict, pattern: str, tag: str) -> None:
    """One `search` of an ablated pattern, with the fuzzy counts a `split` cannot show.

    A `split` renders as parts, so the drawn operation hides the error counts entirely - and on this
    row the `(?b)`-free ablation reports a change POSITION outside the seven-character subject
    (ledger entry 11's change-stack pollution). The value is a machine address and differs per run;
    what reproduces is that it is out of range.
    """
    compiled = DOORS.compile_row(row, pattern)
    match = compiled.search(row["subject"], timeout=DOORS.CALL_TIMEOUT)
    print(f"    search: {tag:<18}{ascii(DOORS.describe(match))}")


def anchored_door(row: dict, lo: int, hi: int) -> None:
    """Upstream's own anchored `match` at the candidate a truncated `(?b)` walk would have missed.

    The family's signature is that this finds a BETTER match than the one upstream answered. Row
    76160's costs MORE than this port's unanchored answer, which points the other way.
    """
    compiled = DOORS.compile_row(row)
    match = compiled.match(row["subject"], lo, hi, timeout=DOORS.CALL_TIMEOUT)
    print(f"    anchored match({lo},{hi})  " + ascii(DOORS.describe(match)))


def main(argv=None) -> int:
    argv = list(argv if argv is not None else sys.argv[1:])
    prune_timeout = float(argv[0]) if argv else 5.0
    print("regex", regex.__version__)
    print(f"rows     {ROWS_FILE}")

    rows = [json.loads(line) for line in ROWS_FILE.read_text(encoding="utf-8").splitlines()
            if line.strip()]
    if len(rows) != len(LABELS):
        raise SystemExit(f"{ROWS_FILE} holds {len(rows)} rows, {len(LABELS)} labels")

    for row, label in zip(rows, LABELS):
        controls(row, label, prune_timeout)
        if "76160" in label:
            without_b = row["pattern"].replace("(?b)", "", 1)
            single_search(row, without_b, "no (?b), (*SKIP)")
            single_search(row, without_b.replace("(*SKIP)", "(*PRUNE)"), "no (?b), (*PRUNE)")
            # 4 and 7 are the span this port answers on the drawn row, in codepoints.
            anchored_door(row, 4, 7)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
