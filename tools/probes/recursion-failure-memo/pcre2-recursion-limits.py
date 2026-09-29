"""How PCRE2 bounds an exponential recursive backtracking search (2026-09-27).

For docs/plan/2026-09-27-recursion-failure-memo-design.md. PCRE2 has no fuzzy matching, so the
shape is the nearest exact one: a recursion inside a nested repeat that cannot match, which makes
a backtracking matcher try every way of splitting the run. PCRE2 does not memoise; it counts, and
stops with an error (match limit, default 10 million) rather than answering.

    python tools/probes/recursion-failure-memo/pcre2-recursion-limits.py
"""
import time

import pcre2

print("pcre2 binding", getattr(pcre2, "__version__", "?"))
# The start verbs switch off the required-character check that would otherwise fail at once.
PATTERN = r"(*NO_START_OPT)(*NO_AUTO_POSSESS)(?:(?:a|a)+(?R)?)+c"
for n in (10, 14, 18, 22, 26, 30):
    subject = "a" * n
    t = time.perf_counter()
    try:
        m = pcre2.compile(PATTERN, jit=False).match(subject)
        answer = "match" if m else "no match"
    except Exception as e:  # noqa: BLE001 - the error class is the finding
        answer = f"{type(e).__name__}: {e}"
    print(f"n={n:<3} {(time.perf_counter() - t) * 1000:9.1f} ms  {answer}")
