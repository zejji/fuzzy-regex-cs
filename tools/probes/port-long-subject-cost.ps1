# Times THIS PORT on chosen rows of a recorded wave at truncated subject lengths, so the cost
# curve is visible rather than inferred from one pass/fail. Written in S52 sitting 5 for the two
# `partial-long` rows that reddened the long-subject wave.
#
# The rows are not committed as data - they are 33,000 characters between them. Reproduce them:
#   python tools/record-oracle.py --generator literals-long,quantifiers-long,partial-long,fuzzy-long --count 150 --seed 20260915
# which writes TestResults/oracle/wave.jsonl, whose rows 305 and 307 are the two.
#
# Truncation is from the RIGHT and that is only sound for a `(?r)` row: the long wrapper pads a
# reversed pattern on the right, so cutting the right removes filler and leaves the base row's own
# text where `^` anchors it. Do not reuse this on a forward row without changing the cut.
#
# Pair with tools/probes/upstream-long-subject-cost.py, which runs the same rows and lengths
# through upstream. Comparing one engine's curve against nothing says only that it is slow.
param(
    [string]$Wave = 'TestResults/oracle/wave.jsonl',
    [string]$Rows = '305,307',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)

Add-Type -Path (Resolve-Path "src/FuzzyRegex/bin/$Configuration/net10.0/FuzzyRegex.dll")
Write-Host "configuration: $Configuration"

$wanted = $Rows.Split(',') | ForEach-Object { [int]$_.Trim() }
$index = 0
$selected = @()
foreach ($line in (Get-Content -LiteralPath $Wave)) {
    $obj = $line | ConvertFrom-Json
    if ($obj.kind -eq 'header') { continue }
    $index++
    if ($wanted -contains $index) { $selected += , @($index, $obj) }
}

foreach ($pair in $selected) {
    $rowNumber = $pair[0]
    $row = $pair[1]
    Write-Host ''
    Write-Host ("row {0}  {1}  partial={2}  flags=0x{3:x} ({4})  full length {5}" -f `
            $rowNumber, $row.pattern, [bool]$row.partial, $row.flags, `
        ([Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]$row.flags), $row.subject.Length)

    # The row's own flags, or the measurement is of a different pattern. Dropping IgnoreCase here
    # is what hid the cost on the first pass in S52 sitting 5: without it both rows answer in
    # milliseconds, because `\p{Lu}` and `[^a]` then walk a fraction of the text.
    $re = [Fuzzy.Text.RegularExpressions.FuzzyRegex]::new(
        $row.pattern,
        [Fuzzy.Text.RegularExpressions.FuzzyRegexOptions]$row.flags,
        [Fuzzy.Text.RegularExpressions.FuzzyRegex]::InfiniteMatchTimeout)

    foreach ($n in 100, 200, 400, 800, 1600, 3200, 6400, 12800, $row.subject.Length) {
        if ($n -gt $row.subject.Length) { continue }
        $subject = $row.subject.Substring(0, $n)
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $match = $re.Match($subject, 0, $subject.Length, [bool]$row.partial,
                [Fuzzy.Text.RegularExpressions.FuzzyRegex]::InfiniteMatchTimeout,
                [System.Threading.CancellationToken]::None)
            $watch.Stop()
            $answer = if ($match.Success) { "($($match.Index),$($match.Index + $match.Length))" } else { 'None' }
        }
        catch {
            $watch.Stop()
            $answer = 'ERROR ' + $_.Exception.GetType().Name
        }
        Write-Host ("  n={0,-6} {1,9:N0} ms   {2}" -f $n, $watch.Elapsed.TotalMilliseconds, $answer)
        if ($watch.Elapsed.TotalSeconds -gt 30) { Write-Host '  (stopping: over 30s)'; break }
    }
}
