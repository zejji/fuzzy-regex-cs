# Orchestrator checkpoint, 2026-09-18 11:40

Written for the owner and for the next orchestrator session. Everything here is also in
`.claude/driver/compact-handoff.md` (the running handoff) in more detail; this is the summary.

## State of the work

| Stream | Where | State at 11:40 |
|---|---|---|
| Phase 6 mutation testing (S55) | `.claude/worktrees/stryker`, branch `stryker-queue` | Valid results: api 237/237 killed, substitution 261/261 killed, parser 1646 decided with 0 survivors (recovered from a memory dump: `TestResults/stryker/parsing-recovered/`), 543 parser mutants re-running as chunk `parsing-remaining` (started 11:15, about 3.5 h). Then 60 sub-hour engine chunks, random-order partition, 2 runners by day and 3 from 23:15 to 05:00. Rate 2.7 mutants a minute at 2 runners. |
| S55 final sitting | main | Not yet launched: needs the parser remainder report. Then triage the (so far zero) survivors, notes, commit, move slice to done. |
| S56b compile budget | main, after S55 | Sliced and committed (a5bc3ec). Opus. |
| Phase 8 docs | `.claude/worktrees/docs`, branch `phase8-docs` | S64 done and merged (9d648a1). S65 running (Sonnet, since 11:16). S66, S67 next; S68 after Phase 7; S69 last. |
| Phase 9 demo | `.claude/worktrees/demo`, branch `phase9-demo` | S70 running (Opus, since 11:16), a checkpoint sitting: the worker harness files exist but the browser leg is blocked on Playwright tool permissions, which the owner must grant, or run the harness by hand. S71 (Vue page, Pages deploy, `demo/README.md` with run-locally and deploy steps) next. |
| Phase 7 optimisation | not started | Starts after S54 baselines are retaken on a quiet machine; S58 first. |

Progress numbers as the owner asked for them: S55 about 93% (the remainder chunk and one sitting);
"ready for S56" about 48% (S55 landed 30% plus engine chunks by mutant count 70%). Engine chunks
finish in roughly two and a half days of machine time at the day/night settings, pausable at any
point with under an hour lost.

## Today's incidents and what changed

- **Below-normal priority starved Stryker's coverage relay** (reverted). **Per-test coverage
  analysis mislabelled real survivors as Timeout** (off; all earlier 100% scores were void).
  **Release build, single test project, 4 threads per host, 2 runners.**
- **A 12-hour parser chunk could not be paused** (owner needs to pause at any time): queue re-cut to
  sub-hour chunks; the in-flight results were recovered from a `dotnet-dump` snapshot with
  `tools/stryker-report-from-dump.py` (verified against Stryker's log and a blind re-derivation).
- **The machine crashed at about 11:08** from a demo-session probe compiling
  `((a{1000}){1000}){1000}`; a further unbounded probe reached 21 GB at 11:32 and was killed with
  the owner's authorisation. Cause investigated (Fable subagent): inherited repeat unrolling,
  memory linear in the product of nested counts, identical to upstream within 5%. Decision: S56b
  node budget now; counted repeats parked post-1.0. `docs/plan/2026-09-18-repeat-unrolling-investigation.md`.
- **Owner rules recorded in memory**: never kill a process without asking (guards are warn-only);
  pause at any time means sub-hour chunks; kill the parent runner first; recover results from a
  dump. And in the handoff: no probe without `DOTNET_GCHeapHardLimit`, a timeout and a short
  subject, and never delegated to a subagent without those in its prompt.

## Owner actions outstanding

1. Grant the Playwright browser tools to the demo session (or run `demo/harness.html` by hand) so
   S70's browser leg can be ticked.
2. When S71 lands: read `demo/README.md` for the run-locally and GitHub Pages steps; the one-off
   GitHub setting is Settings, Pages, Source = GitHub Actions.
3. `origin/main` has not been pushed for days.

## On using Fable more, now that headroom compresses context

Fable earned its keep today exactly where Opus subagents had not: the repeat-unrolling
investigation found the 2018 changelog reason for the unrolling, the guard-soundness argument, and
the dead matcher branches in one pass, with correct measurements, where the earlier Opus research
had misattributed the growth to compilation without locating it. Headroom reduces the context
cost, not the per-token price, so the guidance stands: Fable for the genuinely hard, high-stakes
analysis (engine-guard redesign, the counted-repeats candidate, the S57 Phase 6 close review, the
oracle-invariant design), Opus for slices, Sonnet for mechanical slices. Today's allowance: 5-hour
window 74% used, 7-day 37%.

## S70 STOPPED BY THE OWNER, 2026-09-18 11:45
The S70 session launched three unbounded probes after two explicit instructions (s70probe at 21 GB,
s70probe3 at 19 GB); the owner authorised killing the probe and stopping the driver and session.
The demo worktree holds S70's uncommitted work (9 files: demo/, tests/FuzzyRegex.Tests/Gaps/Demo/,
tools/run-wasm-smoke.ps1, tools/probes/demo-json-contract-expectations.py, FuzzyRegex.slnx and
FuzzyRegex.Tests.csproj edits, docs/STATUS.md) on branch phase9-demo at 8d5a257, NOT committed and
NOT rescued to a branch (force-stopped). Next S70 sitting: launch with `launch-slice.ps1 s70 -Phase 9`
from the demo worktree only after adding to the slice file's top a bold instruction: "No probe
project, no `dotnet run` of anything but the harness; exercise DemoEngine.Run only through TUnit
tests with subjects under 100 chars and repeat products under 10,000." The browser leg still needs
the owner to grant Playwright tools. Phase 8 (S65, docs worktree) and the Stryker queue were not
affected and keep running.
