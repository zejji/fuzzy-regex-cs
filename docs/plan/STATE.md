# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 1 - port the upstream test suite.

**Last completed:** Phase 0 scaffolding (2026-08-29). Solution, analyzers, CI, the four skills,
the parity ratchet and generated status board, the budget gate and the slice driver.

**Current slice:** none in flight. Next is `docs/plan/slices/S01-api-surface-stub.md`.

**Next action:** run `tools/run-slices.ps1`, or open a fresh session and invoke the `port-slice`
skill.

**Blockers:** none.

**Worth knowing before S01:**

- Two contradictions in the design spec were found and resolved during Phase 0; both are
  recorded inline in the spec and listed in its section 15. Skip reasons name a **capability**
  (`needs:lookbehind`), not a slice id, and Phase 1 opens with an API surface stub so the ported
  tests can compile.
- The budget gate has no live plan-utilisation check - none exists locally. It runs on
  deterministic slice caps plus rolling token windows from the session logs. `budget.json`
  currently holds estimates; S05 recalibrates them from `slice-log.jsonl`.
- The Python oracle is `regex` 2026.7.19, one release behind the pinned submodule
  (2026.8.12 has no PyPI release). Rule the version gap out before blaming the port.
- No baseline exists for the engine yet: the ratchet currently guards 21 tooling and convention
  tests. It gains teeth as the ported suite lands.
