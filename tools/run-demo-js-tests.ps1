<#
.SYNOPSIS
    Runs the browser demo's JavaScript unit tests with node's own test runner.

.DESCRIPTION
    S71. The demo's page logic is three small modules - the URL fragment, the highlighted view and
    its display cap, and the worker pool with its warm spare - and all three are pure enough to test
    without a browser. `node --test` needs nothing installed: no bundler, no test framework, no
    package.json, which is the same reasoning that vendors Vue rather than depending on a toolchain.

    What this canNOT check is anything that needs a real browser: that the runtime boots in a Web
    Worker, that terminate() kills a wedged construction, that the page keeps painting. That is
    wwwroot/checks.html, driven in a real browser, and tools/run-wasm-smoke.ps1 for the artefacts.

    It is deliberately NOT part of tools/check-ratchet.ps1 or of ci.yml: a demo failure must not
    block a library merge (the slice file's reason for a separate Pages workflow). .github/workflows/
    pages.yml runs it before it deploys anything.

.PARAMETER Node
    The node executable. Only needed when node is not on PATH.

.EXAMPLE
    pwsh -File tools/run-demo-js-tests.ps1
#>
[CmdletBinding()]
param(
    [string]$Node = 'node'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
# A glob and not the directory: node 22 and later treat a positional argument as a glob pattern
# rather than as a directory to walk, and `node --test demo/tests` under node 24.16 fails with
# "Cannot find module ...\demo\tests" (measured 2026-09-18).
$tests = 'demo/tests/*.test.js'

$version = & $Node --version
if ($LASTEXITCODE -ne 0) {
    throw "Could not run '$Node'. Node 18 or newer provides the `node --test` runner these tests use."
}

# `node --test <dir>` was added in Node 18 and the test runner stopped being experimental in 20.
# Below that the command exits 0 having run nothing at all, which would be a green light over an
# empty run - the vacuous-pass failure tools/run-wasm-smoke.ps1's integrity floor exists to stop.
$major = [int]($version.TrimStart('v') -split '\.')[0]
if ($major -lt 20) {
    throw "Node $version is too old: `node --test` needs 18 and is only stable from 20. Found major version $major."
}

Write-Host "Running the demo's JavaScript tests with node $version from $repoRoot."
Push-Location $repoRoot
try {
    & $Node --test $tests
}
finally {
    Pop-Location
}

if ($LASTEXITCODE -ne 0) {
    throw 'The demo JavaScript tests failed.'
}

Write-Host 'DEMO JS TESTS GREEN.'
