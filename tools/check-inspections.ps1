<#
.SYNOPSIS
    Runs ReSharper's command-line inspection over the solution and fails on any ERROR-severity
    finding, so Rider's red squiggles cannot be missed by a build that never saw them.
.DESCRIPTION
    Rider and ReSharper report "Anonymous function can be made static" (Roslyn IDE0320) at ERROR
    severity, but on the .NET 10.0.400 SDK the IDE0320 analyzer reports nothing under `dotnet build`
    whatever .editorconfig says (measured 2026-09-12, DECISIONS). ReSharper's own host does run it.
    So this script is the gate: same engine the IDE uses, run headless, exit 1 on findings.

    Owner decision, 2026-09-12: these must fail the build so they cannot be missed. CI runs this
    as its own job; run it locally before a commit that adds a lambda.

    Requires the ReSharper global tool: dotnet tool install -g JetBrains.ReSharper.GlobalTools
.PARAMETER Severity
    Minimum severity to report and fail on. ERROR by default; pass WARNING to see the backlog of
    style warnings too (about 465 on 2026-09-12, none of them owner-requested yet).
.EXAMPLE
    pwsh -File tools/check-inspections.ps1
#>
param(
    [ValidateSet('ERROR', 'WARNING', 'SUGGESTION')][string]$Severity = 'ERROR',
    [string]$Output = '.scratch/inspect.xml'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
New-Item -ItemType Directory -Force -Path (Split-Path $Output) | Out-Null

if (-not (Get-Command jb -ErrorAction SilentlyContinue)) {
    Write-Error 'jb (JetBrains.ReSharper.GlobalTools) is not installed: dotnet tool install -g JetBrains.ReSharper.GlobalTools'
}

# --no-build: the caller has built already, and a second build here would race the compiler
# server. --no-buildin-settings: judge by the repo's .editorconfig alone, not by whatever Rider
# profile happens to be on this machine.
& jb inspectcode FuzzyRegex.slnx --no-build --no-buildin-settings -f=Xml "-o=$Output" "-e=$Severity" | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Error "jb inspectcode exited $LASTEXITCODE" }

[xml]$report = Get-Content -LiteralPath $Output -Raw
$types = @{}
foreach ($t in $report.SelectNodes('//IssueType')) { $types[$t.Id] = $t }
# The CLI's -e filter lets compiler-class warnings (UnassignedField.Compiler and friends) through at
# any level, so filter again here by the issue TYPE's own severity.
$rank = @{ 'SUGGESTION' = 1; 'WARNING' = 2; 'ERROR' = 3 }
$issues = @($report.SelectNodes('//Issue') | Where-Object {
    $t = $types[$_.TypeId]; $t -and $rank[$t.Severity] -ge $rank[$Severity]
})

if ($issues.Count -eq 0) {
    Write-Host "Inspections: GREEN (no findings at $Severity or above)" -ForegroundColor Green
    exit 0
}

$issues | Group-Object TypeId | Sort-Object Count -Descending | ForEach-Object {
    $t = $types[$_.Name]
    Write-Host ("  {0,5}  {1,-10} {2,-45} {3}" -f $_.Count, $t.Severity, $_.Name, $t.Description)
}
foreach ($i in $issues | Select-Object -First 40) {
    Write-Host "    $($i.File):$($i.Line)  $($i.TypeId)  $($i.Message)"
}
if ($issues.Count -gt 40) { Write-Host "    ... and $($issues.Count - 40) more (see $Output)" }
Write-Host "Inspections: RED ($($issues.Count) findings at $Severity or above)" -ForegroundColor Red
exit 1
