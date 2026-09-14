"""Ledger entry 5's sixth door: a `(*SKIP)` truncates upstream's own `(?b)` walk.

`do_best_fuzzy_match` walks `start_pos` across the slice, running `basic_match` once per
candidate and holding the next run to strictly fewer errors than the last. Its loop guard
(`upstream/src/_regex.c:17625`) reads the LIVE `state->slice_start`/`slice_end`:

    while (state->slice_start <= start_pos && start_pos <= state->slice_end) {

`RE_OP_SKIP` (`:14555`) assigns `slice_start` mid-attempt - `slice_end` under `(?r)`,
`:14553` - and nothing puts it back: `init_match` (`:3404`) resets the stacks, the groups
and the guards but not the slice, and this walk calls it once per candidate. `start_pos` is
set to `state->match_pos`, the start of the match the candidate just found, so a verb that
consumed anything leaves `slice_start` ABOVE `start_pos` and the guard is false on the next
turn. The walk ends on its FIRST successful candidate, and every better match further along
the subject is never attempted.

WHAT JUDGES IT IS THE VERB'S OWN DEFINITION, not a preference between two rankings.
`(*SKIP)` sets a skip point: a later match attempt must not start BELOW it (PCRE2
pcre2pattern, "Verbs that act after backtracking"). On the minimal row the skip point is 1
and the candidate the walk never reaches starts at 2, which the verb permits outright. And
`(?b)` is documented as the match with the fewest errors among those that exist, so a
zero-error match that the same compiled pattern's own `match` finds settles it with no
second engine - which is as well, because PCRE2 has no fuzzy matching at all
(`tools/probes/pcre2-has-no-fuzzy-matching.py`).

The `(*PRUNE)` control is what makes the moved bound the cause rather than the pattern's
meaning: it prunes backtracking exactly as `(*SKIP)` does and moves no bound.

Run:  python tools/probes/upstream-bestmatch-walk-truncated-by-a-skip.py
      python tools/probes/upstream-bestmatch-walk-truncated-by-a-skip.py --hunt

The same five rows are in `bestmatch-walk-truncated-rows.jsonl` beside this file, in the oracle's
own row format, and that file is what `ExpectedDivergences.bestmatch-walk-truncated-by-a-skip`
records. It carries a SIXTH row - `(?b)(?:\w(*SKIP)a|a){e<=1}` over `'a b c'`, which both engines
answer `(0, 2)` with one substitution - because every one of the five is a perfect match and a wave
with no error in it fails the consumer's non-degeneracy guard. Replay the lot with
`pwsh -File tools/run-oracle.ps1 -Rows tools/probes/bestmatch-walk-truncated-rows.jsonl`.

Measured 2026-09-14 against regex 2026.9.10 (the S44 pin). This port answered the same on
every row below until S48 restored the caller's slice before each candidate; it now answers
the `(*PRUNE)` column.
"""

import itertools
import sys

import regex

# The minimal row and the four larger ones the hunt first drew, each one a `(?b)` search whose
# answer the `(*SKIP)` changes and whose better candidate the pattern's own `match` still finds.
ROWS = [
    (r"(?b)(?:a(*SKIP)b){e<=1}", "axab"),
    (r"(?b)(?:b(*SKIP)a){e<=1}", "bxba"),
    (r"(?b)(?:b(*SKIP)ab){e<=1}", "bbbab"),
    (r"(?b)(?:\w(*SKIP)ab){e<=1}", "abcabc"),
    (r"(?b)(?:\w(*SKIP)ab){e<=2}", "qqxyab"),
]

# The hunt's alphabet. Small on purpose: the door needs only a verb that consumes, a fuzzy
# section, and a better candidate to the right of the skip point.
PIECES = [r"\w", r"\d", r".", r"[a-c]", r"[^a]", r"a", r"b", r"ab", r"[ab]+", r"\D"]
TAILS = [r"ab", r"bc", r"a", r"xy", r"[bc]", r"\d", r"b\w"]
FUZZ = ["{e<=1}", "{e<=2}", "{e<=3}", "{i<=1,d<=1,s<=1}", "{s<=2}", "{1i+1d+1s<=3}"]
SUBJECTS = ["zzab", "zzzab", "abcabc", "a b c", "zzabc", "qqxyab", "bbbab", "0a1b2c", "aXbXc"]


def answer(pattern, subject):
    try:
        m = regex.compile(pattern).search(subject)
    except Exception as exc:  # noqa: BLE001 - a shape that will not compile is not a row
        return ("error", type(exc).__name__)
    return None if m is None else (m.span(), m.fuzzy_counts)


def doors(pattern, subject):
    """What the same compiled pattern answers anchored at every start position."""
    compiled = regex.compile(pattern)
    out = []
    for i in range(len(subject) + 1):
        m = compiled.match(subject, i)
        if m is not None:
            out.append((i, m.span(), sum(m.fuzzy_counts)))
    return out


def report(pattern, subject):
    prune = pattern.replace("(*SKIP)", "(*PRUNE)")
    gone = pattern.replace("(*SKIP)", "")
    print(f"{pattern!r} over {subject!r}")
    print(f"   search, as written      {answer(pattern, subject)}")
    print(f"   search, (*PRUNE)        {answer(prune, subject)}")
    print(f"   search, verb deleted    {answer(gone, subject)}")
    print(f"   its own match() doors   {doors(pattern, subject)}")


def hunt():
    """Every skip/prune difference in the small alphabet above, and how many there are."""
    rows = 0
    hits = 0
    for piece, tail, fuzz in itertools.product(PIECES, TAILS, FUZZ):
        for body in (
            f"(?b)(?:{piece}(*SKIP){tail}){fuzz}",
            f"(?b)(?:{piece}(*SKIP){tail}|{tail}){fuzz}",
            f"(?b)(?:{piece}(*SKIP)){fuzz}{tail}",
        ):
            for subject in SUBJECTS:
                rows += 1
                skip = answer(body, subject)
                if isinstance(skip, tuple) and skip and skip[0] == "error":
                    continue
                if skip != answer(body.replace("(*SKIP)", "(*PRUNE)"), subject):
                    hits += 1
    print(f"{rows} rows, {hits} of them answer differently with (*SKIP) than with (*PRUNE)")


def main():
    print("regex", regex.__version__)
    if "--hunt" in sys.argv:
        hunt()
        return
    for pattern, subject in ROWS:
        report(pattern, subject)
        print()


if __name__ == "__main__":
    main()
