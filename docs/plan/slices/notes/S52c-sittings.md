# S52c sitting notes

Per-sitting record for `docs/plan/slices/S52c-metamorphic-invariants.md`. The slice file keeps the
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
