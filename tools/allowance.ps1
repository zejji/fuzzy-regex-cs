<#
.SYNOPSIS
    Prints the subscription allowance the statusline last saw, and exits 1 when a launch should
    not happen. No LLM call, no network: it reads ~/.claude/last-status.json, which the user
    statusline script (~/.claude/statusline/status.ps1) rewrites on every interactive update.

.DESCRIPTION
    Fields per https://code.claude.com/docs/en/statusline (read 2026-09-16). The file is only as
    fresh as the last interactive prompt, so the age is printed and a stale file (older than
    -MaxAgeMinutes) is reported but does not block. Floors are percentages used.

.EXAMPLE
    pwsh -File tools/allowance.ps1                       # print
    pwsh -File tools/allowance.ps1 -WeeklyFloor 90 -ExtraFloor 85 ; if ($LASTEXITCODE) { 'do not launch' }
#>
param(
    [int]$FiveHourFloor = 95,
    [int]$WeeklyFloor = 95,
    [int]$ExtraFloor = 90,
    [int]$MaxAgeMinutes = 120
)
$file = Join-Path $env:USERPROFILE '.claude\last-status.json'
if (-not (Test-Path -LiteralPath $file)) { Write-Host 'allowance: no statusline snapshot yet (open an interactive session once)'; exit 0 }
$age = [int]((Get-Date) - (Get-Item -LiteralPath $file).LastWriteTime).TotalMinutes
$d = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
$rl = $d.rate_limits
$fmt = { param($w) if ($w) { '{0:N0}% (resets {1})' -f $w.used_percentage, $w.resets_at } else { 'n/a' } }
Write-Host ("allowance ({0} min old): 5h {1} | 7d {2} | extra {3}" -f $age, (& $fmt $rl.five_hour), (& $fmt $rl.seven_day), (& $fmt $rl.spend_limit))
if ($age -gt $MaxAgeMinutes) { Write-Host "  snapshot is stale; ask the owner to press Enter in the interactive session to refresh it" -ForegroundColor Yellow }
$block = ($rl.five_hour -and $rl.five_hour.used_percentage -ge $FiveHourFloor) -or
         ($rl.seven_day -and $rl.seven_day.used_percentage -ge $WeeklyFloor) -or
         ($rl.spend_limit -and $rl.spend_limit.used_percentage -ge $ExtraFloor)
if ($block) { Write-Host '  at or above a floor: do not launch' -ForegroundColor Red; exit 1 }
exit 0
