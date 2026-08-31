# Roadmap

Phases, in order, from the design spec section 12. This file lists **what** each phase covers and
nothing about how far along it is: status lives in `docs/STATUS.md` (generated from the test
suite) and in `docs/plan/slices/` versus `docs/plan/slices/done/`. Duplicating status here would
just give it somewhere to rot.

Slice files are authored **one phase ahead only**. A detailed plan for phase 5 written today
would be wrong by the time phase 5 arrives, for the same reason a stale TODO list is.

| Phase | Content | Sessions (est.) | Main model |
|---|---|---:|---|
| 0 | Scaffolding: solution, build props, CI, skills, driver script, budget gate, slice files for phase 1 | 1-2 | Opus |
| 1 | Port the full upstream test suite, all skipped initially | 3-6 | Sonnet under Opus |
| 2 | **Compile-parity corpus first**, then parser and compiler (`_regex_core.py`), Unicode tables and case folding, pattern-level public API | 8 | Opus |
| 3 | **Differential oracle harness first**, then VM core: literals, classes, quantifiers, groups, backrefs, anchors | 11-16 | Opus |
| 4 | Advanced: lookaround, atomic and possessive, recursion, branch reset, named lists, POSIX, partial matching | 8-12 | Opus |
| 5 | Fuzzy matching, `BESTMATCH`, `ENHANCEMATCH` | 5-8 | Opus |
| 6 | Oracle *hardening* (broader generators, all Unicode planes), gap tests | 3-5 | Opus/Sonnet |
| 7 | Benchmarks and optimisation | 5-10 | Opus |
| 8 | Docs, packaging, NuGet, 1.0 | 2-3 | Sonnet/Opus |

Roughly 42-69 slice sessions in total, at plus or minus 50%. The generated status board makes the
real rate visible within the first two phases, which is when these numbers should be revised
against evidence rather than trusted.

**Phase 2's measured rate, recorded at its close (S13, 2026-08-31).** Phase 2 was re-estimated at
8 slices when it opened and landed in exactly 8 (S06-S13), so the *slice* estimate held. The
*driver session* count did not: `docs/plan/slice-log.jsonl` shows 8 slices took 11 driver sessions,
because S07 failed twice before it was parked and S10 failed once - about 35% more sessions than
slices. A completed slice cost 29M to 81M tokens, median around 48M, and a failed attempt costs
roughly the same as a successful one. **So read every phase estimate below as slices, and budget
1.35x that many driver sessions.** Phase 3's 11-16 slices is therefore 15-22 sessions. The estimate
itself is left alone: Phase 2 was parser work and Phase 3 is engine work, so its rate is not
evidence about Phase 3's, and revising a number on the strength of a different kind of work would
be worse than leaving it.

One number the owner should revisit before Phase 3 runs unattended: `docs/plan/budget.json` sets
`maxSlicesPerDay` to 5 and `maxSlicesPerWeek` to 12, and its own note flags that the weekly cap now
binds after two and a half busy days. Changing it is an owner decision, not a slice's.

Fuzzy matching - the reason this port exists - is usable at the end of phase 5, about two thirds
of the way through.

**Phase 3 opens with the differential oracle, not with the VM.** The oracle was originally phase 6
work, which left phases 3-5 - the entire engine - resting on the ported suite and the ratchet
alone. Those two cannot carry it: passing the original project's own tests is evidence of parity,
not proof of it, and the published figure for the closest analogue is that 72% of transpiled
functions were semantically equivalent *despite compiling and passing the existing tests*. The
infrastructure already exists from phase 0 (`tests/FuzzyRegex.OracleTests/`, `oracle.yml`,
Python `regex` installed), so this is a change of when the harness gets written, not of what has
to be built. Design spec amendment 10 has the evidence and the one caveat that does not apply
to us.

**Phase 2 opens with a compile-parity corpus, and carries the Unicode tables.** Both changes were
made at the Phase 1 checkpoint (2026-08-30). The parser's output is bytecode, and upstream's
bytecode is observable by intercepting `_regex.compile` while upstream's own suite runs: 1,534
patterns, 46 parse errors and 62 replacement templates, bit-exact. That is the oracle for a phase
in which nothing can match yet, so it is slice S06 and every later slice is verified against it.
The Unicode tables were parked in Phase 6, but the parser consults them for `\p{...}`, for every
case-insensitive pattern and for `\N{...}`, so they cannot wait; they are S09, transliterated
from upstream's generated C rather than regenerated. The reasoning and the measurements are in
`docs/plan/2026-08-30-phase2-decisions.md`. The estimate moved from 5-8 to 8 sessions.

## Candidates parked for later

- **Mutation testing (Stryker.NET), phase 6, scoped and on demand.** It answers "do our tests
  actually pin this behaviour?", which is worth asking of a port. But it is the wrong tool for
  most of this codebase: the differential oracle answers the same question with real ground
  truth and also finds behaviour we never tested at all, whereas mutation testing only finds
  code paths existing tests fail to pin. Stryker reruns the suite per mutant, so a 30k-line
  engine would take hours to days per run - never a merge gate. Where it would earn its keep is
  the small, bounded parts the oracle cannot reach: the public API layer (option handling,
  argument validation, `MatchTimeout`, Span overloads) and the parse-error paths. Revisit in
  phase 6 with real code and real timings; any estimate made now would be a guess. It cannot
  test the PowerShell tooling at all.

## Phase boundaries are human checkpoints

`tools/run-slices.ps1` refuses to cross one. When the pending queue empties, the driver stops and
the owner reviews progress, then authors and approves the next phase's slices. See
`docs/plan/OPERATIONS.md`.

## Where the work queue actually is

- `docs/plan/slices/` - pending, lowest number first. Listing this directory **is** the todo list.
- `docs/plan/slices/done/` - completed, with closing notes appended.
- `docs/plan/STATE.md` - current slice, blockers, next action. Thirty lines maximum, rewritten
  each session.
- `docs/plan/DECISIONS.md` - append-only dated one-liners.
