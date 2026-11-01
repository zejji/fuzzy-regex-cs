<#
    The scheduled oracle reports fresh seeds without failing the run (oracle.yml), so on CI the
    job summary is where a red fresh seed is seen at all. These drive sweep-seeds.ps1 with a log
    that already holds every planned seed, so it goes straight to its summary: nothing is recorded
    and run-oracle.ps1 is never called.
#>

BeforeAll {
    $script:ScriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'sweep-seeds.ps1'

    # One log line as sweep-seeds.ps1 writes it, for the question that -Count 5 asks.
    function script:LogLine([int]$seed, [bool]$green, [int]$diverged, [hashtable]$divergeBy, [bool]$harnessFailed = $false) {
        [ordered]@{
            seed          = $seed
            question      = '5||Release'
            masterSeed    = -1
            count         = 5
            configuration = 'Release'
            green         = $green
            harnessFailed = $harnessFailed
            diverged      = $diverged
            seconds       = 1
            tally         = "agree 10  fault 0  diverge $diverged  of 10 rows"
            divergeBy     = $divergeBy
            expectedBy    = @{}
            recordedAt    = '2026-10-30T00:00:00Z'
        } | ConvertTo-Json -Compress -Depth 4
    }

    function script:Sweep([string[]]$lines, [string]$seeds) {
        $dir = Join-Path ([System.IO.Path]::GetTempPath()) "sweep-summary-$([guid]::NewGuid().ToString('n'))"
        New-Item -ItemType Directory -Path $dir | Out-Null
        $log = Join-Path $dir 'sweep.jsonl'
        $summary = Join-Path $dir 'summary.md'
        Set-Content -LiteralPath $log -Value $lines
        $saved = $env:GITHUB_STEP_SUMMARY
        try {
            $env:GITHUB_STEP_SUMMARY = $summary
            & pwsh -NoProfile -File $ScriptPath -Seeds $seeds -Count 5 -Log $log *> $null
            $exit = $LASTEXITCODE
        }
        finally {
            $env:GITHUB_STEP_SUMMARY = $saved
        }
        $text = if (Test-Path -LiteralPath $summary) { Get-Content -LiteralPath $summary -Raw } else { '' }
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
        [pscustomobject]@{ Exit = $exit; Summary = $text }
    }
}

Describe 'sweep-seeds.ps1 job summary' {
    It 'lists every seed with its verdict and tally, and still exits non-zero when a seed is red' {
        $result = Sweep @(
            (LogLine 11 $true 0 @{}),
            (LogLine 22 $false 3 @{ verbs = 2; fuzzy = 1 })
        ) '11,22'

        $result.Exit | Should -Be 1
        $result.Summary | Should -Match 'RED at 1 of 2 seeds'
        $result.Summary | Should -Match '\| 11 \| green \| agree 10  fault 0  diverge 0  of 10 rows \|  \|'
        $result.Summary | Should -Match '\| 22 \| RED \| agree 10  fault 0  diverge 3  of 10 rows \| fuzzy 1, verbs 2 \|'
    }

    It 'names a seed whose harness failed apart from a divergence' {
        $result = Sweep @((LogLine 33 $false 0 @{} $true)) '33'

        $result.Summary | Should -Match '\| 33 \| HARNESS FAILED \|'
    }

    It 'says green when every seed is green' {
        $result = Sweep @((LogLine 11 $true 0 @{})) '11'

        $result.Exit | Should -Be 0
        $result.Summary | Should -Match 'GREEN at all 1 seeds'
    }
}
