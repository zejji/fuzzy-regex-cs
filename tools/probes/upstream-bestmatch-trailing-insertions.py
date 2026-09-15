"""Ledger entry 12: what `(?b)` does to a match whose fit needs trailing insertions.

Written by S46 on 2026-09-14 against `regex` 2026.9.10, and it CORRECTS the symptom the
entry was first written with. The entry said "*n* trailing insertions need `max_errors`
above *2n-1*", which reads as though a large enough budget buys the match back. It does
not: under `(?b)` the caller never sets `max_errors`, because `do_best_fuzzy_match`'s
second pass sets it to `fewest_errors` = *n* itself (`_regex.c:17732`), so on THIS
pattern the doubled guard at `:15515-15517` needs *n > 2n-2* - false for every *n >= 2*
at every budget. Blocks 6 and 7 below say that arithmetic is `(?:x)`'s and not the
defect's.

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

S52 SITTING 16 ADDED THE LAST TWO BLOCKS, AND THEY WIDEN THE SYMPTOM AGAIN (2026-09-15).
"k >= 2 trailing insertions" is not the boundary either. Block 6 is the minimised form of
seed 523701539 row 41539 of the seed sweep: a fit costing ONE insertion and one
substitution, refused under `(?b)` and answered the moment the flag goes. Block 7 says the
number of insertions is not on its own the question - a width-1 section body refuses from
k=2 while a width-2 body never refuses at any k this probe reaches.

**The exact law is OPEN and this probe does not claim one.** What IS established, and what
the entry now rests on, is the attribution rather than the shape: restoring upstream's
doubled term in this port's `Matcher.cs` makes this port refuse exactly the four sweep rows
of this family and no others of the eight the sweep drew with the same signature
(`pwsh -File tools/run-oracle.ps1 -Rows`, 2026-09-15, 4 of 8 agreeing where 0 of 8 did).
"""

import regex

BUDGETS = range(0, 7)
TRAILING = "yzwvu"


def cell(pattern: str, subject: str) -> str:
    """One `fullmatch`, rendered as its insertion count or as a dash."""
    m = regex.fullmatch(pattern, subject)
    return "-" if m is None else f"i{m.fuzzy_counts[1]}"


def describe(m) -> str:
    """One match, rendered as its span and counts, for the blocks that need both."""
    return "None" if m is None else f"{m.span()} counts={m.fuzzy_counts}"


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
    print()

    # Block 6 (S52 sitting 16): ONE trailing insertion is enough, when the fit spends
    # another error as well. The minimised, all-ASCII form of seed 523701539 row 41539 -
    # the sweep row is the same pattern over 'a0' + U+1D518 + '0' + U+1F600, and nothing
    # about it needs an astral subject. The three subjects below are the ablations: take
    # the trailing insertion away and `(?b)` answers at either error count.
    print("fullmatch (a0)(?:(?:\\1)){e<=3} - one insertion is enough when a substitution")
    print("is spent too, and the SECTION BODY decides whether it is refused")
    print(f"    {'subject':<10}{'body':<10}{'(?b)':<26}{'flagless':<26}")
    for body in (r"(?:\1)", "a0"):
        for subject in ("a0x0y", "a0xy", "a0x0"):
            pattern = f"(a0)(?:{body}){{e<=3}}"
            print(
                f"    {subject:<10}{body:<10}"
                f"{describe(regex.fullmatch('(?b)' + pattern, subject)):<26}"
                f"{describe(regex.fullmatch(pattern, subject)):<26}"
            )
    print()

    # Block 7 (S52 sitting 16): the number of trailing insertions is not on its own the
    # question - the width of the fuzzy section's body moves the boundary, which is the
    # measurement that says this entry does not yet know its own law.
    print("fullmatch (?b)(?:BODY){e<=6} over BODY's own text + k trailing 'Q'")
    print("    " + "body".ljust(12) + "".join(f"k={k}".rjust(6) for k in range(5)))
    for body, text in (("x", "x"), ("xy", "xy"), ("xyz", "xyz"), ("x?", ""), ("x*", "")):
        cells = "".join(
            ("-" if regex.fullmatch(f"(?b)(?:{body}){{e<=6}}", text + "Q" * k) is None else "M").rjust(6)
            for k in range(5)
        )
        print("    " + body.ljust(12) + cells)
