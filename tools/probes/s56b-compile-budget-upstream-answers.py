"""Upstream answers for the patterns S56b's compile-budget tests use.

Upstream has no compile budget, so the budget's own behaviour has no upstream answer; what this
probe pins is the *matching* behaviour of the large-but-legal patterns the budget must keep
admitting, so the gap tests' expected values carry provenance (the S52c rule).

Run:  python tools/probes/s56b-compile-budget-upstream-answers.py

Memory rule (S56b, owner 2026-09-18): every pattern here is bounded - the largest unrolls about
100,000 copies, about 25 MB - and every subject is under 100 characters. Do not add a nested
counted repeat whose counts multiply past that without a hard memory limit on the process.
"""

import sys
import time

import regex

# 1 GB address-space cap, so a mistake here fails as a MemoryError rather than taking the machine
# down. `resource` is Unix-only; on Windows the cases below are small enough to run uncapped, and
# the same script under WSL gets the cap.
try:
    import resource

    resource.setrlimit(resource.RLIMIT_AS, (1 << 30, 1 << 30))
except (ImportError, ValueError, OSError):
    print("note: RLIMIT_AS not available on this platform, running without the cap")

CASES = [
    # (pattern, subject, call)
    (r"(a{100000})?b", "b", "match"),
    (r"(a{1000}){100}", "a" * 99, "match"),
    (r"a{200}", "a" * 99, "match"),
    (r"a{50}", "a" * 99, "match"),
]


def main() -> int:
    print(f"regex {regex.__version__}, Python {sys.version.split()[0]}")
    for pattern, subject, call in CASES:
        started = time.perf_counter()
        compiled = regex.compile(pattern)
        elapsed = (time.perf_counter() - started) * 1000
        m = getattr(compiled, call)(subject)
        answer = "None" if m is None else f"span={m.span()} groups={m.groups()}"
        print(f"{pattern!r}.{call}({subject[:12]!r}{'...' if len(subject) > 12 else ''}) -> {answer}"
              f"   [compile {elapsed:.0f} ms]")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
