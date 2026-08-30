# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 2 - parser, compiler, Unicode tables, pattern-level public API. Eight slices (S06-S13);
S06 done, seven pending.

**Last completed:** S06 (2026-08-30), the compile-parity corpus. 1547 compiles, 50 parse errors,
62 replacement templates, 29,322 bytecode integers, recorded from upstream's own suite and
deterministic over four runs. Ratchet green at 3664 tests, 37 passing.

**Current slice:** none in flight. Next is `docs/plan/slices/S07-parser-skeleton.md`.

**Next action:** `tools/run-slices.ps1` (Opus by default). Phase boundary is after S13.

**Blockers:** none.

**Worth knowing before the next slice:**

- **The corpus is now the gate.** `PatternCompiler.Compile` / `.CompileReplacement` in
  `src/FuzzyRegex/Parsing/` are the seam; a row skips only for a `NotImplementedException` whose
  message starts with `needs:<tag>`, and anything else fails. Read S06's closing notes in
  `slices/done/` before starting - they list what the corpus cannot check and why.
- **S07 and S08 must sort at two named points** (`_check_firstset`, `Branch._flush_set_members`)
  exactly as `tools/record-compile-corpus.py`'s `_render_key` does, or the corpus disagrees on
  those rows. PORTMAP's "Where we diverge" has the rule. A **third** order leak was found and
  fixed at the recorder's input; the port sorts nothing for that one.
- **S07 adds the first instance fields** to `FuzzyRegex`: the `initonly` reflection test
  (DECISIONS 2026-08-29) is due there. Build centrally, once, at the end of a slice (S05 notes).
- **Regenerate the fixture with `python tools/record-compile-corpus.py`** if upstream moves; CI
  checks it with `--check` and `--verify-determinism` in `oracle.yml`. Never hand-edit it.
- **Phase 2 changed the plan in four places**; reasoning in
  `docs/plan/2026-08-30-phase2-decisions.md`, one-liners in DECISIONS (2026-08-30).
