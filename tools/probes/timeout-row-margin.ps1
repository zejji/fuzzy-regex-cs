# THIS PORT'S half of tools/probes/timeout-row-margin.py. Read that file first: it carries the
# reasoning, and comparing one engine's timings against nothing says only that it is slow.
#
# The `timeout` generator's rows are the only ones whose recorded timeout the consumer COMPARES,
# and the licence for that is that BOTH engines blow through the budget by a wide margin. The
# Python half measures upstream; this one measures here.
#
#   pwsh -File tools/probes/timeout-row-margin.ps1                 # the shapes, at 20x the budget
#   pwsh -File tools/probes/timeout-row-margin.ps1 -Operations     # every shape, every operation
#
# RELEASE by default, because that is what `run-oracle.ps1` has recorded and consumed waves under
# since S52 sitting 6. Debug makes this port 5-8x slower, which only widens the margin - so a
# Release measurement is the conservative one and a Debug run cannot overturn it.
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [int]$Margin = 20,
    [switch]$Operations
)

$ErrorActionPreference = 'Stop'

# The generator's own table, read out of tools/record-oracle.py rather than restated, for the same
# reason the Python half imports it: a probe holding its own copy goes on reporting a healthy margin
# after the generator has drifted away from it.
$recorder = Get-Content -LiteralPath 'tools/record-oracle.py' -Raw
$budget = [double]([regex]::Match($recorder, 'TIMEOUT_ROW_BUDGET_SECONDS = ([0-9.]+)').Groups[1].Value)
$length = [int]([regex]::Match($recorder, 'MIN_TIMEOUT_REPEATS = (\d+)').Groups[1].Value)
$table = [regex]::Match($recorder, 'TIMEOUT_SHAPES = \(([\s\S]*?)\n\)').Groups[1].Value
$shapes = [regex]::Matches($table, '\("([^"]+)",\s*r"(.*?)"\),') | ForEach-Object {
    , @($_.Groups[1].Value, $_.Groups[2].Value)
}

# Every parse is checked, because the failure mode of all three is a SILENT WRONG ANSWER rather than
# an error: a budget read as 0 makes every shape "finished", and a table read short measures a family
# smaller than the generator draws and reports it healthy. A count-only guard is not enough - if the
# TIMEOUT_SHAPES block itself stops matching, both the parsed count and the declared count are zero
# and agree - so the declared count is taken from the block and the block is required to be there.
if ($budget -le 0) { throw 'could not read TIMEOUT_ROW_BUDGET_SECONDS out of tools/record-oracle.py' }
if ($length -le 0) { throw 'could not read MIN_TIMEOUT_REPEATS out of tools/record-oracle.py' }
$declared = ([regex]::Matches($table, '(?m)^\s*\(')).Count
if ($declared -eq 0) { throw 'could not read TIMEOUT_SHAPES out of tools/record-oracle.py' }
if ($shapes.Count -ne $declared) {
    throw "read $($shapes.Count) shapes but TIMEOUT_SHAPES declares $declared - the table's spelling has moved"
}

Add-Type -Path (Resolve-Path "src/FuzzyRegex/bin/$Configuration/net10.0/FuzzyRegex.dll")

$confirm = [System.TimeSpan]::FromSeconds($budget * $Margin)
$subject = ('a' * $length) + '!'
$none = [System.Threading.CancellationToken]::None
$infinite = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::InfiniteMatchTimeout

# The recorder's ALL_OPERATIONS, READ OUT OF IT so the two halves' matrices line up column for
# column. Each operation reaches a DIFFERENT loop in this port, and a loop that never polls the
# deadline is a hang nothing else in the suite can see - which is the whole reason the generator
# draws the operation rather than fixing it at `search`.
#
# The first draft hard-coded this list with `split` sixth where ALL_OPERATIONS has it eighth, so
# reading the port's column six against upstream's column six compared `split` with `finditer`.
# Caught by the blind review; the two `finditer` headers were also truncated to the same string and
# are now distinguishable.
# ALL_OPERATIONS = OPERATIONS + SUB_OPERATIONS + ITER_OPERATIONS, read in that order.
$opNames = @('OPERATIONS', 'SUB_OPERATIONS', 'ITER_OPERATIONS') | ForEach-Object {
    $tuple = [regex]::Match($recorder, "^$_ = \(([^)]*)\)", 'Multiline').Groups[1].Value
    [regex]::Matches($tuple, '"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
}
if ($opNames.Count -ne 8) { throw "expected 8 operations, read $($opNames.Count) from tools/record-oracle.py" }

function Invoke-Row {
    param($Compiled, [string]$Operation, [string]$Text, [System.TimeSpan]$Budget)

    switch ($Operation) {
        'sub' { $replacements = 0; return $Compiled.Replace($Text, 'z', -1, [ref]$replacements, $Budget, $none) }
        'subf' { $replacements = 0; return $Compiled.ReplaceFormat($Text, 'z', -1, [ref]$replacements, $Budget, $none) }
        'split' { return $Compiled.Split($Text, -1, $Budget, $none) }
        # Matches takes `partial` between `overlapped` and `timeout`, and the collection is lazy -
        # so @() around it is what forces the scan to actually run inside the deadline.
        'finditer' { return @($Compiled.Matches($Text, 0, $Text.Length, $false, $false, $Budget, $none)) }
        'finditer-overlapped' { return @($Compiled.Matches($Text, 0, $Text.Length, $true, $false, $Budget, $none)) }
        'match' { return $Compiled.MatchAtStart($Text, 0, $Text.Length, $false, $Budget, $none) }
        'fullmatch' { return $Compiled.FullMatch($Text, 0, $Text.Length, $false, $Budget, $none) }
        default { return $Compiled.Match($Text, 0, $Text.Length, $false, $Budget, $none) }
    }
}

function Test-StillRunning {
    param([string]$Pattern, [string]$Operation, [System.TimeSpan]$Budget)

    $re = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new(
        $Pattern, [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]::None, $infinite)
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $null = Invoke-Row -Compiled $re -Operation $Operation -Text $subject -Budget $Budget
        $watch.Stop()
        return @($false, $watch.Elapsed.TotalMilliseconds)
    }
    catch {
        $watch.Stop()
        $inner = $_.Exception
        while ($inner.InnerException) { $inner = $inner.InnerException }
        if ($inner -is [System.Text.RegularExpressions.RegexMatchTimeoutException]) {
            return @($true, $watch.Elapsed.TotalMilliseconds)
        }
        return @($inner.GetType().Name, $watch.Elapsed.TotalMilliseconds)
    }
}

function Format-Cell {
    param($Value)
    if ($Value -is [bool]) { if ($Value) { 'running' } else { 'finished' } } else { [string]$Value }
}

Write-Host ("configuration: {0}; subject {1} 'a's and a '!'; budget {2}s" -f $Configuration, $length, $budget)
Write-Host ("still running after {0}s, which is {1}x the budget?" -f $confirm.TotalSeconds, $Margin)
Write-Host ''

if ($Operations) {
    # '-overlapped' shortened rather than truncated: 'finditer' and 'finditer-overlapped' both cut to
    # 'finditer' at any sane column width, and two columns with the same heading are unreadable.
    Write-Host (("{0,-22} " -f 'shape') + (($opNames | ForEach-Object { "{0,11}" -f ($_ -replace '-overlapped', '-ovl') }) -join ' '))
    $ok = 0; $run = 0
    foreach ($shape in $shapes) {
        $cells = foreach ($operation in $opNames) {
            $result = Test-StillRunning -Pattern $shape[1] -Operation $operation -Budget $confirm
            $run++
            if ($result[0] -is [bool] -and $result[0]) { $ok++ }
            "{0,11}" -f (Format-Cell $result[0])
        }
        Write-Host (("{0,-22} " -f $shape[0]) + ($cells -join ' '))
    }
    Write-Host ''
    Write-Host ("{0} of {1} cells still running at {2}x the budget." -f $ok, $run, $Margin)
    if ($ok -ne $run) { exit 1 }
    exit 0
}

Write-Host ("{0,-22} {1,10} {2,12}   pattern" -f 'shape', 'at budget', ("at {0}s" -f $confirm.TotalSeconds))
$drifted = @()
foreach ($shape in $shapes) {
    $atBudget = Test-StillRunning -Pattern $shape[1] -Operation 'search' -Budget ([System.TimeSpan]::FromSeconds($budget))
    $atConfirm = Test-StillRunning -Pattern $shape[1] -Operation 'search' -Budget $confirm
    if (-not ($atConfirm[0] -is [bool] -and $atConfirm[0])) { $drifted += $shape[0] }
    Write-Host ("{0,-22} {1,10} {2,12}   {3}" -f $shape[0], (Format-Cell $atBudget[0]), (Format-Cell $atConfirm[0]), $shape[1])
}

Write-Host ''
if ($drifted.Count -eq 0) {
    Write-Host ("ALL {0} SHAPES are still running at {1}x the budget." -f $shapes.Count, $Margin)
    exit 0
}
Write-Host ("*** {0} FINISHED inside the margin - the family has drifted, redraw the table. ***" -f ($drifted -join ', '))
exit 1
