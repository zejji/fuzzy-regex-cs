<#
.SYNOPSIS
    Polls the account's live rate-limit usage and writes it where the driver, the session hook and
    tools/allowance.ps1 read it. No LLM call. Runs forever; launch detached and leave it.

.DESCRIPTION
    The statusline snapshot (~/.claude/last-status.json) is only as fresh as the last interactive
    prompt, which overnight can be half an hour old while an Opus sitting burns several percent a
    minute. This script asks the same source Claude Code itself uses. Measured 2026-09-18 21:50 with a
    real call: GET https://api.anthropic.com/api/oauth/usage with the OAuth access token from
    ~/.claude/.credentials.json (claudeAiOauth.accessToken) and header anthropic-beta: oauth-2025-04-20
    returns JSON whose five_hour and seven_day objects carry `utilization` (85.0 when the statusline
    showed 85%) and `resets_at` as an ISO-8601 string with offset. The token is re-read from the
    credentials file on every poll because Claude Code refreshes it (expiresAt was 00:39 next day at
    the time of measurement); it is never printed or logged.

    Output file ~/.claude/last-usage.json has the SAME shape as the statusline snapshot's rate_limits
    (used_percentage, resets_at in Unix seconds), so every reader takes whichever file is newer.

.EXAMPLE
    Start-Process pwsh -ArgumentList '-NoProfile','-File','C:/.../tools/usage-poll.ps1' -WindowStyle Hidden
#>
param(
    [int]$IntervalSeconds = 180,
    [string]$Out = (Join-Path $env:USERPROFILE '.claude\last-usage.json'),
    [string]$Log = (Join-Path $env:USERPROFILE '.claude\usage-poll.log')
)
$ErrorActionPreference = 'Stop'
$credentials = Join-Path $env:USERPROFILE '.claude\.credentials.json'

function Get-UsageOnce {
    $token = (Get-Content -LiteralPath $credentials -Raw | ConvertFrom-Json).claudeAiOauth.accessToken
    if (-not $token) { throw 'no OAuth access token in the credentials file' }
    $r = Invoke-RestMethod -Uri 'https://api.anthropic.com/api/oauth/usage' -TimeoutSec 30 `
        -Headers @{ Authorization = "Bearer $token"; 'anthropic-beta' = 'oauth-2025-04-20' }
    $window = {
        param($w)
        if (-not $w) { return $null }
        [ordered]@{
            used_percentage = [int][Math]::Round([double]$w.utilization)
            # Invoke-RestMethod already turns the ISO string into a [DateTime] (observed 2026-09-18);
            # accept either form.
            resets_at       = if ($w.resets_at -is [DateTime]) { [DateTimeOffset]::new($w.resets_at).ToUnixTimeSeconds() } else { [DateTimeOffset]::Parse([string]$w.resets_at).ToUnixTimeSeconds() }
        }
    }
    [ordered]@{
        polled_at   = (Get-Date).ToString('o')
        rate_limits = [ordered]@{ five_hour = (& $window $r.five_hour); seven_day = (& $window $r.seven_day) }
    }
}

while ($true) {
    try {
        $u = Get-UsageOnce
        $tmp = "$Out.tmp"
        $u | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $tmp -NoNewline
        Move-Item -LiteralPath $tmp -Destination $Out -Force
        "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') 5h=$($u.rate_limits.five_hour.used_percentage)% 7d=$($u.rate_limits.seven_day.used_percentage)%" | Add-Content -LiteralPath $Log
    } catch {
        "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') poll failed: $($_.Exception.Message)" | Add-Content -LiteralPath $Log
    }
    Start-Sleep -Seconds $IntervalSeconds
}
