# What System.Text.RegularExpressions.GroupCollection's IReadOnlyDictionary<string, Group> face
# actually answers, measured rather than assumed.
#
# S53b gives this port's GroupCollection the same face, and the slice file says "Keys (named groups
# only, in number order, as .NET does)". That is a claim about an external system, so it is proved
# here: what Keys holds, what Count counts, what the KeyValuePair enumerator yields, and what
# ContainsKey / TryGetValue / the indexer say for a name the pattern does not have.
#
# Run:  pwsh -File tools/probes/dotnet-groupcollection-dictionary.ps1

Set-StrictMode -Version Latest

function Show([string] $label, [scriptblock] $probe) {
    try {
        Write-Output "  ${label}: $(& $probe)"
    } catch {
        Write-Output "  ${label}: $($_.Exception.GetType().Name): $($_.Exception.Message)"
    }
}

Write-Output ".NET $([System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription)"
Write-Output ""

# One named group before an unnamed one and one named group that did not take part, so number
# order and name order differ and an absent group is visible.
$m = [regex]::Match('ab', '(?<a>a)(b)(?<c>c)?')
$groups = $m.Groups
$dict = [System.Collections.Generic.IReadOnlyDictionary[string, System.Text.RegularExpressions.Group]] $groups

Write-Output "pattern '(?<a>a)(b)(?<c>c)?' over 'ab'"
Show "Count" { $groups.Count }
Show "Keys" { '[' + ($dict.Keys -join ', ') + ']' }
Show "Values (name=value,success)" {
    '[' + (($dict.Values | ForEach-Object { "$($_.Name)=$($_.Value),$($_.Success)" }) -join '; ') + ']'
}
Show "KeyValuePair enumerator" {
    $pairs = [System.Collections.Generic.IEnumerable[System.Collections.Generic.KeyValuePair[string, System.Text.RegularExpressions.Group]]] $groups
    '[' + (($pairs | ForEach-Object { "$($_.Key)->$($_.Value.Value)" }) -join '; ') + ']'
}
Show "ContainsKey('a')" { $dict.ContainsKey('a') }
Show "ContainsKey('2')" { $dict.ContainsKey('2') }
Show "ContainsKey('nope')" { $dict.ContainsKey('nope') }
Show "ContainsKey('c') (named, did not match)" { $dict.ContainsKey('c') }

$out = $null
Show "TryGetValue('a')" {
    $ok = $dict.TryGetValue('a', [ref] $out)
    "$ok / $($out.Value)"
}
Show "TryGetValue('nope')" {
    $ok = $dict.TryGetValue('nope', [ref] $out)
    "$ok / $(if ($null -eq $out) { '<null>' } else { "'" + $out.Value + "'" })"
}
Show "indexer ['nope'].Success" { $groups['nope'].Success }
Show "indexer ['nope'].Name" { $groups['nope'].Name }

Write-Output ""
Write-Output "pattern with no named groups: '(a)(b)'"
$plain = [regex]::Match('ab', '(a)(b)').Groups
$plainDict = [System.Collections.Generic.IReadOnlyDictionary[string, System.Text.RegularExpressions.Group]] $plain
Show "Count" { $plain.Count }
Show "Keys" { '[' + ($plainDict.Keys -join ', ') + ']' }
