"""How far past its budget is each `timeout` shape still running, on UPSTREAM?

S52 sitting 14. The `timeout` generator is the only one whose recorded `timeout` outcome the
consumer COMPARES rather than skips, and the whole licence for that is the margin: upstream's own
deadline is otherwise a fact about the recording machine's wall clock, so a row that needed eleven
seconds and a row that needed nine would record differently on two runs and neither would say
anything about the engine. A shape still running after twenty times the budget says something about
the engine.

This half measures upstream. Run `tools/probes/timeout-row-margin.ps1` for this port's half;
comparing one engine's timings against nothing says only that it is slow, which is S52 sitting 5's
lesson stated in a different place.

    python tools/probes/timeout-row-margin.py           # the shapes, at the generator's own length
    python tools/probes/timeout-row-margin.py --operations   # every shape against every operation
    python tools/probes/timeout-row-margin.py --knee    # where each shape stops being catastrophic
    python tools/probes/timeout-row-margin.py --rejected     # the shapes deliberately NOT in it

Every mode exits non-zero on its own bad news, so a caller that reads only the status cannot be told
the family is healthy by a report that says it is not.

THE NEGATIVE HALF IS THE INTERESTING ONE and `--rejected` prints it: the textbook catastrophic
patterns do NOT blow up on this engine, so a reader who assumes `(a+)+$` belongs in the family and
edits the table will produce rows that answer - and a timeout row that answers is a divergence.
"""

import argparse
import importlib.util
import pathlib
import sys
import time

import regex

# The generator's own table, its own budget and its own length, read out of the recorder rather than
# restated here. A probe holding its own copy measures whatever it was written against and goes on
# reporting a healthy margin after the generator has drifted away from it, which is the one failure
# this probe exists to catch.
_spec = importlib.util.spec_from_file_location(
    "record_oracle", pathlib.Path(__file__).resolve().parent.parent / "record-oracle.py"
)
_recorder = importlib.util.module_from_spec(_spec)
sys.modules["record_oracle"] = _recorder
_spec.loader.exec_module(_recorder)

SHAPES = _recorder.TIMEOUT_SHAPES

# Shapes a reader would expect to be here and which are NOT catastrophic on this engine, with what
# they actually answer. Printed by `--rejected` so the exclusion is evidence rather than folklore.
REJECTED = (
    ("nested-plus", r"(a+)+$", "the textbook one; a nested repeat over a one-character body collapses"),
    ("nested-star", r"(a*)*$", "likewise, and it matches at once"),
    ("dot-star-pair", r"(.*,)*z", "the textbook comma one"),
    ("backref-nested", r"(a+)+\1$", "a backreference does not rescue a collapsed repeat"),
    ("lookahead-nested", r"(?=(a+)+$)a", "nor does a lookahead"),
    ("alt-pair", r"(?:ab|a)+$", "the branches are NOT ambiguous over this subject"),
    ("alt-class", r"(?:[ab]|a)+$", "a class branch and a literal branch fold together"),
    ("alt-same-ci", r"(?i)(a|A)+$", "IGNORECASE removes the ambiguity rather than adding to it"),
    ("alt-same-rev", r"(?r)(a|a)+^", "reversed, the same ambiguity does not blow up"),
)

BUDGET = _recorder.TIMEOUT_ROW_BUDGET_SECONDS
LENGTH = _recorder.MIN_TIMEOUT_REPEATS  # the SHORTEST subject the generator draws, so the worst case
MARGIN = 20  # what the consumer's licence rests on: still running after MARGIN * BUDGET

OPERATIONS = _recorder.ALL_OPERATIONS


def ask(compiled, operation, subject, budget):
    """Puts one operation to upstream under a deadline, exactly as the recorder does."""
    if operation == "sub":
        return compiled.subn("z", subject, count=0, timeout=budget)
    if operation == "subf":
        return compiled.subfn("z", subject, count=0, timeout=budget)
    if operation == "split":
        return compiled.split(subject, maxsplit=0, timeout=budget)
    if operation in ("finditer", "finditer-overlapped"):
        return list(compiled.finditer(subject, overlapped=operation.endswith("overlapped"), timeout=budget))
    return getattr(compiled, operation)(subject, timeout=budget)


def still_running(pattern, operation, subject, budget):
    """(ran_out, seconds) - did it use the whole budget, and how long did the call take?"""
    compiled = regex.compile(pattern)
    started = time.perf_counter()
    try:
        ask(compiled, operation, subject, budget)
    except TimeoutError:
        return True, time.perf_counter() - started
    except Exception as e:  # noqa: BLE001
        return f"{type(e).__name__}", time.perf_counter() - started
    return False, time.perf_counter() - started


def subject_of(length):
    return "a" * length + "!"


def report_margin(length, margin):
    confirm = BUDGET * margin
    print(f"regex {regex.__version__}; subject {length} 'a's and a '!'; budget {BUDGET}s")
    print(f"still running after {confirm}s, which is {margin}x the budget?\n")
    print(f"{'shape':<22} {'at budget':>10} {'at ' + str(confirm) + 's':>12}   pattern")
    drifted = []
    for label, pattern in SHAPES:
        at_budget, _ = still_running(pattern, "search", subject_of(length), BUDGET)
        at_confirm, _ = still_running(pattern, "search", subject_of(length), confirm)
        if at_confirm is not True:
            drifted.append(label)
        print(f"{label:<22} {_cell(at_budget):>10} {_cell(at_confirm):>12}   {pattern}")
    print()
    if not drifted:
        print(f"ALL {len(SHAPES)} SHAPES are still running at {margin}x the budget.")
    else:
        print(f"*** {', '.join(drifted)} FINISHED inside {confirm}s - the family has drifted, redraw the table. ***")
    return not drifted


def _cell(value):
    return "running" if value is True else ("finished" if value is False else value)


def report_operations(length, margin):
    confirm = BUDGET * margin
    subject = subject_of(length)
    print(f"regex {regex.__version__}; subject {length} 'a's and a '!'; still running after {confirm}s?\n")
    # '-overlapped' shortened rather than truncated, and the same spelling as the port half's, so the
    # two matrices can be read side by side: at any sane column width 'finditer' and
    # 'finditer-overlapped' both cut to 'finditer'.
    print(f"{'shape':<22} " + " ".join(f"{o.replace('-overlapped', '-ovl'):>11}" for o in OPERATIONS))
    cells_run = 0
    cells_ok = 0
    for label, pattern in SHAPES:
        row = []
        for operation in OPERATIONS:
            ran_out, _ = still_running(pattern, operation, subject, confirm)
            cells_run += 1
            cells_ok += 1 if ran_out is True else 0
            row.append(f"{_cell(ran_out):>11}")
        print(f"{label:<22} " + " ".join(row))
    print(f"\n{cells_ok} of {cells_run} cells still running at {margin}x the budget.")
    return cells_ok == cells_run


def report_knee(margin):
    """Where each shape stops being catastrophic, which is what sets MIN_TIMEOUT_REPEATS."""
    confirm = BUDGET * margin
    print(f"regex {regex.__version__}; shortest subject still running after {confirm}s\n")
    print(f"{'shape':<22} {'knee':>6}")
    above = []
    for label, pattern in SHAPES:
        knee = None
        for length in range(8, LENGTH + 1, 4):
            ran_out, _ = still_running(pattern, "search", subject_of(length), confirm)
            if ran_out is True:
                knee = length
                break
        if knee is None:
            above.append(label)
        print(f"{label:<22} {knee if knee is not None else '>' + str(LENGTH):>6}")
    print(f"\nMIN_TIMEOUT_REPEATS is {LENGTH}; every knee above must be at or below it.")
    if above:
        print(f"*** {', '.join(above)} has NO knee at or below {LENGTH} - raise MIN_TIMEOUT_REPEATS "
              "or drop the shape. ***")
    return not above


def report_rejected(length, margin):
    confirm = BUDGET * margin
    print(f"regex {regex.__version__}; shapes NOT in the family, subject {length} 'a's and a '!'\n")
    print(f"{'shape':<22} {'at ' + str(confirm) + 's':>12}   why it is out")
    catastrophic = []
    for label, pattern, why in REJECTED:
        subject = ("a," * length) if label == "dot-star-pair" else subject_of(length)
        ran_out, elapsed = still_running(pattern, "search", subject, confirm)
        verdict = _cell(ran_out) if ran_out is not False else f"{elapsed * 1000:.0f}ms"
        print(f"{label:<22} {verdict:>12}   {why}")
        if ran_out is True:
            catastrophic.append(label)
            print(f"    *** {label} IS catastrophic now - it belongs in TIMEOUT_SHAPES. ***")
    return not catastrophic


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--length", type=int, default=LENGTH, help=f"subject length (default {LENGTH})")
    parser.add_argument("--margin", type=int, default=MARGIN, help=f"multiple of the budget (default {MARGIN})")
    parser.add_argument("--operations", action="store_true", help="every shape against every operation")
    parser.add_argument("--knee", action="store_true", help="the shortest subject each shape blows up on")
    parser.add_argument("--rejected", action="store_true", help="the shapes deliberately NOT in the family")
    args = parser.parse_args()

    # Every mode exits non-zero on its own bad news, so a caller that only reads the status - CI, a
    # `&&` chain - cannot be told the family is healthy by a report that says it is not. The first
    # draft exited 0 from `--knee` and `--rejected` however loudly they printed; the blind review
    # caught it by planting a catastrophic shape in REJECTED and watching the probe exit 0.
    if args.knee:
        raise SystemExit(0 if report_knee(args.margin) else 1)
    if args.rejected:
        raise SystemExit(0 if report_rejected(args.length, args.margin) else 1)
    if args.operations:
        raise SystemExit(0 if report_operations(args.length, args.margin) else 1)
    raise SystemExit(0 if report_margin(args.length, args.margin) else 1)
