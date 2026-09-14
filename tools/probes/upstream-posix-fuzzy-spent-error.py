"""Ledger entry 9: which POSIX-plus-fuzzy rows kill the interpreter, on the pinned release.

Entry 9 states the rule as "every row whose `fuzzy_counts` are (0, 0, 0) is safe and every
row with a non-zero count faults", measured by S43 on `regex` 2026.7.19. S46 re-ran it on
the pinned 2026.9.10 on 2026-09-14 and the rule holds unchanged.

**The row worth carrying into the report is case 3.** `(?p)(?:abc){e<=1}` over `'abcd'`
LOOKS like an exact match and faults anyway, because POSIX leftmost-longest stretches it to
spend an insertion. That is the sharpest demonstration that no predicate is a safe test -
not over the pattern, and not over the subject either, which is what the `interactions`
generator's own suppression comment rests on.

Unlike `upstream-posix-fuzzy-crash.py` and `upstream-posix-fuzzy-changes-crash.py`, this
one **runs every case in its own child process**, so one fault does not take the rest of the
run with it and the whole table prints. The access it reproduces is reading
`Match.fuzzy_changes`, which is what `tools/record-oracle.py` does for every match it
records - and `record-oracle.py --rows` over a faulting row exits 139 and writes no output
file at all, which is why `_generate_interactions` suppresses POSIX on a fuzzy row.

Run it:

    python tools/probes/upstream-posix-fuzzy-spent-error.py

Expected on 2026.9.10: cases 1, 2, 5 and 6 `ok`, cases 3 and 4 `DIED`, and every faulting
case still printing its span and counts, because those are read before the changes are.
"""

import subprocess
import sys

CASES = [
    # POSIX, fuzzy, matched exactly: safe.
    (r"(?p)(?:abc){e<=1}", "abc", "match"),
    # Entry 9's own alternation case, which spends nothing: safe.
    (r"(?p)(?:aa|a){e<=1}", "aa", "match"),
    # THE ONE THAT MATTERS. Nothing in the pattern or the subject says "this will spend an
    # error"; leftmost-longest makes it spend one, and it faults.
    (r"(?p)(?:abc){e<=1}", "abcd", "search"),
    # A substitution it cannot avoid: faults, as entry 9 records.
    (r"(?p)(?:abc){e<=1}", "axc", "match"),
    # The two controls: remove the fuzzy section, or remove POSIX. Both safe.
    (r"(?p)(?:abc)", "abc", "match"),
    (r"(?:abc){e<=1}", "abc", "match"),
]

# Run in a child so a fault is an exit code rather than the end of this process. The span
# and the counts are printed and flushed BEFORE the changes are touched, which is what
# makes a faulting row still report them.
CHILD = r"""
import sys, regex
pattern, subject, op = sys.argv[1], sys.argv[2], sys.argv[3]
m = getattr(regex, op)(pattern, subject)
if m is None:
    print("None", flush=True)
else:
    print(f"span={m.span()} counts={m.fuzzy_counts}", flush=True)
    print(f"changes={m.fuzzy_changes}", flush=True)
"""

if __name__ == "__main__":
    import regex

    print("regex", regex.__version__, "| python", sys.version.split()[0])
    print()

    for pattern, subject, op in CASES:
        done = subprocess.run(
            [sys.executable, "-c", CHILD, pattern, subject, op],
            capture_output=True,
            text=True,
        )
        answered = " ".join(done.stdout.split())
        verdict = "ok  " if done.returncode == 0 else f"*** DIED rc={done.returncode} ***"
        print(f"{op:7} {pattern!r:24} {subject!r:8} {verdict}  {answered}")
