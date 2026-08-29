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

Then one blind review pass: ask a subagent to review the diff for defects, with the instruction
that a finding must come with a failing test or a concrete reproduction. Style opinions and
speculative rewrites are out of scope - reject them. Fix real findings, re-run the ratchet, and
move on. One pass. No loops.

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
