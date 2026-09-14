# Probes this port on the same rows as upstream-bestmatch-walk-truncated-by-a-skip.py (ledger entry
# 5's sixth door). Run from the repo root after a Debug build. Since S48 every row answers the
# `(*PRUNE)` column - the zero-error candidate the walk used to stop short of.
Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
function Describe($m) {
    if (-not $m.Success) { return 'None' }
    $c = $m.FuzzyCounts
    "($($m.Index),$($m.Index + $m.Length)) ($($c.Substitutions),$($c.Insertions),$($c.Deletions))"
}
function S($pattern, $subject) {
    $skip = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern)
    $prune = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern.Replace('(*SKIP)', '(*PRUNE)'))
    $gone = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($pattern.Replace('(*SKIP)', ''))
    "{0} over '{1}'" -f $pattern, $subject
    "   search, as written      {0}" -f (Describe $skip.Match($subject))
    "   search, (*PRUNE)        {0}" -f (Describe $prune.Match($subject))
    "   search, verb deleted    {0}" -f (Describe $gone.Match($subject))
    $doors = @()
    for ($i = 0; $i -le $subject.Length; $i++) {
        $m = $skip.MatchAtStart($subject, $i)
        if ($m.Success) {
            $c = $m.FuzzyCounts
            $doors += "($i, ($($m.Index), $($m.Index + $m.Length)), $($c.Substitutions + $c.Insertions + $c.Deletions))"
        }
    }
    "   its own MatchAtStart doors   [{0}]" -f ($doors -join ', ')
    ''
}
S '(?b)(?:a(*SKIP)b){e<=1}' 'axab'
S '(?b)(?:b(*SKIP)a){e<=1}' 'bxba'
S '(?b)(?:b(*SKIP)ab){e<=1}' 'bbbab'
S '(?b)(?:\w(*SKIP)ab){e<=1}' 'abcabc'
S '(?b)(?:\w(*SKIP)ab){e<=2}' 'qqxyab'
