<#
.SYNOPSIS
    Launches one slice of the port, detached, and prints the PID.

.DESCRIPTION
    The driver must NOT be started as an agent's background tool task. Measured 2026-08-31: such a
    task was reaped by the harness 42 minutes in, mid-slice, with the slice session healthy. The
    driver never reached its own rollback, so no rescue branch was written and the work was left
    loose in the working tree. Start-Process removes the dependency on the harness entirely.

    Pair it with tools/heartbeat.sh, armed in the SAME turn - see docs/plan/OPERATIONS.md.

    Tracked rather than left in .scratch/ because .scratch is cleared by slice sessions: this
    script was recreated twice and lost again mid-run on 2026-09-01, which is what promoted it.

.EXAMPLE
    pwsh -File tools/launch-slice.ps1 s25
    pwsh -File tools/launch-slice.ps1 s58 -Phase 7   # from a worktree: only that phase's slices
    pwsh -File tools/launch-slice.ps1 s55 -Model sonnet   # a mechanical slice on the cheaper model
    pwsh -File tools/launch-slice.ps1 s57e -StopBy 07:30  # overnight: end before the owner leaves
#>
param(
    [Parameter(Mandatory)][string]$Tag,
    [int]$Phase = 0,
    # Which slice to run. The tag by default, because a tag has always been a slice id and the
    # driver otherwise takes the lowest-numbered pending slice in the phase - which on 2026-09-21
    # started S68 for a launch tagged s80. Pass '' to get that behaviour back deliberately.
    [string]$Slice = $Tag,
    [ValidateSet('opus', 'sonnet', 'fable')][string]$Model = 'opus',
    # Passed straight through to run-slices.ps1: be finished by this time of day, and start no
    # sitting too short to reach a commit.
    [string]$StopBy = ''
)

$repo = Split-Path -Parent $PSScriptRoot
$log = Join-Path $repo ".scratch/driver-$Tag.log"
$err = Join-Path $repo ".scratch/driver-$Tag.err"

New-Item -ItemType Directory -Force -Path (Join-Path $repo '.scratch') | Out-Null

# The driver's allowance gate is only as good as tools/usage-poll.ps1, and nothing restarts that.
# It died at some point on the night of 2026-09-21 and the 09:38 sitting the next morning started
# blind - "allowance unknown (no allowance snapshot); starting anyway" - which is the gate not
# gating. Start one here if none is running, so launching a slice cannot leave it unwatched.
# Matching on '-File ... usage-poll.ps1' rather than the bare name, and skipping this process,
# because a query whose own command line names the script matches itself: measured 2026-09-22,
# twice - a process count of "1 driver running" and then of "2 pollers", both of them the query.
$poller = @(Get-CimInstance Win32_Process -Filter "Name = 'pwsh.exe'" |
    Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -like '*-File*usage-poll.ps1*' })
if (-not $poller) {
    $pollProc = Start-Process -FilePath 'pwsh' `
        -ArgumentList '-NoProfile', '-File', (Join-Path $repo 'tools/usage-poll.ps1') `
        -WorkingDirectory $repo -WindowStyle Hidden -PassThru
    Write-Output "POLLER_PID=$($pollProc.Id) (none was running)"
}
else {
    Write-Output "poller already running (PID $($poller[0].ProcessId))"
}

Import-Module (Join-Path $repo 'tools/PortTools.psm1') -Force

$proc = Start-Process -FilePath 'pwsh' `
    -ArgumentList (Resolve-DriverArguments -DriverPath (Join-Path $repo 'tools/run-slices.ps1') `
        -Phase $Phase -Slice $Slice -Model $Model -StopBy $StopBy) `
    -WorkingDirectory $repo `
    -RedirectStandardOutput $log `
    -RedirectStandardError $err `
    -WindowStyle Hidden -PassThru

Write-Output "DETACHED_PID=$($proc.Id) log=.scratch/driver-$Tag.log"
