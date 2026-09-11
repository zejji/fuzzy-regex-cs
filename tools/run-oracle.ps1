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

    'verbs' is deliberately NOT in the default list, and S29 is the slice that left it out. It finds
    four rows in 1200 at seed 20260913 - 502, 504, 519 and 863, all '(?r)', all carrying a '(*SKIP)',
    all a multi-match operation - where this port starts a later match of the same scan at a position
    upstream never tries. That shows three ways: row 502 gains a match upstream does not have, rows
    519 and 863 keep the count and move one match's span, and row 504 splits into three parts where
    upstream splits into one. They are NOT a verb defect. They are upstream's 'search_start'
    (upstream/src/_regex.c:8385), which this port does not
    implement: each 'search_start_*' scanner bounds itself with text_end/text_start where the
    'try_match_*' predicate 'basic_match' consults bounds itself with slice_end/slice_start. Nothing
    but a '(*SKIP)' moves the slice inside an attempt, so the two agree on every other pattern -
    and once one does, upstream's fast path walks past a start position its own slow path accepts,
    while this port, having only the slow path, tries it. Minimised to

        regex.finditer(r'(?r)(?:a*(*SKIP)b|[^a-f])$', '\n' + 'b', regex.M)   # upstream: one match

    and pinned in tests/FuzzyRegex.Tests/Gaps/Engine/BacktrackingVerbTests.cs. Leaving the generator
    in the default list would turn every later slice's oracle run red for this reason and hide that
    slice's own result. Run it explicitly:

        tools/run-oracle.ps1 -Generator verbs -Count 1200 -Seed 20260913

    Phase 7 owns the fix, because porting 'search_start' is the fix; put 'verbs' back in this list
    the moment it lands. See docs/plan/slices/done/S29-backtracking-verbs.md, docs/PORTMAP.md's
    prefilter row and DECISIONS 2026-09-11.

    Note that the recorder ALSO neutralises upstream's other start-position prefilter for this
    generator - 'locate_required_string', see PREFILTER_FREE_GENERATORS in tools/record-oracle.py -
    which is a different mechanism and does not fix these four rows.

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
    [string]$Generator = 'literals,literal-dot,anchors,classes,groups,quantifiers,boundaries,backrefs,case-folding,reverse,substitution,iteration,interactions,lookaround,conditionals,recursion',
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
