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

# The map from a feature key to the sections of COMPARISON.md that explain it. This is the whole
# contract between the documentation and the demo: every key `examples.json` uses appears here,
# every key the heading notes link to appears here, and every heading here must exist in
# COMPARISON.md or the build stops.
#
# A key may name several headings - "fuzzy" is three, because the three budget forms are three
# sections there - and they are rendered in the order written here.
#
# The last two are opened by a heading note rather than by a sample (S75, item 2): the Subject note
# points at the UTF-16 rule and the Flags note at the version default. `help-notes.ts` holds the
# other side of that link, and BuildDemoHelp.Tests.ps1 compares the two lists with this one.
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
    indices      = @('### **Indices are UTF-16 code units**, not codepoints')
    version      = @('### Version 1 is the default')
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
    The markdown constructs a mapped section may not contain, because the page cannot render them.

.DESCRIPTION
    Split-Runs below understands exactly two things: `inline code` and `**bold**`. Anything else
    reaches the panel as literal markdown - a sub-heading printed with its hashes, a bullet with its
    dash, a link as `[text](url)` with no link in it. None of these is in the mapped sections of
    docs/COMPARISON.md today, and the job of this check is to keep it that way: adding one is then a
    red build on the pull request that adds it, rather than markup quietly appearing on the page.

    Fenced code is skipped, because a C# sample legitimately holds hashes, dashes and brackets.
#>
function Get-UnsupportedConstructs {
    param([string[]]$Body)

    $found = @()
    $fenced = $false
    for ($i = 0; $i -lt $Body.Length; $i++) {
        $line = $Body[$i]
        if ($line.TrimEnd().StartsWith('```')) { $fenced = -not $fenced; continue }
        if ($fenced) { continue }

        # A heading of the same level or higher ended the section already, so any heading left in
        # the body is a deeper one.
        if ($line -match '^\s*#{1,6} ') { $found += "a sub-heading the page cannot render, on line $($i + 1): $($line.Trim())" }
        elseif ($line -match '^\s*([-*+]|\d+\.)\s+') { $found += "a list item the page cannot render, on line $($i + 1): $($line.Trim())" }

        if ($line -match '\[[^\]]*\]\([^)]*\)') { $found += "a link the page cannot render, on line $($i + 1): $($line.Trim())" }
    }

    return , @($found)
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

    # The leading comma, as in Get-Section above: a function's return goes through the pipeline,
    # which unrolls a one-element array into the element. A paragraph of exactly one run - the
    # commonest paragraph in COMPARISON.md - then serialised as a JSON object where the page's
    # validator wants a list, and the whole help panel was rejected (S72, 2026-09-19).
    return , @($runs)
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

    # Leading comma for the same reason: a section of one paragraph is a list of one block.
    return , @($blocks)
}

$entries = [ordered]@{}
$problems = @()

foreach ($key in $map.Keys) {
    $sections = @()
    foreach ($heading in $map[$key]) {
        $body = Get-Section -Lines $lines -Heading $heading
        if ($null -eq $body) {
            $problems += "  key '$key' wants a section headed: $heading"
            continue
        }

        foreach ($construct in (Get-UnsupportedConstructs -Body $body)) {
            $problems += "  key '$key', section '$heading' contains $construct"
        }

        $blocks = ConvertTo-Blocks -Body $body

        # A heading that is still there but whose prose has moved - under a sub-heading, or into a
        # neighbouring section - would otherwise be written green and rendered as a disclosure that
        # opens onto nothing, which nobody can tell from a feature that was never documented.
        if ($blocks.Count -eq 0) {
            $problems += "  key '$key', section '$heading' has no body: the heading is there and the prose under it is not"
        }

        # The heading itself is shown above the panel's prose, without its "### " marker.
        $sections += , [ordered]@{
            heading = Split-Runs ($heading -replace '^#{1,6} ', '')
            blocks  = $blocks
        }
    }

    $entries[$key] = $sections
}

if ($problems.Count -gt 0) {
    $source = Resolve-Path -LiteralPath $Comparison
    Write-Host "build-demo-help: FAILED - $($problems.Count) problem(s) with $source" -ForegroundColor Red
    $problems | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host 'A heading may have been renamed or deleted (update the map in tools/build-demo-help.ps1), its prose may have moved, or the section may have grown markdown the demo cannot render.' -ForegroundColor Red
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

# An explicit exit on the success path too, because tools/build-demo-web.ps1 calls this script
# in-process and then reads $LASTEXITCODE. That variable is written by a native command or by
# `exit` and by nothing else, so a script that runs off its end leaves the caller reading whatever
# its own last native command set - a gate whose answer comes from somewhere else entirely.
exit 0
