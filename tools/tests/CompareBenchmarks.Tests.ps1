#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

<#
    The noise floor S58 measured is only worth having if the gate applies it the way the measurement
    assumed: per workload, two-sided, and never excusing a change of KIND. These tests pin that,
    because every one of those is a way for a floor to turn a red run green by accident -
    "a compare script that averages across workloads or ignores its own floor" is on this slice's
    own review hunt list.

    -UseExisting makes it testable without a benchmark run: the fixtures below are a
    BenchmarkDotNet report and a baseline with the fields the script actually reads, so a run takes
    a second rather than half an hour.
#>

BeforeAll {
    $script:ToolsRoot = Split-Path -Parent $PSScriptRoot
    $script:RepoRoot = Split-Path -Parent $script:ToolsRoot
    $script:ScriptPath = Join-Path $script:ToolsRoot 'compare-benchmarks.ps1'
    $script:Name = 'Fuzzy.Text.RegularExpressions.Benchmarks.WorkloadBenchmarks.Literal'

    # A BenchmarkDotNet full-compressed report carrying one benchmark. Only the fields the script
    # reads are here; the real file has some hundreds more.
    function New-Report {
        param([string]$Path, [double]$MedianNs, [double]$MinNs, [long]$Bytes)

        New-Item -ItemType Directory -Force -Path (Join-Path $Path 'results') | Out-Null
        $report = [ordered]@{
            HostEnvironmentInfo = [ordered]@{
                OsVersion              = 'Windows 11 (10.0.22631.0)'
                Architecture           = 'X64'
                ProcessorName          = 'Test CPU'
                PhysicalCoreCount      = 8
                LogicalCoreCount       = 16
                RuntimeVersion         = '.NET 10.0.0'
                DotNetCliVersion       = '10.0.100'
                BenchmarkDotNetVersion = '0.15.8'
            }
            Benchmarks          = @(
                [ordered]@{
                    FullName   = $script:Name
                    Statistics = [ordered]@{ Median = $MedianNs; Min = $MinNs }
                    Memory     = [ordered]@{ BytesAllocatedPerOperation = $Bytes }
                }
            )
        }
        $report | ConvertTo-Json -Depth 8 |
            Set-Content -LiteralPath (Join-Path $Path 'results/Probe-report-full-compressed.json') -Encoding utf8
    }

    function New-Baseline {
        param([string]$Path, [double]$MedianNs, [long]$Bytes, [string]$Job = 'medium')

        [ordered]@{
            machine    = [ordered]@{ id = 'test'; processor = 'Test CPU'; os = 'Windows 11' }
            takenUtc   = '2026-09-19T00:00:00Z'
            job        = $Job
            multimodal = @()
            contended  = @()
            benchmarks = [ordered]@{
                $script:Name = [ordered]@{ medianNs = $MedianNs; minNs = $MedianNs; allocatedBytes = $Bytes }
            }
        } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding utf8
    }
}

Describe 'compare-benchmarks.ps1 noise floor' {
    BeforeEach {
        # Under .scratch and relative: the script joins -ArtifactsPath onto the repository root, and
        # .scratch is gitignored, so a fixture a failing test leaves behind cannot dirty the tree.
        $script:Relative = ".scratch/compare-probe-$([guid]::NewGuid().ToString('n'))"
        $script:Artifacts = Join-Path $RepoRoot $Relative
        New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
        $script:Baseline = Join-Path $Artifacts 'baseline.json'
    }

    AfterEach {
        Remove-Item -LiteralPath $Artifacts -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'calls a slowdown inside the floor `same`, and does not call it a regression' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 118 -MinNs 117 -Bytes 1000

        # 1.18x: over the threshold, inside the floor. Without the floor this run is RED.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Threshold 1.05 -NoiseFloor 1.25 -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
        $report | Should -Match 'same'
    }

    It 'still calls a slowdown outside the floor a regression' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 140 -MinNs 139 -Bytes 1000

        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Threshold 1.25 -NoiseFloor 1.10 -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'regressed:.*slower'
    }

    It 'is two-sided: an improvement inside the floor is `same` and not a win' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 88 -MinNs 87 -Bytes 1000

        # An unexplained 12% improvement inside a 25% floor is the same measurement artefact as a
        # 12% regression. Reporting one but not the other is how a run that measured nothing gets
        # written up as a win.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Threshold 1.25 -NoiseFloor 1.25 -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'same'
        $report | Should -Not -Match '0\.88x'
    }

    It 'floors allocation on its own number, not the timing one' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1080

        # The timing floor is wide and the allocation floor is narrow, which is the whole point of
        # two parameters: allocation is very nearly deterministic, so 8% of it is a real change.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Threshold 1.05 -NoiseFloor 1.25 -AllocationNoiseFloor 1.02 `
            -AllocationSlackBytes 0 -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'allocates more'
    }

    It 'excuses an allocation ratio inside the allocation floor' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1080

        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Threshold 1.05 -NoiseFloor 1.25 -AllocationNoiseFloor 1.15 `
            -AllocationSlackBytes 0 -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
    }

    It 'excuses an allocation that moved by no more than the slack, whatever the ratio' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 517
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 582

        # S61's two runs of an unchanged tree (2026-09-23): ValidateEmails read 517 B and then 582 B
        # per operation, 1.13x, and FuzzyPhraseThreeAlternation 4,656 B and then 3,984 B. A ratio
        # floor alone would call the first a regression; a few hundred bytes across 100,000 calls
        # is the harness, not the engine.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
        $report | Should -Match 'same'
    }

    It 'never lets the floor excuse a lost allocation reading, however wide the floor' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 0

        # A baselined allocation that has become zero is a diagnoser that did not attach far more
        # often than an engine that stopped allocating. That is a change of kind, and a floor is a
        # statement about degree.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Threshold 1.25 -NoiseFloor 5 -AllocationNoiseFloor 5 -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'lost'
    }

    It 'fails a run for any allocation rise beyond the floor and the slack, not only one past -Threshold' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 100000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 120000

        # S61 scope item 5: allocation is gated on its own floor, not on -Threshold. Until S61 this
        # test pinned the opposite - with -Threshold 1.25 governing both axes, a benchmark
        # allocating 1.20x its baseline was GREEN although S58 measured the machine's allocation
        # noise at 2.8e-5. Allocation has no time-style jitter to forgive, so any rise the floor
        # and the slack do not explain is a change somebody made.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'allocates more \(1\.2x\)'
    }

    It 'shows a failing allocation rise that is too small for two decimal places' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 5000000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 5002000

        # S61 blind review: 2,000 B on 5 MB is past the default 1.0001x floor and the 1,024 B slack,
        # so the run is RED, but at two places the row, the reason and the floor all read 1.00x.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match ' 1\.0004x'
        $report | Should -Match 'allocates more \(1\.0004x\)'
        $report | Should -Match 'allocation 1\.0001x'
    }

    It 'shows a failing allocation rise however tight the floor is set' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 100000000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 100002000

        # S61 second review pass: four fixed places still printed 1.00002x as 1.0000x and "1x".
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Job medium -AllocationNoiseFloor 1.00001 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match ' 1\.00002x'
        $report | Should -Match 'allocates more \(1\.00002x\)'
        $report | Should -Match 'allocation 1\.00001x'
    }

    It 'shows a one-byte rise on a very large baseline' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 30000000000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 30000000001

        # S61 third review pass: a cap of ten places printed this 1 + 3.3e-11 rise as 1.0000000000x.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Job medium -AllocationNoiseFloor 1 -AllocationSlackBytes 0 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'allocates more \(1\.00000000003x\)'
    }

    It 'does not call an allocation drop a regression' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 100000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 20000

        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match '0\.20x'
    }

    It 'prints the floor it is applying, so a comparison cannot silently use one' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1000

        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -NoiseFloor 1.25 -AllocationNoiseFloor 1.02 -AllocationSlackBytes 512 `
            -Job medium 2>&1 | Out-String
        $report | Should -Match 'Floor:\s+time 1\.25x, allocation 1\.02x or 512 B'
    }
}

Describe 'compare-benchmarks.ps1 job recording' {
    BeforeEach {
        $script:Relative = ".scratch/compare-probe-$([guid]::NewGuid().ToString('n'))"
        $script:Artifacts = Join-Path $RepoRoot $Relative
        New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
        $script:Baseline = Join-Path $Artifacts 'baseline.json'
    }

    AfterEach {
        Remove-Item -LiteralPath $Artifacts -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'warns when the run job differs from the job the baseline was taken with' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000 -Job medium
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1000

        # A Short job's three iterations and a Medium job's fifteen have different spreads before
        # any code changes, so the ratio would mix a real difference with a method one.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline 2>&1 | Out-String
        $report | Should -Match 'CAUTION: the baseline was taken with --job medium and this run used default'
    }

    It 'is quiet about the job when the two match' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000 -Job medium
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1000

        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Job medium 2>&1 | Out-String
        $report | Should -Match 'Job:\s+baseline medium, this run medium'
        $report | Should -Not -Match 'CAUTION: the baseline was taken'
    }

    It 'records the job in a baseline it writes, so a later run can check it' {
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1000
        $written = Join-Path $Artifacts 'written.json'

        & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $written -UpdateBaseline -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        (Get-Content -LiteralPath $written -Raw | ConvertFrom-Json).job | Should -Be 'medium'
    }
}
