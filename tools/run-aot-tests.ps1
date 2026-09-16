<#
.SYNOPSIS
    Publishes the test suite as a Native AOT executable and runs it. The dynamic half of the AOT gate.

.DESCRIPTION
    `<IsAotCompatible>true</IsAotCompatible>` on src/FuzzyRegex has enforced the STATIC half since
    Phase 2 (ROADMAP, amendment 12): the trim, AOT and single-file analyzers run on every build and
    TreatWarningsAsErrors turns a hazard into a failed build. Static analysis cannot see a
    runtime-only failure, so this is the other half - a real ILCompiler binary, run.

    Publishing the whole SUITE rather than a sample is deliberate (S53, owner question 2026-09-16).
    TUnit is source-generated and documents Native AOT as PublishAot plus `dotnet publish`, so all
    6,155 tests can run under the same trimming and reflection constraints a consumer's AOT app
    enforces. That is stronger evidence than any hand-written sample, which can only assert the
    cases somebody thought of. samples/FuzzyRegex.AotSmoke covers what only a separate consumer
    can show - a project reference from outside, binary size and startup time.

    tests/FuzzyRegex.OracleTests is NOT published: it parses its corpus JSON reflectively by
    design, so it is not an AOT candidate and never claimed to be.

.PARAMETER Rid
    The runtime identifier to publish for. Defaults to this machine's. The ILCompiler is
    platform-specific, so a green win-x64 run is not evidence for linux-x64 - CI runs both.

.PARAMETER Configuration
    Release by default: PublishAot is a release-shaped operation and the CI gate uses Release.

.PARAMETER SkipPublish
    Run the executable already in the publish directory. For iterating on a failure without
    paying the four-minute native compile again.

.EXAMPLE
    tools/run-aot-tests.ps1
    tools/run-aot-tests.ps1 -Rid linux-x64
#>
[CmdletBinding()]
param(
    [string]$Rid,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# THE HANG FIX, and it belongs here rather than on the publish command line. A persistent MSBuild
# node left over from an earlier build wedges this publish completely: three times on 2026-09-16 it
# printed "Determining projects to restore...", restored, and then sat there - no `ilc.exe` ever
# started, an ordinary `dotnet build` of the same project hung alongside it, and even
# `dotnet build-server shutdown` hung. `-nodeReuse:false` on the command line is NOT sufficient,
# because it stops this build leaving a node behind without stopping it JOINING one that is already
# wedged; the third hang happened with that flag already in place. These two environment variables
# are what actually worked, every time. A fresh process each time costs a few seconds and cannot
# wedge, which is the right trade for a step the driver runs unattended.
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests/FuzzyRegex.Tests'

if (-not $Rid) {
    $Rid = if ($IsWindows) { 'win-x64' } elseif ($IsMacOS) { 'osx-x64' } else { 'linux-x64' }
}

# The ILCompiler shells out to the platform linker, and on Windows it locates MSVC by running
# vswhere.exe, which the Visual Studio *Installer* directory owns and which is not on PATH in a
# plain shell. Without this the publish gets as far as "Generating native code" and then fails
# with MSB3073 and "'vswhere.exe' is not recognized" - measured on this machine 2026-09-16.
# GitHub's windows-latest image already has it on PATH, so this only helps a local run.
if ($IsWindows) {
    $installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer'
    if ((Test-Path $installer) -and ($env:PATH -notlike "*$installer*")) {
        $env:PATH = "$installer$([System.IO.Path]::PathSeparator)$env:PATH"
        Write-Host "Added the Visual Studio Installer directory to PATH so the ILCompiler can find vswhere."
    }
}

$publishDir = Join-Path $project "bin/$Configuration/net10.0/$Rid/publish"
$exeName = if ($Rid -like 'win-*') { 'FuzzyRegex.Tests.exe' } else { 'FuzzyRegex.Tests' }
$exe = Join-Path $publishDir $exeName

if (-not $SkipPublish) {
    # About 90 seconds to two minutes for a full ILCompiler run on this machine, measured twice by
    # S53's verifier; much less when obj/ is warm and only the managed build changes.
    Write-Host "Publishing the suite as Native AOT for $Rid. Expect a minute or two."

    # Trim and AOT warnings stay errors: Directory.Build.props sets TreatWarningsAsErrors and this
    # script does not relax it. src/FuzzyRegex has no suppressions at all; the test project's are
    # in its own csproj, each with the code and the reason.
    # The two flags belong with the environment variables set at the top of this script; see the
    # comment there for why both are needed.
    dotnet publish $project --configuration $Configuration --runtime $Rid `
        -p:PublishAot=true -nodeReuse:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) {
        throw "The Native AOT publish failed for $Rid. That is the gate reporting a real hazard, not a flake."
    }
}

if (-not (Test-Path $exe)) {
    throw "No published executable at $exe."
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Running $exeName ($sizeMb MB, native)."

& $exe
$testExit = $LASTEXITCODE

if ($testExit -ne 0) {
    throw "The native test run failed (exit $testExit). Compare it with a JIT run: a test that " +
        "passes under `dotnet run` and fails here is an AOT incompatibility, which is precisely " +
        "what this gate exists to find."
}

Write-Host "AOT GREEN: the whole suite passes in a Native AOT binary on $Rid."
