<#
.SYNOPSIS
    Proves that tools/run-wasm-smoke.ps1's two sitting-4 checks fire: a missing COMPRESSED artefact,
    and a file on disk the boot manifest never named.

.DESCRIPTION
    S70 sitting 4. The blind review of sitting 3's delta found both of these passing silently, and
    both are checks whose absence is invisible - the script printed GREEN either way. So they get a
    probe rather than a paragraph, for the reason the other two probes in this directory exist:
    S18's and S22's controls were described in prose and are now unreproducible.

    What it stages, each against a throwaway COPY of the publish so the real one is never touched:

      1. a control run over the untouched copy, expecting GREEN. Without it, a perturbation that
         "fails" proves nothing - the copy might have been broken before the probe touched it;
      2. every .br and .gz deleted but one, expecting a non-zero exit AND the missing-artefact
         wording. Measured before the fix: GREEN, with the printed gzip wire figure quietly
         collapsing from 2,076,631 bytes to 3,796;
      3. the compressed files restored, then one .wasm duplicated under a fingerprint nobody
         published, expecting a non-zero exit AND the orphan wording. Measured before the fix:
         GREEN, at 23 files and 8.36 MB against the true 22 and 6.94 MB;
      4. a final control over the restored copy, expecting GREEN again, so that a perturbation that
         merely broke the copy for good cannot be read as a check firing.

    Needs an existing publish (tools/run-wasm-smoke.ps1 first). Takes seconds: it copies ~7 MB and
    runs the check four times with -SkipPublish, building nothing.

.PARAMETER PublishWebRoot
    The published wwwroot to copy. Defaults to the project's own publish output.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$PublishWebRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# tools/probes/ is two levels down, not one.
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$smoke = Join-Path $repoRoot 'tools/run-wasm-smoke.ps1'

if ([string]::IsNullOrWhiteSpace($PublishWebRoot)) {
    $PublishWebRoot = Join-Path $repoRoot "demo/FuzzyRegex.Demo.Wasm/bin/$Configuration/net10.0/publish/wwwroot"
}
if (-not (Test-Path $PublishWebRoot)) {
    throw "No publish at $PublishWebRoot. Run tools/run-wasm-smoke.ps1 first."
}

# The whole publish directory is what the check is pointed at, so the copy is of its parent: the
# manifest the check reads sits beside wwwroot, not inside it. A per-run GUID because this repo runs
# concurrent worktrees.
$sourcePublish = Split-Path -Parent $PublishWebRoot
$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) "wasm-smoke-artefacts-$([guid]::NewGuid())"
Copy-Item $sourcePublish $sandbox -Recurse
$sandboxWeb = Join-Path $sandbox 'wwwroot'

function Invoke-Check {
    # A child pwsh reports a thrown terminating error as a non-zero exit code, not as an exception
    # in this process, so the verdict has to be read from $LASTEXITCODE.
    $output = & pwsh -NoProfile -File $smoke -SkipPublish -OutDir $sandbox -Configuration $Configuration 2>&1 |
        Out-String
    return @{ Output = $output; ExitCode = $LASTEXITCODE; Green = $output -match 'WASM SMOKE GREEN' }
}

$results = [ordered]@{}
try {
    $control = Invoke-Check
    if ($control.ExitCode -ne 0 -or -not $control.Green) {
        throw "The copied publish is not clean before the probe perturbs it (exit " +
        "$($control.ExitCode)), so nothing this probe went on to observe would be evidence."
    }
    Write-Host 'Control: the copy checks GREEN before anything is perturbed.'

    # --- 2: the compressed variants -----------------------------------------------------------
    $compressed = @(Get-ChildItem $sandboxWeb -File -Recurse |
            Where-Object { $_.Extension -in @('.br', '.gz') })
    $keep = $compressed | Where-Object { $_.Extension -eq '.gz' } | Select-Object -First 1
    $deleted = @($compressed | Where-Object { $_.FullName -ne $keep.FullName })
    $deleted | Remove-Item -Force
    Write-Host "Deleted $($deleted.Count) of $($compressed.Count) compressed variants."

    $withoutCompressed = Invoke-Check
    $results['compressed'] = @{
        Fired   = ($withoutCompressed.ExitCode -ne 0 -and -not $withoutCompressed.Green -and
            $withoutCompressed.Output -match 'named by the manifest are not in the publish')
        Exit    = $withoutCompressed.ExitCode
        Deleted = $deleted.Count
    }

    Copy-Item (Join-Path $sourcePublish 'wwwroot') $sandbox -Recurse -Force
    Write-Host 'Restored the compressed variants.'

    # --- 3: a file nobody named ---------------------------------------------------------------
    $victim = Get-ChildItem (Join-Path $sandboxWeb '_framework') -File -Filter '*.wasm' |
        Sort-Object Name |
        Select-Object -First 1
    $orphan = Join-Path $victim.DirectoryName 'FuzzyRegex.probeorphan.wasm'
    Copy-Item $victim.FullName $orphan
    Write-Host "Added an orphan: $(Split-Path -Leaf $orphan)."

    $withOrphan = Invoke-Check
    $results['orphan'] = @{
        Fired = ($withOrphan.ExitCode -ne 0 -and -not $withOrphan.Green -and
            $withOrphan.Output -match 'are not named by the manifest')
        Exit  = $withOrphan.ExitCode
    }

    Remove-Item $orphan -Force

    # --- 4: the closing control ----------------------------------------------------------------
    $restored = Invoke-Check
    $results['restored'] = @{ Green = ($restored.ExitCode -eq 0 -and $restored.Green) }
}
finally {
    Remove-Item $sandbox -Recurse -Force
}

Write-Host ''
if ($results['compressed'].Fired -and $results['orphan'].Fired -and $results['restored'].Green) {
    Write-Host ("BOTH ARTEFACT CHECKS FIRED: $($results['compressed'].Deleted) compressed variants " +
        "deleted gives exit $($results['compressed'].Exit) and the missing wording; one orphan file " +
        "gives exit $($results['orphan'].Exit) and the orphan wording; the copy checks GREEN again " +
        'once both are undone.')
    exit 0
}

Write-Host ("A CHECK DID NOT FIRE: compressed $($results['compressed'].Fired) " +
    "(exit $($results['compressed'].Exit)), orphan $($results['orphan'].Fired) " +
    "(exit $($results['orphan'].Exit)), restored-GREEN $($results['restored'].Green)")
exit 1
