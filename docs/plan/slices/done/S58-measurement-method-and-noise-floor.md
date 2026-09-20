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

**Workload added 2026-09-18 (research sweep):** a fuzzy no-match large-subject case, e.g.
`(?:needle){e<=1}` over 1 MB with no near-occurrence, alongside the existing set. It is the
workload S60 item 10 exists for; without a baseline here that item cannot be judged.

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

- [x] Noise floor measured and committed with date, SHA, job and machine state; the BDN affinity
      and GC-mode question answered from a real run's artifacts.
- [x] pyperf installed; `pyperf system show` and `pyperf check` output archived with verdicts.
- [x] EventPipe topN and the dotTrace/Rider MCP allocation route each proven on one real capture,
      with the text archived, or the failure and the fallback recorded. (The allocation route
      **failed**: nothing installed reads a captured trace back to a source site. The failure, both
      attempted routes and the arithmetic fallback - now demonstrated on real numbers - are in
      `phase7-research/profiles/README.md`.)
- [~] `.claude/skills/optimise/SKILL.md`, `SYNC-DIVERGENCE.md` and `check-sync-divergence.ps1`
      landed and wired into the ratchet. (Ledger and script landed and wired. The skill file is
      **not landed**: this session cannot write under `.claude/`, proven by two independently
      refused routes on two days. Finished body parked in
      `phase7-research/optimise-skill-pending.md`, linked from ROADMAP's Phase 7 entry, one move
      for the owner. Blocks no slice.)
- [x] `compare-benchmarks.ps1` reports the allocation ratio and honours the floor, tool tests cover
      both; span-copy and lazy-walk costs measured, both decisions written up with numbers and put
      to the owner, neither implemented here.
- [x] Every finding not acted on carries a `ponytail:`/`Phase 7` comment and an OPTIMISATION-NOTES
      row; no oracle answer changed. (One exception, deliberate and recorded: the `IsMatch`
      `visibleCaptures` finding has its OPTIMISATION-NOTES row but **not** its source comment,
      because this slice may not touch `src/`. Handed to S60; see DECISIONS 2026-09-19.)
- [x] Ratchet, oracle at three seeds and AOT green; blind review (hunt: a floor taken with the
      driver running; a compare script that averages across workloads or ignores its own floor; a
      span benchmark whose result is dead-code eliminated; a pyperf baseline recorded although
      `check` warned; a `sync-divergence:` marker the script would not catch), commit. (Oracle red
      at 1 of 3 seeds on one pre-existing triaged row, and AOT tests red on a pre-existing IL2065,
      both reproduced byte-identically against an untouched `src/`; see the sittings notes.)

---

## Closing notes - 2026-09-19, four sittings

Per-sitting detail is `docs/plan/slices/notes/S58-sittings.md`; this is what a later slice needs.

**What landed.** This machine's noise floor (`bench/baselines/<machine-id>/noise-floor.md`) with the
BDN affinity and GC-mode question answered from a real run's generated `.csproj` and
`runtimeconfig.json`; pyperf installed, `system show` and `check` archived with verdicts and the
Python side's own floor measured; the CPU profile route proven end to end and archived under
`artifacts/prof/S58/`; `compare-benchmarks.ps1` with an allocation-ratio column and a `-NoiseFloor`
default, covered by tool tests; `SYNC-DIVERGENCE.md` and `check-sync-divergence.ps1`, wired into the
ratchet; and two written decisions for the owner - the span-threading and lazy-walk document
(`docs/plan/2026-09-19-span-threading-decision.md`) and the allocation attribution behind it.
**No `src/` change:** `git diff 2c1e747 -- src` is empty, which is the slice's own claim about
itself.

**The two things a later slice should know.**

1. **The allocation profiler route is closed, and the arithmetic route is now a demonstrated
   method rather than a plan.** Nothing installed reads a captured .NET allocation trace back to a
   source site; the fallback is `bench/FuzzyRegex.Benchmarks/Attribution.cs`, run with
   `dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- attribution`, which measures
   `MatchState.Create`, `IsMatch` and `Match` at every point of both sweeps. Its result: a capture
   group costs **264.00 B** in a `Match`, of which **40.00 B is the state** and **224.00 B is
   outside it**, at all five steps with no residual; the state's own line is **984 B + 40 B per
   group**. That corrected step 2 of the decision document, which had told the owner pooling was
   worth 1,296 B a step.
2. **`IsMatch` allocates byte-for-byte what `Match` allocates**, because `IsMatch` is
   `Run(...).Success` and `Run` passes `visibleCaptures: true` unconditionally - a predicate call
   pays 224 B per group for captures nobody can read. It has its `OPTIMISATION-NOTES.md` row but
   **not** its paired `ponytail:` source comment, because this slice may not touch `src/`. **S60
   owes that comment**, and nothing enforces the pairing automatically.

**Still open for the owner, blocking nothing.** `.claude/skills/optimise/SKILL.md` cannot be written
by an unattended session - two independent routes refused at the permission layer, on two days. The
finished body is in `docs/plan/phase7-research/optimise-skill-pending.md`, linked from ROADMAP's
Phase 7 entry, and moving it is one command.

**No negative control was run**: this slice runs no oracle wave of its own and changes no engine
behaviour, so there is nothing for a control to detect.

**Review.** One blind pass over the sitting-4 diff raised **2 findings; both reproduced; both
fixed.** (1) The state's line was written as "1,024 B a step plus 40 B per group", which
double-counts the first group - `984 + 40n` fits all six measured `Create` rows and `1,024 + 40n`
fits none. (2) The probe's `(first Match)` column was not a first call: `Measure` runs each
operation nine times and `IsMatch` shares `Match`'s `Run` path, so `Match`'s "first" sample is that
path's tenth walk. The probe now reports a first figure for `Create` and `IsMatch` only, which
exposed a genuinely-first `Run` at **11,848 B against a steady 1,392 B** - a one-time cost the old
column hid. Because those fixes changed `Attribution.cs` and re-quoted every table, **a second blind
pass over that delta was required and was run**: it raised **6 findings, all 6 reproduced and
fixed** - a first-call figure in a source comment quoted from the reviewer's own control rather than
from this tree (12,504 B, corrected to this run's 11,848 B); "nineteenth walk" for what the code
makes the tenth; "five rows" for six; a 74% share taken against 1,392 B in a section headed 1,296 B,
now stated with the 96 B offset; a per-pattern explanation for a first-call excess that the numbers
show scales per group and has not been isolated; and two quoted output blocks that silently dropped
the run's interleaved slope lines. **The independent verifier** (fresh Opus, briefed only with the
tree) re-ran the probe, the ratchet, the tool tests, the AOT smoke and the archived BDN artifacts
and reported every quoted number CONFIRMED, with one DIFFERENT and three COULD NOT RUN: the
DIFFERENT was the gates paragraph citing `git diff dfa8767 -- src` as empty when `dfa8767` is a
pre-rebase duplicate of S56b that is not an ancestor of this branch - corrected to `2c1e747`, the
S56b commit that is, against which it is empty. The three COULD NOT RUN were the oracle wave, the
AOT test run and S54's 12,643 ms figure, none in its commanded set, all run by the session itself
and quoted from that output. Sitting 1's calibration wall-clock times are one run's medians and are
not reproducible by re-running; no conclusion rests on them.
