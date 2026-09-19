<#
.SYNOPSIS
    Generates the browser demo's contextual help (`demo/.../wwwroot/help.json`) from
    `docs/COMPARISON.md`.

.DESCRIPTION
    The demo's help panels and the user documentation say the same thing because they ARE the same
    thing: this script lifts named sections out of `docs/COMPARISON.md` and writes them as the JSON
    the page reads, keyed by the same feature key `examples.json` uses. Nothing under `demo/`
    carries a second copy of the prose (S72).

    It FAILS when a heading it was told to extract is not in the file. That is the point: rename a
    heading in COMPARISON.md and the demo build goes red on the pull request that renames it,
    instead of the panel silently going blank on the deployed page. Run it in CI as well as at
    publish time for exactly that reason.

    The output is a tree of blocks, not markdown: paragraphs split into plain and `code` runs, and
    fenced code blocks kept whole. The page renders it with Vue's text interpolation, so no part of
    this file can inject markup into the page and the demo needs no markdown renderer.

    help.json is GENERATED, never committed - `demo/**/wwwroot/help.json` is gitignored. A generated
    file that is also committed is how a line-ending conversion quietly breaks the WebAssembly
    integrity check (S71), and a stale committed copy would defeat the fail-on-rename guarantee.

.PARAMETER Comparison
    The markdown file to read. Defaults to `docs/COMPARISON.md`; a scratch copy is how the
    fail-on-rename behaviour is demonstrated.

.PARAMETER Destination
    Where to write help.json. Defaults to the demo project's wwwroot.

.EXAMPLE
    pwsh -File tools/build-demo-help.ps1

.EXAMPLE
    pwsh -File tools/build-demo-help.ps1 -Comparison .scratch/renamed.md
    # Proves the failure path: reports the key and the heading it could not find, exit code 1.
#>
param(
    [string]$Comparison,
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if (-not $Comparison) { $Comparison = Join-Path $repo 'docs/COMPARISON.md' }
if (-not $Destination) { $Destination = Join-Path $repo 'demo/FuzzyRegex.Demo.Wasm/wwwroot/help.json' }

# The map from a sample's feature key to the sections of COMPARISON.md that explain it. This is the
# whole contract between the documentation and the demo: every key `examples.json` uses appears
# here, and every heading here must exist in COMPARISON.md or the build stops.
#
# A key may name several headings - "fuzzy" is three, because the three budget forms are three
# sections there - and they are rendered in the order written here.
$map = [ordered]@{
    fuzzy        = @(
        '### `{e<=n}`: allow up to `n` errors of any kind',
        '### `{s,i,d,e}`: separate budgets per kind of error',
        '### Cost forms: `{Ni+Md<n}` weights errors instead of just counting them'
    )
    bestmatch    = @('### `FuzzyRegexOptions.BestMatch` / `(?b)`: rank by the best fuzzy match, not the first')
    enhancematch = @('### `FuzzyRegexOptions.EnhanceMatch` / `(?e)`: tighten a match after it is found')
    namedlists   = @('### `\L<name>`: fuzzy matching against a named list of words')
    posix        = @('### `FuzzyRegexOptions.Posix` / `(?p)`: leftmost-longest instead of leftmost-first')
    partial      = @('### Partial matching: `partial: true` means "so far, so good"')
    reverse      = @('### `FuzzyRegexOptions.RightToLeft` / `(?r)`: search from the right')
    replace      = @('### Replacement templates speak upstream''s language')
    timeout      = @('### A per-call `timeout` on every input-dependent method')
}

if (-not (Test-Path -LiteralPath $Comparison)) {
    throw "build-demo-help: cannot read '$Comparison'. The demo's help is generated from it, so there is nothing to generate."
}

$lines = [System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $Comparison))

<#
.SYNOPSIS
    The body lines of one section: everything after its heading, up to the next heading of the same
    or a higher level.
#>
function Get-Section {
    param([string[]]$Lines, [string]$Heading)

    $start = -1
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        if ($Lines[$i].TrimEnd() -eq $Heading) { $start = $i; break }
    }

    if ($start -lt 0) { return $null }

    # "### " is level 3, so the section ends at the next "### " or "## " - and never inside a fenced
    # code block, where a C# comment could begin with a hash.
    $level = ($Heading -split ' ')[0].Length
    $body = [System.Collections.Generic.List[string]]::new()
    $fenced = $false
    for ($i = $start + 1; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]
        if ($line.TrimEnd().StartsWith('```')) { $fenced = -not $fenced }
        if (-not $fenced -and $line -match '^(#{1,6}) ') {
            if ($Matches[1].Length -le $level) { break }
        }
        $body.Add($line)
    }

    return , $body.ToArray()
}

<#
.SYNOPSIS
    Splits one paragraph into runs of plain text and `inline code`, so the page can render code
    spans without a markdown renderer and without any HTML.
#>
function Split-Runs {
    param([string]$Text)

    $runs = @()
    foreach ($piece in [regex]::Split($Text, '(`[^`]+`)')) {
        if (-not $piece) { continue }
        if ($piece.StartsWith('`') -and $piece.EndsWith('`') -and $piece.Length -ge 2) {
            $runs += [ordered]@{ code = $true; text = $piece.Trim('`') }
        }
        else {
            # Markdown's bold markers would otherwise reach the page as literal asterisks. The demo
            # renders no emphasis, so they are simply dropped; the words survive.
            $runs += [ordered]@{ code = $false; text = $piece.Replace('**', '') }
        }
    }

    return @($runs)
}

<#
.SYNOPSIS
    Turns a section's markdown body into the block list the page renders: paragraphs and fenced
    code, in order.
#>
function ConvertTo-Blocks {
    param([string[]]$Body)

    $blocks = @()
    $paragraph = [System.Collections.Generic.List[string]]::new()
    $code = $null
    $language = ''

    foreach ($line in $Body) {
        if ($line.TrimEnd().StartsWith('```')) {
            if ($null -eq $code) {
                if ($paragraph.Count -gt 0) {
                    $blocks += , [ordered]@{ kind = 'paragraph'; runs = Split-Runs ($paragraph -join ' ') }
                    $paragraph.Clear()
                }
                $language = $line.TrimEnd().Substring(3).Trim()
                $code = [System.Collections.Generic.List[string]]::new()
            }
            else {
                $blocks += , [ordered]@{ kind = 'code'; language = $language; text = ($code -join "`n") }
                $code = $null
            }
            continue
        }

        if ($null -ne $code) { $code.Add($line); continue }

        if ([string]::IsNullOrWhiteSpace($line)) {
            if ($paragraph.Count -gt 0) {
                $blocks += , [ordered]@{ kind = 'paragraph'; runs = Split-Runs ($paragraph -join ' ') }
                $paragraph.Clear()
            }
            continue
        }

        $paragraph.Add($line.Trim())
    }

    if ($paragraph.Count -gt 0) {
        $blocks += , [ordered]@{ kind = 'paragraph'; runs = Split-Runs ($paragraph -join ' ') }
    }

    return @($blocks)
}

$entries = [ordered]@{}
$missing = @()

foreach ($key in $map.Keys) {
    $sections = @()
    foreach ($heading in $map[$key]) {
        $body = Get-Section -Lines $lines -Heading $heading
        if ($null -eq $body) {
            $missing += "  key '$key' wants a section headed: $heading"
            continue
        }

        # The heading itself is shown above the panel's prose, without its "### " marker.
        $sections += , [ordered]@{
            heading = Split-Runs ($heading -replace '^#{1,6} ', '')
            blocks  = ConvertTo-Blocks -Body $body
        }
    }

    $entries[$key] = $sections
}

if ($missing.Count -gt 0) {
    $source = Resolve-Path -LiteralPath $Comparison
    Write-Host "build-demo-help: FAILED - $($missing.Count) heading(s) missing from $source" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host 'Either the heading was renamed (update the map in tools/build-demo-help.ps1) or the section was deleted.' -ForegroundColor Red
    exit 1
}

$payload = [ordered]@{
    source  = 'docs/COMPARISON.md'
    note    = 'GENERATED by tools/build-demo-help.ps1. Do not edit, and do not commit: the demo build regenerates it.'
    entries = $entries
}

$json = $payload | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText($Destination, $json + "`n")

$count = ($entries.Keys | ForEach-Object { $entries[$_].Count } | Measure-Object -Sum).Sum
Write-Host "build-demo-help: GREEN - $($entries.Count) keys, $count sections -> $Destination"
