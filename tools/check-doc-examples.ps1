<#
.SYNOPSIS
    Compiles every ```csharp block in README.md and docs/COMPARISON.md against src/FuzzyRegex and
    runs it, checking that what it prints is exactly the `// ` comment lines that follow the code.

.DESCRIPTION
    Documentation examples rot silently; this makes them a build. Each block becomes one scoped
    section of a generated Program.cs in .scratch/doc-examples/ (gitignored). `using` lines are
    hoisted; trailing `// text` lines in a block are its expected output. A block with no expected
    lines only has to compile. Exit 1 on any compile error or mismatch, with the block's file and
    ordinal named. Phase 8 turns this into the convention test; until then it is the gate Junie's
    docs tasks are reviewed with (first used 2026-09-16 on the README's three examples).

.EXAMPLE
    pwsh -File tools/check-doc-examples.ps1
    pwsh -File tools/check-doc-examples.ps1 -Files README.md
#>
param([string[]]$Files = @('README.md', 'docs/COMPARISON.md'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $repo '.scratch/doc-examples'
New-Item -ItemType Directory -Force $dir | Out-Null

$blocks = @()
foreach ($f in $Files) {
    $path = Join-Path $repo $f
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $text = Get-Content -LiteralPath $path -Raw
    $n = 0
    foreach ($m in [regex]::Matches($text, '```csharp\r?\n(.*?)```', 'Singleline')) {
        $n++
        $lines = $m.Groups[1].Value -split '\r?\n' | Where-Object { $_ -ne '' -or $true }
        $usings = @($lines | Where-Object { $_ -match '^using .*;\s*$' })
        $code = @($lines | Where-Object { $_ -notmatch '^using .*;\s*$' })
        # Expected output = the run of `// ` comment lines at the END of the block, or, when a
        # block has none, the `// text` trailing each Console.WriteLine line, in order.
        $expected = New-Object System.Collections.Generic.List[string]
        for ($i = $code.Count - 1; $i -ge 0; $i--) {
            if ($code[$i] -match '^\s*$') { continue }
            if ($code[$i] -match '^// ?(.*)$') { $expected.Insert(0, $Matches[1]) } else { break }
        }
        if ($expected.Count -eq 0) {
            foreach ($l in $code) { if ($l -match 'Console\.WriteLine\(.*\);\s*// ?(.*\S)\s*$') { $expected.Add($Matches[1]) } }
        }
        # `// True - why it is true` compares as `True`: text after ` - ` is annotation, not output.
        $expected = [System.Collections.Generic.List[string]]@($expected | ForEach-Object { ($_ -split ' - ', 2)[0].TrimEnd() })
        $blocks += [pscustomobject]@{ File = $f; Ordinal = $n; Usings = $usings; Code = ($code -join "`n"); Expected = @($expected) }
    }
}
if (-not $blocks) { Write-Host 'no csharp blocks found'; exit 0 }

$allUsings = @('using Fuzzy.Text.RegularExpressions;') + ($blocks | ForEach-Object { $_.Usings }) | Sort-Object -Unique
$program = New-Object System.Text.StringBuilder
foreach ($u in $allUsings) { [void]$program.AppendLine($u) }
[void]$program.AppendLine('var __only = args.Length > 0 ? args[0] : null;')
$k = 0
foreach ($b in $blocks) {
    $k++
    [void]$program.AppendLine("if (__only is null || __only == `"$k`") { Console.WriteLine(`"=== $k`"); try {")
    [void]$program.AppendLine($b.Code)
    [void]$program.AppendLine("} catch (Exception e) { Console.WriteLine(`"EXCEPTION: `" + e.GetType().Name + `": `" + e.Message); } }")
}
Set-Content -LiteralPath (Join-Path $dir 'Program.cs') -Value $program.ToString() -Encoding utf8
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>false</TreatWarningsAsErrors><EnableNETAnalyzers>false</EnableNETAnalyzers><RunAnalyzers>false</RunAnalyzers><NoWarn>`$(NoWarn);IDE0055;S1199;CS8321</NoWarn></PropertyGroup>
  <ItemGroup><ProjectReference Include="../../src/FuzzyRegex/FuzzyRegex.csproj" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $dir 'doc-examples.csproj') -Encoding utf8

$build = dotnet build (Join-Path $dir 'doc-examples.csproj') -c Release --nologo -v q 2>&1 | Out-String
if ($build -match ' error ') {
    Write-Host 'doc examples: COMPILE FAILED' -ForegroundColor Red
    ($build -split "`n" | Where-Object { $_ -match ' error ' } | Select-Object -First 8) | ForEach-Object { $_.Trim() }
    exit 1
}
# Run the managed .dll, not the native launcher beside it: that launcher is doc-examples.exe on
# Windows but extensionless `doc-examples` on Linux and macOS, so looking for the .exe found
# nothing there and the run dereferenced a null (CI run 35532078667, 2026-09-20). The project is
# framework-dependent and has no RID, so the .dll is at the same path on every platform.
$dll = Join-Path $dir 'bin/Release/net10.0/doc-examples.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    Write-Host "doc examples: BUILD OUTPUT MISSING, expected $dll" -ForegroundColor Red
    exit 1
}
$out = & dotnet $dll 2>&1 | ForEach-Object { "$_" }

$fail = 0
$k = 0
foreach ($b in $blocks) {
    $k++
    $start = [array]::IndexOf($out, "=== $k")
    $end = if ($k -lt $blocks.Count) { [array]::IndexOf($out, "=== $($k + 1)") } else { $out.Count }
    # A PowerShell range with start > end runs BACKWARDS, so an empty section must be special-cased.
    $actual = if ($start -lt 0 -or ($end - 1) -lt ($start + 1)) { @() } else { @($out[($start + 1)..($end - 1)]) }
    if ($b.Expected.Count -eq 0) { Write-Host ("ok   {0} #{1} (compiles; no expected output)" -f $b.File, $b.Ordinal); continue }
    if (($actual -join "`n") -eq ($b.Expected -join "`n")) { Write-Host ("ok   {0} #{1}" -f $b.File, $b.Ordinal) }
    else {
        $fail = 1
        Write-Host ("FAIL {0} #{1}" -f $b.File, $b.Ordinal) -ForegroundColor Red
        Write-Host "  expected:"; $b.Expected | ForEach-Object { "    $_" }
        Write-Host "  actual:";   $actual     | ForEach-Object { "    $_" }
    }
}
exit $fail
