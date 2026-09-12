# Probes this port (the built Debug assembly) on the same cases as the upstream-* and pcre2-* probes. Run from the repo root after a build.
Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
function S($pattern, $subject, $pos = 0) {
    $m = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern).Match($subject, $pos)
    "{0,-22} {1,-11} pos={2} -> {3}" -f $pattern, "'$subject'", $pos, ($(if ($m.Success) { "($($m.Index),$($m.Index + $m.Length))" } else { 'None' }))
}
S '(?:..(*SKIP)x|q)x' 'ab cd xx'; S '(?:..(*SKIP)x|q)x' 'abcdxxx'; S '(?:aa(*SKIP)x|M)x' 'aaaaxx'; S '..(*SKIP)xx' 'cd xxx'; S '..(*SKIP)xx' 'ab cd xxx'; S '(?:.(*SKIP)x|q)x' 'd xx'
