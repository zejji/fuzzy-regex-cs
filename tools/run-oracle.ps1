<#
.SYNOPSIS
    Records a differential oracle wave from upstream and diffs this port's matching against it.

.DESCRIPTION
    Record, consume and verdict in one command. From phase 3 on, this is what every slice that
    touches the engine runs locally before it commits (VERIFICATION.md rule 7): the ported suite
    passing is evidence of parity, not proof of it.

    Deliberately not a merge gate (design spec amendment 7). It needs Python, its rows change from
    run to run, and a flaky ground truth must not be able to block a merge. CI runs it on a
    schedule from .github/workflows/oracle.yml; a divergence it finds is minimised into an ordinary
    permanent test in tests/FuzzyRegex.Tests/Gaps/ rather than left living in a wave.

.PARAMETER Generator
    Comma-separated generator names. See tools/record-oracle.py for what each emits.

    EVERY GENERATOR IS ON THE DEFAULT LIST. 'partial-sliced' joined it in S33 and 'verbs' in S34,
    which is the first time the list has been complete.

    Both had been held out because a handful of their rows diverge for a reason already judged, and
    holding a whole generator out for a few per cent of its rows trades all of its coverage for
    none. What replaced that is an accounted-for list,
    tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs: six named families, each with the reason,
    the engine that is right, the permanent test that pins this port's answer, and the minimised row
    it was found on. A row one of them accounts for is printed in the report as 'EXPECTED <id>' and
    tallied separately; every other divergence still reds the run.

    Read that file before trusting a green wave. It is NOT xfail_strict - a wave's rows come from a
    random seed, so there is no row identity to pin - and what makes it honest instead is
    'Every_expected_divergence_still_diverges', which runs each entry's minimised row through the
    live engine on every oracle run and fails when one stops diverging.

    The six families, in short:

    'search-start-partial' - upstream's 'search_start' prefilter (upstream/src/_regex.c:8385) gives
    every scanner a partial arm of its own; the slow path this port runs has none, and neither does
    upstream's, which is why upstream's own 'match' and 'fullmatch' answer None to the same row:

        regex.compile(r'(?r)\b$').search('', partial=True)   # upstream: (0, 0) partial; ours: None

    Port right, judged against PCRE2 10.47 and upstream issue 589 in
    docs/plan/2026-09-12-divergence-research.md. It reaches 'partial' at about one row in two
    thousand and 'partial-sliced' at about three in a thousand.

    'search-start-skip-slice' - the same prefilter, seen from the other end. Each 'search_start_*'
    scanner bounds itself with text_end/text_start where the 'try_match_*' predicate 'basic_match'
    consults bounds itself with slice_end/slice_start. Nothing but a '(*SKIP)' moves the slice inside
    an attempt, so the two agree on every other pattern; once one does, upstream's fast path walks
    past a start position its own slow path accepts:

        regex.finditer(r'(?r)(?:a*(*SKIP)b|[^a-f])$', '\n' + 'b', regex.M)   # upstream: one match

    Four rows in 1200 at seed 20260913 - 502, 504, 519 and 863, all '(?r)', all a multi-match
    operation. Port right on every '(*SKIP)' case PCRE2 can be asked.

    'reverse-fullmatch-narrowed-slice' - an upstream bug, settled by S33. 'try_match's RE_OP_SUCCESS
    arm (:7829) bounds a reversed fullmatch by text_start where 'basic_match' (:15167) and the search
    loop (:11880) both bound it by slice_start, so a general repeat asking whether its tail could
    match is told no:

        regex.compile(r'(?r)(ab)+').fullmatch('xabz', 1, 3)   # upstream: None; ours: (1, 3)

    Phase 7 owns the first two, because porting 'search_start' is the fix - and it must port the
    prefilter WITHOUT importing its answers, which is what the pinned tests are for. The third is
    upstream's to fix; see docs/plan/upstream-reports/LEDGER.md.

    'overlapped-skip-stale-slice' - an upstream bug, judged by S34, and not the prefilter: nothing
    but the verb is involved. A '(*SKIP)' moves slice_start mid-attempt (:14553) and nothing puts it
    back - init_match (:3404), do_match (:18121) and scanner_search_or_match (:20874) all leave it
    alone - so a scanner carries the slice into the next match, and an overlapped scan then resumes
    BELOW it at match_pos + 1 (:20903). Upstream's scanner then contradicts its own matcher:

        regex.compile(r'(?:[^\d](*SKIP)){2}').finditer('abcde', overlapped=True)
        # upstream (0,2) (1,3) (2,3) (3,5) - and its own .match('abcde', 2) is (2, 4)

    (2, 3) is one character for a pattern whose minimum width is two, and the answer is not even
    stable: a gc.collect() between iterations of the '{2,3}' form changes it. Upstream has since
    patched one consequence of the same carry-over (commit b77694a, issue 613) in the 2026.8.30
    release, past the pin. THIS PORT STILL CARRIES THE PRE-FIX CODE, in Matcher.cs's GreedyRepeatOne
    backtrack arm; the Phase 6 sync ports it, test-first.

    'reverse-group-call-direction' - an upstream bug that upstream has ALREADY FIXED, so there
    is nothing to report and the sync is where it goes away. build_GROUP() did not propagate the
    match direction into a called group, so a group called from a lookaround running the other way
    ran its body backwards and recorded the span without swapping its ends:

        regex.compile(r'(?r)(?<g>[ab]+)(?=(?&g))b').search('abbaa').spans('g')
        # upstream [(2, 1), (0, 2)] - an END BEFORE ITS START, rendered as ''; ours [(2, 5), (0, 2)]

    That is issue 614, fixed 2026-08-30 by commit 9398a6d and released in 2026.8.30. Upstream's own
    inline copy of the called body - '(?r)(?<g>[ab]+)(?=([ab]+))b' - already gives this port's
    answer. It also surfaces as an EMPTY capture rather than an inverted one, which is what seed 31
    draws for r'(?r)\b(?<g>[ab]+)(?=(?&g))b'; the entry covers both.

    'bounded-lazy-repeat-partial' - port right, and the one entry keyed on ROWS rather than on a
    predicate. A bounded lazy repeat that reaches its maximum loses its partial here:

        regex.compile('ba??x').match('baa', partial=True)   # upstream: (0, 3) partial; ours: None

    Pinned in PartialMatchingTests.cs with the measurement that matters most - S31 tried the obvious
    fix (ask the partial guard once more before the loop's limit break) and the second blind pass
    measured it over 20,160 targeted rows as 125 rows fixed and 219 introduced, because upstream's
    specialised arms also CAP the limit per tail op and never reach that position. It needs those
    arms, which are Phase 7's. Do not re-try the guard; read that test's comment first. S33 looked
    for a discriminator and found none that does not also swallow a genuine missed partial, so the
    entry lists the rows a probe has judged one at a time; widening it means judging another row,
    never loosening a condition.

    The recorder also neutralises upstream's other start-position prefilter, 'locate_required_string',
    for 'verbs' and 'partial-sliced' - see PREFILTER_FREE_GENERATORS in tools/record-oracle.py. That
    is a different mechanism from 'search_start', it IS reachable from Python where 'search_start' is
    not, and on a partial row it can suppress a partial outright rather than merely move an attempt.

.PARAMETER Seeds
    The generator seeds, run one after another. THREE IS THE FLOOR, and the default is three:
    7, 4242 and today's date as yyyyMMdd.

    One seed is how four divergence families stayed hidden until S33's blind review (DECISIONS
    2026-09-12). Every slice from S14 to S33 ran a single seed, each happened to be clean, and the
    conclusion "the wave is green" was drawn from it every time; running the same generators at
    seeds nobody had used turned up three unjudged families at once. A wave counts at three seeds
    and not before - VERIFICATION.md rule 7.

    Two of the three are fixed on purpose. 7 and 4242 are the seeds the families S34 classified
    were found at, so the default run is also their regression test; the date rotates daily, which
    is what keeps this a generative tester rather than a fixture. Pass an explicit list to
    reproduce one run - `-Seeds 4242` is a single seed, and is for minimising, not for believing.

    Comma-separated, like -Generator: `-Seeds 7,31,4242`. Not an [int[]], because `pwsh -File`
    passes every argument as a string and would bind the second seed to the next parameter.

.PARAMETER Count
    Rows per generator.

.PARAMETER Rows
    Record these explicit rows (JSONL) instead of generating any - the minimisation path.

.PARAMETER SkipRecord
    Re-run the consumer against the wave already on disk, without recording a new one.

.EXAMPLE
    tools/run-oracle.ps1
    tools/run-oracle.ps1 -Count 2000
    tools/run-oracle.ps1 -Seeds 4242
    tools/run-oracle.ps1 -Rows .scratch/candidate.jsonl
    tools/run-oracle.ps1 -SkipRecord
#>
[CmdletBinding()]
param(
    [string]$Generator = 'literals,literal-dot,anchors,classes,groups,quantifiers,boundaries,backrefs,case-folding,reverse,substitution,iteration,interactions,lookaround,conditionals,recursion,partial,partial-sliced,posix,verbs',
    [string]$Seeds = "7,4242,$(Get-Date -Format 'yyyyMMdd')",
    [int]$Count = 300,
    [string]$Rows,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$SkipRecord
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$wavePath = Join-Path $repoRoot 'TestResults/oracle/wave.jsonl'
$reportPath = Join-Path $repoRoot 'TestResults/oracle/report.txt'

# A run of explicit rows, and a re-run of the wave already on disk, are both a single wave by
# construction: neither has a seed to vary, and -1 stands for "this run has no seed". Everything
# else runs every seed.
#
# The `@()` goes OUTSIDE the `if`, and that is the whole of why this line is shaped this way.
# PowerShell unwraps a one-element array on assignment, so `$runs = if (...) { @(-1) }` leaves an
# Int32 and `$runs.Count` then throws under Set-StrictMode - which turned every single-seed run,
# every -Rows run and every -SkipRecord run into a crash AFTER the wave had been compared, with
# exit 1 and no verdict line. Found by S34's second blind pass.
$runs = @(
    if ($Rows -or $SkipRecord) {
        -1
    }
    else {
        # Comma-separated, like -Generator, and for the same reason: `pwsh -File` hands every
        # argument over as a string, so a real [int[]] parameter binds the second seed to the next
        # parameter instead. Measured the hard way in S34.
        $Seeds.Split(',') | Where-Object { $_.Trim() } | ForEach-Object { [int]$_.Trim() }
    }
)

$failed = @()

foreach ($seed in $runs) {
    if ($seed -ge 0) {
        Write-Host ''
        Write-Host "===== seed $seed =====" -ForegroundColor Cyan
    }

    if (-not $SkipRecord) {
        Write-Host 'Recording a wave from upstream...' -ForegroundColor Cyan
        $recorderArgs = @((Join-Path $PSScriptRoot 'record-oracle.py'), '--output', $wavePath)
        if ($Rows) {
            $recorderArgs += @('--rows', $Rows)
        }
        else {
            $recorderArgs += @('--generator', $Generator, '--count', $Count, '--seed', $seed)
        }

        python @recorderArgs
        if ($LASTEXITCODE -ne 0) {
            # A wave that did not record is not a wave that agreed. The most likely cause by far is
            # the version policy: `pip install regex` on a dev machine, or a stale submodule.
            Write-Host 'Oracle: RED - the recorder failed, so nothing was compared.' -ForegroundColor Red
            exit 1
        }
    }

    if (-not (Test-Path -LiteralPath $wavePath)) {
        Write-Host "Oracle: RED - there is no wave at $wavePath." -ForegroundColor Red
        exit 1
    }

    Write-Host ''
    Write-Host "Running the consumer ($Configuration)..." -ForegroundColor Cyan
    # Deleted first, so a consumer that fails before it writes one - a malformed wave, a build error -
    # cannot leave the previous run's report on screen as if it described this one.
    if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }
    # A non-zero exit means a row diverged, or the harness's own tests failed. Either way the report
    # below is what says which, so the exit code is captured rather than thrown on.
    dotnet test (Join-Path $repoRoot 'tests/FuzzyRegex.OracleTests/FuzzyRegex.OracleTests.csproj') `
        --configuration $Configuration
    $consumerExit = $LASTEXITCODE

    Write-Host ''
    if (Test-Path -LiteralPath $reportPath) {
        Get-Content -LiteralPath $reportPath | Write-Host
    }
    else {
        Write-Host "No report at $reportPath - the consumer did not get as far as the wave." -ForegroundColor Yellow
    }

    if ($consumerExit -ne 0) {
        $failed += $seed
        # The next seed overwrites both files, so a red run's evidence is kept under its own seed
        # or it is gone by the time the summary prints.
        if ($seed -ge 0) {
            Copy-Item -LiteralPath $wavePath -Destination (Join-Path $repoRoot "TestResults/oracle/wave-$seed.jsonl") -Force
            if (Test-Path -LiteralPath $reportPath) {
                Copy-Item -LiteralPath $reportPath -Destination (Join-Path $repoRoot "TestResults/oracle/report-$seed.txt") -Force
            }
        }
    }
}

Write-Host ''
if ($failed.Count -eq 0) {
    $where = if ($runs.Count -eq 1 -and $runs[0] -lt 0) { 'the wave' } else { "all $($runs.Count) seeds" }
    Write-Host "Oracle: GREEN - no row diverged from upstream, at $where." -ForegroundColor Green
    exit 0
}

Write-Host "Oracle: RED at $($failed.Count) of $($runs.Count) seeds: $($failed -join ', ')" -ForegroundColor Red
Write-Host '  Each red seed kept its own wave and report at TestResults/oracle/wave-<seed>.jsonl' -ForegroundColor Yellow
Write-Host '  and report-<seed>.txt, so a later seed cannot overwrite the evidence.' -ForegroundColor Yellow
Write-Host '  Minimise each diverging row and pin it as a test: see the header of' -ForegroundColor Yellow
Write-Host '  tools/record-oracle.py for the workflow, VERIFICATION.md rule 7 for why.' -ForegroundColor Yellow
exit 1
