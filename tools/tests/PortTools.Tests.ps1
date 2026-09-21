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
            Should -Be 'Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Caret_matches_start'
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
        $passing = @('Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Caret_matches_start')
        $results = $script:Results | Where-Object Outcome -ne 'Failed'
        (Test-Ratchet -Results $results -BaselinePassing $passing).IsGreen | Should -BeTrue
    }

    It 'is red when a baselined test now fails' {
        $passing = @('Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Dollar_matches_end')
        $verdict = Test-Ratchet -Results $script:Results -BaselinePassing $passing
        $verdict.IsGreen | Should -BeFalse
        $verdict.Regressions | Should -Contain 'Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Dollar_matches_end'
    }

    It 'is red when a baselined test has disappeared from the run' {
        $passing = @('Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Deleted_test')
        $verdict = Test-Ratchet -Results $script:Results -BaselinePassing $passing
        $verdict.IsGreen | Should -BeFalse
        $verdict.Missing | Should -Contain 'Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Deleted_test'
    }

    It 'is red when any test fails, even one that was never in the baseline' {
        $verdict = Test-Ratchet -Results $script:Results -BaselinePassing @()
        $verdict.IsGreen | Should -BeFalse
        $verdict.Failures | Should -Contain 'Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Dollar_matches_end'
    }

    It 'treats a baselined test that became skipped as a regression' {
        $passing = @('Fuzzy.Text.RegularExpressions.Tests.Ported.Lookaround.LookbehindTests.Lookbehind_is_variable_length')
        $results = $script:Results | Where-Object Outcome -ne 'Failed'
        $verdict = Test-Ratchet -Results $results -BaselinePassing $passing
        $verdict.IsGreen | Should -BeFalse
    }

    It 'stays red on a missing test unless removals are explicitly accepted' {
        # Renaming or deleting a test removes it from the run, which is indistinguishable from
        # losing coverage. The operator has to say so on purpose.
        $passing = @('Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Renamed_away')
        $results = $script:Results | Where-Object Outcome -ne 'Failed'

        $verdict = Test-Ratchet -Results $results -BaselinePassing $passing -AcceptRemovals
        $verdict.IsGreen | Should -BeTrue
        $verdict.Missing | Should -Contain 'Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Renamed_away'
    }

    It 'still goes red on a real regression even when removals are accepted' {
        $passing = @('Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Dollar_matches_end')
        (Test-Ratchet -Results $script:Results -BaselinePassing $passing -AcceptRemovals).IsGreen |
            Should -BeFalse
    }

    It 'reports the passing count so a drop is visible even without named regressions' {
        (Test-Ratchet -Results $script:Results -BaselinePassing @()).PassingCount | Should -Be 2
    }

    It 'tells apart two tests whose ids differ only in case' {
        # A parameterised test that asserts case-insensitive behaviour produces exactly this:
        # Known_names_resolve(LATIN SMALL LETTER A) and Known_names_resolve(latin small letter a)
        # are different tests with ids that differ only in case. PowerShell's default hashtable
        # and Sort-Object -Unique are both case-insensitive, so before this the ratchet kept one
        # of them and silently stopped watching the other.
        $results = @(
            [pscustomobject]@{ Id = 'X.Resolves(ABC)'; Outcome = 'Passed' }
            [pscustomobject]@{ Id = 'X.Resolves(abc)'; Outcome = 'Skipped' }
        )

        $verdict = Test-Ratchet -Results $results -BaselinePassing @('X.Resolves(abc)', 'X.Resolves(ABC)')

        # -BeExactly, not -Contain: Pester's -Contain and -Be are themselves case-insensitive,
        # so they would pass whichever id survived.
        $verdict.IsGreen | Should -BeFalse
        $verdict.Regressions.Count | Should -Be 1
        $verdict.Regressions[0] | Should -BeExactly 'X.Resolves(abc)'
    }
}

Describe 'Update-Baseline' {
    It 'writes exactly the currently passing test ids, sorted for a readable diff' {
        $path = Join-Path $TestDrive 'baseline.json'
        $results = Read-TestResults -TrxPath $script:Fixture
        Update-Baseline -Results $results -BaselinePath $path -UpstreamCommit 'abc123'

        $saved = Get-Content $path -Raw | ConvertFrom-Json
        $saved.passing | Should -Be @(
            'Fuzzy.Text.RegularExpressions.Tests.Gaps.SurrogateTests.Surrogate_pair_indices',
            'Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Caret_matches_start')
        $saved.upstreamCommit | Should -Be 'abc123'
    }

    It 'keeps both of two passing tests whose ids differ only in case' {
        # Sort-Object -Unique is case-insensitive, so it used to collapse these into one and the
        # baseline stopped covering the other. Found by S09, whose \N{...} tests assert exactly
        # this kind of case-insensitivity.
        $path = Join-Path $TestDrive 'case.json'
        $results = @(
            [pscustomobject]@{ Id = 'X.Resolves(ABC)'; Outcome = 'Passed' }
            [pscustomobject]@{ Id = 'X.Resolves(abc)'; Outcome = 'Passed' }
        )

        Update-Baseline -Results $results -BaselinePath $path -UpstreamCommit 'abc123'

        # -BeExactly on each element, not -Contain: Pester's -Contain is case-insensitive and
        # would pass on a baseline that had kept only one of the two.
        $saved = Get-Content $path -Raw | ConvertFrom-Json
        $ids = @($saved.passing)
        $saved.passingCount | Should -Be 2
        $ids.Count | Should -Be 2
        @($ids | Where-Object { $_ -cmatch '^X\.Resolves\(ABC\)$' }).Count | Should -Be 1
        @($ids | Where-Object { $_ -cmatch '^X\.Resolves\(abc\)$' }).Count | Should -Be 1
    }

    It 'keeps both of two passing tests whose ids differ only by a compatibility character' {
        # -CaseSensitive fixed the case collision above but not the culture-sensitivity behind it:
        # the comparer still folds U+212A KELVIN SIGN into 'K', so these two collapsed into one and
        # the baseline silently stopped covering whichever lost. Found by S23's blind review, in
        # S23's own regenerated baseline - the '(?i)[a-z]' rows of CaseInsensitiveMatchingTests are
        # exactly this pair, and regenerating swapped which of them was listed.
        $path = Join-Path $TestDrive 'kelvin.json'
        $kelvin = [char]0x212A
        $results = @(
            [pscustomobject]@{ Id = 'X.Folds(K)'; Outcome = 'Passed' }
            [pscustomobject]@{ Id = "X.Folds($kelvin)"; Outcome = 'Passed' }
        )

        Update-Baseline -Results $results -BaselinePath $path -UpstreamCommit 'abc123'

        $saved = Get-Content $path -Raw | ConvertFrom-Json
        $ids = @($saved.passing)
        $saved.passingCount | Should -Be 2
        $ids.Count | Should -Be 2
        # [string]::Equals with StringComparison.Ordinal, not '-eq': PowerShell's own string
        # comparison is culture-sensitive too, and matches both of these against either.
        $ordinal = [System.StringComparison]::Ordinal
        @($ids | Where-Object { [string]::Equals($_, 'X.Folds(K)', $ordinal) }).Count | Should -Be 1
        @($ids | Where-Object { [string]::Equals($_, "X.Folds($kelvin)", $ordinal) }).Count | Should -Be 1
    }

    It 'drops a result with no id rather than baselining an empty string' {
        # The pipeline that used to sort these dropped a $null on the way through; casting to
        # [string[]] turns one into '' instead, and an empty id in the baseline is a test the
        # ratchet then reports as Missing for ever. Found by S23's second blind pass.
        $path = Join-Path $TestDrive 'nullid.json'
        $results = @(
            [pscustomobject]@{ Id = $null; Outcome = 'Passed' }
            [pscustomobject]@{ Id = 'X.Has(anId)'; Outcome = 'Passed' }
        )

        Update-Baseline -Results $results -BaselinePath $path -UpstreamCommit 'abc123'

        $saved = Get-Content $path -Raw | ConvertFrom-Json
        $saved.passingCount | Should -Be 1
        @($saved.passing) | Should -Be @('X.Has(anId)')
    }
}

Describe 'Get-BaselinePassing' {
    It 'reads the passing ids back out of a baseline file' {
        $path = Join-Path $TestDrive 'roundtrip.json'
        Update-Baseline -Results (Read-TestResults -TrxPath $script:Fixture) -BaselinePath $path -UpstreamCommit 'abc123'

        Get-BaselinePassing -BaselinePath $path |
            Should -Contain 'Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors.AnchorTests.Caret_matches_start'
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

Describe 'Read-Budget' {
    It 'reads the budget file' {
        $path = Join-Path $TestDrive 'budget-ok.json'
        '{"maxSlicesPerDay":5,"sliceTimeoutMinutes":180}' | Set-Content -LiteralPath $path -Encoding utf8

        (Read-Budget -Path $path -LastGood $null).sliceTimeoutMinutes | Should -Be 180
    }

    It 'keeps the last budget that parsed when the file is caught mid-save' {
        # The driver re-reads this file before every slice, so it will eventually read one the
        # operator is halfway through saving. A parse failure there must not kill an unattended
        # run that has been going for hours.
        $path = Join-Path $TestDrive 'budget-torn.json'
        '{"maxSlicesPerDay":5,"sliceTimeout' | Set-Content -LiteralPath $path -Encoding utf8
        $lastGood = [pscustomobject]@{ maxSlicesPerDay = 5; sliceTimeoutMinutes = 180 }

        $budget = Read-Budget -Path $path -LastGood $lastGood -WarningAction SilentlyContinue

        $budget.sliceTimeoutMinutes | Should -Be 180
    }

    It 'keeps the last budget when the file has been truncated to nothing' {
        # An empty file parses to nothing rather than throwing, so it would silently null the
        # budget and blow up on the first property read instead.
        $path = Join-Path $TestDrive 'budget-empty.json'
        Set-Content -LiteralPath $path -Value '' -Encoding utf8
        $lastGood = [pscustomobject]@{ sliceTimeoutMinutes = 180 }

        (Read-Budget -Path $path -LastGood $lastGood -WarningAction SilentlyContinue).sliceTimeoutMinutes |
            Should -Be 180
    }

    It 'throws on the very first read, because there is no last good budget to fall back on' {
        # No budget at all is not the same as a torn read: carrying on would mean running with
        # no caps whatsoever.
        { Read-Budget -Path (Join-Path $TestDrive 'no-budget.json') -LastGood $null } | Should -Throw
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

    It 'records a rate-limited attempt as such, so it is not read back as a broken slice' {
        $path = Join-Path $TestDrive 'slice-log-rl.jsonl'
        Write-SliceLogEntry -Path $path -Slice 'S08' -Outcome 'rate-limited' -TotalTokens 42

        (Get-Content $path | ConvertFrom-Json).outcome | Should -Be 'rate-limited'
    }

    It 'records a checkpoint - a green commit with the slice still pending - as its own outcome' {
        $path = Join-Path $TestDrive 'slice-log-cp.jsonl'
        Write-SliceLogEntry -Path $path -Slice 'S08' -Outcome 'checkpoint' -TotalTokens 42

        (Get-Content $path | ConvertFrom-Json).outcome | Should -Be 'checkpoint'
    }

    It 'records the rescue details, so rescued work can be found from the log alone' {
        $path = Join-Path $TestDrive 'slice-log-rescue.jsonl'
        $rescue = [pscustomobject]@{
            StashLabel   = 'slice-rescue S08 2026-08-30 12:00:00'
            BranchName   = 'slice-rescue/S08-20260830-120000'
            AbandonedSha = 'deadbee'
        }

        Write-SliceLogEntry -Path $path -Slice 'S08' -Outcome 'failed' -TotalTokens 1 -Rescue $rescue

        $entry = Get-Content $path | ConvertFrom-Json
        $entry.rescue.branch | Should -Be 'slice-rescue/S08-20260830-120000'
        $entry.rescue.abandonedSha | Should -Be 'deadbee'
    }

    It 'writes no rescue field when nothing was rescued' {
        $path = Join-Path $TestDrive 'slice-log-norescue.jsonl'
        Write-SliceLogEntry -Path $path -Slice 'S08' -Outcome 'completed' -TotalTokens 1

        (Get-Content $path | ConvertFrom-Json).PSObject.Properties.Name | Should -Not -Contain 'rescue'
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

    It 'reports a CHECKPOINT, not a failure, when a green committed session left its slice file pending' {
        # S29 and S40a both ended a session with a green commit and the slice still open, meaning
        # "more sessions needed"; the driver rolled both back and a recovery session had to fish
        # the commit out of the reflog. A committed, clean, green tree is never thrown away.
        $facts = $script:Landed.Clone(); $facts.SliceStillPending = $true
        Get-SliceFailureReason @facts | Should -Be 'checkpoint'
    }

    It 'still fails a pending slice whose ratchet is red - a checkpoint must be green' {
        $facts = $script:Landed.Clone(); $facts.SliceStillPending = $true; $facts.RatchetError = 'the parity ratchet is red'
        Get-SliceFailureReason @facts | Should -Be 'the parity ratchet is red'
    }

    It 'names the most fundamental problem first when several hold at once' {
        $facts = @{
            HeadBefore = 'aaa111'; HeadAfter = 'aaa111'; IsClean = $false
            SliceStillPending = $true; RatchetError = 'the parity ratchet is red'
        }
        Get-SliceFailureReason @facts | Should -Match 'no commit'
    }
}

Describe 'Undo-FailedSlice' {
    BeforeAll {
        # A real repository, not a mock. This function's whole job is destroying and preserving
        # work on disk, and every bug it has had was in what git actually did.
        function script:New-ScratchRepo {
            $path = Join-Path ([System.IO.Path]::GetTempPath()) "undo-slice-$([guid]::NewGuid().ToString('N'))"
            New-Item -ItemType Directory -Path $path | Out-Null
            git -C $path init --quiet
            git -C $path config user.email 'test@example.com'
            git -C $path config user.name 'Test'
            New-Item -ItemType Directory -Path (Join-Path $path 'docs/plan') -Force | Out-Null
            New-Item -ItemType Directory -Path (Join-Path $path 'src') -Force | Out-Null
            'baseline' | Set-Content (Join-Path $path 'src/kept.txt')
            'docs/plan/slice-log.jsonl' | Set-Content (Join-Path $path '.gitignore')
            git -C $path add -A
            git -C $path commit --quiet -m 'baseline'
            $path
        }
    }

    It 'keeps a green in-session commit and rolls back only the uncommitted remainder when asked' {
        # S43's second sitting: a checkpoint commit, then dirty unfinished work, then a kill. The
        # commit is green and must survive; only the dirty remainder is stashed.
        $repo = script:New-ScratchRepo
        try {
            $before = (git -C $repo rev-parse HEAD).Trim()
            'checkpoint' | Set-Content (Join-Path $repo 'src/checkpoint.txt')
            git -C $repo add -A; git -C $repo commit --quiet -m 'checkpoint'
            $checkpoint = (git -C $repo rev-parse HEAD).Trim()
            'unfinished' | Set-Content (Join-Path $repo 'src/unfinished.txt')

            $rescue = Undo-FailedSlice -RepoRoot $repo -HeadBefore $before -SliceName 'S43' -KeepHeadIfGreen { $true }

            (git -C $repo rev-parse HEAD).Trim() | Should -Be $checkpoint
            $rescue.KeptHead | Should -Be $checkpoint
            $rescue.AbandonedSha | Should -BeNullOrEmpty
            Test-Path (Join-Path $repo 'src/unfinished.txt') | Should -BeFalse
            git -C $repo stash list | Should -Match 'slice-rescue'
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'still resets to the session start when the in-session commit is red' {
        $repo = script:New-ScratchRepo
        try {
            $before = (git -C $repo rev-parse HEAD).Trim()
            'broken' | Set-Content (Join-Path $repo 'src/broken.txt')
            git -C $repo add -A; git -C $repo commit --quiet -m 'red commit'
            $red = (git -C $repo rev-parse HEAD).Trim()

            $rescue = Undo-FailedSlice -RepoRoot $repo -HeadBefore $before -SliceName 'S43' -KeepHeadIfGreen { $false }

            (git -C $repo rev-parse HEAD).Trim() | Should -Be $before
            $rescue.KeptHead | Should -BeNullOrEmpty
            $rescue.AbandonedSha | Should -Be $red
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'rescues uncommitted work into a stash instead of deleting it' {
        $repo = script:New-ScratchRepo
        try {
            'green work' | Set-Content (Join-Path $repo 'src/new.txt')
            $head = (git -C $repo rev-parse HEAD).Trim()

            $rescue = Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName 'S07-parser-skeleton'

            $rescue.StashLabel | Should -Match 'slice-rescue S07-parser-skeleton'
            git -C $repo stash list | Should -Match 'slice-rescue'
            # The rescued file is gone from the tree but recoverable from the stash.
            Test-Path (Join-Path $repo 'src/new.txt') | Should -BeFalse
            git -C $repo stash show --include-untracked --name-only 'stash@{0}' | Should -Contain 'src/new.txt'
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'pins the rescued work to a permanent branch, which a stash drop cannot destroy' {
        # A stash entry is invisible unless somebody thinks to run `git stash list`, and
        # `git stash clear` deletes the lot. A branch shows up in `git branch` and survives.
        $repo = script:New-ScratchRepo
        try {
            'green work' | Set-Content (Join-Path $repo 'src/new.txt')
            $head = (git -C $repo rev-parse HEAD).Trim()

            $rescue = Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName 'S08-compiler'

            $rescue.BranchName | Should -Match '^slice-rescue/S08-compiler-\d{8}-\d{6}$'
            git -C $repo branch --list $rescue.BranchName | Should -Not -BeNullOrEmpty

            git -C $repo stash clear
            git -C $repo stash apply $rescue.BranchName | Out-Null
            Get-Content (Join-Path $repo 'src/new.txt') | Should -Be 'green work'
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'records the SHA of the commit the reset is about to throw away' {
        # A session can commit and still fail the ratchet. Without the SHA that commit survives
        # only in the reflog, where nobody looks and gc eventually collects it.
        $repo = script:New-ScratchRepo
        try {
            $head = (git -C $repo rev-parse HEAD).Trim()
            'bad' | Set-Content (Join-Path $repo 'src/bad.txt')
            git -C $repo add -A
            git -C $repo commit --quiet -m 'a slice that failed the ratchet'
            $committed = (git -C $repo rev-parse HEAD).Trim()

            $rescue = Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName 'S08'

            $rescue.AbandonedSha | Should -Be $committed
            # The SHA is worth recording only if it still resolves to the work after the reset.
            (git -C $repo show --stat $committed | Out-String) | Should -Match 'src/bad.txt'
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'reports no abandoned SHA when the session never committed' {
        $repo = script:New-ScratchRepo
        try {
            'work' | Set-Content (Join-Path $repo 'src/new.txt')
            $head = (git -C $repo rev-parse HEAD).Trim()

            (Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName 'S08').AbandonedSha |
                Should -BeNullOrEmpty
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'leaves the tree at HeadBefore, not at HEAD, when the session committed a red slice' {
        # The case the reset was written for: a session commits and still fails. Resetting to
        # HEAD would keep that commit and strand the slice.
        $repo = script:New-ScratchRepo
        try {
            $head = (git -C $repo rev-parse HEAD).Trim()
            'bad' | Set-Content (Join-Path $repo 'src/bad.txt')
            git -C $repo add -A
            git -C $repo commit --quiet -m 'a slice that failed the ratchet'

            Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName 'S07' | Out-Null

            (git -C $repo rev-parse HEAD).Trim() | Should -Be $head
            Test-Path (Join-Path $repo 'src/bad.txt') | Should -BeFalse
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'keeps the gitignored slice log, because the budget gate counts from it' {
        # -u rather than -a, and clean -fd rather than -fdx. Removing the log would reset the
        # driver's own daily cap and let it run unlimited sessions.
        $repo = script:New-ScratchRepo
        try {
            $log = Join-Path $repo 'docs/plan/slice-log.jsonl'
            '{"slice":"S07"}' | Set-Content $log
            'work' | Set-Content (Join-Path $repo 'src/new.txt')
            $head = (git -C $repo rev-parse HEAD).Trim()

            Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName 'S07' | Out-Null

            Test-Path $log | Should -BeTrue
            Get-Content $log | Should -Be '{"slice":"S07"}'
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'leaves the working tree clean so the retry starts from known-good state' {
        $repo = script:New-ScratchRepo
        try {
            'work' | Set-Content (Join-Path $repo 'src/new.txt')
            'edit' | Add-Content (Join-Path $repo 'src/kept.txt')
            $head = (git -C $repo rev-parse HEAD).Trim()

            Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName 'S07' | Out-Null

            git -C $repo status --porcelain | Should -BeNullOrEmpty
            Get-Content (Join-Path $repo 'src/kept.txt') | Should -Be 'baseline'
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'reports no stash and no branch when the tree was already clean' {
        $repo = script:New-ScratchRepo
        try {
            $head = (git -C $repo rev-parse HEAD).Trim()

            $rescue = Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName 'S07'

            $rescue.StashLabel | Should -BeNullOrEmpty
            $rescue.BranchName | Should -BeNullOrEmpty
            git -C $repo stash list | Should -BeNullOrEmpty
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }

    It 'tolerates an empty slice name rather than throwing under StrictMode' {
        $repo = script:New-ScratchRepo
        try {
            'work' | Set-Content (Join-Path $repo 'src/new.txt')
            $head = (git -C $repo rev-parse HEAD).Trim()

            # Should -Not -Throw runs the block in its own scope, so the result has to come out
            # through $script: rather than a local.
            { $script:EmptyNameRescue = Undo-FailedSlice -RepoRoot $repo -HeadBefore $head -SliceName '' } |
                Should -Not -Throw
            git -C $repo stash list | Should -Match 'slice-rescue'
            # No dangling separator: a component starting with a hyphen is read as an option by
            # every later git command, and a branch nobody can name is no better than no branch.
            $script:EmptyNameRescue.BranchName | Should -Match '^slice-rescue/\d{8}-\d{6}$'
            git -C $repo branch --list $script:EmptyNameRescue.BranchName | Should -Not -BeNullOrEmpty
        }
        finally { Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue }
    }
}

Describe 'Test-HeadroomProxy' {
    It 'reports the proxy healthy when /health answers ready' {
        Mock -ModuleName PortTools Invoke-RestMethod { [pscustomobject]@{ status = 'healthy'; ready = $true; version = '0.37.0' } }

        $probe = Test-HeadroomProxy -BaseUrl 'http://127.0.0.1:8787'

        $probe.Healthy | Should -BeTrue
        $probe.Detail | Should -Match '0\.37\.0'
    }

    It 'probes /health on the given base URL, not the messages endpoint' {
        Mock -ModuleName PortTools Invoke-RestMethod { [pscustomobject]@{ status = 'healthy'; ready = $true } }

        Test-HeadroomProxy -BaseUrl 'http://127.0.0.1:9999/' | Out-Null

        Should -Invoke -ModuleName PortTools Invoke-RestMethod -Times 1 -Exactly `
            -ParameterFilter { $Uri -eq 'http://127.0.0.1:9999/health' }
    }

    It 'reports unhealthy when the proxy answers but is not ready, so a slice is not launched into a half-started proxy' {
        Mock -ModuleName PortTools Invoke-RestMethod { [pscustomobject]@{ status = 'starting'; ready = $false } }

        $probe = Test-HeadroomProxy -BaseUrl 'http://127.0.0.1:8787'

        $probe.Healthy | Should -BeFalse
        $probe.Detail | Should -Match 'starting'
    }

    It 'reports unhealthy, with the error, when nothing is listening' {
        Mock -ModuleName PortTools Invoke-RestMethod { throw 'No connection could be made because the target machine actively refused it.' }

        $probe = Test-HeadroomProxy -BaseUrl 'http://127.0.0.1:8787'

        $probe.Healthy | Should -BeFalse
        $probe.Detail | Should -Match 'refused'
    }
}

Describe 'Test-AllowanceFloor' {
    BeforeAll {
        $script:Now = [datetimeoffset]::Parse('2026-09-18T22:00:00+01:00')
        $script:Reset = [datetimeoffset]::Parse('2026-09-19T01:10:00+01:00')
        function script:Snapshot([int]$five, [int]$age = 3) {
            [pscustomobject]@{ Source = 'last-usage.json'; AgeMinutes = $age; FiveHourPercent = $five; FiveHourResetsAt = $script:Reset; SevenDayPercent = 61; SevenDayResetsAt = $script:Reset.AddDays(2) }
        }
    }
    It 'lets a sitting start below the floor' {
        (Test-AllowanceFloor -Allowance (Snapshot 87) -Now $script:Now).Allowed | Should -BeTrue
    }
    It 'spends the weekly window to 98% before blocking (owner 2026-09-19)' {
        $a = Snapshot 40; $a.SevenDayPercent = 97
        (Test-AllowanceFloor -Allowance $a -Now $script:Now).Allowed | Should -BeTrue
        $a.SevenDayPercent = 98
        $v = Test-AllowanceFloor -Allowance $a -Now $script:Now
        $v.Allowed | Should -BeFalse
        $v.Reason | Should -Match 'seven-day'
    }
    It 'blocks at the floor and waits for the five-hour reset' {
        $v = Test-AllowanceFloor -Allowance (Snapshot 88) -Now $script:Now
        $v.Allowed | Should -BeFalse
        $v.WaitUntil | Should -Be $script:Reset
        $v.Reason | Should -Match 'five-hour'
    }
    It 'treats a stale snapshot as unknown and lets the sitting start, saying so' {
        $v = Test-AllowanceFloor -Allowance (Snapshot 99 -age 60) -Now $script:Now
        $v.Allowed | Should -BeTrue
        $v.Stale | Should -BeTrue
    }
    It 'treats a missing snapshot as unknown' {
        (Test-AllowanceFloor -Allowance $null).Stale | Should -BeTrue
    }
    It 'waits ten minutes when the reset time is already in the past, rather than spinning' {
        $snap = Snapshot 95; $snap.FiveHourResetsAt = $script:Now.AddMinutes(-1)
        (Test-AllowanceFloor -Allowance $snap -Now $script:Now).WaitUntil | Should -Be $script:Now.AddMinutes(10)
    }
}

Describe 'Read-Allowance' {
    BeforeAll {
        Set-StrictMode -Version Latest
        $script:Dir = Join-Path ([IO.Path]::GetTempPath()) ("allowance-" + [guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:Dir | Out-Null
    }
    AfterAll { Remove-Item -LiteralPath $script:Dir -Recurse -Force -ErrorAction SilentlyContinue }
    It 'reads the shape both writers produce' {
        $p = Join-Path $script:Dir 'good.json'
        '{"rate_limits":{"five_hour":{"used_percentage":45,"resets_at":1789794600},"seven_day":{"used_percentage":68,"resets_at":1789995600}}}' | Set-Content -LiteralPath $p
        $a = Read-Allowance -Paths @($p)
        $a.FiveHourPercent | Should -Be 45
        $a.FiveHourResetsAt.ToUnixTimeSeconds() | Should -Be 1789794600
        $a.FiveHourResetsAt.Offset | Should -Be ([DateTimeOffset]::Now.Offset)  # printed as local time, not UTC
    }
    It 'returns unknown, not an error, for a snapshot without rate_limits (the 2026-09-19 04:12 crash)' {
        $p = Join-Path $script:Dir 'shapeless.json'
        '{"model":{"id":"x"}}' | Set-Content -LiteralPath $p
        Read-Allowance -Paths @($p) | Should -BeNullOrEmpty
    }
    It 'returns unknown for a half-written file' {
        $p = Join-Path $script:Dir 'partial.json'
        '{"rate_limits":{"five_hour":{"used_perc' | Set-Content -LiteralPath $p -NoNewline
        Read-Allowance -Paths @($p) | Should -BeNullOrEmpty
    }
    It 'returns unknown when no file exists' {
        Read-Allowance -Paths @((Join-Path $script:Dir 'missing.json')) | Should -BeNullOrEmpty
    }
}

Describe 'Resolve-SliceTimeout' {
    BeforeAll {
        # A Monday evening, so "07:30" is tomorrow and "23:00" is tonight.
        $script:Evening = [datetime]'2026-09-21T22:00:00'
    }

    It 'gives the budget its full run when nothing constrains it' {
        $result = Resolve-SliceTimeout -BudgetMinutes 285 -Now $script:Evening
        $result.Minutes | Should -Be 285
        $result.TooShort | Should -BeFalse
    }

    It 'lets an explicit override beat the budget' {
        (Resolve-SliceTimeout -BudgetMinutes 285 -OverrideMinutes 90 -Now $script:Evening).Minutes | Should -Be 90
    }

    It 'keeps the budget when the stop is further away than the budget reaches' {
        $result = Resolve-SliceTimeout -BudgetMinutes 285 -StopBy '07:30' -Now $script:Evening
        $result.Minutes | Should -Be 285
        $result.Reason | Should -Be '285 minutes'
    }

    It 'clamps to the stop when the stop comes first' {
        # 04:02 to 07:30 is 208 minutes, well inside the 285 the budget would allow.
        $result = Resolve-SliceTimeout -BudgetMinutes 285 -StopBy '07:30' -Now ([datetime]'2026-09-22T04:02:00')
        $result.Minutes | Should -Be 208
        $result.TooShort | Should -BeFalse
        $result.Reason | Should -Match 'clamped by the 07:30 stop'
    }

    It 'reads a stop time earlier in the day as tomorrow' {
        # 22:00 Monday to 07:30 Tuesday is 570 minutes, not a negative nine and a half hours.
        (Resolve-SliceTimeout -BudgetMinutes 1200 -StopBy '07:30' -Now $script:Evening).Minutes | Should -Be 570
    }

    It 'reads a stop time later today as today' {
        (Resolve-SliceTimeout -BudgetMinutes 1200 -StopBy '23:00' -Now $script:Evening).Minutes | Should -Be 60
    }

    It 'refuses a gap too short to reach a commit, and says how short' {
        $result = Resolve-SliceTimeout -BudgetMinutes 285 -StopBy '07:30' -Now ([datetime]'2026-09-22T06:50:00')
        $result.TooShort | Should -BeTrue
        $result.Reason | Should -Match 'only 40 minutes remain before 07:30'
    }

    It 'lands the sitting before the stop rather than on it' {
        # The driver kills the session at Now + Minutes, so the arithmetic must not round up past
        # the stop - a fraction of a second either way is a minute of the owner's morning.
        $now = [datetime]'2026-09-22T04:00:30'
        $result = Resolve-SliceTimeout -BudgetMinutes 285 -StopBy '07:30' -Now $now
        $now.AddMinutes($result.Minutes) | Should -BeLessOrEqual ([datetime]'2026-09-22T07:30:00')
    }

    It 'throws on a stop time it cannot read, rather than running to the full budget' {
        { Resolve-SliceTimeout -BudgetMinutes 285 -StopBy 'half seven' -Now $script:Evening } |
            Should -Throw '*24-hour time of day*'
    }
}
