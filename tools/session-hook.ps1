<#
.SYNOPSIS
    PreToolUse hook for unattended slice sessions: tells the session how long it has left before the
    driver kills it, and relays a one-shot message from the orchestrator.
.DESCRIPTION
    A `claude -p` session cannot be messaged, and it does not know the driver's deadline. This hook
    runs on every tool call the session makes and returns `additionalContext` the model sees on its
    next step. Two inputs, both written by the driver or the orchestrator into .scratch/:
      .claude/driver/session-deadline.txt      ISO-8601 deadline, written by run-slices.ps1 when a sitting
                                               starts (not .scratch/, which slice sessions clear).
      orchestrator-message.txt  free text; injected once, then renamed .delivered so it is not
                                repeated.
    Silent unless FUZZY_SLICE_SESSION=1 is in the environment, which only the driver sets, so an
    interactive session in this repo is never nagged and never consumes a message meant for a slice.
    Reminders start at 30 minutes out and are worded to make the session checkpoint under 25.
    Added 2026-09-13 after S43's second sitting ran to the wire without knowing it (DECISIONS).
#>
$ErrorActionPreference = 'SilentlyContinue'
if ($env:FUZZY_SLICE_SESSION -ne '1') { exit 0 }
$null = [Console]::In.ReadToEnd()
$repo = Split-Path -Parent $PSScriptRoot
$parts = New-Object System.Collections.Generic.List[string]

$deadlineFile = Join-Path $repo '.claude/driver/session-deadline.txt'
if (Test-Path -LiteralPath $deadlineFile) {
    $deadline = [datetimeoffset]::Parse((Get-Content -LiteralPath $deadlineFile -Raw).Trim())
    $left = [int][Math]::Floor(($deadline - [datetimeoffset]::Now).TotalMinutes)
    if ($left -le 30) {
        $urgency = if ($left -le 15) { 'COMMIT A CHECKPOINT NOW' } else { 'plan to checkpoint under 25 minutes' }
        $parts.Add("[driver deadline] $left minutes until the driver kills this session at $($deadline.ToString('HH:mm')). $urgency - a green commit with STATE.md saying what is left is kept; uncommitted work is stashed and costs a recovery sitting. The pre-commit inspection takes about four minutes; reserve it.")
    }
}

# Allowance, same rule as the driver's gate (owner 2026-09-18: never exhaust the five-hour window).
# The hook is a plain script, so it reads the two snapshot files itself rather than importing the module.
$usage = @((Join-Path $env:USERPROFILE '.claude\last-usage.json'), (Join-Path $env:USERPROFILE '.claude\last-status.json')) |
    Where-Object { Test-Path -LiteralPath $_ } | Get-Item | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($usage -and ((Get-Date) - $usage.LastWriteTime).TotalMinutes -le 45) {
    $limits = (Get-Content -LiteralPath $usage.FullName -Raw | ConvertFrom-Json).rate_limits
    $five = $limits.five_hour
    if ($five -and [int]$five.used_percentage -ge 92) {
        $resets = [DateTimeOffset]::FromUnixTimeSeconds([long]$five.resets_at).ToLocalTime().ToString('HH:mm')
        $parts.Add("[allowance] the account's five-hour window is at $($five.used_percentage)% (resets $resets). COMMIT A GREEN CHECKPOINT NOW and end the session; the driver waits for the reset and a fresh sitting continues. Do not start new work.")
    }
    # The weekly window has no early reset; at 100% every session dies mid-work until the reset day.
    # Owner 2026-09-19: spend the week to the end but lose as little as possible, so order the
    # checkpoint at 98%, the same mark the gate uses: a sitting that starts below it gets its run, one at it never starts.
    $seven = $limits.seven_day
    if ($seven -and [int]$seven.used_percentage -ge 98) {
        $resets = [DateTimeOffset]::FromUnixTimeSeconds([long]$seven.resets_at).ToLocalTime().ToString('ddd HH:mm')
        $parts.Add("[allowance] the account's SEVEN-DAY window is at $($seven.used_percentage)% (resets $resets). COMMIT A GREEN CHECKPOINT NOW and end the session; at 100% every session stops mid-work until the reset. Do not start new work.")
    }
}

$messageFile = Join-Path $repo '.claude/driver/orchestrator-message.txt'
if (Test-Path -LiteralPath $messageFile) {
    $text = (Get-Content -LiteralPath $messageFile -Raw).Trim()
    if ($text) { $parts.Add("[message from the orchestrator] $text") }
    Move-Item -LiteralPath $messageFile -Destination ($messageFile + '.delivered') -Force
}

if ($parts.Count -eq 0) { exit 0 }
@{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; additionalContext = ($parts -join "`n") } } | ConvertTo-Json -Compress
