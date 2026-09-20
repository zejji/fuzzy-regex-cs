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

### **Indices are UTF-16 code units**, not codepoints

Body.

### Version 1 is the default

Body.

## Something else
'@

    # Line endings normalised once, so a test that edits the fixture can match a two-line sequence
    # with `n and not care whether this file was checked out with CRLF.
    $script:Normalised = $Markdown.Replace("`r`n", "`n")
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

    It 'sets $LASTEXITCODE itself, so a caller does not read the last native command instead' {
        # tools/build-demo-web.ps1 calls this script in-process and then tests $LASTEXITCODE. That
        # variable is only written by a native command or by `exit`, so a script that simply runs
        # off its end leaves whatever the caller's previous native command set - and the gate then
        # reports whatever that happened to be. Called here the way the build calls it, not in a
        # child process, because a child process always has an exit code and the defect cannot
        # appear there.
        Set-Content -LiteralPath $Source -Value $Markdown -Encoding utf8

        & pwsh -NoProfile -Command 'exit 3'   # a failed native command, as a caller could have run
        $LASTEXITCODE | Should -Be 3

        & $ScriptPath -Comparison $Source -Destination $Output | Out-Null

        $LASTEXITCODE | Should -Be 0
    }

    It 'refuses a mapped section that has no body, rather than writing an empty panel' {
        # The heading is still there, so the fail-on-rename check is satisfied; what moved is the
        # prose, under a sub-heading or into another section. On screen that is a disclosure which
        # opens onto nothing, and nothing tells the difference between it and a feature nobody
        # documented - so the build stops instead (S72 review, 2026-09-19).
        $heading = '### A per-call `timeout` on every input-dependent method'
        Set-Content -LiteralPath $Source -Value $Normalised.Replace("$heading`n`nBody.", $heading) -Encoding utf8

        $report = & pwsh -NoProfile -File $ScriptPath -Comparison $Source -Destination $Output 2>&1 | Out-String

        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'FAILED'
        $report | Should -Match "key 'timeout'"
        $report | Should -Match 'no body'
    }

    It 'refuses markdown it cannot render, naming the construct and the line' -ForEach @(
        @{ What = 'a sub-heading'; Line = '#### A deeper heading'; Expected = 'heading' }
        @{ What = 'a bullet'; Line = '- a list item'; Expected = 'list item' }
        @{ What = 'a numbered item'; Line = '1. a numbered item'; Expected = 'list item' }
        @{ What = 'a link'; Line = 'See [the docs](https://example.invalid) for more.'; Expected = 'link' }
    ) {
        # Split-Runs understands backticks and `**` and nothing else, so any of these reaches the
        # panel as literal markdown - "#### A deeper heading" printed with its hashes. None is in
        # docs/COMPARISON.md's mapped sections today; the point is that adding one is a red build
        # and not a page that has quietly started showing markup to visitors.
        $plain = 'One sentence with no code in it at all.'
        Set-Content -LiteralPath $Source -Value $Normalised.Replace($plain, "$plain`n`n$Line") -Encoding utf8

        $report = & pwsh -NoProfile -File $ScriptPath -Comparison $Source -Destination $Output 2>&1 | Out-String

        $LASTEXITCODE | Should -Be 1
        $report | Should -Match 'FAILED'
        $report | Should -Match $Expected
    }

    It 'leaves a fenced code block alone, hashes, dashes, brackets and all' {
        # The guard above must not fire inside a fence: COMPARISON.md's C# samples hold comments
        # that start with a hash and expressions full of brackets, and refusing those would make
        # the check useless on the only file it is ever run against.
        $sample = 'var m = FuzzyRegex.Match("x");'
        Set-Content -LiteralPath $Source -Value $Normalised.Replace($sample, "# a comment`n- not a list`nvar m = FuzzyRegex.Match(""[a](b)"");") -Encoding utf8

        $report = & pwsh -NoProfile -File $ScriptPath -Comparison $Source -Destination $Output 2>&1 | Out-String

        $LASTEXITCODE | Should -Be 0
        $report | Should -Match 'GREEN'
    }

    It 'documents every feature the sidebar names and every panel a heading note opens, and no others' {
        # The two lists are written out twice - the map in build-demo-help.ps1 and the `features`
        # array in tests/FuzzyRegex.Tests/Gaps/Demo/DemoExamplesTests.cs - and both are checked
        # against examples.json, which is the file the page actually reads. Drop a key from either
        # side and one of the two gates goes red.
        #
        # A panel is opened by a worked example or by one of the six heading notes (S75, item 2), so
        # the map is checked against both sources. A key in neither is a panel nothing opens.
        #
        # Run against the REAL docs/COMPARISON.md, so this also proves the shipped mapping resolves.
        $repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
        $comparison = Join-Path $repo 'docs/COMPARISON.md'
        $examplesPath = Join-Path $repo 'demo/FuzzyRegex.Demo.Wasm/wwwroot/examples.json'

        $report = & pwsh -NoProfile -File $ScriptPath -Comparison $comparison -Destination $Output 2>&1 | Out-String
        $report | Should -Match 'GREEN'

        $help = Get-Content -LiteralPath $Output -Raw | ConvertFrom-Json
        $documented = @($help.entries.PSObject.Properties.Name)

        $examples = Get-Content -LiteralPath $examplesPath -Raw | ConvertFrom-Json
        # The syntax-tour rows carry no key and have no panel; the rest name the feature they show.
        # Test for the property by name: a row without it made CI's strict host throw
        # PropertyNotFoundException, first on `$_.key` (run 35519223744) and then on the `.Value` of
        # the missing bag entry (run 35522263001), both 2026-09-20. Reproduced under
        # `Set-StrictMode -Version Latest`; this form is clean there.
        $demonstrated = @($examples | ForEach-Object { if ($_.PSObject.Properties.Name -contains 'key') { $_.key } } | Where-Object { $_ } | Select-Object -Unique)

        # The keys the six heading notes link to, read out of the file the page imports. Those the
        # samples already cover are dropped, so what is left is the tail of the map: the panels that
        # exist for a heading note alone.
        $notes = Get-Content -LiteralPath (Join-Path $repo 'demo/web/src/lib/help-notes.ts') -Raw
        $linked = @([regex]::Matches($notes, "helpKey:\s*'([^']+)'") | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
        $linked | Should -Not -BeNullOrEmpty
        $headingOnly = @($linked | Where-Object { $demonstrated -notcontains $_ })

        $documented | Should -Be (@($demonstrated) + $headingOnly)
    }
}
