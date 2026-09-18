<#
.SYNOPSIS
    Publishes demo/FuzzyRegex.Demo.Wasm for the browser and asserts the artefact set, rather than
    eyeballing it.

.DESCRIPTION
    S70, the engine half of the Phase 9 demo. This is the publish-side gate; the browser-side one
    is demo/FuzzyRegex.Demo.Wasm/wwwroot/harness.html, driven in a real browser.

    What it checks, and why each one is here rather than assumed:

      - the publish succeeds with zero warnings. Directory.Build.props sets TreatWarningsAsErrors,
        so a trim warning from PublishTrimmed is a failed publish, which is the point of publishing
        trimmed at all. This one, and only this one, is skipped by -SkipPublish;
      - _framework/ exists and is not empty, and _framework/dotnet.js is present. That is the file
        worker.js imports, so its absence is the difference between a demo and a blank page;
      - .nojekyll is at the web root. Without it GitHub Pages runs Jekyll, Jekyll drops every
        underscore-prefixed directory, and the app serves a 404 for its own runtime;
      - worker.js and harness.html reached the web root;
      - every `integrity` entry in the static web assets manifest matches a SHA-256 recomputed from
        the file on disk. A published app whose hashes do not match its files fails at boot with a
        subresource-integrity error and no useful message;
      - the integrity check actually checked something. A scan that finds nothing must fail rather
        than pass over nothing - S53's AOT gate shipped three tests that were passing vacuously,
        and the floor below is the fix that lesson bought.

    It also prints the size baseline: total published bytes and the largest three files. Those are
    the DEMO's own numbers. They are not comparable with S53's 6.65 MB Native AOT binary - that is a
    win-x64 executable and this is an IL bundle interpreted in a browser, so it is a different
    compiler producing a different unit.

.PARAMETER Configuration
    Release by default.

.PARAMETER SkipClean
    Keep obj/ and bin/ instead of clearing them first. Faster, and weaker: an incremental
    re-publish does not re-emit trim warnings, so a run with this switch cannot claim there were
    none (S53's verifier found exactly that).

.PARAMETER SkipPublish
    Check the publish that is already on disk instead of producing a new one. This exists to make
    the missing-artefact branch testable at all: the script publishes before it checks, so a file
    deleted to exercise that branch is restored by the publish before the check can see it. With
    this switch, `Remove-Item <webRoot>/_framework/*.wasm` then a re-run is expected to throw.
    It asserts nothing about the build, so it is a check of the last publish, not a smoke run.

.EXAMPLE
    tools/run-wasm-smoke.ps1
    tools/run-wasm-smoke.ps1 -SkipClean
    tools/run-wasm-smoke.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipClean,
    [switch]$SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The hang fix, for the reason set out at the top of tools/run-aot-tests.ps1: a leftover MSBuild
# node wedges the publish indefinitely, and `-nodeReuse:false` alone does not stop this build
# joining one that is already wedged.
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'demo/FuzzyRegex.Demo.Wasm'
$publishDir = Join-Path $project "bin/$Configuration/net10.0/publish"
$webRoot = Join-Path $publishDir 'wwwroot'

if ($SkipPublish) {
    Write-Host 'Checking the publish already on disk. This run asserts nothing about the build.'
}
else {
    if (-not $SkipClean) {
        Write-Host 'Clearing obj/ and bin/ so the publish re-emits any trim warning it has.'
        foreach ($dir in @((Join-Path $project 'obj'), (Join-Path $project 'bin'))) {
            if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
        }
    }

    Write-Host "Publishing the browser demo ($Configuration)."

    dotnet publish $project --configuration $Configuration -nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        throw 'The WebAssembly publish of the browser demo failed.'
    }
}

if (-not (Test-Path $webRoot)) {
    throw "No published web root at $webRoot."
}

# --- the artefact set -------------------------------------------------------------------------

$required = @(
    '.nojekyll'
    'worker.js'
    'harness.html'
    '_framework/dotnet.js'
)
foreach ($relative in $required) {
    $path = Join-Path $webRoot $relative
    if (-not (Test-Path $path)) {
        throw "The publish is missing $relative. Nothing downstream works without it."
    }
}

$frameworkFiles = @(Get-ChildItem (Join-Path $webRoot '_framework') -File)
if ($frameworkFiles.Count -eq 0) {
    throw '_framework/ is empty. The runtime did not reach the publish output.'
}

# --- integrity --------------------------------------------------------------------------------

$manifest = Get-ChildItem $publishDir -Filter '*.staticwebassets.endpoints.json' -File |
    Select-Object -First 1
if ($null -eq $manifest) {
    throw "No *.staticwebassets.endpoints.json in $publishDir, so no integrity hashes to check."
}

$endpoints = (Get-Content $manifest.FullName -Raw | ConvertFrom-Json).Endpoints
$checked = 0
$mismatched = @()
$missing = @()
$hashes = @{}

foreach ($endpoint in $endpoints) {
    $claimed = $endpoint.EndpointProperties |
        Where-Object { $_.Name -eq 'integrity' } |
        Select-Object -ExpandProperty Value -First 1
    if (-not $claimed) { continue }

    # The manifest also describes the .br and .gz variants of each asset, and their `integrity` is
    # the hash of the UNCOMPRESSED file, because subresource integrity is defined over the
    # representation the browser ends up with rather than over the bytes on the wire. Measured
    # 2026-09-18: comparing those entries against the compressed file on disk reports 84 false
    # mismatches. Each compressed variant shares its hash with the plain endpoint checked below,
    # so skipping them loses no coverage.
    if ($endpoint.AssetFile -match '\.(br|gz)$') { continue }

    # A manifest entry whose file is not on disk is a missing artefact, not an entry to skip. It
    # was skipped until the S70 review pointed out what that costs: every .wasm could be deleted
    # from the publish and this script still printed GREEN, because the integrity floor below was
    # met by the assets that remained.
    $file = Join-Path $webRoot $endpoint.AssetFile
    if (-not (Test-Path $file)) {
        $missing += $endpoint.AssetFile
        continue
    }

    if (-not $hashes.ContainsKey($file)) {
        $bytes = [System.IO.File]::ReadAllBytes($file)
        $sha = [System.Security.Cryptography.SHA256]::HashData($bytes)
        $hashes[$file] = 'sha256-' + [Convert]::ToBase64String($sha)
    }

    $checked++
    if ($hashes[$file] -ne $claimed) {
        $mismatched += "$($endpoint.AssetFile): manifest says $claimed, the file on disk is $($hashes[$file])"
    }
}

if ($missing.Count -gt 0) {
    # Count the distinct files, not the endpoints: each asset is named by both a fingerprinted route
    # and a plain one, so one deleted file was reported as "2 file(s)" until this was made unique.
    $missingFiles = @($missing | Select-Object -Unique)
    $missingFiles | ForEach-Object { Write-Host "missing: $_" -ForegroundColor Red }
    throw "$($missingFiles.Count) file(s) named by the manifest are not in the publish. The app cannot boot without them."
}

if ($mismatched.Count -gt 0) {
    $mismatched | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    throw "$($mismatched.Count) published file(s) do not match their integrity hash. The app will refuse to boot."
}

# The non-vacuity floor. A real publish of this project checks 44 uncompressed endpoints (measured
# 2026-09-18 - roughly 22 assets, each with a fingerprinted route and a plain one), so a run that
# checked a handful has found a manifest it can no longer read rather than a clean app, and must
# not report GREEN.
$integrityFloor = 30
if ($checked -lt $integrityFloor) {
    throw "Only $checked integrity entries were checkable, below the floor of $integrityFloor. " +
        'That is a manifest this script can no longer read, not a clean publish.'
}

# --- the size baseline ------------------------------------------------------------------------

$served = @(Get-ChildItem $webRoot -File -Recurse |
        Where-Object { $_.Extension -notin @('.br', '.gz') })
$totalBytes = ($served | Measure-Object -Property Length -Sum).Sum
$gzBytes = (Get-ChildItem $webRoot -File -Recurse -Filter '*.gz' |
        Measure-Object -Property Length -Sum).Sum

Write-Host ''
Write-Host "published: $($served.Count) files, $totalBytes bytes on disk ($([math]::Round($totalBytes / 1MB, 2)) MB)"
# GitHub Pages "doesn't natively support using Brotli-compressed resources", so the bytes a visitor
# waits for are the uncompressed ones or, where Pages negotiates gzip, these.
Write-Host "gzip variants: $gzBytes bytes ($([math]::Round($gzBytes / 1MB, 2)) MB) - the wire figure where Pages negotiates gzip; Brotli is not served by Pages"
Write-Host 'largest three:'
$served |
    Sort-Object Length -Descending |
    Select-Object -First 3 |
    ForEach-Object {
        Write-Host "  $($_.Name)  $($_.Length) bytes"
    }

Write-Host ''
Write-Host "integrity: $checked published endpoints recomputed and matched"
# The banner has to say which run this was. A -SkipPublish run asserts nothing about the build, and
# printed a verdict indistinguishable from a real one until the qualifier was added: a log tail, or
# anything grepping for the banner, could not tell a publish-and-check from a check over whatever
# happened to be on disk.
if ($SkipPublish) {
    Write-Host 'WASM SMOKE GREEN (artefacts only - nothing was published, so this says nothing about the build).'
}
else {
    Write-Host 'WASM SMOKE GREEN.'
}
