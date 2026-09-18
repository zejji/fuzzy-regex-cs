<#
.SYNOPSIS
    Publishes demo/FuzzyRegex.Demo.Wasm for the browser and asserts the artefact set, rather than
    eyeballing it.

.DESCRIPTION
    S70, the engine half of the Phase 9 demo. This is the publish-side gate; the browser-side one
    is demo/FuzzyRegex.Demo.Wasm/wwwroot/harness.html, driven in a real browser.

    What it checks, and why each one is here rather than assumed:

      - the publish succeeds and its output carries no `warning <CODE>` line. Directory.Build.props
        sets TreatWarningsAsErrors, so a trim warning from PublishTrimmed already fails the publish
        - but that promotes compiler warnings, not every category MSBuild can emit, so the output
        is scanned rather than the exit code trusted to stand for it. This one, and only this one,
        is skipped by -SkipPublish;
      - _framework/ exists and is not empty, and _framework/dotnet.js is present. That is the file
        worker.js imports, so its absence is the difference between a demo and a blank page;
      - .nojekyll is at the web root. Without it GitHub Pages runs Jekyll, Jekyll drops every
        underscore-prefixed directory, and the app serves a 404 for its own runtime;
      - worker.js and harness.html reached the web root;
      - every file the manifest names is on disk, the compressed .br and .gz variants included.
        Those are what a visitor downloads, and checking only the uncompressed ones let 41 of 42 of
        them be deleted with the script still GREEN (measured, S70 sitting 4);
      - every `integrity` entry in the static web assets manifest matches a SHA-256 recomputed from
        the file on disk. A published app whose hashes do not match its files fails at boot with a
        subresource-integrity error and no useful message;
      - nothing is on disk that the manifest does not name. Stale output from an earlier build has
        a different fingerprint in its name, so it accumulates rather than being overwritten, and
        it would be counted into the size baseline printed below;
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

.PARAMETER OutDir
    Publish to this directory instead of the project's default bin/<Configuration>/net10.0/publish.
    The clean step then clears obj/ and this directory, and leaves bin/ alone.

    This is not a convenience. The default publish directory can be held open by something outside
    this repository - a static file server serving the demo has it as its working directory, which
    is exactly how it gets served - and Windows will not delete a directory in that state. Without
    somewhere else to publish, the only way to get a clean publish is to kill another process's
    server, and the zero-trim-warning claim is the one claim -SkipClean cannot make. Clearing obj/
    is what forces the linker to run again and re-emit its warnings; bin/ holds no linker state.

.EXAMPLE
    tools/run-wasm-smoke.ps1
    tools/run-wasm-smoke.ps1 -SkipClean
    tools/run-wasm-smoke.ps1 -SkipPublish
    tools/run-wasm-smoke.ps1 -OutDir .scratch/wasm-clean-publish
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipClean,
    [switch]$SkipPublish,
    [string]$OutDir
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

$useOutDir = -not [string]::IsNullOrWhiteSpace($OutDir)
if ($useOutDir) {
    $publishDir = if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $repoRoot $OutDir }
    $publishDir = [System.IO.Path]::GetFullPath($publishDir)

    # The clean step deletes this directory, so refuse anything whose loss would matter. Two gates,
    # because the first one alone is not enough: it stops -OutDir . and -OutDir .., and the review
    # of this diff showed -OutDir tools sailing through it to delete the directory this script is
    # in. Anything git tracks is work; anything it does not (.scratch/, a path outside the repo) is
    # disposable, which is exactly the distinction wanted here.
    $separator = [System.IO.Path]::DirectorySeparatorChar
    $normalisedRoot = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/')
    $normalisedOut = $publishDir.TrimEnd('\', '/')
    $comparison = [System.StringComparison]::OrdinalIgnoreCase

    if ($normalisedOut.Equals($normalisedRoot, $comparison) -or
        $normalisedRoot.StartsWith($normalisedOut + $separator, $comparison)) {
        throw "-OutDir $publishDir contains the repository. This script deletes that directory."
    }

    # git is asked only about a path inside the repository. Outside it there is nothing tracked to
    # lose, and `git ls-files` on such a path exits 128 with "is outside repository" - which would
    # otherwise make the safest possible -OutDir the one this refused.
    $insideRepo = $normalisedOut.StartsWith($normalisedRoot + $separator, $comparison)
    if ($insideRepo -and (Test-Path $publishDir)) {
        $tracked = @(git -C $repoRoot ls-files --cached -- $publishDir)
        if ($LASTEXITCODE -ne 0) {
            throw "Could not ask git whether $publishDir holds tracked files, and this script " +
            'deletes that directory. Refusing rather than guessing.'
        }
        if ($tracked.Count -gt 0) {
            throw "-OutDir $publishDir holds $($tracked.Count) git-tracked file(s), starting with " +
            "$($tracked[0]). This script deletes that directory."
        }
    }
}
else {
    $publishDir = Join-Path $project "bin/$Configuration/net10.0/publish"
}
$webRoot = Join-Path $publishDir 'wwwroot'

if ($SkipPublish) {
    Write-Host 'Checking the publish already on disk. This run asserts nothing about the build.'
}
else {
    if (-not $SkipClean) {
        # obj/ is what has to go: the linker's warnings are re-emitted only when it runs again. bin/
        # is cleared too in the default case because that is where the publish lands, but with
        # -OutDir the publish lands elsewhere and bin/ may be held open by something serving it.
        $toClear = if ($useOutDir) { @((Join-Path $project 'obj'), $publishDir) }
        else { @((Join-Path $project 'obj'), (Join-Path $project 'bin')) }
        Write-Host "Clearing $($toClear -join ' and ') so the publish re-emits any trim warning it has."
        foreach ($dir in $toClear) {
            if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
        }
    }

    Write-Host "Publishing the browser demo ($Configuration) to $publishDir."

    # The output is captured, not streamed, so that the zero-warning claim can be CHECKED rather
    # than inferred. Directory.Build.props promotes compiler warnings to errors, which is why a
    # non-zero exit was treated as enough - but TreatWarningsAsErrors does not promote every
    # category MSBuild can emit, so a warning that escapes it would have left this script printing
    # GREEN over a publish whose own description says it has none.
    $publishOutput = if ($useOutDir) {
        dotnet publish $project --configuration $Configuration -nodeReuse:false --output $publishDir 2>&1
    }
    else {
        dotnet publish $project --configuration $Configuration -nodeReuse:false 2>&1
    }
    $publishExit = $LASTEXITCODE
    $publishOutput | ForEach-Object { Write-Host $_ }
    if ($publishExit -ne 0) {
        throw 'The WebAssembly publish of the browser demo failed.'
    }

    # The MSBuild/compiler shape, `warning IL2026:` or `warning MSB3277:`, rather than the word on
    # its own: the emscripten command lines this publish prints are full of prose that is not a
    # diagnostic.
    $publishWarnings = @($publishOutput |
            Where-Object { $_ -match '\bwarning\s+[A-Za-z]{2,}\d+' })
    if ($publishWarnings.Count -gt 0) {
        $publishWarnings | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw "The publish emitted $($publishWarnings.Count) warning(s). Trimming this app is the " +
        'reason it is published trimmed, so a trim warning is a failed publish.'
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
$named = @{}

foreach ($endpoint in $endpoints) {
    $claimed = $endpoint.EndpointProperties |
        Where-Object { $_.Name -eq 'integrity' } |
        Select-Object -ExpandProperty Value -First 1
    if (-not $claimed) { continue }

    # A manifest entry whose file is not on disk is a missing artefact, not an entry to skip. It
    # was skipped until the S70 review pointed out what that costs: every .wasm could be deleted
    # from the publish and this script still printed GREEN, because the integrity floor below was
    # met by the assets that remained.
    #
    # This existence check runs BEFORE the compressed-variant skip below, and the order is the
    # whole point. With the skip first - which is how sitting 3 left it - 41 of the 42 .br and .gz
    # files could be deleted and the script still printed GREEN, with the gzip wire figure it
    # prints collapsing from 2,076,631 bytes to 3,796 and nothing objecting (measured, sitting 4).
    # Those files are what a visitor actually downloads.
    $file = Join-Path $webRoot $endpoint.AssetFile
    $named[$file] = $true
    if (-not (Test-Path $file)) {
        $missing += $endpoint.AssetFile
        continue
    }

    # The manifest also describes the .br and .gz variants of each asset, and their `integrity` is
    # the hash of the UNCOMPRESSED file, because subresource integrity is defined over the
    # representation the browser ends up with rather than over the bytes on the wire. Measured
    # 2026-09-18: comparing those entries against the compressed file on disk reports 84 false
    # mismatches. Each compressed variant shares its hash with the plain endpoint checked below,
    # so skipping the HASH loses no coverage. Its existence is checked above.
    if ($endpoint.AssetFile -match '\.(br|gz)$') { continue }

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

# --- files on disk the manifest never named -----------------------------------------------------

# The loop above asks "is every file the manifest names on disk?". This asks the other direction,
# which is not the same question and is the one the size baseline depends on: a file nobody named
# is still counted in the bytes printed below, and the figure goes in the slice notes as the demo's
# baseline. With -SkipClean or a re-used -OutDir it is not hypothetical - the fingerprint in an
# asset's name changes every build, so a stale FuzzyRegex.<old>.wasm simply sits there. Measured in
# sitting 4: one duplicated .wasm took a GREEN run from 22 files and 6.94 MB to 23 and 8.36 MB.
$onDisk = @(Get-ChildItem $webRoot -File -Recurse)
$orphans = @($onDisk | Where-Object { -not $named.ContainsKey($_.FullName) })
if ($orphans.Count -gt 0) {
    $orphans | ForEach-Object {
        Write-Host "not named by the manifest: $($_.FullName.Substring($webRoot.Length + 1))" -ForegroundColor Red
    }
    throw "$($orphans.Count) file(s) in the publish are not named by the manifest. They are " +
    'either stale output from an earlier build or something this script does not understand; ' +
    'either way the size baseline below would be wrong.'
}

# --- the size baseline ------------------------------------------------------------------------

$served = @($onDisk | Where-Object { $_.Extension -notin @('.br', '.gz') })
# Measure-Object over an empty pipeline returns nothing at all, and `.Sum` on $null throws under
# Set-StrictMode -Version Latest - so an empty set died here with a PowerShell internal error
# instead of a diagnostic, after every check above had passed.
$measured = $served | Measure-Object -Property Length -Sum
$totalBytes = if ($null -eq $measured) { 0L } else { [int64]$measured.Sum }
$gzMeasured = @($onDisk | Where-Object { $_.Extension -eq '.gz' }) |
    Measure-Object -Property Length -Sum
$gzBytes = if ($null -eq $gzMeasured) { 0L } else { [int64]$gzMeasured.Sum }

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
# Which directory this run is about. With -OutDir the answer is not the project's default, and a
# log that does not say so cannot be told apart from one that checked a stale publish.
Write-Host "checked: $webRoot"
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
