# This port's answers on the row of tools/probes/upstream-partial-retry-reversed-longer.py.
# S52, 2026-09-15. Run from the repo root after a Debug build of src/FuzzyRegex.
Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')

function Answer($pattern, $subject) {
    $m = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern).Match($subject, 0, -1, $true)
    if (-not $m.Success) { return 'None' }
    $g1 = 'g1=unset'
    if ($m.Groups.Count -gt 1 -and $m.Groups[1].Success) {
        $g1 = 'g1=(' + $m.Groups[1].Index + ',' + ($m.Groups[1].Index + $m.Groups[1].Length) + ')'
    }
    $kind = if ($m.PartialMatch) { 'partial' } else { 'complete' }
    return '(' + $m.Index + ',' + ($m.Index + $m.Length) + ') ' + $g1 + ' ' + $kind
}

'subject 00a SPACE .'
'  as drawn, (*SKIP)  : ' + (Answer '(?r)(?:[A-Z](*SKIP).|\d)([^\p{L}])\B' '00a .')
'  verb -> (*PRUNE)   : ' + (Answer '(?r)(?:[A-Z](*PRUNE).|\d)([^\p{L}])\B' '00a .')
'  verb deleted       : ' + (Answer '(?r)(?:[A-Z].|\d)([^\p{L}])\B' '00a .')
''
'upstream as drawn is (0,5) g1=(1,2) partial; its (*PRUNE) answer is (0,2) g1=(1,2) partial,'
'which is what this port answers to the code unit with the verb left in place.'
