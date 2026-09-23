"""S87 (2026-09-23): upstream leaves `total_errors` stale when a fuzzy section is undone.

Ledger entry 32. END_FUZZY writes `state->total_errors` from the section's counts
(upstream/src/_regex.c:12484). When that total is over budget it backtracks without putting the
old total back, and the backtrack arm (:15569-15571) subtracts the counts and leaves the total
alone. A match found later through another branch then carries the old section's total, and
`do_best_fuzzy_match` reads it (:17653). If the stale total is no better than the last match's,
`start_pos` never moves and the search never ends.

Each line runs in a child process under a time limit, so a hang prints as HANGS.

    python tools/probes/s87-stale-total-errors.py
"""

import multiprocessing
import sys

import regex

LIMIT = 5

ROW_3752 = r"(?b)\b\K(?:(?:\U0001D7EEa(?:[[:alpha:]]+?){s<=1:\W}){s<=1,i<=1,d<=1}(*SKIP)\S|\S)"
ROW_3752_SUBJECT = "\U0001D7EE\r\n\U0001D518\U0001D518\U0001D518\rAa"

# (label, operation, pattern, subject, flags)
CASES = [
    ("the minimal hang", "search", r"(?b)(?:(?:a(?:x+?){s<=1}){e<=2}|2)", "2y", 0),
    ("constrained", "search", r"(?b)(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)", "2\n", 0),
    ("constrained, no (?b)", "search", r"(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)", "2\n", 0),
    ("minimal, no (?b)", "search", r"(?:(?:a(?:x+?){s<=1}){e<=2}|2)", "2y", 0),
    ("minimal, inner section removed", "search", r"(?b)(?:(?:a(?:x+?)){e<=2}|2)", "2y", 0),
    ("(?e)", "search", r"(?e)(?:(?:a(?:x+?){s<=1}){e<=2}|2)", "2y", 0),
    ("(?e), inner section removed", "search", r"(?e)(?:(?:a(?:x+?)){e<=2}|2)", "2y", 0),
    ("(?e), branches swapped", "search", r"(?e)(?:2|(?:a(?:x+?){s<=1}){e<=2})", "2y", 0),
    ("row 3752 as drawn", "split", ROW_3752, ROW_3752_SUBJECT, 0x400A),
    ("row 3752, (*PRUNE)", "split", ROW_3752.replace("(*SKIP)", "(*PRUNE)"), ROW_3752_SUBJECT, 0x400A),
    ("row 3752, verb deleted", "split", ROW_3752.replace("(*SKIP)", ""), ROW_3752_SUBJECT, 0x400A),
    ("tie, cost equation", "search", r"(?b)((?:abc){e<=2,2i+1d+3s<=4}(?1)?)", "bb", 0),
]


def run(operation, pattern, subject, flags):
    compiled = regex.compile(pattern, flags)
    if operation == "split":
        return ascii(compiled.split(subject))
    m = compiled.search(subject)
    return "None" if m is None else f"span={m.span()} fuzzy_counts={m.fuzzy_counts}"


def into(queue, args):
    queue.put(run(*args))


def bounded(args):
    queue = multiprocessing.Queue()
    child = multiprocessing.Process(target=into, args=(queue, args))
    child.start()
    child.join(LIMIT)
    if child.is_alive():
        child.terminate()
        child.join()
        return f"HANGS (no answer in {LIMIT} s)"
    return queue.get()


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")
    print(f"regex {regex.__version__}")
    for label, operation, pattern, subject, flags in CASES:
        print(f"{label:32} {operation}({ascii(pattern)}, {ascii(subject)}, {flags:#x})")
        print(f"{'':32}   -> {bounded((operation, pattern, subject, flags))}")
