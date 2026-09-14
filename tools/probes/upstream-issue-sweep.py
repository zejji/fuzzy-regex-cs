"""Every class C case of the S49 upstream issue sweep, run against `regex` 2026.9.10.

S49 (2026-09-14) re-triaged the live tracker snapshot in
`docs/plan/upstream-issues/2026-09-14-open.json` (74 open issues) and reproduced or dismissed each
issue that claims an engine or parser bug. This file is the upstream half of that evidence; the
port half is `tools/probes/port-issue-sweep.ps1` and the second-engine half is
`tools/probes/pcre2-partial-truncation-assertions.py`. The verdicts are in
`docs/plan/upstream-issues/2026-09-14-triage.md`.

    python tools/probes/upstream-issue-sweep.py

It inserts the venv's site-packages on `sys.path` rather than running its interpreter, because the
unattended sandbox allows `python` and not `.venvs/*/Scripts/python`. Same CPython, so the
extension loads. This is the convention every `upstream-*.py` probe here uses.

WHAT IT MEASURED on 2026-09-14, regex 2026.9.10, CPython 3.14, Windows:

  334  NO LONGER REPRODUCES. The maintainer's own reduction and the reporter's full pattern both
       answer None / no matches, in milliseconds. The 2019 report was a crash.
  367  Reproduces - and it is NOT a bug. PCRE2 10.47 answers PARTIAL on the identical rows
       (the pcre2 probe above), so "a partial match does not prove a completion exists" is what
       every engine does. Upstream's own documentation promises more than any engine delivers
       (`upstream/docs/Features.html:576`), which makes this a documentation gap.
  397  Reproduces: MemoryError in about 0.7s, for BOTH of the reporter's patterns - including the
       one the 2021 reporter said did not crash. This port answers None instead, because S47's
       ledger-entry-14 fix bounds exactly this recursion.
  425  Reproduces. In `(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))` the second branch gives BOTH of its
       groups number 1, so 'BUG' is unrecoverable. Every rule the maintainer floated in the issue
       numbers consecutively within a branch, so the current behaviour matches none of them.
  470  Reproduces - and this port already diverges deliberately (DIVERGENCES.md, shipped S41/S42).
  551  NO LONGER REPRODUCES. All three of the reporter's flag combinations answer None in 0.00s.
       The maintainer's "I've done a partial fix" (2025-02-10) has landed.
  554  Reproduces, and bisecting it is the useful part: `regex` succeeds to n=6,000,000 and raises
       MemoryError at n=10,000,000, where stdlib `re` succeeds. Upstream costs 161-192 bytes per
       repetition against `re`'s 92-99 - roughly twice, and flat in n on both.
  563  Reproduces exactly as reported.
  564  Reproduces exactly as reported.
  589  Reproduces. Upstream answers None where a completion ('Truest') demonstrably exists, which
       contradicts its own documented definition of a partial match, and PCRE2 answers PARTIAL on
       the same row under both SOFT and HARD.
  596  NO LONGER REPRODUCES. Across runs `{e<=0}` costs 2.16-2.53 us/search against the plain
       literal's 2.35-2.50 - a ratio at or below 1.0, not the reported 210. The harness is
       calibrated: it measures the plain literal at 2.35-2.50 us against the reporter's own
       2.88 us, so it is measuring the same thing the report did. Quote the ratio, not the
       absolute microseconds, which move a few percent between runs.
"""

import glob
import os
import re as stdlib_re
import sys
import time
import tracemalloc

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, glob.glob(os.path.join(_ROOT, ".venvs", "regex-2026.9.10", "Lib", "site-packages"))[0])

import regex  # noqa: E402 - must follow the sys.path insertion above


def show(label, thunk):
    """Runs one case and prints its answer, so an exception is evidence rather than a crash."""
    started = time.perf_counter()
    try:
        print(f"{label}\n    -> {thunk()!r}   [{time.perf_counter() - started:.2f}s]")
    except Exception as error:  # noqa: BLE001 - a MemoryError IS the measurement here
        print(f"{label}\n    -> {type(error).__name__}: {error}   [{time.perf_counter() - started:.2f}s]")


def span(match):
    if match is None:
        return None
    return (match.span(), match.group(), getattr(match, "partial", None))


print(f"regex {regex.__version__}  |  CPython {sys.version.split()[0]}")

print("\n=== 334: 'kernel crash on fuzzy regex search' - DOES NOT REPRODUCE")
show(r"  match(r'(?:(?=(e)?)\1){e<=1}', ' ')            # the maintainer's own reduction",
     lambda: regex.match(r'(?:(?=(e)?)\1){e<=1}', ' '))
show("  the reporter's full pattern, finditer('al, ')",
     lambda: list(regex.compile(
         r'(?e)(?:(?:(?=(?P<if_2_3>expression1\W+)?)(?P=if_2_3))?(?(if_2_3)expression2|expression3)){e<=1}',
         regex.IGNORECASE | regex.DOTALL | regex.ENHANCEMATCH).finditer('al, ')))

print("\n=== 367: partial matching assumes a lookahead is satisfiable - REPRODUCES, NOT A BUG")
for n in (2, 3, 4, 5, 9):
    show(rf"  match(r'(?!(1{{2,}})\1+$)((?:11)+)$', '1'*{n}, partial=True)",
         lambda n=n: span(regex.match(r'(?!(1{2,})\1+$)((?:11)+)$', '1' * n, partial=True)))
show(r"  match(r'(?!.+).*', '1', partial=True)   # the reporter's simplest form",
     lambda: span(regex.match(r'(?!.+).*', '1', partial=True)))
show(r"  match(r'(?!.+).*', '1')                 # and no completion exists",
     lambda: span(regex.match(r'(?!.+).*', '1')))

print("\n=== 397: a left-recursive DEFINE exhausts memory - REPRODUCES")
_DEFINE = r"(?(DEFINE)(?P<e>[cd]|(?&e))(?P<t>(?&e):(?&e)))"
show(f"  search({_DEFINE + '(?&t)'!r}, 'a[14]', S)",
     lambda: span(regex.search(_DEFINE + r"(?&t)", "a[14]", regex.S)))
show(f"  search({_DEFINE + '(?&e)+(?&e)'!r}, 'a[14]', S)   # the reporter said this one was fine",
     lambda: span(regex.search(_DEFINE + r"(?&e)+(?&e)", "a[14]", regex.S)))

print("\n=== 425: branch reset with mixed named and numbered groups - REPRODUCES")
for pattern in (r'(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))',
                r'(?|(?P<bug>xxx)(!)|(BUG)(?P<bug>!))',
                r'(?|(xxx)(?P<bug>!)|(?P<bug>BUG)(!))',
                r'(?|(xxx)(?P<bug>!)|(BUG)(?P<bug>!))'):
    def branch_reset(pattern=pattern):
        compiled = regex.compile(pattern)
        match = compiled.match("BUG!")
        if match is None:
            return None
        return dict(groups=match.groups(), bug=match.groupdict().get("bug"),
                    groupindex=dict(compiled.groupindex), ngroups=compiled.groups)
    show(f"  {pattern}  on 'BUG!'", branch_reset)

print("\n=== 470: BESTMATCH ignores the user's own costs - REPRODUCES (this port diverges by design)")
for budget in ("2", "1"):
    show(f"  search(r'(voices){{1i+1d+2s<={budget}}}', 'voixes voicees', BESTMATCH)",
         lambda budget=budget: (lambda m: (m.span(), m.group(), m.fuzzy_counts))(
             regex.search(rf'(voices){{1i+1d+2s<={budget}}}', 'voixes voicees', regex.BESTMATCH)))

print("\n=== 551: infinite loop on a V1 search - DOES NOT REPRODUCE")
_TEXT_551 = ("Yrkeshögskola . Studieämnen . Studieämnen . Studieämnen . Studieämnen . "
             "Studieämnen . Studieämnen . Studieämnen")
for name, flags in (("V1|I", regex.V1 | regex.IGNORECASE | regex.UNICODE),
                    ("V0|I", regex.V0 | regex.IGNORECASE | regex.UNICODE),
                    ("V0|I|FULLCASE", regex.V0 | regex.IGNORECASE | regex.FULLCASE | regex.UNICODE)):
    show(f"  compile(r'(Högskolan?)[\\s\\S]*([\\d,.]+)p', {name}).search(text)",
         lambda flags=flags: span(regex.compile(r"(Högskolan?)[\s\S]*([\d,.]+)p", flags).search(_TEXT_551)))

print("\n=== 554: excessive memory on fullmatch('(ab)*', 'ab'*n) - REPRODUCES, bisected")
# The per-repetition cost is the point of the entry, so it is measured here rather than quoted from
# a scratch script. The subject is built BEFORE tracemalloc.start(), so it is not traced and the
# peak is the engine's own allocation: `get_traced_memory()` reads (0, 0) at start() with a
# 2,000,041-byte subject already live. So the B/rep figures below are directly comparable between
# the two engines AND meaningful in absolute terms.
for n in (1_000_000, 2_000_000, 4_000_000, 6_000_000, 10_000_000):
    subject = "ab" * n
    for name, fullmatch in (("regex", regex.fullmatch), ("re   ", stdlib_re.fullmatch)):
        def measured(fullmatch=fullmatch, subject=subject, n=n):
            tracemalloc.start()
            try:
                end = fullmatch("(ab)*", subject).end()
                peak = tracemalloc.get_traced_memory()[1]
                return f"end={end}, traced peak {peak / 1e6:.1f} MB ({peak / n:.0f} B/rep)"
            finally:
                tracemalloc.stop()
        show(f"  {name} fullmatch('(ab)*', 'ab'*{n:,})", measured)
    del subject

print("\n" + r"=== 563: \m with a fuzzy quantifier at position 0 - REPRODUCES")
for pattern, subject in ((r'\m(?:Y){i}\M', 'XY YX'), (r'\m(?:X){i}\M', 'XY YX'), (r'\m(?:Y){i}\M', ' XY YX')):
    show(f"  findall({pattern!r}, {subject!r})",
         lambda pattern=pattern, subject=subject: regex.findall(pattern, subject))

print("\n=== 564: a looser fuzzy budget returns FEWER matches - REPRODUCES")
for pattern in (r'(?b)\m(?:Y){1i+1d+1s<=1}\M', r'(?b)\m(?:Y){1i+1d+1s<=2}\M'):
    show(f"  findall({pattern!r}, ' XY Z')", lambda pattern=pattern: regex.findall(pattern, ' XY Z'))

print("\n=== 589: partial fullmatch denies a prefix whose completion exists - REPRODUCES")
_P589 = regex.compile(r"(?!(True|False)\b)(.*)")
show("  fullmatch('True', partial=True)   # 'True' IS a prefix of 'Truest'",
     lambda: span(_P589.fullmatch("True", partial=True)))
show("  fullmatch('Truest')               # ... and 'Truest' is a complete match",
     lambda: span(_P589.fullmatch("Truest")))
show("  fullmatch('True')", lambda: span(_P589.fullmatch("True")))

print("\n=== 596: '{e<=0}' causes a 210x slow-down - DOES NOT REPRODUCE")
_TEXT_596 = "lorem ipsum " * 1024
for pattern in ("CARTE DE RESIDENT", "(?:CARTE DE RESIDENT)", "(?:CARTE DE RESIDENT){e<=0}"):
    def timed(pattern=pattern, runs=2000):
        compiled = regex.compile(pattern)
        started = time.perf_counter()
        for _ in range(runs):
            compiled.search(_TEXT_596)
        return f"{(time.perf_counter() - started) / runs * 1e6:.2f} us/search over {runs} searches"
    show(f"  {pattern}", timed)
