"""What upstream does when a group call re-enters the position it is already at.

Ledger entry 14, and the evidence S47 sitting 3 ported PCRE2's guard on. PCRE2 refuses this shape
with `PCRE2_ERROR_RECURSELOOP`, "nested recursion at the same subject position", in microseconds -
see `tools/probes/pcre2-bounds-an-unbounded-recursion.py`, which runs the same patterns there.
Upstream has no guard at all.

Three tables:

1. The shapes PCRE2 refuses, PLUS the left-recursive and lazy variants a positional guard could
   plausibly change the answer of. The question they settle is whether a guard costs an answer:
   it cannot, because upstream has none to lose - every one raises MemoryError.
2. The recursions that MUST consume, which no guard may touch. Upstream answers all of them at
   once, and so does this port.
3. Oracle row 72179 (`interactions`, seed 20260914, 6000 rows), minimised. Upstream answers
   `no match` there - but only because its required-string prefilter rejects the subject before
   the engine runs. Put the required character into the subject and it blows up like the rest.

Run: `python tools/probes/upstream-same-position-reentry.py`
"""

import sys
import time

import regex

REFUSED_BY_PCRE2 = [
    (r"(?P<g1>(?:a?)(?&g1)?)", "aaaa", "search"),
    (r"(?P<g1>(?:a*)(?&g1)?)", "aaaa", "search"),
    (r"(?P<g1>(?:ab)?(?&g1)?)", "abab", "search"),
    (r"(?:(?R))", "ab", "search"),
    # Not PCRE2's own rows: left recursion, a lazy body, and a call in both branches. All three are
    # shapes where a POSITIONAL guard refuses a path a progress proof might have allowed.
    (r"(?P<g>(?&g)a|b)", "ba", "search"),
    (r"(?P<g1>(?:ab)??(?&g1)?)", "abab", "search"),
    (r"(?P<g1>x(?&g1)?|(?&g1)?y)", "xxy", "search"),
]

MUST_CONSUME = [
    (r"(?:a(?R)?b)", "aabb", "search"),
    (r"(?P<g1>a(?&g1)?b)", "aabb", "search"),
    (r"(?P<g1>(?:ab)(?&g1)?)", "abab", "search"),
]

WAVE_ROW = [
    (r"(?P<g2>(?:[A-Z]\d?){s<=1,i<=1,d<=1}(?P>g2)?)a", "B", "fullmatch"),
    (r"(?P<g2>(?:[A-Z]\d?){s<=1,i<=1,d<=1}(?P>g2)?)a", "Ba", "fullmatch"),
    (r"(?P<g2>(?:[A-Z]\d?){s<=1,i<=1,d<=1}(?P>g2)?)a", "Ba", "search"),
    (r"(?P<g2>(?:[A-Z]\d?){s<=1,i<=1,d<=1}(?P>g2)?)", "B", "fullmatch"),
]


def run(title: str, cases: list[tuple[str, str, str]]) -> None:
    print(f"\n{title}")
    for pattern, subject, operation in cases:
        start = time.perf_counter()
        try:
            match = getattr(regex, operation)(pattern, subject, timeout=10)
            answer = f"{match.span()} {match.groups()!r}" if match else "no match"
        except Exception as error:  # noqa: BLE001 - MemoryError and TimeoutError are both answers
            answer = f"{type(error).__name__}: {error}"
        print(f"  {operation:9} {subject!r:8} {time.perf_counter() - start:6.2f}s  {answer:24} {pattern}")


print("regex", regex.__version__, "| python", sys.version.split()[0])
run("A call re-entering the position it is already at - PCRE2 refuses each of these:", REFUSED_BY_PCRE2)
run("The same recursions made to consume, which no guard may touch:", MUST_CONSUME)
run("Oracle row 72179 minimised, and what upstream's prefilter is hiding:", WAVE_ROW)
