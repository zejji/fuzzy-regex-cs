---
slice: S48
phase: 6
title: Inherited (*SKIP) and partial-matching bugs - ledger entry 5's remaining door, and every other entry this port still shares
delivers: []
---

# S48 - The verb and partial inherited bugs

Phases 4 and 5 judged many `(*SKIP)` and partial divergences as upstream's with the port right;
those need no fix. This slice takes what is left: the entries where the port still shares
upstream's answer.

## Scope

- **Inventory first.** Walk `docs/plan/upstream-reports/LEDGER.md` entries 1-14 and every
  `ExpectedDivergences` entry, and table each as one of: port right (pinned, no work); fixed here
  already (name the slice); still inherited (this slice's list); upstream-only (no port symptom).
  The S43 handover names **entry 5's fifth door** as still inherited, with a proposed fix: reset
  the slice in `init_match` (`_regex.c:3404`) or save and restore it around `do_match`, closing all
  of entry 5's symptoms at once. S40a and S40b already restore the slice at match start and before
  the partial retry; find which door is still open and close it the same way.
- **Judge before fixing**, to amendment 16: PCRE2 10.47 via ctypes for verbs and partial
  (PARTIAL_SOFT and PARTIAL_HARD), Perl 5.42, the pcre2pattern section on backtracking verbs, and
  self-refutation where the same engine's anchored door beats its search. Every fix is a deliberate
  divergence with a narrow entry and a negative control.
- **Ledger entries 1-4**: confirm each is upstream-only with the port pinned right; any that turns
  out to be shared joins the list.
- Anything inherited and not fixable inside the slice is parked as a named blocker in STATE.md
  with the evidence, never quietly left.

## Verification

- The inventory table in the closing notes, one row per entry, with the slice or test that proves
  each classification.
- Waves GREEN at three seeds and 99991; the `search-start-*` and `overlapped-skip-*` controls re-run.

## Done when

- [ ] Inventory complete; every "still inherited" row fixed test-first or parked with evidence.
- [ ] Entries, controls, ledger updated; nothing filed.
- [ ] Ratchet GREEN, blind review (hunt: a fix that moves a permanent Phase 4 pinned answer; a slice
      reset that changes `(?r)` end-of-subject semantics, S35's trap), commit.
