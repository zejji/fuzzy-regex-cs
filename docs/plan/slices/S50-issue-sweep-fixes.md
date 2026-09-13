---
slice: S50
phase: 6
title: Upstream issue sweep, part two - fix every issue S49 reproduced in this port
delivers: []
---

# S50 - The issue sweep, part two

Un-skips and turns green every `needs:issue-<n>` test S49 left, one issue at a time, each judged
to amendment 16 before the fix and each fix a deliberate divergence with an entry and a control.

## Scope

- Work S49's inherited list in the order S49 recommends, smallest mechanism first. For each: the
  definition (docs; a PCRE2 or Perl run where the construct exists there; comparable libraries for
  fuzzy semantics), the mechanism in `_regex.c` with line references, the fix, the pinned test, the
  divergence entry, the negative control, the ledger update.
- **Likely members and what is already known**: 563 and 564 are fuzzy-search shapes S39 flagged
  to recognise; 596 is a no-op constraint upstream does not elide, and eliding `{e<=0}` at compile
  time is a parser change with a compile-parity consequence, so record the corpus rows it changes;
  551 and 554 are resource blowups whose fix may be a bound rather than an answer, decided the way
  S47 decides entry 14; 367 and 425 are correctness bugs with clear expected answers in their
  issue threads.
- A fix that would change an answer upstream gets right on any oracle row is wrong; the wave says so.
- Anything S49 reproduced that cannot be fixed inside the slice is parked as a named blocker in
  STATE.md with the evidence.

## Verification

- Every `needs:issue-<n>` tag gone; each fix has a control that fires; waves GREEN at three seeds
  and 99991; the compile-parity corpus GREEN or its changed rows named for 596.

## Done when

- [ ] Every reproduced issue fixed test-first or parked with evidence; zero `needs:issue-*` skips.
- [ ] Entries, controls, ledger updated; nothing filed.
- [ ] Ratchet GREEN, blind review (hunt: a bound that turns a finite pattern into an exception; a
      fix judged from the issue thread alone without a run), commit.
