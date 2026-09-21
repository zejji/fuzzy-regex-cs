<#
.SYNOPSIS
    Prints one SHA-256 over the repository's tracked test sources.

.DESCRIPTION
    `docs/STATUS.md` is generated from a test RUN, so it goes stale the moment another test lands -
    and nothing noticed until CI regenerated the page on three runners and compared. That is what
    run 35569653546 reported on 2026-09-21: the committed page said 4,449 gap tests where the tree
    held 4,456, because a sitting regenerated the page and then wrote seven more tests before it
    committed.

    So `tools/check-ratchet.ps1` writes this stamp beside the page it generates, and the pre-commit
    hook recomputes it over the tree being committed. Different means the page describes some other
    set of tests, and the commit stops with the command that fixes it. Milliseconds, where re-running
    the suite in a hook would cost minutes on every commit.

    Tracked files only, listed by git: CI regenerates the page from what was committed, so a file
    nobody committed is not part of what the page describes. Contents come from the working tree,
    which for a commit is the same thing - the hook already refuses a partially staged `.cs` file.

.PARAMETER Root
    The repository to stamp. Defaults to this script's repository, and the tests pass a fixture.

.EXAMPLE
    pwsh -File tools/status-stamp.ps1
#>
param([string]$Root)

$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }

# Ordinal, not culture-aware: a stamp that depends on the machine's locale would differ between a
# developer's commit and CI for no reason anybody could see.
$paths = @(git -C $Root ls-files -- 'tests/*.cs') | Sort-Object -CaseSensitive

$lines = foreach ($path in $paths) {
    $file = Join-Path $Root $path
    if (-not (Test-Path -LiteralPath $file)) { continue }   # staged deletion, not yet on disk
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $path"
}

$text = ($lines -join "`n") + "`n"
$bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($text)
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    Write-Output ([System.Convert]::ToHexString($sha.ComputeHash($bytes)).ToLowerInvariant())
}
finally {
    $sha.Dispose()
}
