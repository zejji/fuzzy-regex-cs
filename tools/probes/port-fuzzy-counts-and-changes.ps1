# This port's answers to ledger entry 11's mechanisms C and D and ledger entry 9's remaining
# port-side count bug - the three items S48b fixed (2026-09-14).
#
# Run from the repo root after a Debug build of src/FuzzyRegex:
#   dotnet build src/FuzzyRegex/FuzzyRegex.csproj -c Debug
#   pwsh -File tools/probes/port-fuzzy-counts-and-changes.ps1
#
# The upstream half is tools/probes/upstream-fuzzy-counts-and-changes.py. Every row carries the
# flag bits the ledger records beside it, written inline, because NONE of them reproduces without
# them - each came off a wave at those flags.
#
# What to look for: on every row the change list holds exactly as many positions of each kind as
# the counts beside it claim. Before S48b it did not, which is the whole of the defect class.

Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
$P = 'Fuzzy.Text.RegularExpressions.FuzzyRegex' -as [type]
$O = 'Fuzzy.Text.RegularExpressions.FuzzyRegexOptions' -as [type]

function Describe($m) {
    if ($null -eq $m -or -not $m.Success) { return 'None' }
    $c = $m.FuzzyCounts
    $ch = $m.FuzzyChanges
    $n = $ch.Substitutions.Count + $ch.Insertions.Count + $ch.Deletions.Count
    $agree = if ($n -eq $c.Total -and $ch.Substitutions.Count -eq $c.Substitutions `
            -and $ch.Insertions.Count -eq $c.Insertions -and $ch.Deletions.Count -eq $c.Deletions) { 'agree' } else { 'CONTRADICT' }
    "({0},{1}) ({2}, {3}, {4}) ([{5}], [{6}], [{7}])  {8}" -f $m.Index, ($m.Index + $m.Length), `
        $c.Substitutions, $c.Insertions, $c.Deletions, `
        ($ch.Substitutions -join ','), ($ch.Insertions -join ','), ($ch.Deletions -join ','), $agree
}

function Scan($label, $pattern, $subject) {
    "$label"
    $r = $P::new($pattern, $O::None)
    $i = 0
    foreach ($m in $r.Matches($subject, 0, -1, $true, $false)) {
        $i++
        "  match {0}: {1}" -f $i, (Describe $m)
        if ($i -ge 12) { break }
    }
    if ($i -eq 0) { '  no matches' }
    ''
}

# Subjects. Spans below are UTF-16 code unit indices; upstream's are codepoint indices, so the
# astral rows need converting before the two are compared.
$FB03 = [char]0xFB03                            # LATIN SMALL LIGATURE FFI
$D400 = [char]::ConvertFromUtf32(0x10400)       # DESERET CAPITAL LETTER LONG I
$F600 = [char]::ConvertFromUtf32(0x1F600)       # GRINNING FACE

'===== ledger 11 mechanism C, the worst measured case ====='
'Flags 258 (0x102): VERSION1 | IGNORECASE. Overlapped finditer.'
'Before S48b match 2 was (4,6) counts (0,0,1) against changes sub[4] - the totals agreed and the'
'KINDS did not. Upstream answers the same spans and counts (codepoints (3,5) and (3,4)).'
Scan '' `
    '(?iV1)(?r)(?p)(?!(?:[^[\p{L}--[a-z]]]\w([\p{L}||\p{N}])){s<=1})(?:([a]+?)(?P<g3>\p{L})){1i+2d+1s<=3:[^a-z]}' `
    ($FB03 + $FB03 + $D400 + $D400 + $D400)

'===== ledger 11 mechanism C, the twin, and the four doors onto it ====='
'Flags 16642 (0x4102): FULLCASE | VERSION1 | IGNORECASE. Overlapped finditer.'
'Before S48b the POSIX+BESTMATCH row had counts (1,0,1) against an EMPTY change list at (0,4).'
'AFTER S48b this port finds a SEVENTH match at (0,9) that upstream''s POSIX+BESTMATCH scan drops -'
'see the upstream probe, where upstream''s own fullmatch at the same flags answers (0,9).'
$BASE = '(\w)(?:\s(?:([\p{L}\p{N}]{2,})){e<=2,s<=1}){1<=e<=2}'
$SUBJ = "A`rA" + [char]0xDF + [char]0xDF + " aaa"
foreach ($pre in @('(?b)(?r)(?p)', '(?b)(?r)', '(?r)(?p)', '(?r)')) {
    Scan "  prefix $pre" ('(?ifV1)' + $pre + $BASE) $SUBJ
}
'  anchored, this port:'
foreach ($pre in @('(?b)(?r)(?p)', '(?b)(?r)', '(?r)')) {
    $r = $P::new('(?ifV1)' + $pre + $BASE, $O::None)
    "    fullmatch {0,-14} -> {1}" -f $pre, (Describe $r.FullMatch($SUBJ))
}
''

'===== ledger 11 mechanism D ====='
'Flags 130 (0x82): ASCII | IGNORECASE. search.'
'Before S48b: (6,8) counts (1,0,0) against changes del[7] - one substitution counted, one deletion'
'reported. Upstream STILL contradicts itself here, anchored as well as searching: it answers'
'codepoints (4,6) counts (1,0,0) with a DELETION at 5. So there is no upstream reference for the'
'change list on this row and self-consistency is the only standard available.'
$r = $P::new('(?ai)(?e)([abz])[a\d]{0,}?(?<=(?:(\d?)[A-Z]' + $F600 + '){s<=1,i<=1,d<=1})\b', $O::None)
"  search: {0}" -f (Describe $r.Match($F600 + "`r`n" + $F600 + "AA"))
''

'===== ledger 9, the remaining port-side count bug ====='
'POSIX plus (?e) spent an error the same span does not need. Before S48b the POSIX row answered'
'(0,5) counts (1,1,1) where the same pattern without POSIX answered (0,5) counts (0,1,1) - three'
'errors for a span this engine itself fits in two. Upstream answers (0,1,1) under POSIX.'
$pat = '(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}'
$subj = '+ aBA'
"  POSIX    {0}" -f (Describe $P::new($pat, $O::Posix).FullMatch($subj))
"  no POSIX {0}" -f (Describe $P::new($pat, $O::None).FullMatch($subj))
