<#
.SYNOPSIS
    Runs the test suite, enforces the parity ratchet and regenerates docs/STATUS.md.

.DESCRIPTION
    The merge gate. A run is green only when every test recorded in tests/parity-baseline.json
    still passes and nothing else fails, so a change that fixes one thing by breaking another
    cannot land (design spec section 5).

    Used by CI on all three operating systems and by tools/run-slices.ps1 to decide whether a
    slice session actually succeeded.

.PARAMETER UpdateBaseline
    Records the current passing set as the new baseline. Run this at the end of a slice, once
    the suite is green, so the next slice inherits a tighter ratchet. The resulting diff shows
    exactly which tests the slice enabled.

.PARAMETER AcceptRemovals
    Accept that baselined tests have gone from the run - a rename, or a deliberate deletion.
    They are still listed. Without this the ratchet is red, because a disappearing test and lost
    coverage look identical from the outside.

.EXAMPLE
    tools/check-ratchet.ps1
    tools/check-ratchet.ps1 -UpdateBaseline
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$UpdateBaseline,
    [switch]$AcceptRemovals,
    [switch]$SkipTestRun,
    # Wall-clock bound on the test run, enforced from OUTSIDE the test host. The platform's own
    # `--timeout` and the assembly [Timeout] proved useless against a CPU-bound engine loop with no
    # cancellation check: a hung host ran 44 minutes past `--timeout 20m` on 2026-09-13 before a
    # human killed it. The suite takes about 30 s.
    [int]$TimeoutSeconds = 1200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'PortTools.psm1') -Force

$trxPath = Join-Path $repoRoot 'TestResults/results.trx'
$baselinePath = Join-Path $repoRoot 'tests/parity-baseline.json'
$statusPath = Join-Path $repoRoot 'docs/STATUS.md'

$controlMarker = Join-Path $repoRoot '.scratch/control-mutation.json'
if (Test-Path -LiteralPath $controlMarker) {
    Write-Host "Ratchet: RED - tools/run-controls.py left a control mutation applied ($controlMarker)." -ForegroundColor Red
    Write-Host '  Run `python tools/run-controls.py --check` to restore the file, then re-run.' -ForegroundColor Yellow
    exit 1
}

if (-not $SkipTestRun) {
    if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath -Force }

    Write-Host "Running tests ($Configuration)..." -ForegroundColor Cyan
    # A non-zero exit here just means tests failed; the ratchet still needs the report to say
    # which ones, so the exit code is deliberately not treated as fatal.
    # The oracle test project is built too, because the ratchet only RUNS FuzzyRegex.Tests and a
    # change that breaks the oracle build (S52 sitting 3: three files flipped to CRLF, 14 IDE0055
    # errors) would otherwise land green. Build only; the oracle waves are run by tools/run-oracle.ps1.
    dotnet build (Join-Path $repoRoot 'tests/FuzzyRegex.OracleTests/FuzzyRegex.OracleTests.csproj') --configuration $Configuration --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Ratchet: RED - tests/FuzzyRegex.OracleTests does not build.' -ForegroundColor Red
        exit 1
    }

    $proc = Start-Process -FilePath 'dotnet' -PassThru -NoNewWindow -ArgumentList @(
        'test', (Join-Path $repoRoot 'tests/FuzzyRegex.Tests/FuzzyRegex.Tests.csproj'),
        '--configuration', $Configuration,
        '--', '--report-trx', '--report-trx-filename', 'results.trx')
    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        $proc.Kill($true)   # the whole tree: MSBuild nodes and the test host, not just `dotnet`
        Write-Host "Ratchet: RED - the test run did not finish within $TimeoutSeconds s and was killed." -ForegroundColor Red
        Write-Host '  A hung test is an engine loop: diff src/ against HEAD before re-running.' -ForegroundColor Yellow
        exit 1
    }
}

# Exit, do not throw. A session that commits non-compiling code produces no report at all, and
# the driver has to be able to record that as a failed slice rather than die inside this script.
try {
    $results = @(Read-TestResults -TrxPath $trxPath)
}
catch {
    Write-Host "Ratchet: RED - no test report was produced. $($_.Exception.Message)" -ForegroundColor Red
    Write-Host '  The build most likely failed. Run `dotnet build` and look at the errors.' -ForegroundColor Yellow
    exit 1
}
$upstreamCommit = (git -C (Join-Path $repoRoot 'upstream') rev-parse HEAD).Trim()

# WriteAllText, not Set-Content: Set-Content appends the platform newline, which would put a
# CRLF at the end of the file on Windows and an LF everywhere else - reintroducing exactly the
# cross-OS diff the LF join in New-StatusReport exists to avoid.
[System.IO.File]::WriteAllText(
    $statusPath,
    (New-StatusReport -Results $results -UpstreamCommit $upstreamCommit) + "`n",
    [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $statusPath" -ForegroundColor DarkGray

$verdict = Test-Ratchet -Results $results -AcceptRemovals:$AcceptRemovals `
    -BaselinePassing @(Get-BaselinePassing -BaselinePath $baselinePath)

Write-Host ''
# Distinct ids, not results: a ported test with two identical [Arguments] rows (upstream has
# duplicate rows, and the port keeps them) runs twice under one id, so the baseline - a set -
# holds fewer entries than the run has passing results. 108 such pairs on 2026-09-12. The gap
# hides nothing: Test-Ratchet reds on ANY failed result, whatever its id.
# Ordinal, for the reason Update-Baseline gives: Sort-Object -Unique folds case and
# compatibility characters and under-counts by 40 here.
$distinct = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@($results | Where-Object Outcome -eq 'Passed' | ForEach-Object Id | Where-Object { $null -ne $_ }),
    [System.StringComparer]::Ordinal).Count
Write-Host "Tests: $($results.Count)  passing: $($verdict.PassingCount) ($distinct distinct ids)  baseline: $($verdict.BaselineCount)"

if ($verdict.IsGreen) {
    Write-Host 'Ratchet: GREEN' -ForegroundColor Green

    # Listed even when accepted: a rename and a lost test look identical from here, so the
    # operator gets to see exactly what disappeared.
    foreach ($id in $verdict.Missing) {
        Write-Host "  removal accepted (baselined test no longer in the run): $id" -ForegroundColor Yellow
    }

    if ($UpdateBaseline) {
        Update-Baseline -Results $results -BaselinePath $baselinePath -UpstreamCommit $upstreamCommit
        Write-Host "Baseline updated: $distinct distinct passing test ids recorded." -ForegroundColor Green
    }

    exit 0
}

Write-Host 'Ratchet: RED' -ForegroundColor Red
foreach ($id in $verdict.Regressions) { Write-Host "  regressed (was passing, now is not): $id" -ForegroundColor Red }
foreach ($id in $verdict.Missing) {
    Write-Host "  missing (baselined test not in this run):  $id" -ForegroundColor Red
}
if ($verdict.Missing.Count -gt 0) {
    Write-Host '  If those tests were renamed or deliberately deleted, re-run with -AcceptRemovals.' -ForegroundColor Yellow
}
foreach ($id in $verdict.Failures) { Write-Host "  failed:    $id" -ForegroundColor Red }

if ($UpdateBaseline) {
    Write-Host 'Baseline NOT updated: the ratchet must be green first.' -ForegroundColor Yellow
}

exit 1
