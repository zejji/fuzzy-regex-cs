<#
    tools/build-demo-help.ps1 writes the demo's help panels, and the page validates what it reads
    with `isHelp` in demo/web/src/lib/shapes.ts - so a shape the validator rejects is a page with no
    help at all and a console error, which is how this test came to exist (S72, 2026-09-19): every
    section whose paragraph held exactly one run serialised that run as an object rather than a
    list, because PowerShell unrolls a single-element array on a function's return.

    The unit tests over the page stub help.json, so only a test over the real generator can catch
    this. It asserts the shape, not the prose: the prose lives in docs/COMPARISON.md and is meant to
    change.
#>

BeforeAll {
    $script:ScriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'build-demo-help.ps1'

    # Every heading the script's map names, because it refuses to write anything at all while one is
    # missing. Only the first carries a body worth asserting on: a paragraph of exactly one run, a
    # fenced block, and a paragraph split into several runs - the three shapes, and the one-run
    # cases are where the unrolling bit.
    $script:Markdown = @'
# Comparison

### `{e<=n}`: allow up to `n` errors of any kind

One sentence with no code in it at all.

```csharp
var m = FuzzyRegex.Match("x");
```

Another paragraph, this one with `code` in the middle of it.

### `{s,i,d,e}`: separate budgets per kind of error

Body.

### Cost forms: `{Ni+Md<n}` weights errors instead of just counting them

Body.

### `FuzzyRegexOptions.BestMatch` / `(?b)`: rank by the best fuzzy match, not the first

Body.

### `FuzzyRegexOptions.EnhanceMatch` / `(?e)`: tighten a match after it is found

Body.

### `\L<name>`: fuzzy matching against a named list of words

Body.

### `FuzzyRegexOptions.Posix` / `(?p)`: leftmost-longest instead of leftmost-first

Body.

### Partial matching: `partial: true` means "so far, so good"

Body.

### `FuzzyRegexOptions.RightToLeft` / `(?r)`: search from the right

Body.

### Replacement templates speak upstream's language

Body.

### A per-call `timeout` on every input-dependent method

Body.

## Something else
'@
}

Describe 'build-demo-help.ps1' {
    BeforeEach {
        $script:Work = Join-Path ([System.IO.Path]::GetTempPath()) "help-probe-$([guid]::NewGuid().ToString('n'))"
        New-Item -ItemType Directory -Path $Work | Out-Null
        $script:Source = Join-Path $Work 'COMPARISON.md'
        $script:Output = Join-Path $Work 'help.json'
    }

    AfterEach {
        Remove-Item -LiteralPath $Work -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'writes a section whose single-run heading and single-run paragraph are still lists' {
        Set-Content -LiteralPath $Source -Value $Markdown -Encoding utf8

        # Not $output: PowerShell variable names are case-insensitive, so that would overwrite the
        # $Output path this test then reads back.
        $report = & pwsh -NoProfile -File $ScriptPath -Comparison $Source -Destination $Output 2>&1 | Out-String
        $report | Should -Match 'GREEN'

        $json = Get-Content -LiteralPath $Output -Raw

        # -NoEnumerate so that PowerShell's own unrolling cannot mask the very defect being tested:
        # without it, a one-element array read back through the pipeline looks like a scalar too.
        $help = $json | ConvertFrom-Json -NoEnumerate
        $section = $help.entries.fuzzy[0]

        # The JSON contract, as demo/web/src/lib/shapes.ts reads it: heading is a list of runs,
        # blocks is a list, and every paragraph's runs is a list. Tested with `-is [array]` and not
        # by piping into `Should -BeOfType`, because the pipeline unrolls the array and would test
        # each run instead of the list holding them.
        ($section.heading -is [array]) | Should -BeTrue -Because 'isHelp requires heading to be an array of runs'
        ($section.blocks -is [array]) | Should -BeTrue

        $paragraphs = @($section.blocks | Where-Object { $_.kind -eq 'paragraph' })
        $paragraphs.Count | Should -Be 2
        ($paragraphs[0].runs -is [array]) | Should -BeTrue -Because 'a paragraph with one run is still a paragraph with a list of runs'
        $paragraphs[0].runs.Count | Should -Be 1
        $paragraphs[0].runs[0].text | Should -Be 'One sentence with no code in it at all.'
        $paragraphs[0].runs[0].code | Should -BeFalse

        # The other one-run case, and the one the real file hits: a heading with no code span in it
        # at all is still a list of one run.
        $plainHeading = $help.entries.replace[0].heading
        ($plainHeading -is [array]) | Should -BeTrue -Because 'a heading with no code span is still a list of runs'
        $plainHeading[0].text | Should -Be "Replacement templates speak upstream's language"
    }

    It 'names the key and the heading it could not find, and exits 1' {
        Set-Content -LiteralPath $Source -Value "# Comparison`n`n### A heading nothing maps to`n`nbody`n" -Encoding utf8

        $report = & pwsh -NoProfile -File $ScriptPath -Comparison $Source -Destination $Output 2>&1 | Out-String

        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'FAILED'
        $report | Should -Match "key 'bestmatch' wants a section headed"
    }
}
