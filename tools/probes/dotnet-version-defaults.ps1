# The System.Text.RegularExpressions column of S50b's V0/V1 table, measured.
#
# The twin of tools/probes/upstream-version-defaults.py. The slice's claim is that flipping this
# port's default to VERSION1 moves it AWAY from .NET on set syntax and case folding, so the .NET
# column has to be measured rather than assumed from the docs.
#
# Run:  pwsh -File tools/probes/dotnet-version-defaults.ps1

Set-StrictMode -Version Latest

function Show([string] $label, [scriptblock] $probe) {
    try {
        $value = & $probe
        Write-Output "  ${label}: $value"
    } catch {
        Write-Output "  ${label}: $($_.Exception.GetType().Name): $($_.Exception.Message)"
    }
}

Write-Output ".NET $([System.Environment]::Version), $([System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription)"

Write-Output ""
Write-Output "1. zero-width split / sub"
Show "Regex.Split('abc', '')" { "[" + (([regex]::Split('abc', '')) -join '|') + "]" }
Show "Regex.Replace('abc', '', '-')" { [regex]::Replace('abc', '', '-') }
Show "Regex.Replace('abxd', 'x*', '-')" { [regex]::Replace('abxd', 'x*', '-') }

Write-Output ""
Write-Output "2. inline flag scoping and turn-off"
Show "'(?i)a(?-i)a' vs 'AA'" { [regex]::IsMatch('AA', '^(?i)a(?-i)a') }
Show "'(?:(?i)a)a' vs 'Aa'" { [regex]::IsMatch('Aa', '^(?:(?i)a)a') }
Show "'(?:(?i)a)a' vs 'AA'" { [regex]::IsMatch('AA', '^(?:(?i)a)a') }

Write-Output ""
Write-Output "3. nested sets and set operations"
Show "'[[a-z]--[aeiou]]' over 'b-]x'" { "[" + (([regex]::Matches('b-]x', '[[a-z]--[aeiou]]') | ForEach-Object { $_.Value }) -join '|') + "]" }
Show "'[a-z-[aeiou]]' over 'b-]x'" { "[" + (([regex]::Matches('b-]x', '[a-z-[aeiou]]') | ForEach-Object { $_.Value }) -join '|') + "]" }
Show "'[[]' over '[a'" { "[" + (([regex]::Matches('[a', '[[]') | ForEach-Object { $_.Value }) -join '|') + "]" }
Show "'[a[b]' over 'ab['" { "[" + (([regex]::Matches('ab[', '[a[b]') | ForEach-Object { $_.Value }) -join '|') + "]" }

Write-Output ""
Write-Output "4. case-insensitive folding"
Show "(?i) 'ss' vs SHARP S" { [regex]::IsMatch([char] 0x00DF, '^(?i)ss$') }
Show "(?i) 'fi' vs LIGATURE FI" { [regex]::IsMatch([char] 0xFB01, '^(?i)fi$') }
Show "(?i) 'k' vs KELVIN SIGN" { [regex]::IsMatch([char] 0x212A, '^(?i)k$') }
Show "(?i) 'i' vs DOTLESS I" { [regex]::IsMatch([char] 0x0131, '^(?i)i$') }
Show "(?i) 'i' vs DOTTED CAPITAL I" { [regex]::IsMatch([char] 0x0130, '^(?i)i$') }

Write-Output ""
Write-Output "5. a backreference to an OPEN group"
Show "'(a\1)' vs 'a'" { [regex]::IsMatch('a', '^(a\1)') }
Show "'(?<x>a\k<x>)' vs 'a'" { [regex]::IsMatch('a', '^(?<x>a\k<x>)') }
