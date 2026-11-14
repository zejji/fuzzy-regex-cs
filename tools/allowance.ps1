<#
.SYNOPSIS
    Prints the Claude subscription allowance left for the account Claude Code is signed in as, and
    exits 1 when a launch should not happen. No LLM call.

.DESCRIPTION
    Sources, best first:
    1. The Claude usage widget's local API (http://127.0.0.1:<WidgetPort>/v1/usage), which polls
       Anthropic every few minutes on its own login and reports how old its figures are. The
       account is the one in ~/.claude.json (oauthAccount.accountUuid).
    2. If the widget is not running but is installed (%LOCALAPPDATA%\Programs\ClaudeUsageWidget),
       start it and wait up to 5 s: it serves its saved figures at once. -NoStart turns this off.
    3. Otherwise the files, as before: the fresher of ~/.claude/last-status.json (rewritten by the
       statusline on every interactive prompt; fields per https://code.claude.com/docs/en/statusline,
       read 2026-09-16) and ~/.claude/last-usage.json (tools/usage-poll.ps1). A hint says why the
       widget was not used.
    A source older than -MaxAgeMinutes is reported but does not block. Floors are percentages used.

.EXAMPLE
    pwsh -File tools/allowance.ps1                       # print
    pwsh -File tools/allowance.ps1 -WeeklyFloor 90 -ExtraFloor 85 ; if ($LASTEXITCODE) { 'do not launch' }
#>
param(
    [int]$FiveHourFloor = 95,
    [int]$WeeklyFloor = 95,
    [int]$ExtraFloor = 90,
    [int]$MaxAgeMinutes = 120,
    [int]$WidgetPort = 47821,
    [switch]$NoStart
)

$fmt = { param($w) if ($w) { $t = $w.resets_at; if ($t -is [long] -or $t -is [int] -or $t -is [double]) { $t = [DateTimeOffset]::FromUnixTimeSeconds([long]$t).ToLocalTime().ToString('ddd HH:mm') }; '{0:N0}% (resets {1})' -f $w.used_percentage, $t } else { 'n/a' } }

function Get-ClaudeCodeAccountUuid {
    $path = Join-Path $env:USERPROFILE '.claude.json'
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $raw = Get-Content -LiteralPath $path -Raw
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        # The file has keys that differ only by case, which ConvertFrom-Json rejects without -AsHashtable.
        $j = $raw | ConvertFrom-Json -AsHashtable
        return $j['oauthAccount']['accountUuid']
    }
    if ($raw -match '"oauthAccount"\s*:\s*\{[^}]*?"accountUuid"\s*:\s*"([^"]+)"') { return $Matches[1] }
    return $null
}

# Returns the widget's entry for the account, $null if the widget does not answer, or a string
# explaining why it cannot be used.
function Get-WidgetAccount([string]$uuid) {
    $url = "http://127.0.0.1:$WidgetPort/v1/usage?account=$uuid"
    try {
        $r = Invoke-WebRequest -Uri $url -TimeoutSec 2 -UseBasicParsing -ErrorAction Stop
        $a = ($r.Content | ConvertFrom-Json).accounts[0]
        if (-not $a.fetched_at) { return "the widget has no figures yet for this account (status: $($a.status))" }
        return $a
    } catch {
        $status = $_.Exception.Response.StatusCode
        if ($status -and [int]$status -eq 404) { return 'this Claude Code account is not added to the widget (right-click the widget > Add account)' }
        return $null
    }
}

$uuid = Get-ClaudeCodeAccountUuid
$hint = $null
$widget = $null
if (-not $uuid) {
    $hint = 'no signed-in Claude Code account found in ~/.claude.json'
} else {
    $widget = Get-WidgetAccount $uuid
    if ($null -eq $widget) {
        $exe = Join-Path $env:LOCALAPPDATA 'Programs\ClaudeUsageWidget\usage-widget.exe'
        if ($NoStart) {
            $hint = 'the usage widget is not running'
        } elseif (Test-Path -LiteralPath $exe) {
            Write-Host 'allowance: starting the usage widget' -ForegroundColor DarkGray
            Start-Process -FilePath $exe
            for ($i = 0; $i -lt 10 -and $null -eq $widget; $i++) { Start-Sleep -Milliseconds 500; $widget = Get-WidgetAccount $uuid }
            if ($null -eq $widget) { $hint = 'the usage widget was started but did not answer within 5 s' }
        } else {
            $hint = 'the usage widget is not installed (https://github.com/zejji/claude-usage-widget/releases)'
        }
    }
    if ($widget -is [string]) { $hint = $widget; $widget = $null }
}

if ($widget) {
    $age = [int]([math]::Floor($widget.age_seconds / 60))
    $rl = $widget.rate_limits
    $models = @($rl.scoped | ForEach-Object { '{0} {1}' -f $_.model, (& $fmt $_) }) -join ' | '
    $line = "allowance ({0} min old, widget): 5h {1} | 7d {2}" -f $age, (& $fmt $rl.five_hour), (& $fmt $rl.seven_day)
    if ($models) { $line += " | $models" }
    Write-Host $line
    if ($widget.status -ne 'ok') { Write-Host "  widget status: $($widget.status)" -ForegroundColor Yellow }
    if ($age -gt $MaxAgeMinutes) { Write-Host '  figures are stale; check the widget (it may be rate limited or offline)' -ForegroundColor Yellow }
    $spend = $null
} else {
    # The fresher of the statusline snapshot and tools/usage-poll.ps1's live poll (same shape), among
    # those that hold figures: a statusline snapshot can lack rate_limits (seen 2026-11-12).
    $file = @((Join-Path $env:USERPROFILE '.claude\last-usage.json'), (Join-Path $env:USERPROFILE '.claude\last-status.json')) |
        Where-Object { (Test-Path -LiteralPath $_) -and (Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json).rate_limits } |
        Get-Item | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $file) { Write-Host 'allowance: no statusline snapshot yet (open an interactive session once)'; exit 0 }
    $age = [int]((Get-Date) - (Get-Item -LiteralPath $file).LastWriteTime).TotalMinutes
    $rl = (Get-Content -LiteralPath $file -Raw | ConvertFrom-Json).rate_limits
    $spend = $rl.spend_limit
    # resets_at arrives as Unix seconds (observed 2026-09-16, e.g. 1789585200), not the ISO string the docs show.
    Write-Host ("allowance ({0} min old): 5h {1} | 7d {2} | extra {3}" -f $age, (& $fmt $rl.five_hour), (& $fmt $rl.seven_day), (& $fmt $spend))
    if ($hint) { Write-Host "  not from the usage widget: $hint" -ForegroundColor DarkGray }
    if ($age -gt $MaxAgeMinutes) { Write-Host '  snapshot is stale; ask the owner to press Enter in the interactive session to refresh it' -ForegroundColor Yellow }
}

$block = ($rl.five_hour -and $rl.five_hour.used_percentage -ge $FiveHourFloor) -or
         ($rl.seven_day -and $rl.seven_day.used_percentage -ge $WeeklyFloor) -or
         ($spend -and $spend.used_percentage -ge $ExtraFloor)
if ($block) { Write-Host '  at or above a floor: do not launch' -ForegroundColor Red; exit 1 }
exit 0
