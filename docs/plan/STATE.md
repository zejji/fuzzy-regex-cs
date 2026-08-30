# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 - parser, compiler, Unicode tables, pattern-level public API. Eight slices authored
and approved by the owner on 2026-08-30 (S06-S13). None started.

**Last completed:** Phase 1 checkpoint (2026-08-30): slice files, decisions A-D, spec amendment
11, generator project deleted.

**Current slice:** none in flight. Next is `docs/plan/slices/S06-compile-parity-corpus.md`.

**Next action:** `tools/run-slices.ps1` (Opus by default). Phase boundary is after S13.

**Blockers:** none.

**Worth knowing before the next slice:**

- **Nothing in Phase 2 can match a string.** The ported suite verifies almost none of it; the
  S06 compile-parity corpus (upstream's exact bytecode for 1534 patterns) is the oracle, and
  every slice's done-criterion is "its corpus rows pass, no row fails".
- **Phase 2 changed the plan in four places**; the reasoning is in
  `docs/plan/2026-08-30-phase2-decisions.md` and the one-liners in DECISIONS (2026-08-30).
  Read it if anything in a slice looks like a departure from the spec.
- **84 upstream compiles are nondeterministic** (set order). S06 sorts at two named points on
  both sides; S07 and S08 must sort identically or the corpus will disagree on those rows.
- **Slices are vertical.** Every function not yet ported throws `NotImplementedException`
  naming the construct; the corpus tests turn that into a skip and anything else into a failure.
- **S07 adds the first instance fields** to `FuzzyRegex`: the `initonly` reflection test
  (DECISIONS 2026-08-29) is due there. Build centrally, once, at the end of a slice (S05 notes).
