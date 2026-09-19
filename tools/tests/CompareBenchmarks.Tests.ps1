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
            -BaselinePath $Baseline -Threshold 1.05 -NoiseFloor 1.25 -AllocationNoiseFloor 1.02 -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'allocates more'
    }

    It 'excuses an allocation ratio inside the allocation floor' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1080

        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Threshold 1.05 -NoiseFloor 1.25 -AllocationNoiseFloor 1.15 -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
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

    It 'does NOT fail a run for an allocation change between the allocation floor and -Threshold' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1200

        # This pins current behaviour rather than desired behaviour, and it is here because the
        # shipped defaults make it easy to read the opposite off S58's measurement. A floor can only
        # EXCUSE a ratio: what fails a run is -Threshold, which governs both axes, and the floor
        # forgives a ratio that is over -Threshold but inside the floor ("calls a slowdown inside
        # the floor `same`" and "excuses an allocation ratio inside the allocation floor" pin that
        # direction). A floor BELOW -Threshold therefore forgives nothing, so with the shipped
        # defaults - -Threshold 1.25, floors 1.13 and 1.0001 - a benchmark allocating 1.20x its
        # baseline is GREEN, even though the machine can resolve allocation to 2.8e-5.
        # Whether allocation deserves a tighter threshold of its own is S63's call (its scope item
        # 7), not a measurement slice's; if S63 changes it, this test is the one to invert.
        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -Job medium 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
        $report | Should -Match '1\.20x'
    }

    It 'prints the floor it is applying, so a comparison cannot silently use one' {
        New-Baseline -Path $Baseline -MedianNs 100 -Bytes 1000
        New-Report -Path $Artifacts -MedianNs 100 -MinNs 99 -Bytes 1000

        $report = & pwsh -NoProfile -File $ScriptPath -UseExisting -ArtifactsPath $Relative `
            -BaselinePath $Baseline -NoiseFloor 1.25 -AllocationNoiseFloor 1.02 -Job medium 2>&1 | Out-String
        $report | Should -Match 'Floor:\s+time 1\.25x, allocation 1\.02x'
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
