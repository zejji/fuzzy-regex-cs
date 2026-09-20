<#
.SYNOPSIS
    Packs src/FuzzyRegex in Release and validates the resulting .nupkg/.snupkg.

.DESCRIPTION
    The lightweight half of the release rehearsal (S66): the part cheap enough to run on every
    push. `dotnet pack` already runs `EnablePackageValidation`'s shape checks (src/FuzzyRegex's
    csproj); this adds `meziantou.validate-nuget-package`'s metadata and content checks
    (license/readme/repository/deterministic-symbols) on top.

    `IconMustBeSet` is excluded: no package icon exists yet (a branding decision, not a packaging
    gap - see docs/plan/RELEASE.md). `RepositoryMustBeSet`'s URL-accessibility checks require the
    packed commit to already be reachable on GitHub, so they fail on uncommitted or unpushed work
    by design - that is the real gate, not a local-only false alarm.

    Nothing here signs or pushes anything. The heavier rehearsal - install from a local feed,
    publish the consumer with PublishAot=true - is tools/run-release-rehearsal.ps1.

.PARAMETER VersionSuffix
    Passed straight to `dotnet pack -p:VersionSuffix=`. Empty (the default) packs whatever
    Directory.Build.props' VersionPrefix says, e.g. `1.0.0`; pass `rc.0` for a rehearsal build.

.EXAMPLE
    pwsh -File tools/pack-and-validate.ps1
    pwsh -File tools/pack-and-validate.ps1 -VersionSuffix rc.0
#>
[CmdletBinding()]
param([string]$VersionSuffix = '')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/FuzzyRegex/FuzzyRegex.csproj'
$outDir = Join-Path $repoRoot '.scratch/pack'

if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
New-Item -ItemType Directory -Force $outDir | Out-Null

$packArgs = @(
    $project, '--configuration', 'Release', '-o', $outDir,
    # Always on here, regardless of $CI: a package built for consumers must never embed a
    # contributor's local absolute source paths (that is what feeds the 112/119 SourceLink
    # findings below - Directory.Build.props otherwise only turns this on when $CI is 'true').
    '-p:ContinuousIntegrationBuild=true'
)
if ($VersionSuffix) { $packArgs += "-p:VersionSuffix=$VersionSuffix" }

Write-Host "Packing FuzzyRegex (Release$(if ($VersionSuffix) { ", suffix $VersionSuffix" }))."
dotnet pack @packArgs
if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

$nupkg = Get-ChildItem -Path $outDir -Filter '*.nupkg' | Select-Object -First 1
if (-not $nupkg) { throw "No .nupkg produced in $outDir." }
$snupkg = Get-ChildItem -Path $outDir -Filter '*.snupkg' | Select-Object -First 1
if (-not $snupkg) { throw "No .snupkg produced in $outDir." }

Write-Host "Validating $($nupkg.Name)."
$validateArgs = @($nupkg.FullName, '--excluded-rules', 'IconMustBeSet')
if ($env:GITHUB_TOKEN) { $validateArgs += @('--github-token', $env:GITHUB_TOKEN) }
dotnet meziantou.validate-nuget-package @validateArgs
if ($LASTEXITCODE -ne 0) { throw 'Package validation failed.' }

Write-Host "PACK AND VALIDATE GREEN: $($nupkg.Name), $($snupkg.Name)"
