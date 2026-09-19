<#
.SYNOPSIS
    Records what else was on the machine while a benchmark run was in flight.

.DESCRIPTION
    A noise floor is only a floor if the two runs it comes from saw the same machine. S58 learned
    that the hard way: the load guard's five-minute averages read 1-2% across a run whose middle
    third was 1.4-2.4x slow, because a five-minute mean over 20 cores cannot see one core taken for
    ninety seconds. This samples often enough to catch that, and it names the process rather than
    reporting a percentage nobody can act on.

    Deliberately cheap: one Get-Process pass per interval, no counters, no WMI. Measured cost is
    below 0.2 s of CPU per sample, so a 30-second interval is under 0.7% of one core of a 20-core
    machine and does not move a benchmark it is watching.

.PARAMETER LogPath
    Where to write. Appends, so a run's log survives a restart of the sampler.

.PARAMETER IntervalSeconds
    Seconds between samples. 30 by default.

.PARAMETER Minutes
    How long to sample for. The sampler exits on its own so a forgotten one cannot outlive the run.

.EXAMPLE
    pwsh -File tools/probes/sample-machine-load.ps1 -LogPath .scratch/load.log -Minutes 45
#>
[CmdletBinding()]
param(
    [string] $LogPath = '.scratch/machine-load.log',
    [int] $IntervalSeconds = 30,
    [int] $Minutes = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$deadline = (Get-Date).AddMinutes($Minutes)
$previous = @{}

"# sampler started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), every ${IntervalSeconds}s for ${Minutes}m" |
    Add-Content -Path $LogPath

while ((Get-Date) -lt $deadline) {
    $stamp = Get-Date
    $current = @{}
    $deltas = foreach ($p in Get-Process) {
        $cpu = $null
        try { $cpu = $p.CPU } catch { continue }
        if ($null -eq $cpu) { continue }

        $key = '{0}:{1}' -f $p.Id, $p.ProcessName
        $current[$key] = $cpu
        if ($previous.ContainsKey($key)) {
            $delta = $cpu - $previous[$key]
            if ($delta -gt 0.5) {
                [pscustomobject]@{ Name = $p.ProcessName; Id = $p.Id; Seconds = $delta }
            }
        }
    }

    if ($previous.Count -gt 0) {
        $top = $deltas | Sort-Object Seconds -Descending | Select-Object -First 5
        $busy = ($top | ForEach-Object { '{0}({1}) {2:N1}s' -f $_.Name, $_.Id, $_.Seconds }) -join ', '
        # An interval where nothing moved yields no objects at all, and under StrictMode
        # Measure-Object then has no .Sum to read - hence the count check rather than a null test.
        $totalCpu = 0
        if (@($deltas).Count -gt 0) {
            $totalCpu = ($deltas | Measure-Object -Property Seconds -Sum).Sum
        }
        $line = '{0} busy={1:N1}s/{2}s {3}' -f $stamp.ToString('HH:mm:ss'), $totalCpu, $IntervalSeconds,
            $(if ($busy) { $busy } else { 'idle' })
        Add-Content -Path $LogPath -Value $line
    }

    $previous = $current
    Start-Sleep -Seconds $IntervalSeconds
}

"# sampler finished $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" | Add-Content -Path $LogPath
