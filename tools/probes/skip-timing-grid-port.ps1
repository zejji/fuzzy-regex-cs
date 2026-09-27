# The port's half of tools/probes/skip-timing-grid.py: one answer line per JSON row, in the same
# spelling the Python side uses. Needs pwsh 7 (Windows PowerShell cannot load a net10.0 assembly) and
# a Release build of src/FuzzyRegex. -Ablate sets the named PatternObject switch by reflection:
# SkipMovesTheSliceWhenItRuns is upstream's (*SKIP) timing (ledger entry 45), and
# VerbsAreConfinedToTheInnermostGroup is upstream's verb scope (ledger entry 47).
param([string]$Rows, [string]$Out, [string]$Ablate)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot '../../src/FuzzyRegex/bin/Release/net10.0/FuzzyRegex.dll')
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$poProp = [Fuzzy.Text.RegularExpressions.FuzzyRegex].GetProperty('PatternObject', $flags)
$timeout = [TimeSpan]::FromSeconds(2)
$lines = [Collections.Generic.List[string]]::new()
foreach ($line in [IO.File]::ReadLines($Rows)) {
    $r = $line | ConvertFrom-Json
    try {
        $re = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new($r.pat)
        if ($Ablate) {
            $po = $poProp.GetValue($re)
            $po.GetType().GetField($Ablate, $flags).SetValue($po, $true)
        }
        if ($r.op -eq 'search' -or $r.op -eq 'partial') {
            $m = $re.Match($r.subj, 0, -1, ($r.op -eq 'partial'), $timeout)
            if ($m.Success) {
                $p = if ($m.PartialMatch) { 'P' } else { '' }
                $lines.Add("$p($($m.Index),$($m.Index + $m.Length))")
            } else { $lines.Add('None') }
        } else {
            $ms = $re.Matches($r.subj, 0, -1, ($r.op -eq 'overlapped'), $false, $timeout)
            $lines.Add((@($ms | ForEach-Object { "($($_.Index),$($_.Index + $_.Length))" }) -join ' '))
        }
    } catch {
        $lines.Add("error $($_.Exception.GetType().Name)")
    }
}
[IO.File]::WriteAllText($Out, ($lines -join "`n") + "`n")
