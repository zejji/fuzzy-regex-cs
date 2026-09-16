<#
.SYNOPSIS
    Publishes samples/FuzzyRegex.AotSmoke as a Native AOT binary, runs it, and reports its size.

.DESCRIPTION
    The consumer half of the AOT gate (S53). tools/run-aot-tests.ps1 publishes the whole test
    suite natively and is the stronger evidence; this covers what only a separate consumer can
    show, and the difference is not cosmetic:

      - it reaches the library through a PROJECT REFERENCE from outside the test tree, the way a
        package consumer does;
      - it roots NOTHING in the trimmer, where the test project roots three assemblies so its
        reflection-based audits stay honest. A table the trimmer would drop from a real app is
        still present in that binary and absent here;
      - it is therefore the only honest place to measure binary size and startup time, which are
        Phase 7's optimisation baseline.

.PARAMETER Rid
    The runtime identifier to publish for. Defaults to this machine's.

.PARAMETER Configuration
    Release by default.

.EXAMPLE
    tools/run-aot-smoke.ps1
    tools/run-aot-smoke.ps1 -Rid linux-x64
#>
[CmdletBinding()]
param(
    [string]$Rid,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The hang fix, for the reason set out at the top of tools/run-aot-tests.ps1: a leftover MSBuild
# node wedges the publish indefinitely, and `-nodeReuse:false` alone does not stop this build
# joining one that is already wedged.
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'samples/FuzzyRegex.AotSmoke'

if (-not $Rid) {
    $Rid = if ($IsWindows) { 'win-x64' } elseif ($IsMacOS) { 'osx-x64' } else { 'linux-x64' }
}

# See the same block in tools/run-aot-tests.ps1: the ILCompiler locates MSVC by running
# vswhere.exe, which lives in the Visual Studio Installer directory and is not on PATH in a plain
# Windows shell. GitHub's windows-latest image already has it.
if ($IsWindows) {
    $installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer'
    if ((Test-Path $installer) -and ($env:PATH -notlike "*$installer*")) {
        $env:PATH = "$installer$([System.IO.Path]::PathSeparator)$env:PATH"
    }
}

$publishDir = Join-Path $project "bin/$Configuration/net10.0/$Rid/publish"
$exeName = if ($Rid -like 'win-*') { 'FuzzyRegex.AotSmoke.exe' } else { 'FuzzyRegex.AotSmoke' }
$exe = Join-Path $publishDir $exeName

Write-Host "Publishing the smoke consumer as Native AOT for $Rid."

# No relaxation of TreatWarningsAsErrors and no IL suppression: a trim or AOT warning from a
# consumer publishing this library is the finding, and it gets fixed in src/ test-first.
# PublishAot is passed here rather than set in the csproj, on purpose: in the csproj it also lands
# in the ordinary build's runtimeconfig, and the app's own "did I run natively?" report then reads
# false under a plain `dotnet run` and claims a native run that never happened.
dotnet publish $project --configuration $Configuration --runtime $Rid `
    -p:PublishAot=true -nodeReuse:false -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) {
    throw "The Native AOT publish of the smoke consumer failed for $Rid."
}

if (-not (Test-Path $exe)) {
    throw "No published executable at $exe."
}

$sizeBytes = (Get-Item $exe).Length
$sizeMb = [math]::Round($sizeBytes / 1MB, 2)

& $exe
$smokeExit = $LASTEXITCODE

# The binary is run a SECOND time, and the second run's timings are the ones to quote. A freshly
# written 6.65 MB executable pays image load and an antivirus scan on its first execution, and
# that dwarfs everything else: measured 2026-09-16 on win-x64, the first run reported 531.8 ms
# before reaching Main and three consecutive re-runs of the same file reported 28.5, 30.9 and
# 27.9 ms. Quoting the cold number as this library's startup cost would overstate it eighteenfold.
if ($smokeExit -eq 0) {
    Write-Host ''
    Write-Host 'Second run (warm: the same file, already loaded and scanned) - these are the timings to quote:'
    & $exe | Select-String -Pattern 'to Main|to console|to 1st answer|to end'
}

Write-Host ''
Write-Host "binary: $exeName  $sizeMb MB ($sizeBytes bytes), $Rid, trimmed, nothing rooted"

if ($smokeExit -ne 0) {
    throw "The smoke consumer reported a wrong answer (exit $smokeExit). A feature area that is " +
        "correct under the JIT and wrong here is a runtime-only AOT failure, and the fix belongs " +
        "in src/ test-first - never a [DynamicDependency] in the sample."
}

Write-Host "AOT SMOKE GREEN on $Rid."
