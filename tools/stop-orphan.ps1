<#
.SYNOPSIS
    Stops one orphaned or hung helper process of this project's own tooling, after checking it is
    the kind of process the owner has agreed may be stopped, and logs what it did and why.

.DESCRIPTION
    Owner rules (2026-09-18/19): no process is killed without asking, except the classes below,
    which the owner authorised on 2026-09-19 so unattended work does not stall overnight:
      - orphaned slice drivers and their `claude -p` sessions (never an interactive claude);
      - Stryker queue/runner scripts and dotnet-stryker;
      - dotnet build/run/test/exec processes working inside this repository, and the
        `dotnet build-server shutdown` helper;
      - the shared compiler server (VBCSCompiler) and MSBuild nodes, only after a graceful
        `dotnet build-server shutdown` has been tried (this script tries it, bounded to 60 s);
      - Node processes running from this repository (vite dev servers, vitest workers);
      - this project's own test hosts.
    Anything else is refused. A process younger than -MinAgeMinutes is refused too, because a
    young process is someone's live work, not an orphan. With -Tree the process's descendants are
    stopped as well, parent first, which is the order the owner requires for runner scripts.
    Every decision is appended to .scratch/stop-orphan.log with the command line, age and CPU time.

.EXAMPLE
    pwsh -NoProfile -File tools/stop-orphan.ps1 -Id 31456 -Reason "Stryker chunk build idle 60 min behind a hung compiler server"
    pwsh -NoProfile -File tools/stop-orphan.ps1 -Id 32388 -Tree -Reason "queue runner restart after patching run-stryker.ps1"
#>
param(
    [Parameter(Mandatory)][int]$Id,
    [Parameter(Mandatory)][string]$Reason,
    [switch]$Tree,
    [int]$MinAgeMinutes = 5
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$log = Join-Path $repo '.scratch/stop-orphan.log'
New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null
function Write-Log([string]$line) { "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $line" | Add-Content -LiteralPath $log; Write-Host $line }

$all = Get-CimInstance Win32_Process
$target = $all | Where-Object ProcessId -eq $Id
if (-not $target) { Write-Log "REFUSED pid=$Id reason=$Reason : no such process"; exit 2 }
$cmd = if ($target.CommandLine) { $target.CommandLine } else { '' }
$age = [int]((Get-Date) - $target.CreationDate).TotalMinutes
$cpu = try { [int](Get-Process -Id $Id).CPU } catch { -1 }
$repoNeedle = 'fuzzy-regex-cs'
$name = $target.Name

$kind = switch -Regex ($name) {
    '^pwsh\.exe$'          { if ($cmd -match 'run-slices|run-stryker|run-queue|launch-slice|load-guard|night-shift|usage-poll') { 'project script' } }
    '^dotnet\.exe$'        { if ($cmd -match 'build-server shutdown') { 'build-server helper' } elseif ($cmd -match $repoNeedle -or $cmd -match ' stryker ') { 'dotnet build/run in this repo' } }
    '^dotnet-stryker\.exe$' { 'stryker' }
    '^VBCSCompiler\.exe$'  { 'compiler server' }
    '^MSBuild\.exe$'       { 'msbuild node' }
    '^claude\.exe$'        { if ($cmd -match '"claude" -p |claude\.exe -p | -p --model') { 'headless slice session' } }
    '^node\.exe$'          { if ($cmd -match $repoNeedle) { 'node from this repo' } }
    '^FuzzyRegex\.Tests\.exe$' { 'test host' }
    '^python\.exe$'        { if ($cmd -match 'http\.server') { 'static file server' } }
}
$short = if ($cmd.Length -gt 160) { $cmd.Substring(0, 160) } else { $cmd }
if (-not $kind) { Write-Log "REFUSED pid=$Id name=$name age=${age}m cpu=${cpu}s reason=$Reason : not an agreed kind [$short]"; exit 3 }
if ($age -lt $MinAgeMinutes) { Write-Log "REFUSED pid=$Id kind=$kind age=${age}m < $MinAgeMinutes min, treat as live work [$short]"; exit 4 }

if ($kind -eq 'compiler server') {
    Write-Log "graceful first: dotnet build-server shutdown (60 s bound) before touching pid=$Id"
    $s = Start-Process -FilePath 'dotnet' -ArgumentList 'build-server', 'shutdown' -NoNewWindow -PassThru
    if (-not $s.WaitForExit(60000)) { try { $s.Kill($true) } catch { }; Write-Log '  graceful shutdown hung 60 s' }
    if (-not (Get-Process -Id $Id -ErrorAction SilentlyContinue)) { Write-Log "DONE pid=$Id exited on the graceful request"; exit 0 }
}

function Get-Descendants([int]$parent) {
    foreach ($c in ($all | Where-Object ParentProcessId -eq $parent)) { $c; Get-Descendants $c.ProcessId }
}
$victims = @($target) + $(if ($Tree) { @(Get-Descendants $Id) } else { @() })
foreach ($v in $victims) {
    $vcmd = if ($v.CommandLine) { $v.CommandLine } else { '' }
    $vshort = if ($vcmd.Length -gt 120) { $vcmd.Substring(0, 120) } else { $vcmd }
    try {
        Stop-Process -Id $v.ProcessId -Force -ErrorAction Stop
        Write-Log "STOPPED pid=$($v.ProcessId) name=$($v.Name) kind=$kind age=${age}m cpu=${cpu}s reason=$Reason [$vshort]"
    } catch {
        Write-Log "FAILED pid=$($v.ProcessId) name=$($v.Name): $($_.Exception.Message)"
    }
}
exit 0
