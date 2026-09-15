# This port's half of `upstream-gate-drawn-skip-rows.py`, for seed 7 row 76160 alone.
#
# The other three rows of `gate-drawn-skip-rows.jsonl` need no port probe: they are judged, so this
# port's answer to each is the judged string in `ExpectedDivergences.cs` and
# `pwsh -File tools/run-oracle.ps1 -Rows tools/probes/gate-drawn-skip-rows.jsonl` replays all four
# through the real comparer. Row 76160 is the one that is NOT judged, and what keeps it off
# `bestmatch-walk-truncated-by-a-skip` is a comparison of the two engines' ANCHORED answers - a
# question the recorded `split` cannot show, because a split renders as parts and hides both the
# span and the error counts.
#
# Measured by S52 sitting 13 (sitting 11 ran the same cases from .scratch and did not commit them).
# Build first: dotnet build src/FuzzyRegex -c Release

Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Release/net10.0/FuzzyRegex.dll')

# The row verbatim out of `gate-drawn-skip-rows.jsonl`, written with [char]/`u{} escapes so no
# editor can normalise the astral and Turkic characters out of it.
$pattern = '(?b)(?:(?:[^\p{L}]{0,1}(?:' + [char]0x00DF + '){e<=1}){e<=2,s<=1}(*SKIP).|[a-f])(?:(?P<g1>[a\d]*?)([\w\s])[^a-f]){s<=1,i<=1,d<=1}\b'
$subject = "`r`u{FB00}`u{0131}`u{0130}A`u{FB00}`u{00DF}"
$flags = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]0x14002

function Describe($m) {
    if (-not $m.Success) { return 'None' }
    $bits = "($($m.Index), $($m.Index + $m.Length))"
    for ($i = 1; $i -lt $m.Groups.Count; $i++) {
        $g = $m.Groups[$i]
        $bits += if ($g.Success) { " g$i=($($g.Index), $($g.Index + $g.Length))" } else { " g$i=unset" }
    }
    if ($null -ne $m.FuzzyCounts) {
        $c = $m.FuzzyCounts
        $bits += " counts=($($c.Substitutions), $($c.Insertions), $($c.Deletions))"
    }
    # FuzzyChanges is a record of three lists; its own ToString renders the list TYPES, so each list
    # is joined by hand or the line says nothing at all.
    if ($null -ne $m.FuzzyChanges) {
        $ch = $m.FuzzyChanges
        $bits += " changes=[s:$($ch.Substitutions -join ',')][i:$($ch.Insertions -join ',')][d:$($ch.Deletions -join ',')]"
    }
    $bits
}

foreach ($case in @(
        @('as drawn, (*SKIP)', $pattern),
        @('(*SKIP)->(*PRUNE)', $pattern.Replace('(*SKIP)', '(*PRUNE)')),
        @('verb deleted', $pattern.Replace('(*SKIP)', '')))) {
    $r = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($case[1], $flags)
    '  {0,-22} search       {1}' -f $case[0], (Describe $r.Match($subject))
    '  {0,-22} match(pos=4) {1}' -f $case[0], (Describe $r.MatchAtStart($subject, 4, $subject.Length - 4, $false))
}
