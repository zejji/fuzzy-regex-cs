# Verification rules

Agent-facing. Applies to every review, fix and engine slice in this repo. Reasoning and citations:
design spec section 8 and amendments 9-10.

## Rules

1. **A finding is a hypothesis.** Reproduce it yourself - run the command, read the output - before
   changing a line. Roughly four in five candidate findings do not survive this.
2. **No reproduction, no finding.** Reject prose rationale, style opinions and speculative rewrites
   unread.
3. **Never ask a reviewer for explanations or proposed corrections.** Those prompts measurably raise
   misjudgement. Ask only for `file:line`, a one-line defect, and the exact command and its output.
4. **One pass per unreviewed change, not per slice.** If the fixes add public API, change tooling, or
   touch anything the reviewer never saw, that delta gets its own first pass. Coverage and iteration
   are different axes.
5. **No critique loops.** Reviewer opines, code changes, reviewer opines again is banned.
6. **Repair against execution feedback is not a critique loop.** A red ratchet, a failing test or an
   oracle divergence is ground truth, not an opinion. Two rounds, then stop and think rather than
   take another swing.
7. **Passing the ported suite is evidence of parity, not proof.** From phase 3, run the differential
   oracle locally before committing any slice that touches the engine, and minimise every divergence
   into a permanent test.
7a. **A wave counts at THREE SEEDS and not before.** `tools/run-oracle.ps1` runs three by default -
   7, 4242 and the run's date - and is green only when every one of them is. One seed is how four
   divergence families stayed hidden from S14 to S33: every slice ran one, each happened to be
   clean, and "the wave is green" was concluded from it every time. A single-seed run is for
   minimising a row you already have, never for believing a result.
8. **Prove the test fails without the fix.** A test that cannot go red is not a test. Mutate it once
   and watch it fail.
9. **Verify on real output**, not just on green tests: the actual rows, the rendered file, the built
   artifact. State what you checked and what you saw.

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

## Limits

The mechanisms are well supported; the magnitudes are not ours. arXiv:2604.10508 used mid-size open
models on benchmark-scale problems. The 72% is GPT-4o on C-to-Rust. No study found compares LLM
review against differential or symbolic verification head to head. Treat every figure above as a
direction with a sound mechanism behind it, never as a forecast.

One caveat that does **not** apply here: cross-implementation differential testing is normally
weakened by dialect divergence (arXiv:2603.00311). We compare against the exact upstream commit we
are a port of, which is the ideal case for the technique rather than the problematic one.
