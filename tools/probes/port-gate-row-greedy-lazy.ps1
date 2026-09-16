# This port's answers on the same grid as tools/probes/upstream-gate-row-greedy-lazy.py.
# S52c scope item 7, 2026-09-16. Run from the repo root after a Debug build of src/FuzzyRegex.
#
# `greedy-lazy-existence-agree` (docs/ORACLE-INVARIANTS.md group F): greediness ORDERS the candidate
# set, it does not change its membership, so the greedy and lazy spellings of one quantifier must
# agree on WHETHER a match exists at a given start. They may differ on the span; they may not differ
# on existence. Gate row 104366 is asked here of one engine at a time, because the oracle's "do they
# agree" question has no useful answer on it - see the upstream probe's header.
Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
$O = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]
$V1 = $O::Version1

$SUBJECT = "`u{FB01}`u{0131}"
$DF = "`u{00DF}"
$FI = "`u{FB01}"

function Answer($pattern, $pos, $endpos, $partial) {
    $r = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern, $V1)
    $m = $r.MatchAtStart($SUBJECT, $pos, ($endpos - $pos), $partial)
    if (-not $m.Success) { return 'no match' }
    $kind = if ($m.PartialMatch) { 'partial' } else { 'complete' }
    "($($m.Index), $($m.Index + $m.Length)) $kind"
}

# label, pattern with {q} for the quantifier, pos, endpos, partial
$cells = @(
    @('the gate row', "(?r)$DF$FI{q}\b", 2, 2, $true),
    @('without the reversal', "$DF$FI{q}\b", 2, 2, $true),
    @('without the \b', "(?r)$DF$FI{q}", 2, 2, $true),
    @('without the unmatchable prefix', "(?r){q}\b", 2, 2, $true),
    @('over the whole subject', "(?r)$DF$FI{q}\b", 0, 2, $true),
    @('not partial', "(?r)$DF$FI{q}\b", 2, 2, $false)
)

$broken = 0
'{0,-34} {1,-22} {2,-22} {3,-22} invariant' -f 'cell', 'lazy', 'greedy', 'possessive'
foreach ($cell in $cells) {
    $lazy = Answer $cell[1].Replace('{q}', '(.*?)') $cell[2] $cell[3] $cell[4]
    $greedy = Answer $cell[1].Replace('{q}', '(.*)') $cell[2] $cell[3] $cell[4]
    $possessive = Answer $cell[1].Replace('{q}', '(.*+)') $cell[2] $cell[3] $cell[4]
    # EXISTENCE ONLY. The spans are allowed to differ - that is what greediness is for.
    $isBroken = ($lazy -eq 'no match') -ne ($greedy -eq 'no match')
    if ($isBroken) { $broken++ }
    $verdict = if ($isBroken) { 'BROKEN' } else { 'holds' }
    '{0,-34} {1,-22} {2,-22} {3,-22} {4}' -f $cell[0], $lazy, $greedy, $possessive, $verdict
}

''
"greedy-lazy-existence-agree is broken on $broken of $($cells.Count) cells, on this port."
