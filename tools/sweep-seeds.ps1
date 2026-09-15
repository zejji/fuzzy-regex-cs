<#
.SYNOPSIS
    Runs the whole default oracle wave at many fresh seeds and summarises every one.

.DESCRIPTION
    THE INSTRUMENT PHASE 5 PAID FOR FIVE TIMES. S33, S34, S35, S40a and S43 each found a real defect
    at a seed no earlier slice had used, on generators that had already been swept at HIGHER row
    counts - and S43's closing note names the lesson outright: "read the seed count, not the row
    count, as the thing that finds defects". `tools/run-oracle.ps1` runs three seeds because three is
    the floor (VERIFICATION.md rule 7a). This runs twenty, because a seed costs about a minute and
    the alternative is finding the twentieth seed's defect in Phase 7 instead.

    Every seed is DRAWN AND RECORDED. The default is to draw them from a master seed with
    `System.Random`, so the whole sweep is reproducible from one number printed at the top and
    written into the log; pass -Seeds to re-run named ones. A seed nobody can re-draw is a red run
    nobody can reproduce, which is the same failure the wave header exists to prevent one level down.

    RESUMABLE PER SEED, so this can be launched detached and survive a machine restart or a kill.
    Each finished seed appends one JSON object to the log; a re-run with the same log skips every
    seed already in it. That is also why the log is JSONL and not a transcript - it is read back.

    NOT A MERGE GATE, for the reasons in `tools/run-oracle.ps1`'s header, and not a slice's exit gate
    either: a slice runs three seeds. This is what the orchestrator runs overnight and what
    `.github/workflows/oracle.yml` runs weekly on a rotating master seed.

.PARAMETER Seeds
    Comma-separated seeds to run instead of drawing any. For reproducing a red seed, or a shortlist.
    Comma-separated rather than [int[]] for the reason `run-oracle.ps1` documents: `pwsh -File`
    passes every argument as a string and would bind the second seed to the next parameter.

.PARAMETER SeedCount
    How many seeds to draw when -Seeds is not given. Twenty by default.

.PARAMETER MasterSeed
    The seed the drawn seeds come from. Omitted means a random one, printed and logged.

.PARAMETER Count
    Rows per generator per seed. 2000 by default - the count the divergence families this repo has
    already judged were found at, and ten times the default wave's 300.

.PARAMETER Generator
    Comma-separated generator names, passed straight to `run-oracle.ps1`. All of them by default.

.PARAMETER Log
    The JSONL progress log. Read back for resumption, so pointing two concurrent sweeps at one file
    would have them skip each other's seeds; give each its own.

.PARAMETER Configuration
    Debug or Release. RELEASE IS THE DEFAULT, for run-oracle.ps1's reason as well as this one: the
    consumer is the slow half of a sweep, AND a Debug consumer makes OracleComparer's 10s row
    timeout measure the build instead of the engine (S52 sitting 6). This splat always passes the
    value on, so run-oracle.ps1's own default never reaches a sweep - the two have to agree by
    hand.

.EXAMPLE
    pwsh -File tools/sweep-seeds.ps1
    pwsh -File tools/sweep-seeds.ps1 -SeedCount 40 -Configuration Release
    pwsh -File tools/sweep-seeds.ps1 -Seeds 99991,31337
    pwsh -File tools/sweep-seeds.ps1 -MasterSeed 20260915        # re-draws the same twenty
#>
[CmdletBinding()]
param(
    [string]$Seeds,
    [int]$SeedCount = 20,
    [int]$MasterSeed = -1,
    [int]$Count = 2000,
    [string]$Generator,
    [string]$Log = 'TestResults/oracle/sweep.jsonl',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$runOracle = Join-Path $PSScriptRoot 'run-oracle.ps1'
$reportPath = Join-Path $repoRoot 'TestResults/oracle/report.txt'
$wavePath = Join-Path $repoRoot 'TestResults/oracle/wave.jsonl'
$logPath = if ([System.IO.Path]::IsPathRooted($Log)) { $Log } else { Join-Path $repoRoot $Log }

# Drawn from an explicitly seeded generator rather than from Get-Random's implicit state, so that
# the master seed printed below genuinely reproduces the list. `1 shl 30` is the recorder's own
# range for a random seed, so a swept seed and a recorder-drawn one are the same kind of number.
if ($Seeds) {
    $planned = @($Seeds.Split(',') | Where-Object { $_.Trim() } | ForEach-Object { [int]$_.Trim() })
    $MasterSeed = -1
}
else {
    if ($MasterSeed -lt 0) { $MasterSeed = [System.Random]::new().Next(1 -shl 30) }
    $draw = [System.Random]::new($MasterSeed)
    $planned = @(1..$SeedCount | ForEach-Object { $draw.Next(1 -shl 30) })
}

# Resumption. A seed is "done" when its own line reached the log, so a seed killed mid-run is simply
# absent and gets redone - which is the right way round, because a half-consumed wave has no verdict.
#
# KEYED ON THE WHOLE QUESTION, NOT ON THE SEED. A seed is only "already done" if it was run at the
# same row count, the same generator list and the same configuration - otherwise re-running the same
# seeds at a higher count, or naming one explicitly to reproduce a red run, silently skips every one
# of them and still prints a verdict and an exit code for rows it never compared. Found by this
# slice's blind review, which reproduced both halves.
$questionKey = "$Count|$Generator|$Configuration"
$alreadyDone = @{}
if (Test-Path -LiteralPath $logPath) {
    foreach ($line in Get-Content -LiteralPath $logPath) {
        if (-not $line.Trim()) { continue }
        # -AsHashtable throughout: an empty JSON object parses to an empty hashtable, whose .Keys is
        # safe, where a PSCustomObject's .PSObject.Properties.Name throws under Set-StrictMode. Found
        # by the first smoke run of this script, on the very first all-green seed.
        $entry = $line | ConvertFrom-Json -AsHashtable
        if (-not $entry.ContainsKey('seed')) { continue }
        # A log written before this key existed has no `question`, so it never matches and its seeds
        # are re-run rather than silently trusted.
        if ($entry['question'] -eq $questionKey) { $alreadyDone[[int]$entry['seed']] = $true }
    }
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $logPath) | Out-Null

Write-Host ''
Write-Host "Sweeping $($planned.Count) seeds at $Count rows a generator, $Configuration." -ForegroundColor Cyan
if ($MasterSeed -ge 0) {
    Write-Host "  master seed $MasterSeed - re-draw this exact list with -MasterSeed $MasterSeed" -ForegroundColor Cyan
}
Write-Host "  seeds: $($planned -join ', ')" -ForegroundColor Cyan
Write-Host "  log:   $logPath" -ForegroundColor Cyan
if ($alreadyDone.Count -gt 0) {
    Write-Host "  resuming: $($alreadyDone.Count) seeds already in the log will be skipped" -ForegroundColor Yellow
}

$redSeeds = @()
$ran = 0

foreach ($seed in $planned) {
    if ($alreadyDone.ContainsKey($seed)) {
        Write-Host "===== seed $seed - already in the log, skipped =====" -ForegroundColor DarkGray
        continue
    }

    Write-Host ''
    Write-Host "===== seed $seed ($($ran + 1) of $($planned.Count - $alreadyDone.Count)) =====" -ForegroundColor Cyan
    $started = Get-Date

    $oracleArgs = @{ Seeds = "$seed"; Count = $Count; Configuration = $Configuration }
    if ($Generator) { $oracleArgs['Generator'] = $Generator }

    # DELETED BEFORE THE RUN, not after it. `run-oracle.ps1` clears the report only once it reaches
    # the consumer, so a seed that dies EARLIER - the recorder failing, which its own header calls
    # "the most likely cause by far" - leaves the PREVIOUS seed's report on disk, and the parsing
    # below would then log this seed with another seed's tally and another seed's diverging
    # generators. Reproduced by this slice's blind review with an unknown generator name.
    if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }

    # `&` rather than dot-sourcing: run-oracle.ps1 calls `exit`, and dot-sourcing it would end this
    # script with it on the first seed. Output goes to the host as it arrives so a detached run's
    # transcript shows progress, and the verdict is read from the report file below rather than from
    # that text.
    & $runOracle @oracleArgs
    $green = $LASTEXITCODE -eq 0
    $elapsed = [int]((Get-Date) - $started).TotalSeconds

    $tally = ''
    $diverged = 0
    $byGenerator = @{}
    $expectedBy = @{}
    if (Test-Path -LiteralPath $reportPath) {
        foreach ($line in Get-Content -LiteralPath $reportPath) {
            if (-not $tally -and $line -match '^agree \d') {
                $tally = $line.Trim()
                if ($tally -match 'diverge (\d+)') { $diverged = [int]$Matches[1] }
                continue
            }
            # `DIVERGE row 33858 (partial) search flags=0x0 version=V0`
            if ($line -match '^DIVERGE row \d+ \(([^)]+)\)') {
                $byGenerator[$Matches[1]] = 1 + ($byGenerator[$Matches[1]] ?? 0)
            }
            # `EXPECTED turkic-default-folding row 16362 (case-folding) match ...`
            elseif ($line -match '^EXPECTED (\S+) row \d+ \(([^)]+)\)') {
                $key = "$($Matches[1])/$($Matches[2])"
                $expectedBy[$key] = 1 + ($expectedBy[$key] ?? 0)
            }
        }
    }

    # Every seed keeps its wave and report, not only the red ones: at twenty seeds the run that
    # matters is often one nobody was watching, and the next seed overwrites both files.
    $keep = Join-Path $repoRoot "TestResults/oracle/sweep-$seed"
    New-Item -ItemType Directory -Force -Path $keep | Out-Null
    if (Test-Path -LiteralPath $reportPath) { Copy-Item -LiteralPath $reportPath -Destination (Join-Path $keep 'report.txt') -Force }
    if (-not $green -and (Test-Path -LiteralPath $wavePath)) {
        Copy-Item -LiteralPath $wavePath -Destination (Join-Path $keep 'wave.jsonl') -Force
    }

    # THE PARSING IS THE ONE THING HERE THAT CAN BE SILENTLY WRONG, so it is checked rather than
    # trusted. If the report's first line ever stops reading `agree N ... diverge N ... of N rows`,
    # every later seed would record `diverged = 0` and a genuine divergence would be logged as a
    # harness failure - a sweep that reports the opposite of the truth, unattended, for as long as
    # nobody looks. There is no format to parse when run-oracle never produced a report at all, which
    # is itself a failure and is reported as one.
    if (-not $tally) {
        Write-Host "seed $seed - the consumer produced no summary line to read." -ForegroundColor Red
        Write-Host '  Either it did not get as far as the wave, or the report format changed and the' -ForegroundColor Red
        Write-Host '  match on `^agree \d` in this script is stale. Fix that before trusting a sweep.' -ForegroundColor Red
        throw "no summary line in $reportPath for seed $seed"
    }

    # A non-zero exit means EITHER a row diverged OR the harness's own tests failed, and over twenty
    # unattended seeds those need telling apart in the log rather than in a transcript nobody reads.
    # The second is not hypothetical: `Our_own_change_positions_always_agree_with_our_own_counts`
    # refuses a wave holding no fuzzy match at all, so `-Generator case-folding` reds every seed with
    # `diverge 0` - measured 2026-09-15 while smoke-testing this script.
    $harnessFailed = (-not $green) -and $diverged -eq 0

    $entry = [ordered]@{
        seed          = $seed
        question      = $questionKey
        masterSeed    = $MasterSeed
        count         = $Count
        configuration = $Configuration
        green         = $green
        harnessFailed = $harnessFailed
        diverged      = $diverged
        seconds       = $elapsed
        tally         = $tally
        divergeBy     = $byGenerator
        expectedBy    = $expectedBy
        recordedAt    = (Get-Date).ToUniversalTime().ToString('o')
    }
    Add-Content -LiteralPath $logPath -Value ($entry | ConvertTo-Json -Compress -Depth 4)

    $ran++
    if ($harnessFailed) {
        $redSeeds += $seed
        Write-Host "seed $seed RED in ${elapsed}s - the HARNESS's own tests failed, no row diverged" -ForegroundColor Red
        Write-Host "  kept at $keep - read the run's own output, not the report" -ForegroundColor Yellow
    }
    elseif (-not $green) {
        $redSeeds += $seed
        $where = ($byGenerator.Keys | Sort-Object | ForEach-Object { "$_ x$($byGenerator[$_])" }) -join ', '
        Write-Host "seed $seed RED in ${elapsed}s - $diverged diverging rows: $where" -ForegroundColor Red
        Write-Host "  kept at $keep" -ForegroundColor Yellow
    }
    else {
        Write-Host "seed $seed green in ${elapsed}s" -ForegroundColor Green
    }
}

Write-Host ''
Write-Host '===== sweep summary =====' -ForegroundColor Cyan
Write-Host "  $ran seeds run, $($alreadyDone.Count) skipped as already logged"
if ($MasterSeed -ge 0) { Write-Host "  master seed $MasterSeed" }

# Read the WHOLE log rather than this run's seeds, so a resumed sweep summarises the sweep and not
# the fragment of it this invocation happened to do.
# FILTERED TO THIS QUESTION, for the same reason resumption is keyed on it. One log can now hold the
# same seed at two row counts, so summarising every line would double-count seeds and could report
# RED for a sweep whose every seed at THIS question was green - reproduced by this slice's second
# blind pass on a log holding seed 7 red at 300 rows and green at 2000.
$all = @(
    Get-Content -LiteralPath $logPath
    | Where-Object { $_.Trim() }
    | ForEach-Object { $_ | ConvertFrom-Json -AsHashtable }
    | Where-Object { $_['question'] -eq $questionKey }
)
$red = @($all | Where-Object { -not $_['green'] })

$perGenerator = @{}
foreach ($entry in $all) {
    foreach ($name in $entry['divergeBy'].Keys) {
        $perGenerator[$name] = $entry['divergeBy'][$name] + ($perGenerator[$name] ?? 0)
    }
}

$harness = @($red | Where-Object { $_['harnessFailed'] })
Write-Host "  log holds $($all.Count) seeds at this question ($questionKey): $($all.Count - $red.Count) green, $($red.Count) red"
if ($harness.Count -gt 0) {
    $harnessSeeds = ($harness | ForEach-Object { $_['seed'] } | Sort-Object) -join ', '
    Write-Host "  $($harness.Count) of the red seeds are the HARNESS's own tests, not a divergence: $harnessSeeds" -ForegroundColor Yellow
}
if ($perGenerator.Count -gt 0) {
    Write-Host '  unaccounted divergences by generator:' -ForegroundColor Red
    foreach ($name in ($perGenerator.Keys | Sort-Object)) {
        Write-Host ("    {0,-16} {1}" -f $name, $perGenerator[$name]) -ForegroundColor Red
    }
}

if ($red.Count -eq 0) {
    Write-Host ''
    Write-Host "Sweep: GREEN - no row diverged from upstream at any of $($all.Count) seeds." -ForegroundColor Green
    exit 0
}

Write-Host ''
$redSeedList = ($red | ForEach-Object { $_['seed'] } | Sort-Object) -join ', '
Write-Host "Sweep: RED at $($red.Count) of $($all.Count) seeds: $redSeedList" -ForegroundColor Red
Write-Host '  Each red seed kept its wave and report at TestResults/oracle/sweep-<seed>/.' -ForegroundColor Yellow
Write-Host '  Reproduce one with: pwsh -File tools/run-oracle.ps1 -Seeds <seed> -Count' $Count -ForegroundColor Yellow
Write-Host '  Then minimise it and judge it - see VERIFICATION.md rule 7 and design spec amendment 16.' -ForegroundColor Yellow
exit 1
