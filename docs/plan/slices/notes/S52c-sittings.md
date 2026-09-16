# S52c sitting notes

Per-sitting record for `docs/plan/slices/done/S52c-metamorphic-invariants.md`. The slice file keeps the
scope, the verification and the boxes; everything a sitting measured or decided goes here.

---

## Sitting 1 (2026-09-16) - CHECKPOINT, not closed

A deliberately short sitting, scoped by the orchestrator to two items and told not to start the
checker, a wave or a review: **scope item 1** (the invariant list, argued) and **scope item 5** (the
bounded thirty-minute try at a fuzzy second engine). Both are done. Nothing in `src/` changed and no
test moved, so the ratchet is untouched at 6144 / 6144 / 0.

### Scope item 1: the list is in `docs/ORACLE-INVARIANTS.md`, and it is argued from the ledger

The slice's starting list was twelve bullets. What is committed is **22 invariants across eight
groups**, each with an ID, a statement in terms of calls the recorder can make, a ground, a cost
tier and - the part that does the work - a **calibration**: the numbered `LEDGER.md` entries that
invariant would have caught **automatically**.

**Arguing from the ledger rather than from plausibility is the whole method here, and it is what
made the list defensible.** The ledger holds 24 entries, every one of them found by hand from a real
upstream run recorded in this repository. Walking all 24 and asking "which pair of doors contradicted
each other here" is what produced the groups, and it is checkable by anyone re-reading the ledger -
no claim about upstream's behaviour in the new file rests on my reasoning about what upstream
probably does. Sixteen of the 24 entries are reached by at least one invariant. The eight that are
not are listed at the foot of the file by name, so the next sitting does not assume the list is
total.

**Highest-yield invariant, by calibration:** `posix-chooses-among-flagless-answers`, which reaches
ledger 9, 16 and 23 - three entries, three different ways of breaking one documented contract
("`POSIX` chooses among the matches the ordinary engine can already make", quoted in entry 23). It
costs one extra call and the recorder already records a flagless twin for several controls.

**Four changes to the slice's starting list, each with its reason in the file:**

1. **`group-spans-inside-match` NARROWED to patterns with no `\K` and no group call.** Stated flat,
   as the slice had it, this one is FALSE: `\K` resets the reported match start, so a group that
   matched before it legitimately lies outside the reported span. `\K` is in the wave's alphabet -
   ledger 23's own pattern is `...\g<1>\K$` - so the unnarrowed form would have fired on every such
   row and flooded triage. That is precisely the failure the slice's own blind-review brief says to
   hunt for, found before the checker was written rather than after the wave.
2. **`fuzzy-budget-monotone-cost` REJECTED as stated, shipped narrowed to `BESTMATCH` only.** The
   slice asserted that loosening a budget "never increases the reported error count for the same
   span". Without `BESTMATCH` the engine reports the *first* acceptable match, not the cheapest, so a
   looser budget reaching a more expensive answer for the same span is legitimate. Only under
   `BESTMATCH` is the engine defined to report the cheapest. The existence limb is unaffected and
   ships as `fuzzy-budget-monotone-existence`.
3. **`v0-v1-agree-outside-nested-sets-and-full-case-folding` REJECTED outright.** Its exception list
   is open-ended - set operators, nested sets, a bare `[`, full case-folding and more - and an
   invariant whose exceptions are not a closed list cannot separate a bug from an exception.
   `inline-version-equals-flag` is the part of the idea that has actually caught something (ledger
   22) and it ships in its place.
4. **`reverse-mirrors-forward` DEFERRED, and `greedy-lazy-existence-agree` shipped instead.** `(?r)`
   makes the scan run right to left; it does not reverse the pattern's semantics, so "mirror" needs
   a pattern transformation the recorder would have to be trusted to get right - and a bug in that
   transformation is indistinguishable from a bug in the engine. `greedy-lazy-existence-agree`
   reaches the same reversed rows for two extra calls and no transformation.

**`bestmatch-no-worse` was STRENGTHENED**: the slice had only the error-count limb, but ledger 12 and
13 are both *existence* losses ("`BESTMATCH` loses a match that plain fuzzy matching finds"), so
existence is limb (a) and the count is limb (b).

**Scope item 7's gate row is handled, and it is `greedy-lazy-existence-agree` that handles it.**
Row 104366 - `(?r)\xdfﬁ(.*?)\b` as `match(subject, 2, 2, partial=True)` over `'ﬁı'` - must not be
pinned by agreement, because over the 33 cells of
`tools/probes/{upstream,port}-reversed-partial-ignores-the-slice-start.*` the two engines agree on 23
and differ on 10 and the 10 split both ways. The invariant that separates right from wrong there is
"a partial call may not deny what the same engine's greedy and lazy spellings of one pattern both
allow", and the file states explicitly that it does **not** wait on the owner's open `slice_start`
versus `text_start` ruling (ledger 24): if it fires on both engines, that is the finding. **The
remaining half of that box - actually running the row through the invariant - belongs to the
checker sitting.**

**One confounder is recorded rather than assumed away**, on `search-anchored-agree`: moving `pos`
from the search's start to the reported position changes what the slice start is, and `\A`, `^`,
`\G`, `\K`, a lookbehind or a reversed run-out may legitimately read it differently. The checker is
required to record the confounder-free twin's answer on a violation, so triage can tell "two doors
disagree" from "the two doors were asked different questions". Ledger 24 is exactly that ambiguity
and it is open.

### Scope item 5: no second fuzzy engine, and the wall is permissions

Recorded in full in `docs/plan/OPERATIONS.md` under "There is no second FUZZY engine on this
machine". The short version, all of it measured today inside the slice's thirty-minute box:

- `agrep` / `tre-agrep`: **ABSENT** - nothing under Git for Windows' `usr\bin` or chocolatey's
  `bin`; scoop is not installed.
- Perl `String::Approx`: **ABSENT** - `Can't locate String/Approx.pm in @INC`.
- `rapidfuzz`, `python-Levenshtein`, `fuzzysearch`: **not present** in Python312's `site-packages`
  nor in either repo venv.
- `fuzzysearch`, the one candidate with a pure-Python fallback: **NOT TESTED**, because
  `pip install` is refused in a non-interactive session - `This command requires approval`.

**The honest reading, and the reason the OPERATIONS entry labours it.** `pip`, `winget` and `choco`
are all installed on this machine and all three are outside the driver's Bash allowlist, so installs
and read-only queries are refused alike. "`fuzzysearch` could not be installed here" is a fact about
this session's permission scope and **not** evidence that it fails to build on Windows, which is the
claim it would be easy to record by mistake. The OPERATIONS entry gives the owner the single command
that settles it, and names the ceiling before the grant is spent: `fuzzysearch` does approximate
*substring* search, so it can cross-check an edit distance on a literal pattern and can say nothing
about `{e<=n}` applied to a class, a group or a repeat.

**A subagent was used for the sweep and its finding needed re-testing, which is the process note
worth carrying.** It reported all four candidates as failures with one root cause, "permission
denied". Three of the four re-tested the same way from this session; what it could not distinguish
is *absent* from *blocked*, and the distinction is the entire value of the record - `agrep` and
`String::Approx` are genuinely absent, `fuzzysearch` is merely blocked. The Perl `String::Approx`
check was not in its brief at all and is the only candidate that a machine with Git for Windows had
a real chance of already carrying.

**An instrument was deliberately not built.** An edit-distance DP is about fifteen lines of stdlib
Python and would independently confirm that a reported `fuzzy_counts` is achievable between a
literal pattern and the matched text. Scope item 5 asks for a second engine or an honest "nothing
does", not for a new instrument, so it is recorded in OPERATIONS as the recommendation for the
checker sitting rather than built here.

### What is left, in the order the next sitting should take it

1. **Scope item 2 - the checker in `tools/record-oracle.py`**, writing `selfContradiction: [<id>]`
   per row and counting violations in the wave summary. Take the `FREE` tier first
   (`fuzzy-counts-match-changes`, `captures-are-the-texts-of-spans`, `lastindex-participated`,
   `group-spans-inside-match`, `no-fault-where-a-twin-answers`): five invariants, zero extra upstream
   calls, and one of them reaches ledger 11's seven doors.
2. **Scope item 4 - the same checker in `OracleComparer`** over the port's answers.
3. **Scope item 3 - the three-seed 2000-row wave and the triage.** Entries 5, 11 and 13 re-found is
   the calibration the slice asks for; `posix-chooses-among-flagless-answers` should re-find 9, 16
   and 23 as well, and if it does not, that is a finding about the generators' POSIX coverage.
4. **Scope item 7's second half** - run gate row 104366 through `greedy-lazy-existence-agree` and
   record what it says.
5. **Scope item 6** - the `docs/VERIFICATION.md` paragraph. The last section of
   `docs/ORACLE-INVARIANTS.md` ("What this list does not do") is written to be the source for it.

**No review and no verifier ran in this sitting**, by the orchestrator's instruction, and the two
documents it produced have therefore had no blind pass over them. They are prose and a list, with no
`src/` or test change behind them; **the checker sitting's review must cover this delta as well as
its own**, because unreviewed work is what the skill's step 2 exists to catch.

---

## Sitting 2 (2026-09-16) - stopped by the orchestrator two minutes in

Not a failure. The owner departed and the orchestrator stopped the session; the only thing it had
produced was a first draft of `tools/probes/upstream-free-tier-invariant-grounds.py`, stashed and
handed to sitting 3, which finished it and ran it. Between the two sittings the owner and the
orchestrator installed TRE in WSL, so scope item 5 - which sitting 1 had closed as "nothing
reachable" - was reopened with a real second engine behind it.

---

## Sitting 3 (2026-09-16) - scope items 2, 3, 4, 5, 6 and 7 all closed

The whole of what sitting 1 left. Ratchet GREEN at 6144 / 6144 / 0 throughout: nothing in `src/`
changed, and the only test change is in `FuzzyRegex.OracleTests`, which the ratchet does not run.

### The route, because the orchestrator asked for it explicitly

The owner's same-result-cheapest-route rule (2026-09-16) says a step done by hand more than twice is
scripted and run over the whole set in one pass, and the classification is read off the output. That
shaped every decision here, so it is worth recording what it actually meant:

1. **The checker was built for the FREE tier only, and the FREE tier turned out to be seven
   invariants rather than five.** Sitting 1 listed five as free. Three more are free in practice
   because `_CONTROLS` has recorded the flagless twin since S48b: `bestmatch-no-worse`,
   `posix-chooses-among-flagless-answers` and `no-fault-where-a-twin-answers` read
   `bestmatchFreeOutcome`, `posixFreeOutcome`, `atomicFreeOutcome` and `pruneOutcome` off the row
   and ask upstream nothing. That is what let one sitting reach ledger 9, 11, 12, 13, 16 and 23's
   families at a call budget of zero - and zero extra calls also means the checker cannot be the
   thing that crashes upstream, which is the ledger-9 hazard the slice's own review brief names.
2. **One instrument, not a row-by-row read.** `tools/probes/invariant-triage.py` prints eligible-vs-
   fired per invariant, firings by generator, and - with `--detail` - the decisive fields of every
   firing row including its twins and, for a span violation, the offending group. Classification was
   read off that table by family. `--write-firing` then hands the firing rows straight to the second
   engine, so the pipeline is three commands and no hand-copying.
3. **Three seeds recorded in parallel as three processes**, about eight minutes wall clock for
   126,240 rows, rather than one sequential run of twenty-five. Recording is Python only, so the C#
   side was edited while it ran.

**The cost of getting this wrong was paid twice and it was worth paying.** The waves were recorded
THREE times: once with the first checker, once after the two narrowings its output forced, and once
more after the third. Each re-record is eight minutes and each was necessary, because a control run
partway through a slice measures a checker that no longer exists by the end of it - the trap the
skill records against S22. **Every number below is from the third and final recording, which is the
code being committed.**

### Scope item 2 - the checker in the recorder

`_structural_violations`, `_choosing_flag_violations`, `_control_violations` and `_twin_answered` in
`tools/record-oracle.py`, writing `selfContradiction: [<id>, ...]` onto the row and a per-id count
into the wave summary. The one invariant that cannot be read back off a row -
`captures-are-the-texts-of-spans`, because a row records a capture's SPAN and never its text - is
collected at record time in `_describe_match`, which is the only place the match object still
exists.

**Six new guards in `--self-check`**, because this checker's failure mode is SILENCE: a checker that
stops firing records a clean wave, and nobody can tell that from a wave with nothing wrong in it.
Each guard is a hand-corrupted row, so no upstream call is involved and none of them can flake.

**`--self-check` is RED on this machine before and after this sitting**, on a guard nothing here
touched: "an interpreter limit rather than a judgement about the pattern: was recorded as if it were
upstream's answer". Confirmed pre-existing by running `git show HEAD:tools/record-oracle.py` and
getting the identical single failure. The guard expects a 2,000-deep nested-group pattern to raise a
recursion error; Python 3.14 evidently no longer does. Not this slice's scope, not silenced, and
carried below.

### Scope item 3 - the three-seed wave, and the triage

**126,240 rows: 22 generators, 2,000 rows a generator, seeds 7, 4242 and 20260916.**

| Invariant | Eligible | Fired | Classification |
| --- | ---: | ---: | --- |
| `fuzzy-counts-match-changes` | 1,774 | 6 | **ledger 11**, re-found automatically |
| `bestmatch-no-worse` | 2,123 | 2 | **ledger 12's shape**, re-found automatically |
| `group-spans-inside-match` | 14,142 | 2 | the `overlapped-skip-stale-slice` family |
| `captures-are-the-texts-of-spans` | 34,721 | 0 | - |
| `lastindex-participated` | 11,684 | 0 | - |
| `posix-chooses-among-flagless-answers` | 5,562 | 0 | - |
| `no-fault-where-a-twin-answers` | 1,800 | 0 | - |

**Eligibility is the column that makes a zero mean anything**, and the triage instrument computes it
independently of the checker for exactly that reason: 0 of 34,721 is cheap evidence, 0 of 0 is a
check that is not running, and the wave summary prints those two identically. The first version of
the instrument got this wrong in the other direction - it defined `no-fault-where-a-twin-answers`'s
eligibility as the violation condition itself and printed "7 of 7", which says nothing at all.

**The calibration the slice asked for came back on two of the three predicted entries.** Ledger 11
and ledger 12 were both re-found, by machine, on rows no earlier sitting had seen and with nobody
looking. That is the claim `docs/ORACLE-INVARIANTS.md` existed to make good. Ledger 13 was not, and
9, 16 and 23 were not: 9 is a crash (the fault limb's business, and it did not draw one in 126,240
rows), and 16 is an overlapped SCAN dropping its longest match, which the implemented shape of
`posix-chooses-among-flagless-answers` does not reach - it compares two single answers at the same
start and treats a different start as ambiguity rather than as a finding. Recorded in the file.

**Ten candidates in 126,240 rows - one row in 12,624 - is as much the result as the six.** The
failure this list was most at risk of is an invariant that is false for a documented reason and
floods the ledger, which is the first thing the slice's review brief says to hunt for. It did not
happen, and the three narrowings below are why. **Not one of them was foreseen; every one was forced
by output.**

#### The six `fuzzy-counts-match-changes` rows (ledger 11)

Every one has counts and change positions describing different edit scripts - `counts=[0, 1, 0]`
beside a single substitution position, and so on. **Four of the six are PARTIAL matches**, which is
a concentration none of entry 11's seven hand-found doors pointed at and is the single most useful
thing this table says to the next sitting. The rows:

    wave-inv-7.jsonl:24809         interactions search    counts=[1,0,1] tallied=[0,0,2]
    wave-inv-7.jsonl:24857         interactions search    counts=[0,1,0] tallied=[1,0,0]  partial
    wave-inv-4242.jsonl:25881      interactions search    counts=[0,1,0] tallied=[1,0,0]  partial
    wave-inv-4242.jsonl:41059      fuzzy        fullmatch counts=[0,1,0] tallied=[1,0,0]  partial
    wave-inv-20260916.jsonl:24011  interactions fullmatch counts=[1,0,0] tallied=[0,0,1]  partial
    wave-inv-20260916.jsonl:25899  interactions fullmatch counts=[0,1,0] tallied=[1,0,0]  partial

Ledger 11 is **not re-opened for them**: it is already open, already reported upstream, and adding
six more doors to a seven-door entry buys nothing. What is recorded there instead is that the
invariant now finds them automatically.

#### The two `bestmatch-no-worse` rows (ledger 12's shape)

The clearer of the two, `wave-inv-7.jsonl:41035`:

    (?b)(?fi)(?:(?:[\U0001f600\U0001d518][ab]){e<=1}){s<=1,i<=1,d<=1}  fullmatch '\U0001d518S\U0001f3fb'
      upstream              no match
      bestmatchFreeOutcome  match (0,5) counts=[1, 1, 0]

`BESTMATCH` selecting nothing from a non-empty set, which is ledger entry 12 itself - fixed in this
port by S46, so the port does not reproduce it. The other, `wave-inv-7.jsonl:24182`, is the same
limb inside a `finditer` scan: upstream's scan is empty and the flagless scan finds `(2, 2)`.

#### The two `group-spans-inside-match` rows

Both `verbs`, both `finditer-overlapped`, both `(*SKIP)`, both reporting a capture OUTSIDE its own
match - group 1 at `(0, 9)` on a match of `(2, 7)`. This is the `overlapped-skip-stale-slice` family
already in `ExpectedDivergences.cs`, and **the three-seed oracle run in this same sitting printed one
of these two rows as `EXPECTED overlapped-skip-stale-slice` with both engines' answers side by
side**, which settles the direction without a probe: upstream reports group 1 at `(0, 9)` for every
match of the scan and this port reports it inside each match. So upstream breaks the invariant, the
port does not, and the existing entry already holds the judgement.

Worth noting for its own sake: this invariant was predicted to calibrate against nothing. It reached
a real family nobody aimed it at, which is the first evidence that the free tier is worth more than
its calibration column claims.

#### The three narrowings, each forced by output

1. **`bestmatch-no-worse` and `posix-chooses-among-flagless-answers` must compare two ANSWERS.**
   `_matches_of` renders a TIMEOUT as "no matches", so the existence limb read a row upstream spent
   ten seconds on as the flag choosing nothing from a non-empty set. Three of the first wave's four
   `bestmatch` firings and its single `posix` firing were that. Guarded.
2. **A TIMEOUT beside a RANKING flag's twin is not a fault.** Five of the fault limb's seven first
   firings were a `(?b)` row that timed out while the same row without `(?b)` answered. `BESTMATCH`
   is DOCUMENTED to do more work (`upstream/README.rst:592`) and POSIX's leftmost-longest must see
   every match at a position before picking the longest, so the twin finishing is a cost difference.
   **The narrowing deliberately keeps the timeout case for `atomicFreeOutcome` and `pruneOutcome`**,
   because those two constructs only ever REMOVE pruning - a row that hangs with them and finishes
   without them cannot be explained by cost, and that is ledger entry 10 itself. Narrowing any wider
   would have thrown that calibration away.
3. **A substitution twin that replaced NOTHING is not an answer.** The remaining two fault firings
   were `subf` rows raising `IndexError` while matching beside a twin that answered - the shape of
   ledger 6. **It is not ledger 6.** Section 7 of the grounds probe measures what it is: upstream's
   `subf` renders the template with `str.format` over the GROUP LIST, so `{0[2]}` on a pattern with
   no group 2 raises `IndexError: list index out of range`, and
   `regex.subf('abcdefgh', '{0[2]}...', 'abcdefgh')` raises it with no verb, no fuzzy section and no
   reversal anywhere. The template is only rendered where something MATCHED, and on both rows the
   twin replaced nothing - so it never rendered the template and never reached the question. The
   checker now tests the replacement count rather than excluding `sub` rows, so a twin that DID
   replace still proves the template good and makes the row's own raise a real finding again.

**My first reading of those two rows was wrong and the probe corrected it.** I hypothesised that
`{0[-2]}` indexes the matched TEXT and that the twin's match was a different length. It indexes the
group list, and the twin had not matched at all. Writing the probe rather than reasoning from the
row shape is what caught it, and it is the same lesson the owner's rule states from the other side.

### Scope item 4 - the same checker on the port

`tests/FuzzyRegex.OracleTests/SelfConsistency.cs`, deliberately parallel to the recorder's Python
statement for statement, over the port's own `MatchOutcome`. It reuses `OracleFuzzy`'s existing
`CountsAgreeWithPositions` rather than restating S47's property.

`OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts` is **renamed** to
`Our_own_answers_never_contradict_themselves` and widened from the one fuzzy limb to all three. Two
things were deliberately not weakened in the process:

- **The floor is still the FUZZY-match count**, not the match count. `tools/run-oracle.ps1`'s own
  documentation rests on this test refusing a single-generator wave, and it refuses one because such
  a wave holds no fuzzy match at all. Widening what is CHECKED must not widen what counts as a wave
  worth believing.
- **The failure message now says whether upstream broke the same invariant on the same row**, read
  off the new `OracleRow.SelfContradiction`. The two cases need completely different work - a port
  bug to minimise and fix, versus the port reproducing an inherited contradiction, which is a ledger
  entry and a judgement about which engine is right - and having to re-derive that by hand is what
  this slice exists to stop.

**`The_self_consistency_checker_fires_on_a_contradiction_and_not_on_a_narrowing`** is the new
direct test, and it is not redundant with the sweep: the sweep runs over whatever a wave happens to
hold, so a checker that silently stopped firing would leave it green. Eight hand-built cases, four
of them negative, including both `\K` and lookaround narrowings and the POSIX
positions-unavailable case.

The rename touched 15 live references across 8 files. The historical records under
`docs/plan/slices/done/` and `.../notes/` were deliberately left alone: they record what a past
session actually ran, and rewriting them would falsify it.

### Scope item 5 - TRE, and an honest ceiling

`tools/probes/tre-fuzzy-check.py`. One file, both halves: the Windows side classifies and batches,
then re-invokes itself inside WSL with `--in-wsl`, which is the only half that imports `tre`. ONE
WSL process per batch, as OPERATIONS requires.

- **`--self-test`: 4 of 4.** TRE answers existence and cheapest cost on four hand-built cases. This
  exists because a second engine that is never asked anything and a second engine that agrees print
  the same thing at the bottom of a report.
- **Over the three waves: 10 rows of 126,240 are in TRE's dialect, and all 10 CONFIRMED** - same
  existence, cheapest cost never above what upstream reported.
- **Of this slice's 10 violation rows, TRE could answer NONE.** Four `fullmatch`, three scans, one
  `partial`, two carrying flags TRE has no form for. Each row's reason is printed, never dropped.

**That is a fact about dialects, not about TRE**: it offers a SEARCH and nothing else, so every
anchored operation is out; its syntax is POSIX ERE, so `\p{...}`, backreferences, lookarounds, verbs
and `\K` are out; and it has no BESTMATCH, no per-section budget and no `fuzzy_changes`. The
generators that produce violations are exactly the ones whose alphabet is furthest from that core.
So on the rows this slice is about, the invariants are the instrument and the second engine is not
available - the reverse of the usual arrangement, and the reason the file exists.

**Two of TRE's answers were this probe's own bugs before they were evidence.** The first dialect
gate let `(?i)`-prefixed and lazily-quantified patterns through - `^\(` matched the `(` of `(?i)`
and left `?i)(?:fo` as the "body", which is all ordinary ERE characters - and TRE refused seven of
them. The corrected gate then reported one DIFFERENT row, `(?i)(?:fo){i<=2}` over `'F'`, where TRE
matched and upstream did not. **That was the probe, not either engine**: it defaulted an unnamed
error kind to unbounded where upstream documents it as forbidden (`upstream/README.rst:561`, "If a
certain type of error is specified, then any type not specified will **not** be permitted"), so
`{i<=2}` was being asked of TRE as "up to 2 insertions and unlimited deletions". Measured both
sides before believing either: TRE's `maxdel=2` matches `fo` against `'F'` at cost 1 and its
`maxins=2` does not, and upstream's `{d<=2}` matches where its `{i<=2}` does not - **the two engines
agree on which edit is which, and only the defaults were wrong.** After the fix: 10 of 10 CONFIRMED,
0 DIFFERENT, 0 refused.

### Scope item 7 - gate row 104366, settled as far as an invariant can settle it

`tools/probes/upstream-gate-row-greedy-lazy.py` and `tools/probes/port-gate-row-greedy-lazy.ps1`,
the same six-cell grid asked of each engine on its own.

**Upstream breaks `greedy-lazy-existence-agree` on 2 of 6 cells. This port breaks it on 0 of 6.**

    cell                             upstream lazy / greedy            port lazy / greedy
    the gate row                     (2,2) partial / NO MATCH          no match / no match
    without the reversal             (2,2) partial / (2,2) partial     (2,2) partial / (2,2) partial
    without the \b                   (2,2) partial / NO MATCH          no match / no match
    without the unmatchable prefix   (2,2) complete / complete         (2,2) complete / complete
    over the whole subject           (0,2) partial / (0,2) partial     (0,2) partial / (0,2) partial
    not partial                      no match / no match               no match / no match

Greediness orders the candidate set; it cannot change its membership. So upstream answering a
zero-width partial to `(.*?)` and no match to `(.*)` over the same empty slice is upstream
contradicting itself, and the port's "no match" - which the oracle could only report as a
disagreement - is the self-consistent answer. **On every cell where upstream is self-consistent the
two engines agree**, which is what says the disagreement is about that contradiction and nothing
else.

**The ablations attribute it**: removing the `(?r)` makes the invariant hold AND makes both engines
agree; removing the `\b` does neither. The mechanism is the reversed partial path, not the boundary.

**What is deliberately NOT done: the row is not pinned.** This settles which engine is
self-consistent on 104366. It does not settle ledger 24's open question of whether upstream's
reversed run-out should read `slice_start` or `text_start` - that is the owner's ruling and S52d's
slice - and the slice file is explicit that this invariant should not wait for it. The finding is
recorded and handed on.

### Review

**One blind pass, Opus, over the whole diff AND over sitting 1's two unreviewed documents** - the
review debt STATE.md recorded is paid. **Four findings raised, four reproduced, four fixed, and
every one of them was a defect this sitting's own three-seed wave had NOT caught.** That is the
single most useful thing to carry out of here: a wave of 126,240 rows is not a substitute for a
reviewer, because three of the four live on shapes no generator draws.

1. **`posix-chooses-among-flagless-answers`'s cost limb was FALSE, and it is the one finding that
   would have produced wrong ledger entries.** I had compared costs at the same START; ledger 9 is
   about the same SPAN. POSIX leftmost-longest is DEFINED to buy length with errors, so a longer
   POSIX match at the same start legitimately costs more. Reproduced:

       (?p)(?:abc){e<=2}  over 'abxxyc'  ->  span (0, 4), counts (1, 1, 0)
           (?:abc){e<=2}  over 'abxxyc'  ->  span (0, 3), counts (1, 0, 0)

   POSIX is longer - so my own "longest" limb holds - and therefore dearer, so my cost limb fired on
   a textbook-correct answer. Fixed to require the same span, which is what entry 9 actually says.
   **BESTMATCH deliberately keeps the same-START form**, because it minimises errors and is free to
   choose any length to do it; the asymmetry is now stated in the code and guarded both ways. The
   reviewer also measured why the wave missed it: only 29 of the 1,122 rows that reach this
   comparison carry fuzzy counts on either side.
2. **The TRE dialect gate admitted `.`, `^` and `$`, which mean different things to the two engines
   once a newline is in the subject.** Measured on both engines: `(?:a.c){e<=1}` over `'a\ncc'` is
   upstream `(0, 3)` at cost 1 - `.` cannot take the newline, so it spends a substitution - and TRE
   `(0, 3)` at cost 0, which my probe would have printed as **CONFIRMED**. A silent false
   confirmation is the worst thing a second engine can do. `(?:abc$){e<=0}` over `'abc\n'` goes the
   other way: upstream matches, TRE does not. Gated on the subject as well as the pattern, so a row
   with no newline anywhere is not thrown away for nothing.
3. **The TRE gate's parenthesis check counted rather than parsed.** `_WHOLE_PATTERN_BUDGET`'s greedy
   `.*` turns `(a)(b){e<=1}` into the body `a)(b`, which has one of each - so it passed, and the
   budget there governs only `(b)`. The same shape as the `?i)(?:fo` hole the gate's own comment
   says testing the whole pattern closes, which is the uncomfortable part: I wrote that comment and
   then made the same mistake four lines down. Fixed with a nesting scan.
4. **`captures-are-the-texts-of-spans` was the one invariant with no `--self-check` guard**, in the
   file whose whole argument is that silence is this checker's failure mode. It is the one collected
   through a threaded `violations` argument inside `_describe_match` rather than read off a row, so
   a guard over `_structural_violations` could not reach it, and the reviewer demonstrated that
   disabling the check outright changed nothing `--self-check` printed. Guarded now with a stub
   match object whose `captures` and `spans` disagree, in both directions.

**No second pass was needed**, and the judgement is about what the fixes touched rather than about
how the first pass went. All four are edits inside functions the reviewer read, they add no public
API and no new file, and the three that change behaviour are each guarded by a new `--self-check`
case that the reviewer's own reproductions define. What the fixes DID invalidate is the wave, so the
three seeds were recorded a fourth time against the committed code and every number in this file and
in `docs/ORACLE-INVARIANTS.md` is from that run.

**Findings raised 4, reproduced 4, fixed 4, second pass not needed.**

### Verifier

A fresh Opus subagent, briefed with the commit-ready tree and nothing else, re-ran twelve claims.
**Eleven CONFIRMED, one COULD NOT RUN.**

The one that matters most is claim 3, because it is the claim the skill's rule against S22 exists
for: the verifier **re-recorded seed 4242 itself** with the recorder as it now stands and got
`42080 rows` and `3 rows contradict themselves: 2 fuzzy-counts-match-changes, 1
group-spans-inside-match`, matching the on-disk wave row for row. So the table in this file and in
`docs/ORACLE-INVARIANTS.md` is a property of the committed code and not of some intermediate one.
It also independently reproduced all seven sections of the grounds probe, both gate-row grids (2 of
6 on upstream, 0 of 6 on the port), TRE's 4-of-4 self-test and its 10-of-10, and all three of the
blind review's fixes - including that `(?p)(?:abc){e<=2}` over `'abxxyc'` now records "no row
contradicts itself".

**COULD NOT RUN: claim 12, the three-seed `tools/run-oracle.ps1`.** It polled for about ten minutes
and the run had only finished recording seed 7, so it had no verdict line to report and correctly
refused to invent one. **Re-run in the same sitting by the slice itself, after every fix was in
place**, which is what the claim now rests on:

    agree 6366  unsupported 0  expected 8  timeout 2  resource 4  diverge 0  of 6380 rows
    agree 6373  unsupported 0  expected 2  timeout 0  resource 5  diverge 0  of 6380 rows
    agree 6371  unsupported 0  expected 4  timeout 0  resource 5  diverge 0  of 6380 rows
    Oracle: GREEN - no row diverged from upstream, at all 3 seeds.

One incidental measurement worth keeping from the verifier's partial run: the checker fires on the
DEFAULT 6,380-row wave too - seed 7 gives "2 rows contradict themselves: 1 bestmatch-no-worse, 1
group-spans-inside-match" - so a slice does not need a 2,000-row sweep to see this instrument work.

### What is left for a later sitting

1. **The `+1` and `+2` tiers.** The strongest single candidate is `posix-chooses-among-flagless-
   answers` limb (b) over a SCAN rather than over one answer, which is what would reach ledger 16;
   it needs the position sampling `search-none-anchored-none` also wants.
2. **A negative-length group span is a free invariant this file does not have.** `_to_index_length`
   can render an end-before-start span, which is impossible, and that is ledger 8 and the
   `group-call-direction` family. Left out here on purpose - it would fire only on rows already
   pinned by `ExpectedDivergences.cs` - but it is the cheapest extension available.
3. **Four of the six ledger-11 firings are PARTIAL matches.** Nothing in entry 11's seven hand-found
   doors points at partial matching, so that is a mechanism worth minimising.
4. **`--self-check`'s interpreter-limit guard is RED on Python 3.14**, pre-existing and unrelated.
