#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

BeforeAll {
    $script:ToolsRoot = Split-Path -Parent $PSScriptRoot
    Import-Module (Join-Path $script:ToolsRoot 'PortTools.psm1') -Force
    $script:Fixture = Join-Path $PSScriptRoot 'fixtures/sample.trx'
}

Describe 'Read-TestResults' {
    BeforeAll { $script:Results = Read-TestResults -TrxPath $script:Fixture }

    It 'reads every result in the report' {
        $script:Results.Count | Should -Be 5
    }

    It 'identifies a test by class and name so the identity survives a rerun' {
        ($script:Results | Where-Object Name -eq 'Caret_matches_start').Id |
            Should -Be 'FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Caret_matches_start'
    }

    It 'normalises the TRX NotExecuted outcome to Skipped' {
        ($script:Results | Where-Object Name -eq 'Lookbehind_is_variable_length').Outcome |
            Should -Be 'Skipped'
    }

    It 'keeps Passed and Failed outcomes as they are' {
        ($script:Results | Where-Object Name -eq 'Caret_matches_start').Outcome | Should -Be 'Passed'
        ($script:Results | Where-Object Name -eq 'Dollar_matches_end').Outcome | Should -Be 'Failed'
    }

    It 'takes the feature area from the namespace under the ported root' {
        ($script:Results | Where-Object Name -eq 'Lookbehind_is_variable_length').Area | Should -Be 'Lookaround'
        ($script:Results | Where-Object Name -eq 'Best_match_picks_least_cost').Area | Should -Be 'Fuzzy'
    }

    It 'marks tests outside the ported root as not ported, so parity is measured on ported tests only' {
        $gap = $script:Results | Where-Object Name -eq 'Surrogate_pair_indices'
        $gap.IsPorted | Should -BeFalse
        $gap.Area | Should -Be 'Gaps'
        ($script:Results | Where-Object Name -eq 'Caret_matches_start').IsPorted | Should -BeTrue
    }

    It 'reads the capability a skipped test is waiting on out of the skip reason' {
        $skipped = $script:Results | Where-Object Name -eq 'Lookbehind_is_variable_length'
        $skipped.SkipReason | Should -Be 'needs:lookbehind - variable-length lookbehind is not implemented'
        $skipped.WaitingOn | Should -Be 'lookbehind'
    }

    It 'leaves WaitingOn empty for tests that are not skipped' {
        ($script:Results | Where-Object Name -eq 'Caret_matches_start').WaitingOn | Should -BeNullOrEmpty
    }

    It 'throws when the report does not exist, rather than reporting an empty green run' {
        { Read-TestResults -TrxPath (Join-Path $TestDrive 'missing.trx') } | Should -Throw
    }
}

Describe 'Test-Ratchet' {
    BeforeAll { $script:Results = Read-TestResults -TrxPath $script:Fixture }

    It 'is green when every baselined test still passes and nothing failed' {
        $passing = @('FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Caret_matches_start')
        $results = $script:Results | Where-Object Outcome -ne 'Failed'
        (Test-Ratchet -Results $results -BaselinePassing $passing).IsGreen | Should -BeTrue
    }

    It 'is red when a baselined test now fails' {
        $passing = @('FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Dollar_matches_end')
        $verdict = Test-Ratchet -Results $script:Results -BaselinePassing $passing
        $verdict.IsGreen | Should -BeFalse
        $verdict.Regressions | Should -Contain 'FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Dollar_matches_end'
    }

    It 'is red when a baselined test has disappeared from the run' {
        $passing = @('FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Deleted_test')
        $verdict = Test-Ratchet -Results $script:Results -BaselinePassing $passing
        $verdict.IsGreen | Should -BeFalse
        $verdict.Missing | Should -Contain 'FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Deleted_test'
    }

    It 'is red when any test fails, even one that was never in the baseline' {
        $verdict = Test-Ratchet -Results $script:Results -BaselinePassing @()
        $verdict.IsGreen | Should -BeFalse
        $verdict.Failures | Should -Contain 'FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Dollar_matches_end'
    }

    It 'treats a baselined test that became skipped as a regression' {
        $passing = @('FuzzyRegex.Tests.Ported.Lookaround.LookbehindTests.Lookbehind_is_variable_length')
        $results = $script:Results | Where-Object Outcome -ne 'Failed'
        $verdict = Test-Ratchet -Results $results -BaselinePassing $passing
        $verdict.IsGreen | Should -BeFalse
    }

    It 'stays red on a missing test unless removals are explicitly accepted' {
        # Renaming or deleting a test removes it from the run, which is indistinguishable from
        # losing coverage. The operator has to say so on purpose.
        $passing = @('FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Renamed_away')
        $results = $script:Results | Where-Object Outcome -ne 'Failed'

        $verdict = Test-Ratchet -Results $results -BaselinePassing $passing -AcceptRemovals
        $verdict.IsGreen | Should -BeTrue
        $verdict.Missing | Should -Contain 'FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Renamed_away'
    }

    It 'still goes red on a real regression even when removals are accepted' {
        $passing = @('FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Dollar_matches_end')
        (Test-Ratchet -Results $script:Results -BaselinePassing $passing -AcceptRemovals).IsGreen |
            Should -BeFalse
    }

    It 'reports the passing count so a drop is visible even without named regressions' {
        (Test-Ratchet -Results $script:Results -BaselinePassing @()).PassingCount | Should -Be 2
    }
}

Describe 'Update-Baseline' {
    It 'writes exactly the currently passing test ids, sorted for a readable diff' {
        $path = Join-Path $TestDrive 'baseline.json'
        $results = Read-TestResults -TrxPath $script:Fixture
        Update-Baseline -Results $results -BaselinePath $path -UpstreamCommit 'abc123'

        $saved = Get-Content $path -Raw | ConvertFrom-Json
        $saved.passing | Should -Be @(
            'FuzzyRegex.Tests.Gaps.SurrogateTests.Surrogate_pair_indices',
            'FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Caret_matches_start')
        $saved.upstreamCommit | Should -Be 'abc123'
    }
}

Describe 'Get-BaselinePassing' {
    It 'reads the passing ids back out of a baseline file' {
        $path = Join-Path $TestDrive 'roundtrip.json'
        Update-Baseline -Results (Read-TestResults -TrxPath $script:Fixture) -BaselinePath $path -UpstreamCommit 'abc123'

        Get-BaselinePassing -BaselinePath $path |
            Should -Contain 'FuzzyRegex.Tests.Ported.Anchors.AnchorTests.Caret_matches_start'
    }

    It 'yields an empty set the ratchet can bind on the very first run, with no baseline yet' {
        # The first ever run must not blow up. PowerShell cannot return an empty array through
        # the pipeline, so the contract is 'emits nothing' and callers wrap the call in @().
        $empty = @(Get-BaselinePassing -BaselinePath (Join-Path $TestDrive 'no-baseline.json'))

        $empty.Count | Should -Be 0
        { Test-Ratchet -Results @() -BaselinePassing $empty } | Should -Not -Throw
    }
}

Describe 'Get-SliceLogEntry' {
    It 'yields an empty set when the log does not exist yet' {
        @(Get-SliceLogEntry -Path (Join-Path $TestDrive 'no-slice-log.jsonl')).Count | Should -Be 0
    }
}

Describe 'New-StatusReport' {
    BeforeAll {
        $script:Report = New-StatusReport -Results (Read-TestResults -TrxPath $script:Fixture) -UpstreamCommit 'abc123'
    }

    It 'reports parity per feature area' {
        $script:Report | Should -Match '\|\s*Anchors\s*\|'
        $script:Report | Should -Match '\|\s*Lookaround\s*\|'
    }

    It 'counts parity as passing over total ported tests in the area' {
        # Anchors: 1 passed of 2 ported tests.
        $script:Report | Should -Match 'Anchors[^\r\n]*\|\s*50(\.0)?%'
        # Lookaround: 0 passed of 1.
        $script:Report | Should -Match 'Lookaround[^\r\n]*\|\s*0(\.0)?%'
    }

    It 'excludes gap tests from the parity figure but still reports them' {
        $script:Report | Should -Match 'Overall parity[^\r\n]*25'
        $script:Report | Should -Match 'Gaps'
    }

    It 'lists the capabilities the skipped tests are waiting on, so the next slice can be scoped' {
        $script:Report | Should -Match 'lookbehind'
        $script:Report | Should -Match 'fuzzy-bestmatch'
    }

    It 'stamps the upstream commit so the board says what it is parity against' {
        $script:Report | Should -Match 'abc123'
    }

    It 'says it is generated so nobody edits it by hand' {
        $script:Report | Should -Match '(?i)generated'
    }

    It 'carries no wall-clock time, so regenerating it produces no diff' {
        # CI regenerates the board and fails on any diff. A timestamp would make that check fail
        # on every run whose minute differed from the committing session's, training everyone to
        # ignore it. Asserting the absence of a clock is the reliable check: comparing two
        # back-to-back calls would pass by luck whenever both land in the same minute.
        # git already records when the file was committed.
        $script:Report | Should -Not -Match '\d{1,2}:\d{2}'
    }

    It 'is byte-for-byte reproducible from the same results' {
        $again = New-StatusReport -Results (Read-TestResults -TrxPath $script:Fixture) -UpstreamCommit 'abc123'
        $again | Should -BeExactly $script:Report
    }

    It 'uses LF endings so regenerating on Linux or macOS does not rewrite every line' {
        $script:Report | Should -Not -Match "`r"
    }
}

Describe 'Get-SessionTokenUsage' {
    BeforeAll {
        $script:LogRoot = Join-Path $TestDrive 'projects/some-project'
        New-Item -ItemType Directory -Path $script:LogRoot -Force | Out-Null
        $recent = (Get-Date).ToUniversalTime().AddHours(-1).ToString('o')
        $old = (Get-Date).ToUniversalTime().AddDays(-9).ToString('o')
        @(
            '{"timestamp":"' + $recent + '","message":{"usage":{"input_tokens":10,"output_tokens":5,"cache_creation_input_tokens":100,"cache_read_input_tokens":1000}}}'
            '{"timestamp":"' + $recent + '","message":{"usage":{"input_tokens":1,"output_tokens":2,"cache_creation_input_tokens":3,"cache_read_input_tokens":4}}}'
            '{"timestamp":"' + $old + '","message":{"usage":{"input_tokens":99999,"output_tokens":99999}}}'
            '{"timestamp":"' + $recent + '","type":"user"}'
            'not json at all'
        ) | Set-Content -Path (Join-Path $script:LogRoot 'session.jsonl') -Encoding utf8
    }

    It 'sums every token type inside the window' {
        Get-SessionTokenUsage -LogRoot (Join-Path $TestDrive 'projects') -Since (Get-Date).ToUniversalTime().AddHours(-24) |
            Should -Be 1125
    }

    It 'ignores records outside the window' {
        Get-SessionTokenUsage -LogRoot (Join-Path $TestDrive 'projects') -Since (Get-Date).ToUniversalTime().AddMinutes(-30) |
            Should -Be 0
    }

    It 'survives malformed lines rather than failing the gate open or closed' {
        { Get-SessionTokenUsage -LogRoot (Join-Path $TestDrive 'projects') -Since (Get-Date).ToUniversalTime().AddDays(-30) } |
            Should -Not -Throw
    }

    It 'returns zero when the log root does not exist' {
        Get-SessionTokenUsage -LogRoot (Join-Path $TestDrive 'nope') -Since (Get-Date).ToUniversalTime().AddDays(-1) |
            Should -Be 0
    }
}

Describe 'Test-BudgetGate' {
    BeforeAll {
        $script:Budget = [pscustomobject]@{
            maxSlicesPerDay  = 2
            maxSlicesPerWeek = 5
            maxTokensPerDay  = 1000
            maxTokensPerWeek = 5000
        }

        function script:New-TestSliceLog {
            param([int[]]$HoursAgo)
            $path = Join-Path $TestDrive ([guid]::NewGuid().ToString() + '.jsonl')
            New-Item -ItemType File -Path $path | Out-Null
            foreach ($h in $HoursAgo) {
                $ts = (Get-Date).ToUniversalTime().AddHours(-$h).ToString('o')
                Add-Content -Path $path -Value ('{"timestamp":"' + $ts + '","slice":"S01","outcome":"completed"}')
            }
            $path
        }
    }

    It 'allows a run when every limit has headroom' {
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @(5)) `
            -TokensLastDay 100 -TokensLastWeek 100
        $verdict.Allowed | Should -BeTrue
    }

    It 'blocks when the daily slice cap is reached' {
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @(1, 2)) `
            -TokensLastDay 0 -TokensLastWeek 0
        $verdict.Allowed | Should -BeFalse
        $verdict.Reason | Should -Match 'slices today'
    }

    It 'counts only slices inside the rolling day window' {
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @(1, 30, 40)) `
            -TokensLastDay 0 -TokensLastWeek 0
        $verdict.Allowed | Should -BeTrue
    }

    It 'blocks when the weekly slice cap is reached' {
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @(30, 40, 50, 60, 70)) `
            -TokensLastDay 0 -TokensLastWeek 0
        $verdict.Allowed | Should -BeFalse
        $verdict.Reason | Should -Match 'slices this week'
    }

    It 'blocks when the daily token budget is spent' {
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @()) `
            -TokensLastDay 1001 -TokensLastWeek 1001
        $verdict.Allowed | Should -BeFalse
        $verdict.Reason | Should -Match 'tokens today'
    }

    It 'blocks when the weekly token budget is spent' {
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @()) `
            -TokensLastDay 0 -TokensLastWeek 6000
        $verdict.Allowed | Should -BeFalse
        $verdict.Reason | Should -Match 'tokens this week'
    }

    It 'blocks while a rate limit reset is still in the future' {
        $future = [DateTimeOffset]::UtcNow.AddHours(3)
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @()) `
            -TokensLastDay 0 -TokensLastWeek 0 -RateLimitResetsAt $future
        $verdict.Allowed | Should -BeFalse
        $verdict.Reason | Should -Match 'rate limit'
    }

    It 'ignores a rate limit reset that has already passed' {
        $past = [DateTimeOffset]::UtcNow.AddHours(-3)
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @()) `
            -TokensLastDay 0 -TokensLastWeek 0 -RateLimitResetsAt $past
        $verdict.Allowed | Should -BeTrue
    }

    It 'accepts a null rate-limit reset, which is what Get-RateLimitResetsAt yields normally' {
        # The driver passes the result of Get-RateLimitResetsAt straight through, and that is
        # nothing at all whenever no rate limit has been hit - the common case.
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (New-TestSliceLog -HoursAgo @()) `
            -TokensLastDay 0 -TokensLastWeek 0 -RateLimitResetsAt $null
        $verdict.Allowed | Should -BeTrue
    }

    It 'allows the first ever run, when no slice log exists yet' {
        $verdict = Test-BudgetGate -Budget $script:Budget -SliceLogPath (Join-Path $TestDrive 'never-written.jsonl') `
            -TokensLastDay 0 -TokensLastWeek 0
        $verdict.Allowed | Should -BeTrue
    }
}

Describe 'Write-SliceLogEntry' {
    It 'appends one JSON line per slice attempt so the gate can count them' {
        $path = Join-Path $TestDrive 'slice-log.jsonl'
        Write-SliceLogEntry -Path $path -Slice 'S01' -Outcome 'completed' -TotalTokens 1234
        Write-SliceLogEntry -Path $path -Slice 'S02' -Outcome 'failed' -TotalTokens 999

        $lines = Get-Content $path
        $lines.Count | Should -Be 2
        ($lines[0] | ConvertFrom-Json).slice | Should -Be 'S01'
        ($lines[1] | ConvertFrom-Json).outcome | Should -Be 'failed'
        ($lines[0] | ConvertFrom-Json).totalTokens | Should -Be 1234
    }
}

Describe 'Get-RateLimitResetsAt' {
    It 'returns the latest future reset recorded in the session logs' {
        $root = Join-Path $TestDrive 'rl'
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $future = [DateTimeOffset]::UtcNow.AddHours(2).ToUnixTimeSeconds()
        $ts = (Get-Date).ToUniversalTime().ToString('o')
        Set-Content -Path (Join-Path $root 'a.jsonl') -Encoding utf8 `
            -Value ('{"timestamp":"' + $ts + '","rateLimit":{"status":"rejected","resetsAt":' + $future + '}}')

        (Get-RateLimitResetsAt -LogRoot $root).ToUnixTimeSeconds() | Should -Be $future
    }

    It 'returns nothing when no rate limit has been hit' {
        $root = Join-Path $TestDrive 'rl2'
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        Set-Content -Path (Join-Path $root 'a.jsonl') -Value '{"type":"user"}'

        Get-RateLimitResetsAt -LogRoot $root | Should -BeNullOrEmpty
    }
}

Describe 'Get-SliceFailureReason' {
    BeforeAll {
        $script:Landed = @{
            HeadBefore        = 'aaa111'
            HeadAfter         = 'bbb222'
            IsClean           = $true
            SliceStillPending = $false
            RatchetError      = $null
        }
    }

    It 'reports nothing when the slice really landed' {
        Get-SliceFailureReason @script:Landed | Should -BeNullOrEmpty
    }

    It 'reports a session that made no commit' {
        $facts = $script:Landed.Clone(); $facts.HeadAfter = 'aaa111'
        Get-SliceFailureReason @facts | Should -Match 'no commit'
    }

    It 'reports a session that left the working tree dirty' {
        $facts = $script:Landed.Clone(); $facts.IsClean = $false
        Get-SliceFailureReason @facts | Should -Match 'dirty'
    }

    It 'reports a slice file that was never moved to done/' {
        $facts = $script:Landed.Clone(); $facts.SliceStillPending = $true
        Get-SliceFailureReason @facts | Should -Match 'done/'
    }

    It 'reports a red ratchet' {
        $facts = $script:Landed.Clone(); $facts.RatchetError = 'the parity ratchet is red'
        Get-SliceFailureReason @facts | Should -Be 'the parity ratchet is red'
    }

    It 'reports a ratchet that could not run at all, rather than passing the slice' {
        # A session that commits non-compiling code produces no test report. Treating that as
        # success would let the driver march on over a broken build.
        $facts = $script:Landed.Clone()
        $facts.RatchetError = 'the parity ratchet could not run: Test report not found'
        Get-SliceFailureReason @facts | Should -Match 'could not run'
    }

    It 'names the most fundamental problem first when several hold at once' {
        $facts = @{
            HeadBefore = 'aaa111'; HeadAfter = 'aaa111'; IsClean = $false
            SliceStillPending = $true; RatchetError = 'the parity ratchet is red'
        }
        Get-SliceFailureReason @facts | Should -Match 'no commit'
    }
}
