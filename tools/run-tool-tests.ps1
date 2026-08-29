<#
.SYNOPSIS
    Runs the Pester tests for tools/PortTools.psm1.

.DESCRIPTION
    The ratchet, the status board and the budget gate decide whether work is allowed to land and
    whether the driver may spend allowance, so they carry tests of their own. Exits non-zero on
    failure so CI and the driver can rely on it.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module Pester -MinimumVersion 5.0.0

$configuration = New-PesterConfiguration
$configuration.Run.Path = Join-Path $PSScriptRoot 'tests'
$configuration.Run.Exit = $true
$configuration.Output.Verbosity = 'Detailed'

Invoke-Pester -Configuration $configuration
