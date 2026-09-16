<#
.SYNOPSIS
    Adds every public symbol the PublicApiAnalyzers report as undeclared (RS0016) to
    src/FuzzyRegex/PublicAPI.Unshipped.txt, then rebuilds to prove the file is complete.

.DESCRIPTION
    The RS0016 message quotes the exact line the analyzer expects ("Symbol '<line>' is not part
    of the declared public API"), so the file is regenerated from the build output; no IDE code
    fix is needed. 'dotnet format analyzers --diagnostics RS0016' did the same on 2026-09-16 once
    and silently did nothing the second time, which is why this exists. Removed symbols (RS0017)
    are reported and left for a human: deleting public API is a decision, not a regeneration.

.EXAMPLE
    pwsh -File tools/update-public-api.ps1
#>
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo 'src/FuzzyRegex/FuzzyRegex.csproj'
$file = Join-Path $repo 'src/FuzzyRegex/PublicAPI.Unshipped.txt'

$out = dotnet build $proj -c Release --nologo -v q 2>&1 | Out-String
$added = [regex]::Matches($out, "RS0016: Symbol '([^']+)' is not part") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$removed = [regex]::Matches($out, "RS0017: Symbol '([^']+)'") | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

$existing = Get-Content $file
# A symbol that is gone but was only ever UNSHIPPED is a pre-release change of mind, and is dropped
# here with a note; one that is in PublicAPI.Shipped.txt is a breaking change and stays for a human.
$shipped = Get-Content (Join-Path $repo 'src/FuzzyRegex/PublicAPI.Shipped.txt')
$dropUnshipped = @($removed | Where-Object { $_ -notin $shipped })
$breaking = @($removed | Where-Object { $_ -in $shipped })
if ($breaking) { Write-Host "RS0017 on SHIPPED symbols (breaking), decide by hand:" -ForegroundColor Red; $breaking | ForEach-Object { "  $_" } }
if ($dropUnshipped) { Write-Host "dropped $($dropUnshipped.Count) unshipped symbol(s) that no longer exist:"; $dropUnshipped | ForEach-Object { "  $_" } }
if (-not $added -and -not $dropUnshipped) { Write-Host "public API: nothing to change ($(($existing | Where-Object { $_ -and $_ -notmatch '^#' }).Count) symbols declared)"; exit ([int][bool]$breaking) }

$body = @($existing | Where-Object { $_ -and $_ -notmatch '^#' -and $_ -notin $dropUnshipped }) + @($added) | Sort-Object -Unique
$lines = @($existing | Where-Object { $_ -match '^#' }) + $body
# LF line endings, BOM as the analyzer's own code fix writes it; the repo is `* text=auto eol=lf`.
[System.IO.File]::WriteAllText($file, (($lines -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding $true))
if ($added) { Write-Host "public API: added $($added.Count) symbol(s):"; $added | ForEach-Object { "  $_" } }

$verify = dotnet build $proj -c Release --nologo -v q 2>&1 | Out-String
if ($verify -match 'RS00\d\d') { Write-Host 'still failing after regeneration:' -ForegroundColor Red; ($verify -split "`n" | Where-Object { $_ -match 'RS00' } | Select-Object -First 5); exit 1 }
Write-Host 'build green'
