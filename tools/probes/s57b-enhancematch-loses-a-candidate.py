r"""Upstream's `(?e)` keeps a three-error fit where its own tighter budget finds a two-error one.

Seed 20260920 row 122739 of the 6000-row gate, minimised::

    python tools/probes/s57b-enhancematch-loses-a-candidate.py

`ENHANCEMATCH` re-runs a fuzzy match inside its own span with the budget tightened to
`fewest_errors - 1` until the fit stops improving (upstream/src/_regex.c:17896 to :17997). The
answer it gives should therefore never spend more errors than the same engine can spend on the
same span. Over 'a6ZZ_' it spends three where the same pattern written `{e<=2}` spends two, and
`{e<=2}` permits a subset of what `{e<=3}` permits, so the fit it settles on was available to it.

The loop is not simply absent: on the neighbouring subject 'a6Z_' it improves three errors to one.

The port half is tools/probes/s57b-enhancematch-loses-a-candidate.cs.

Written by S57b against regex 2026.9.10, 2026-09-20.
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

WAVE_PATTERN = r"(?:abx\d+[\U0001f600\U0001d518]){e<=3:[^x]}"
WAVE_SUBJECT = "abx6\U0001d518\U0001d518\U0001f3fb"


def fit(pattern, subject, **kw):
    match = regex.compile(pattern).fullmatch(subject, **kw)
    if match is None:
        return "no match"
    return f"span={match.span()} counts={match.fuzzy_counts}"


print("regex", regex.__version__)
print()
print("the minimised row, (?:a\\d+Z) over 'a6ZZ_'")
for prefix in ("", "(?e)", "(?b)"):
    line = "  ".join(
        f"e<={budget}: {fit(f'{prefix}(?:a\\d+Z){{e<={budget}}}', 'a6ZZ_'):32}"
        for budget in (2, 3, 4)
    )
    print(f"  {prefix or 'plain':6} {line}")

print()
print("the control: the same pattern over 'a6Z_', where the loop does improve")
for prefix in ("", "(?e)"):
    print(f"  {prefix or 'plain':6} e<=3: {fit(f'{prefix}(?:a\\d+Z){{e<=3}}', 'a6Z_')}")

print()
print("the wave row itself, seed 20260920 row 122739")
for prefix in ("", "(?e)", "(?b)"):
    print(f"  {prefix or 'plain':6} {fit(prefix + WAVE_PATTERN, WAVE_SUBJECT, partial=True)}")
print(
    "  tighter section  "
    + fit(WAVE_PATTERN.replace("{e<=3:", "{e<=2:"), WAVE_SUBJECT, partial=True)
)
