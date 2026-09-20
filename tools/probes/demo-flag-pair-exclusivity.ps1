# Which pairs of FuzzyRegexOptions members does the library refuse?
#
# S74 replaces the demo's free-text flags box with checkboxes, and a checkbox grid can offer a
# combination the engine then rejects. The slice spec names two exclusive pairs - Ascii/Unicode and
# Version0/Version1 - and says to confirm them and to find any other pair the engine rejects, so
# that every rejected pair becomes a radio group rather than an error path the control can reach.
#
# Every unordered pair of the 13 non-None members, compiled against the pattern the demo starts
# with. Reading the two `throw` sites in PatternCompiler.cs is not the same as running them: the
# encoding check tests `AllEncodings`, which includes upstream's LOCALE, and the version check runs
# against a default, so the reachable set is a question about the code as built.
#
# Run:  pwsh -File tools/probes/demo-flag-pair-exclusivity.ps1
#
# Recorded 2026-09-20, FuzzyRegex at this worktree's HEAD. See docs/plan/slices/S74-flags-control.md.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$assembly = Join-Path $repo 'src/FuzzyRegex/bin/Release/net10.0/FuzzyRegex.dll'

if (-not (Test-Path -LiteralPath $assembly)) {
    throw "demo-flag-pair-exclusivity: no assembly at '$assembly'. Run: dotnet build src/FuzzyRegex/FuzzyRegex.csproj -c Release"
}

Add-Type -LiteralPath $assembly

$optionsType = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]
$names = [System.Enum]::GetNames($optionsType) | Where-Object { $_ -ne 'None' }

Write-Output "FuzzyRegexOptions: $($names.Count) members besides None"
Write-Output ''

# A pattern with no inline flags and no syntax that any one flag alone rejects, so a refusal is
# about the combination and not about the pattern.
$pattern = 'ab'

function Test-Options {
    param([string[]] $Names)

    $value = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]::None
    foreach ($name in $Names) {
        $value = $value -bor [System.Enum]::Parse($optionsType, $name)
    }

    try {
        [void][Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern, $value)
        return $null
    } catch {
        $inner = $_.Exception
        return "$($inner.GetType().Name): $($inner.Message)"
    }
}

Write-Output '1. Each member on its own'
foreach ($name in $names) {
    $refusal = Test-Options -Names @($name)
    if ($refusal) { Write-Output "  REJECTED $name -> $refusal" }
}
Write-Output "  (nothing above means every member is legal alone)"

Write-Output ''
Write-Output '2. Every unordered pair'
$rejected = 0
$tested = 0
for ($i = 0; $i -lt $names.Count; $i++) {
    for ($j = $i + 1; $j -lt $names.Count; $j++) {
        $tested++
        $refusal = Test-Options -Names @($names[$i], $names[$j])
        if ($refusal) {
            $rejected++
            Write-Output "  REJECTED $($names[$i]) + $($names[$j]) -> $refusal"
        }
    }
}

Write-Output ''
Write-Output "  $tested pairs tested, $rejected rejected"

Write-Output ''
Write-Output '3. Every member at once, minus one side of each rejected pair'
$all = $names | Where-Object { $_ -notin @('Ascii', 'Version0') }
$refusal = Test-Options -Names $all
if ($refusal) { Write-Output "  REJECTED $($all -join '|') -> $refusal" }
else { Write-Output "  ACCEPTED $($all -join '|')" }

# A radio group has to show one option selected before anybody has chosen, and the honest one to
# pre-select is whichever the library already applies. Both enum doc comments claim their member is
# "already on" by default; S74's radios make that claim visible, so it is measured here rather than
# read. `Options` is the compiled pattern's own answer about what it is doing.
Write-Output ''
Write-Output '4. Does naming the default change what the pattern does?'
foreach ($case in @(
        @{ label = 'nothing named'; names = @() },
        @{ label = 'Version1 named'; names = @('Version1') },
        @{ label = 'Unicode named'; names = @('Unicode') },
        @{ label = 'both named'; names = @('Version1', 'Unicode') },
        @{ label = 'Version0 named'; names = @('Version0') },
        @{ label = 'Ascii named'; names = @('Ascii') }
    )) {
    $value = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]::None
    foreach ($name in $case.names) {
        $value = $value -bor [System.Enum]::Parse($optionsType, $name)
    }

    $compiled = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern, $value)
    Write-Output "  $($case.label.PadRight(16)) -> Options = $($compiled.Options)"
}
