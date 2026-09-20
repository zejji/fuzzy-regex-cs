# Date each of S57B's twenty gate rows by replaying them at every engine commit since S52's close.
#
# S57's sitting 3 proposed a 6000-row wave per bisect step. This replays the twenty rows THEMSELVES
# instead, and it is both cheaper and sharper:
#
#   * cheaper - about 20 seconds a step against about four minutes, because 20 rows are compared
#     rather than 126,080;
#   * sharper - the recorder changed after S52 closed (S52c's metamorphic invariants, S53b), so a
#     seed does not draw the same wave it drew then and a 6000-row step would be dating a row
#     against a wave that no longer contains it. The row is handed in, so every step asks about the
#     SAME twenty rows.
#
# `-Rows` re-records upstream's side at every step from the installed `regex` module, which does not
# change between steps, so the only thing varying is this port.
#
# Each step prints the run's own classification per row - DIVERGE, or EXPECTED and the entry that
# accounted for it, or neither, which means the two engines agreed. A row that agrees at one commit
# and diverges at the next is a regression introduced there.
#
#   pwsh -File tools/probes/s57b-date-the-rows.ps1
#
# Written by S57b, 2026-09-20.

param(
    [string]$Worktree = '.claude/worktrees/s57b-date',
    [string]$Rows = 'tools/probes/s57b-gate-rows.jsonl',
    # A comma-separated list of commits to walk instead of the whole range, for re-checking one
    # transition without paying for the other fifteen steps.
    [string]$Only
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$rowsPath = Join-Path $repoRoot $Rows
$worktreePath = Join-Path $repoRoot $Worktree

# Oldest first, so the first step at which a row turns red is the commit that introduced it.
#
# The S52 close is the FLOOR and it is walked first, because its reading is the baseline every later
# step is compared against. It has to be named explicitly: `b678aeb` is the slice-closing commit and
# touches no engine file, so `git log b678aeb..HEAD -- src/FuzzyRegex` leaves it out and the walk
# would start at S52b with nothing to compare against.
#
# `[array]` is load-bearing. PowerShell unwraps a one-element array out of an `if`, so `-Only` with
# a single commit otherwise hands back a bare string and `$commits.Count` throws under strict mode.
$floor = 'b678aeb'
[array]$commits = if ($Only) {
    $Only.Split(',') | Where-Object { $_.Trim() } | ForEach-Object {
        git -C $repoRoot log -1 --format='%h %s' $_.Trim()
    }
}
else {
    git -C $repoRoot log -1 --format='%h %s' $floor
    git -C $repoRoot log --reverse --format='%h %s' "$floor..HEAD" -- src/FuzzyRegex
}

Write-Host ("Dating $($commits.Count) step(s): " + $(if ($Only) { "just $Only." }
    else { "the S52 close ($floor) and every engine commit since." }))

foreach ($line in $commits) {
    $sha = $line.Split(' ')[0]
    $subject = $line.Substring($sha.Length + 1)

    git -C $worktreePath checkout --detach --force --quiet $sha
    # `--quiet` BEFORE the path. After it, git reads it as a pathspec and prints
    # "error: pathspec '--quiet' did not match any file(s) known to git" at every step.
    git -C $worktreePath submodule update --init --quiet upstream

    $log = Join-Path $repoRoot ".scratch/s57b-date-$sha.txt"
    pwsh -NoProfile -File (Join-Path $worktreePath 'tools/run-oracle.ps1') -Rows $rowsPath `
        *> $log

    $text = Get-Content -Raw -Path $log
    $summary = if ($text -match 'agree (\d+).*?expected (\d+).*?diverge (\d+)\s+of (\d+) rows') {
        "agree $($Matches[1])  expected $($Matches[2])  diverge $($Matches[3]) of $($Matches[4])"
    }
    elseif ($text -match 'Build failed with exit code') {
        # Not every commit builds in isolation. `20d0f59` declares the public surface one commit
        # before the code that adds it, so RS0017 fails the build there. A step that cannot build
        # says so; it dates nothing, and it is not a row changing state.
        'DID NOT BUILD at this commit - dates nothing'
    }
    else {
        'NO SUMMARY LINE - read the log'
    }

    $diverging = [regex]::Matches($text, '(?m)^DIVERGE row (\d+) ') |
        ForEach-Object { [int]$_.Groups[1].Value } | Sort-Object -Unique
    $expected = [regex]::Matches($text, '(?m)^EXPECTED (\S+) row (\d+) ') |
        ForEach-Object { "$($_.Groups[2].Value)=$($_.Groups[1].Value)" }

    Write-Host ''
    Write-Host "=== $sha  $subject"
    Write-Host "    $summary"
    Write-Host "    DIVERGE  $($diverging -join ',')"
    Write-Host "    EXPECTED $($expected -join '  ')"
}
