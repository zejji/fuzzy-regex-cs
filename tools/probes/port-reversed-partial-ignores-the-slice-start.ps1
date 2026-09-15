# This port's answers on the same grid as
# tools/probes/upstream-reversed-partial-ignores-the-slice-start.py.
# S52 sitting 10, 2026-09-15. Run from the repo root after a Debug build of src/FuzzyRegex.
#
# This port carries upstream's `text_start = 0` (`upstream/src/_regex.c:18442`) as
# `MatchState.TextStart` and asks the same `TextPos <= TextStart && PartialSide == PartialLeft`
# question at every site (Engine/Matcher.cs:3273, :5788, :6644, :6705, :6787, :7393, :7458, :7556
# and the rest). It has no `search_start` second rule, so it answers that one question uniformly.
Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
$O = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]
$V1 = $O::Version1

function Answer($pattern, $subject, $pos, $endpos) {
    $r = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern, $V1)
    $m = $r.MatchAtStart($subject, $pos, ($endpos - $pos), $true)
    if (-not $m.Success) { return 'None' }
    "($($m.Index), $($m.Index + $m.Length)) partial=$($m.PartialMatch)"
}

$out = [System.Collections.Generic.List[string]]::new()
function Block($title, $rows) {
    $out.Add('')
    $out.Add("=== $title")
    foreach ($row in $rows) {
        $out.Add(('  {0,-46} {1}' -f $row[0], (Answer $row[1] $row[2] $row[3] $row[4])))
    }
}

$SUBJECT = "`u{FB01}`u{0131}"
$DF = "`u{00DF}"
$FI = "`u{FB01}"
$DRAWN = "(?r)$DF$FI(.*?)\b"

Block 'the control: one visible character, as the whole subject and as a slice' @(
    @("(?r)ya          over 'a', no slice", '(?r)ya', 'a', 0, 1),
    @("(?r)ya(.*?)\b   over 'a', no slice", '(?r)ya(.*?)\b', 'a', 0, 1),
    @("(?r)ya(.*)\b    over 'a', no slice", '(?r)ya(.*)\b', 'a', 0, 1),
    @("(?r)ya          over 'xya' slice (2,3)", '(?r)ya', 'xya', 2, 3),
    @("(?r)ya(.*?)\b   over 'xya' slice (2,3)", '(?r)ya(.*?)\b', 'xya', 2, 3),
    @("(?r)ya(.*)\b    over 'xya' slice (2,3)", '(?r)ya(.*)\b', 'xya', 2, 3)
)

Block 'greedy against lazy, same slice, same visible text' @(
    @("(?r)ya(.*?)\b   'xya' slice (2,3)  LAZY", '(?r)ya(.*?)\b', 'xya', 2, 3),
    @("(?r)ya(.*)\b    'xya' slice (2,3)  GREEDY", '(?r)ya(.*)\b', 'xya', 2, 3),
    @("(?r)ya(.?)\b    'xya' slice (2,3)  BOUNDED", '(?r)ya(.?)\b', 'xya', 2, 3)
)

Block 'the drawn row and its cuts, empty slice (2, 2)' @(
    @('as drawn   (?r)\xdfﬁ(.*?)\b', $DRAWN, $SUBJECT, 2, 2),
    @('no trailing \b', "(?r)$DF$FI(.*?)", $SUBJECT, 2, 2),
    @('no lazy group', "(?r)$DF$FI\b", $SUBJECT, 2, 2),
    @('neither', "(?r)$DF$FI", $SUBJECT, 2, 2),
    @('one literal only', "(?r)$DF(.*?)\b", $SUBJECT, 2, 2),
    @('greedy instead of lazy', "(?r)$DF$FI(.*)\b", $SUBJECT, 2, 2)
)

Block 'an empty slice is an empty slice: (?r)ab(.*?)\b at every empty slice of ''xyz''' @(
    @('pos = endpos = 0', '(?r)ab(.*?)\b', 'xyz', 0, 0),
    @('pos = endpos = 1', '(?r)ab(.*?)\b', 'xyz', 1, 1),
    @('pos = endpos = 2', '(?r)ab(.*?)\b', 'xyz', 2, 2),
    @('pos = endpos = 3', '(?r)ab(.*?)\b', 'xyz', 3, 3),
    @('the empty subject', '(?r)ab(.*?)\b', '', 0, 0)
)

Block 'and (?r)a at every empty slice of ''xyz'' - the same question, inverted' @(
    @('pos = endpos = 0', '(?r)a', 'xyz', 0, 0),
    @('pos = endpos = 1', '(?r)a', 'xyz', 1, 1),
    @('pos = endpos = 2', '(?r)a', 'xyz', 2, 2),
    @('pos = endpos = 3', '(?r)a', 'xyz', 3, 3)
)

Block 'the min-width inversion, side by side at the empty slice (2, 2) of ''xyz''' @(
    @('(?r)a          needs 1 character', '(?r)a', 'xyz', 2, 2),
    @('(?r)ab         needs 2', '(?r)ab', 'xyz', 2, 2),
    @('(?r)ab(.*?)\b  needs 2', '(?r)ab(.*?)\b', 'xyz', 2, 2),
    @('(?r)abc(.*?)\b needs 3', '(?r)abc(.*?)\b', 'xyz', 2, 2)
)

Block 'forward, the same shapes - the slice end behaves like the string end, uniformly' @(
    @("a              'xyz' slice (2,2)", 'a', 'xyz', 2, 2),
    @("ab(.*?)\b      'xyz' slice (2,2)", 'ab(.*?)\b', 'xyz', 2, 2),
    @("ab(.*)\b       'xyz' slice (2,2)", 'ab(.*)\b', 'xyz', 2, 2),
    @("ay             'ay'  no slice", 'ay', 'ay', 0, 1),
    @("ay             'xya' slice (2,3)", 'ay', 'xya', 2, 3)
)

$out | ForEach-Object { $_ }
