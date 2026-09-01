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
#>
param([Parameter(Mandatory)][string]$Tag)

$repo = Split-Path -Parent $PSScriptRoot
$log = Join-Path $repo ".scratch/driver-$Tag.log"
$err = Join-Path $repo ".scratch/driver-$Tag.err"

New-Item -ItemType Directory -Force -Path (Join-Path $repo '.scratch') | Out-Null

$proc = Start-Process -FilePath 'pwsh' `
    -ArgumentList '-NoProfile', '-File', (Join-Path $repo 'tools/run-slices.ps1'), '-MaxSlices', '1' `
    -WorkingDirectory $repo `
    -RedirectStandardOutput $log `
    -RedirectStandardError $err `
    -WindowStyle Hidden -PassThru

Write-Output "DETACHED_PID=$($proc.Id) log=.scratch/driver-$Tag.log"
