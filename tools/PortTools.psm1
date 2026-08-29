<#
.SYNOPSIS
    Tooling for the FuzzyRegex port: the parity ratchet, the generated status board and the
    budget gate the slice driver consults before each autonomous session.

.DESCRIPTION
    Every number this module produces is derived from the repository or from the local Claude
    Code session logs. Nothing here is hand-maintained state, which is the whole point: derived
    status cannot rot the way a prose TODO list does (design spec section 8).

    Tests: tools/tests/PortTools.Tests.ps1 (Pester 5+). Run them with tools/run-tool-tests.ps1.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:PortedNamespaceRoot = 'Fuzzy.Text.RegularExpressions.Tests.Ported.'
$script:TestNamespaceRoot = 'Fuzzy.Text.RegularExpressions.Tests.'
$script:TrxNamespace = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'

function Read-TestResults {
    <#
    .SYNOPSIS
        Reads a Microsoft.Testing.Platform TRX report into one object per test result.

    .DESCRIPTION
        Verified against a real TUnit run on 2026-08-29: the TRX carries the outcome on
        UnitTestResult/@outcome ('Passed', 'Failed', 'NotExecuted' for skipped), the declaring
        type on TestDefinitions/UnitTest/TestMethod/@className, and the skip reason as
        'Skipped: <reason>' inside Output/DebugTrace. A skipped test's reason names the
        capability it waits on ('needs:lookbehind ...'), never a slice id: ported tests are
        written in phase 1, long before the slices that enable them are authored.

    .OUTPUTS
        Objects with Id, Class, Name, Area, IsPorted, Outcome, SkipReason, WaitingOn.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TrxPath
    )

    if (-not (Test-Path -LiteralPath $TrxPath)) {
        # Never treat a missing report as an empty green run: that would let the ratchet pass
        # on a build that never produced results.
        throw "Test report not found: $TrxPath"
    }

    [xml]$trx = Get-Content -LiteralPath $TrxPath -Raw
    $ns = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
    $ns.AddNamespace('t', $script:TrxNamespace)

    $classByTestId = @{}
    foreach ($definition in $trx.SelectNodes('//t:TestDefinitions/t:UnitTest', $ns)) {
        $method = $definition.SelectSingleNode('t:TestMethod', $ns)
        if ($method) {
            $classByTestId[$definition.GetAttribute('id')] = $method.GetAttribute('className')
        }
    }

    foreach ($result in $trx.SelectNodes('//t:Results/t:UnitTestResult', $ns)) {
        $class = $classByTestId[$result.GetAttribute('testId')]
        $name = $result.GetAttribute('testName')

        $outcome = switch ($result.GetAttribute('outcome')) {
            'NotExecuted' { 'Skipped' }
            default { $_ }
        }

        $skipReason = $null
        if ($outcome -eq 'Skipped') {
            $trace = $result.SelectSingleNode('t:Output/t:DebugTrace', $ns)
            if ($trace) {
                $skipReason = ($trace.InnerText -replace '^\s*Skipped:\s*', '').Trim()
            }
        }

        $area, $isPorted = Get-FeatureArea -ClassName $class

        [pscustomobject]@{
            Id         = "$class.$name"
            Class      = $class
            Name       = $name
            Area       = $area
            IsPorted   = $isPorted
            Outcome    = $outcome
            SkipReason = $skipReason
            WaitingOn  = if ($skipReason -and $skipReason -match '^needs:([a-z0-9][a-z0-9-]{2,})(?![a-z0-9-])') { $Matches[1] } else { $null }
        }
    }
}

function Get-FeatureArea {
    <#
    .SYNOPSIS
        Returns the feature area for a test class, and whether it is a ported upstream test.

    .DESCRIPTION
        The area is the namespace segment below the ported root, so
        Fuzzy.Text.RegularExpressions.Tests.Ported.Lookaround.LookbehindTests reports 'Lookaround'. Tests outside
        the ported root (gap tests, convention tests) report their own segment and are excluded
        from the parity figure - parity means parity with upstream's suite.
    #>
    [CmdletBinding()]
    param([string]$ClassName)

    if (-not $ClassName) { return @('Unknown', $false) }

    foreach ($root in @($script:PortedNamespaceRoot, $script:TestNamespaceRoot)) {
        if ($ClassName.StartsWith($root, [System.StringComparison]::Ordinal)) {
            $segment = $ClassName.Substring($root.Length).Split('.')[0]
            if ($segment) {
                # The parentheses matter: -eq binds looser than the comma, so without them
                # PowerShell reads this as @($segment, $root) -eq $script:PortedNamespaceRoot,
                # which filters the array instead of building a pair.
                return @($segment, ($root -eq $script:PortedNamespaceRoot))
            }
        }
    }

    return @('Unknown', $false)
}

function Get-SliceFailureReason {
    <#
    .SYNOPSIS
        Decides whether a slice session actually landed, from facts the driver has gathered.

    .DESCRIPTION
        Pure, so it can be tested without a git repository or a test run - and it is the single
        highest-stakes decision the driver makes. Getting it wrong in the permissive direction
        means a failed slice is recorded as progress and the driver builds the next slice on top
        of a broken tree.

        A slice landed only if ALL of these hold: the session made a commit, it left the working
        tree clean, it moved its slice file to done/, and the parity ratchet is green. Checks are
        ordered most-fundamental first so the reported reason is the useful one.

    .PARAMETER RatchetError
        Null when the ratchet is green. Otherwise the reason - which includes the ratchet failing
        to run at all (no test report, because the committed code does not compile). That case
        must count as a failure, never as a pass.

    .OUTPUTS
        The failure reason, or nothing when the slice landed.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$HeadBefore,
        [Parameter(Mandatory)][AllowEmptyString()][string]$HeadAfter,
        [Parameter(Mandatory)][bool]$IsClean,
        [Parameter(Mandatory)][bool]$SliceStillPending,
        [AllowNull()][AllowEmptyString()][string]$RatchetError
    )

    if ($HeadAfter -eq $HeadBefore) { return 'no commit was made' }
    if (-not $IsClean) { return 'the working tree was left dirty' }
    if ($SliceStillPending) { return 'the slice file was not moved to slices/done/' }
    if ($RatchetError) { return $RatchetError }

    return $null
}

function Test-Ratchet {
    <#
    .SYNOPSIS
        The parity ratchet: a run is green only if every baselined test still passes and nothing
        fails.

    .DESCRIPTION
        Three ways to be red, reported separately so the failure message says what happened:
        a baselined test now fails or is skipped (Regressions), a baselined test is gone from the
        run entirely (Missing), or any test at all failed (Failures). The third catches a newly
        added failing test, which a count-based ratchet would wave through.

    .PARAMETER AcceptRemovals
        Treat missing tests as intentional. A rename or a deliberate deletion looks exactly like
        lost coverage from the outside, so it needs saying out loud: without this switch the
        ratchet is red, and with it the removals are still listed for the operator to check. The
        alternative - hand-editing the baseline - would let real coverage loss through unnoticed.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Results,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$BaselinePassing,
        [switch]$AcceptRemovals
    )

    $byId = @{}
    foreach ($result in $Results) { $byId[$result.Id] = $result }

    $missing = [System.Collections.Generic.List[string]]::new()
    $regressions = [System.Collections.Generic.List[string]]::new()

    foreach ($id in $BaselinePassing) {
        if (-not $byId.ContainsKey($id)) { $missing.Add($id) }
        elseif ($byId[$id].Outcome -ne 'Passed') { $regressions.Add($id) }
    }

    $failures = @($Results | Where-Object Outcome -eq 'Failed' | ForEach-Object Id)
    $passingCount = @($Results | Where-Object Outcome -eq 'Passed').Count

    [pscustomobject]@{
        IsGreen       = (($AcceptRemovals -or $missing.Count -eq 0) -and $regressions.Count -eq 0 -and $failures.Count -eq 0)
        Missing       = $missing.ToArray()
        Regressions   = $regressions.ToArray()
        Failures      = $failures
        PassingCount  = $passingCount
        BaselineCount = $BaselinePassing.Count
    }
}

function Update-Baseline {
    <#
    .SYNOPSIS
        Records the currently passing tests as the new ratchet baseline.

    .DESCRIPTION
        The baseline is the sorted set of passing test ids rather than a count, so the git diff
        of a slice shows exactly which tests it enabled - and a swap (one test enabled, another
        quietly broken) cannot hide behind an unchanged total.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Results,
        [Parameter(Mandatory)][string]$BaselinePath,
        [Parameter(Mandatory)][string]$UpstreamCommit
    )

    $passing = @($Results | Where-Object Outcome -eq 'Passed' | ForEach-Object Id | Sort-Object -Unique)

    $baseline = [ordered]@{
        comment        = 'Generated by tools/PortTools.psm1 (Update-Baseline). The parity ratchet fails if any id listed here stops passing.'
        generatedUtc   = (Get-Date).ToUniversalTime().ToString('o')
        upstreamCommit = $UpstreamCommit
        passingCount   = $passing.Count
        passing        = $passing
    }

    if ($PSCmdlet.ShouldProcess($BaselinePath, 'Write parity baseline')) {
        $json = ConvertTo-Json -InputObject $baseline -Depth 4
        Set-Content -LiteralPath $BaselinePath -Value $json -Encoding utf8
    }
}

function Get-BaselinePassing {
    <#
    .SYNOPSIS
        Reads the passing-test ids out of a baseline file, or an empty set if there is none yet.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$BaselinePath)

    # Emits nothing when there is no baseline yet. Callers wrap the call in @() so the first
    # ever run binds an empty array rather than $null - PowerShell has no way for a function to
    # return an empty array through the pipeline without it being unrolled away.
    if (-not (Test-Path -LiteralPath $BaselinePath)) { return }

    $baseline = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
    return $baseline.passing
}

function New-StatusReport {
    <#
    .SYNOPSIS
        Renders docs/STATUS.md: parity per feature area, derived entirely from a test run.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Results,
        [Parameter(Mandatory)][string]$UpstreamCommit
    )

    $ported = @($Results | Where-Object IsPorted)
    $gaps = @($Results | Where-Object { -not $_.IsPorted })

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('<!--')
    $lines.Add('  GENERATED FILE - do not edit by hand.')
    $lines.Add('  Written by tools/PortTools.psm1 (New-StatusReport) from a test run.')
    $lines.Add('  Regenerate: tools/check-ratchet.ps1')
    $lines.Add('-->')
    $lines.Add('')
    $lines.Add('# FuzzyRegex parity status')
    $lines.Add('')
    # Deliberately no timestamp. CI regenerates this file and fails on any diff, so anything
    # that varies between runs would make that check cry wolf every time. git already records
    # when it was last written.
    $lines.Add("Parity against upstream commit ``$UpstreamCommit``.")
    $lines.Add('')

    $overallPercent = Get-Percent -Passed @($ported | Where-Object Outcome -eq 'Passed').Count -Total $ported.Count
    $lines.Add("**Overall parity: $overallPercent** ($(@($ported | Where-Object Outcome -eq 'Passed').Count) of $($ported.Count) ported upstream tests passing).")
    $lines.Add('')

    $lines.Add('## Ported upstream tests, by feature area')
    $lines.Add('')
    $lines.Add('| Area | Tests | Passing | Skipped | Failing | Parity |')
    $lines.Add('|---|---:|---:|---:|---:|---:|')
    foreach ($group in $ported | Group-Object Area | Sort-Object Name) {
        $passed = @($group.Group | Where-Object Outcome -eq 'Passed').Count
        $skipped = @($group.Group | Where-Object Outcome -eq 'Skipped').Count
        $failed = @($group.Group | Where-Object Outcome -eq 'Failed').Count
        $percent = Get-Percent -Passed $passed -Total $group.Count
        $lines.Add("| $($group.Name) | $($group.Count) | $passed | $skipped | $failed | $percent |")
    }
    $lines.Add('')

    $lines.Add('## Our own tests (gap tests and conventions)')
    $lines.Add('')
    $lines.Add('These are not upstream tests, so they do not count towards parity.')
    $lines.Add('')
    $lines.Add('| Area | Tests | Passing | Skipped | Failing |')
    $lines.Add('|---|---:|---:|---:|---:|')
    foreach ($group in $gaps | Group-Object Area | Sort-Object Name) {
        $passed = @($group.Group | Where-Object Outcome -eq 'Passed').Count
        $skipped = @($group.Group | Where-Object Outcome -eq 'Skipped').Count
        $failed = @($group.Group | Where-Object Outcome -eq 'Failed').Count
        $lines.Add("| $($group.Name) | $($group.Count) | $passed | $skipped | $failed |")
    }
    $lines.Add('')

    $waiting = @($Results | Where-Object WaitingOn | Group-Object WaitingOn | Sort-Object Count -Descending)
    $lines.Add('## Tests waiting on a capability')
    $lines.Add('')
    $lines.Add('What the next slice should deliver, biggest win first.')
    $lines.Add('')
    if ($waiting.Count -eq 0) {
        $lines.Add('None: every test is enabled.')
    }
    else {
        $lines.Add('| Capability | Tests it would enable |')
        $lines.Add('|---|---:|')
        foreach ($group in $waiting) {
            $lines.Add("| ``$($group.Name)`` | $($group.Count) |")
        }
    }
    $lines.Add('')

    # LF, not Environment.NewLine: CI regenerates this on Linux and macOS too, and a CRLF/LF
    # flip would rewrite every line of the diff.
    return ($lines -join "`n")
}

function Get-Percent {
    [CmdletBinding()]
    param([int]$Passed, [int]$Total)

    if ($Total -eq 0) { return 'n/a' }
    return ('{0:0.0}%' -f (100.0 * $Passed / $Total))
}

function Get-SessionTokenUsage {
    <#
    .SYNOPSIS
        Total tokens recorded in local Claude Code session logs since a given time.

    .DESCRIPTION
        Every token type is summed - input, output, cache creation and cache read. Cache reads
        dominate the total, which is fine: this number is a rolling consumption signal to be
        calibrated against measured slice sessions, not a price calculation. A price table would
        rot; a monotone token count will not.

        Verified 2026-08-29: Claude Code writes one JSON object per line to
        ~/.claude/projects/<encoded-cwd>/<session-id>.jsonl, with per-message counts under
        .message.usage and an ISO-8601 .timestamp.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$LogRoot,
        [Parameter(Mandatory)][datetime]$Since
    )

    if (-not (Test-Path -LiteralPath $LogRoot)) { return 0 }

    [long]$total = 0
    # A file untouched since the cutoff cannot hold a record after it, so skip it unread:
    # the log root can hold hundreds of megabytes.
    $files = Get-ChildItem -LiteralPath $LogRoot -Filter '*.jsonl' -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTimeUtc -ge $Since.ToUniversalTime() }

    foreach ($file in $files) {
        foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
            if ($line -notlike '*"usage"*') { continue }

            try { $record = $line | ConvertFrom-Json } catch { continue }

            $usage = $record.PSObject.Properties['message'] ? $record.message.PSObject.Properties['usage']?.Value : $null
            if (-not $usage) { continue }

            $stamp = $record.PSObject.Properties['timestamp']?.Value
            if (-not $stamp) { continue }

            [datetime]$when = [datetime]::MinValue
            if (-not [datetime]::TryParse($stamp, [cultureinfo]::InvariantCulture,
                    [System.Globalization.DateTimeStyles]::AdjustToUniversal -bor [System.Globalization.DateTimeStyles]::AssumeUniversal,
                    [ref]$when)) {
                continue
            }
            if ($when -lt $Since.ToUniversalTime()) { continue }

            foreach ($field in 'input_tokens', 'output_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens') {
                $value = $usage.PSObject.Properties[$field]?.Value
                if ($value) { $total += [long]$value }
            }
        }
    }

    return $total
}

function Get-RateLimitResetsAt {
    <#
    .SYNOPSIS
        The latest still-future rate-limit reset recorded in the session logs, or nothing.

    .DESCRIPTION
        Claude Code records a rateLimit object only when a request is actually rejected (429),
        carrying a Unix-seconds resetsAt. That makes it a reliable back-off signal and nothing
        more: it says the allowance is exhausted now, never how much is left.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$LogRoot)

    if (-not (Test-Path -LiteralPath $LogRoot)) { return $null }

    $now = [DateTimeOffset]::UtcNow
    $latest = $null

    $files = Get-ChildItem -LiteralPath $LogRoot -Filter '*.jsonl' -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTimeUtc -ge $now.UtcDateTime.AddDays(-8) }

    foreach ($file in $files) {
        foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
            if ($line -notlike '*"rateLimit"*') { continue }

            try { $record = $line | ConvertFrom-Json } catch { continue }

            $seconds = $record.PSObject.Properties['rateLimit'] ? $record.rateLimit.PSObject.Properties['resetsAt']?.Value : $null
            if (-not $seconds) { continue }

            $resetsAt = [DateTimeOffset]::FromUnixTimeSeconds([long]$seconds)
            if ($resetsAt -gt $now -and (-not $latest -or $resetsAt -gt $latest)) { $latest = $resetsAt }
        }
    }

    return $latest
}

function Test-BudgetGate {
    <#
    .SYNOPSIS
        Decides whether the slice driver may start another autonomous session.

    .DESCRIPTION
        Four deterministic limits plus a back-off, checked in order of authority:

          1. an outstanding rate limit (the account is already out of allowance),
          2. slices started in the last 24 hours,
          3. slices started in the last 7 days,
          4. tokens consumed in the last 24 hours and 7 days.

        Validated in Phase 0: no local source reports live plan-utilisation, so there is no
        honest '60% of plan' check to make. ~/.claude/stats-cache.json lags by weeks and carries
        no plan denominator; a rateLimit record only appears once a request has been rejected;
        the claude.ai usage page needs an interactive browser, which an unattended driver has
        not got. The deterministic caps are therefore the gate, with token windows as the safety
        net for an unusually expensive slice (design spec section 9 sanctions this fallback).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Budget,
        [Parameter(Mandatory)][string]$SliceLogPath,
        [Parameter(Mandatory)][long]$TokensLastDay,
        [Parameter(Mandatory)][long]$TokensLastWeek,
        # Nullable on purpose: Get-RateLimitResetsAt yields nothing whenever no rate limit has
        # been hit, which is the normal case, and callers pass its result straight through.
        [AllowNull()][Nullable[DateTimeOffset]]$RateLimitResetsAt
    )

    $now = [DateTimeOffset]::UtcNow
    $slices = @(Get-SliceLogEntry -Path $SliceLogPath)
    $slicesToday = @($slices | Where-Object { $_.Timestamp -gt $now.AddHours(-24) }).Count
    $slicesThisWeek = @($slices | Where-Object { $_.Timestamp -gt $now.AddDays(-7) }).Count

    $reason = $null
    if ($null -ne $RateLimitResetsAt -and $RateLimitResetsAt -gt $now) {
        $reason = "an account rate limit is in force until $($RateLimitResetsAt.ToString('u'))"
    }
    elseif ($slicesToday -ge $Budget.maxSlicesPerDay) {
        $reason = "$slicesToday slices today reaches the cap of $($Budget.maxSlicesPerDay)"
    }
    elseif ($slicesThisWeek -ge $Budget.maxSlicesPerWeek) {
        $reason = "$slicesThisWeek slices this week reaches the cap of $($Budget.maxSlicesPerWeek)"
    }
    elseif ($TokensLastDay -ge $Budget.maxTokensPerDay) {
        $reason = "$TokensLastDay tokens today reaches the cap of $($Budget.maxTokensPerDay)"
    }
    elseif ($TokensLastWeek -ge $Budget.maxTokensPerWeek) {
        $reason = "$TokensLastWeek tokens this week reaches the cap of $($Budget.maxTokensPerWeek)"
    }

    [pscustomobject]@{
        Allowed        = ($null -eq $reason)
        Reason         = $reason
        SlicesToday    = $slicesToday
        SlicesThisWeek = $slicesThisWeek
        TokensLastDay  = $TokensLastDay
        TokensLastWeek = $TokensLastWeek
    }
}

function Get-SliceLogEntry {
    <#
    .SYNOPSIS
        Reads the slice log: one record per slice session the driver has started.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    # Emits nothing when the log does not exist; callers wrap in @().
    if (-not (Test-Path -LiteralPath $Path)) { return }

    foreach ($line in Get-Content -LiteralPath $Path) {
        if (-not $line.Trim()) { continue }
        try { $record = $line | ConvertFrom-Json } catch { continue }

        [pscustomobject]@{
            Timestamp   = [DateTimeOffset]::Parse($record.timestamp, [cultureinfo]::InvariantCulture)
            Slice       = $record.slice
            Outcome     = $record.outcome
            TotalTokens = $record.PSObject.Properties['totalTokens']?.Value
        }
    }
}

function Write-SliceLogEntry {
    <#
    .SYNOPSIS
        Appends one slice session to the slice log.

    .DESCRIPTION
        Failed attempts are logged too: a slice that burned allowance and produced nothing still
        spent the budget, so the gate must count it.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Slice,
        [Parameter(Mandatory)][ValidateSet('completed', 'failed', 'parked')][string]$Outcome,
        [long]$TotalTokens = 0
    )

    $entry = [ordered]@{
        timestamp   = [DateTimeOffset]::UtcNow.ToString('o')
        slice       = $Slice
        outcome     = $Outcome
        totalTokens = $TotalTokens
    }

    if ($PSCmdlet.ShouldProcess($Path, "Log slice $Slice as $Outcome")) {
        $directory = Split-Path -Parent $Path
        if ($directory -and -not (Test-Path -LiteralPath $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }
        Add-Content -LiteralPath $Path -Value (ConvertTo-Json -InputObject $entry -Compress) -Encoding utf8
    }
}

Export-ModuleMember -Function `
    Read-TestResults, Get-FeatureArea, Test-Ratchet, Update-Baseline, Get-BaselinePassing,
    New-StatusReport, Get-SessionTokenUsage, Get-RateLimitResetsAt, Test-BudgetGate,
    Get-SliceLogEntry, Write-SliceLogEntry, Get-SliceFailureReason
