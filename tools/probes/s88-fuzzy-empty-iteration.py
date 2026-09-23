"""S88 (2026-09-23): upstream repeats a fuzzy iteration that only deleted until MemoryError.

Ledger entry 33. A fuzzy edit bumps `capture_change` (upstream/src/_regex.c:10487), and the
repeat guards are off under fuzzy matching (:9596), so END_GREEDY_REPEAT (:12552) reads an
iteration that deleted its way through the body without moving as progress. Its one fuzzy
exception stops the repeat at the end of the slice. A fuzzy section inside the body starts each
iteration with a fresh budget, so with no maximum the repeat never stops anywhere else.

Each line runs in a child process under a time limit, so a hang prints as HANGS and a
MemoryError prints as its name.

    python tools/probes/s88-fuzzy-empty-iteration.py
"""

import multiprocessing
import sys

import regex

LIMIT = 20

# (label, pattern, subject[, flags])
CASES = [
    ("the S84 finding, V1", r"(?i)(x)(?:(?:\1){d<=2})+$", "xy"),
    ("the S84 finding, V0", r"(?i)(x)(?:(?:\1){d<=2})+$", "xy", regex.V0),
    ("without IgnoreCase", r"(x)(?:(?:\1){d<=2})+$", "xy"),
    ("a literal body", r"(x)(?:(?:x){d<=2})+$", "xy"),
    ("minimal", r"(?:(?:x){d<=1})+y", "y"),
    ("minimal, reversed", r"(?r)y(?:(?:x){d<=1})+", "y"),
    ("minimum 3", r"(?:(?:x){d<=1}){3,}y", "xy"),
    ("stop at the slice end", r"(?:(?:x){d<=1})+", ""),
    ("stop at the slice end, minimum 3", r"(?:(?:x){d<=1}){3,}", "x"),
    ("stop at the slice start, reversed", r"(?r)(?:(?:x){d<=1})+y", "y"),
    ("bounded", r"(?:(?:x){d<=1}){1,3}y", "y"),
    ("exactly 3", r"(?:(?:x){d<=1}){3,3}y", "xy"),
    ("lazy", r"(?:(?:x){d<=1})+?y", "y"),
    ("(?e)", r"(?e)(?:(?:x){d<=1})+y", "y"),
    ("(?b)", r"(?b)(?:(?:x){d<=1})+y", "y"),
    ("(?b), bounded", r"(?b)(?:(?:x){d<=1}){1,3}y", "y"),
    ("inside a section", r"(?:(?:(?:x){d<=1})+y){e<=5}", "y"),
    ("inside a section, charged outside", r"(?:(?:(?:x){d<=1}z)+y){d<=3}", "y"),
    ("repeat in a section, no inner section", r"(?:\d+a0b+?){d<=2}", "67a0bab"),
    ("a pass that only sets a group", r"(?:(?(1)c|z)|())*(?:d){e<=1}", "cd"),
    ("a pass that deletes and sets a group", r"(?:(?(1)c|z)|()(?:x){d<=1})*$", "c"),
    ("the same, group set in a lookahead", r"(?:(?(1)c|z)|(?=())(?:x){d<=1})*$", "c"),
    ("a group body that loops", r"(?:(?(1)c|z)|()(?:x){d<=1})+d", "cd"),
    ("a group call in the body", r"(?(DEFINE)(()))(?:(?(2)c|z)|(?1)(?:x){d<=1})*$", "c"),
]


def run(pattern, subject, flags):
    try:
        m = regex.search(pattern, subject, flags)
    except MemoryError:
        return "MemoryError"
    if m is None:
        return "None"
    return f"span={m.span()} fuzzy_counts={m.fuzzy_counts} fuzzy_changes={m.fuzzy_changes}"


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
    return queue.get() if not queue.empty() else f"child exited with {child.exitcode}"


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")
    print(f"regex {regex.__version__}, every search under regex.V1 unless it says V0")
    for label, pattern, subject, *flags in CASES:
        flags = flags[0] if flags else regex.V1
        print(f"{label:40} search({ascii(pattern)}, {ascii(subject)})")
        print(f"{'':40}   -> {bounded((pattern, subject, flags))}")
