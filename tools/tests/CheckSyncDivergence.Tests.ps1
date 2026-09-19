#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

<#
    tools/check-sync-divergence.ps1 is the only thing standing between Phase 7 and a ledger that
    drifts out of date, and it runs from the ratchet where a false GREEN is invisible. Both failure
    halves are tested, because a check that only catches the missing row still lets a stale row
    survive - and a stale row is worse than none, since it sends a sync slice looking for a
    divergence that has already been re-aligned.

    Fixtures are built in a temp tree and the script is pointed at them, so the tests say nothing
    about how many divergences the real engine happens to carry today.
#>

BeforeAll {
    $script:ScriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'check-sync-divergence.ps1'

    $script:LedgerHeader = @'
# Structural divergences from upstream's shape

## How to write a row

| Column | What it must say |
|---|---|
| Where | The file, as a path under `src/FuzzyRegex`, in backticks. |

## The ledger

| Where | Upstream's shape | Ours | Measured gain | Re-aligning | Decided |
|---|---|---|---|---|---|
'@
}

Describe 'check-sync-divergence.ps1' {
    BeforeEach {
        $script:Work = Join-Path ([System.IO.Path]::GetTempPath()) "sync-probe-$([guid]::NewGuid().ToString('n'))"
        $script:Source = Join-Path $Work 'src'
        New-Item -ItemType Directory -Path (Join-Path $Source 'Engine') | Out-Null
        $script:Ledger = Join-Path $Work 'SYNC-DIVERGENCE.md'
    }

    AfterEach {
        Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'is green when every marker has a row and every row a marker' {
        Set-Content -LiteralPath (Join-Path $Source 'Engine/Matcher.cs') -Encoding utf8 -Value @'
// sync-divergence: upstream fuses these two arms / we split them / the fused form re-scans.
// Re-aligning: re-fuse if upstream changes the arm order.
int x = 1;
'@
        Set-Content -LiteralPath $Ledger -Encoding utf8 `
            -Value ($LedgerHeader + "| ``Engine/Matcher.cs:12`` | fused | split | 1.4x on dense | re-fuse | S59 |`n")

        $report = & pwsh -NoProfile -File $ScriptPath -SourceRoot $Source -LedgerPath $Ledger 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
    }

    It 'is green on a tree with no divergences at all, which is the state it ships in' {
        Set-Content -LiteralPath (Join-Path $Source 'Engine/Matcher.cs') -Value 'int x = 1;' -Encoding utf8
        Set-Content -LiteralPath $Ledger -Value $LedgerHeader -Encoding utf8

        $report = & pwsh -NoProfile -File $ScriptPath -SourceRoot $Source -LedgerPath $Ledger 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
    }

    It 'names the file when a marker has no ledger row' {
        Set-Content -LiteralPath (Join-Path $Source 'Engine/Matcher.cs') -Encoding utf8 `
            -Value "// sync-divergence: we reordered the arms.`nint x = 1;"
        Set-Content -LiteralPath $Ledger -Value $LedgerHeader -Encoding utf8

        $report = & pwsh -NoProfile -File $ScriptPath -SourceRoot $Source -LedgerPath $Ledger 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'Marker with no ledger row: Engine/Matcher\.cs'
    }

    It 'names the file when a ledger row has no marker, so a re-aligned divergence cannot linger' {
        Set-Content -LiteralPath (Join-Path $Source 'Engine/Matcher.cs') -Value 'int x = 1;' -Encoding utf8
        Set-Content -LiteralPath $Ledger -Encoding utf8 `
            -Value ($LedgerHeader + "| ``Engine/Matcher.cs`` | fused | split | 1.4x | re-fuse | S59 |`n")

        $report = & pwsh -NoProfile -File $ScriptPath -SourceRoot $Source -LedgerPath $Ledger 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'Ledger row with no marker: Engine/Matcher\.cs'
    }

    It 'reads only the ledger table, so a path quoted in the documentation above it is not a row' {
        Set-Content -LiteralPath (Join-Path $Source 'Engine/Matcher.cs') -Value 'int x = 1;' -Encoding utf8

        # `Engine/Optimiser.cs` appears in a table under a different heading. Counting it would make
        # the check red on a tree with no divergences, which is how a check gets disabled.
        $withProse = $LedgerHeader -replace '\| Where \| The file', '| `Engine/Optimiser.cs` | The file'
        Set-Content -LiteralPath $Ledger -Value $withProse -Encoding utf8

        $report = & pwsh -NoProfile -File $ScriptPath -SourceRoot $Source -LedgerPath $Ledger 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
    }

    It 'is red when the ledger is missing rather than silently passing' {
        $report = & pwsh -NoProfile -File $ScriptPath -SourceRoot $Source -LedgerPath (Join-Path $Work 'gone.md') 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'the ledger is missing'
    }

    It 'pairs by file, so a marker that moves to another line stays green' {
        Set-Content -LiteralPath (Join-Path $Source 'Engine/Matcher.cs') -Encoding utf8 `
            -Value "int a = 1;`nint b = 2;`n// sync-divergence: moved down the file.`nint c = 3;"
        Set-Content -LiteralPath $Ledger -Encoding utf8 `
            -Value ($LedgerHeader + "| ``Engine/Matcher.cs:12`` | fused | split | 1.4x | re-fuse | S59 |`n")

        $report = & pwsh -NoProfile -File $ScriptPath -SourceRoot $Source -LedgerPath $Ledger 2>&1 | Out-String
        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
    }
}
