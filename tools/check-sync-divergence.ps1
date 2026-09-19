<#
.SYNOPSIS
    Pairs every `sync-divergence:` marker in the engine with a row in docs/plan/SYNC-DIVERGENCE.md.

.DESCRIPTION
    Phase 7 is allowed to give up upstream's shape where a measurement justifies it, provided a
    future sync-upstream slice can find the place and is told what to do there (owner ruling,
    2026-09-18 research review (c)). That only holds if the note cannot be forgotten, so this runs
    from the ratchet and fails on either half being missing:

      - a marker in src/FuzzyRegex whose file has no ledger row;
      - a ledger row naming a file that no longer carries a marker.

    The second half is the one that matters over time: a later slice that re-aligns the code and
    deletes the marker, but leaves the row, hands the sync slice a map of divergences that are not
    there any more.

    Pairing is by FILE, not by line. Line numbers move under CSharpier and under the next edit
    either side of the marker, and a check that goes red on a reformat is a check people learn to
    bypass.

.PARAMETER SourceRoot
    The tree scanned for markers. Defaults to src/FuzzyRegex.

.PARAMETER LedgerPath
    The ledger. Defaults to docs/plan/SYNC-DIVERGENCE.md.

.EXAMPLE
    tools/check-sync-divergence.ps1
#>
[CmdletBinding()]
param(
    [string]$SourceRoot = '',
    [string]$LedgerPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $SourceRoot) { $SourceRoot = Join-Path $repoRoot 'src/FuzzyRegex' }
if (-not $LedgerPath) { $LedgerPath = Join-Path $repoRoot 'docs/plan/SYNC-DIVERGENCE.md' }

if (-not (Test-Path -LiteralPath $LedgerPath)) {
    Write-Host "Sync divergence: RED - the ledger is missing ($LedgerPath)." -ForegroundColor Red
    exit 1
}

# The path a marker and a row agree on: relative to the source root, forward slashes, so the same
# file reads the same on Windows and on Linux.
function Get-RelativeSourcePath {
    param([string]$FullPath, [string]$Root)

    $rooted = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $full = [System.IO.Path]::GetFullPath($FullPath)
    return $full.Substring($rooted.Length).TrimStart('\', '/').Replace('\', '/')
}

# --- The markers ---------------------------------------------------------------------------------

$markerFiles = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
if (Test-Path -LiteralPath $SourceRoot) {
    foreach ($file in Get-ChildItem -LiteralPath $SourceRoot -Recurse -Filter '*.cs' -File) {
        if (Select-String -LiteralPath $file.FullName -Pattern 'sync-divergence:' -SimpleMatch -Quiet) {
            [void]$markerFiles.Add((Get-RelativeSourcePath -FullPath $file.FullName -Root $SourceRoot))
        }
    }
}

# --- The ledger ----------------------------------------------------------------------------------

# Only the table under "## The ledger" is read. The file carries other tables - the column guide is
# one - and parsing every pipe-delimited line would make the documentation part of the check.
$rowFiles = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$inLedger = $false
foreach ($line in [System.IO.File]::ReadAllLines($LedgerPath)) {
    if ($line -match '^##\s') {
        $inLedger = $line -match '^##\s+The ledger\s*$'
        continue
    }

    if (-not $inLedger -or -not $line.StartsWith('|')) { continue }

    $cells = $line.Trim().Trim('|').Split('|')
    $where = $cells[0]
    # Header and separator rows have no backticked path, so they fall out here rather than needing
    # to be counted and skipped.
    foreach ($match in [regex]::Matches($where, '`([^`]+)`')) {
        # `Engine/Matcher.cs:197` and a bare `:4786` continuation both appear in OPTIMISATION-NOTES;
        # the line part is dropped either way, and a continuation contributes no file.
        $token = $match.Groups[1].Value.Split(':')[0].Trim()
        if ($token.EndsWith('.cs', [System.StringComparison]::OrdinalIgnoreCase)) {
            [void]$rowFiles.Add($token.Replace('\', '/'))
        }
    }
}

# --- The two halves ------------------------------------------------------------------------------

$unrecorded = @($markerFiles | Where-Object { -not $rowFiles.Contains($_) })
$stale = @($rowFiles | Where-Object { -not $markerFiles.Contains($_) })

if ($unrecorded.Count -eq 0 -and $stale.Count -eq 0) {
    Write-Host "Sync divergence: GREEN - $($markerFiles.Count) marked file(s), all paired with a ledger row." -ForegroundColor Green
    exit 0
}

Write-Host 'Sync divergence: RED' -ForegroundColor Red
foreach ($file in $unrecorded) {
    Write-Host "  Marker with no ledger row: $file" -ForegroundColor Red
}
foreach ($file in $stale) {
    Write-Host "  Ledger row with no marker: $file" -ForegroundColor Red
}
Write-Host "  Add or remove the row in $(Get-RelativeSourcePath -FullPath $LedgerPath -Root $repoRoot), or the comment in the source." -ForegroundColor Yellow
Write-Host '  A row is: where, upstream''s shape, ours, the measured gain, how to re-align, the slice.' -ForegroundColor Yellow
exit 1
