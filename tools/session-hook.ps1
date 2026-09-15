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

$messageFile = Join-Path $repo '.claude/driver/orchestrator-message.txt'
if (Test-Path -LiteralPath $messageFile) {
    $text = (Get-Content -LiteralPath $messageFile -Raw).Trim()
    if ($text) { $parts.Add("[message from the orchestrator] $text") }
    Move-Item -LiteralPath $messageFile -Destination ($messageFile + '.delivered') -Force
}

if ($parts.Count -eq 0) { exit 0 }
@{ hookSpecificOutput = @{ hookEventName = 'PreToolUse'; additionalContext = ($parts -join "`n") } } | ConvertTo-Json -Compress
