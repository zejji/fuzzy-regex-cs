# This port's answers on the same grid as tools/probes/upstream-turkic-from-the-pattern-side.py.
# S52, 2026-09-15. Run from the repo root after a Debug build of src/FuzzyRegex.
Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
$O = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]
$IGN = $O::IgnoreCase; $FULL = $O::FullCase; $ML = $O::Multiline; $V1 = $O::Version1

$SUB25 = "`u{FB00}`r `u{FB00}"
$SUB34 = "s`u{FB01}`u{FB00}`u{0131}`u{0130}"

function Row25($first) {
    $lists = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.IReadOnlyCollection[string]]]::new()
    $lists['w1'] = [string[]]@(($first + "`u{0131}"), "`u{FB00}")
    $r = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new(
        '(?(?=\D)[\p{L}||\p{N}])\L<w1>{e<=2}\K',
        ($IGN -bor $FULL -bor $V1),
        $lists
    )
    $m = $r.MatchAtStart($SUB25, 0, -1, $true)
    if (-not $m.Success) { return 'None' }
    "($($m.Index),$($m.Index + $m.Length)) counts=$($m.FuzzyCounts) subs=[$($m.FuzzyChanges.Substitutions -join ',')]"
}

function Row34($pattern) {
    $r = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern, ($IGN -bor $FULL -bor $ML -bor $V1))
    $m = $r.MatchAtStart($SUB34, 5, 0, $true)
    if (-not $m.Success) { return 'None' }
    "($($m.Index),$($m.Index + $m.Length)) partial=$($m.PartialMatch)"
}

'=== seed 20260915 row 25482: the Turkic letter is a member of a \L<name> list'
'  as drawn, w1=[U+0130 U+0131, U+FB00]      : ' + (Row25 "`u{0130}")
'  CONTROL: a first letter whose DEFAULT fold is also longer than one character'
'    U+00DF + U+0131                         : ' + (Row25 "`u{00DF}")
'    U+FB00 + U+0131                         : ' + (Row25 "`u{FB00}")
'    U+01F0 + U+0131                         : ' + (Row25 "`u{01F0}")
'  CONTROL: a first letter that folds to ONE character'
'    h + U+0131                              : ' + (Row25 'h')
'    i + U+0131                              : ' + (Row25 'i')

''
'=== seed 20260915 row 34508: the Turkic letter is the pattern leading literal, empty slice [5,5)'
'  as drawn, ^U+0130\K\b                     : ' + (Row34 '^İ\K\b')
'  CONTROL: a leading literal whose DEFAULT fold is also longer than one character'
'    ^U+00DF\K\b                             : ' + (Row34 '^ß\K\b')
'    ^U+FB00\K\b                             : ' + (Row34 '^ﬀ\K\b')
'    ^U+01F0\K\b                             : ' + (Row34 '^ǰ\K\b')
'  CONTROL: a leading literal that folds to ONE character'
'    ^h\K\b                                  : ' + (Row34 '^h\K\b')
'    ^i\K\b                                  : ' + (Row34 '^i\K\b')
'    ^U+0131\K\b                             : ' + (Row34 '^ı\K\b')
'  CONTROL: the same letters with no ^'
'    U+0130\K\b                              : ' + (Row34 'İ\K\b')
'    U+00DF\K\b                              : ' + (Row34 'ß\K\b')
'    h\K\b                                   : ' + (Row34 'h\K\b')
