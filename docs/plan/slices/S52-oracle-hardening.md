---
slice: S52
phase: 6
title: Oracle hardening - a seed sweep tool, all Unicode planes, longer subjects, and timeout rows
delivers: []
---

# S52 - Oracle hardening

The one lesson Phase 5 paid for five times (S33, S34, S35, S40a, S43): scope hardening on SEEDS, not
on rows. Each of those found a real defect at a seed no earlier slice had used, on generators
already swept at higher row counts. A new seed costs about a minute.

## Scope

- **`tools/sweep-seeds.ps1`**: run every generator at N fresh seeds (default 20, 2000 rows each),
  drawn and recorded so a red seed is reproducible, consuming each wave and summarising per seed
  and per generator. Detached-friendly (writes progress to a log, resumable per seed) so the
  orchestrator can run it overnight. Add a weekly CI job on `oracle.yml` with a rotating seed.
- **All Unicode planes.** Every generator's subject and literal alphabets gain astral characters
  (SMP letters and digits, emoji with modifiers and ZWJ), unpaired surrogates where the .NET string
  allows and Python does not (recorded as `unsupported` on the Python side rather than dropped, so
  the asymmetry is visible), and the `\X` grapheme cases. Indices are UTF-16 here and codepoints
  there; the recorder's `_to_index_length` already converts, so the risk is in the port, not the
  harness.
- **Longer subjects**: a `long` variant of `literals`, `quantifiers`, `partial` and `fuzzy` with
  subjects of 1,000 to 20,000 characters, to reach the paths that only a long text takes
  (`search_start`-shaped scanning, repeat guards, fuzzy insert budgets far from the start).
- **Timeout rows**: patterns known to be slow on both engines under a small `timeout=`, comparing
  that both time out (upstream's `TimeoutError` versus this port's `RegexMatchTimeoutException`)
  rather than skipping them. Depends on S51's `timeout` comparison.
- **Lift the last `interactions` exclusions** if S46 and S47 left any, and re-run at the fourth
  seed.
- Every divergence found is judged to amendment 16; the slice does not end with an unjudged row.

## Verification

- Seed sweep run once here with its output committed to the closing notes: 20 seeds, every
  generator, and the verdict per seed. Default wave GREEN at three seeds, 6000 rows; `fuzzy` and
  `interactions` at 99991.

## Done when

- [ ] Sweep tool committed and run; CI job added; astral, long and timeout generators recorded.
- [ ] Every divergence judged, fixed or entered with a control; nothing unjudged.
- [ ] Ratchet GREEN, blind review (hunt: an astral row whose index is converted twice; a long-subject
      generator that never reaches the path it was written for), commit.
