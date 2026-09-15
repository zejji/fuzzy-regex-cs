# This port's answers on the same rows as tools/probes/upstream-turkic-without-spans.py.
# S52, 2026-09-15. Run from the repo root after a Debug build of src/FuzzyRegex.
Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
$IGN = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]::IgnoreCase
$FULL = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]::FullCase
$ML = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]::Multiline

function Esc($s) {
    $out = ''
    foreach ($c in $s.ToCharArray()) {
        if ([int]$c -gt 31 -and [int]$c -lt 127) { $out += $c } else { $out += ('\u{0:x4}' -f [int]$c) }
    }
    return $out
}

function Span($pattern, $subject, $opts) {
    $m = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern, $opts).Match($subject)
    if (-not $m.Success) { return 'None' }
    $g1 = 'g1=unset'
    if ($m.Groups.Count -gt 1 -and $m.Groups[1].Success) {
        $g1 = 'g1=(' + $m.Groups[1].Index + ',' + ($m.Groups[1].Index + $m.Groups[1].Length) + ')'
    }
    return '(' + $m.Index + ',' + ($m.Index + $m.Length) + ") '" + (Esc $m.Value) + "' " + $g1
}

'=== seed 7 row 29165: (?r)(?(?!s)[A-Z]{2}|(S))$ over s CR S U+0131, I|F|M'
'  port match                    : ' + (Span '(?r)(?(?!s)[A-Z]{2}|(S))$' "s`rS`u{0131}" ($IGN -bor $FULL -bor $ML))
'  upstream matched (2,4)        : S + U+0131, by the range spanning I'
'  port [A-Z] vs U+0131          : ' + (Span '[A-Z]' "`u{0131}" ($IGN -bor $FULL))
'  port [A-Y] vs U+0131          : ' + (Span '[A-Y]' "`u{0131}" ($IGN -bor $FULL))

'=== seed 4242 row 24416: ^(?:U+FB01 U+0130){e<=2:\S}([^a-f]+)$ over U+0130 U+0130 U+FB01 SP U+FB01 U+FB00, I|F'
$sub24 = "`u{0130}`u{0130}`u{FB01} `u{FB01}`u{FB00}"
'  port match                    : ' + (Span '^(?:ﬁİ){e<=2:\S}([^a-f]+)$' $sub24 ($IGN -bor $FULL))
'  upstream was (0,6) g1=(3,6)   : its fuzzy section ate three characters, this port two'
'  port section alone            : ' + (Span '^(?:ﬁİ){e<=2:\S}' $sub24 ($IGN -bor $FULL))
'  port U+0130 vs i              : ' + (Span 'İ' 'i' ($IGN -bor $FULL))
'  port U+0130 vs i + U+0307     : ' + (Span 'İ' "i`u{0307}" ($IGN -bor $FULL))

'=== the three minimal span-less shapes'
$r = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new('(?i)I')
'  port Replace((?i)I,X) on U+0131  : ' + (Esc ($r.Replace("`u{0131}", 'X')))
$s = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new('(?i)(I)')
'  port Split((?i)(I)) on a U+0131 b: ' + (($s.Split("a`u{0131}b") | ForEach-Object { "'" + (Esc $_) + "'" }) -join ' ')
