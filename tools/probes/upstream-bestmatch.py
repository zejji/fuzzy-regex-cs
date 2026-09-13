"""Upstream's answers for the `BESTMATCH` cases S42's gap tests assert.

Every expected value in tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyBestMatchTests.cs was read off this
script's output, and each assertion quotes the line it came from. Re-run it after an upstream bump
and the labels line up with the test names.

    python tools/probes/upstream-bestmatch.py

Measured on regex 2026.7.19, 2026-09-13. The two rows worth reading twice:

  * `max-errors 12 (?b)` needs 12 errors, which is more than RE_MAX_ERRORS (10, upstream:203), so its
    second pass cannot succeed at all and it is the only row here that reaches the widened-slice
    fallback. `fallback2 (?b)` is the same shape inside the limit, and is the control for it.
  * `470 bestmatch` is upstream's open issue 470 and this port still agrees with it. 'voixes' is one
    substitution costing 2 and 'voicees' is one insertion costing 1, so the cheaper match is the
    later one - but the first pass holds the next run to FEWER ERRORS than the one it has (:17674),
    both are one error, and the search stops. See the test of the same name.
"""

import regex

print("regex version:", regex.__version__)


def show(label, m):
    if m is None:
        print(f"{label}: NO MATCH")
    else:
        print(
            f"{label}: span={m.span()} value={m.group()!r} "
            f"counts={m.fuzzy_counts} changes={m.fuzzy_changes} "
            f"groups={m.groups()}"
        )


# Upstream issue 470: ranking by error count rather than by cost.
show("470 bestmatch", regex.search(r"(?b)(voices){1i+1d+2s<=2}", "voixes voicees"))

# The first fuzzy match is not the best one.
show("first-not-best (?b)", regex.search(r"(?b)(foobar){e<=3}", "fxxbar foobar"))
show("first-not-best     ", regex.search(r"(foobar){e<=3}", "fxxbar foobar"))

# Equal-best candidates: the earliest wins.
show("equal-best (?b)", regex.search(r"(?b)(cat){e<=1}", "cbt xxx cet"))

# (?b) with (?r).
show("reverse (?b)", regex.search(r"(?br)(foobar){e<=3}", "fxxbar foobar"))

# (?b) in a scan.
print(
    "scan (?b):",
    [(m.span(), m.group(), m.fuzzy_counts) for m in regex.finditer(r"(?b)(cat){e<=1}", "cbt cat cet")],
)

# (?b) with partial=True, both ways.
show("partial (?b)", regex.search(r"(?b)(foobar){e<=1}", "xxxxfoob", partial=True))
show("partial full(?b)", regex.search(r"(?b)(foobar){e<=1}", "xxfoobarxx", partial=True))

# RE_MAX_ERRORS binds, and the widened-slice fallback is what that reaches.
show("max-errors 12 (?b)", regex.match(r"(?b)(^\d{12}$){i<=12}", "123456789012" + "x" * 12))
show("fallback2 (?b)", regex.match(r"(?b)(^123$){s,i,d}", "xxxxxxxx123"))

# The perfect-match arm, which clears the change list rather than running the second pass.
show("perfect (?b)", regex.search(r"(?b)(cat){e<=1}", "xxcatxx"))
