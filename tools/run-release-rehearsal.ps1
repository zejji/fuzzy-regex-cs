<#
.SYNOPSIS
    The full release rehearsal (S66): pack, install from a local feed, run the README's first
    sample, and publish the consumer as trimmed Native AOT - all on a nupkg nobody outside this
    machine ever sees.

.DESCRIPTION
    tools/pack-and-validate.ps1 covers what CI checks on every push - and needs the packed commit
    already pushed to GitHub, since that is what its SourceLink checks resolve against, so it
    cannot be green on uncommitted or unpushed work. This script packs on its own account for that
    reason, and checks something unrelated to nuget.org metadata: that a consumer who has never
    seen this source tree, reaching the library through `dotnet add package` rather than a project
    reference, gets a working, AOT-publishable package. samples/FuzzyRegex.AotSmoke already proves
    the library compiles and runs correctly under AOT constraints; this proves the PACKAGING
    around it - the .nupkg's dependency and asset metadata - does not break that when a consumer
    installs it for real.

    A folder of .nupkg files is a valid NuGet feed on its own, so the pack output directory IS
    the local feed; nothing is copied anywhere else. The consumer's restore is pointed at an
    isolated NUGET_PACKAGES cache (deleted at the start of every run) so a stale extraction of a
    same-numbered previous rehearsal package can never be served instead of the one just packed -
    the real release always bumps the version, but a rehearsal run does not have to.

.PARAMETER Rid
    The runtime identifier to publish the consumer for. Defaults to this machine's.

.EXAMPLE
    pwsh -File tools/run-release-rehearsal.ps1
#>
[CmdletBinding()]
param([string]$Rid)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$feedDir = Join-Path $repoRoot '.scratch/pack'
$consumerDir = Join-Path $repoRoot '.scratch/release-rehearsal-consumer'
$nugetCacheDir = Join-Path $repoRoot '.scratch/release-rehearsal-nuget-cache'
$version = '1.0.0-rc.0'

if (-not $Rid) {
    $Rid = if ($IsWindows) { 'win-x64' } elseif ($IsMacOS) { 'osx-x64' } else { 'linux-x64' }
}

Write-Host '--- Step 1: pack ---'
if (Test-Path $feedDir) { Remove-Item -Recurse -Force $feedDir }
New-Item -ItemType Directory -Force $feedDir | Out-Null
dotnet pack (Join-Path $repoRoot 'src/FuzzyRegex/FuzzyRegex.csproj') --configuration Release -o $feedDir `
    -p:ContinuousIntegrationBuild=true -p:VersionSuffix=rc.0
if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

Write-Host ''
Write-Host '--- Step 2: install from the local feed into a fresh consumer ---'
if (Test-Path $consumerDir) { Remove-Item -Recurse -Force $consumerDir }
if (Test-Path $nugetCacheDir) { Remove-Item -Recurse -Force $nugetCacheDir }
New-Item -ItemType Directory -Force $consumerDir | Out-Null
$env:NUGET_PACKAGES = $nugetCacheDir

# Empty overrides so MSBuild's upward directory search stops here instead of reaching the repo
# root's Directory.Build.props/Directory.Packages.props - otherwise `dotnet add package` below
# writes its <PackageVersion> into the REAL Directory.Packages.props, because central package
# management applies to every project under the repo root regardless of where it sits on disk
# (caught during S66's own rehearsal: it landed a stray FuzzyRegex 1.0.0-rc.0 entry there).
'<Project />' | Set-Content -Path (Join-Path $consumerDir 'Directory.Build.props') -Encoding utf8
'<Project />' | Set-Content -Path (Join-Path $consumerDir 'Directory.Packages.props') -Encoding utf8

dotnet new console -n Consumer -o $consumerDir --force | Out-Null

@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="local-rehearsal-feed" value="$feedDir" />
  </packageSources>
</configuration>
"@ | Set-Content -Path (Join-Path $consumerDir 'nuget.config') -Encoding utf8

$csproj = Join-Path $consumerDir 'Consumer.csproj'
(Get-Content $csproj -Raw) -replace '<TargetFramework>[^<]*</TargetFramework>', '<TargetFramework>net10.0</TargetFramework>' |
    Set-Content -Path $csproj -Encoding utf8

Push-Location $consumerDir
try {
    dotnet add package FuzzyRegex --version $version
    if ($LASTEXITCODE -ne 0) { throw "dotnet add package FuzzyRegex $version failed." }
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host "--- Step 3: run the README's first sample ---"
# README.md, "Exact match with named groups" - the exact snippet, so this rehearsal is proof the
# thing a real consumer pastes from the README actually runs from the packaged library.
@'
using Fuzzy.Text.RegularExpressions;

Match match = FuzzyRegex.Match("2026-09-16", @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})");

foreach (string key in match.Groups.Keys)
{
    Console.WriteLine($"{key}: {match.Groups[key].Value}");
}
'@ | Set-Content -Path (Join-Path $consumerDir 'Program.cs') -Encoding utf8

$expected = @('0: 2026-09-16', 'year: 2026', 'month: 09', 'day: 16')

$runOutput = dotnet run --project $consumerDir --configuration Release 2>&1
if ($LASTEXITCODE -ne 0) { throw "dotnet run failed:`n$runOutput" }
$runLines = @($runOutput | Where-Object { $_ -match '^\S+: ' })
if (@(Compare-Object $expected $runLines -SyncWindow 0).Count -ne 0) {
    throw "dotnet run printed unexpected output.`nExpected: $($expected -join ' | ')`nActual:   $($runLines -join ' | ')"
}
Write-Host "dotnet run: $($runLines -join ', ')"

Write-Host ''
Write-Host '--- Step 4: publish the consumer as trimmed Native AOT ---'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
if ($IsWindows) {
    $installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer'
    if ((Test-Path $installer) -and ($env:PATH -notlike "*$installer*")) {
        $env:PATH = "$installer$([System.IO.Path]::PathSeparator)$env:PATH"
    }
}

dotnet publish $consumerDir --configuration Release --runtime $Rid -p:PublishAot=true -nodeReuse:false -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "Native AOT publish of the consumer failed for $Rid." }

$publishDir = Join-Path $consumerDir "bin/Release/net10.0/$Rid/publish"
$exeName = if ($Rid -like 'win-*') { 'Consumer.exe' } else { 'Consumer' }
$exe = Join-Path $publishDir $exeName
if (-not (Test-Path $exe)) { throw "No published executable at $exe." }

$aotOutput = & $exe
$aotExit = $LASTEXITCODE
$aotLines = @($aotOutput | Where-Object { $_ -match '^\S+: ' })
if ($aotExit -ne 0) { throw "The published AOT consumer exited $aotExit." }
if (@(Compare-Object $expected $aotLines -SyncWindow 0).Count -ne 0) {
    throw "The AOT consumer printed unexpected output.`nExpected: $($expected -join ' | ')`nActual:   $($aotLines -join ' | ')"
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host "AOT publish: $($aotLines -join ', ')"
Write-Host "binary: $exeName  $sizeMb MB, $Rid, installed from a local feed, trimmed"
Write-Host ''
Write-Host 'RELEASE REHEARSAL GREEN.'
