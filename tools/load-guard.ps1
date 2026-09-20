<#
.SYNOPSIS
    Runs forever, samples total CPU load, logs sustained high load and kills Stryker runs before
    the machine falls over.
.DESCRIPTION
    Owner rule, 2026-09-17: a Stryker queue at four runners held the machine at 100% for minutes
    and the 06:24 crash is suspected to be that. This guard is the independent watcher: every
    -SampleSeconds it reads the processor load; when the load has been at or above -WarnPercent
    for -WarnSeconds it writes a WARN line; when it has been at or above -KillPercent for
    -KillSeconds it stops every process whose command line mentions stryker (the runner, the
    Stryker CLI and its test hosts) and writes a KILLED line. Every -SummaryMinutes it writes the
    period's average and peak so the log doubles as a load record. Start it detached
    (Start-Process pwsh -File tools/load-guard.ps1) and tail the log for WARN|KILLED.
.EXAMPLE
    pwsh -File tools/load-guard.ps1 -Log .scratch/load-guard.log
#>
param(
    [int]$WarnPercent = 85,
    [int]$WarnSeconds = 60,
    [int]$KillPercent = 95,
    [int]$KillSeconds = 180,
    [int]$SampleSeconds = 5,
    [int]$SummaryMinutes = 5,
    [string]$Log = '.scratch/load-guard.log'
)
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path (Split-Path $Log) | Out-Null
function Write-Log([string]$line) { "$(Get-Date -Format 'HH:mm:ss') $line" | Add-Content -Path $Log }
Write-Log "guard started: warn >=$WarnPercent% for ${WarnSeconds}s, kill stryker >=$KillPercent% for ${KillSeconds}s, sample every ${SampleSeconds}s"
$warnRun = 0; $killRun = 0; $warned = $false
$period = @(); $periodStart = Get-Date
while ($true) {
    $load = [int](Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
    $period += $load
    if ($load -ge $WarnPercent) { $warnRun += $SampleSeconds } else { $warnRun = 0; $warned = $false }
    if ($load -ge $KillPercent) { $killRun += $SampleSeconds } else { $killRun = 0 }
    if ($warnRun -ge $WarnSeconds -and -not $warned) { Write-Log "WARN load >=$WarnPercent% for ${warnRun}s (now $load%)"; $warned = $true }
    if ($killRun -ge $KillSeconds) {
        $victims = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match 'stryker' -and $_.CommandLine -notmatch 'load-guard|night-shift|after-parsing|pause-stryker' })
        foreach ($v in $victims) { try { Stop-Process -Id $v.ProcessId -Force -ErrorAction Stop } catch {} }
        Write-Log "KILLED $($victims.Count) stryker processes: load >=$KillPercent% for ${killRun}s"
        $killRun = 0
    }
    if (((Get-Date) - $periodStart).TotalMinutes -ge $SummaryMinutes) {
        $avg = [int](($period | Measure-Object -Average).Average); $max = ($period | Measure-Object -Maximum).Maximum
        Write-Log "summary avg=$avg% peak=$max% over $SummaryMinutes min"
        $period = @(); $periodStart = Get-Date
    }
    Start-Sleep -Seconds $SampleSeconds
}
