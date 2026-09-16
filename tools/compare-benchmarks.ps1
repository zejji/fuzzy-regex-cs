<#
.SYNOPSIS
    Runs the benchmark suite and compares it against the committed baseline for this machine.

.DESCRIPTION
    Phase 7's regression gate. Optimisation is where silent behaviour change is likeliest and where
    a "faster" change can quietly be slower somewhere else, so every optimisation slice runs this
    and reads the per-benchmark ratio rather than an average.

    Baselines live in bench/baselines/<machine-id>/net10.0.json, one file per machine, because a
    baseline carried over from different hardware means nothing (the `benchmark` skill). The
    machine id is derived from the processor name BenchmarkDotNet itself reports, so it cannot
    drift away from the numbers it labels.

    RED on any benchmark slower than the baseline by more than -Threshold, and on any baselined
    benchmark missing from the run - a disappearing benchmark and a lost measurement look identical
    from the outside, which is the rule tools/check-ratchet.ps1 already applies to tests.

    THE WORKING DIRECTORY MATTERS and this script sets it. BenchmarkDotNet locates the benchmark
    project by walking up from the working directory to the nearest solution file and then
    searching every subdirectory below it; from the repository root that search also finds the copy
    of the project inside each git worktree under .claude/worktrees/ and the run dies having
    executed nothing. bench/FuzzyRegex.Benchmarks.slnx exists to stop that walk at bench/.

.PARAMETER Threshold
    How much slower than the baseline a benchmark may be before the run is RED. Defaults to 1.25,
    which is the v1.0 gate's own per-workload tolerance (the `benchmark` skill); reusing that
    number rather than inventing a second one.

.PARAMETER UpdateBaseline
    Record this run as the new baseline for this machine instead of comparing against it. Used once
    by S54 to create them, and afterwards only when the owner accepts a measured change.

.PARAMETER UseExisting
    Compare the artifacts already in -ArtifactsPath rather than measuring again. For iterating on
    this script without paying for a run.

.PARAMETER Filter
    BenchmarkDotNet's --filter. A partial run compares only what it measured; the missing-benchmark
    check is skipped, and the output says so.

.EXAMPLE
    pwsh -File tools/compare-benchmarks.ps1
    pwsh -File tools/compare-benchmarks.ps1 -UpdateBaseline
    pwsh -File tools/compare-benchmarks.ps1 -Filter '*Fuzzy*'
#>
[CmdletBinding()]
param(
    [double]$Threshold = 1.25,
    [switch]$UpdateBaseline,
    [switch]$UseExisting,
    [string]$Filter = '*',
    [string]$ArtifactsPath = '.scratch/benchmark-artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchDir = Join-Path $repoRoot 'bench'
$artifacts = Join-Path $repoRoot $ArtifactsPath

if (-not $UseExisting) {
    if (Test-Path -LiteralPath $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }

    Write-Host "Running the benchmark suite (filter '$Filter'). This takes tens of minutes." -ForegroundColor Cyan
    Push-Location $benchDir
    try {
        dotnet run -c Release --project FuzzyRegex.Benchmarks -- `
            --filter $Filter --exporters json --artifacts $artifacts
    }
    finally {
        Pop-Location
    }
}

$reports = @(Get-ChildItem -Path (Join-Path $artifacts 'results') -Filter '*-report-full-compressed.json' -ErrorAction SilentlyContinue)
if ($reports.Count -eq 0) {
    Write-Host "Benchmarks: RED - no JSON report under $artifacts/results." -ForegroundColor Red
    Write-Host '  If the run printed "Found more than one matching project file", see bench/FuzzyRegex.Benchmarks.slnx.' -ForegroundColor Yellow
    exit 1
}

$current = [ordered]@{}
$host_ = $null
foreach ($report in $reports) {
    $json = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
    if ($null -eq $host_) { $host_ = $json.HostEnvironmentInfo }
    foreach ($benchmark in $json.Benchmarks) {
        $current[$benchmark.FullName] = [ordered]@{
            medianNs       = [math]::Round($benchmark.Statistics.Median, 2)
            allocatedBytes = $benchmark.Memory.BytesAllocatedPerOperation
        }
    }
}

# Derived from what BenchmarkDotNet reports, so the folder name and the numbers inside it cannot
# describe different machines. Non-alphanumerics collapse to a single hyphen.
$machineId = ("$($host_.OsVersion.Split(' ')[0])-$($host_.Architecture)-$($host_.ProcessorName)" `
        -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLowerInvariant()
$baselineDir = Join-Path $repoRoot "bench/baselines/$machineId"
$baselinePath = Join-Path $baselineDir 'net10.0.json'

if ($UpdateBaseline) {
    if ($Filter -ne '*') {
        Write-Host "Baseline NOT updated: -Filter '$Filter' would record a partial suite." -ForegroundColor Red
        exit 1
    }

    New-Item -ItemType Directory -Force -Path $baselineDir | Out-Null
    [ordered]@{
        machine    = [ordered]@{
            id             = $machineId
            processor      = $host_.ProcessorName
            physicalCores  = $host_.PhysicalCoreCount
            logicalCores   = $host_.LogicalCoreCount
            os             = $host_.OsVersion
            architecture   = $host_.Architecture
            runtime        = $host_.RuntimeVersion
            sdk            = $host_.DotNetCliVersion
            benchmarkDotNet = $host_.BenchmarkDotNetVersion
        }
        takenUtc   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        benchmarks = $current
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $baselinePath -Encoding utf8NoBOM
    Write-Host "Baseline written: $baselinePath ($($current.Count) benchmarks)." -ForegroundColor Green
    exit 0
}

if (-not (Test-Path -LiteralPath $baselinePath)) {
    Write-Host "Benchmarks: RED - no baseline for this machine at $baselinePath." -ForegroundColor Red
    Write-Host '  Take one with -UpdateBaseline, on a quiet machine, and commit it.' -ForegroundColor Yellow
    exit 1
}

$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
Write-Host ''
Write-Host "Baseline: $baselinePath (taken $($baseline.takenUtc))"
Write-Host "Machine:  $($baseline.machine.processor), $($baseline.machine.os)"
Write-Host ''
Write-Host ('{0,-58} {1,>12} {2,>12} {3,>8}' -f 'Benchmark', 'baseline ns', 'now ns', 'ratio')

$regressions = @()
$missing = @()
foreach ($name in $baseline.benchmarks.PSObject.Properties.Name) {
    $was = $baseline.benchmarks.$name.medianNs
    if (-not $current.Contains($name)) {
        if ($Filter -eq '*') { $missing += $name }
        continue
    }

    $now = $current[$name].medianNs
    $ratio = if ($was -gt 0) { $now / $was } else { 1 }
    $short = $name -replace '^Fuzzy\.Text\.RegularExpressions\.Benchmarks\.', ''
    $colour = if ($ratio -gt $Threshold) { 'Red' } elseif ($ratio -lt 0.9) { 'Green' } else { 'Gray' }
    Write-Host ('{0,-58} {1,12:N1} {2,12:N1} {3,8:N2}x' -f $short, $was, $now, $ratio) -ForegroundColor $colour
    if ($ratio -gt $Threshold) { $regressions += "$short ($([math]::Round($ratio, 2))x)" }
}

$new = @($current.Keys | Where-Object { $_ -notin $baseline.benchmarks.PSObject.Properties.Name })
foreach ($name in $new) {
    Write-Host ("  not in the baseline (new benchmark): $name") -ForegroundColor Yellow
}

Write-Host ''
if ($regressions.Count -eq 0 -and $missing.Count -eq 0) {
    Write-Host "Benchmarks: GREEN - nothing more than $($Threshold)x slower than the baseline." -ForegroundColor Green
    exit 0
}

Write-Host 'Benchmarks: RED' -ForegroundColor Red
foreach ($name in $regressions) { Write-Host "  regressed: $name" -ForegroundColor Red }
foreach ($name in $missing) { Write-Host "  missing (baselined benchmark not in this run): $name" -ForegroundColor Red }
exit 1
