---
slice: S58
phase: 7
title: The measurement method, this machine's noise floor, and the two numbers the span and lazy-walk decisions wait on
delivers: []
---

# S58 - Measure first, change nothing

Owner ground rule 2026-09-16 (DECISIONS, "Phase 7 ground rules"): **measure first; no design
decision on the span threading or the lazy-walk state shape until the benchmarks exist.** This
slice therefore changes **no engine code** - its output is numbers, two routes proven end to end,
two written decisions for the owner, and the ledger that keeps structural divergence honest. The
method is `docs/plan/phase7-research/BENCHMARKING-METHOD.md`, the profiling routes are
`PROFILING.md`, the per-slice checklist is `OPTIMISATION-TECHNIQUES.md` section 3. It runs after
S54, whose suite and `tools/compare-benchmarks.ps1` it extends rather than rewrites.

## Scope

1. **Noise floor.** Build Release once and do not touch the tree. Run the S54 suite twice,
   `--job medium --exporters json`, into `artifacts/bench/<date>-S58-noise-A` and `-noise-B`, and
   compare A against B with the same script Phase 7 will use for before-and-after. The largest
   per-workload time ratio **and** the largest allocation ratio are the thresholds. Commit them as
   `bench/baselines/<machine-id>/noise-floor.md` with the date, the git SHA, the job, and the state
   of the machine (driver idle - S54's own review hunt names "a baseline taken with the driver
   still running tests"). Repeat the pair once after a reboot; if the floors differ materially the
   higher is the threshold and the gap is the note. While those artifacts exist, read one run's
   generated `.notcs`/`runtimeconfig.json` to settle BENCHMARKING-METHOD's UNVERIFIED question of
   whether BDN pins affinity or forces a GC mode, and record the answer beside the floor.
2. **pyperf installed and probed.** `python -m pip install regex pyperf`, then archive the verbatim
   output of `python -m pyperf system show` into the baseline folder - that converts the research's
   strong inference ("`pyperf system tune` documents no Windows procedure") into evidence. Run
   `python -m pyperf check` on the committed Python baseline and record its verdict with the
   numbers; a baseline that fails `check` is not evidence. Measure the Python side's own floor the
   same way as step 1: the same workload twice, compared with `pyperf compare_to`.
3. **CPU profile route proven end to end**, on one real benchmark, not on a sample: BDN with
   `-p EP` on a single filter, then `dotnet-trace report <trace> topN -n 30 --inclusive` and the
   exclusive form. Archive both topN texts under `artifacts/prof/S58/`, name the subsystem
   inclusive time blamed and the method exclusive time blamed, and keep the `.speedscope.json`
   written alongside for the owner.
4. **Allocation attribution route proven**, on one snapshot:
   `dotnet tool install --global JetBrains.dotTrace.GlobalTools`, one Timeline capture of a scan
   workload, then `dotTraceGetSnapshotInfo` and `dotTraceGetCallTree` with `filterEvent: "memory"`
   through the Rider MCP, with an excerpt of the returned tree archived. PROFILING marks the
   end-to-end capture-then-read as **not verified**; this is the probe. If it cannot be made to
   work unattended, record exactly where it failed and fall back to `EventPipeProfiler(GcVerbose)`
   plus the arithmetic attribution PROFILING section 7 describes - do not leave the question open.
5. **`.claude/skills/optimise/SKILL.md`**, written from `OPTIMISATION-TECHNIQUES.md` section 3's
   fourteen steps plus the summary table, so every later Phase 7 session loads it instead of
   re-reading the research. It gains one step the research does not have, the sync-divergence ledger
   entry of item 7, and stays a checklist rather than a second copy of the research.
6. **`tools/compare-benchmarks.ps1` extended** (S54 committed it): an allocated-bytes/op ratio
   column beside the time ratio, and a `-NoiseFloor` parameter defaulting to the number step 1
   records, below which a ratio is reported `same` whatever the percentage. Per workload, never an
   average - the gate is per workload by design. Cover the new behaviour in `tools/tests`, run by
   `tools/run-tool-tests.ps1`.
7. **`docs/plan/SYNC-DIVERGENCE.md`**, the ledger for structural divergences from upstream's shape
   (owner ruling (c): divergence is allowed, the notes are enforced by checklist and script). One
   row per divergence: where the port and upstream no longer line up, why, the measured gain, and
   how to re-align when `sync-upstream` next touches that code. Enforced the way
   `OPTIMISATION-NOTES.md` enforces deferrals - a marker in the source paired with a row - through
   `tools/check-sync-divergence.ps1`: every `sync-divergence:` comment in `src/FuzzyRegex` must
   have a ledger row naming that file, and every row a live comment. The script fails on either
   half being missing and is called from the ratchet so it cannot be forgotten. The minimum gain
   that justifies a divergence stays deferred until S62 has concrete examples.
8. **Span-copy cost measured** (`FuzzyRegex.cs:338`, `:873`, the "biggest allocation win
   available"): benchmarks calling the `string` and `ReadOnlySpan<char>` overloads on the *same*
   workloads, with `MemoryDiagnoser`, over short, 1 KB and 1 MB subjects. Write the result up for
   the owner in `docs/plan/<date>-span-threading-decision.md`: the measured cost of the copy per
   call and per byte, what threading a span through `MatchState` would touch, and the `yield`
   constraint that blocks the obvious form. Recommend; the owner decides. **No threading here.**
9. **Lazy-walk per-step state cost measured** (`Iteration.cs:216`, `:336`): `EnumerateMatches`
   against `Matches` on the 1 MB subject, walked to the end and stopped after two, which is the
   pair S54 pinned for exactly this purpose, plus a group-count sweep so the bytes can be
   attributed arithmetically (`MatchState.Create`'s `GroupData[]`, `RepeatData[]`, three
   `ByteStack`s, two `long[]`s, one vectorised subject pass). Write the pooled-state-on-`Dispose`
   versus `ref struct` enumerator choice up in the same document, with the API consequence stated
   plainly: a ref struct enumerator is not an `IEnumerable<T>`, so the owner signs it off.

## Verification

- `pwsh -File tools/check-ratchet.ps1` GREEN, and `pwsh -File tools/run-oracle.ps1` GREEN at its
  three default seeds (7, 4242, 20260916) - `src/` is untouched, so both prove the slice changed
  nothing, which is the claim.
- `pwsh -File tools/run-aot-tests.ps1` and `pwsh -File tools/run-aot-smoke.ps1` GREEN, binary size
  recorded against the 6,972,928-byte baseline. `pwsh -File tools/run-tool-tests.ps1` GREEN, over
  the compare script's allocation ratio and floor verdict and `check-sync-divergence.ps1` failing
  on each half of the pair.
- `pwsh -File tools/compare-benchmarks.ps1` run A-against-B, printing `same` for every workload once
  the recorded floor is applied; that is the script's self-test. No `tools/update-public-api.ps1`
  run: the public surface does not change in this slice.

## Done when

- [ ] Noise floor measured and committed with date, SHA, job and machine state; the BDN affinity
      and GC-mode question answered from a real run's artifacts.
- [ ] pyperf installed; `pyperf system show` and `pyperf check` output archived with verdicts.
- [ ] EventPipe topN and the dotTrace/Rider MCP allocation route each proven on one real capture,
      with the text archived, or the failure and the fallback recorded.
- [ ] `.claude/skills/optimise/SKILL.md`, `SYNC-DIVERGENCE.md` and `check-sync-divergence.ps1`
      landed and wired into the ratchet.
- [ ] `compare-benchmarks.ps1` reports the allocation ratio and honours the floor, tool tests cover
      both; span-copy and lazy-walk costs measured, both decisions written up with numbers and put
      to the owner, neither implemented here.
- [ ] Every finding not acted on carries a `ponytail:`/`Phase 7` comment and an OPTIMISATION-NOTES
      row; no oracle answer changed.
- [ ] Ratchet, oracle at three seeds and AOT green; blind review (hunt: a floor taken with the
      driver running; a compare script that averages across workloads or ignores its own floor; a
      span benchmark whose result is dead-code eliminated; a pyperf baseline recorded although
      `check` warned; a `sync-divergence:` marker the script would not catch), commit.
