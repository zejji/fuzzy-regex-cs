# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S55 IS A CHECKPOINT, NOT DONE (sitting 2, 2026-09-17).** Slice file still in
`docs/plan/slices/` (not `done/`) - it needs at least one more sitting. Full detail:
`docs/plan/slices/notes/S55-sittings.md`.

**Tooling works.** `tools/run-stryker.ps1` runs Stryker's MTP/TUnit runner successfully -
`-t mtp --target-framework net10.0` (or the config-file equivalent) avoids a Buildalyzer probe
that otherwise picks VS Build Tools' MSBuild and fails with MSB4276. An orchestrator message
mid-sitting claimed Stryker cannot run MTP at all and asked for the slice to be marked blocked and
the tooling deleted; that claim was reproduced and found false (two independent successful runs,
real coverage capture, real killed mutants, no refusal message in either log), and the
orchestrator withdrew it once shown the evidence. Do not re-litigate this without re-reading
`S55-sittings.md` first.

**`-Mutate` takes exactly one glob.** Passing more than one (repeated `-m`, or several JSON-array
entries) silently mutates nothing - found while sizing the API-layer run. `tools/stryker-queue.json`
was written and fixed to one glob per chunk.

**Progress: `*.cs` (API layer) done, 237 mutants, 0 survived, but its report was deleted before
being made permanent - re-run as chunk `api` before trusting this number long-term.** `Parsing/*.cs`
(2189 mutants) was left running in the background past this sitting's deadline; read
`TestResults/stryker/parsing/` first next sitting. `Engine/Substitution.cs` not started. The
engine chunk queue is a first cut from `Matcher.cs`'s method boundaries, not calibrated - expect to
split further once real per-chunk timings exist.

**Ratchet was NOT re-run this sitting** (deliberate): no `src/` or `tests/` file changed, only
`tools/`, `stryker-config.json` and `docs/`, and a live Stryker background job made contending for
the same build outputs unwise. The last verified-green state is unchanged (09aece2, 6262/6262/0).

**Carried:** benchmark baseline still needs retaking on a quiet machine
(`pwsh -File tools/compare-benchmarks.ps1 -UpdateBaseline`, ~32 min). `.claude/skills/benchmark/
SKILL.md` documents a stale run command. `_regex.c:22091`'s comment on `text_length` is wrong.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push;
`stash@{0}` (S54 sitting 1's rescue stash) is safe to drop.