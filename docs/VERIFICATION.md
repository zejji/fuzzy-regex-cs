# Verification rules

Agent-facing. Applies to every review, fix and engine slice in this repo. Reasoning and citations:
design spec section 8 and amendments 9-10.

## Rules

1. **A finding is a hypothesis.** Reproduce it yourself - run the command, read the output - before
   changing a line. Roughly four in five candidate findings do not survive this.
2. **Require a reproduction.** Reject prose rationale, style opinions and speculative rewrites
   unread.
3. **Never ask a reviewer for explanations or proposed corrections.** Those prompts measurably raise
   misjudgement. Ask only for `file:line`, a one-line defect, and the exact command and its output.
4. **One pass per unreviewed change, rather than per slice.** If the fixes add public API, change tooling, or
   touch anything the reviewer never saw, that delta gets its own first pass. Coverage and iteration
   are different axes.
5. **No critique loops.** Reviewer opines, code changes, reviewer opines again is banned.
6. **Repair against execution feedback is exempt from rule 5.** A red ratchet, a failing test or an
   oracle divergence is ground truth rather than an opinion. Two rounds, then stop and think instead
   of taking another swing.
7. **Passing the ported suite is evidence of parity without proving it.** From phase 3, run the differential
   oracle locally before committing any slice that touches the engine, and minimise every divergence
   into a permanent test.
7a. **A wave counts only at THREE SEEDS.** `tools/run-oracle.ps1` runs three by default -
   7, 4242 and the run's date - and is green only when every one of them is. One seed is how four
   divergence families stayed hidden from S14 to S33: every slice ran one, each happened to be
   clean, and "the wave is green" was concluded from it every time. A single-seed run is for
   minimising a row you already have, never for believing a result.
7b. **The oracle proves agreement; only the metamorphic invariants prove consistency, and neither
   alone says anything about correctness where the port inherits upstream's answer.** A wave asks
   "do the two engines agree?" and is blind by construction to a bug the port inherited line for
   line, because then they do - ledger entry 11's mechanisms C and D are exactly that. The
   invariants of `docs/ORACLE-INVARIANTS.md` ask the other question, of ONE engine at a time: does
   this answer contradict another answer the same engine gives? The recorder applies them to
   upstream on every wave row and writes `selfContradiction` into the row;
   `OracleWaveTests.Our_own_answers_never_contradict_themselves` applies the same checks to this
   port. **On a row where the port inherits, read both** - agreement with a self-contradictory
   upstream is not evidence of anything, and gate row 104366 is the worked example: over its
   six-cell grid upstream breaks `greedy-lazy-existence-agree` on two cells and this port breaks it
   on none, so the oracle's "they disagree" had no useful reading and the invariant settled which
   engine was self-consistent. The two together are still not proof: both engines can be wrong in
   the same way and satisfy every invariant, which is what the second engines in
   `docs/plan/OPERATIONS.md` are the third leg for - and what makes the FUZZY rows the thin ice,
   since TRE answered 10 of 126,240 wave rows and none of this slice's violations.
8. **Prove the test fails without the fix.** A test that cannot go red proves nothing. Mutate it once
   and watch it fail.
9. **Verify on real output.** Green tests alone are not enough: read the actual rows, the rendered
   file, the built artifact. State what you checked and what you saw.
10. **Every engine assumption a change relies on is listed, cited and asserted.** Before the first
    commit, the builder writes down each fact about the engine the change depends on ("the anchor
    is fixed while the set lives", "only call frames sit above a call"), with the `file:line` that
    makes it true, and adds a `Debug.Assert` that checks it at run time. Grids run on a Debug build
    so the assertions fire. An assumption that cannot be asserted gets a witness test instead.
11. **A grid is green only if it could have gone red.** For every construct the change reads - each
    key field, each exclusion, each listed assumption - the grid reports how many rows had that
    construct present AND the new mechanism active. A zero means the grid is blind to it and proves
    nothing about it: extend the grammar or write the row by hand, then rerun.
12. **Every part is justified before the first commit, by the builder.** Each key field, exclusion,
    guard and prune has either a witness test that goes red when the part is removed, or a written
    argument with `file:line` that no witness can exist. "None found yet" is neither; it is an
    open item and the change is not done.

## Judging a red scheduled sweep

The scheduled oracle sweep runs fresh seeds, so it keeps drawing new rows of upstream bugs that
have already been judged. Most keys in `ExpectedDivergences.cs` are tables of judged rows, and a
new row of the same bug is not in the table, so the sweep goes red. A key that recognised such
rows automatically would also claim a port bug of the same shape. On 2026-10-09, three attempts at
one did exactly that (`.claude/driver/triage-redseeds-2026-10-09.md`). So the judgement is made by
a person or an agent and written down as data, one line per row, in
`tests/FuzzyRegex.OracleTests/JudgedDivergences.jsonl`.

**How a row is judged.** Take the red row from the sweep's `report.txt`. Reduce it to a minimal
witness and run that witness on both engines. Name the cause: a `LEDGER.md` entry, a
`KNOWN-DEFECTS.md` D-number, or a pending candidate listed in the data file's header. A row whose
cause cannot be named is a new finding, and it goes through the normal triage with a red test. Never guess a cause. Two mechanisms on one row are fine when both are named.

**How it is recorded.** One command per row:

```
python tools/judge-row.py --seed 4242 --row 34781 --cause "ledger 2" --witness "ba??x over 'baa', match, partial: upstream partial (0,3), port no match"
```

The tool reads the question and upstream's answer from `TestResults/oracle/sweep-<seed>/wave.jsonl`
and both answers' text from that sweep's `report.txt`. It refuses a row a key already claims, a
question already judged, and a cause that names nothing. Use `--dry-run` to print the line without
writing it, and `--sweeps` when the sweep folders are in another checkout. The `judged-row` entry
then accounts for the row only while the question and both answers are exactly the recorded ones,
so if either engine's answer changes, the row turns red again.

**Rows upstream crashed on.** This port must never reproduce an upstream crash. Where upstream
crashes on a pattern, the port either answers correctly (preferred) or refuses the pattern at
compile time. The sweep still puts a crashed row to this port, and its `FAULT` block shows the
port's answer on a `port` line. Once the crash is explained (a ledger entry, its location in
upstream's C source, and a check under the MSan screen), judge the row like any other, adding the
evidence with `--crash`:

```
python tools/judge-row.py --seed 187242955 --row 25286 --cause "ledger 11" --witness "..." --crash "exit 3221225477 (access violation), again when run alone; invalid-pointer read in match_fuzzy_changes, _regex.c:20554, MSan clean"
```

The line records upstream's answer as `<upstream crashed>` and pins the port's exact answer. The
row is then accounted for only while upstream's recorded row is still a crash and the port's
answer is still exactly the pinned one. The loader refuses a crash line whose pinned answer is an
error raised while matching (that is a port bug to fix, not an answer to judge). It also refuses a
line marked as a crash whose recorded row is not one, and a crash line whose evidence does not
name the recorded exit code.

**The triage budget is two hours per scheduled run.** Rows still unjudged when the two hours are up
stay red in that run and are picked up by the next one. A red run with known rows in it is better
than a hurried judgement.

**When a mechanism gets its own key.** A mechanism that produces five or more judged rows in each
of three consecutive scheduled runs may be given its own key in `ExpectedDivergences.cs`. The key
needs a planted-bug control: a test showing that a port bug of the same shape is not claimed. If
no key passes that control, the mechanism's rows stay in the data file. Once a key claims a row,
delete its line from the data file; `Every_expected_divergence_still_diverges` fails until you do.

## Reviewer brief

Paste into the review subagent's prompt, filling the three placeholders:

```
Review <SCOPE> in <REPO> for DEFECTS only.

<CONTEXT: what this code is, what is deliberate and must not be reported, where the
source of truth lives and how to run it.>

RULES:
- A finding MUST come with a reproduction: the exact command and its exact output, or a
  failing test. No prose rationale, no speculation, no "consider whether".
- Style, naming and structure opinions are OUT OF SCOPE. Do not report them.
- Specifically hunt for: <THE FAILURE MODES THIS CHANGE COULD PLAUSIBLY HAVE.>
- Verify the build and the test suites; report exact counts.

OUTPUT: numbered findings only. Each: `file:line` - one-line defect - exact reproduction
command and output. If nothing, say "No defects found." No preamble, no summary of the
diff, no praise.
```

Do not add "explain your reasoning" or "suggest a fix" to this brief. Both make it worse.

## Verifier brief - the clause without which it can destroy the slice

The independent verifier of spec amendment 16 limb (d) re-runs claims rather than reviewing, so it
applies and reverts controls. **It is working on a tree full of UNCOMMITTED work with no commit to
fall back to, which makes the ordinary undo the one command it must never use.** Put this in every
verifier brief, verbatim:

```
You are working on a tree with UNCOMMITTED changes and there is NO commit to fall back to.
NEVER run `git checkout`, `git restore`, `git stash`, `git reset` or `git clean`, on any
path, for any reason. To revert a control you applied, re-edit exactly what you edited, or
use the slice's own revert script if it names one. Finish by showing `git status --porcelain`
and `git diff --stat`.
```

Measured, 2026-09-15 (S52 sitting 9): a verifier reverted a four-character control with
`git checkout -- tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` and discarded the entire
slice's work in that file - two new oracle entries, four row constants and three rewritten `Reason`
texts - although the slice's own `break`/`restore` script was sitting in `.scratch/`. It was
rebuilt and proved identical against a dump the verifier had taken from the good build, but only
because the verifier happened to have built that file minutes earlier; nothing guaranteed that.

## Why these rules, in one line each

| Rule | Evidence |
|---|---|
| 1, 2 | An adversarial evidence gate killed ~79% of 171 candidate findings (arXiv:2604.19049). Removing execution-grounded validation raised unnecessary repairs by 131.7% (arXiv:2604.10800). |
| 3 | Prompts requiring explanations and proposed corrections raise misjudgement rates (arXiv:2603.00539). |
| 4 | Not from a study - from this repo. S01 ran its one pass, then committed ~200 lines of public API the reviewer never saw. |
| 5 | Four benchmarked review techniques scored 2.79-9.22% precision; LLM reviewers systematically over-flag correct code (arXiv:2509.01494, arXiv:2603.00539). |
| 6 | Two repair rounds against error tracebacks capture 76-95% of achievable improvement across seven models, no model regressing (arXiv:2604.10508). |
| 7 | Only 72% of LLM-transpiled functions were semantically equivalent despite compiling and passing the existing tests; differential testing against the reference caught the rest at 85.7-88.2% precision, 100% recall (arXiv:2510.07604). |
| 7a | Not from a study - from this repo. S33's blind review ran the generators at seeds no earlier slice had used and found three unjudged divergence families at once, none of them caused by S33 (DECISIONS 2026-09-12). A 2000-row generator run costs about fifteen seconds, so the third seed is not the expensive part of anything. |
| 10-12 | Not from a study - from this repo, 2026-09-26/27. Six defects in the F-A and failed-call memo work reached blind review, each resting on an unchecked engine assumption (the search anchor "fixed for one call", only call frames above a call, END_FUZZY always restoring the totals) or on a grid that could not produce the case (no `\G` in the grammar; three key fields "with no witness" had witnesses a reviewer built in minutes). Every one was caught before main, but each cost a full review round. |

## Limits

The mechanisms are well supported; the magnitudes are not ours. arXiv:2604.10508 used mid-size open
models on benchmark-scale problems. The 72% is GPT-4o on C-to-Rust. No study found compares LLM
review against differential or symbolic verification head to head. Treat every figure above as a
direction with a sound mechanism behind it, never as a forecast.

One caveat that does **not** apply here: cross-implementation differential testing is normally
weakened by dialect divergence (arXiv:2603.00311). We compare against the exact upstream commit we
are a port of, which is the ideal case for the technique rather than the problematic one.
