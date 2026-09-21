<#
    tools/status-stamp.ps1 answers one question: "were these the test sources docs/STATUS.md was
    generated from?" The pre-commit hook asks it, and refuses a commit whose staged tests no longer
    match the stamp the ratchet wrote - which is the failure CI run 35569653546 reported, a sitting
    having regenerated the page and then added seven more gap tests before it committed.

    The tests below run against a throwaway git repository, never this one, because the stamp of
    this repository changes whenever anybody edits a test.
#>

BeforeAll {
    $script:ScriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'status-stamp.ps1'

    function New-Fixture {
        $work = Join-Path ([System.IO.Path]::GetTempPath()) "stamp-probe-$([guid]::NewGuid().ToString('n'))"
        New-Item -ItemType Directory -Path (Join-Path $work 'tests/Gaps') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $work 'tests/Gaps/OneTests.cs') -Value 'class One { }' -NoNewline
        Set-Content -LiteralPath (Join-Path $work 'tests/Gaps/TwoTests.cs') -Value 'class Two { }' -NoNewline
        Set-Content -LiteralPath (Join-Path $work 'README.md') -Value 'not a test' -NoNewline
        git -C $work init --quiet | Out-Null
        git -C $work add -A | Out-Null
        return $work
    }
}

Describe 'status-stamp.ps1' {
    BeforeEach {
        $script:Work = New-Fixture
    }

    AfterEach {
        Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'prints one SHA-256 over the tracked test sources' {
        $stamp = & $ScriptPath -Root $Work

        $stamp | Should -Match '^[0-9a-f]{64}$'
    }

    It 'gives the same answer twice for an unchanged tree' {
        (& $ScriptPath -Root $Work) | Should -Be (& $ScriptPath -Root $Work)
    }

    It 'changes when a test source changes, which is the whole point' {
        $before = & $ScriptPath -Root $Work

        Add-Content -LiteralPath (Join-Path $Work 'tests/Gaps/OneTests.cs') -Value ' // and another test'

        (& $ScriptPath -Root $Work) | Should -Not -Be $before
    }

    It 'changes when a test source is added and staged' {
        $before = & $ScriptPath -Root $Work

        Set-Content -LiteralPath (Join-Path $Work 'tests/Gaps/ThreeTests.cs') -Value 'class Three { }' -NoNewline
        git -C $Work add -A | Out-Null

        (& $ScriptPath -Root $Work) | Should -Not -Be $before
    }

    It 'ignores a file that is not a tracked test source' {
        # An untracked file is deliberately outside the stamp: CI regenerates the page from the
        # committed tree, so a file nobody committed is not part of what the page describes.
        $before = & $ScriptPath -Root $Work

        Set-Content -LiteralPath (Join-Path $Work 'tests/Gaps/FourTests.cs') -Value 'class Four { }' -NoNewline
        Add-Content -LiteralPath (Join-Path $Work 'README.md') -Value 'edited'

        (& $ScriptPath -Root $Work) | Should -Be $before
    }
}
