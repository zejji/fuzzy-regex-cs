<#
.SYNOPSIS
    Runs port slices autonomously: budget gate, fresh Claude session, verify, repeat.

.DESCRIPTION
    One loop iteration is one slice (design spec section 9):

      1. pick the next pending slice from docs/plan/slices/,
      2. refuse to cross a phase boundary,
      3. check the budget gate,
      4. run a fresh `claude -p` session on the port-slice skill,
      5. verify the slice really landed - the session succeeded, a commit was made, the working
         tree is clean, the slice file moved to done/ and the ratchet is green,
      6. log what it cost, and go again.

    It stops at the first of: no pending slices (phase boundary), a phase change, the budget
    gate, -MaxSlices, or two consecutive failures - which parks the slice with a note in
    docs/plan/STATE.md and waits for a human.

    A session killed by the account's usage limit is NOT one of those failures: the slice was
    never broken, so it is rescued and logged but not counted, and the budget gate stops the run
    on the next iteration with the reset time.

    Every session is fresh. Context is never carried between slices: that is the whole point
    (design spec section 8).

.PARAMETER MaxSlices
    Stop after this many successful slices. Default: run until another stop condition fires.

.PARAMETER DryRun
    Show what the driver would do - which slice, and the budget verdict - and start nothing.

.EXAMPLE
    tools/run-slices.ps1
    tools/run-slices.ps1 -MaxSlices 3
    tools/run-slices.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [int]$MaxSlices = [int]::MaxValue,
    # Restrict the run to one phase's slices, so a driver in a worktree (Phase 7 optimisation) can
    # skip the earlier phase's files that main is still working through. 0 = any phase.
    [int]$Phase = 0,
    [ValidateSet('opus', 'sonnet', 'fable')][string]$Model = 'opus',
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'PortTools.psm1') -Force

$slicesDir = Join-Path $repoRoot 'docs/plan/slices'
$statePath = Join-Path $repoRoot 'docs/plan/STATE.md'
$sliceLogPath = Join-Path $repoRoot 'docs/plan/slice-log.jsonl'
$budgetPath = Join-Path $repoRoot 'docs/plan/budget.json'
$sessionLogRoot = Join-Path $env:USERPROFILE '.claude/projects'

# The unattended session may edit the repo, build, test and commit - and nothing else. A tool
# outside this list stalls the slice rather than doing something unreviewed on the machine.
$allowedTools = @(
    'Read', 'Write', 'Edit', 'Glob', 'Grep', 'TodoWrite', 'Skill', 'Task', 'Agent',
    # TWO tools, two sets of rules. This machine sets CLAUDE_CODE_USE_POWERSHELL_TOOL=1 in
    # ~/.claude/settings.json, which the child session inherits, so it reaches for the PowerShell
    # tool as readily as Bash - and PowerShell tool calls are matched against 'PowerShell(...)'
    # rules, never 'Bash(...)'. A Bash-only allowlist silently denies every PowerShell call.
    # Measured 2026-08-30: with only the Bash rules below, `dotnet --version | Select-Object
    # -Last 1` was denied; adding the PowerShell mirrors ran it.
    'Bash(dotnet *)', 'Bash(git *)', 'Bash(pwsh *)', 'Bash(python *)',
    'PowerShell(dotnet *)', 'PowerShell(git *)', 'PowerShell(pwsh *)', 'PowerShell(python *)',

    # Claude Code decomposes a compound command and requires EVERY part to match, so
    # `dotnet build ... | Select-Object -Last 60` is denied on the filter, not on dotnet. These
    # read, filter and print; none of them writes, deletes or executes. Mutating tools (sed -i
    # and friends) stay out on purpose: edits belong in the Edit tool, where they are visible in
    # the transcript. The cmdlets are PowerShell-only - `Select-Object` is not a command in Git
    # Bash - and head/tail/grep are Bash-only, so each name goes on the side that has it.
    'Bash(head *)', 'Bash(tail *)', 'Bash(grep *)',
    'PowerShell(Select-Object *)', 'PowerShell(Select-String *)',
    'PowerShell(Get-Content *)', 'PowerShell(Get-ChildItem *)',

    # Research tools, added 2026-09-14 after S45's first sitting stalled on all of them: the
    # owner's evidence standard (spec amendment 16) requires the definitive document quoted and a
    # real run of a second engine, so a session needs the web, a fetch into .scratch/, and perl.
    # tasklist / Get-Process are read-only and let a session see an orphaned test host (S44).
    'WebFetch', 'WebSearch', 'Bash(curl *)', 'Bash(perl *)', 'Bash(wsl *)',
    'Bash(tasklist *)', 'PowerShell(Get-Process *)',
    # Compound commands are matched part by part, so `cd repo && dotnet run ...` was denied on the
    # cd (S45, 2026-09-14). These read or set nothing outside the shell. Owner-approved 2026-09-14.
    'Bash(cd *)', 'Bash(export *)', 'Bash(wc *)', 'Bash(cat *)', 'Bash(ls *)', 'Bash(echo *)'
)

function Get-PendingSlice {
    Get-ChildItem -LiteralPath $slicesDir -Filter 'S*.md' -File -ErrorAction SilentlyContinue |
        Sort-Object Name |
        Where-Object { $Phase -eq 0 -or (Get-SlicePhase -SliceFile $_) -eq $Phase } |
        Select-Object -First 1
}

function Get-SlicePhase {
    param([System.IO.FileInfo]$SliceFile)

    foreach ($line in Get-Content -LiteralPath $SliceFile.FullName -TotalCount 20) {
        if ($line -match '^\s*phase:\s*(\d+)\s*$') { return [int]$Matches[1] }
    }

    throw "$($SliceFile.Name) has no 'phase:' line in its front matter; the driver cannot tell which phase it belongs to."
}

function Get-GitState {
    [pscustomobject]@{
        Head    = (git -C $repoRoot rev-parse HEAD).Trim()
        IsClean = -not (git -C $repoRoot status --porcelain)
    }
}

function Invoke-SliceSession {
    <#
        Runs one fresh Claude session. The prompt is deliberately tiny - the port-slice skill
        self-orients from STATE.md, the roadmap, the slice file and the generated status, so a
        long briefing here would only add stale context.
    #>
    param([int]$TimeoutMinutes, [string]$HeadBefore)

    $process = $null

    try {
        # ProcessStartInfo.ArgumentList, not Start-Process -ArgumentList. Start-Process joins the
        # list with spaces and quotes nothing, so 'Bash(dotnet *)' would reach the CLI as two
        # separate tokens and the scoped Bash allowlist - the entire safety boundary for an
        # unattended session - would silently not apply. ArgumentList quotes each argument.
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = 'claude'
        $startInfo.WorkingDirectory = $repoRoot
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardInput = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        # The session hook (tools/session-hook.ps1, wired in .claude/settings.json) speaks only to
        # sessions carrying this variable, and reads the deadline from the file written here, so
        # an unattended session knows when the driver will kill it and can be sent a one-shot
        # message through .claude/driver/orchestrator-message.txt. Added 2026-09-13 (DECISIONS);
        # moved out of .scratch/ on 2026-09-15 because slice sessions clear that directory and
        # took the deadline file with it mid-sitting.
        $startInfo.Environment['FUZZY_SLICE_SESSION'] = '1'
        $deadline = [datetimeoffset]::Now.AddMinutes($TimeoutMinutes)
        New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot '.claude/driver') | Out-Null
        Set-Content -LiteralPath (Join-Path $repoRoot '.claude/driver/session-deadline.txt') -Value $deadline.ToString('o') -NoNewline
        Remove-Item -LiteralPath (Join-Path $repoRoot '.claude/driver/orchestrator-message.txt.delivered') -Force -ErrorAction SilentlyContinue
        $arguments = @(
            '-p', '--model', $Model, '--output-format', 'json',
            '--permission-mode', 'acceptEdits', '--allowedTools'
        ) + $allowedTools
        foreach ($argument in $arguments) { $startInfo.ArgumentList.Add($argument) }

        $process = [System.Diagnostics.Process]::Start($startInfo)

        # Start draining both streams before waiting: a full pipe buffer would deadlock a session
        # that runs for an hour and prints as it goes.
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()

        # The prompt goes on stdin because --allowedTools is variadic and would otherwise swallow
        # a trailing prompt argument.
        $process.StandardInput.Write("Invoke the port-slice skill.`n")
        $process.StandardInput.Close()

        if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
            # A force kill here is the last resort, not the first move, and it is loud about it.
            # Killing a session that has already committed would discard a slice that actually
            # landed: the commit is the slice (the port-slice skill), so a session racing the
            # deadline may well have committed in the seconds before this fired, and the rollback
            # in the caller resets to $HeadBefore. Grace is short because the session is not
            # cooperating with a shutdown - nothing here can ask it to stop, so this only avoids
            # losing work that is already safe.
            Write-Host "  the session has run past $TimeoutMinutes minutes; checking whether it committed before killing it" -ForegroundColor Yellow
            $headNow = (git -C $repoRoot rev-parse HEAD).Trim()
            if ($headNow -ne $HeadBefore) {
                Write-Host "  it committed $headNow at the deadline - giving it 60s to finish and exit on its own" -ForegroundColor Yellow
                $null = $process.WaitForExit(60 * 1000)
            }
            if (-not $process.HasExited) {
                Write-Host "  FORCE KILLING the session tree now" -ForegroundColor Red
                $process.Kill($true)
            }
            return [pscustomobject]@{
                Ok = $false; Reason = "the session exceeded $TimeoutMinutes minutes and was killed"
                TotalTokens = 0; CostUsd = 0
            }
        }

        $raw = $stdout.GetAwaiter().GetResult()
        if (-not $raw) {
            $error = $stderr.GetAwaiter().GetResult()
            return [pscustomobject]@{
                Ok = $false; Reason = "the session produced no output: $error"; TotalTokens = 0; CostUsd = 0
            }
        }

        try {
            $response = $raw | ConvertFrom-Json
        }
        catch {
            $head = $raw.Substring(0, [Math]::Min(200, $raw.Length))
            return [pscustomobject]@{
                Ok = $false; Reason = "the session output was not JSON: $head"; TotalTokens = 0; CostUsd = 0
            }
        }

        $tokens = 0
        $usage = $response.PSObject.Properties['usage']?.Value
        if ($usage) {
            foreach ($field in 'input_tokens', 'output_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens') {
                $value = $usage.PSObject.Properties[$field]?.Value
                if ($value) { $tokens += [long]$value }
            }
        }

        $denials = @($response.PSObject.Properties['permission_denials']?.Value)
        if ($denials.Count -gt 0) {
            # Worth surfacing: a slice that stalled on the allowlist is a driver configuration
            # problem, not a porting problem, and the fix is a human decision.
            Write-Host "  permission denials: $($denials.Count) - check the allowlist in this script" -ForegroundColor Yellow

            # And say WHICH, because the count alone cannot be acted on. Measured 2026-09-01: S26
            # reported 25 denials, and by the time anyone looked the JSON response was gone - it is
            # never written to the log, and the session transcript does not record denials either.
            # So the count was unactionable and the allowlist could only have been widened by
            # guessing, which is how a safety boundary quietly stops being one.
            $denials |
                ForEach-Object {
                    # No '?.' on a bare variable here: PowerShell parses `$x?.` as a variable named
                    # 'x?', which under Set-StrictMode throws rather than returning null. Measured
                    # against a synthetic response, 2026-09-01.
                    $name = $_.PSObject.Properties['tool_name']?.Value
                    $toolInput = $_.PSObject.Properties['tool_input']?.Value
                    $detail = $null
                    if ($null -ne $toolInput) {
                        $detail = $toolInput.PSObject.Properties['command']?.Value
                        if (-not $detail) { $detail = $toolInput.PSObject.Properties['file_path']?.Value }
                    }
                    if ($detail) { "$name($detail)" } else { "$name" }
                } |
                Group-Object |
                Sort-Object Count -Descending |
                Select-Object -First 15 |
                ForEach-Object {
                    # Truncated: a denied command can be a whole script, and this goes in a log a
                    # human skims.
                    $shown = $_.Name.Substring(0, [Math]::Min(120, $_.Name.Length))
                    Write-Host "      x$($_.Count)  $shown" -ForegroundColor DarkYellow
                }
        }

        return [pscustomobject]@{
            Ok          = (-not $response.is_error) -and ($response.subtype -eq 'success')
            Reason      = if ($response.is_error) { "the session reported an error: $($response.result)" } else { $null }
            TotalTokens = $tokens
            CostUsd     = $response.PSObject.Properties['total_cost_usd']?.Value
            SessionId   = $response.session_id
        }
    }
    finally {
        if ($process -and -not $process.HasExited) { $process.Kill($true) }
    }
}

function Test-SliceLanded {
    <#
        Gathers the facts, then asks Get-SliceFailureReason (in PortTools.psm1, where it is
        tested) for the verdict. Returns the failure reason, or nothing if the slice landed.
    #>
    param([string]$HeadBefore, [string]$SliceName)

    $git = Get-GitState

    # check-ratchet.ps1 THROWS rather than exiting non-zero when there is no test report, which
    # is exactly what a session that commits non-compiling code leaves behind. Unhandled, that
    # would kill the driver right here - before the slice could be logged, rolled back or parked,
    # so the two-failure rule would never fire.
    $ratchetError = $null
    try {
        & (Join-Path $PSScriptRoot 'check-ratchet.ps1') | Out-Host
        if ($LASTEXITCODE -ne 0) { $ratchetError = 'the parity ratchet is red' }
    }
    catch {
        $ratchetError = "the parity ratchet could not run: $($_.Exception.Message)"
    }

    return Get-SliceFailureReason `
        -HeadBefore $HeadBefore `
        -HeadAfter $git.Head `
        -IsClean $git.IsClean `
        -SliceStillPending (Test-Path -LiteralPath (Join-Path $slicesDir $SliceName)) `
        -RatchetError $ratchetError
}

function Add-ParkNote {
    param([string]$SliceName, [string]$Reason, [object]$Rescue)

    $note = @(
        ''
        "## PARKED $(Get-Date -Format 'yyyy-MM-dd HH:mm') - needs a human"
        ''
        "Slice $SliceName failed twice. Last reason: $Reason"
    )

    # The rescued work is the first thing whoever picks this up will want, and the recovery
    # commands are exactly the ones nobody remembers under pressure. Spell them out here rather
    # than leaving them in console output that has long since scrolled away.
    if ($Rescue.BranchName) {
        $note += @(
            ''
            "The last attempt's uncommitted work is on branch ``$($Rescue.BranchName)``:"
            ''
            "    git stash apply $($Rescue.BranchName)     # restores it into the working tree"
            "    git show $($Rescue.BranchName)            # or just look at it first"
        )
    }
    if ($Rescue.AbandonedSha) {
        $note += @(
            ''
            "It also made a commit, discarded by the rollback but still reachable as ``$($Rescue.AbandonedSha)``:"
            ''
            "    git show $($Rescue.AbandonedSha)"
            "    git cherry-pick $($Rescue.AbandonedSha)"
        )
    }

    $note += @(
        ''
        'The driver has stopped. Investigate, then either fix the slice and restart the driver,'
        'or run the slice interactively (escalating to a stronger model if it has already failed'
        'twice on Opus).'
    )
    Add-Content -LiteralPath $statePath -Value ($note -join "`n") -Encoding utf8
}

# ---------------------------------------------------------------------------------------------

# Read inside the loop, not here: budget.json says editing it takes effect immediately, and that
# is only true if the driver re-reads it every time round. $null seeds the first read, which is
# the one Read-Budget refuses to paper over.
$budget = $null
$startingPhase = $null
$completed = 0
$consecutiveFailures = 0
$checkpoints = 0

while ($completed -lt $MaxSlices) {
    $slice = Get-PendingSlice
    if (-not $slice) {
        Write-Host 'Stopping: no pending slices left. This is a phase boundary - author the next phase and restart.' -ForegroundColor Cyan
        break
    }

    $phase = Get-SlicePhase -SliceFile $slice
    if ($null -eq $startingPhase) { $startingPhase = $phase }
    if ($phase -ne $startingPhase) {
        Write-Host "Stopping: $($slice.Name) belongs to phase $phase and this run started on phase $startingPhase. Phase boundaries are a human checkpoint." -ForegroundColor Cyan
        break
    }

    $budget = Read-Budget -Path $budgetPath -LastGood $budget

    $verdict = Test-BudgetGate -Budget $budget -SliceLogPath $sliceLogPath `
        -TokensLastDay (Get-SessionTokenUsage -LogRoot $sessionLogRoot -Since ([datetime]::UtcNow.AddDays(-1))) `
        -TokensLastWeek (Get-SessionTokenUsage -LogRoot $sessionLogRoot -Since ([datetime]::UtcNow.AddDays(-7))) `
        -RateLimitResetsAt (Get-RateLimitResetsAt -LogRoot $sessionLogRoot)

    Write-Host ''
    Write-Host "Next slice: $($slice.Name) (phase $phase)" -ForegroundColor Cyan
    Write-Host ("  budget: {0} slices today, {1} this week; {2:N0} tokens today, {3:N0} this week" -f `
            $verdict.SlicesToday, $verdict.SlicesThisWeek, $verdict.TokensLastDay, $verdict.TokensLastWeek) -ForegroundColor DarkGray

    if (-not $verdict.Allowed) {
        Write-Host "Stopping: budget gate says no - $($verdict.Reason)." -ForegroundColor Yellow
        break
    }

    if ($DryRun) {
        Write-Host 'Dry run: would start a slice session here.' -ForegroundColor DarkGray
        break
    }

    $headBefore = (Get-GitState).Head
    Write-Host "  running $Model session..." -ForegroundColor DarkGray
    $session = Invoke-SliceSession -TimeoutMinutes $budget.sliceTimeoutMinutes -HeadBefore $headBefore

    $failureReason = if (-not $session.Ok) { $session.Reason } else { Test-SliceLanded -HeadBefore $headBefore -SliceName $slice.Name }

    if (-not $failureReason) {
        Write-SliceLogEntry -Path $sliceLogPath -Slice $slice.BaseName -Outcome 'completed' -TotalTokens $session.TotalTokens
        $completed++
        $consecutiveFailures = 0
        Write-Host ("  done: {0} ({1:N0} tokens, `${2:N2})" -f $slice.BaseName, $session.TotalTokens, $session.CostUsd) -ForegroundColor Green
        continue
    }

    # A green commit with the slice file still pending: the session needed more than one sitting
    # and said so in STATE.md. Keep the commit, count nothing against the slice, and let the loop
    # start a fresh session on the same slice. Bounded, so a slice that checkpoints without ever
    # landing stops the driver rather than spending the whole daily budget on itself.
    if ($failureReason -eq 'checkpoint') {
        $checkpoints++
        $consecutiveFailures = 0
        Write-SliceLogEntry -Path $sliceLogPath -Slice $slice.BaseName -Outcome 'checkpoint' -TotalTokens $session.TotalTokens
        Write-Host ("  CHECKPOINT ({0} of 3): {1} committed green but is still open - a fresh session continues it ({2:N0} tokens)" -f $checkpoints, $slice.BaseName, $session.TotalTokens) -ForegroundColor Yellow
        if ($checkpoints -ge 3) {
            Write-Host "Stopping: $($slice.BaseName) has checkpointed three times without landing. Read its STATE.md notes and decide whether to split it." -ForegroundColor Red
            break
        }
        continue
    }

    # A session killed by the account's usage limit is not a broken slice, and counting it as one
    # parks a slice nobody has any reason to investigate. Get-RateLimitResetsAt only reports a
    # reset that a request actually hit and that is still in the future, so its presence here is
    # the classification.
    $rateLimitResetsAt = Get-RateLimitResetsAt -LogRoot $sessionLogRoot

    if ($rateLimitResetsAt) {
        Write-Host "  RATE-LIMITED, not failed: the account allowance ran out mid-slice, resetting at $($rateLimitResetsAt.ToString('u'))" -ForegroundColor Magenta
    }
    else {
        $consecutiveFailures++
        Write-Host "  FAILED ($consecutiveFailures of 2): $failureReason" -ForegroundColor Red
    }

    # Roll back before logging: the rollback restores tracked files to $headBefore, and a park
    # note written before it would be reverted by it.
    # Roll back to the last GREEN commit the session made, not to where it started: a session that
    # committed a green checkpoint and then left dirty work must keep the checkpoint. The ratchet
    # is re-run on the clean HEAD after the stash to decide. Added 2026-09-13 (DECISIONS).
    $rescue = Undo-FailedSlice -RepoRoot $repoRoot -HeadBefore $headBefore -SliceName $slice.BaseName -KeepHeadIfGreen {
        & (Join-Path $PSScriptRoot 'check-ratchet.ps1') | Out-Host
        $LASTEXITCODE -eq 0
    }
    if ($rescue.KeptHead) {
        Write-Host "  kept the session's green commit $($rescue.KeptHead) - only the uncommitted remainder was rolled back" -ForegroundColor Yellow
    }
    if ($rescue.BranchName) {
        Write-Host "  work rescued onto branch $($rescue.BranchName) - restore with: git stash apply $($rescue.BranchName)" -ForegroundColor DarkGray
    }
    if ($rescue.AbandonedSha) {
        Write-Host "  its commit $($rescue.AbandonedSha) was rolled back - inspect with: git show $($rescue.AbandonedSha)" -ForegroundColor DarkGray
    }

    if ($rateLimitResetsAt) {
        Write-SliceLogEntry -Path $sliceLogPath -Slice $slice.BaseName -Outcome 'rate-limited' `
            -TotalTokens $session.TotalTokens -Rescue $rescue
        # No sleep. The budget gate at the top of the next iteration refuses to start while the
        # reset is in the future, so the driver stops there of its own accord and says why.
        continue
    }

    if ($consecutiveFailures -ge 2) {
        Write-SliceLogEntry -Path $sliceLogPath -Slice $slice.BaseName -Outcome 'parked' `
            -TotalTokens $session.TotalTokens -Rescue $rescue
        Add-ParkNote -SliceName $slice.BaseName -Reason $failureReason -Rescue $rescue
        Write-Host "Stopping: $($slice.BaseName) failed twice and has been parked. See docs/plan/STATE.md." -ForegroundColor Red
        break
    }

    Write-SliceLogEntry -Path $sliceLogPath -Slice $slice.BaseName -Outcome 'failed' `
        -TotalTokens $session.TotalTokens -Rescue $rescue
}

Write-Host ''
Write-Host "Driver finished. Slices completed this run: $completed." -ForegroundColor Cyan
