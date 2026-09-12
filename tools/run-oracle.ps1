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

    'partial-sliced' joined the default list in S33. 'verbs' did NOT, and the reason changed:
    it is no longer the four judged rows S29 held it out for - those are classified now - but a
    FIFTH, unjudged family the S33 blind review found by running the generator at seeds S29 never
    used. Details below under "What S33 found and did not fix".

    Both generators were held out because a handful of their rows diverge for a reason already
    judged, and holding a whole generator out for a few per cent of its rows trades all of its
    coverage for none. What replaced that is an accounted-for list,
    tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs: three named families, each with the reason,
    the engine that is right, the permanent test that pins this port's answer, and the minimised row
    it was found on. A row one of them accounts for is printed in the report as 'EXPECTED <id>' and
    tallied separately; every other divergence still reds the run.

    Read that file before trusting a green wave. It is NOT xfail_strict - a wave's rows come from a
    random seed, so there is no row identity to pin - and what makes it honest instead is
    'Every_expected_divergence_still_diverges', which runs each entry's minimised row through the
    live engine on every oracle run and fails when one stops diverging.

    The three families, in short:

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
    upstream's to fix; see docs/plan/upstream-reports/2026-09-12-draft.md.

    The recorder also neutralises upstream's other start-position prefilter, 'locate_required_string',
    for 'verbs' and 'partial-sliced' - see PREFILTER_FREE_GENERATORS in tools/record-oracle.py. That
    is a different mechanism from 'search_start', it IS reachable from Python where 'search_start' is
    not, and on a partial row it can suppress a partial outright rather than merely move an attempt.

    WHAT S33 FOUND AND DID NOT FIX. Running the generators at seeds no earlier slice had used turned
    up three unjudged divergences, none of them caused by S33's change (each reproduces identically
    on the commit before it) and none of them in its scope. **A default wave is NOT reliably green
    today**, and was not before S33 either - the single seed each slice happened to run was what made
    it look so. All three are reproduced, with their commands, in docs/plan/STATE.md and are the
    subject of the next slice.

    1. Forward '(*SKIP)' inside a bounded repeat, and the reason 'verbs' stays off this list. Red at
       seeds 7, 31 and 4242; clean only at 20260913, which is the seed S29 quoted:

           regex.finditer(r'(?:[^\d](*SKIP)){2,3}', '\r\naabb ', regex.M, overlapped=True)
           # upstream (0,3) (1,4) (2,4) (3,4) (4,7) (5,7); this port (2,5) and (3,6) in the middle

       Not the prefilter: upstream answers the same with 'locate_required_string' switched off.

    2. A reversed group call records a capture whose end is before its start, which is upstream's
       own number and not a translation slip here:

           regex.compile(r'(?r)(?<g>[ab]+)(?=(?&g))b').search('>abbaa\r<').spans('g')
           # [(3, 2), (1, 3)] - the first is an inverted span for an empty capture

       This port records (3, 3) for it. 'recursion' is in the list below and has been since S30, so
       this one reds a default wave at seed 4242 whatever happens to 'verbs'.

    3. The bounded-lazy-repeat family below, which the note used to say no generator reaches. It
       does: 'partial' and 'partial-sliced' at seed 314159 give two rows of it, '^([A-Z]??)__$' over
       '__aA ' and '(?r)A(.??)' over '_ﬃ'[0:2]. Judged and PORT RIGHT, so it could be
       classified - it is not, because no discriminator for it has been found that does not also
       swallow a real missed partial. Until one is, this is a known red.

    A FOURTH FAMILY, and the one item 3 above is about. A bounded lazy repeat that reaches its
    maximum loses its partial:

        regex.compile('ba??x').match('baa', partial=True)   # upstream: (0, 3) partial; ours: None

    Pinned in PartialMatchingTests.cs with the measurement that matters most - S31 tried the obvious
    fix (ask the partial guard once more before the loop's limit break) and the second blind pass
    measured it over 20,160 targeted rows as 125 rows fixed and 219 introduced, because upstream's
    specialised arms also CAP the limit per tail op and never reach that position. It needs those
    arms, which are Phase 7's. Do not re-try the guard; read that test's comment first.

.PARAMETER Seed
    The generator seed. Omitted, the recorder picks one at random and records it in the wave
    header, which is what makes a divergence reproducible.

.PARAMETER Count
    Rows per generator.

.PARAMETER Rows
    Record these explicit rows (JSONL) instead of generating any - the minimisation path.

.PARAMETER SkipRecord
    Re-run the consumer against the wave already on disk, without recording a new one.

.EXAMPLE
    tools/run-oracle.ps1
    tools/run-oracle.ps1 -Count 2000
    tools/run-oracle.ps1 -Rows .scratch/candidate.jsonl
    tools/run-oracle.ps1 -SkipRecord
#>
[CmdletBinding()]
param(
    [string]$Generator = 'literals,literal-dot,anchors,classes,groups,quantifiers,boundaries,backrefs,case-folding,reverse,substitution,iteration,interactions,lookaround,conditionals,recursion,partial,partial-sliced,posix',
    [int]$Seed = -1,
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

if (-not $SkipRecord) {
    Write-Host 'Recording a wave from upstream...' -ForegroundColor Cyan
    $recorderArgs = @((Join-Path $PSScriptRoot 'record-oracle.py'), '--output', $wavePath)
    if ($Rows) {
        $recorderArgs += @('--rows', $Rows)
    }
    else {
        $recorderArgs += @('--generator', $Generator, '--count', $Count)
        if ($Seed -ge 0) { $recorderArgs += @('--seed', $Seed) }
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

Write-Host ''
if ($consumerExit -eq 0) {
    Write-Host 'Oracle: GREEN - no row diverged from upstream.' -ForegroundColor Green
    exit 0
}

Write-Host 'Oracle: RED' -ForegroundColor Red
Write-Host "  The wave that found it is at $wavePath, and the header records its seed." -ForegroundColor Yellow
Write-Host '  Minimise each diverging row and pin it as a test: see the header of' -ForegroundColor Yellow
Write-Host '  tools/record-oracle.py for the workflow, VERIFICATION.md rule 7 for why.' -ForegroundColor Yellow
exit 1
