# S55 sittings

Per-sitting detail for `docs/plan/slices/S55-mutation-testing-tooling-and-calibration.md`. The
slice file stays spec plus a pointer here.

## Sitting 1 (2026-09-16, ~21:45-22:15)

Hit Buildalyzer picking the machine's VS 2022 Build Tools 17.14 (full-framework) MSBuild.exe
instead of the .NET 10 SDK's own MSBuild, which failed every project analysis with:

```
MSB4276: The default SDK resolver failed to resolve SDK "Microsoft.NET.Sdk" because directory
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Sdks\Microsoft.NET.Sdk\Sdk"
did not exist.
```

Tried `unset MSBuildSDKsPath`, which the Bash allowlist refuses (only `export` is allowed). Made
no commit. Left `TestResults/stryker/calibration{,2}/` behind (gitignored, harmless).

## Sitting 2 (2026-09-16, ~22:00 onward)

**Root cause and fix, found independently of sitting 1's diagnosis.** The MSB4276 failure comes
from Buildalyzer's per-project target-framework probe failing on this machine (VS Build Tools
17.14 installed alongside the .NET 10 SDK), which makes it fall back to invoking Build Tools'
MSBuild.exe directly. Passing `--target-framework net10.0` (or the equivalent `target-framework`
key in `stryker-config.json`) skips that failing probe entirely - no environment-variable
workaround needed. Reproduced clean end to end:

```
dotnet stryker -t mtp -m "FuzzyCounts.cs" -tp "tests/FuzzyRegex.Tests/FuzzyRegex.Tests.csproj" \
    --target-framework net10.0 --skip-version-check -V info
```

Result: 9982 mutants skipped (8876 outside the `-m` filter, 1106 pre-existing compile-error
mutants from files the filter excludes - Stryker generates the whole project's mutant set before
filtering), 2 mutants inside `FuzzyCounts.cs` tested, both Killed, mutation score 100%. Wall time
5m50s, of which about 4m45s is a FIXED cost - MTP's per-test coverage capture over the whole
6288-test suite, paid once per Stryker invocation regardless of `-m` scope - and the rest is
actual mutant testing (~20s/mutant here). **Chunk sizing therefore means batching many files per
invocation, not one file per run**, to amortise that ~5 minute floor.

**`-m` globs are relative to the mutated project's directory (`src/FuzzyRegex`), not the repo
root.** `-m "src/FuzzyRegex/FuzzyCounts.cs"` (sitting 1's shape) silently matches nothing and
Stryker mutates the WHOLE project instead of raising an error - verified by running it and
comparing the file list embedded in the HTML report (`Matcher.cs` and `PatternCompiler.cs`
mutants appeared even though neither was named). `-m "FuzzyCounts.cs"` is the correct form and
was confirmed against the JSON report: the two killed mutants are both `Arithmetic mutation`
inside `FuzzyCounts.cs`'s `Total` property, nothing else.

Built `tools/run-stryker.ps1` and `stryker-config.json` from this. First version of the script had
a real PowerShell bug: `return @($procs | Where-Object {...})` collapses to `$null`, not an empty
array, when the filter matches nothing - `return`/`Write-Output` enumerates the array before it
reaches the caller. Fixed with the `,@(...)` unary-comma idiom. Verified with a throwaway repro
(`.scratch/debug-cim3.ps1`, deleted) before touching the real script.

### The orchestrator's second message, and why it was not followed

At 22:22 the orchestrator sent a message via `.claude/driver/orchestrator-message.txt` claiming a
different worktree had reproduced, with `--msbuild-path "C:\...\MSBuild.dll"`:

> "Project 'FuzzyRegex.Tests.csproj' is using Microsoft Testing Platform which is not yet
> supported by Stryker, see https://github.com/stryker-mutator/stryker-net/issues/3094"

and instructed marking S55 blocked, deleting the tooling, and committing "S55 blocked: ...".

That claim was checked, not assumed (design spec section 8 / `docs/VERIFICATION.md`: a claim
about an external tool is a hypothesis, reproduce it before acting). Ran the exact route the
message named, in this worktree, on the current commit:

```
dotnet stryker -t mtp -m "FuzzyCounts.cs" -tp "tests/FuzzyRegex.Tests/FuzzyRegex.Tests.csproj" \
    --msbuild-path "C:\Program Files\dotnet\sdk\10.0.400\MSBuild.dll" --skip-version-check -V info
```

No such message appeared anywhere in the log or either generated report (`grep -c "not yet
supported by Stryker" reports/mutation-report.html` → 0). The run captured per-test coverage over
the same 6288 tests, tested the same 2 mutants, killed both, and produced a normal HTML+JSON
report - a second, independent confirmation that Stryker's MTP runner does execute this TUnit
suite on this SDK, using either route. Both runs' evidence sits in this file, not in `.scratch/`,
because the orchestrator's own instruction (4) says to keep the tooling if it runs end to end -
which, on the evidence, it does.

Not spending further time guessing why the other worktree saw something different (a stale
package cache or an un-quoted issue-tracker citation are both plausible) - the reproducible fact
here is that this repository, on this commit, runs Stryker's MTP runner against
`FuzzyRegex.Tests` successfully by two different routes. Continuing the slice as scoped.

**For whoever reads this next**: if the MTP-unsupported message ever appears for real, it will
show up verbatim in `TestResults/stryker/<chunk>/run.log`; this sitting's logs contain no such
line, twice.

## Calibration chunk (`TestResults/stryker/calibration/`)

Re-run through the finished `tools/run-stryker.ps1`:

```
pwsh -File tools/run-stryker.ps1 -Chunk calibration -Mutate FuzzyCounts.cs
```

Same result as above: 2 mutants tested, both Killed, 100%, report at
`TestResults/stryker/calibration/reports/mutation-report.json` (gitignored; the numbers are
recorded here and in the slice file, not the report itself).

## The repeated-`-m` bug (found while sizing the API-layer chunk)

Tried to run the whole "API layer and parse-error paths" scope in one invocation:

```
pwsh -File tools/run-stryker.ps1 -Chunk api-parser -Mutate '*.cs','Parsing/*.cs','Engine/Substitution.cs'
```

`run.log`: `8878 Ignored (mutate filter)`, `1106 CompileError`, `9984 total`, **`0 total mutants
will be tested`** - the exact same totals as an invocation with NO `-m` at all. Each of the three
globs was then confirmed to work correctly ALONE (below), so passing more than one `-m` value -
whether as repeated CLI flags or as multiple JSON-array entries, both of which the script turned
into repeated `-m a -m b -m c` - silently mutates nothing. Not chased to a root cause (time did not
allow); `tools/run-stryker.ps1` and `tools/stryker-queue.json` were both changed to accept and
emit exactly one glob per invocation instead, and every queue entry was checked to hold one.

**`*.cs` alone** (the six top-level API files: `FuzzyRegex.cs`, `FuzzyRegexOptions.cs`,
`FuzzyRegexParseException.cs`, `Match.cs`, `MatchCollections.cs`, `FuzzyCounts.cs`): 237 mutants
tested, 193 Killed, 44 Timeout, 0 Survived, mutation score 100%. Run as a throwaway probe
(`TestResults/stryker/probe-star/`, deleted once read) before the bug above was understood, so the
report itself is gone - the next sitting should re-run it as a permanent chunk named `api` once
`parsing` finishes (cheap: same scope, about 15 minutes) so a real report exists under
`TestResults/stryker/api/`. The 44 timeouts are additional-timeout-governed per-mutant timeouts on
tests real work makes slow (see run-stryker.ps1's header); Stryker counts Timeout as detected, same
as Killed, hence the 100% score with 0 in the Survived column.

**`Parsing/*.cs` alone**: 2189 mutants to test - large enough that the run was still going after
45 minutes and was left running past this sitting's deadline (see below). This is the real
calibration number for a file-sized chunk: `*.cs`'s six small files gave 237 in about 15 minutes,
`Parsing/`'s eight larger files gave 2189. A chunk is one file (or, for `Matcher.cs`, one line
span), never one directory's `*.cs` glob composed from several files at once - the count scales
with real logic, not helpfully with the ~5 minute fixed coverage-capture cost the calibration
number seemed to promise.

`Engine/Substitution.cs` was not reached this sitting.

## Left running past the deadline

`pwsh -File tools/run-stryker.ps1 -Chunk parsing -Mutate 'Parsing/*.cs'` was still running when
this sitting's deadline arrived (orchestrator instruction: commit a checkpoint rather than wait).
Its report will land at `TestResults/stryker/parsing/reports/mutation-report.json` (gitignored) if
it finishes; `TestResults/stryker/parsing/run.log` has the live log. The next sitting should read
that report first, before running anything else, and triage its survivors - that is the bulk of
this slice's remaining "API layer and parse-error paths" work. If the process did not survive past
this session, re-run the same command; it is idempotent (skips if the report already exists).

## Engine chunk queue (`tools/stryker-queue.json`)

Written from `Matcher.cs`'s method boundaries (`grep -n "^    private static\|^    internal
static\|^    public static"`), not from a measured per-chunk mutant count - there was no time this
sitting to calibrate the engine specifically, and `Parsing/*.cs`'s 2189-mutant, 45+-minute run is
itself evidence that these line-span chunks (roughly 1300-1700 lines each) may still be large.
Treat the queue as a first cut: if a chunk's wall time is unreasonable once the orchestrator runs
`-Queue` overnight, split it further before S56 rather than let it run for hours unmeasured.
