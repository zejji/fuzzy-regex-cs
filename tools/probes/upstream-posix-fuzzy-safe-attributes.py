"""Which reads of a faulting POSIX-plus-fuzzy match are safe, one child process per read.

Ledger entry 9 says only ``Match.fuzzy_changes`` faults. The S46 POSIX design rests on that
being true of **every** access ``tools/record-oracle.py`` makes, not just of the ones an
earlier probe happened to try: ``_describe_match`` also reads ``span(n)`` and ``spans(n)``
for every group, ``lastindex``, ``lastgroup`` and ``partial``, and the iteration and
substitution doors walk a scanner before any of that. A single unsafe access among them
kills the recorder rather than failing a test, so each one is measured here.

The pattern HAS TWO GROUPS, one of which takes no part in the match, because the reads that
matter are over the whole group range and a pattern with no groups measures only group 0.
S46's blind review found the first version of this probe asking about a groupless pattern
while three code comments cited it for a claim about every group.

Every case runs in its own child, so a fault is an exit code and the whole table prints. A
fault and an ordinary exception are told apart rather than both printed as a death: only a
negative return code, or a Windows 0xC0000005, is a process killed by the access violation.
The subject is ``'axc'`` against ``(?p)(?P<g1>(?:abc){e<=1})(?P<g2>d)?`` - entry 9's
substitution row wrapped in groups, which spends an error and therefore faults on
``fuzzy_changes``.

Run it:

    python tools/probes/upstream-posix-fuzzy-safe-attributes.py

Measured 2026-09-14 on regex 2026.9.10: every read is safe except ``fuzzy_changes``.
"""

import subprocess
import sys

PATTERN = r"(?p)(?P<g1>(?:abc){e<=1})(?P<g2>d)?"
SUBJECT = "axc"

# Each entry is one expression evaluated on the match (or on the compiled pattern, for the
# scanner doors). Every one is a read `tools/record-oracle.py` really makes.
READS = [
    "m.span(0)",
    "[m.span(n) for n in range(c.groups + 1)]",
    "[m.spans(n) for n in range(c.groups + 1)]",
    "m.lastindex",
    "m.lastgroup",
    "m.partial",
    "m.fuzzy_counts",
    "m.fuzzy_changes",
    # The doors that walk a scanner rather than answering one match. None of these reads a
    # match attribute, so all are expected safe - but 'expected' is the word this probe exists
    # to replace.
    "len(list(c.finditer(s)))",
    "len(list(c.finditer(s, overlapped=True)))",
    "c.subn('X', s)",
    "c.split(s)",
    # And the scanner walk followed by the per-match reads the recorder really does.
    "[(x.span(0), x.fuzzy_counts) for x in c.finditer(s)]",
]

CHILD = r"""
import sys, regex
expression = sys.argv[1]
c = regex.compile(sys.argv[2])
s = sys.argv[3]
m = c.search(s)
print(repr(eval(expression)), flush=True)
"""

# Windows reports an access violation as this unsigned status; POSIX shells report a signal
# death as a negative return code. Anything else is an ordinary Python exception, which is an
# answer rather than a fault.
ACCESS_VIOLATION = 0xC0000005


def verdict(returncode: int) -> str:
    if returncode == 0:
        return "ok    "
    if returncode < 0 or returncode == ACCESS_VIOLATION:
        return f"*** FAULT rc={returncode} ***"
    return f"raised (rc={returncode})"


if __name__ == "__main__":
    import regex

    print("regex", regex.__version__, "| python", sys.version.split()[0])
    print(f"pattern {PATTERN!r} subject {SUBJECT!r}")
    print()

    for expression in READS:
        done = subprocess.run(
            [sys.executable, "-c", CHILD, expression, PATTERN, SUBJECT],
            capture_output=True,
            text=True,
        )
        answered = " ".join(done.stdout.split()) or " ".join(done.stderr.split())[-80:]
        print(f"{expression:46} {verdict(done.returncode)}  {answered}")
