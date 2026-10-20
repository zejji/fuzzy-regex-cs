<#
.SYNOPSIS
    Checks that a release tag names the version Directory.Build.props would pack, and prints it.

.DESCRIPTION
    The release workflow (.github/workflows/release.yml) runs this first on a pushed tag. The tag
    must be exactly `v` followed by VersionPrefix (for example v1.0.0 for 1.0.0), because the
    workflow packs with no VersionSuffix. Anything else stops the release before it builds, since
    nuget.org never accepts the same version number twice.

.PARAMETER Tag
    The tag name, for example v1.0.0. The workflow passes $GITHUB_REF_NAME.

.PARAMETER PropsPath
    The props file holding VersionPrefix. Defaults to the repository's Directory.Build.props.

.EXAMPLE
    pwsh -File tools/check-release-tag.ps1 -Tag v1.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Tag,
    [string]$PropsPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Directory.Build.props')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

[xml]$props = Get-Content -LiteralPath $PropsPath -Raw
$node = $props.SelectSingleNode('/Project/PropertyGroup/VersionPrefix')
if (-not $node -or -not $node.InnerText.Trim()) { throw "No VersionPrefix found in $PropsPath." }
$version = $node.InnerText.Trim()

if ($Tag -cne "v$version") {
    throw "Tag $Tag does not match the packed version: expected v$version (VersionPrefix in $PropsPath)."
}

$version
