<#
.SYNOPSIS
    Records a differential oracle wave from upstream and diffs this port's matching against it.

.DESCRIPTION
    Record, consume and verdict in one command. From phase 3 on, this is what every slice that
    touches the engine runs locally before it commits (VERIFICATION.md rule 7): the ported suite
    passing is evidence of parity, not proof of it.

    Deliberately not a merge gate (design spec amendment 7). It needs Python, its rows change from
    run to run, and a flaky ground truth must not be able to block a merge. CI runs it on a
    schedule from .github/workflows/oracle.yml; a divergence it finds is minimised into an ordinary
    permanent test in tests/FuzzyRegex.Tests/Gaps/ rather than left living in a wave.

.PARAMETER Generator
    Comma-separated generator names. See tools/record-oracle.py for what each emits.

.PARAMETER Seed
    The generator seed. Omitted, the recorder picks one at random and records it in the wave
    header, which is what makes a divergence reproducible.

.PARAMETER Count
    Rows per generator.

.PARAMETER Rows
    Record these explicit rows (JSONL) instead of generating any - the minimisation path.

.PARAMETER SkipRecord
    Re-run the consumer against the wave already on disk, without recording a new one.

.EXAMPLE
    tools/run-oracle.ps1
    tools/run-oracle.ps1 -Count 2000
    tools/run-oracle.ps1 -Rows .scratch/candidate.jsonl
    tools/run-oracle.ps1 -SkipRecord
#>
[CmdletBinding()]
param(
    [string]$Generator = 'literals,literal-dot,anchors,classes,groups,quantifiers,boundaries,backrefs,case-folding,reverse,substitution,iteration',
    [int]$Seed = -1,
    [int]$Count = 300,
    [string]$Rows,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$SkipRecord
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$wavePath = Join-Path $repoRoot 'TestResults/oracle/wave.jsonl'
$reportPath = Join-Path $repoRoot 'TestResults/oracle/report.txt'

if (-not $SkipRecord) {
    Write-Host 'Recording a wave from upstream...' -ForegroundColor Cyan
    $recorderArgs = @((Join-Path $PSScriptRoot 'record-oracle.py'), '--output', $wavePath)
    if ($Rows) {
        $recorderArgs += @('--rows', $Rows)
    }
    else {
        $recorderArgs += @('--generator', $Generator, '--count', $Count)
        if ($Seed -ge 0) { $recorderArgs += @('--seed', $Seed) }
    }

    python @recorderArgs
    if ($LASTEXITCODE -ne 0) {
        # A wave that did not record is not a wave that agreed. The most likely cause by far is
        # the version policy: `pip install regex` on a dev machine, or a stale submodule.
        Write-Host 'Oracle: RED - the recorder failed, so nothing was compared.' -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path -LiteralPath $wavePath)) {
    Write-Host "Oracle: RED - there is no wave at $wavePath." -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host "Running the consumer ($Configuration)..." -ForegroundColor Cyan
# Deleted first, so a consumer that fails before it writes one - a malformed wave, a build error -
# cannot leave the previous run's report on screen as if it described this one.
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }
# A non-zero exit means a row diverged, or the harness's own tests failed. Either way the report
# below is what says which, so the exit code is captured rather than thrown on.
dotnet test (Join-Path $repoRoot 'tests/FuzzyRegex.OracleTests/FuzzyRegex.OracleTests.csproj') `
    --configuration $Configuration
$consumerExit = $LASTEXITCODE

Write-Host ''
if (Test-Path -LiteralPath $reportPath) {
    Get-Content -LiteralPath $reportPath | Write-Host
}
else {
    Write-Host "No report at $reportPath - the consumer did not get as far as the wave." -ForegroundColor Yellow
}

Write-Host ''
if ($consumerExit -eq 0) {
    Write-Host 'Oracle: GREEN - no row diverged from upstream.' -ForegroundColor Green
    exit 0
}

Write-Host 'Oracle: RED' -ForegroundColor Red
Write-Host "  The wave that found it is at $wavePath, and the header records its seed." -ForegroundColor Yellow
Write-Host '  Minimise each diverging row and pin it as a test: see the header of' -ForegroundColor Yellow
Write-Host '  tools/record-oracle.py for the workflow, VERIFICATION.md rule 7 for why.' -ForegroundColor Yellow
exit 1
