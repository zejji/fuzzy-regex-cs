---
slice: S34
phase: 4
title: Three-seed sweep - judge the residual divergences and make the default wave reliably green
delivers: []
---

# S34 - Three-seed sweep

Added 2026-09-12 after S33's blind review found that **a default oracle wave is not reliably
green and never was**: every slice ran one seed, and that seed happened to be clean. Three
divergence families surfaced at other seeds, none caused by S33. The owner's rule applies: each
gets a verdict backed by an isolating probe, a second engine where one exists, and a blind
review; a port bug is fixed test-first; a port-right divergence is pinned permanently and
classified in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`. The phase cannot close on a
red default wave, so this slice precedes S35.

## Scope

1. **Forward `(*SKIP)` inside a bounded repeat under an overlapped scan.**
   `regex.finditer(r'(?:[^\d](*SKIP)){2,3}', '\r\naabb ', regex.M, overlapped=True)` gives upstream
   `(0,3)(1,4)(2,4)(3,4)(4,7)(5,7)`; the port has `(2,5)` and `(3,6)` in the middle. Probed at the
   checkpoint (`tools/probes/`, add the script): upstream's **own** `match` and `search` at every
   start position 0..6 agree with the port exactly - `(2,(2,5))`, `(3,(3,6))` - so only its
   overlapped scanner disagrees, and the hypothesis is S29's mechanism: the scanner keeps one
   `RE_State` across matches, a `(*SKIP)` that moved `slice_start`/`slice_end` in a failed attempt
   persists into the next scan, and `scanner_search_or_match` (`:20874`) does not reset the slice
   between overlapped scans. Prove or refute by reading that function and by emulating the carry-over
   in a probe. If upstream's scanner is the inconsistent party, the port is right: pin it, classify
   the family, and add it to the upstream report draft. If the port's `Iteration.Scan` differs from
   upstream's scanner in a way *both* of upstream's doors would reject, fix it.
2. **A reversed capture whose end precedes its start.**
   `regex.compile(r'(?r)(?<g>[ab]+)(?=(?&g))b').search('>abbaa\r<').spans('g')` is `[(3, 2), (1, 3)]`
   upstream; the port records `(3, 3)`. A span with end before start is not a valid span, so
   upstream is wrong on its face - but find *why* (a group call inside a lookahead under `(?r)`,
   with `build_GROUP` direction propagation - issue 614, fixed upstream 2026-08-30 - the first
   suspect), state what the correct span is and why the port's `(3,3)` is it, blind-review the
   verdict, pin, classify, draft.
3. **The bounded-lazy-repeat partial family** (`ba??x` on `baa`, judged port-right in the research
   document, PCRE2 concurring). It reaches the `partial` wave at seed 314159, two rows. S33 found no
   classifier predicate that does not also swallow a genuine missed partial. Write the
   `ExpectedDivergences` entry on the *minimised rows* rather than on a predicate, so the staleness
   alarm still fires, and widen the entry only with rows a probe has individually judged.
4. **Three seeds are the new floor.** `run-oracle.ps1` gains a `-Seeds` list (default three, one
   of them the run's date) and reports per seed; `docs/VERIFICATION.md` and the `port-slice` skill
   say a wave counts only at three seeds. **Then `verbs` goes on the default list** and the whole
   default list runs green at all three seeds, with every classified row printed as `EXPECTED`.
5. **The upstream report draft** gains the items above that turn out to be upstream's, in the
   same shape as the existing four. Still not filed.

## Verification

- Every generator on the default list, three seeds, zero unexpected divergences; the command and
  its output quoted.
- `Every_expected_divergence_still_diverges` covers every new entry; each entry names its permanent
  test and its minimised row.
- Controls: `python tools/run-controls.py --slices S29,S31,S33` against the committed code; a new
  control for any engine change this slice makes.

## Done when

- [ ] Items 1 and 2 have a written verdict each, a blind review's report, and either a fix or a
      permanent test plus an `ExpectedDivergences` entry.
- [ ] Item 3 classified on minimised rows; the staleness test covers it.
- [ ] Three-seed runs in `run-oracle.ps1`, documented in VERIFICATION.md and the skill.
- [ ] `verbs` on the default list; the whole default list green at three seeds.
- [ ] Report draft extended, not filed.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a classifier entry wide enough to
      swallow a genuine divergence - mutate the engine and confirm the entry does not absorb it),
      commit.
