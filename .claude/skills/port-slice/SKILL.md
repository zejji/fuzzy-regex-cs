---
name: port-slice
description: Use to execute one slice of the mrab-regex to .NET port. Self-orients from STATE.md, the roadmap, the slice file and the generated status; works test-first; finishes with the ratchet green, a commit made and the slice file moved to done/. Invoke this at the start of any port working session, including after an interrupted one.
---

# Executing a port slice

One slice, one session, one commit. Context is not carried between slices, so everything you
need is in the repository. Read the four small files below and nothing else until you know what
this slice is.

## 1. Recover, then orient

Recovery first. An earlier session may have been interrupted.

```powershell
git status --porcelain          # dirty?
git log --oneline -3
```

- **Working tree dirty, or `docs/plan/STATE.md` says a slice is in flight:** decide between
  finishing that slice to green or resetting to the last green commit and restarting it. Prefer
  finishing if the work is nearly done and you can see what it was doing; prefer
  `git reset --hard HEAD` if it is scattered or you cannot tell. Never build on top of a mess.
- **Tree clean:** carry on.

Then orient, in this order and no further:

1. `docs/plan/STATE.md` - current slice, blockers, next action.
2. `docs/plan/ROADMAP.md` - which phase this is and what comes next.
3. `docs/plan/slices/` - the pending queue. Your slice is the lowest-numbered file there.
4. `docs/STATUS.md` - generated parity board, so you know what already works.

Read the slice file itself in full. It names its scope, its upstream line references and its
done-criteria. Do not widen the scope. If the slice looks wrong or impossible as written, stop
and say so in STATE.md rather than improvising a different slice.

## 2. Work test-first

The tests for your slice already exist and are skipped. That is the design: Phase 1 ported the
whole upstream suite ahead of the engine.

1. **Un-skip the slice's tests.** The slice file lists the capability tags it delivers; remove
   the `[Skip("needs:<tag> ...")]` attributes carrying exactly those tags. Find them with
   `grep -rn 'needs:<tag>' tests/`. Do not touch any other test's skip attribute, and do not
   un-skip a test whose tag your slice does not deliver just because it happens to pass.
2. **Watch them fail.** `dotnet test tests/FuzzyRegex.Tests` - confirm they fail, and fail for
   the reason you expect. A test that passes before you write any code is a test that is not
   testing the thing.
3. **Implement to green**, in small steps, running the tests as you go.
4. **Any behaviour you discover that no test covers becomes a test**, in the matching
   `Fuzzy.Text.RegularExpressions.Tests.Ported.<Area>` or `Fuzzy.Text.RegularExpressions.Tests.Gaps.<Area>` namespace.

Port faithfully. `Parsing/` mirrors `upstream/regex/_regex_core.py` and `Engine/` mirrors
`upstream/src/_regex.c`, structure and all, so that a future upstream diff maps onto our files
mechanically. Keep upstream's function and variable names recognisable, and quote the upstream
line reference in a comment above anything non-obvious. Resist tidying: a clever restructuring
costs more at every future sync than it saves today. Optimisation is Phase 7, behind benchmarks.

Record each upstream symbol you port in `docs/PORTMAP.md`.

**Delegate the mechanical parts.** Bulk test translation, searching upstream for a symbol,
checking a table - hand those to a Sonnet subagent with a tight brief and ask for compact output.
Keep the judgement in this session.

## 3. Finish

In this order. Every step, every time.

```powershell
pwsh -File tools/check-ratchet.ps1                  # must print GREEN
pwsh -File tools/check-ratchet.ps1 -UpdateBaseline  # only once it is green
```

Then one blind review pass over the diff. Brief the subagent to hand over a **reproduction** -
the exact command and its output, or a failing test - not prose. Do not ask it for explanations or
proposed corrections: prompts that request those measurably raise misjudgement rates. Style
opinions and speculative rewrites are out of scope; reject them unread.

**Wait for the reviewer inside the same turn.** Dispatch it as a blocking call and read its
report as the tool result. Never write a progress message like "review still running, commit to
follow" and end your turn: the driver runs you under `claude -p`, where ending your turn ends the
process, and a session that ends before the commit is a failed slice - the driver stashes your
work and resets the tree to where you found it, so the next attempt starts from scratch. If a
reviewer has not reported yet, you are not finished - stay in the turn. This is not hypothetical:
S07's first attempt reached a green ratchet, 495 passing tests and 410 matching corpus rows, then
ended its turn to wait for a background reviewer and lost the lot (2026-08-30, 54.5M tokens; the
rescue stash was added afterwards, in response to that failure).

Treat every finding as a hypothesis and reproduce it yourself before touching code. Roughly four
in five candidate findings do not survive that gate, and acting on one that should have been
killed is how a review breaks working code.

Then, in this order:

1. **Fix the real findings**, re-run the ratchet, and move on. **No critique loops** - reviewer
   opines, code changes, reviewer opines again is banned. Repairing against a red ratchet, a
   failing test or an oracle divergence is *not* a critique loop; that is ground truth, and two
   rounds of it is the sweet spot. If two rounds have not fixed it, stop and think instead of
   iterating.
2. **If the fixes added public API, changed tooling, or touched anything the reviewer never saw,
   run one more blind pass over that delta only.** This is not a second opinion on reviewed code;
   it is a first pass over unreviewed code, and skipping it is how S01 shipped ~200 lines of
   unreviewed public API. Judge it by what changed, not by how the first pass went.

From phase 3 onward, also run the differential oracle locally before you commit any slice that
touches the engine, and minimise every divergence into a permanent test. The ported suite passing
is evidence of parity, not proof of it - see design spec amendment 10.

All of this is stated compactly, with the evidence and a paste-ready reviewer brief, in
`docs/VERIFICATION.md`. Read that before deciding these rules are bureaucracy; the full reasoning
is in design spec section 8 and amendments 9 and 10.

Then:

1. `git mv docs/plan/slices/S<nn>-*.md docs/plan/slices/done/` and append closing notes to the
   file: what landed, anything surprising, anything the next slice should know, and a
   **"Review" paragraph stating the outcome of the blind pass: findings raised, findings
   reproduced, findings fixed, and whether a second pass over unreviewed changes was needed.**
   A slice whose closing notes do not say this has not recorded its review, and the owner will
   treat it as not reviewed (S06 shipped without the record, and nobody can now tell). Tick the
   slice file's "Done when" boxes as you go; an unticked box is a visible gap.
2. Rewrite `docs/plan/STATE.md` (rewrite it, never append; 30 lines maximum).
3. Append a dated one-liner to `docs/plan/DECISIONS.md` for any decision a future session would
   otherwise have to re-derive.
4. Run `git status --porcelain` and read every line. Each one is either part of this slice or
   scratch you forgot to delete - there is no third category. Delete the scratch; never commit it.
5. Commit everything in one commit: `S<nn>: <what landed>`, then check `git status --porcelain`
   prints nothing at all.

The commit is the slice. A session that ends without a green ratchet, a commit and a clean tree
has not completed a slice, and the driver will treat it as a failure.

## Working under the driver

The unattended session runs with a scoped Bash allowlist and a sandbox. These are the shapes it
rejects, all of which have already burned turns on real slices:

- **Compound commands are decomposed, and every part must match the allowlist.** `dotnet build
  ... | Select-Object -Last 60` is rejected on the filter, not on `dotnet`, and so is
  `dotnet run ... ; grep ...`. Run one command per call and read the output, or pipe only into
  something the allowlist names.
- **The `Bash` and `PowerShell` tools have separate allowlists, and separate vocabularies.**
  `head`, `tail` and `grep` are allowed on the Bash side; `Select-Object`, `Select-String`,
  `Get-Content` and `Get-ChildItem` on the PowerShell side. Piping `dotnet` into `Select-Object`
  from the Bash tool fails twice over - wrong rule family, and the cmdlet does not exist in Git
  Bash. Pick the tool that owns the filter you want.
- **Heredocs and `-c` script blocks are refused outright** ("contains script block that may
  execute arbitrary code"). To run Python, `Write` a `.py` file and run `python thatfile.py`.
- **Glob patterns inside a shell path always need approval.** Use the `Glob` tool instead.
- **The `PowerShell` tool has a second gate the allowlist cannot open.** Before any rule is
  consulted, it statically refuses: `{ }` script blocks (so no `Measure-Command { ... }`), `$()`
  and `(...)` subexpressions, several statements joined by `;`, a nested shell (`pwsh -File ...`),
  a `python ... && dotnet ...` chain, a literal path ending in `\` (read as a UNC share), and any
  command over 1015 bytes. Nine such calls were refused during S09 - every one recovered, but
  each cost a turn. Split multi-statement commands into separate calls, drop the wrapper, and run
  `pwsh -File tools/check-ratchet.ps1` through the **`Bash`** tool, where `Bash(pwsh *)` allows it
  and there is no nested-shell gate.
- **A commit message over ~1015 bytes cannot go inline.** `Write` it to `.scratch/msg.txt` and
  run `git commit -F .scratch/msg.txt`. Every closing note this skill asks for exceeds the cap,
  so this is the normal path, not the exception.
- **Throwaway scratch goes in `.scratch/`, never anywhere else in the working tree.** Oracle
  scripts, build logs, TRX probes, one-off Python: write them to `.scratch/` at the repo root.
  It is gitignored, so a file you forget cannot dirty the tree; it is inside the repo, so the
  sandbox lets you write there when the session scratchpad may not. This matters because a
  scratch file anywhere else is a slice-killer - the driver fails any slice whose working tree
  is dirty at the end, and four forgotten `scratch-*.py` files at the repo root came within one
  commit of throwing away the whole of S08 (2026-08-30).
- **Use the `Read`, `Edit` and `Grep` tools, not `cat`, `sed -i` and `grep`.** They are always
  allowed, they never trip the decomposition rule, and an `Edit` is visible in the transcript
  where a `sed -i` is not.

## Rules that are not negotiable

- **Never end a session with uncommitted work.** Committing is the last thing you do and it is
  not optional. If you are out of road - blocked, out of budget, or the slice is wrong - write
  the blocker into STATE.md and commit *that*, so the next session starts from a clean tree.
  Work left uncommitted is work the driver throws away.

- **Never weaken a test to get green.** If a ported test is wrong, prove it against upstream
  (run the Python `regex` module and quote the output) before changing it, and record why in
  DECISIONS.md.
- **Never disable an analyzer to get a build - but do decide each finding on its merits.** The
  two honest options are to fix the underlying problem or to disapply the rule, and which is
  right is a question about the rule, not about your deadline. Keep and obey rules that improve
  correctness, catch real defects or protect type-safety. Disapply rules that are noise here,
  buy nothing, or would force worse code - a faithful port cannot edit upstream's test data to
  satisfy an analyzer that misreads a regex pattern as an IP address. What is banned is
  suppressing to reach green without deciding, and contorting code to satisfy a rule that offers
  nothing. When you disapply: use the narrowest scope that matches the reason, put it in
  `.editorconfig` beside the existing per-directory relaxations, and write the reason - what the
  rule wanted, and why it does not apply here. Say in your closing notes which option you chose
  and why. A whole-repo disable is right only when the rule is wrong for the whole repo, and
  that claim needs saying out loud.
- **Never edit `docs/STATUS.md` or `tests/parity-baseline.json` by hand.** Both are generated.
- **Stop rather than guess.** If the slice is blocked, write the blocker into STATE.md, commit
  that, and stop. A parked slice is cheap; a wrong slice built on a guess is not.
