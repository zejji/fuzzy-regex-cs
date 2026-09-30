<#
.SYNOPSIS
    Launches tools/matrix/run.py detached, with its output in a log, and returns at once.

.DESCRIPTION
    The matrix waves run for hours in chunks of under 45 minutes (run.py stops itself after
    -MaxMinutes and resumes where it stopped when launched again). They must not run as a tool's
    background task, which a harness can kill mid-chunk, so this starts a hidden process and writes
    its PID next to the log for polling and for stopping. Stop it with
    `taskkill //PID <pid> //T //F`: /T takes the dotnet and python children with it.

.EXAMPLE
    pwsh -File tools/matrix/launch.ps1 -Rows .scratch/matrix/pilot.jsonl -Run pilot
#>
param(
    [Parameter(Mandatory)][string]$Rows,
    [Parameter(Mandatory)][string]$Run,
    [int]$Chunk = 250,
    [double]$MaxMinutes = 40,
    [string]$Constructs = ''
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '../..')
$out = Join-Path $repo ".scratch/matrix/results/$Run"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$log = Join-Path $out 'run.log'
$err = Join-Path $out 'run.err.log'
$runArgs = @((Join-Path $PSScriptRoot 'run.py'), (Resolve-Path $Rows), '--run', $Run, '--chunk', $Chunk,
    '--max-minutes', $MaxMinutes)
if ($Constructs) { $runArgs += @('--constructs', (Resolve-Path $Constructs)) }
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:PYTHONUNBUFFERED = '1'
$p = Start-Process -FilePath 'python' -ArgumentList $runArgs -WorkingDirectory $repo -WindowStyle Hidden `
    -RedirectStandardOutput $log -RedirectStandardError $err -PassThru
Set-Content -Path (Join-Path $out 'run.pid') -Value $p.Id
Write-Output "launched run.py for '$Run' as PID $($p.Id); log $log"
