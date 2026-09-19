<#
.SYNOPSIS
    Prints, for two committed benchmark baselines, each benchmark's own contention signal in each
    run and the ratio between them.

.DESCRIPTION
    `tools/compare-benchmarks.ps1` answers "is this a regression". This answers the question behind
    a noise floor: "did these two runs see the same machine". It reports min/median WITHIN each run
    - BenchmarkDotNet's own contention signal, where well under 1 means that benchmark's iterations
    disagreed with each other - beside the median ratio between the runs, in run order.

    S58 used it to show that a discarded run's damage was a contiguous episode in the middle of the
    run rather than a uniformly slower machine. It reads only committed baseline JSON, so it stays
    reproducible after the artifacts are gone.

.PARAMETER BaselineA
    The earlier run's baseline JSON.

.PARAMETER BaselineB
    The later run's baseline JSON. Ratios are B over A.

.EXAMPLE
    pwsh -File tools/probes/compare-two-baselines.ps1 `
      -BaselineA bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-A.json `
      -BaselineB bench/baselines/windows-x64-13th-gen-intel-core-i7-13850hx/2026-09-19-S58-noise-B-discarded.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $BaselineA,
    [Parameter(Mandatory)] [string] $BaselineB
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$a = (Get-Content -Path $BaselineA -Raw | ConvertFrom-Json).benchmarks
$b = (Get-Content -Path $BaselineB -Raw | ConvertFrom-Json).benchmarks

$rows = foreach ($property in $b.PSObject.Properties) {
    $inA = $a.PSObject.Properties[$property.Name]
    if (-not $inA) { continue }

    $later = $property.Value
    $earlier = $inA.Value
    [pscustomobject]@{
        Benchmark = $property.Name -replace '^Fuzzy\.Text\.RegularExpressions\.Benchmarks\.', ''
        A_minmed  = [math]::Round($earlier.minNs / $earlier.medianNs, 2)
        B_minmed  = [math]::Round($later.minNs / $later.medianNs, 2)
        Ratio     = [math]::Round($later.medianNs / $earlier.medianNs, 2)
        # Unrounded, so the count below agrees with the run's own report rather than with a
        # 0.849 that displays as 0.85.
        RawMinMed = $later.minNs / $later.medianNs
    }
}

$rows | Select-Object Benchmark, A_minmed, B_minmed, Ratio | Format-Table -AutoSize | Out-String -Width 160

$contended = @($rows | Where-Object { $_.RawMinMed -lt 0.85 })
$widest = ($rows | Sort-Object Ratio -Descending | Select-Object -First 1)
$narrowest = ($rows | Sort-Object Ratio | Select-Object -First 1)
'Rows: {0}. Below 0.85 in the later run: {1}. Widest ratio: {2} ({3}). Narrowest: {4} ({5}).' -f
    @($rows).Count, $contended.Count, $widest.Ratio, $widest.Benchmark, $narrowest.Ratio, $narrowest.Benchmark
