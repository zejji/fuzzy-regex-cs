# Roadmap

Phases, in order, from the design spec section 12. This file lists **what** each phase covers and
nothing about how far along it is: status lives in `docs/STATUS.md` (generated from the test
suite) and in `docs/plan/slices/` versus `docs/plan/slices/done/`. Duplicating status here would
just give it somewhere to rot.

Slice files are authored **one phase ahead only**. A detailed plan for phase 5 written today
would be wrong by the time phase 5 arrives, for the same reason a stale TODO list is.

| Phase | Content | Sessions (est.) | Main model |
|---|---|---:|---|
| 0 | Scaffolding: solution, build props, CI, skills, driver script, budget gate, slice files for phase 1 | 1-2 | Opus |
| 1 | Port the full upstream test suite, all skipped initially | 3-6 | Sonnet under Opus |
| 2 | **Compile-parity corpus first**, then parser and compiler (`_regex_core.py`), Unicode tables and case folding, pattern-level public API | 8 | Opus |
| 3 | **Differential oracle harness first**, then VM core: literals, classes, quantifiers, groups, backrefs, anchors - plus the Match object, substitution and the iteration API (`Matches`/`Split`/`Replace`), which nothing else claims and without which no group test can even run | 11-16 | Opus |
| 4 | Advanced: lookaround (lookahead and lookbehind), conditional-with-lookaround, the `(*PRUNE)`/`(*SKIP)` verbs, recursion and group calls, partial matching, POSIX leftmost-longest. (Atomic, possessive, branch reset and named lists were listed here originally and turned out to be finished by Phases 2-3 - see the 2026-09-11 note below) | 10 | Opus |
| 5 | Fuzzy matching, `BESTMATCH`, `ENHANCEMATCH` | 5-8 | Opus |
| 6 | **Opens with the upstream sync and bug sweep (owner decision, 2026-09-12; three slices, gate for Phase 7)**, then oracle *hardening* (broader generators, all Unicode planes), gap tests, the native-AOT compatibility gate, and a Stryker.NET mutation-testing pass that now covers the engine as well as the API layer | 9-14 | Opus/Sonnet |
| 7 | Benchmarks and optimisation, every optimisation AOT-compatible. **Target stays `net10.0` (LTS) alone through 1.0 (owner, 2026-09-14): .NET 11's gains are JIT-side and reach a `net10.0` assembly on a .NET 11 host; S54 measures both runtimes from one build, and Phase 8 revisits a `net11.0` target only for a post-GA library API worth calling.** | 5-10 | Opus |
| 8 | Docs, packaging, NuGet, 1.0. **The user documentation is written from `docs/DIVERGENCES.md`**, the running record of every deliberate difference from upstream (Version 1 default, cost ranking, fixed inherited bugs, API shape, timeouts); every slice appends there when it diverges, so Phase 8 starts from a list, not from memory (owner, 2026-09-14). The ledger's upstream reports are filed in this phase, owner-approved. | 2-3 | Sonnet/Opus |
| 9 | Browser demo: Vue 3 page, the engine in a Web Worker, deployed to GitHub Pages | 2-3 | Opus |

Roughly 52-80 slice sessions in total, at plus or minus 50%. The generated status board makes the
real rate visible within the first two phases, which is when these numbers should be revised
against evidence rather than trusted.

**Phase 2's measured rate, recorded at its close (S13, 2026-08-31).** Phase 2 was re-estimated at
8 slices when it opened and landed in exactly 8 (S06-S13), so the *slice* estimate held. The
*driver session* count did not: `docs/plan/slice-log.jsonl` shows 8 slices took 11 driver sessions,
because S07 failed twice before it was parked and S10 failed once - about 35% more sessions than
slices. A completed slice cost 29M to 81M tokens, median around 48M, and a failed attempt costs
roughly the same as a successful one. **So read every phase estimate below as slices, and budget
1.35x that many driver sessions.** Phase 3's 11-16 slices is therefore 15-22 sessions. The estimate
itself is left alone: Phase 2 was parser work and Phase 3 is engine work, so its rate is not
evidence about Phase 3's, and revising a number on the strength of a different kind of work would
be worse than leaving it.

**Phase 3's measured rate, recorded at its close (S26, 2026-09-01). Thirteen slices, thirteen
sessions: 1.0, not Phase 2's 1.35.** `docs/plan/slice-log.jsonl` has no `failed` and no `parked`
entry between S14 and S26, where Phase 2 had three across eight slices. What moved was the cost per
slice rather than the number of attempts: the twelve logged slices S14-S25 came to 690M tokens, a
median of **60.5M** against Phase 2's median of 48M, spread from 10.5M (S14, the harness alone) to
91.5M (S22, case folding). So a Phase 3 slice cost about a quarter more than a Phase 2 slice and
landed first time where Phase 2's often did not, and the two effects roughly cancel in tokens per
phase. (S26's own figure is not in that median - a slice cannot log its own cost before it ends -
and the log has no entry at all for S08 or for the successful retries of S07 and S10, so Phase 2's
*total* is not recoverable from it. Only the medians and the session counts are.)

**Why the failures stopped is worth carrying forward, because it is not luck.** Every Phase 2
failure was a slice that ran out of road inside one session - S07 twice, S10 once. Phase 3's slices
were authored after that, each with its verification named in the slice file, and the driver gained
the rescue stash. **Do not read 1.0 as the rate for a phase whose slices are authored less
tightly**; read it as what a well-scoped slice costs. Budget Phase 4 at 1.0-1.35 and expect the
higher number if a slice file leaves its verification open.

**Phase 3 as authored (2026-08-31) was 13 slices, S14-S26** - inside the 11-16 band, and it landed
in exactly 13. The Phase 3 content line above
was corrected the same day: the original omitted the Match object, substitution and the iteration
API, which are necessarily Phase 3 work (Phase 4/5 do not claim them, `Match.Result` was
constrained to Phase 3 at the S13 close, and group/backref tests cannot run without `m.Groups`);
four of the 13 slices are that unlisted work, and the band still held.

**Does Phase 4's 8-12 hold, seen from the close of Phase 3? For the content named above, yes - but
four capability families have no phase at all, and assigning them is an owner decision rather than a
slice's (flagged S26, 2026-09-01).** The generated board's remaining tags divide cleanly. Phase 4's
row claims lookaround (61 tests), lookbehind (17), recursion (60), branch reset (21), named lists
(20), POSIX (8), possessive (8), partial matching (82) and the conditional-with-lookaround form
(15); Phase 5 claims the nine `fuzzy-*` tags (184). What nothing claims is **`inline-flags` (29
tests), `backtracking-verbs` (34), `version-flags` (11) and `comments` (4)** - 78 tests, more than
lookaround and lookbehind together. Three of the four are parser-adjacent rather than engine work,
so they were plausibly *assumed* into Phase 2 and are not in fact done. The options are to widen
Phase 4 to 10-14 slices, or to leave Phase 4 as authored and open a small phase for the flag-scoping
families. Either way the tags want assigning before Phase 4's slices are written, because a slice
file cannot verify a capability no phase has claimed.

**Phase 4 as authored (2026-09-11) is 7 slices, S27-S33, and the two owner decisions above are
closed by evidence rather than by choice.** At the checkpoint every remaining tag on the board was
probed - its `[Skip]` attributes removed, the suite run - and 92 skipped tests already passed:
`inline-flags`, `version-flags`, `comments`, `branch-reset`, `possessive` and `(*FAIL)` entirely,
and 17 of the 20 `named-lists` tests. Upstream has no `STRING_SET` opcode (`StringSet` lowers to
`BRANCH`, `_regex_core.py:4069`) and no possessive one (`PossessiveRepeat._compile` emits `ATOMIC`
plus the greedy repeat, `:3034`), so both families were done the moment S13, S19 and S20 landed,
and the skip prose describing "a parser that does not compile flags yet" was Phase 1's view of a
parser that has existed since S13. The four unclaimed families therefore collapse to `(*PRUNE)` and
`(*SKIP)`, 32 tests, which Phase 4 takes as S29; nothing needs a phase of its own. The un-skip is
commit `8ae8607`; the rule that every phase close probes the board this way is in DECISIONS and
in the phase-close slice. The content line above is corrected to what is actually left: lookaround 63 and lookbehind
17 (S27), conditionals 15 (S28), verbs 32 (S29), recursion 60 (S30), partial 82 (S31), POSIX 8
(S32), and the close (now S36). Seven slices against the 8-12 estimate, because four of the families
the estimate counted were already done. **Eight from 2026-09-12**: S33 was added at the checkpoint
to act on the divergence research (fix the port's one confirmed bug, pin the rest permanently),
and the phase close became S34; S34 was then added the same day, after S33's review found the default oracle wave was never reliably green - three seeds are the floor from here; and S35 was added after an independent specification-grounded verification reversed one verdict against the port and S34's 2000-row wave found a shared crash - so the close is S36. Budget it at 1.0-1.35 sessions per slice as the Phase 3
note says: 7-10 driver sessions. The `budget.json` question is also closed - it was raised to 20 a
day and 30 a week on 2026-08-31, and STATE.md's note was stale.

**Phase 4's measured rate, recorded at its close (S36, 2026-09-12). Ten slices, eleven sessions:
1.1, between Phase 2's 1.35 and Phase 3's 1.0.** `docs/plan/slice-log.jsonl` has one `failed` entry
across S27-S35 - S29's first attempt - and S36 is the eleventh session, so the estimate of 7-10
sessions was one short. The nine completed slices before this one came to 468M tokens, a **median of
50.9M**, spread from 17.2M (S28, conditionals) to 108.6M (S34, the three-seed sweep); the failed S29
attempt cost 54.9M, which is again about what a successful slice costs. So a Phase 4 slice was
slightly cheaper than a Phase 3 one (median 60.5M) and the attempt rate slightly worse, and the two
roughly cancel.

**Where the extra three slices came from is the part worth carrying into Phase 5, because it was not
scope creep.** Phase 4 was authored as seven and closed as ten, and none of the three additions was a
feature: S33 acted on the divergence research, S34 made three seeds the floor after S33's review
found the default wave had never been reliably green, and S35 fixed the two bugs S34's wider wave
exposed. All three came out of *verification getting stricter*, and each found a real defect. S36
then found two more the same way, by composing Phase 4's families into the `interactions` generator.
Budget Phase 5 accordingly: its 5-8 counts fuzzy features, and the oracle will add slices to it.

**Does Phase 5's 5-8 still look right? Yes, with one caveat worth stating now.** The nine `fuzzy-*`
tags are 185 tests and the machinery is enumerated - 33 functions in PORTMAP's fuzzy bucket, three
`do_*_fuzzy_match` entry points, 27 `Seam.For(Opcode.Fuzzy)` sites in `Matcher.cs` - so the count is
grounded rather than guessed. The caveat is that fuzzy matching has no oracle generator at all yet,
and every phase so far has found its worst bugs through one. Expect a generator slice *inside*
Phase 5 rather than after it, and read 5-8 as 6-10 sessions at Phase 4's measured 1.1.

**Phase 5 as authored (2026-09-12) is 7 slices, S37-S43, inside the 5-8 band.** S37 clears Phase
4's one unfinished item first (the composed `interactions` wave red at 6000 rows), because S43
widens that same generator with fuzzy and a generator that is red before the widening cannot tell a
new divergence from an old one. S38-S40 are plain fuzzy matching split by upstream mechanism rather
than by tag: the spine (state, constraints, `FUZZY`/`END_FUZZY`, one-character and zero-width items,
insertions, `do_simple_fuzzy_match`, counts and changes, and the `fuzzy` oracle generator - written
FIRST, as S36's handover asked), then strings, backreferences and the `*_REPEAT_ONE` loops, then the
`{...:test}` constraint. Tags cannot follow that split - `fuzzy-matching` (97 tests) needs all three
- so S38 and S39 deliver no tag by name and instead run S36's tag probe at their close, un-skipping
any tag that is entirely green, and S40 is where the seven plain tags must be delivered at the
latest. S41 (`ENHANCEMATCH`, 6 tests) and S42 (`BESTMATCH`, 18 tests) are the two entry points, each
adding its flag to the generator; S43 closes the phase as S36 did. Budget at Phase 4's measured 1.1
sessions per slice: 8 sessions, and the Phase 4 pattern says the wave will add one or two slices.

**One correction to that authoring, made at S40 and recorded as design spec amendment 18
(2026-09-13): the tags do not split the way the slices do.** S40 was written to deliver seven tags
outright. With the `{...:test}` constraint ported, 31 test cases still tagged `fuzzy-matching`,
`fuzzy-budget`, `fuzzy-counts` or `fuzzy-changes` failed, and every one of them failed at the
`ENHANCEMATCH` or `BESTMATCH` seam rather than on an assertion - their patterns carry `(?e)`,
`(?b)`, `(?be)` or `FuzzyRegexOptions.BestMatch`. They are now tagged for the capability they
actually wait on, and eight mixed fan-out methods were split so a plain row is not skipped to keep a
ranking-mode sibling company. **No slice moved and no scope changed**: S40 delivers the constraint,
the three per-error-kind tags and every plain row of the other four, and the 55 skips left on the
board are exactly S41's and S42's scope.

**Phase 5 is 8 slices from S40's close, not 7 - S40a - and it is the Phase 4 pattern repeating
exactly (design spec amendment 19, 2026-09-13).** S40's slice file asked for the default wave green
at 6000 rows a generator, which is ten times anything run before, and it cannot finish: upstream
never returns from `regex.search('.?x(?>a(*SKIP)z)', 'xzxa')` and the recorder has no per-row
timeout. At seed 7, where the wave does complete, four rows diverge in `partial`, `partial-sliced`
and `verbs` - **proven to be HEAD's**, by consuming the identical saved wave with HEAD's engine in a
worktree. S40's blind review added a fourth: a fuzzy section inside a lookbehind reports
`FuzzyChanges` that contradict its own `FuzzyCounts`, where plain `(?r)` does not. S40a takes all
four and the recorder timeout, and its exit gate is the 6000-row wave. As with S33, S34 and S35,
none of this was scope creep and none of it was a feature - it is verification getting stricter and
finding real defects, which is the thing the Phase 5 budget note above said to expect.

**S40a did not close in one session, and what it found changes the shape of the rest of Phase 5
(2026-09-13). This paragraph is the ROADMAP half; the design spec's matching amendment 20 was written by the
orchestrator on 2026-09-13** - the spec IS in this repository, at
`docs/superpowers/specs/2026-08-29-fuzzy-regex-port-design.md`, whatever the S40a session believed.

S40a's exit gate was "the default wave green at three seeds at 6000 rows". Run for the first time,
it gives **3 + 5 + 7 = 15 diverging rows**, not the four S40 recorded - S40 saw four because only
seed 7 ever completed, which is the same single-seed trap VERIFICATION rule 7a was written for, one
level up. Eleven of the fifteen have never been triaged, and **one is a crash rather than a wrong
answer**: row 93133 (`recursion`, `subf`) throws `ArgumentException: capture index out of range` out
of `Substitution.ExpandField` where upstream answers `sub 0`. Two independent blind reviews confirmed
none of the fifteen is caused by S40a's own changes.

Two of S40a's four items also turned out to rest on premises measurement overturned: the
`(*SKIP)`-in-an-atomic hang is upstream's and **already fixed upstream** (`b77694a`, released
2026.8.30), and the fuzzy change/count contradiction is upstream's too and **inherited**, with S38's
pinned rows proving this port reproduces it faithfully. Neither was a fix for S40a to make. What
S40a did fix is one real engine defect - the scanner carrying a slice a `(*SKIP)` moved into the next
match, ledger entry 5's own proposed fix - plus the recorder's per-row timeout.

So the honest read is that **Phase 5 needs at least three more slices than its eight**, and they are
verification work rather than features, exactly as this file's own Phase 5 budget note predicted
("its 5-8 counts fuzzy features, and the oracle will add slices to it"). Budget them at Phase 4's
measured 1.1 sessions per slice.

**S40a's second session (2026-09-13) triaged the fifteen, and the split is now three slices with
files rather than a proposal - S40b, S40c and S40d.** The fifteen are three mechanisms and not
eleven unknowns:

- **four rows are one port defect** (S40b): a `(*SKIP)` in the non-partial pass moves `slice_start`
  and the partial pass inherits it, so the search is not leftmost. Its fix turns S37's permanent
  pinned answer red - and that answer is the same defect, pinning a span this port's own matcher
  beats - so the fix and two "wrong answer on purpose" tests are one judgement;
- **seven rows are one question about this port** (S40c): upstream leaks a partial through a group
  call inside an opposite-direction lookaround, on both widths and in 2026.9.10 as well, so it is
  not issue 614; this port leaks it on an ASCII subject and not on an astral one, and every other
  optional tail it answers consistently. Session 1 read that as an astral defect; the twelve-case
  matrix in `tools/probes/upstream-call-partial-leak.py` says the ASCII row is the outlier;
- **two rows are the reversed `(*SKIP)` carried slice in shapes no tell reaches** (S40d), which is
  the reversed `anchoredScan` the recorder refuses, plus the gate itself.

**And the "crash" was not one.** Row 93133's `ArgumentException` is what upstream raises for the
same template the moment it has a match to expand it against - `{1[2]}` names a third capture of a
group that made one, and upstream's `IndexError` is the identical answer - so the whole divergence
is upstream losing the match, and the row is now classified under `group-call-loses-the-match`. The
port's format-field handling was measured against upstream's on every index form, negative included,
and agrees on all of them. Row 116766 is classified too, which is what widened
`overlapped-skip-extra-match-reversed` past overlapped scans: S40a's own engine fix stopped this port
reproducing the carried slice, so a plain reversed `finditer` now ends where upstream keeps going.
**Thirteen rows are left and every one has a slice with its name on it** - re-measured on the
committed code as 3 + 5 + 5, not inferred.

One number this re-plan corrects, because it was load-bearing and nobody had measured it: **the
6000-row three-seed gate takes about a minute**, 17 seconds to record 126,000 rows and 6 to consume
them. The forty minutes S40 lost was the hanging row the recorder now times out, not the volume, and
a draft of S40d that budgeted an hour a seed was corrected by its blind review.

One number the owner should revisit before Phase 3 runs unattended: `docs/plan/budget.json` sets
`maxSlicesPerDay` to 5 and `maxSlicesPerWeek` to 12, and its own note flags that the weekly cap now
binds after two and a half busy days. Changing it is an owner decision, not a slice's.

**Phase 5's measured rate, recorded at its close (S43, 2026-09-13). Eleven slices, fourteen
sessions: 1.27, the worst ratio since Phase 2's 1.35.** `docs/plan/slice-log.jsonl` has one `failed`
entry (S40a's first attempt) and two `checkpoint` ones (S42 and S43) across S37-S43 - three sessions
that did not close a slice, where Phase 4 had one and Phase 3 none. The ten completed slices before
this one came to **702M tokens, a median of 69.9M**, spread from 17.9M (S40b, one engine fix) to
127.7M (S42's second sitting, `BESTMATCH` ranking); the three non-closing sessions cost 83.0M, 80.6M
and 78.3M, which is again about what a slice costs. So a Phase 5 slice was a third dearer than a
Phase 4 one (median 50.9M) AND needed more attempts, and this time the two did not cancel: Phase 5
is the most expensive phase so far by a clear margin.

**Eleven slices against an estimate of 5-8, and the whole overrun is the oracle.** The seven
authored slices - S37-S43 - delivered the entire fuzzy feature set and landed as authored. The four
additions were S40a, S40b, S40c and S40d, every one of them a widened wave finding a defect, and
every one of them finding a real one. **This file predicted it before the phase began** ("its 5-8
counts fuzzy features, and the oracle will add slices to it"), so the estimate was wrong about what
it was counting rather than about the work. Carry that into Phase 6, whose own gate is oracle
hardening: an estimate that counts features cannot bound a phase whose gate is a generator, and 9-14
should be read as the floor.

**And read the seed count, not the row count, as the thing that finds defects.** S43's gate asks for
the default wave at three seeds and `fuzzy`/`interactions` at a fourth the phase had never used. The
three went green once the seven known rows were judged; the fourth produced three more divergences
immediately, on a generator pair that had already been swept at 6000 rows three times. One of them
gave ledger entry 8 a three-item reproduction and overturned that entry's own written claim that no
minimal form existed. That is VERIFICATION rule 7a one level up, and it is the cheapest instrument
this project has: a new seed costs about a minute.

Fuzzy matching - the reason this port exists - is usable at the end of phase 5, about two thirds
of the way through.

**Phase 3 opens with the differential oracle, not with the VM.** The oracle was originally phase 6
work, which left phases 3-5 - the entire engine - resting on the ported suite and the ratchet
alone. Those two cannot carry it: passing the original project's own tests is evidence of parity,
not proof of it, and the published figure for the closest analogue is that 72% of transpiled
functions were semantically equivalent *despite compiling and passing the existing tests*. The
infrastructure already exists from phase 0 (`tests/FuzzyRegex.OracleTests/`, `oracle.yml`,
Python `regex` installed), so this is a change of when the harness gets written, not of what has
to be built. Design spec amendment 10 has the evidence and the one caveat that does not apply
to us.

**Phase 2 opens with a compile-parity corpus, and carries the Unicode tables.** Both changes were
made at the Phase 1 checkpoint (2026-08-30). The parser's output is bytecode, and upstream's
bytecode is observable by intercepting `_regex.compile` while upstream's own suite runs: 1,534
patterns, 46 parse errors and 62 replacement templates, bit-exact. That is the oracle for a phase
in which nothing can match yet, so it is slice S06 and every later slice is verified against it.
The Unicode tables were parked in Phase 6, but the parser consults them for `\p{...}`, for every
case-insensitive pattern and for `\N{...}`, so they cannot wait; they are S09, transliterated
from upstream's generated C rather than regenerated. The reasoning and the measurements are in
`docs/plan/2026-08-30-phase2-decisions.md`. The estimate moved from 5-8 to 8 sessions.

**Native AOT compatibility is a requirement, enforced in two places (owner decision, 2026-08-31).**
Static analysis and a real published binary answer different questions, so both are in the plan and
neither replaces the other.

The static half is `<IsAotCompatible>true</IsAotCompatible>` on `src/FuzzyRegex`, set now rather
than at the end. On net8 and later that one property turns on the trim, AOT and single-file
analyzers, and because `Directory.Build.props` already sets `TreatWarningsAsErrors`, the first
slice that introduces a hazard fails its own build instead of leaving it for phase 8 to find.
It is cheap today: the library source contains no reflection at all - checked 2026-08-31, every
`System.Reflection` hit under `src/` was in generated `obj/` assembly-info or in a compiled binary,
and the only `System.Type` use is `typeof(...)` and `GetType()` inside `Equals`/`GetHashCode`,
which is statically known and trim-safe. Retrofitting the same property across a finished 30k-line
engine is the expensive version of this decision.

The dynamic half is a tiny consumer app published with `PublishAot=true` and run in CI, asserting
real matches. Static analysis is necessary and not sufficient - it cannot see a runtime-only
failure - so this is the test that actually proves the claim, and it belongs in phase 6 alongside
the rest of the hardening.

**S53 built it, and the static half turned out to have been enough for `src/` (2026-09-16).** The
gate is two commands, both in CI as the `native-aot` job on Windows and Linux: the whole suite
published natively (`tools/run-aot-tests.ps1`, 6,155 tests, 0 failed, 3 skipped) and a consumer
smoke app (`tools/run-aot-smoke.ps1`, 29 of 29). **`src/FuzzyRegex` was not modified at all** and
emits zero trim and zero AOT warnings. Every defect the gate found was in the test project, in
AwesomeAssertions or in the build - `BeEquivalentTo` cannot run natively, and neither can the S52b
object-graph walk, which reads BCL private fields and is therefore skipped in the native run only.
**Phase 7's baseline, from the consumer binary and the WARM run: 6,972,928 bytes byte-exact, and -
as ranges over seven observations on a busy machine, which is how they must be read - 28-54 ms to
`Main` and 30-57 ms to the first answer, so the library's own first compile and match costs 1-3 ms.
Re-measure the timings on a quiet machine before optimising against them.** The test
binary's 38.2 MB is not a consumer number - that publish roots three assemblies so its reflection
audits stay honest. The gate also exposed a defect that has nothing to do with AOT and constrains
every future slice: **a reflection scan that finds nothing makes a subset assertion pass**, which
three permanent S52b/convention tests were doing, and every such scan now carries a measured
non-vacuity floor.

**This is why it constrains phase 7 specifically.** The classic .NET regex optimisation is
`RegexOptions.Compiled`: emit IL at run time through `Reflection.Emit` and `DynamicMethod`. That is
fundamentally incompatible with native AOT, which has no JIT to emit into. Phase 7 has to know the
constraint before it plans, because the alternative is discovering the conflict after the fast path
is built. Waiting until phase 8 is worse still - a packaging phase is the worst moment to find a
design problem.

**It constrains phase 7; it does not disqualify a fast path.** Researched 2026-08-31 on the owner's
question, and recorded here so phase 7 does not plan as though the option does not exist. Nothing
below changes phase 7's scope: the design spec's optimisation levers already park source-generated
compiled patterns as **explicitly post-1.0** - "the bytecode design does not preclude them, and
building them now is speculative" - and that still stands. Whether this port ever gets a compiled
backend, and in which form, is an open decision for the owner.

- **A single assembly can carry both paths.** `RuntimeFeature.IsDynamicCodeSupported` is false
  under native AOT and true under the JIT, so one build takes the emit path where it exists and a
  fallback where it does not; methods reaching `Reflection.Emit` carry `[RequiresDynamicCode]`,
  which moves the warning to callers rather than hiding it. Microsoft's own library guidance
  prefers this to multi-targeting or separate packages, so `IsAotCompatible` stays honest and
  non-AOT users are not deprived of anything.
- **The real split is not AOT versus JIT, it is when the pattern is known.** A source generator
  (.NET's own `[GeneratedRegex]` route) is AOT-compatible and, per Microsoft's docs, gives "all
  the throughput performance benefits of `RegexOptions.Compiled` (more, in fact)" plus the startup
  win - but only for patterns that are compile-time literals. Patterns built at run time from
  config, user input or composition can be served only by runtime emit, and for a fuzzy-matching
  library that is plausibly a large share of real use.
- **There is an AOT-safe middle tier**: compiling the node graph into a chain of delegates at
  construction time removes dispatch overhead without `Reflection.Emit`, and serves
  runtime-constructed patterns. Reach for it before a second backend if measurement says dispatch
  is the bottleneck.
- **Trap: `Expression.Compile()` is not a workaround.** Under native AOT it silently falls back to
  the LINQ interpreter, the same silent degradation as `RegexOptions.Compiled` becoming a no-op
  where dynamic code is forbidden. Neither throws; both just get slower.
- **Most of the available speed is in the interpreter, and serves everyone.** Prefilters, better
  inner loops and opcode specialisation need no dynamic code and help AOT, JIT and
  runtime-constructed patterns alike. S19 measured the point: upstream's apparent instant answer on
  `(a|a)*b` was `locate_required_string` rejecting the subject before the engine ran, not a faster
  engine, and upstream is exponential on it too (23.3s at n=26). Any compiled backend stacks on top
  of that work, not instead of it - and either backend is phase-sized, since .NET maintains
  `RegexCompiler` and its source generator as near-1:1 twins for exactly that reason.

**Phase 7 ports upstream's start optimisations without importing their answers (owner rule,
2026-09-12).** `locate_required_string` and the `search_start_*` family change what upstream answers
on patterns with `(*SKIP)` and on some partial matches, and the research in
`docs/plan/2026-09-12-divergence-research.md` shows those changed answers are wrong: PCRE2 agrees
with this port on every case with its own optimiser on or off, and upstream's `..(*SKIP)xx` retries
below the position the verb committed past. The tests that pin the port's answers in
`Gaps/Engine/BacktrackingVerbTests.cs`, `PartialMatchingTests.cs` and `ReverseMatchingTests.cs` are
**permanent**; a Phase 7 slice that turns one red has ported a bug, and the fix is to make the
prefilter honour the slice the verb moved (the way upstream's own slow path does), not to invert the
test. The oracle rows for those shapes stay recorded against a prefilter-free upstream, or in the
strict manifest, until upstream itself is fixed.

**Techniques from other engines (2026-09-18).** The owner asked whether the Rust `fuzzy-regex-rs`
library has anything to borrow. Two prefilter refinements were folded into S60 (a rarity gate on the
skip character; a large-list fast path for `\L<name>`); its automaton design and fuzzy algebra are
out of bounds because this port returns mrab-regex's answers exactly. Full table in
`docs/plan/2026-09-18-fuzzy-regex-rs-techniques.md`. The two bets the wider research sweep of
2026-09-18 left for the owner are now planned as experiments rather than left on a list (spec
amendment 28, owner decision 2026-09-19): **S62b** rewrites loops nothing can backtrack into as
atomic behind PCRE2's guard list, and **S62c** is a one-sitting spike on selective memoisation of
failed positions, both bounded by the oracle at three seeds and both allowed to end in "reverted,
recorded".

**S60 splits into S60 and S60b (2026-09-20, spec amendment 30).** S60 was authored over seventeen
scope items and three sittings measured what that is: it closes on the required-string half - the
forward `locate_required_string` arm, `string_search`/`simple_string_search`, the per-pattern
needle, the `(*SKIP)` constraint implemented rather than asserted, 19 gap tests and three judged
oracle rows - and its items 2, 3, 6, 8-14, 16 and 17 move to
`docs/plan/slices/S60b-search-start-and-the-researched-prefilters.md` with **S60's item numbers
kept**, so the `ponytail:` comments and `OPTIMISATION-NOTES.md` rows already in the tree still
resolve. Item 2 alone is upstream's `search_start` (`_regex.c:8385`), the dispatcher its
`do_search_start` flag (`:588`) turns on, over about thirty `search_start_*` functions; items 8-14,
16 and 17 are nine researched optimisations, each wanting its own measurement and each allowed to
end in "measured, not worth it, recorded". Phase 7 is nine slices where it was eight (six as
drafted, plus S62b and S62c from amendment 28); the 5-10 session band is unchanged, because it is
the same work counted differently.

**Phase 7 is six slices, S58-S63 (drafted 2026-09-16 from the owner's notes of 2026-09-14, the
research in `docs/plan/phase7-research/` and the ground rules in DECISIONS).** **S58** is
measurement only and changes no engine code: this machine's noise floor from two runs of an
unchanged build, pyperf installed and probed, the EventPipe and dotTrace/Rider MCP routes proven
end to end, the optimise checklist written from the research (in
`docs/plan/phase7-research/optimise-skill-pending.md`, because S58's session was refused permission
to write under `.claude/`; it becomes `.claude/skills/optimise/SKILL.md` when the owner moves it),
`tools/compare-benchmarks.ps1` extended with an allocation ratio and the floor, the span-copy and
lazy-walk costs measured and written up for the owner, and `docs/plan/SYNC-DIVERGENCE.md` created
with a script that pairs a `sync-divergence:` marker to a ledger row. **S59** adds the bounded MRU
pattern cache behind `FuzzyRegex.CacheSize` (default 15, as `Regex`), keyed on the raw flags,
AOT-safe, tested under S52b's concurrency contract. **S60** ports the `locate_required_string` and
`search_start` family from the six waiting arms, on `SearchValues<char>` and vectorised `IndexOf`.
**S61** implements whichever span-threading and lazy-walk shapes the owner signed off, reuses
`MatchState` buffers across a walk, and turns the allocation ratio into a gate. **S62** takes the
inner loop - `Node.Values` to an array, one inlined predicate at a time behind its own number,
`SkipLocalsInit` and layout where they measure - and measures dispatch cost without changing it.
**S63** runs the v1.0 gate per the `benchmark` skill, refreshes both baselines on a quiet machine,
commits the results, and triages every failure into a follow-up slice or an owner decision. The
ground rules they all obey: measure first and decide the span and lazy-walk questions on numbers,
not guesses; structural divergence is allowed only with a `SYNC-DIVERGENCE.md` row enforced by
checklist and script, its gain threshold deferred until S62 has examples; AOT compatibility
outranks AOT-specific speed, so benchmarking is on the JIT and the AOT publish is green every
slice; the permanent pins and the oracle at three seeds bound every one of them, and an
optimisation that changes an answer has ported an upstream bug.

**Phase 6 opens with the upstream sync and the bug sweep, and Phase 7 does not start until both are
closed (owner decision, 2026-09-12).** Measured that day: the pin is 2026.8.12 and upstream had moved
21 commits and five releases to 2026.9.10, which is also PyPI's newest and is byte-for-byte upstream's
head - 188 lines of `_regex.c`, three of the parser, no new tests. The substance was the four
memory-safety fixes (issues 611-614, all already on the inherited-bug list; 614 is the
group-call-in-lookbehind divergence S30 found, where this port was right) and four Python-API
error-propagation PRs (615-618) that have nothing to port. Three slices, in this order:

1. **Sync upstream** to the newest *release* with the `sync-upstream` skill: bump the submodule, walk
   the changelog delta one entry at a time, port each fix test-first, record every entry in PORTMAP as
   ported or not-applicable-with-reason. Rebuild the local oracle to the same release and confirm
   `src/` at the tag is byte-identical to the PyPI wheel, as amendment 7 requires. Re-run every wave;
   `ExpectedDivergences` must fire on each pinned divergence upstream has since fixed, which is the
   proof the list is strict. **Releases, never head, for the pin and the oracle**: a head-only fix has
   no PyPI wheel to compare against, and a local C build is a choice this project made against. But
   diff release..head at every sync, and if head carries an unreleased engine or parser fix, port it
   too, test-first, recorded in PORTMAP as "ahead of release, from commit X", with the fix's own
   issue and test case as its ground truth. On 2026-09-12 head and release were the same commit.
2. **Issue sweep**, re-triaged from the live tracker rather than the 2026-08-31 snapshot: every open
   issue reproduced here or dismissed with a written reason; every reproduced bug fixed here and
   recorded in `docs/plan/upstream-reports/LEDGER.md`. **Nothing is filed upstream until absolutely
   everything else in the plan is done** (owner decision, 2026-09-12); filing is the last step of
   Phase 8, each entry re-verified against the then-current release and approved by the owner first.
3. **Our own findings**: every divergence the research documents and every gap test marked as an
   upstream bug is judged to the standard in spec amendment 16 - documented definition, a real run
   of a second engine, a survey of comparable libraries where upstream defines nothing, release
   history, a blind review and an independent verifier - and lands in one of its four outcomes:
   the port is wrong, so it is fixed; upstream is wrong, so the port's answer is pinned and
   ledgered; both are wrong, so **the port is fixed here even though the bug is inherited**, and
   ledgered; or the evidence is strong but not conclusive, so it is ledgered and the owner decides.
   The list of known bugs in this port - ours or inherited - is empty before Phase 7 touches the
   engine. **Ledger entry 7 was the first item on this list** (S35, 2026-09-12), recorded then as
   "`İ` (U+0130) never reaches the full case fold because upstream's expansion inventory is not
   lower-cased where the text it is sought in is". S45 settled it against `CaseFolding.txt` on
   2026-09-14 and **that statement of the cause was wrong**: upstream's table builder merges
   CaseFolding.txt's Turkic-only `T` rows into both default tables, which the file itself says to
   exclude by default, so upstream applies a Turkish locale rule with no locale asked for. The
   lost expansion of `U+0130` is one consequence and `(?i)I` matching `ı` is the other. Fixed in
   S45 by substituting the default case data for the four codepoints; the ledger entry was
   rewritten cause and fix, and this port now diverges from upstream on four of `test_turkic`'s
   sixteen cells, deliberately and in agreement with PCRE2, Perl and .NET.

The estimate moves from 7-12 to 9-14 for the two slices this adds beyond the sweep already planned.

**Phase 6 as authored (2026-09-13) is 14 slices, S44-S57, against the 9-14 band read as amendment 21
says: expect the gate, not the features, to set the number.** In order: S44 the upstream sync to
2026.9.10 (submodule, wheel byte-identity, changelog delta, every `Example` re-recorded); S45-S48 the
inherited-bug fixes from the ledger, grouped by mechanism rather than by entry - the dotted-I fold
(entry 7), the `BESTMATCH` family (12, 13, 9's port half), the fuzzy consistency and resource pair
(11, 14), and the verb/partial doors (5's remainder plus an inventory of every entry's final state);
S49-S50 the issue sweep in two halves, live re-triage with a reproduction or written dismissal per
issue, then the fixes; S51 per-call timeouts and `CancellationToken` (owner request, amendment 22),
placed before optimisation because the poll is on the hot path; S52 oracle hardening scoped on seeds
first (a sweep tool, all planes, long subjects, timeout rows); S53 the AOT dynamic gate; S54 benchmark
baselines and the optimiser-trap pins; S55-S56 mutation testing as tooling-and-calibration then
survivors, with the engine runs done detached overnight by the orchestrator between the two, in
chunks sized from the calibration (owner decision 2026-09-13: many small runs, long wall time is
acceptable where it improves the outcome); S57 the coverage backstop and the close. Three slices
need the orchestrator to act before launch (S44 installs the wheel, S49 snapshots the tracker, S55
installs Stryker and S56 needs the overnight queue run) because the driver cannot install or post;
each file says so at the top. Budget at Phase 5's measured 1.27 sessions a slice: about 18 sessions.

**Phase 6 gained three slices on 2026-09-14, all owner-decided.** S47b and S47c after an independent
audit of S44-S46 (two oracle pins wider than their evidence, evidence only in scratch, notes claiming
unmeasured numbers; and ledger entry 13's mechanism, since traced to the line in a debug build of
upstream). **S52b thread safety** (spec amendment 23): the design spec promises a compiled pattern
is immutable and shareable across threads, as upstream and .NET `Regex` do, and nothing in the
repository proved it - no test used a second thread. S52b adds a structural immutability test over
the whole pattern graph, a static-state audit, a debug `ArrayPool` wrapper that catches double
returns, a deterministic stress test under real parallelism, and the documented contract. **Its tests
are PERMANENT and constrain Phase 7 directly**: optimisation is where caches appear, and a Phase 7
slice that turns one of them red has introduced shared mutable state and removes it rather than
widening an allowlist. Placed after S52 so the stress test can draw its subjects from the hardened
waves. Estimate 10-15 becomes 13-18.

**S50b makes Version 1 the default (owner decision 2026-09-14, spec amendment 24).** Measured first:
on 2026.9.10 the only live V0/V1 differences are nested sets with set operations and full
case-folding; zero-width handling and inline-flag scoping are already identical. Both differences
are the better behaviour and the library's reason to exist, and the port has no `re` users to
protect, so the compile-time default flips, the oracle states upstream's default explicitly per
row, the ported suite pins `Version0` through one helper, and the one loud edge (`[` inside a set)
gets an error that names `Version0`. Before S51 so every later slice tests and measures the shipped
default. Estimate 13-18 becomes 14-19.

**S56b adds a compile budget (owner, 2026-09-18).** A probe of `((a{1000}){1000}){1000}` crashed the
owner's machine at compile time; the cause is inherited repeat unrolling (investigation above). S56b
puts a configurable node budget at the compiler's single node-creation point, throwing a parse
exception before any matching; default about a million nodes, exact parity below it. Runs on main
after S55. Estimate 15-20 becomes 16-21.

**S52c adds metamorphic invariants (owner, 2026-09-15, spec amendment 25).** The oracle sees
disagreement, not correctness: a bug the port inherited line for line agrees with upstream and
hides. Every inherited bug so far was found by upstream contradicting itself, by hand. S52c makes
the recorder check a fixed list of language-level invariants on upstream's own answers for every
wave row (search versus anchored match, fullmatch versus a whole-span match, counts versus
changes, budget monotonicity, group spans inside the match, split re-joining the subject) and on
the port's, flagging violations for ledger triage. The skill now requires gap-test expected values
to carry provenance from a real upstream run or a DIVERGENCES row, and S57 audits the ones written
before the rule. Estimate 14-19 becomes 15-20.

**S53b adds API completeness before optimisation (owner decision 2026-09-16, after the Fable review
in `docs/plan/2026-09-16-unported-members-review.md`).** Seventeen of the twenty-one "no port
equivalent" rows stand; four decisions change, all additive and all cheaper before S54 baselines
the shapes users will call: lazy `EnumerateMatches`/`EnumerateSplits` over the existing
`NextMatch` path (the built-in `Regex` is lazy and .NET 7/9 added the same entry points), `Ascii`/
`Unicode`/`Word` on `FuzzyRegexOptions` with `Options` no longer stripping them (a latent cache-key
bug for Phase 7), `GroupCollection : IReadOnlyDictionary<string, Group>` as .NET 5+'s is, and
`beginning`/`length` on `Replace`. The pattern cache stays a Phase 7 item, recorded as PLANNED with
`CacheSize` as the equivalent. `docs/plan/OPTIMISATION-NOTES.md` now indexes every deferred
optimisation so Phase 7 does not rediscover them. S53 publishes the whole TUnit suite as Native AOT
before building the sample. Estimate 16-21 becomes 17-22.

**S52d fixes ledger 24 on the owner's ruling (2026-09-15, Option B of
`docs/plan/upstream-reports/ledger-24-briefing.md`).** A reversed match with `partial=True` runs
out of text at the slice start and reports a partial there; `^`, `\A`, `` and lookbehind keep
Python `re`'s whole-string view of `pos`. Upstream holds both rules and picks one by optimisation
path; the port inherited both. Amendment 16 outcome (c): fixed here at the nine partial run-out
sites through one shared helper, proven over the wave with S52c's invariant checker, pinned
narrowly, not filed. After S52c. Estimate 15-20 becomes 16-21.

**And S48's own inventory added a fifth: S48b (2026-09-14, spec amendment 25).** S45-S48 were the
four slices meant to empty the known-bug list. S48 inventoried all fifteen ledger entries, which is
what its scope asked for, and thirteen are closed: entries 1-4, 6 and 8 are upstream-only with this
port pinned right, 7, 12 and 14 were fixed here, 9's crash half and 13's port half were already
closed, 10 is closed by the sync, 15 is a regression this port never had, and entry 5's sixth door
is fixed by S48 itself. What is left is three items that are **one mechanism seen three ways** -
the fuzzy counts are saved and restored as a block while the change list is unwound one item at a
time - so ledger 11's mechanisms C and D and ledger 9's remaining port-side count bug become S48b,
placed before the issue sweep. Ledger entry 11 had already written "That is a slice of its own".

**Two things S48 measured that Phase 6 should carry forward.** First, the sixth door was found by
READING upstream's C while inventorying, not by a wave, and no generator can see it: the negative
control that restores the fault moves nothing at all across 24,000 `interactions` rows at four
seeds, while the same fault changes upstream's answer on 1,861 of 11,340 shapes in a hand-built
alphabet. **That is a measured gap and it belongs to S52**: the generators compose `(?b)` with
`(*SKIP)` and never in a shape where the walk truncation decides anything. Second, three of the
"five broken control sites" were S31-A, S31-B and S31-C, broken because S40b's own fix moved the
text they mutate; S48 repaired all three and they now fire 10-68 divergences of 600. A control that
will not resolve is not a control, and the repair cost minutes.

**S57 splits: the backstop closes as S57, the exit gate's twenty rows become S57b (2026-09-20, spec
amendment 32).** S57's third sitting ran the gate as the phase actually defines it - `run-oracle.ps1
-Count 6000` at three seeds, 126,080 rows a seed, not the 300-row default wave that every recent
slice's "oracle GREEN" line means - and it is RED: diverge 3 at seed 7, 3 at 4242 and 14 at
20260920, twenty distinct rows. Not a regression from S60: the pre-S60 tree (`14aad0a~1`) gives 3,
3 and 15 on the same command, so S60 is one row better, and the recorder itself has changed since
S52's close (S52c's metamorphic invariants, S53b), so "S52 closed green at 6000" is not a regression
window - dating the rows is a scripted bisect and belongs with the fixes. Twenty rows judged to this
project's standard is not a tail-end task (S52 spent eighteen sittings on thirty-seven), so they
move to `docs/plan/slices/S57b-the-6000-row-gate-rows.md`, and Phase 6's bookkeeping, measured rate
and Phase 7 handover stay in S57 with them, because all three write that the phase is closed. **The
phase does not close until the 6000-row gate is green at three seeds.** Estimate 17-22 becomes
18-23.

**Phase 6 has an exit gate, in this order.** "Sweep for coverage gaps" without criteria produces a
number nobody acts on, so the phase closes against these, biggest signal first.

1. **Skips remaining in the ported suite.** This is the real coverage metric for a port, and it
   already exists: 1,880 skipped of 5,492 as of 2026-08-31, about 34%, each carrying a
   machine-readable `needs:` reason that `tools/PortTools.psm1` aggregates into `docs/STATUS.md`.
   That is an enumerated, per-capability gap list of upstream behaviour this port does not yet
   have, which is more than any coverage tool can tell us.
2. **Oracle waves across every generator, with zero divergences.** Real ground truth, and unlike
   coverage it finds behaviour that was never tested at all - S16's leading-anchor bug was found
   by a wave and by no ported test.
3. **Mutation testing**, already parked for phase 6 below. It is the only instrument that answers
   "would a regression actually fail a test?". Originally scoped to the public API layer and the
   parse-error paths - the two places the oracle cannot reach - and **widened on 2026-08-31 to
   include the engine** (design spec amendment 14, DECISIONS 2026-08-31). What confined it was
   machine time, not doubt about its value, and the owner has since allowed it to run overnight
   with no contention. The engine is what phase 7 rewrites, so it is the part whose coverage most
   needs measuring first.
4. **Line coverage last, and only as a backstop** - to find a file or a branch with no test at all.
   Never as a percentage target.

Phase 6 also has to leave phase 7 something to regress against, because optimisation is where
silent behaviour change is likeliest. Two things get pinned before phase 7 starts: the benchmark
baselines, measured and committed per `.claude/skills/benchmark/SKILL.md` (the skill defines them;
nothing is committed under `bench/baselines/` yet, so this is work, not a tick), and the edge cases
an optimiser is tempted to special-case - zero-width and empty matches, anchors, `MatchTimeout`,
large inputs, and pathological backtracking.

**Phase 6 also sweeps upstream's open issues, because the oracle cannot see them (2026-08-31).**
Triage every open issue on `mrabarnett/mrab-regex`; drop the questions, the feature requests and
the Python-specific ones; for what is left, reproduce it as a test, fix it here, and report it
there. Sized at 3-5 sessions on top of the phase's existing 3-5, which is why the row now reads
6-10.

The reason this is a separate instrument, and not more oracle waves, is that **the oracle is blind
to an inherited bug by construction.** It compares this port against upstream, so a bug we
reproduce faithfully produces agreement - the thing the oracle reports as success. A divergence
appears only where we accidentally failed to reproduce one. Widening the generators cannot help,
because the blindness is in what the comparison means rather than in how much of it we run. It also
has to come before phase 7: optimising on top of behaviour already known to be wrong means
measuring the wrong answer and then locking it in.

The size is measured, not guessed. All 79 open issues were triaged with `gh` on 2026-08-31: 40 are
not bugs, 24 are Python-specific and cannot exist in a C# port, 12 are real engine bugs this port
would inherit, and 3 cannot be judged without trying to reproduce them. The 12 fall in four groups
- four 2026 memory-safety bugs from a fuzzing campaign (611-614), four fuzzy and BESTMATCH bugs
(470, 563, 564, 596), two resource blowups (551, 554), and two singletons (367 on partial matching,
425 on branch reset). What a reproduce-fix-report cycle costs per issue is *not* measured, so 3-5
is an estimate whose only basis is that count.

Two things to know before starting. First, the memory-safety four are heap-buffer-overflow reads
and writes in C, which cannot happen in a memory-safe language - the same defect arrives here as an
`IndexOutOfRangeException` or as a quietly wrong answer instead. The underlying logic errors are
inherited regardless: boolean precedence desyncing the group count, a stale required-string cache,
a stale backtrack limit, and `build_GROUP()` dropping match direction for a group called from a
lookbehind. Several are compiler-side, in code S15 and S16 have already ported, so one may surface
before phase 6 arrives; that is a thing to recognise if it happens, not a plan.

Second, fixing a bug upstream still has creates a permanent oracle divergence, so the sweep brings
an intentional-divergence allowlist with it: a version-controlled, machine-readable manifest the
oracle consults, which reclassifies a listed divergence as expected rather than skipping it
silently. Row identity, our result, upstream's result, a reason carrying the upstream issue number
and our DECISIONS entry, and the upstream commit it was recorded against. **It has to be strict:**
if a listed divergence stops diverging, because upstream fixed it or because our fix regressed, the
run fails - the way pytest's `xfail_strict` turns an unexpected pass into a failure, and unlike
Chromium's TestExpectations, which lacks that and is documented as accumulating stale entries. This
is the same shape as `tests/parity-baseline.json` and `tools/check-ratchet.ps1`, which is already a
pinned manifest that fails both on a regression and on a baselined test quietly vanishing, so the
allowlist follows that convention instead of inventing a second one.

Reporting upstream has a template and a rule. The maintainer merges external pull requests (7 of
the last 20 closed, February to May 2026), there is no CLA and no CONTRIBUTING.md, and crisp
technical reports are acted on within hours - #607 and #608 were both fixed the same evening -
while design-trade-off arguments stall. The four traits those reports shared: a minimal, directly
runnable reproduction with the version or commit pinned; the faulting function or mechanism named,
not just the symptom; the bug explicitly distinguished from a similar one already fixed; and a
concrete fix proposed. The rule: nothing is filed upstream until the owner has approved the drafted
text, and `gh` stays out of the driver's allowlist in `tools/run-slices.ps1`, so an unattended
slice session cannot post - it stalls instead, which is the right failure. Licence was checked the
same day and needs nothing: `NOTICE` already declares the derivative work, attributes mrab-regex at
the pinned commit, carries upstream's CNRI/Secret Labs statement and credits the UCD, which is
Apache 2.0 section 4(b) and 4(c) satisfied.

**Phase 9 is a browser demo, and it comes after 1.0 (2026-08-31).** It depends on a public API that
has stopped moving and on the phase 6 trim and AOT gate, and demo polish must not hold up the
release, so it sits at the end. The alternative is yours to take: if the README has to carry a
working "try it in your browser" link *at* 1.0, the demo moves into phase 8 and phase 8's estimate
goes up accordingly. The 2-3 sessions in the table are a guess with no measured basis - nothing
comparable has been built here.

**The engine runs in a Web Worker, and that is the whole safety design.** A public demo invites
strangers to type pathological patterns. Regex matching is unbounded in the worst case, and this is
a fuzzy engine, so approximate matching is combinatorially worse than the exact case. `MatchTimeout`
only bounds a freeze if the engine polls the deadline in its inner loop, which means one bug at one
missed check point turns a bounded stutter into a frozen tab that the user cannot close cleanly. The
worker makes "the page never freezes" a property of the browser's scheduler rather than of the
engine's diligence: a runaway is killed with `worker.terminate()`, and a warm spare worker is
spawned in advance so the respawn is not felt. `MatchTimeout` stays in the demo as the fast
common-case exit and as defence in depth, never as the only safety net.

**The shape.** The main thread is a Vue 3 + TypeScript UI built with Vite, in strict mode, with a
CSS framework allowed (design spec amendment 27, owner decision 2026-09-18; until then a vendored
ESM build with no build step). No CDN dependency, ever: everything pinned and installed with `npm ci`. The worker hosts its own .NET WebAssembly runtime (a `wasmbrowser` project) and
exposes exactly one `[JSExport]` method: pattern, flags and subject in as strings, a JSON result
out. SolidJS was considered and rejected - the UI is three inputs, a result pane and an examples
list, so fine-grained reactivity would optimise the part that was never the bottleneck, and the
owner already knows Vue. Framework choice is orthogonal to responsiveness here, because all the
expensive work is behind `postMessage`. Blazor was the original recommendation and was dropped once
the UI no longer had to be .NET: it brings the Components assemblies and boot machinery for no
benefit at this size, and Microsoft's own Blazor-on-a-worker sample needs more files than the plain
route, not fewer.

**No cross-origin isolation is needed, and that distinction is worth recording.** The worker boots a
second, independent runtime and talks to the page over `postMessage`; COOP and COEP headers are
required only by `WasmEnableThreads`, which is an unrelated mechanism this design does not use.
GitHub Pages cannot set custom response headers at all, so without this note someone will later read
"WebAssembly in a worker" as "impossible on Pages" and redesign around a constraint that was never
there.

**Known risks, stated at the precision the evidence supports.** The `wasmbrowser` and `wasmconsole`
project templates are documented as experimental - "the developer workflow for the templates is
evolving" - but the `[JSImport]`/`[JSExport]` interop the worker actually depends on has been
supported since .NET 8. So expect template ergonomics to shift and the API surface to hold. Respawn
latency after `terminate()` has no published figure and none has been measured here; the warm spare
is the documented mitigation, and the compiled module is browser-cached, so a respawn hits cache
rather than re-downloading. `[JSExport]` requires `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in
the demo project, which fails at build time rather than at run time, so it cannot reach a user.

**Deployment mechanics, all of which have bitten someone before.** GitHub Actions publishes to
Pages. A `.nojekyll` file is mandatory: without it Jekyll drops the underscore-prefixed `_framework`
folder and the app serves a 404 for its own runtime. The base href is rewritten to the repository
subpath. `*.js binary` goes in `.gitattributes`, because this repository sets `* text=auto eol=lf`
and any line-ending conversion of the framework JavaScript breaks its SRI integrity check and the
app will not boot. `InvariantGlobalization` is already true repo-wide in `Directory.Build.props`, so
the demo inherits it.

**Scope, deliberately small.** Three inputs; a result pane showing highlighted spans and a group
table of index, length and captures; a sidebar of about eight worked examples loaded from a JSON
array, which *is* the guided tour; and the three inputs encoded in the URL fragment so a case can be
shared in a bug report. Subject length and the number of displayed matches are capped. Explicitly
out: accounts, persistence, analytics, anything server-side.

**It also earns its keep as a test.** Publishing the library into a live WebAssembly host is a real
trimming and AOT proof under a different runtime from the CI gate, which is why phase 9 is tied to
the phase 6 gate rather than being pure marketing.

Precedent for both halves: PeachPDF keeps its demo as a project in the same repository and deploys
it to Pages alongside the docs (it uses Blazor, which this does not), and `dotnet/blazor-samples`
ships `DotNetOnWebWorkersReact`, available for .NET 10 and later, as a non-Blazor host driving a
worker-hosted runtime.

**Phase 9 sliced, 2026-09-16.** The owner confirmed the design above unchanged - Vue 3 on the main
thread with the engine in a Web Worker, over the Blazor alternative he had raised - and confirmed
the staging: v1 is this section's deliberately small page, v2 adds an editable sample per major
feature with contextual help, v3 is polish, and 1.0 need not wait for all of it. `wasm-tools` and
`wasm-experimental` were installed (10.0.112) the same day, so nothing blocks a start. Three slices,
each verified against current Microsoft Learn documentation on the day it was written:
**S70** builds `demo/FuzzyRegex.Demo.Wasm` (a `wasmbrowser` project, one `[JSExport]` taking
pattern, flags and subject as strings and returning JSON), a plain HTML harness that boots it in a
worker and proves the `postMessage` round trip and `terminate()` plus respawn, and
`tools/run-wasm-smoke.ps1` to publish and check the artefact set.
**S71** is the v1 page itself - three inputs, highlighted spans, a group table, eight worked
examples, the inputs in the URL fragment, the caps and the warm spare - plus the GitHub Actions job
that deploys it to Pages with `.nojekyll`, the base href and `*.js binary`, and the README link.
**S72** is v2: one editable sample per major feature, help panels generated at build time from
`docs/COMPARISON.md` so there is one source, and UI polish with regex101 as the named reference.
Two documented facts sharpen this section rather than contradict it. `[JSExport]`/`[JSImport]` is
"currently limited to the main thread even if multi-threading support is enabled"
(`dotnet/runtime/src/mono/wasm/features.md`), so `WasmEnableThreads` and its COOP/COEP requirement
are not merely unneeded here, they would break the interop; and GitHub Pages "doesn't natively
support using Brotli-compressed resources", so the bytes a visitor waits for are the uncompressed
ones and the size baseline has to be recorded twice, on disk and on the wire.

**Phase 9 moves ahead of 1.0 (owner decision, 2026-09-18; spec amendment 26).** The owner wants to
experiment with the library through the demo before launching it publicly, so v1 (S71) ships before
the release and the README carries the link at 1.0. Phases 8 and 9 start now, in parallel with the
Phase 6 mutation queue and Phase 7, each in its own worktree with `-Phase`. Running the demo
locally and deploying it to GitHub Pages are documented step by step in `demo/README.md`, written
by S71.

**Phase 9 reopens for one slice, S73 (owner decision, 2026-09-19; spec amendment 29).** S72 closed
the phase on features. The owner's first look at the live page judged it as a product and found
seven faults: the match results sit below the fold on a laptop ("not intuitive at all"); the copy
is "horrible AI-speak"; the design is flat; the layout is a web page rather than an application;
there is no way to take the current case away as C#; the GitHub link is only in the footer; and the
match numbers look like links while appearing to do nothing. **S73** answers all seven in one
slice: a fixed shell with its own scroll regions so the answer is above the fold at 1366x768 and
1440x900, every user-facing string rewritten against a banned-phrase list that a test enforces, one
visual identity with the fuzzy edit types given meaning in colour, a "C# for this case" panel that
prints the API `DemoEngine` itself calls and copies to the clipboard, the GitHub link in the header,
and two-way linking between a highlight and its match row. Layout and copy decisions are cited to
NN/g, Material's window size classes, GOV.UK's style guide and Wikipedia's signs-of-AI-writing list,
named in the slice. Nothing in the engine, the worker, the caps or the single-source help changes,
and the accessibility rules from S71 and S72 are met again rather than traded away. The draft is
`docs/plan/slices/S73-demo-as-a-product.md`; the owner reviews it before any sitting runs, and the
phase closes again when it lands.

**S74 follows S73 in the reopened phase (owner decision 2026-09-20; spec amendment 31):** the
flags text field becomes a collapsible checkbox panel with a summary row, radio groups for the
exclusive pairs and per-flag help generated from the enum comments. Spec:
`docs/plan/slices/S74-flags-control.md`. Design agreed with the owner in conversation; no review
round before the sitting.

**S75 follows S74 (owner decisions 2026-09-20; spec amendment 33):** the edit markers keep their
letters, drawn in a clear marker row, and gain hover notes, a legend and an alignment view; every input heading gets a help
note linking to the docs; the fuzzy test set gets a worked example; the snippet's identifiers are
pinned by `nameof`; and the copy linter runs over `README.md` and `docs/*.md`. Spec:
`docs/plan/slices/S75-edit-markers-help-notes-and-the-prose-linter.md`. The owner reviews it before
the sitting.

**S76 and S77 follow S75 (owner findings 2026-09-20 and 2026-09-21; spec amendment 35).** **S76**
gave the four samples whose Help tab opened empty a feature key each, and wrote the three sections
of `docs/COMPARISON.md` that two of them needed: how set operations, `\p{...}` properties and
`IgnoreCase` differ from `System.Text.RegularExpressions`. Landed 2026-09-21;
`docs/plan/slices/done/S76-help-for-the-last-four-samples.md` has the closing notes. **S77** is the
owner's two follow-ups: the help panels' C# examples are printed in one colour, and a heading note's
link opens its help entry with nothing on screen to say it has. Landed 2026-09-21, reusing the
tokenizer the snippet box already had (0.25 kB gzipped, all in) and landing the reader on the
section with focus, a scroll and one fade:
`docs/plan/slices/done/S77-help-code-colour-and-the-jump-that-lands.md`.

**Phase 8 sliced, 2026-09-18**, from `2026-09-16-llm-friendly-docs-research.md`'s "Do" list and the
owner's 2026-09-14 note that the user documentation is written from `DIVERGENCES.md`. Six slices,
Sonnet unless stated: **S64** README as the complete getting-started and nupkg readme, plus
`IncludeSymbols`/snupkg; **S65** `COMPARISON.md` checked row by row against `DIVERGENCES.md`'s
SHIPPED rows and the two convention tests (every public member documented, every SHIPPED row
named); **S66** pack, validate and a dry-run release checklist, nothing published; **S67** the
registries (Context7, DeepWiki) as owner-performed steps with prepared files; **S68** the
`<remarks>` divergence notes on every affected public member, **after Phase 7** because it edits
`src/`; **S69** (Opus) the upstream-report filing under the ledger's owner-approval rule and the 1.0
release, **after the Phase 6 exit gate and the Phase 7 performance gate**.

## Candidates parked for later
- **Counted repeats without unrolling (post-1.0).** The compiler unrolls the minimum count of every
  counted repeat (inherited from upstream, 2018.11.22, to keep the position-keyed repeat guard sound),
  so memory is linear in the product of nested counts; S56b bounds it with a node budget. The
  structural fix emits counted repeats and redesigns the guard: flat memory, but a hot-path
  divergence from upstream whose backtracking effects the oracle cannot see. Take it up when Phase 7
  has benchmarks to measure it against. Investigation: `2026-09-18-repeat-unrolling-investigation.md`.

- **Mutation testing (Stryker.NET), phase 6, scoped and on demand.** It answers "do our tests
  actually pin this behaviour?", which is worth asking of a port. But it is the wrong tool for
  most of this codebase: the differential oracle answers the same question with real ground
  truth and also finds behaviour we never tested at all, whereas mutation testing only finds
  code paths existing tests fail to pin. Stryker reruns the suite per mutant, so a 30k-line
  engine would take hours to days per run - never a merge gate. Where it would earn its keep is
  the small, bounded parts the oracle cannot reach: the public API layer (option handling,
  argument validation, `MatchTimeout`, Span overloads) and the parse-error paths. Revisit in
  phase 6 with real code and real timings; any estimate made now would be a guess. It cannot
  test the PowerShell tooling at all.

  **Updated 2026-08-31 (design spec amendment 14):** the "hours to days per run" objection was a
  working-day objection, and the owner has allowed this pass and the phase 7 optimisation slices
  to run overnight with the machine to themselves. The scope therefore widens to the engine, which
  is precisely what phase 7 rewrites. It stays a one-off measurement with a written verdict, never
  a merge gate - partly for runtime, partly because Stryker.NET reaches TUnit only through its
  Microsoft Testing Platform runner, which is still preview as of 4.16.0.

## Phase boundaries are human checkpoints

`tools/run-slices.ps1` refuses to cross one. When the pending queue empties, the driver stops and
the owner reviews progress, then authors and approves the next phase's slices. See
`docs/plan/OPERATIONS.md`.

## Where the work queue actually is

- `docs/plan/slices/` - pending, lowest number first. Listing this directory **is** the todo list.
- `docs/plan/slices/done/` - completed, with closing notes appended.
- `docs/plan/STATE.md` - current slice, blockers, next action. Thirty lines maximum, rewritten
  each session.
- `docs/plan/DECISIONS.md` - append-only dated one-liners.
