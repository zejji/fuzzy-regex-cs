---
slice: S47b
phase: 6
title: Corrections from the independent audit of S44-S46 - two pins narrowed, evidence promoted out of scratch, notes made true
delivers: []
---

# S47b - Audit corrections

An independent Opus audit (2026-09-14, `.scratch/audit-s44-s46.md`; its findings are copied below
so the slice does not depend on scratch) read S44, S45 and S46 against spec amendment 16. Nothing it
found is a wrong answer in the engine. Everything it found is a pin wider than its evidence, evidence
that only exists in gitignored files, or a note that claims a measurement nobody made. Each of those
is the kind of thing that later hides a real bug, so they are fixed as a slice, test-first where a
test applies, and not as a tidy-up.

## Owner decision, 2026-09-14 (recorded here; copy into DECISIONS.md)

Ledger entry 13 (BESTMATCH loses a match its own flagless `search` or anchored `match` finds) is
amendment 16 outcome (d): upstream is wrong with strong evidence, but the mechanism is not
established to the line. The owner accepted the recommendation: **keep the pin, narrow it to the
rows and the minimised shape where the contradiction was measured, and let every other `(?b)`
divergence show red for triage.** The mechanism trace is S47c. The pin widens only on evidence.

## Scope

1. **Narrow `bestmatch-loses-a-candidate`** (`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs`,
   around line 1490). Today `Applies` is "any `(?b)` row where the port's answer equals upstream's
   flagless answer", which a port that ignored `(?b)` would also satisfy. Replace with: the five
   measured wave rows (74938, 76251, 76593, 76681, 77937 by their recorded patterns and subjects, not
   by row number) plus the minimised shape `(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)` over `ab.`, and the
   structural test that upstream returned NO match while its flagless answer or its anchored
   `match` at a position in range returns one - the contradiction itself, not the agreement with the
   port. Test-first: a fabricated `(?b)` row where the port ignores the flag must be RED before the
   change and stay red after; the five real rows stay classified. Re-run the `fuzzy` wave at three
   seeds and 99991; any newly red `(?b)` row is triaged, not re-pinned.
2. **Narrow `turkic-default-folding`** (around line 1588). S45's own finding 6 records a live false
   positive: a total match failure on `(?i)ı.` over `ıx` classifies as expected. The pin must require
   that the port DID match and that the only difference is the Turkic pairing (the row involves one
   of U+0049, U+0069, U+0130, U+0131 under case-insensitivity and upstream's answer is what the `T`
   rows would give). Test-first with the recorded false positive as the red case.
3. **Promote S45's evidence.** `.scratch/s45_sweep.py`, `.scratch/s45_perl.pl`,
   `.scratch/s45-definition.py` and the PCRE2 grid become `tools/probes/upstream-turkic-*.py|.pl`,
   runnable from a clean checkout, output quoted in a header comment with the versions
   (regex 2026.9.10, Perl 5.42.2, PCRE2 10.47, .NET 10.0.10). Ledger entry 7 gains a `Reproduce:`
   line naming them. If a scratch file is gone, re-derive it from the slice notes and say so.
4. **Make the notes true.** In `docs/plan/slices/done/`: S45's "Default wave GREEN at all three
   seeds, 6300 rows: expected 4/1/2, diverge 0/0/0" is corrected with S46's measured numbers and the
   two divergences named; S44's ticked "Waves GREEN at three seeds and 99991" gets the 99991 result
   (run it now) or the tick comes off; S46's ticked "fixed test-first" for entry 12 gets the red
   count from a stashed `src/`, or the wording says the test was written alongside. Corrections are
   appended as dated notes, the original text left in place and struck through.
5. **Independent verifier, first use.** The skill now requires a verifier pass after the blind
   review (see `.claude/skills/port-slice/SKILL.md`, added 2026-09-14). This slice is the first to
   run it: the verifier re-runs each promoted probe from the committed files and confirms every
   number quoted in items 1-3.

## Not in scope

- Tracing upstream's mechanism for entry 13 (S47c).
- The five unresolvable control sites and S42-2G (S57 re-judges controls; if a session has time
  after items 1-5, fixing their anchors is welcome and goes in the notes).

## Verification

- Ratchet GREEN; `fuzzy` and default waves at three seeds and 99991 with the narrowed pins; the
  two red-first tests named with their before/after outcome; probes run from a clean checkout.

## Done when

- [ ] Both pins narrowed, each with a red-first test that a too-wide pin would have passed.
- [ ] S45's probes in `tools/probes/`, ledger 7 has `Reproduce:`.
- [ ] S44, S45, S46 notes corrected with dated, visible amendments.
- [ ] Owner decision on entry 13 copied into DECISIONS.md.
- [ ] Blind review, then verifier pass; ratchet GREEN; commit.
