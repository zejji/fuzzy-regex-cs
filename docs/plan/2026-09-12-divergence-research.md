# Divergence research: the parked S29, S31 and S32 findings, judged

Written at the Phase 4 owner checkpoint, 2026-09-12, after the owner asked how sure we were that
each parked divergence had been called the right way. Every figure below is from a run made that
day, not from reading a manual; where a document was read, it is quoted and the run beside it says
whether the document's prediction held. Two did not.

Engines run: Python `regex` 2026.7.19 (upstream), PCRE2 10.47 through Git for Windows' bundled
`libpcre2-8-0.dll` (`tools/probes/pcre2-partial-and-skip.py`), Perl 5.42.2, GNU grep 3.0 `-P`, and
this port's built assembly. Surveyed and found to have no comparable feature: Oniguruma, RE2,
Hyperscan, Rust `regex`, .NET, Java (which has `hitEnd`/`requireEnd`, a different shape) for
partial matching; everything but Boost.Regex and Perl/PCRE2 for backtracking verbs.

## Rule applied to every case

Upstream's README defines a partial match as one "that matches up to the end of string, but that
string has been truncated and you want to know whether a complete match could be possible if the
string had not been truncated." PCRE2's `pcre2partial` and Boost's `match_partial` documentation
say the same thing in different words. So the test for a partial-match dispute is: **can this
subject be extended into a complete match?** The test for a `(*SKIP)` dispute is PCRE2's
definition: on failure the bumpalong goes to the position where `(*SKIP)` was encountered, and a
retry below that position is not permitted.

## Verdicts

| Case | Upstream | PCRE2 | Perl | Port | Verdict |
|---|---|---|---|---|---|
| `(?:..(*SKIP)x\|q)x` on `ab cd xx`, search | None | (4,8), with and without `NO_START_OPTIMIZE` | None | (4,8) | **Port right.** Upstream and Perl answers come from their start optimisers; PCRE2 documents the class and names Perl. |
| `..(*SKIP)xx` on `cd xxx`, search | (1,5) | (2,6) both ways | (2,6) | (2,6) | **Upstream bug.** Its answer retries *below* the position the verb committed past. |
| `(?r)(?:a*(*SKIP)b\|[^a-f])$` on `\nb`, MULTILINE, finditer | 1 match | n/a (no reverse) | n/a | 2 matches | **Port right by construction**: upstream's `search_start_END_OF_LINE_rev` bounds by `text_end` while its own slow path bounds by `slice_end`; emulating the fast path reproduces upstream exactly (S29 notes). Upstream internal inconsistency. |
| `ba??x` on `baa`, match, partial | partial (0,3) | None soft and hard, anchored | n/a | None | **Upstream bug.** `baa` cannot be extended to a match; upstream's greedy `ba?x` agrees with everyone. Same on `bab`. |
| `a(bc)*` on the empty slice `abc[1:1]`, reversed, partial | partial (1,1) | None (PCRE2 rule 1: no character inspected) | n/a | **None** forward-partial, reversed-None | **Port bug.** Upstream deliberately differs from PCRE2 on empty subjects (README `\d{4}` example, issue 469) and the port follows it forward and on whole empty subjects; only the reversed narrowed-slice arm disagrees. |
| `(?r)\b$` on empty, search, partial | partial reversed, None forward, None `match` | partial (its `\b`-at-end rule) | n/a | None | **Port right, upstream inconsistent.** Maintainer's model (issue 589) evaluates `\b` on the real string; the empty string has no word character. Upstream's reversed answer is its prefilter's. |
| `(?r)(ab)+` fullmatch `xabz[1:3]` | None | n/a | n/a | (1,3) | **Upstream bug**, found by S32's review. Upstream fullmatches the same slice at `pos=0` (`abz[0:2]` gives (0,2)), matches it with `match`, and fullmatches it forward; only reversed + general repeat + `pos > 0` fails. Fullmatching `ab` against a slice that is exactly `ab` cannot be None. |

## What the documents predicted, and where they were wrong

A Sonnet survey derived from `pcre2partial`'s prose that PCRE2 would answer **partial** for
`ba??x` on `baa` and for `a(bc)*` on an empty subject. The binary answers **None** to both, soft
and hard, anchored and unanchored. The `(*SKIP)` survey could not run `pcre2test` and left PCRE2's
`NO_START_OPTIMIZE` column "undetermined"; the direct library call settled it as (4,8) both ways.
Rule for this project: **when an engine's binary is reachable, run it; a derivation from its
manual is a hypothesis with the same standing as a reviewer's claim.**

## Second engine, quoted

PCRE2 `pcre2pattern`, "Optimizations that affect backtracking verbs": *"When one of these
optimizations bypasses the running of a match, any included backtracking verbs will not, of
course, be processed... Experiments with Perl suggest that it too has similar optimizations, and
like PCRE2, turning them off can change the result of a match."* Perl's `use re 'debug'` trace for
the first case reads `Found floating substr "x" at offset 6 (rx_origin now 3)` and attempts starts
3 and 5 only.

PCRE2 `pcre2partial`, requirements: *"the next pattern item must be one that inspects a
character, and at least one of the following must be true: (1) At least one character has already
been inspected..."* - which is why an empty subject is no match in PCRE2 and, by upstream's
documented choice, a partial in upstream.

Boost.Regex: *"A partial match is one that matched one or more characters at the end of the text
input, but did not match all of the regular expression (although it may have done so had more
input been available)."*

## Upstream issue history consulted

299 (grey area acknowledged: stop at first partial or keep looking), 469 (empty partial at end of
string is intended), 489 (partial captures only closed groups, by design), 514 (declined: "says
it's partial as soon as it hits the end of the string"), 539 and 546 (two partial bugs fixed in
2024.7.24 and 2024.11.6, the second in the lazy-quantifier neighbourhood), 553 (maintainer walks
"could it match if extended"), 589 (open: `\b` evaluated at the real end of string under partial).

## What follows from the owner's rule of 2026-09-12

Every conclusively identified bug gets fixed in the port before 1.0, inherited or not. So:

- **Port bug, fix now (S33):** the reversed empty-slice partial.
- **Port right, pin permanently (S33):** the three `(*SKIP)` cases, the lazy-repeat partial, the
  `\b$` reversed search. Their gap tests currently say inverted in Phase 7; that instruction is
  withdrawn. Phase 7 ports upstream's prefilters **without** importing their answers: a pinned
  "port is right" test changing is a regression, and ROADMAP's Phase 7 entry now says so.
- **Upstream bug the port reproduces today:** none of the above - in every row where upstream is
  wrong the port already answers correctly. The inherited bugs remain the Phase 6 list (issues
  611-614, 367, 425, 554, 551, and S30's group-call-in-lookbehind), each to be fixed there.
- **Open, needs the same research:** `(?r)(ab)+` fullmatch on a narrowed slice. S33 settles it.
- **Report upstream**, after the owner approves the text: `..(*SKIP)xx` retry-below-commit, the
  lazy-repeat partial, the `\b$` reversed inconsistency, and the reversed-fullmatch case if S33
  confirms it. Draft: `docs/plan/upstream-reports/2026-09-12-draft.md`.
