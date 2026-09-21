# This port's answers to upstream issues 563 and 564 - the same rows as the upstream half,
# tools/probes/issue-563-anchor-rule.py, so the two outputs can be read side by side.
#
# Run from the repo root after a Debug build of src/FuzzyRegex:
#   dotnet build src/FuzzyRegex/FuzzyRegex.csproj -c Debug
#   pwsh -File tools/probes/port-issue-563-anchor-rule.ps1
#
# WHAT IT MEASURED on 2026-09-21, after S57c. Sections 1 to 6 are the upstream probe's sections;
# section 7 is the reversed direction, which the differential oracle found on its own (seed
# 20260921, row 3752) once the fix made the two engines disagree there.
#
#   1. \m(?:Y){i}\M over 'XY YX'            -> ['XY', 'YX']   upstream ['YX']
#   2. search(' XY', pos=0) and pos=1       -> (1, 3) both    upstream: a match then None
#   3. (?m)^(?:abc){i<=1} over 'xabc'       -> ['xabc']       upstream []
#      (?=x)(?:abc){i<=1} over 'xabc'       -> []             both: a lookaround is not a
#                                                             position assertion, on purpose
#   4. \m(?:Y){i}\M over 'q XY YX'          -> ['q XY', 'YX'] upstream ['XY', 'YX'], and the
#                                                             answer here no longer depends on
#                                                             the leading space: ' q XY YX' gives
#                                                             ['q XY', 'YX'] on both engines
#   5. (?:abc){i<=1} over 'xabc'            -> ['abc']        both: nothing pins the anchor
#   6. (?b)\m(?:Y){1i+1d+1s<=2}\M           -> ['XY', 'Z']    upstream ['Z']
#   7. (?r)^a(?:b){i<=1}$ over "ab`r"       -> (0, 3) i=1     upstream None, while the same
#                                                             pattern forwards matches (0, 3)
#
# Sections 1 to 6 print findall-style match text, which is what the upstream probe prints.
# Section 7 prints the span and the fuzzy counts as well, because the whole row turns on an
# insertion that the match text alone does not show.

Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
$P = 'Fuzzy.Text.RegularExpressions.FuzzyRegex' -as [type]

$cr = [char]0x000D

function Findall($pattern, $subject) {
    $values = @($P::new($pattern).Matches($subject) | ForEach-Object { "'" + $_.Value + "'" })
    '[' + ($values -join ', ') + ']'
}

function Detail($pattern, $subject) {
    $m = $P::new($pattern).Match($subject)
    if (-not $m.Success) { return 'None' }
    $c = $m.FuzzyCounts
    '({0}, {1}) fuzzy=(s {2}, i {3}, d {4})' -f $m.Index, ($m.Index + $m.Length), $c.Substitutions, $c.Insertions, $c.Deletions
}

function Show($subject) { "'" + ($subject -replace "`r", '\r') + "'" }

$sections = [ordered]@{
    '1. the issue as reported'                              = @(
        @('\m(?:Y){i}\M', 'XY YX'),
        @('\m(?:X){i}\M', 'XY YX'),
        @('\m(?:Y){i}\M', ' XY YX'))
    '3. the ^/\A exemption, and the same pattern without it' = @(
        @('^(?:abc){i<=1}', 'xabc'),
        @('\A(?:abc){i<=1}', 'xabc'),
        @('(?m)^(?:abc){i<=1}', 'xabc'),
        @('(?=x)(?:abc){i<=1}', 'xabc'))
    '4. a runaway leading insertion is fine off the anchor'  = @(
        @('\m(?:Y){i}\M', 'q XY YX'),
        @('\m(?:Y){i}\M', ' q XY YX'))
    '5. rows the fix must not move'                          = @(
        @('(?:abc){i<=1}', 'xabc'),
        @('(?<![0-9])(?:abc){i<=1}', 'xabc'),
        @('(?:Y){i}', 'qXY'))
    '6. issue 564, the looser budget finding fewer matches'  = @(
        @('(?b)\m(?:Y){1i+1d+1s<=1}\M', ' XY Z'),
        @('(?b)\m(?:Y){1i+1d+1s<=2}\M', ' XY Z'))
}

foreach ($title in $sections.Keys) {
    ''
    $title
    foreach ($row in $sections[$title]) {
        '  {0,-28} over {1,-12} -> {2}' -f $row[0], (Show $row[1]), (Findall $row[0] $row[1])
    }
}

''
'2. the mechanism: one span, two search starts'
foreach ($start in 0, 1) {
    $m = $P::new('\m(?:Y){i}\M').Match(' XY', $start, -1)
    $found = if ($m.Success) { "(({0}, {1}), '{2}')" -f $m.Index, ($m.Index + $m.Length), $m.Value } else { 'None' }
    "  search(' XY', pos={0}) -> {1}" -f $start, $found
}

''
'7. the reversed direction, oracle seed 20260921 row 3752 minimised'
foreach ($row in @(
        @('(?r)^a(?:b){i<=1}$', "ab$cr"),
        @('^a(?:b){i<=1}$', "ab$cr"),
        @('(?r)a(?:b){i<=1}$', "ab$cr"),
        @('(?r)^a(?:b){i<=1}$', 'ab'),
        @('(?r)^a(?:b){s<=1}$', "ab$cr"))) {
    '  {0,-28} over {1,-12} -> {2}' -f $row[0], (Show $row[1]), (Detail $row[0] $row[1])
}
