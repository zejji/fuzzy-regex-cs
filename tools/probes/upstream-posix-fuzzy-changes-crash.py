r"""Upstream faults the interpreter reading `fuzzy_changes` for a POSIX match that spent an error.

S43, found by the composed `interactions` wave the Phase 5 close added. Reading
``match.fuzzy_changes`` raises no exception: it kills the process with an access violation
(``0xC0000005`` on Windows, SIGSEGV elsewhere), so no ``except`` clause can see it and the recorder
cannot write such a row down at any wave size. It is a new memory-safety bug of the same family as
upstream issues 611-614, the 2026 fuzzing campaign.

THE CONDITION IS TWO THINGS AND NOTHING ELSE, and the table below is the evidence for both halves:

  * POSIX - as ``(?p)`` or as the flag; and
  * a fuzzy match that actually SPENT an error.

Any error kind does it - substitution, insertion or deletion alike. No alternation is needed; a
first draft of this probe thought one was, because every negative control it happened to try was
also a zero-error match. Every row whose ``fuzzy_counts`` are ``(0, 0, 0)`` is safe.

**"AND NOTHING ELSE" IS TOO STRONG, MEASURED S52 SITTING 15, AND THE CONDITION IS OPEN.** Every case
below still answers exactly as it did. But three rows of
``tools/probes/sweep-divergence-rows.jsonl`` have ablations that carry POSIX, spend an error and DO
NOT fault: row 4's
``(*PRUNE)`` ablation (compiled flags ``0x1b42a``, a partial match, counts ``(0, 1, 0)``, changes
``([], [1], [])``), row 18's ``(?b)``-free ablation (a complete ``fullmatch``, counts ``(2, 2, 1)``,
changes ``([0, 1, 2], [3, 1], [])``), and **row 32, which gives a safe pair and a faulting one from
ONE pattern** - its ``as drawn`` and ``(*PRUNE)`` spellings both answer their changes, and only the
verb-deleted spelling faults. So neither "partial versus complete", nor the kind of error, nor the
pattern separates them, and what the real condition is remains UNKNOWN. See all three by removing
the POSIX guard in ``gate-divergence-doors.py``'s ``describe`` and running it over that rows file;
S52 sitting 15's notes carry it as Control A (the slice file is
``docs/plan/slices/S52-oracle-hardening.md`` while S52 is open, and moves under ``done/`` when it
closes).

Nothing rests on the wide statement: ``record-oracle.py:1019`` and ``gate-divergence-doors.py`` both
guard on POSIX ALONE, which is conservative in the safe direction and cannot crash whatever the
condition turns out to be.

``m.span()`` and ``m.fuzzy_counts`` answer correctly on the very same match - the child below prints
them BEFORE touching the changes, which is why the crashing rows still report them. That is what
makes this port's expected answer fully determined rather than derived: upstream gives the span and
the counts itself, and the same pattern without ``(?p)`` gives the changes.

The mechanism is very likely ``restore_best_match`` putting back the fuzzy COUNTS and not the
changes list - POSIX is the one path that keeps a candidate aside and copies it back
(``check_posix_match``, upstream/src/_regex.c:11602), and a zero-error candidate has no change to
leave dangling. That is a hypothesis with the right shape, not a measurement; Phase 6's upstream
report owns confirming it in the C.

THIS PORT IS RIGHT AND ANSWERS ALL OF IT. Pinned in Gaps/Engine/FuzzyPosixTests.cs.

Measured 2026-09-13 against regex 2026.7.19; the two "nested fuzzy, reversed, astral" cases (seventh
and eighth of the sixteen, beside the other faulting rows rather than at the end) and the narrowing
above were measured 2026-09-15 against regex 2026.9.10, on which every earlier case still answers as
its label says.
"""

import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent

# (pattern, flags, subject, what the row is for)
CASES = [
    # POSIX and an error spent: faults, whichever kind of error it is.
    (r"(?p)(?:abc){e<=1}", 0, "axc", "POSIX, 1 substitution - FAULTS"),
    (r"(?p)(?:abc){s<=1}", 0, "axc", "POSIX, substitution budget - FAULTS"),
    (r"(?p)(?:abc){d<=1}", 0, "ac", "POSIX, 1 deletion - FAULTS"),
    (r"(?p)(?:abc){i<=1}", 0, "abxc", "POSIX, 1 insertion - FAULTS"),
    (r"(?p)(?:a|aa){e<=1}", 0, "aa", "POSIX, an alternation, 1 insertion - FAULTS"),
    (r"(?:abc){e<=1}", 0x10000, "axc", "POSIX as the FLAG rather than inline - FAULTS"),
    # S52 sitting 15, reached from the opposite direction: a REVERSED pattern with one fuzzy section
    # nested inside another, drawn by `interactions` rather than written by hand. It is row 32 of
    # `sweep-divergence-rows.jsonl` with its `(*SKIP)` deleted, reduced to the inner section, and it
    # is here with its POSIX-free twin because that pair is the cleanest thing this file has: the
    # SAME pattern and subject, the one bit changed, crash against answer.
    (r"(?r)(?:\U00010428\U00010428(?:\U00010400){1i+2d+1s<=3}){1i+2d+1s<=3}", 0x10000,
     "\U00010400a\U00010428\U00010428\U00010428", "POSIX, nested fuzzy, reversed, astral - FAULTS"),
    (r"(?r)(?:\U00010428\U00010428(?:\U00010400){1i+2d+1s<=3}){1i+2d+1s<=3}", 0,
     "\U00010400a\U00010428\U00010428\U00010428", "the same, POSIX bit cleared - safe"),
    # POSIX and no error spent: safe. This is the half a first draft got wrong.
    (r"(?p)(?:abc){e<=1}", 0, "abc", "POSIX, 0 errors - safe"),
    (r"(?p)(?:aa|a){e<=1}", 0, "aa", "POSIX, an alternation, 0 errors - safe"),
    (r"(?p)(?:a|b){e<=1}", 0, "a", "POSIX, equal-length branches, 0 errors - safe"),
    (r"(?p)(?:a|aa)", 0, "aa", "POSIX, no fuzzy section - safe"),
    (r"(?p)abc", 0, "abc", "POSIX, a plain literal - safe"),
    # An error spent and NO POSIX: safe, and it is where the expected changes come from.
    (r"(?:abc){e<=1}", 0, "axc", "no POSIX, 1 substitution - safe"),
    (r"(?:abc){d<=1}", 0, "ac", "no POSIX, 1 deletion - safe"),
    (r"(?:abc){i<=1}", 0, "abxc", "no POSIX, 1 insertion - safe"),
]

CHILD = HERE / "_posix_fuzzy_changes_child.py"
CHILD.write_text(
    "import sys, json, regex\n"
    "pattern, flags, subject = json.loads(sys.argv[1])\n"
    "m = regex.compile(pattern, flags).match(subject, timeout=10.0)\n"
    "if m is None:\n"
    "    sys.stdout.write('nomatch'); raise SystemExit\n"
    # Printed and FLUSHED before the changes are touched, so a faulting row still reports them.
    "sys.stdout.write(f'span {m.span()} counts {tuple(m.fuzzy_counts)} | ')\n"
    "sys.stdout.flush()\n"
    "sys.stdout.write(f'changes {tuple(tuple(p) for p in m.fuzzy_changes)}')\n",
    encoding="utf-8",
)

try:
    for pattern, flags, subject, why in CASES:
        try:
            done = subprocess.run(
                [sys.executable, str(CHILD), json.dumps([pattern, flags, subject])],
                capture_output=True, text=True, timeout=60,
            )
            answer = done.stdout.strip()
            if done.returncode != 0:
                answer = (
                    f"{answer}  *** CRASH rc=0x{done.returncode & 0xFFFFFFFF:08X} ***"
                    if answer
                    else f"*** CRASH rc=0x{done.returncode & 0xFFFFFFFF:08X} ***"
                )
        except subprocess.TimeoutExpired:
            answer = "*** harness timeout ***"
        line = f"{pattern!r:24} flags={flags:<3} {subject!r:7} {answer}\n    {why}"
        sys.stdout.buffer.write(line.encode("utf-8", "backslashreplace") + b"\n")
finally:
    CHILD.unlink(missing_ok=True)
