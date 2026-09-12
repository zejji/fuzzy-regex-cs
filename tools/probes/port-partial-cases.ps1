# Probes this port (the built Debug assembly) on the same cases as the upstream-* and pcre2-* probes. Run from the repo root after a build.
Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
function P($pattern, $subject, $pos = 0, $len = -1, $search = $false) {
    $r = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern)
    $m = if ($search) { $r.Match($subject, $pos, $len, $true) } else { $r.MatchAtStart($subject, $pos, $len, $true) }
    $ans = if ($m.Success) { "(($($m.Index), $($m.Index + $m.Length)), partial=$($m.PartialMatch))" } else { 'None' }
    "{0,-14} {1,-8} pos={2} len={3} search={4} -> {5}" -f $pattern, "'$subject'", $pos, $len, $search, $ans
}
'# family 3'
P 'ba??x' 'baa'; P 'ba??x' 'bab'; P 'ba??x' 'ba'; P 'ba?x' 'baa'; P 'ba{0,1}?x' 'baa'; P '.{0,2}?x' 'baa'; P 'ba*?x' 'baa'
'# family 2: narrowed slice (len = endpos - pos)'
P '(?r)a(bc)*' 'abc' 1 0; P 'a(bc)*' 'abc' 1 0; P '(?r)a(bc)*' 'abc' 2 0; P 'a(bc)*' 'abc' 2 0; P 'a(bc)*' '' 0 -1; P '(?r)a(bc)*' '' 0 -1; P '(?r)a(bc)*' 'abc' 0 1
'# family 1'
P '(?r)\b$' '' 0 -1 $true; P '\b$' '' 0 -1 $true; P '(?r)\b$' '' 0 -1 $false
