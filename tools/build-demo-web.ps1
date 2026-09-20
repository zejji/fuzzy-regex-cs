<#
.SYNOPSIS
    Builds the browser demo's front end: type-check, unit tests, then the production bundle.

.DESCRIPTION
    S71. `demo/web` is a Vite + Vue 3 + TypeScript project, and `npm run build` is the whole gate:
    `vue-tsc` type-checks the page against the engine's JSON contract (src/types.ts), Vitest runs the
    unit tests for the four page modules, and only then does Vite write the bundle. A failure at any
    of the three is a non-zero exit, so there is one command to run and one thing to read.

    The bundle is written INTO demo/FuzzyRegex.Demo.Wasm/wwwroot, beside the hand-written worker.js
    and checks.html, so that the .NET wasm publish afterwards gathers page, worker and runtime into
    one static-asset manifest. That ordering is not optional: publish before build and the published
    manifest names a page from the previous build.

    What this canNOT check is anything that needs a real browser: that the runtime boots in a Web
    Worker, that terminate() kills a wedged construction, that the page keeps painting. That is
    wwwroot/checks.html, driven in a real browser, and tools/run-wasm-smoke.ps1 for the artefacts.

    It is deliberately NOT part of tools/check-ratchet.ps1 or of ci.yml: a demo failure must not
    block a library merge (the slice file's reason for a separate Pages workflow). .github/workflows/
    pages.yml runs it before it publishes anything.

.PARAMETER Node
    The node executable. Only needed when node is not on PATH.

.PARAMETER SkipInstall
    Skip `npm ci`. For a working tree whose node_modules is already in step with the lockfile; CI
    must never pass it, because `npm ci` is what pins the versions the lockfile names.

.EXAMPLE
    pwsh -File tools/build-demo-web.ps1
#>
[CmdletBinding()]
param(
    [string]$Node = 'node',
    [switch]$SkipInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$web = Join-Path $repoRoot 'demo/web'

# Resolved rather than run-and-check-$LASTEXITCODE: with $ErrorActionPreference = 'Stop', a missing
# executable throws CommandNotFoundException before any exit code exists, so an exit-code test after
# the call is unreachable and this diagnostic - the one that names the floor and .nvmrc - was never
# printed (found by the blind review, 2026-09-19).
if (-not (Get-Command $Node -ErrorAction SilentlyContinue)) {
    throw "Could not run '$Node'. The demo's front end needs Node 22.12 or newer; see demo/web/.nvmrc."
}

# Both checks are needed, and the second is not redundant: Get-Command resolves a node that exists
# but cannot run (a stub, a broken install, an .exe for the wrong architecture), and that one fails
# with an exit code and no output instead of a CommandNotFoundException.
$version = & $Node --version
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($version)) {
    throw "'$Node' did not report a version (exit $LASTEXITCODE). The demo's front end needs Node 22.12 or newer; see demo/web/.nvmrc."
}

# The floor demo/web/package.json declares in `engines`, and it is Vite 8's own: below it the build
# fails somewhere inside the toolchain with a message about a missing API rather than about Node.
# Checked here so the failure names the real cause.
$parts = $version.TrimStart('v') -split '\.'
$major = [int]$parts[0]
$minor = [int]$parts[1]
if ($major -lt 22 -or ($major -eq 22 -and $minor -lt 12)) {
    throw "Node $version is too old: demo/web needs 22.12 or newer (see demo/web/.nvmrc, which pins 24.16.0)."
}

# The help panels come out of docs/COMPARISON.md, and this is the step that fails when a heading
# there has been renamed (S72). It runs FIRST because it is the cheapest of the three gates: a
# rename should not cost an npm ci and a bundle before it is reported.
#
# Reading $LASTEXITCODE after a .ps1 is only sound because build-demo-help.ps1 exits explicitly on
# both paths - the variable is written by a native command or by `exit` and by nothing else, so a
# script that ran off its end would leave the `node --version` code above standing here instead.
# tools/tests/BuildDemoHelp.Tests.ps1 pins that contract from the calling side.
& (Join-Path $PSScriptRoot 'build-demo-help.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Generating the demo help from docs/COMPARISON.md failed.' }

Write-Host "Building the demo's front end with node $version from $web."
Push-Location $web
try {
    if (-not $SkipInstall) {
        # `npm ci` and never `npm install`: ci installs exactly what package-lock.json names and
        # fails if the lockfile and package.json disagree, which is the property that makes a build
        # here and a build on the runner the same build.
        & npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
    }

    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'The demo front-end build failed (type-check, unit tests or bundle).' }
}
finally {
    Pop-Location
}

# The build is only useful if it landed where the .NET publish will look, and `vite build` reports
# success for a configured outDir nobody has checked. Both artefacts, by name.
$page = Join-Path $repoRoot 'demo/FuzzyRegex.Demo.Wasm/wwwroot/index.html'
$assets = Join-Path $repoRoot 'demo/FuzzyRegex.Demo.Wasm/wwwroot/assets'
if (-not (Test-Path $page)) { throw "The build did not write $page." }
if (-not (Test-Path $assets)) { throw "The build did not write $assets." }

Write-Host 'DEMO WEB BUILD GREEN.'
