"""Upstream's time and answer on the two recursion rows of the 2026-09-27 failure-memo design.

docs/plan/2026-09-27-recursion-failure-memo-design.md. Each subject prefix runs in its own
subprocess with a 60 s wall clock, because row A makes upstream allocate until MemoryError.

    python tools/probes/recursion-failure-memo/upstream.py
"""
import subprocess
import sys

ROWS = {
    "A": (r"(|)(?:(?:(?:(?:.)+((?:(?R)){2,}|)){2<=e<=3}(?=b))){1<=s<=1,1<=d<=2}", "baxbax", "search"),
    "B": (r"(?b)(?:(?:.(?:(?:(?:b)+(?R)||)){1<=e<=2}(?:c)*?){2<=d<=3})", "xxaxabxx", "fullmatch"),
}

CHILD = r"""
import sys, time, regex
pattern, subject, how = sys.argv[1:4]
p = regex.compile(pattern)
t = time.perf_counter()
try:
    m = getattr(p, how)(subject)
    answer = "no match" if m is None else f"{m.span()} {m.fuzzy_counts}"
except MemoryError:
    answer = "MemoryError"
print(f"{(time.perf_counter() - t) * 1000:.1f} ms  {answer}")
"""

if __name__ == "__main__":
    import regex
    print("regex", regex.__version__)
    for name, (pattern, subject, how) in ROWS.items():
        print(f"row {name} ({how}): {pattern}")
        for n in range(1, len(subject) + 1):
            try:
                out = subprocess.run([sys.executable, "-c", CHILD, pattern, subject[:n], how],
                                     capture_output=True, text=True, timeout=60).stdout.strip()
            except subprocess.TimeoutExpired:
                out = "timeout (60 s)"
            print(f"  {subject[:n]:<9} {out}")
