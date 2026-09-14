"""Ledger entry 12: what `(?b)` does to a match whose fit needs trailing insertions.

Written by S46 on 2026-09-14 against `regex` 2026.9.10, and it CORRECTS the symptom the
entry was first written with. The entry said "*n* trailing insertions need `max_errors`
above *2n-1*", which reads as though a large enough budget buys the match back. It does
not: under `(?b)` the caller never sets `max_errors`, because `do_best_fuzzy_match`'s
second pass sets it to `fewest_errors` = *n* itself (`_regex.c:17732`), so the doubled
guard at `:15515-15517` needs *n > 2n-2* - false for every *n >= 2* at every budget.

Run it:

    python tools/probes/upstream-bestmatch-trailing-insertions.py

Expected on 2026.9.10 - the first two blocks match exactly when N >= k, and the `(?b)`
block matches only for k <= 1:

    plain  k=2      -     -    i2    i2    i2    i2    i2
    (?e)   k=2      -     -    i2    i2    i2    i2    i2
    (?b)   k=2      -     -     -     -     -     -     -

The last two blocks are the controls that place the defect in the TRAILING-insertion arm
rather than in the flag: leading insertions and substitutions are unaffected by `(?b)`.

This port answers the `(?e)` row under `(?b)` as well, which is upstream's own answer once
the flag is deleted. Pinned by
`Gaps/Engine/FuzzyBestMatchTests.Bestmatch_admits_trailing_insertions_up_to_the_sections_own_budget`.
"""

import regex

BUDGETS = range(0, 7)
TRAILING = "yzwvu"


def cell(pattern: str, subject: str) -> str:
    """One `fullmatch`, rendered as its insertion count or as a dash."""
    m = regex.fullmatch(pattern, subject)
    return "-" if m is None else f"i{m.fuzzy_counts[1]}"


def block(title: str, subject_of, pattern_of) -> None:
    print(title)
    print("      " + "".join(f"N={n}".rjust(6) for n in BUDGETS))
    for k in range(0, 5):
        row = "".join(cell(pattern_of(n), subject_of(k)).rjust(6) for n in BUDGETS)
        print(f"k={k}  " + row)
    print()


if __name__ == "__main__":
    print("regex", regex.__version__)
    print()

    for flag in ("", "(?e)", "(?b)"):
        block(
            f"fullmatch {flag}(?:x){{e<=N}} over 'x' + k TRAILING characters",
            lambda k: "x" + TRAILING[:k],
            lambda n, flag=flag: f"{flag}(?:x){{e<={n}}}",
        )

    # Control 1: the same insertions, LEADING. Unaffected by the flag, because the
    # trailing-insertion arm is the only place a trailing insertion can come from.
    for flag in ("", "(?b)"):
        block(
            f"fullmatch {flag}(?:x){{e<=N}} over k LEADING characters + 'x'",
            lambda k: TRAILING[:k] + "x",
            lambda n, flag=flag: f"{flag}(?:x){{e<={n}}}",
        )

    # Control 2: the same error COUNT spent on substitutions, which never reach the arm.
    print("fullmatch (?b)(?:x*k){e<=N} over 'q'*k - substitutions, not insertions")
    print("      " + "".join(f"N={n}".rjust(6) for n in BUDGETS))
    for k in range(1, 5):
        cells = []
        for n in BUDGETS:
            m = regex.fullmatch(f"(?b)(?:{'x' * k}){{e<={n}}}", "q" * k)
            cells.append(("-" if m is None else f"s{m.fuzzy_counts[0]}").rjust(6))
        print(f"k={k}  " + "".join(cells))
