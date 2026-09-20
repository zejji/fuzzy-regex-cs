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

    RED on any benchmark slower than the baseline by more than -Threshold, on any benchmark
    allocating more than -Threshold times the baseline's bytes per operation, and on any baselined
    benchmark missing from the run - a disappearing benchmark and a lost measurement look identical
    from the outside, which is the rule tools/check-ratchet.ps1 already applies to tests.

    Allocation is checked as well as time because Phase 7's first optimisation lever is allocation
    elimination (the `benchmark` skill), so a change that trades bytes for nanoseconds is precisely
    what this has to be able to see.

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

.PARAMETER Job
    BenchmarkDotNet's --job. Recorded in the baseline, because two runs of different jobs are not
    comparable and a baseline that does not say which job produced it cannot be checked.

.PARAMETER BaselinePath
    Compare against this baseline file rather than the committed one for this machine. What the
    noise floor is measured with: two runs of an UNCHANGED tree, A recorded as a baseline and B
    compared against it, so the ratios are the machine's own variation and nothing else.

.PARAMETER NoiseFloor
    The time ratio below which this machine cannot distinguish a change from its own noise. A ratio
    inside the band [1/NoiseFloor, NoiseFloor] is reported `same` whatever the percentage, and is
    never a regression. Per workload, never an average - the gate is per workload by design, and a
    mean would let one badly regressed case hide behind twenty unchanged ones.

.PARAMETER AllocationNoiseFloor
    The same, for the allocated-bytes ratio. A separate number because allocation is very nearly
    deterministic where time is not, so sharing one floor would throw away most of the allocation
    signal - which is Phase 7's first optimisation lever.

.EXAMPLE
    pwsh -File tools/compare-benchmarks.ps1
    pwsh -File tools/compare-benchmarks.ps1 -UpdateBaseline
    pwsh -File tools/compare-benchmarks.ps1 -Filter '*Fuzzy*'
    pwsh -File tools/compare-benchmarks.ps1 -UseExisting -ArtifactsPath artifacts/bench/noise-B -BaselinePath artifacts/bench/noise-A.json
#>
[CmdletBinding()]
param(
    [double]$Threshold = 1.25,
    [switch]$UpdateBaseline,
    [switch]$UseExisting,
    [string]$Filter = '*',
    [string]$ArtifactsPath = '.scratch/benchmark-artifacts',
    [string]$Job = '',
    [string]$BaselinePath = '',
    # Defaults are this machine's measured floor - see bench/baselines/<machine-id>/noise-floor.md,
    # which records how they were taken and what they cover. They are NOT a guess to be tuned: a
    # floor raised to make a red run green has stopped measuring the machine and started hiding the
    # change.
    # Measured 2026-09-19 (S58) from runs A and E, an unchanged tree on a quiet machine: the time
    # ratios spanned 0.88916 to 1.0768, and 1/0.88916 = 1.1247 is the wider side. Allocation moved at
    # most 307 bytes in 11.06 MB, 2.8e-5, so its floor is four orders of magnitude tighter.
    [double]$NoiseFloor = 1.13,
    [double]$AllocationNoiseFloor = 1.0001
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchDir = Join-Path $repoRoot 'bench'
$artifacts = Join-Path $repoRoot $ArtifactsPath

if (-not $UseExisting) {
    if (Test-Path -LiteralPath $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }

    Write-Host "Running the benchmark suite (filter '$Filter'). This takes tens of minutes." -ForegroundColor Cyan
    $jobArgs = if ($Job) { @('--job', $Job) } else { @() }
    Push-Location $benchDir
    try {
        dotnet run -c Release --project FuzzyRegex.Benchmarks -- `
            --filter $Filter --exporters json --artifacts $artifacts @jobArgs
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
$unmeasured = @()
$host_ = $null
foreach ($report in $reports) {
    $json = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
    if ($null -eq $host_) { $host_ = $json.HostEnvironmentInfo }
    foreach ($benchmark in $json.Benchmarks) {
        # BenchmarkDotNet writes an entry with no Statistics at all when the benchmark did not run -
        # its generated project failed to build, or the host timed out. Reading straight through
        # that gave "The property 'Median' cannot be found on this object" and no clue what had
        # happened; found by S54's verifier when BDN's boilerplate build hit its own 120 s timeout.
        if ($null -eq $benchmark.PSObject.Properties['Statistics'] -or $null -eq $benchmark.Statistics) {
            $unmeasured += $benchmark.FullName
            continue
        }

        # $null, not 0, when the report carries no Memory node: a missing measurement and a genuine
        # zero are different facts, and conflating them is what let a lost allocation reading look
        # like a 100% improvement. `-1` records "not measured" in the baseline, which the comparison
        # below refuses to read as an improvement.
        $bytes = if ($null -ne $benchmark.PSObject.Properties['Memory'] -and $null -ne $benchmark.Memory) {
            $benchmark.Memory.BytesAllocatedPerOperation
        }
        else { -1 }

        $current[$benchmark.FullName] = [ordered]@{
            medianNs       = [math]::Round($benchmark.Statistics.Median, 2)
            minNs          = [math]::Round($benchmark.Statistics.Min, 2)
            allocatedBytes = $bytes
        }
    }
}

# BenchmarkDotNet's own multimodality warning, read out of its run log. It is the honest signal for
# "something else was running": a bimodal distribution means some iterations were descheduled, and
# the median it reports is then worth rather less than its decimal places suggest.
# Every .log, not 'BenchmarkRun-*.log': BenchmarkDotNet names the log after the run, and a run
# covering one benchmark class is named after that class instead. A narrower glob silently matched
# nothing on every filtered run.
$multimodal = @(
    Get-ChildItem -Path $artifacts -Filter '*.log' -ErrorAction SilentlyContinue |
        ForEach-Object { Get-Content -LiteralPath $_.FullName } |
        Select-String -Pattern '^\s*(\S+):\s+\S+\s+->\s+It seems that the distribution' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Sort-Object -Unique
)

# A benchmark whose fastest iteration is this far below its median was being descheduled for most
# of the run. 0.85 is a judgement, not a measurement: on an idle machine these sit at 0.95-1.00, and
# the contended run S54 first recorded had thirteen rows between 0.53 and 0.76.
$_contendedFloor = 0.85
$contended = @(
    $current.Keys | Where-Object {
        $current[$_].medianNs -gt 0 -and ($current[$_].minNs / $current[$_].medianNs) -lt $_contendedFloor
    }
)

# Derived from what BenchmarkDotNet reports, so the folder name and the numbers inside it cannot
# describe different machines. Non-alphanumerics collapse to a single hyphen.
$machineId = ("$($host_.OsVersion.Split(' ')[0])-$($host_.Architecture)-$($host_.ProcessorName)" `
        -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLowerInvariant()
$baselineDir = Join-Path $repoRoot "bench/baselines/$machineId"
# -BaselinePath overrides where the comparison reads from AND where -UpdateBaseline writes to, so
# that the A-against-B noise-floor measurement uses this same code path rather than a second one
# written for the occasion. A floor measured by a different comparison than the gate applies is a
# floor for a comparison nobody runs.
$baselinePath = if ($BaselinePath) {
    if ([System.IO.Path]::IsPathRooted($BaselinePath)) { $BaselinePath } else { Join-Path $repoRoot $BaselinePath }
}
else { Join-Path $baselineDir 'net10.0.json' }
$baselineDir = Split-Path -Parent $baselinePath

if ($unmeasured.Count -gt 0) {
    Write-Host "Benchmarks: RED - $($unmeasured.Count) benchmark(s) produced no measurement at all:" -ForegroundColor Red
    foreach ($name in $unmeasured) { Write-Host "  $name" -ForegroundColor Red }
    Write-Host '  The run did not complete. Look in the artifacts log before reading anything below.' -ForegroundColor Yellow
    exit 1
}

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
        # Which BenchmarkDotNet job produced these numbers. Two runs of different jobs are not
        # comparable - a Short job's three iterations and a Medium job's fifteen have different
        # spreads before any code changes - so a baseline that does not say which job it is cannot
        # be checked against a later run, only trusted.
        job        = if ($Job) { $Job } else { 'default' }
        # Recorded rather than assumed: BenchmarkDotNet flags a multimodal distribution when the
        # machine was not quiet, and a median carrying that flag should not be read to two decimal
        # places. Listing them here means the next reader sees which rows to distrust without
        # having to still have the run log.
        multimodal = @($multimodal)
        # The blunter contention signal, and the one that matters to the gate: a benchmark whose
        # fastest iteration is far below its median spent most of the run being descheduled, so the
        # median records the contention and not the code. A baseline like that is PESSIMISTIC, which
        # means the -Threshold gate has less headroom than it looks like it has - a row recorded at
        # 0.55 min/median can get nearly twice as slow and still compare GREEN. Recorded per row so
        # a later reader can see which numbers to distrust without still having the run log.
        contended  = @($contended)
        benchmarks = $current
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $baselinePath -Encoding utf8NoBOM
    Write-Host "Baseline written: $baselinePath ($($current.Count) benchmarks)." -ForegroundColor Green
    if ($multimodal.Count -gt 0) {
        Write-Host "  $($multimodal.Count) benchmark(s) had a multimodal distribution:" -ForegroundColor Yellow
        foreach ($name in $multimodal) { Write-Host "    $name" -ForegroundColor Yellow }
    }
    if ($contended.Count -gt 0) {
        Write-Host "  $($contended.Count) of $($current.Count) benchmark(s) have min/median below $_contendedFloor - THE MACHINE WAS NOT QUIET." -ForegroundColor Yellow
        Write-Host '  Those medians are pessimistic, so the threshold has less headroom than it appears.' -ForegroundColor Yellow
        Write-Host '  Re-take this baseline on an idle machine before trusting it as a gate.' -ForegroundColor Yellow
    }
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
$baselineJob = if ($baseline.PSObject.Properties['job']) { $baseline.job } else { 'unrecorded' }
$thisJob = if ($Job) { $Job } else { 'default' }
Write-Host "Job:      baseline $baselineJob, this run $thisJob"
if ($baselineJob -ne 'unrecorded' -and $baselineJob -ne $thisJob) {
    Write-Host "CAUTION: the baseline was taken with --job $baselineJob and this run used $thisJob." -ForegroundColor Yellow
    Write-Host '  Different jobs have different spreads before any code changes, so these ratios mix' -ForegroundColor Yellow
    Write-Host '  a real difference with a measurement-method one. Re-run with the baseline"s job.' -ForegroundColor Yellow
}
if ($NoiseFloor -gt 1 -or $AllocationNoiseFloor -gt 1) {
    Write-Host ("Floor:    time {0:N2}x, allocation {1:N2}x - inside these a row reads 'same'." -f $NoiseFloor, $AllocationNoiseFloor)
}
$baselineContended = @(if ($baseline.PSObject.Properties['contended']) { $baseline.contended })
if ($baselineContended.Count -gt 0) {
    Write-Host ''
    Write-Host "CAUTION: $($baselineContended.Count) of this baseline's rows were measured on a machine that was not" -ForegroundColor Yellow
    Write-Host '  quiet, so their medians are pessimistic and this comparison has less headroom than the' -ForegroundColor Yellow
    Write-Host '  threshold suggests. Re-take the baseline on an idle machine before relying on it.' -ForegroundColor Yellow
}
Write-Host ''
if ($Filter -ne '*') {
    Write-Host "PARTIAL RUN (-Filter '$Filter'): only what it measured is compared, and the" -ForegroundColor Yellow
    Write-Host '  missing-benchmark check is SKIPPED. A green partial run is not a green suite.' -ForegroundColor Yellow
    Write-Host ''
}

Write-Host ('{0,-58} {1,12} {2,12} {3,8} {4,8}' -f 'Benchmark', 'baseline ns', 'now ns', 'time', 'alloc')

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

    # Inside the noise floor this machine cannot tell a change from its own variation, so the ratio
    # is reported `same` and is never a regression however large the percentage looks. The band is
    # two-sided on purpose: an unexplained IMPROVEMENT inside the floor is the same measurement
    # artefact as a regression inside it, and reporting one but not the other is how a run that
    # measured nothing comes to look like a win.
    $withinFloor = $NoiseFloor -gt 1 -and $ratio -le $NoiseFloor -and $ratio -ge (1 / $NoiseFloor)

    # Allocation is compared as well as time, and to the same threshold. Phase 7's first lever is
    # allocation elimination (`benchmark` skill), so a change that trades bytes for nanoseconds is
    # exactly what this has to be able to see; a baseline that recorded allocations and never read
    # them back would have let any allocation regression through.
    $wasBytes = $baseline.benchmarks.$name.allocatedBytes
    $nowBytes = $current[$name].allocatedBytes

    # -1 on either side means that side was never measured: not an improvement and not a
    # regression, so it is reported as such rather than folded into a ratio.
    $allocUnmeasured = ($wasBytes -lt 0 -or $nowBytes -lt 0)
    # A baselined allocation that has become zero is far more likely to be a diagnoser that did not
    # attach than an engine that stopped allocating altogether, and reading it as a 100%
    # improvement is how a whole run of lost readings passes GREEN. Flagged so somebody looks.
    $allocLost = (-not $allocUnmeasured) -and $wasBytes -gt 0 -and $nowBytes -eq 0
    $allocAppeared = (-not $allocUnmeasured) -and $wasBytes -eq 0 -and $nowBytes -gt 0
    $allocRatio = if (-not $allocUnmeasured -and $wasBytes -gt 0) { $nowBytes / $wasBytes } else { 1 }
    # A separate floor from the time one: allocation is very nearly deterministic, so sharing a
    # floor sized for timing jitter would discard most of the allocation signal.
    $allocWithinFloor = $AllocationNoiseFloor -gt 1 -and $allocRatio -le $AllocationNoiseFloor `
        -and $allocRatio -ge (1 / $AllocationNoiseFloor)
    # `lost` and `appeared` are NOT excused by the floor. Both mean the measurement changed kind
    # rather than degree, and a floor is a statement about degree.
    $allocRegressed = $allocLost -or $allocAppeared -or ($allocRatio -gt $Threshold -and -not $allocWithinFloor)

    $allocCell =
    if ($allocUnmeasured) { '    n/a' }
    elseif ($allocLost) { '   lost' }
    elseif ($allocAppeared) { '    new' }
    elseif ($allocWithinFloor) { '   same' }
    else { '{0,6:N2}x' -f $allocRatio }

    $timeCell = if ($withinFloor) { '  same' } else { '{0,5:N2}x' -f $ratio }

    $short = $name -replace '^Fuzzy\.Text\.RegularExpressions\.Benchmarks\.', ''
    $regressed = $ratio -gt $Threshold -and -not $withinFloor
    $colour =
    if ($regressed -or $allocRegressed) { 'Red' }
    elseif ($ratio -lt 0.9 -and -not $withinFloor) { 'Green' }
    else { 'Gray' }
    Write-Host ('{0,-58} {1,12:N1} {2,12:N1} {3,6} {4,7}' -f $short, $was, $now, $timeCell, $allocCell) -ForegroundColor $colour

    if ($regressed) { $regressions += "$short slower ($([math]::Round($ratio, 2))x)" }
    if ($allocRegressed) {
        $why =
        if ($allocLost) { 'allocation no longer measured (diagnoser lost?)' }
        elseif ($allocAppeared) { 'now allocates where the baseline did not' }
        else { "allocates more ($([math]::Round($allocRatio, 2))x)" }
        $regressions += "$short $why - $wasBytes -> $nowBytes bytes"
    }
}

$new = @($current.Keys | Where-Object { $_ -notin $baseline.benchmarks.PSObject.Properties.Name })
foreach ($name in $new) {
    Write-Host ("  not in the baseline (new benchmark): $name") -ForegroundColor Yellow
}

Write-Host ''
if ($regressions.Count -eq 0 -and $missing.Count -eq 0) {
    $scope = if ($Filter -eq '*') { 'the suite' } else { "the '$Filter' subset" }
    Write-Host "Benchmarks: GREEN - nothing in $scope more than $($Threshold)x slower, or allocating $($Threshold)x more, than the baseline." -ForegroundColor Green
    exit 0
}

Write-Host 'Benchmarks: RED' -ForegroundColor Red
foreach ($name in $regressions) { Write-Host "  regressed: $name" -ForegroundColor Red }
foreach ($name in $missing) { Write-Host "  missing (baselined benchmark not in this run): $name" -ForegroundColor Red }
exit 1
