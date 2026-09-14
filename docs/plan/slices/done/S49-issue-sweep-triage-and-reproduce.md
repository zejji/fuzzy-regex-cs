---
slice: S49
phase: 6
title: Upstream issue sweep, part one - live re-triage, and a reproduction or a written dismissal for every open issue
delivers: []
---

# S49 - The issue sweep, part one

The oracle is blind to inherited bugs by construction (amendment 13), so the tracker is the
instrument. The 2026-08-31 triage of 79 issues is a stale reading of a moving list; re-triage live.

## Before launch (orchestrator)

`gh` is not in the driver's allowlist and stays out, so an unattended session cannot post. The
orchestrator snapshots the tracker before launch into `docs/plan/upstream-issues/<date>-open.json`
with `gh issue list -R mrabarnett/mrab-regex --state open --limit 500 --json
number,title,body,labels,createdAt,updatedAt,comments`, and the session works from that file.
Comments are included because the maintainer's "looks like a bug" is evidence.

## Scope

- **Re-triage every open issue** into the 2026-08-31 classes (A not a bug; B Python-specific or
  API-shape; C engine or parser bug) with a one-line reason each, in
  `docs/plan/upstream-issues/<date>-triage.md`. Diff against the old triage: closed, new, reclassed.
- **Reproduce every class C issue** against `.venvs/regex-2026.9.10` AND against this port, as a
  probe under `tools/probes/` and a test under `Gaps/`. Known from the old triage: 367 (partial
  with jointly unsatisfiable lookaheads), 425 (branch reset with mixed named and numbered groups),
  470 (done, S42), 551 (infinite loop on a V1 search), 554 (fullmatch `MemoryError` on a long
  string), 563 (`\m` with a fuzzy quantifier at position 0), 564 (loosening `<=1` to `<=2` returns
  fewer matches), 596 (`{e<=0}` 210x slowdown), and 611-614 (fixed upstream, verified by S44).
  Each lands in one of: reproduces on both (inherited, S50 fixes); reproduces upstream only (port
  right, pin and ledger); does not reproduce (dismissed with the run quoted); not a bug.
- **Resource issues (551, 554, 596)** are reproduced under `timeout` and a memory cap, with the
  numbers, not by waiting.
- Tests for inherited issues are written now, failing, and skipped with `needs:issue-<n>` so S50
  un-skips them; the ratchet stays green.

## Verification

- Triage table complete with one reason per issue; a probe and a test per class C issue; a ledger
  entry per inherited issue.

## Done when

- [x] Every open issue classed and reasoned; every class C issue reproduced or dismissed with output.
- [x] Failing tests written and tagged for S50; probes committed; ledger updated; nothing filed.
- [x] Ratchet GREEN, blind review (hunt: a dismissal that reasoned instead of ran; a reproduction
      against the old interpreter), commit.

---

## Closing notes (2026-09-14, one sitting)

**The tracker had barely moved, and measuring that was the first useful thing.** 74 open against the
old triage's 79: five closed (609, and 611-614, the memory-safety group S44's sync already carried),
**none opened**, and - checked rather than assumed - **not one still-open issue's body or comments
changed either**: the newest `updatedAt` across all 74 is 2026-08-28 and the newest comment is
2026-03-25. So every reclassification below is a triage correction, not a response to new
information, and that framing is what made it worth re-reading the text properly instead of
diffing it.

**The re-triage was done blind and then reconciled.** A Sonnet subagent got the 74 bodies and
comments with no sight of the old classes; the two readings agreed on 60 of 74. Eleven of the
fourteen disagreements are the A/B boundary and decide nothing. The four that matter - 334, 367,
397, 596 - were settled by running the case, and 589, which both readings left undecided, was
settled by a second engine. Final: **A=36, B=28, C=10**, and no class D, because every issue the
old triage could not judge has now been run.

**Three of the old triage's twelve "real engine bugs" are stale, and one was never a bug.**

- **334 does not reproduce.** The 2019 "kernel crash" and the maintainer's own reduction both answer
  None in milliseconds on 2026.9.10 and here.
- **551 does not reproduce.** All three of the reporter's flag combinations answer None in 0.00s.
  The maintainer's "I've done a partial fix" (2025-02-10) has landed.
- **596 does not reproduce.** `(?:X){e<=0}` costs what `(?:X)` costs on both engines. The harness is
  calibrated against the report: it measures upstream's plain literal at 2.4-2.5 us where the
  reporter measured 2.88, so it is measuring the same thing, and the 210x is gone.
- **367 is not a bug at all**, which is the reclassification with the most behind it. It complains
  that a partial match is reported where no continuation could complete it. PCRE2 10.47 answers
  PARTIAL on every one of its rows, including the reporter's own `(?!.+).*` over '1'. What is
  actually wrong is upstream's documented promise - "whether a complete match could be possible if
  the string had not been truncated" (`upstream/docs/Features.html:576`) - which no engine keeps and
  which 367's own example shows to be undecidable, since it encodes primality. The existing pin in
  `Gaps/Engine/PartialMatchingTests.cs` was marked provisional pending this sweep; it is now
  permanent, and its comment says why.

**589 went the other way: both readings had it undecided, and it is a real bug this port inherits.**
It is the same machinery as 367 failing in the opposite direction - a prefix denied although its
completion exists. Two things decide it. Upstream's own documented definition (above) makes 'True' a
partial, because 'Truest' matches. And PCRE2 answers PARTIAL where upstream answers None, with the
mechanism visible in one row: `True\b` over 'True' is `match` under SOFT and `PARTIAL` under HARD,
so PCRE2 treats a boundary at the end of the available text as *unresolved* rather than deciding it
against text the caller has declared truncated. **367 and 589 must not be conflated** - 367's false
positive is shared by every engine and undecidable; 589's false negative is shared by no second
engine and is decidable at the truncation point - and ledger entry 21 says so explicitly, because a
report that merges them would be rejected and so would a fix.

**397 turns out to be already fixed here, by a slice that never knew it.** A left-recursive DEFINE
raises `MemoryError` upstream in ~0.8s for **both** of the 2021 reporter's patterns, including the
one he said did not crash. This port answers None - S47's ledger-entry-14 positional guard, which
was built for the fuzzy shape and covers the plain one. It is not a silent cap: the same DEFINE
still matches `'c:d'`, and well-founded recursion is untouched. The DIVERGENCES row for entry 14 now
names issue 397, so a reader hunting it finds the answer.

**S50's list is five: 425, 554, 563, 564, 589**, each with a failing test skipped `needs:issue-<n>`
in `tests/FuzzyRegex.Tests/Gaps/UpstreamIssues/InheritedIssueTests.cs` and a ledger entry (17-21).
All five were watched failing with the skips commented out - `total: 5, failed: 5`, each for the
expected reason - before being parked. **554 is the one to read first**: this port does not merely
inherit it, it is worse, giving up at n=4,000,000 where upstream manages 6,000,000 and stdlib `re`
manages 10,000,000.

**Every test asserts less than its issue asks for, deliberately.** 425's numbering rule is genuinely
open upstream, so the test asserts only what the maintainer's options 2 and 3 share; 564 asserts
monotonicity rather than a span list; 554 asserts upstream's own ceiling rather than a byte figure.
A sweep slice has no authority to choose upstream's semantics, and a test that did would have to be
rewritten by the slice that actually fixes it.

**No `src/` file changed** (`git diff --stat -- src/` is empty), so no oracle wave is owed and no
negative control was run - there is no generator or engine change for one to act on. The ratchet is
GREEN at 5968 total / 5963 passing / 5 skipped, baseline unchanged at 5855; `tests/parity-baseline.json`
differs from HEAD only in its `generatedUtc`.

### Review

**Two blind passes and an independent verifier, and all three found something.**

The **first blind pass** (Opus, over the whole diff, hunting a dismissal that reasoned instead of
ran and a reproduction against the wrong interpreter) raised **three findings; all three
reproduced; all three were fixed.**

1. **The argument for calling 425 a defect over-claimed, and the reviewer quoted the text that
   refutes it.** The draft said the maintainer offers three numbering rules, that all three number
   the two groups differently, and that today's behaviour "matches none of them". His option 1 is
   explicitly labelled "(current behaviour)". So the honest position is narrower: options 2 and 3
   both fix the row, option 1 is the status quo, and this slice **does** commit a fixer to rejecting
   it - on the ground that under option 1 `(?P<bug>BUG)`'s text is unreachable through any API and
   `groupindex` maps the name onto another group's text, which the maintainer himself calls "the
   problem". Rewritten in ledger 17 and in the test comment, with the over-claim recorded rather
   than quietly deleted.
2. **The `re` and `regex` bytes-per-repetition figures existed only in a gitignored scratch script**
   while the ledger claimed every reproduction was re-runnable from the committed tree. This is the
   exact failure an earlier audit caught in S44-S46. Fixed by moving the `tracemalloc` measurement
   into `tools/probes/upstream-issue-sweep.py` itself.
3. **"~8 MB of subject" in the 554 test was half the real size** - 4,000,000 repetitions of "ab" is
   8,000,000 chars, 16 MB in UTF-16. Corrected.

The reviewer also confirmed, and this is worth recording because it is most of the slice: all five
tests fail today, the probes load `.venvs/regex-2026.9.10`, every quoted PCRE2 row reproduces
verbatim, `Features.html:576` carries the quoted sentence, the dismissals and reproductions all
reproduce, and the totals and tracker-freshness claims hold.

A **second blind pass** went over only the delta the first reviewer never saw - the new `tracemalloc`
code, the rewritten ledger paragraphs and the changed comments. It raised **three more, all
reproduced, all fixed**: the new comment claimed the traced peak *includes* the subject string when
the probe builds the subject before `tracemalloc.start()` (checked: `get_traced_memory()` reads
`(0, 0)` at `start()` with a 2,000,041-byte subject live, so the peaks are engine allocation only -
a better fact than the one claimed); the ledger repeated it; and the table's n=2,000,000 row quoted
`re`/`regex` cells the probe did not measure, fixed by adding 2,000,000 to the probe's sizes.

**The independent verifier (fresh Opus, commit-ready tree, 40-odd checks) confirmed everything
except the port's own memory figures - and that one was worth the whole exercise.** It could not
reproduce the port's per-repetition bytes at all: it measured 537 B/rep flat where the table said
574 then 343. Chasing it rather than averaging it found the cause. The engine rents its backtracking
buffer from a **process-wide** pool, so a later call may reuse an earlier call's buffer and appear to
allocate half as much; a fresh `FuzzyRegex` per row does not help, and whether the buffer survives
depends on GC timing. Measured both ways: n=2,000,000 allocates **1166.2 MB (611 B/rep) cold** and
**654.2 MB (343 B/rep) warm**, and both occur across runs of the same script. The probe now uses
`GC.GetTotalAllocatedBytes` instead of a live-heap delta, and **only the first row's figure is
quoted** - 611 B/rep at n=1,000,000, identical in every run, against upstream's 192 and `re`'s 99.
The 1GB bound is the one number that is deterministic by construction, and it is what the test
asserts.

**The same lesson caught a second claim on the way out.** The port's issue-596 timing was recorded as
a ratio of 0.83-0.87, and the next run gave 0.90. The comment now states what the measurement
actually supports - `(?:X){e<=0}` against `(?:X)` sat between 0.88 and 1.02 across five runs, so the
constraint costs nothing detectable - and says in terms not to quote a tighter band. **Read this and
finding 3 of the second pass together: three separate numbers in this slice were quoted more
precisely than the instrument justified, and two of them were caught only by someone re-running
them.** A figure that moves between runs needs a ratio or a range, and a figure that moves with the
*order of calls* needs its cause found before it is quoted at all.

**Nothing was filed upstream** (owner decision, 2026-09-12: filing is the last step of Phase 8), and
`gh` was not used.
