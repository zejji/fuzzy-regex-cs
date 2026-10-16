# Correctness battery

One run looks for correctness root causes four ways at once and writes a triage table. It is the
battery of the plan to 1.0 (`docs/plan/2026-10-16-plan-to-1.0.md`, section 3). A full run takes
about 3-4 hours; it is not timing, so it can run at any hour, but it waits while a timing run holds
`.scratch/test-lock`.

    tools/battery/run.sh 20261016-1 --commit 84dff3fd      # launches detached, returns at once
    tools/battery/run.sh smoke1 --commit main --smoke      # a few rows per part, a few minutes

The run id is the date and a run number; its digits are the master seed every part draws from, so
the same id reproduces the same rows. Launching an id again resumes it: finished parts and finished
steps inside a part are skipped. Without `--commit` the run tests main's current HEAD.

Output goes to `.scratch/battery/<run-id>/` in the main checkout:

| File | What |
|---|---|
| `status` | progress, one line per event; the place to watch |
| `config.json` | the commit, seeds and sizes, fixed when the run starts |
| `triage.md` | the result: one row per disagreement cluster, with a witness |
| `findings.jsonl` | every interesting row, its cluster and every reference's verdict |
| `oracle/`, `bed/`, `control/`, `pairs/`, `matrix/`, `rows/` | each part's own output |

Stop a run with `taskkill //PID $(cat .scratch/battery/<run-id>/driver.pid) //T //F`. Once the
triage has been read, `python tools/battery/battery.py clean <run-id>` removes the run's worktrees.

## Parts

The commit under test is checked out in detached worktrees, `.claude/worktrees/bat-<run-id>-<part>`,
one per part, so parts running at once never share a build folder. Each heavy step first waits
until no timing run holds the lock, 6 GB plus the step's own need (1-2 GB) is free, and the CPU is
under 85%. A smoke run (`--smoke`) takes about 8 minutes, most of it the first builds and the
oracle consumer's own test project (about 6 minutes in Debug whatever the row count).

| Part | Rows | Reference | How |
|---|---|---|---|
| `oracle`, `oracle2` | 10 seeds x 26 generators x 2,000, alternate seeds each | upstream `regex` 2026.9.10 | `tools/sweep-seeds.ps1`, one seed per call, Debug consumer |
| `probe` | the D85 bed, 276,591 | Python `re`, `regex` | `probe.cs` (Debug), then the bed's `compare.py` |
| `probe` (control) | pair and matrix rows it applies to | the same pattern with the repeat guards off | `probe.cs`, plain and wrapped |
| `pa`, `pb` | 35 cells x 2,500 (two halves) | upstream, PCRE2 + Perl, `re`, reference matcher, TRE | `gen/pairs.py`, then `tools/matrix/run.py` |
| `matrix` | the complete matrix at two new seeds | as above | `tools/matrix/gen.py` and `run.py` |

Notes on what each part does and why:

- **Debug consumer.** The oracle runs with `-Configuration Debug` so the engine's `Debug.Assert`
  invariants run. A Debug consumer is slow enough to time out on long rows, and
  `run-oracle.ps1` warns that such a timeout is not a divergence, so every diverging row is re-asked
  with a Release consumer and a row that agrees there is counted as a Debug timeout. A failed assert
  ends the test host (a failed `Debug.Assert` calls `Environment.FailFast` when no listener is
  installed), so that seed gets no verdict; its wave is then run through
  `tools/matrix/port-runner.cs`, which turns asserts into answers, to name the rows.
- **The probe.** `probe.cs` is `tools/probes/port-probe.cs` with a listener that turns a failed
  assert into an exception, so a Debug run reports the row instead of dying.
- **Guards-off control.** Upstream does not consult the repeat guards inside a fuzzy section. Wrapping
  a pattern as `(?:P){s<=1:[\x00]}` makes it fuzzy without allowing any edit (the subjects never hold
  `\x00`), so the guards are off and nothing else changes. It is applied only where that holds: no
  fuzzy section, verb, `\K`, call, partial match, flag outside the pattern or named list.
- **Feature pairs.** `gen/pairs.py` builds rows where two constructs meet: fuzzy x {backref,
  lookaround, atomic, verb, conditional, `\K`, reverse, partial}; `\K` x {atomic, possessive,
  `(*PRUNE)`, `(*SKIP)`, lookahead, lookbehind}; backref x repeat; partial x {lookaround, atomic,
  verb}; and each of those reversed. `(*COMMIT)` is left out because upstream refuses it ("unknown
  verb"). Ruby is not a reference: it is not installed here.

## Triage

`triage.py` judges each row of the pair and matrix runs again with `run.py`'s own `judge_row`, adds
Python `re` and the guards-off control as references, and keeps a row when the port disagrees with
every reference that answered, when the references disagree and the port sides with one, or when a
self-check fails (an assert or exception, memo on or off, a partial answer, a written-out call).
Rows cluster by source, category, which references fail and pass, and the cell (pairs) or the risky
constructs (matrix, oracle). Attribution is automatic only where an existing screen decided it
(ExpectedDivergences, judged rows, the MemorySanitizer screen); a cluster that matches a known
signature gets a HINT; the rest are UNJUDGED. Each cluster's witness is its smallest row, shrunk by
a batch delta reducer while the port still differs from upstream and the row keeps its constructs.
Nothing is fixed or registered here; that is the orchestrator's job.
