# This port's answer on every case of tools/probes/upstream-issue-sweep.py, so the two halves of the
# S49 sweep can be read side by side. Run from the repo root after a Debug build:
#
#     pwsh -File tools/probes/port-issue-sweep.ps1
#
# WHAT IT MEASURED on 2026-09-14, against the Debug build of commit 6169112:
#
#   334  Agrees with upstream 2026.9.10 - None / 0 matches. Neither engine crashes; nothing to fix.
#   367  Agrees with upstream AND with PCRE2. Already pinned as correct in
#        Gaps/Engine/PartialMatchingTests.cs; S49 dismissed the issue rather than the test.
#   397  DIVERGES, and this port is right: None where upstream raises MemoryError. That is S47's
#        ledger-entry-14 positional guard doing its job on a non-fuzzy shape. The rows below prove
#        it is not a silent cap - the same left-recursive DEFINE still matches 'c:d', and a
#        well-founded DEFINE recursion is untouched.
#   425  REPRODUCES upstream exactly on all four rows. Inherited; S50 fixes it.
#   470  DIVERGES by design (DIVERGENCES.md, shipped S41/S42): both budgets answer 'voicees'.
#   551  Agrees with upstream - None, in milliseconds. No loop on either engine.
#   554  REPRODUCES the mechanism and is WORSE: the port throws its 1GB backtracking-stack bound at
#        n=4,000,000, where upstream still succeeds at 6,000,000 and stdlib `re` succeeds at
#        10,000,000. Cold, the port allocates 611 bytes per repetition against the 192 and 99 that
#        upstream-issue-sweep.py measures for `regex` and `re` at the same size; all three exclude
#        the subject, which is built before measuring starts. See the note above the 554 section
#        for why only the first row's byte figure is quotable. Inherited and amplified; S50 owns
#        it. (PowerShell reports the bound as a MethodInvocationException wrapping the engine's
#        InvalidOperationException.)
#   563  REPRODUCES upstream exactly. Inherited.
#   564  REPRODUCES upstream exactly. Inherited.
#   589  REPRODUCES upstream exactly. Inherited.
#   596  Does not reproduce. Compare `(?:X){e<=0}` against `(?:X)` - the same pattern without the
#        constraint, which is the apples-to-apples pair - and the ratio sat between 0.88 and 1.02
#        across five runs on 2026-09-14, against the reported 210. Do NOT quote the microseconds or
#        a tighter band than that: this timing is noise-dominated here, and a draft of this comment
#        quoting 0.83-0.87 was falsified by the next run. The only claim the measurement supports
#        is that the constraint costs nothing detectable. The absolute figures are ~350x upstream's
#        because this port has no required-string prefilter yet; that is Phase 7's scope and not
#        issue 596.

Add-Type -Path (Resolve-Path 'src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll')
$R = [Fuzzy.Text.RegularExpressions.FuzzyRegex]
$O = [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]

function Describe($m) {
    if ($null -eq $m) { return 'null' }
    if (-not $m.Success) { return 'None' }
    $c = $m.FuzzyCounts
    "span=($($m.Index),$($m.Index + $m.Length)) '$($m.Value)' partial=$($m.PartialMatch) counts=($($c.Substitutions),$($c.Insertions),$($c.Deletions))"
}
# Every case runs under a real MatchTimeout, so a resource case reports a bound instead of hanging.
function Case($label, $block) {
    try { "{0}`n    -> {1}" -f $label, (& $block) }
    catch { "{0}`n    -> {1}: {2}" -f $label, $_.Exception.GetType().Name, $_.Exception.Message }
}
function New-Re($pattern, $options = $O::None, $seconds = 30) {
    $R::new($pattern, $options, [TimeSpan]::FromSeconds($seconds))
}

'=== 334: the fuzzy crash - agrees with upstream, no crash'
Case "  MatchAtStart('(?:(?=(e)?)\1){e<=1}', ' ')" { Describe (New-Re '(?:(?=(e)?)\1){e<=1}').MatchAtStart(' ') }
Case "  the reporter's full pattern over 'al, '" {
    $re = New-Re '(?e)(?:(?:(?=(?P<if_2_3>expression1\W+)?)(?P=if_2_3))?(?(if_2_3)expression2|expression3)){e<=1}' ($O::IgnoreCase -bor $O::Singleline -bor $O::EnhanceMatch)
    "$(($re.Matches('al, ') | Measure-Object).Count) matches"
}

''
'=== 367: partial matching assumes a lookahead is satisfiable - agrees with upstream and PCRE2'
foreach ($n in 2, 3, 4, 5, 9) {
    Case "  MatchAtStart('(?!(1{2,})\1+`$)((?:11)+)`$', '1'x$n, partial)" {
        Describe (New-Re '(?!(1{2,})\1+$)((?:11)+)$').MatchAtStart(('1' * $n), 0, -1, $true)
    }.GetNewClosure()
}
Case "  MatchAtStart('(?!.+).*', '1', partial)" { Describe (New-Re '(?!.+).*').MatchAtStart('1', 0, -1, $true) }
Case "  MatchAtStart('(?!.+).*', '1')" { Describe (New-Re '(?!.+).*').MatchAtStart('1') }

''
'=== 397: a left-recursive DEFINE - this port answers None where upstream raises MemoryError'
$define = '(?(DEFINE)(?P<e>[cd]|(?&e))(?P<t>(?&e):(?&e)))'
foreach ($c in @(($define + '(?&t)'), 'a[14]'), @(($define + '(?&e)+(?&e)'), 'a[14]'),
               @(($define + '(?&t)'), 'c:d'), @(($define + '(?&t)'), 'xx c:d xx'),
               @('(?(DEFINE)(?P<b>a(?&b)?b))(?&b)', 'aaabbb'),
               @('(?(DEFINE)(?P<num>\d+))(?&num)-(?&num)', 'x 12-34 y')) {
    Case "  Match('$($c[0])', '$($c[1])')" { Describe (New-Re $c[0] $O::Singleline).Match($c[1]) }.GetNewClosure()
}

''
'=== 425: branch reset with mixed named and numbered groups - reproduces upstream'
foreach ($p in '(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))', '(?|(?P<bug>xxx)(!)|(BUG)(?P<bug>!))',
               '(?|(xxx)(?P<bug>!)|(?P<bug>BUG)(!))', '(?|(xxx)(?P<bug>!)|(BUG)(?P<bug>!))') {
    Case "  $p  on 'BUG!'" {
        $re = New-Re $p
        $m = $re.MatchAtStart('BUG!')
        if (-not $m.Success) { return 'None' }
        $gs = foreach ($i in $re.GroupNumbers) {
            if ($i -gt 0) { "g$i=" + $(if ($m.Groups[$i].Success) { "'" + $m.Groups[$i].Value + "'" } else { 'None' }) }
        }
        "bug='$($m.Groups['bug'].Value)' bugIndex=$($re.GroupNumberFromName('bug')) | " + ($gs -join ' ')
    }.GetNewClosure()
}

''
'=== 470: BESTMATCH cost ranking - this port diverges deliberately, both budgets agree'
foreach ($b in '2', '1') {
    Case "  (voices){1i+1d+2s<=$b} in 'voixes voicees' BestMatch" {
        Describe (New-Re "(voices){1i+1d+2s<=$b}" $O::BestMatch).Match('voixes voicees')
    }.GetNewClosure()
}

''
'=== 551: the V1 search that once looped for ever - agrees with upstream'
$t551 = "Yrkesh" + [char]0xF6 + "gskola . Studie" + [char]0xE4 + "mnen . Studie" + [char]0xE4 + "mnen . Studie" + [char]0xE4 + "mnen . Studie" + [char]0xE4 + "mnen . Studie" + [char]0xE4 + "mnen . Studie" + [char]0xE4 + "mnen . Studie" + [char]0xE4 + "mnen"
$p551 = "(H" + [char]0xF6 + "gskolan?)[\s\S]*([\d,.]+)p"
foreach ($c in @('(?V1)', $O::IgnoreCase), @('(?V0)', $O::IgnoreCase), @('(?V0)', ($O::IgnoreCase -bor $O::FullCase))) {
    Case "  $($c[0]) $($c[1])" {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        "$(Describe (New-Re ($c[0] + $p551) $c[1]).Match($t551))  in $($sw.ElapsedMilliseconds) ms"
    }.GetNewClosure()
}

''
'=== 563: \m with a fuzzy quantifier at position 0 - reproduces upstream'
foreach ($c in @('\m(?:Y){i}\M', 'XY YX'), @('\m(?:X){i}\M', 'XY YX'), @('\m(?:Y){i}\M', ' XY YX')) {
    Case "  Matches('$($c[0])', '$($c[1])')" {
        (((New-Re $c[0]).Matches($c[1]) | ForEach-Object { "'" + $_.Value + "'" }) -join ', ')
    }.GetNewClosure()
}

''
'=== 564: a looser fuzzy budget returns FEWER matches - reproduces upstream'
foreach ($p in '(?b)\m(?:Y){1i+1d+1s<=1}\M', '(?b)\m(?:Y){1i+1d+1s<=2}\M') {
    Case "  Matches('$p', ' XY Z')" {
        (((New-Re $p).Matches(' XY Z') | ForEach-Object { "'" + $_.Value + "'" }) -join ', ')
    }.GetNewClosure()
}

''
'=== 589: partial fullmatch denies a prefix whose completion exists - reproduces upstream'
Case "  FullMatch('True', partial)" { Describe (New-Re '(?!(True|False)\b)(.*)').FullMatch('True', 0, -1, $true) }
Case "  FullMatch('Truest')" { Describe (New-Re '(?!(True|False)\b)(.*)').FullMatch('Truest') }
Case "  FullMatch('True')" { Describe (New-Re '(?!(True|False)\b)(.*)').FullMatch('True') }

''
'=== 596: {e<=0} slow-down - does not reproduce (the ratio is what matters, not the microseconds)'
$t596 = 'lorem ipsum ' * 1024
foreach ($p in 'CARTE DE RESIDENT', '(?:CARTE DE RESIDENT)', '(?:CARTE DE RESIDENT){e<=0}') {
    Case "  $p" {
        $re = New-Re $p
        $null = $re.Match($t596)   # JIT and warm the pattern before timing it
        $sw = [Diagnostics.Stopwatch]::StartNew()
        for ($i = 0; $i -lt 200; $i++) { $null = $re.Match($t596) }
        "{0:F1} us/search over 200 searches" -f ($sw.Elapsed.TotalMilliseconds * 1000 / 200)
    }.GetNewClosure()
}

''
'=== 554: fullmatch (ab)* on a long subject - reproduces, and fails EARLIER than upstream'
# Two things had to be got right before this figure reproduced, and both are worth knowing.
#
# GetTotalAllocatedBytes is a cumulative counter of everything allocated, so unlike a live-heap
# delta it does not depend on whether a collection happened to run mid-call. The first draft used
# [GC]::GetTotalMemory and an independent verifier could not reproduce its numbers at all.
#
# And ONLY THE FIRST ROW'S BYTE FIGURE IS REPRODUCIBLE, which is worth stating plainly because two
# drafts of this probe quoted the others. The engine rents its backtracking buffer from a
# process-wide pool, so a later call may find the earlier call's buffer still there and allocate
# nothing for it. A fresh FuzzyRegex per row does not help - the pool is not per-instance - and
# whether the buffer survives depends on GC timing. Measured 2026-09-14: n=2,000,000 allocates
# 1166.2 MB (611 B/rep) in a cold process and 654.2 MB (343 B/rep) when the pool is warm, and both
# occur across runs of this very script.
#
# So quote n=1,000,000's 611 B/rep - the first call in a fresh process, identical in every run -
# and read the later rows as success/failure only. The 1GB bound at n=4,000,000 is deterministic
# regardless, because it is a fixed constant rather than a measurement.
#
# The subject is built before the baseline is taken, so it is not counted.
foreach ($n in 1000000, 2000000, 4000000) {
    Case "  FullMatch('(ab)*', 'ab'x$n)" {
        $re554 = New-Re '(ab)*' $O::None 120
        $s = [string]::new(('ab' * $n))
        $base = [GC]::GetTotalAllocatedBytes($true)
        $sw = [Diagnostics.Stopwatch]::StartNew()
        try {
            $m = $re554.FullMatch($s)
            $allocated = [GC]::GetTotalAllocatedBytes($true) - $base
            "len={0} in {1} ms, allocated {2:F1} MB ({3:F0} B/rep)" -f $m.Length, $sw.ElapsedMilliseconds, ($allocated / 1MB), ($allocated / $n)
        }
        finally { $s = $null; $re554 = $null; [GC]::Collect() }
    }.GetNewClosure()
}
