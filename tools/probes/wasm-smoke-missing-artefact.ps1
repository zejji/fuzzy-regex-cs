<#
.SYNOPSIS
    Proves that tools/run-wasm-smoke.ps1 fails when a published file named by the boot manifest is
    missing, rather than printing GREEN over a publish that cannot boot.

.DESCRIPTION
    S70. The smoke script's missing-artefact branch was reasoned but never seen to fire, because the
    script publishes before it checks and the publish restores whatever you deleted. -SkipPublish
    makes the branch reachable; this probe exercises it.

    Three runs of the check, because one proves nothing:

      1. before the deletion, expecting GREEN. Without this control, the probe passes just as
         happily against a publish that was already broken, and its perturbation is what proves
         nothing;
      2. with one published .wasm deleted, expecting a non-zero exit AND the missing-artefact
         branch's own wording. Exit code plus the file's name is not enough to tell this branch
         from the integrity-mismatch branch, which names the same file;
      3. after the restore, expecting GREEN again. That is what shows the file came back whole,
         rather than the probe merely saying it copied it.

    Run it against an existing publish (tools/run-wasm-smoke.ps1 first). It leaves the publish as it
    found it.

    Measured 2026-09-18: deleting ONE file reports two missing manifest ENTRIES, because each asset
    is named by a fingerprinted route and a plain one; the script counts the distinct files.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# tools/probes/ is two levels down, not one.
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$smoke = Join-Path $repoRoot 'tools/run-wasm-smoke.ps1'
$webRoot = Join-Path $repoRoot "demo/FuzzyRegex.Demo.Wasm/bin/$Configuration/net10.0/publish/wwwroot"
if (-not (Test-Path $webRoot)) {
    throw "No publish at $webRoot. Run tools/run-wasm-smoke.ps1 first."
}

function Invoke-Check {
    # A child pwsh reports a thrown terminating error as a non-zero exit code, not as an exception
    # in this process, so the verdict has to be read from $LASTEXITCODE. Reading it as an exception
    # is what made the first run of this probe report the opposite of what its own output showed.
    $output = & pwsh -NoProfile -File $smoke -SkipPublish -Configuration $Configuration 2>&1 | Out-String
    return @{ Output = $output; ExitCode = $LASTEXITCODE; Green = $output -match 'WASM SMOKE GREEN' }
}

$control = Invoke-Check
if ($control.ExitCode -ne 0 -or -not $control.Green) {
    throw "The publish is not clean before the probe perturbs it (exit $($control.ExitCode)), so " +
    'nothing this probe went on to observe would be evidence about the branch under test.'
}
Write-Host 'Control: the publish checks GREEN before the deletion.'

$victim = Get-ChildItem (Join-Path $webRoot '_framework') -Filter '*.wasm' -File |
    Sort-Object Name |
    Select-Object -First 1

# A per-run backup directory, not a fixed %TEMP%\<name>: this repo runs concurrent worktrees, and a
# machine-global path means a second run overwrites the only copy of the first run's victim.
$backupDir = Join-Path ([System.IO.Path]::GetTempPath()) "wasm-smoke-probe-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $backupDir | Out-Null
$backup = Join-Path $backupDir $victim.Name

Copy-Item $victim.FullName $backup -Force
Remove-Item $victim.FullName -Force
Write-Host "Deleted $($victim.Name) from the publish."

try {
    $perturbed = Invoke-Check
}
finally {
    Copy-Item $backup $victim.FullName -Force
    Remove-Item $backupDir -Recurse -Force
    Write-Host "Restored $($victim.Name)."
}

# The integrity-mismatch branch also exits non-zero and also names the file, so the branch under
# test has to be identified by its own wording, not by the exit code and a filename.
$namedTheFile = $perturbed.Output -match "missing: .*$([regex]::Escape($victim.Name))"
$saidMissing = $perturbed.Output -match 'named by the manifest are not in the publish'

$restored = Invoke-Check
if ($restored.ExitCode -ne 0 -or -not $restored.Green) {
    throw "The restore did not put the publish back: it no longer checks GREEN (exit " +
    "$($restored.ExitCode)). The backup of $($victim.Name) has already been deleted; re-publish."
}

if ($perturbed.ExitCode -ne 0 -and $namedTheFile -and $saidMissing -and -not $perturbed.Green) {
    Write-Host ''
    # Parenthesised: Write-Host takes a bare + and the string after it as further positional
    # arguments and prints them, so an unparenthesised concatenation puts a literal "+" in the
    # verdict line.
    Write-Host ("MISSING-ARTEFACT BRANCH FIRED: exit $($perturbed.ExitCode), reported " +
        "$($victim.Name) missing, printed no GREEN; the publish checks GREEN again after the restore.")
    exit 0
}

Write-Host ''
Write-Host ("BRANCH DID NOT FIRE: exit $($perturbed.ExitCode), named the file: $namedTheFile, " +
    "used the missing-artefact wording: $saidMissing, printed GREEN: $($perturbed.Green)")
exit 1
