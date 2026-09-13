"""Upstream exhausts memory on a self-recursive call whose fuzzy section can match the empty string.

S43, found while composing fuzzy into the `interactions` generator at the Phase 5 close. Upstream's
551/554 resource-blowup family, reached by a shape no earlier wave could draw.

THE RULE, which accounts for most of the rows below: a group that calls ITSELF makes progress only
if its body must consume something. A fuzzy section can match the empty string whenever its budget
permits as many DELETIONS as the section has atoms, so a budget that reaches n deletions of an
n-atom section is the dangerous one; `{s<=n}` and `{i<=n}` never delete anything, whatever n is.
With nothing to force progress, upstream allocates until it raises MemoryError, in under a second.

IT IS A PREDICTOR AND NOT A PROOF. Over the eleven constraints `INTERACTION_FUZZY_CONSTRAINTS` can
draw, the rule accounts for nine; `{e<=2,i<=1}` and `{e<=2,s<=1}` cap the total at two, cap no
deletions, and are nevertheless safe in 0.00s. Why a compound constraint behaves differently has not
been established. The generator's exclusion rests on the measured table, not on the rule.

Whole-pattern recursion is the degenerate case: `(?:(?R)){e<=1}` is a section whose only content is
the recursion, so it matches empty at any budget - which is why `(?R)` and `(?0)` blow up even with
a base case, and why the identical recursion WITHOUT a fuzzy section answers instantly.

Each case runs in its own subprocess: MemoryError is catchable, but a case that allocates hard is
better contained.

THIS PORT REPRODUCES THE BLOWUP FAITHFULLY AND SAFELY, raising
`InvalidOperationException: the regular expression engine's backtracking stack exceeded its 1GB
limit` where upstream raises MemoryError. It is an inherited bug, not one of this port's own.
Pinned in tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyRecursionTests.cs.

A guard that forces progress is NOT sufficient to make the shape safe to generate, and that is the
finding that kept it out of the wave: `(?P<g1>A(?:Ab){C}(?&g1)?)` is safe for every constraint tried
here, yet a 600-row wave of real drawn rows still raised MemoryError on four of them. Progress
bounds the DEPTH and does nothing about the BRANCHING, and a fuzzy section offers a fresh
insert/delete/substitute choice at every position of every level.

Measured 2026-09-13 against regex 2026.7.19.
"""

import json
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent

CASES = [
    # Whole-pattern recursion inside a fuzzy section: always blows up.
    (r"(?:(?R)){e<=1}", "ab"),
    (r"(?:a(?R)?b){e<=1}", "aabb"),
    (r"a(?:(?0)){e<=1}b", "aabb"),
    (r"(?:(?R)){s<=1}", "ab"),
    (r"(?=(?:(?R)){e<=1})a", "ab"),
    # The same recursion with NO fuzzy section: instant.
    (r"(?:a(?R)?b)", "aabb"),
    # A self-recursive NAMED call, over EVERY constraint INTERACTION_FUZZY_CONSTRAINTS can draw.
    # Two atoms - three of the eleven blow up:
    (r"(?P<g1>(?:Ab){e<=1}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){e<=2}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){s<=1}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){i<=1}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){d<=1}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){e<=2,i<=1}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){e<=2,s<=1}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){s<=1,i<=1,d<=1}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){1<=e<=2}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){1i+2d+1s<=3}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Ab){2i+1d+1s<=2}(?&g1)?)", "AbAb"),
    # The two the rule mispredicts, one budget further out, plus the boundary that moves with the
    # atom count. Three atoms - none of the eleven blows up:
    (r"(?P<g1>(?:Ab){d<=2}(?&g1)?)", "AbAb"),
    (r"(?P<g1>(?:Abc){e<=2}(?&g1)?)", "AbcAbc"),
    (r"(?P<g1>(?:Abc){1<=e<=2}(?&g1)?)", "AbcAbc"),
    (r"(?P<g1>(?:Abc){2i+1d+1s<=2}(?&g1)?)", "AbcAbc"),
    (r"(?P<g1>(?:Abc){e<=3}(?&g1)?)", "AbcAbc"),
    # A call to a group that has already CLOSED is not recursion, and is safe.
    (r"(?P<g1>[ab]+)(?:(?&g1)c){e<=1}", "ababc"),
]

CHILD = HERE / "_fuzzy_recursion_child.py"
CHILD.write_text(
    "import sys, json, regex, time\n"
    "pattern, subject = json.loads(sys.argv[1])\n"
    "start = time.perf_counter()\n"
    "try:\n"
    "    m = regex.compile(pattern).search(subject, timeout=5.0)\n"
    "    out = {'span': m.span() if m else None}\n"
    "except Exception as exc:\n"
    "    out = {'exc': type(exc).__name__}\n"
    "out['seconds'] = round(time.perf_counter() - start, 2)\n"
    "sys.stdout.write(json.dumps(out))\n",
    encoding="utf-8",
)

try:
    for pattern, subject in CASES:
        start = time.perf_counter()
        try:
            done = subprocess.run(
                [sys.executable, str(CHILD), json.dumps([pattern, subject])],
                capture_output=True, text=True, timeout=90,
            )
            answer = done.stdout.strip() or f"*** rc=0x{done.returncode & 0xFFFFFFFF:08X} ***"
        except subprocess.TimeoutExpired:
            answer = "*** harness timeout ***"
        line = f"{pattern!r:38} {subject!r:8} wall={time.perf_counter() - start:5.2f}s {answer}"
        sys.stdout.buffer.write(line.encode("utf-8", "backslashreplace") + b"\n")
finally:
    CHILD.unlink(missing_ok=True)
