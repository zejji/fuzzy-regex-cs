<#
.SYNOPSIS
    The two controls on tools/probes/wasm-smoke-missing-artefact.ps1: they show that probe would
    have reported success without the branch under test having fired.

.DESCRIPTION
    S70, sitting 3, from the blind review. A probe that claims to have seen a branch fire is only
    worth its word if something would have caught it claiming that wrongly. These are the two ways
    the first version of that probe was wrong, each staged deliberately:

      Control A - corrupt one published file (append a byte) instead of deleting it. The
        integrity-MISMATCH branch then fires: non-zero exit, and the file's name in the output,
        which is everything the first version of the probe tested for. Expected: the missing-
        artefact wording is absent, so the hardened probe's discriminator separates the two.

      Control B - delete a published file and then run the probe. Expected: the probe refuses on
        its own control run rather than reporting the branch fired, because a publish that was
        already broken makes its perturbation prove nothing.

    Measured 2026-09-18 against the committed tree:
      Control A: exit 1, names the file True, missing-artefact wording False, mismatch wording True.
      Control B: probe exit 1, refused on the control run True, claimed the branch fired False.

    Needs an existing publish (tools/run-wasm-smoke.ps1 first). It restores what it perturbs.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$smoke = Join-Path $repoRoot 'tools/run-wasm-smoke.ps1'
$probe = Join-Path $PSScriptRoot 'wasm-smoke-missing-artefact.ps1'
$webRoot = Join-Path $repoRoot "demo/FuzzyRegex.Demo.Wasm/bin/$Configuration/net10.0/publish/wwwroot"
if (-not (Test-Path $webRoot)) {
    throw "No publish at $webRoot. Run tools/run-wasm-smoke.ps1 first."
}

$file = Get-ChildItem (Join-Path $webRoot '_framework') -Filter '*.wasm' -File |
    Sort-Object Name |
    Select-Object -First 1
$backupDir = Join-Path ([System.IO.Path]::GetTempPath()) "wasm-smoke-controls-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $backupDir | Out-Null
$backup = Join-Path $backupDir $file.Name
Copy-Item $file.FullName $backup -Force

$failures = @()

try {
    Write-Host "=== Control A: corrupt $($file.Name), expect the MISMATCH branch, not the missing one ==="
    Add-Content -Path $file.FullName -Value 'x' -NoNewline
    $a = & pwsh -NoProfile -File $smoke -SkipPublish -Configuration $Configuration 2>&1 | Out-String
    $aExit = $LASTEXITCODE
    $aNames = $a -match [regex]::Escape($file.Name)
    $aMissing = $a -match 'named by the manifest are not in the publish'
    $aMismatch = $a -match 'do not match their integrity hash'
    Write-Host "exit $aExit, names the file $aNames, missing wording $aMissing, mismatch wording $aMismatch"
    if (-not ($aExit -ne 0 -and $aNames -and -not $aMissing -and $aMismatch)) {
        $failures += 'Control A did not produce a mismatch that is distinguishable from a missing file.'
    }

    Copy-Item $backup $file.FullName -Force

    Write-Host ''
    Write-Host "=== Control B: delete $($file.Name), expect the probe to REFUSE, not to report a firing ==="
    Remove-Item $file.FullName -Force
    $b = & pwsh -NoProfile -File $probe -Configuration $Configuration 2>&1 | Out-String
    $bExit = $LASTEXITCODE
    $bRefused = $b -match 'not clean before the probe perturbs it'
    $bClaimed = $b -match 'BRANCH FIRED'
    Write-Host "probe exit $bExit, refused on its control run $bRefused, claimed a firing $bClaimed"
    if (-not ($bExit -ne 0 -and $bRefused -and -not $bClaimed)) {
        $failures += 'Control B: the probe drew a conclusion from a publish that was already broken.'
    }
}
finally {
    Copy-Item $backup $file.FullName -Force
    Remove-Item $backupDir -Recurse -Force
    Write-Host ''
    Write-Host "Restored $($file.Name)."
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    exit 1
}

Write-Host 'BOTH CONTROLS BEHAVED: the missing-artefact probe can tell the two failures apart and refuses a dirty start.'
exit 0
