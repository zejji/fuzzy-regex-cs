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
tools/check-ratchet.ps1                  # must print GREEN
tools/check-ratchet.ps1 -UpdateBaseline  # only once it is green
```

Then one blind review pass over the diff. Brief the subagent to hand over a **reproduction** -
the exact command and its output, or a failing test - not prose. Do not ask it for explanations or
proposed corrections: prompts that request those measurably raise misjudgement rates. Style
opinions and speculative rewrites are out of scope; reject them unread.

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
   file: what landed, anything surprising, anything the next slice should know.
2. Rewrite `docs/plan/STATE.md` (rewrite it, never append; 30 lines maximum).
3. Append a dated one-liner to `docs/plan/DECISIONS.md` for any decision a future session would
   otherwise have to re-derive.
4. Commit everything in one commit: `S<nn>: <what landed>`.

The commit is the slice. A session that ends without a green ratchet and a commit has not
completed a slice, and the driver will treat it as a failure.

## Rules that are not negotiable

- **Never weaken a test to get green.** If a ported test is wrong, prove it against upstream
  (run the Python `regex` module and quote the output) before changing it, and record why in
  DECISIONS.md.
- **Never disable an analyzer to get a build.** The per-directory relaxations in `.editorconfig`
  already cover what a faithful port legitimately needs.
- **Never edit `docs/STATUS.md` or `tests/parity-baseline.json` by hand.** Both are generated.
- **Stop rather than guess.** If the slice is blocked, write the blocker into STATE.md, commit
  that, and stop. A parked slice is cheap; a wrong slice built on a guess is not.
