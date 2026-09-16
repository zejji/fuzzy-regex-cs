<#
.SYNOPSIS
    Runs Stryker.NET mutation testing over one chunk (a --mutate glob) or a queued list of them.

.DESCRIPTION
    Calibrated 2026-09-16 on src/FuzzyRegex/FuzzyCounts.cs: 9982 mutants were generated for the
    whole FuzzyRegex project, 8876 filtered out by -m, 1106 left over from files the glob did not
    match (a stale mutant set Stryker still reports; harmless), and the 2 mutants actually inside
    the chunk were both killed in about 40s of test time. Total wall time 5m50s, almost all of it
    a FIXED cost: MTP's per-test coverage capture runs the whole 6288-test suite once per
    invocation, independent of chunk size.

    THAT FIXED COST DOES NOT MEAN "BATCH MANY FILES PER CHUNK" - a single real file already
    overwhelms it. src/FuzzyRegex/*.cs (six small API files) gave 237 testable mutants in about
    15 minutes; src/FuzzyRegex/Parsing/*.cs (eight files) gave 2189 in a run that was still going
    after 45 minutes. FuzzyCounts.cs's 2 mutants were the exception, not the rule - most real
    files have plenty of mutable logic. Chunk by FILE (or by line span within one large file, see
    -Mutate below), not by directory.

    -Mutate TAKES EXACTLY ONE GLOB. Passing several (`-m a -m b -m c`, whether as repeated -m
    flags or as separate array entries in tools/stryker-queue.json) was tried on 2026-09-16 for
    '*.cs','Parsing/*.cs','Engine/Substitution.cs' together and silently mutated NOTHING from any
    of the three - 0 mutants tested, the same totals as an unfiltered whole-project run. The root
    cause was not chased down (each of the three globs was confirmed to work correctly alone), so
    until it is: call this script once per file/span, never with more than one -Mutate value.
    tools/stryker-queue.json follows this - every entry has exactly one glob.

    Requires `-t mtp` (Stryker's TUnit/Microsoft Testing Platform runner - still preview, design
    spec amendment 14) and `--target-framework net10.0`, both set in stryker-config.json. Without
    --target-framework, Buildalyzer's per-project multi-targeting probe fails on a machine that
    has VS 2022 Build Tools 17.14 installed alongside the .NET 10 SDK: it falls back to invoking
    Build Tools' full-framework MSBuild.exe directly, which cannot resolve "Microsoft.NET.Sdk"
    (MSB4276) because Build Tools ships no .NET 10 SDK resolver. --target-framework net10.0
    sidesteps the failing probe entirely - no MSBuildSDKsPath or --msbuild-path override needed.
    Verified 2026-09-16 (S55 sitting 2): sitting 1 hit the MSB4276 failure and did not find this;
    the orchestrator's suggested routes (export MSBuildSDKsPath, or --msbuild-path pointed at the
    SDK's own MSBuild.dll) were not needed once --target-framework was added.

    -Mutate globs are relative to the mutated project's own directory (src/FuzzyRegex), NOT the
    repo root: 'FuzzyCounts.cs' matches, 'src/FuzzyRegex/FuzzyCounts.cs' silently matches nothing
    and Stryker mutates the whole project instead of erroring (verified 2026-09-16).

    Refuses to start while a `dotnet build`/`dotnet test` is already running: Stryker's own build
    simulation writes to the same obj/bin outputs check-ratchet.ps1 uses, and a concurrent build
    corrupted one such file and rolled back a green commit once already (S26 postmortem;
    tools/find-lock-holder.ps1).

    Resumable: a chunk whose report already exists under TestResults/stryker/<chunk>/ is skipped
    unless -Force is passed, so an interrupted -Queue run can be relaunched and only does the
    chunks it has not finished.

    Coverage-capture timeouts on the slowest tests (ThreadSafetyStressTests, OptimiserTrapsTests,
    parts of InheritedIssueTests - all deliberately expensive: real parallelism, megabyte scans,
    ten-thousand-deep recursion) are EXPECTED and left alone, not excluded. Stryker marks a test
    "Dubious" when it cannot finish the initial per-test coverage capture in time and falls back
    to treating it as covering every mutant, which costs speed, not correctness - and excluding
    these tests would blind mutation testing to exactly the edge-case tests Phase 6/7 care about
    most. additional-timeout/timeout-ratio (config-only, no CLI flag) govern the PER-MUTANT test
    timeout, not this initial capture step, so there is nothing to tune here; accept the Dubious
    mark. Verified 2026-09-17.

.PARAMETER Chunk
    Short name for this run. Reports and the log go to TestResults/stryker/<chunk>/.

.PARAMETER Mutate
    Exactly one --mutate glob pattern (see DESCRIPTION for why more than one silently mutates
    nothing, and for how the one glob resolves).

.PARAMETER Queue
    Run every chunk listed in tools/stryker-queue.json in order. A chunk whose run errors is
    logged and the queue moves on; it never stops on one chunk's failure.

.PARAMETER Force
    Re-run a chunk even if TestResults/stryker/<chunk>/reports/mutation-report.json already exists.

.EXAMPLE
    tools/run-stryker.ps1 -Chunk calibration -Mutate 'FuzzyCounts.cs'

.EXAMPLE
    tools/run-stryker.ps1 -Chunk parsing -Mutate 'Parsing/*.cs'

.EXAMPLE
    tools/run-stryker.ps1 -Queue
#>
[CmdletBinding(DefaultParameterSetName = 'Single')]
param(
    [Parameter(ParameterSetName = 'Single', Mandatory)][string]$Chunk,
    [Parameter(ParameterSetName = 'Single', Mandatory)][string]$Mutate,
    [Parameter(ParameterSetName = 'Queue', Mandatory)][switch]$Queue,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Join-Path $repoRoot 'TestResults/stryker'

function Test-BuildInFlight {
    # CommandLine is only on Win32_Process (CIM), not System.Diagnostics.Process.
    # The leading comma stops PowerShell enumerating the array on return: `return @(...)` with
    # zero matches collapses to $null at the call site, not an empty array (verified 2026-09-16).
    $procs = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue
    , @($procs | Where-Object { $_.CommandLine -match '\bdotnet(\.exe)?"?\s+(test|build)\b' })
}

function Invoke-StrykerChunk {
    param([string]$ChunkName, [string]$MutateGlob)

    $chunkDir = Join-Path $outputRoot $ChunkName
    $reportPath = Join-Path $chunkDir 'reports/mutation-report.json'
    if ((Test-Path -LiteralPath $reportPath) -and -not $Force) {
        Write-Host "Skipping '$ChunkName': report already exists at $reportPath (-Force to re-run)." -ForegroundColor Yellow
        return $true
    }

    $blockers = Test-BuildInFlight
    if ($blockers.Count -gt 0) {
        Write-Host "Refusing to start '$ChunkName': a dotnet build/test is already running (pid $($blockers[0].ProcessId))." -ForegroundColor Red
        return $false
    }

    New-Item -ItemType Directory -Force -Path $chunkDir | Out-Null
    $logPath = Join-Path $chunkDir 'run.log'

    Write-Host "Stryker chunk '$ChunkName': mutate = $MutateGlob" -ForegroundColor Cyan

    Push-Location $repoRoot
    try {
        & dotnet stryker -m $MutateGlob -O $chunkDir --skip-version-check -V info 2>&1 |
            Tee-Object -FilePath $logPath
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        Write-Host "Chunk '$ChunkName' exited $exitCode - see $logPath." -ForegroundColor Red
        return $false
    }
    Write-Host "Chunk '$ChunkName' done - report at $reportPath." -ForegroundColor Green
    return $true
}

if ($Queue) {
    $queuePath = Join-Path $repoRoot 'tools/stryker-queue.json'
    $chunks = @(Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json)
    $failed = @()
    foreach ($c in $chunks) {
        $globs = @($c.mutate)
        if ($globs.Count -ne 1) {
            throw "Chunk '$($c.chunk)' has $($globs.Count) mutate globs; exactly one is supported (see DESCRIPTION)."
        }
        $ok = Invoke-StrykerChunk -ChunkName $c.chunk -MutateGlob $globs[0]
        if (-not $ok) { $failed += $c.chunk }
    }
    if ($failed.Count -gt 0) {
        Write-Host "Queue finished with failures: $($failed -join ', ')" -ForegroundColor Red
        exit 1
    }
    Write-Host 'Queue finished: all chunks done.' -ForegroundColor Green
    exit 0
}

$ok = Invoke-StrykerChunk -ChunkName $Chunk -MutateGlob $Mutate
exit ($ok ? 0 : 1)
