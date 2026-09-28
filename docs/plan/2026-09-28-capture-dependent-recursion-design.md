# Capture-dependent recursion and the call guard: design

Date: 2026-09-28. Status: design only, nothing in `src/` changed. Branch `maint/call-guard` at
b3b988f (not merged); main is 9a27677 and holds the reviewed guard, 94ba2d5. Follows ledger entry
14 (`docs/plan/upstream-reports/LEDGER.md`, "The guard refined") and the two failed repair rounds,
1b72ada and b3b988f.

Measurements come from the owner's laptop, Release, one run per cell, with other jobs running, so
read them as orders of magnitude. The first row of each run includes JIT (about 200 ms). The
prototypes were built in `.scratch` copies (`git archive` of `src`), never in this tree, and have
been deleted; section 8 says how to rebuild them.

## Summary

- **The blow-up is in breadth, not depth.** On `(?:()|a)(?R)|(?(1)x)` over `a...ay`, b3b988f nests
  at most once per position. Each position gives a choice, "set group 1 here and nest, or not", and
  every choice sequence leads to a different key, so it walks 2^n call chains. A bound on nesting
  depth, which is what option (b) proposes, cannot reduce that.
- **The key is too fine, not too coarse.** b3b988f keys a call on the *spans* of the groups that a
  conditional or backreference reads. But a conditional reads only whether its group is set, and a
  backreference reads only the captured *text*. The empty groups that `()` sets at positions 3 and
  7 are different spans but the same state as far as any reader can tell. Keying on what is
  actually read (option d, new) takes every review shape back to main's speed.
- **(d) fixes every known capture-dependent wrong answer.** It matches upstream on the three red
  rows and on a new witness where main gives a wrong span rather than None:
  `.*z|\1b|(?(1)(?=(?<g>aa))|(?=(?<g>a)))(?R)` over `aab` gives `(0,3)` upstream and in (d), but
  `(1,3)` on main. That witness also rules out keying on set-or-unset alone.
- **(d) leaves one exponential class, and a failure memo closes it.** If a backreferenced group is
  captured inside a lookaround, it can take a different text at every position.
  `(?:(?=(a*))|a)(?R)|\1x` over `a^20 y` takes about 5 s under (d), 14 ms on main and 51 ms with
  (d) plus a C1-style failed-call memo. Upstream raises MemoryError on this shape.
- **(c), accepting None, has no support.** No engine surveyed returns None on the red rows.
  Upstream and PCRE2's JIT answer `(0,1)`. PCRE2's interpreter and Perl raise an error instead.
  Main returns None on those rows, and a wrong span on the new witness.
- **New, separate defect: the reach can be full before the first call.** `.*z|(?:|(?R)a)`
  fullmatch over `aa` gives `(0,2)` upstream and None on main. The `.*z` branch reads the whole
  text first, so no later call can show growth, and the finite left recursion is refused again.
  PCRE2 fails this row with an error. No key change fixes it, because it is in the reach component
  (section 6b). It needs its own red tests and design step.
- **Recommendation: (d) now, plus the failed-call memo extended with the guard's state (a) once C1
  is on main.** Key each open call on what is read: a set-or-unset bit for a group that only
  conditionals read, and the captured text for a group that a backreference reads. Build (d) first,
  test-first. Before (d) can merge, C1 must merge with its key extended by the guard's state
  (section 7), so the lookaround class is not a measured regression on main.

## 1. What the guard must guarantee

The guard (ledger 14; `Matcher.cs:8493-8512`, `MatchState.cs:293-331` at b3b988f) refuses a group
call that re-enters the same group at the same position when nothing new has been reached since the
open call was made. Two properties matter:

1. **Termination.** Every path must be finite.
2. **No wrong answer where upstream's search terminates.** Upstream has no guard: it runs the
   backtracking search to the end, or runs out of memory. Wherever it does finish, its answer is the
   first success in backtracking order, and the port must give the same one. Where upstream raises
   MemoryError, no reference answer exists, and the port's answer must still be one the pattern's
   grammar allows. The brief says "no wrong None". The new witness in section 3 shows that a wrong
   span is possible too, so the property has to cover both.

A refusal is safe only when the refused call would repeat the open call's work exactly. That holds
when everything the called group can read is the same as when the open call was made. The
position, the call and the reach are in main's key. The captures a reader looks at are not, and
that is the defect.

## 2. Why b3b988f is exponential

A timeline makes this concrete. Take `(?:()|a)(?R)|(?(1)x)` over `aaaay`. Call a call at position
j, with group 1 holding value s, `R(j, s)`.

- `R(0, unset)` first tries `()`. That sets group 1 to (0,0), and the call `R(0, (0,0))` has a
  new key, so it is let through. Inside it, `()` again gives the same key and is refused, so it
  takes `a` and calls `R(1, (0,0))`.
- `R(1, (0,0))` tries `()`, which sets (1,1). `R(1, (1,1))` is a new key, so it is let through.
- And so on. At every position, the call made with the *old* value and the call made after
  re-setting the group are both explored. Each has its own subtree, and those subtrees differ only
  in which earlier position last set group 1.

The subtree size f(m), with m characters left, obeys f(m) = 2 f(m-1) + 1, which gives 2^m. Only
O(n^2) distinct (position, value) states exist. The same state is reached along many different
chains, and b3b988f re-explores it each time.

| `(?:()|a)(?R)|(?(1)x)`, search over `a^n y` | n = 20 | n = 24 |
|---|---|---|
| main 9a27677 | 0 ms, (0,20) | 0 ms, (0,24) |
| b3b988f | 0.9 to 2.5 s, 6.3 M calls, (0,20) | 16.7 s, 101 M calls, (0,24) (c1 build, memo off) |
| upstream 2026.9.10 | MemoryError, 0.9 s | MemoryError |

With more groups read it is worse. `(?:()|()|a)(?R)|(?(1)x)|(?(2)x)` times out at 20 s for
n = 12 under b3b988f.

## 3. Engine survey

Probes: `regex` 2026.9.10 and `pcre2` 0.7.1 (libpcre2 10.47) from Python 3.14, with the PCRE2
interpreter (`jit=False`) and JIT measured separately, and Perl 5.42.3 (Cygwin). Perl was measured
with search only, because a `\A...\z` wrapper changes what `(?R)` recurses into.

| Pattern, subject | upstream | PCRE2 interpreter | PCRE2 JIT | Perl | main | (d) |
|---|---|---|---|---|---|---|
| `(?(a)(?(b)x|(?<b>)(?R))|(?<a>)(?R))`, `x`, search | (0,1) | error: nested recursion at the same subject position | (0,1) | dies: Infinite recursion | None | (0,1) |
| same, fullmatch | (0,1) | error | error | not measured | None | (0,1) |
| `(?:\1x|\2()(?R)|()(?R))`, `x`, both | (0,1) | error | search (0,1), fullmatch error | dies | None | (0,1) |
| `(?:(?P=b)x|(?P=a)(?<b>)(?R)|(?<a>)(?R))`, `x`, both | (0,1) | error | search (0,1), fullmatch error | dies (`\k<>` form) | None | (0,1) |
| `.*z|\1b|(?(1)(?=(?<g>aa))|(?=(?<g>a)))(?R)`, `aab`, search | **(0,3)** | not measured | not measured | not measured | **(1,3)** | (0,3) |
| `(?:()|a)(?R)|(?(1)x)`, `aaaay`, search | MemoryError | error | JIT stack limit | dies | (0,4) | (0,4) |
| `(?:()|...|())(?R)|\1x` (9 groups), `x`, fullmatch | MemoryError | error | error | dies | (0,1) | (0,1) |
| `(?:|(?R)a)`, `aa`, fullmatch | (0,2) | (0,2) | (0,2) | not measured | (0,2) | (0,2) |

What the survey says:

- PCRE2's interpreter and Perl refuse *every* same-position recursion, capture-dependent or not.
  Both treat it as an error in the pattern and fail the whole match, not just the path. PCRE2's
  JIT has no such check, and answers `(0,1)` wherever its stack lasts.
- No engine returns None on the red rows. Where an engine answers, it answers `(0,1)`.
- The grammar agrees. For `G -> (?(a) (?(b) x | (?<b>) G) | (?<a>) G)`, 'x' is derived by setting
  a, then b, then matching x: three steps, all finite.

So option (c) would pin an answer that neither principle nor any engine gives. The engines that
refuse these shapes do it loudly, with an error. The port would do it silently. The new witness
also shows that main's refusal can change a match's span, which (c) would have to accept as well.

## 4. The options, measured

Prototypes, all built on b3b988f:

- **r2**: b3b988f as it is. The key is the spans of the read groups.
- **bmask**: the key is only whether each read group is set.
- **(d)**: the key is a set-or-unset bit for a group that only conditionals read, and the captured
  text for a group that a backreference reads.
- **c1**: r2 plus a failed-call memo in the style of C1. Its key is (call, position, key captures,
  and each open call at or after the position with its captures and a bit saying whether its reach
  equals the current reach). A call that runs out of choices without returning is recorded, and a
  later call with the same key fails at once. The prototype has none of C1's exclusions.
- **d+c1**: (d) with the same memo.

Timings (ms), with "t/o" for a timeout at 20 s:

| Shape (search unless marked) | main | r2 | bmask | (d) | c1 | d+c1 | upstream |
|---|---|---|---|---|---|---|---|
| `(?:()|a)(?R)|(?(1)x)`, n = 24 | 0 | t/o (16.7 s earlier run) | 1 | 1 | 5 (1,250 calls) | 1 (145 calls) | MemoryError |
| two read groups, n = 12 | 0 | t/o | 0 | 0 | 52 | 0 | not run |
| three read groups, n = 12 | 0 | t/o | 1 | 1 | 1,074 (362 k calls) | 0 | not run |
| 9 empty groups + `\1x`, fullmatch `x` | 7 | 9 | 7 | 6 | 6 | 5 | MemoryError |
| `(?:()|a)(?R)|\1x`, n = 20 | 1 | 3,370 | 7 | 7 | 7 | 4 | MemoryError |
| `(?:(?=(a*))|a)(?R)|\1x`, n = 20 | 14 | 5,463 | 20 | **5,008** | 17 | 51 | MemoryError |
| `(?:(?=(a*))|a)(?R)|(?(1)x)`, n = 20 | 0 | 2,545 | 1 | 0 | 1 | 1 | MemoryError |
| witness `.*z|\1b|...`, `aab` | **(1,3)** | (0,3) | **(1,3)** | (0,3) | (0,3) | (0,3) | (0,3) |

The spans agree across all columns on every timing row, except the witness row, where main and
bmask are wrong.

### (a) Memoise call outcomes

C1 (`maint/fuzzy-exact-deletion`, `Matcher.cs:3103-3155` and `PatternObject.cs:780` there)
records a call's entry key when the call runs out of choices without returning. A later call with
that key fails at once. Its key already holds the spans of the read groups and the open calls at
or after the position. Keying on the read state therefore stops being expensive: the same state
reached along a different chain is answered from the set, and is not explored again.

- **Measured:** c1 turns the 2^n walk into about 2n^2 calls (882 at n = 20, 51,842 at n = 160 in
  116 ms). The memo alone is still polynomial of degree about r + 1, where r is the number of read
  groups: 362 k calls, 1.1 s, for three groups at n = 12. With (d)'s key, the same rows take 152
  calls.
- **Soundness with the reach guard (reasoned, not yet shown by a witness).** C1 was written before
  the guard read the reach. A refusal now depends on whether the reach has grown since an ancestor's
  call was made. The reach grows monotonically during an attempt. So a call made later with the
  same key sees at least the reach the first one saw, and can grow less from there. That means it
  gets refused at least as often, and a recorded failure stays a failure. That argument covers the
  called group's own inner calls. It does not cover an ancestor outside the call whose reach equalled
  the entry reach the first time (so it could refuse) but not the second time. So the key needs, for
  each open call it includes, one bit: does its reach equal the current reach. The prototype has
  this bit. **When fuzzy-a2's C1 merges with main's reach guard, its key must gain this bit as
  well**, or it is unsound by this argument.
- **C2** (exit summaries, fuzzy-a2's `2026-09-28-exact-deletion-retry-rederivation.md` section 4)
  would also cover calls that return and then fail further on. I did not find a capture-dependent
  shape where that matters: with the z-continuation shapes I tried, the calls returned exits and
  none of them blew up under r2 (for example, `(?:(?:()|a)(?R)|(?(1)|))z` over `a^16 yz` made 580
  calls). C2 adds a new backtracking opcode and replay frames, and nothing measured needs it.
- **Limit:** production C1 is off wherever a capture group, call or fuzzy section sits inside a
  lookaround, atomic group, possessive repeat or conditional test (`UseCallMemo`). The one
  remaining exponential class, backreferenced groups captured inside lookarounds, is exactly what
  it switches off for. Section 7 deals with that.

### (b) A bounded guard

The brief proposes allowing re-entry through a read-state change "at most once per state on the
current call chain". There are two ways to read that:

1. *Each read state at most once on the chain.* That is what b3b988f already does, since the set
   holds each key once. It is the exponential row above.
2. *At most one read-state change per (call, position, reach).* That still passes the red rows.
   Each needs only one re-entry at position 0, because the first call is not a re-entry. But on
   the blow-up shape, b3b988f already nests at most once per position (section 2), so this bound
   changes nothing there.

Either way, the depth bound leaves the 2^n breadth alone. The variant that does fix the speed is
**bmask**, which keys only on whether each group is set. It bounds the chain at r + 1 per
position, because along a chain groups only become set. It is as fast as main on every row. But
it is wrong wherever the *value* of a backreferenced group matters: the witness row, 6 rows of
grid 3 and 3 rows of grid 4 (section 6).

### (c) Accept None

Covered by section 3: no support in principle or in the survey, and the new witness shows the
defect is not limited to None. Reject.

### (d) Key on what the callee reads (new)

A group call can observe a read group's capture in exactly two ways:

- a group-exists conditional, `(?(n)...)` or `(?(name)...)`, which reads whether the group is set
  (upstream `build_GROUP_EXISTS`, port `NodeCompiler.cs:1143-1160`);
- a backreference, including named and fuzzy ones, which compares the captured text with the text
  ahead (`NodeCompiler.cs:1375-1388`).

Both mark the group `Referenced` (`NodeCompiler.cs:323-327`), which is all b3b988f's
`GroupsSomethingReads` uses (`Matcher.cs:3061-3073`). The design splits that flag. A group read
only by conditionals contributes one bit. A group read by a backreference contributes its text:
two captures with equal text make every backreference behave the same way, whatever their
positions. Comparing exact text is finer than case-insensitive or fuzzy comparison needs, so it
can only keep two states apart that could safely have been merged, never merge two that differ.

- **Termination:** each backreferenced group has at most (n + 1) n / 2 + 1 distinct texts, and
  far fewer in practice (one for all empty captures). Each conditional-only group has two states.
  The bound in `MatchState.cs:316-322` becomes (n + 1) * product of those counts.
- **Cost:** one bit or one (start, length) pair per read group per call. Equality compares text
  only when the lengths match, and the hash can be the length plus a few characters. This is no
  worse than b3b988f's per-call snapshot (the SHORTCUT at `MatchState.cs:46-50`).
- **Residual:** the only way a group gets a different text at the same position, without new text
  being reached, is a capture inside a lookaround. Outside a lookaround, a group captured between
  two calls at the same position must be empty, because the position did not move. So the residual
  class is "a backreferenced group captured inside a lookaround, re-captured on a same-position
  recursion": 5 s at n = 20 above. The failed-call memo collapses it (51 ms in d+c1).
- **One unproven point:** at a group start or end, `CaptureChange` is bumped when a referenced
  group's new span differs from its current one (`Matcher.cs:7450-7453`). A repeat's
  empty-iteration check reads that counter (`Matcher.cs:7618`). So a called group can, in
  principle, behave differently for two incoming spans with the same text. No grid row shows it:
  (d) and r2 agree on all 39,000 rows, including grid 4, which puts captures of the read group
  inside repeats within recursions. Before the build, try to construct a witness. If one exists,
  key that group's span only when its capture sits inside a repeat body in a called group.

## 5. The review's shapes, per option

From `.claude/driver/correctness-checklist-2026-09-26.md` at 06:56 and 07:05, plus the repro rows:

| Shape | main | b3b988f | bmask | (d) | d+c1 |
|---|---|---|---|---|---|
| Three red rows (search and fullmatch) | None, wrong | (0,1) | (0,1) | (0,1) | (0,1) |
| k empty groups + `(?R)` + `\1` (k = 8, 9) | fast | fast (the fix 1b72ada lacked) | fast | fast | fast |
| One read group, `a^n y` | 0 ms | 2^n | 0 ms | 0 ms | 0 ms |
| More read groups | 0 ms | worse | 0 ms | 0 ms | 0 ms |
| Finite left recursion `(?:|(?R)a)` | (0,2) | (0,2) | (0,2) | (0,2) | (0,2) |
| Value witness `.*z|\1b|...` | (1,3), wrong | (0,3) | (1,3), wrong | (0,3) | (0,3) |
| Lookaround-captured backreference, n = 20 | 14 ms | 5.5 s | 20 ms | **5.0 s** | 51 ms |

## 6. Grids

Four generated grids of recursive patterns with capture setters and readers, each over short
subjects, with search and fullmatch. All 5 prototypes and main ran with a 0.5 s timeout per row.
Rows where either side timed out are left out of the comparison. r2 is the reference for the key,
because it keys on the exact spans.

| Grid | Rows | main vs r2 | bmask vs r2 | (d) vs r2 | c1 vs r2 | d+c1 vs r2 |
|---|---|---|---|---|---|---|
| 1: general recursion, lookarounds, repeats | 9,000 | 0 | 0 | 0 | 0 | 0 |
| 2: flag setters and conditional or backreference tests | 9,000 | 13 | 0 | 0 | 0 | 0 |
| 3: one named group, captured with different texts inside lookarounds | 9,000 | 7 | 6 | 0 | 0 | 0 |
| 4: captures of the read group inside repeats | 12,000 | 19 | 3 | 0 | 0 | 0 |

Timeouts in grid 4: r2 30, (d) 15, c1 8, d+c1 4, main 0.

**Upstream.** Upstream ran in-process with a 0.5 s timeout. On grid 2 it answered 4,486 rows and
on grid 3 5,551; it timed out or ran out of memory on the rest. Every variant, main included,
agrees with upstream on every row it answered. So the rows where main or bmask differ from r2 are
all rows upstream cannot answer in 0.5 s. I re-ran the 40 such rows across grids 2 to 4, one
subprocess each with a 30 s limit. Upstream raised MemoryError or timed out on 39 of them. It
answered the other one:

- `(?:(?(g)|(?<g>)))*|(?(g)b|a)(?R)(?:(?<g>)(?P=g))+|(?:(?<g>)(?P=g))+(?:(?<g>))*(?R)`, fullmatch
  over `ab`: upstream gives `(0,2)`, as do r2, bmask and (d). Main gives None.

On the 39 rows upstream cannot settle, the answer has to come from the rule. The (d) key is at
least as fine as main's, so (d) refuses only calls that main also refuses: it keeps every path
main keeps, and some that main wrongly cuts. What (d) still cuts wrongly comes from the reach rule
(section 6b), not from the key, and main has that gap too.

Upstream on grid 4 answered 2,598 of 12,000 rows. Main differs from it on 2 rows. r2, bmask, (d),
c1 and d+c1 each differ on 1, the same row, which main also gets wrong. The other main row is the
`(0,2)` row above. The shared row is a separate defect, below.

### 6b. A second defect, independent of captures: the reach can be full before the call

The row every variant gets wrong, minimised:
`(?:a|(?<g>))*?(?P=g)|(?<g>)a(?R)|(?R)(?P=g)x`, fullmatch over `ax`. Upstream gives `(0,2)`, and
every port variant, main included, gives None. Its captures are a distraction. The same thing
happens with none:

| `.*z|(?:|(?R)a)`, fullmatch | upstream | PCRE2 interpreter and JIT | main |
|---|---|---|---|
| `aa` | (0,2) | error: nested recursion at the same subject position | **None** |
| `a` | (0,1) | not measured | (0,1) |
| without the `.*z|` branch, `aa` | (0,2) | (0,2) | (0,2) |

The reach is measured over the whole attempt (`MatchState.cs:333-350`). The `.*z` branch fails
only after reading to the end of the text, so the reach is already full before the first call is
made. After that, no inner call can show growth, and the finite left recursion that 94ba2d5 was
built to let through is refused again. PCRE2's `last_used_ptr` rule has the same gap, but fails the
match with an error instead of cutting the path. This is in the guard's reach component, which
none of the options in section 4 touch. It needs its own design step. One lead: measure the
text reached since each open call was made, not since the attempt began. Whether that still
terminates has to be argued before anything is built.

## 7. Recommendation and build plan

Adopt (d), with the failed-call memo extended as in (a). Reject (b) and (c).

Why (d) rather than a bound or a memo alone:

- It removes the cause, which is that b3b988f keys on information no reader looks at. It is the
  exact-state rule the guard's own safety argument asks for, and it is cheaper than b3b988f.
- It is the only single change that is right on every row measured and as fast as main on every
  review shape.
- The memo is still needed, but only for one residual class, and C1 already exists (fuzzy-a2).
  The memo works well on (d)'s key: 145 calls against 1,250 on the one-group shape.

Order, each step test-first:

1. **This branch: (d).** Red first, on b3b988f: the value witness, with upstream's `(0,3)`, and
   the grid row with upstream's `(0,2)` (section 6; b3b988f passes both, main fails both).
   Timing tests at 100 ms for the one-, two- and three-group `a^n y` shapes and for `(?:()|a)(?R)|\1x` (all red on b3b988f); the three red rows and the k = 8
   and 9 test kept. Then split `Referenced` into conditional-read and backreference-read, and
   change `CallCaptures` to bits and texts. Run the oracle and a matrix wave, then a blind review.
   The lookaround-captured residual stays red as an explicit open-defect timing test.
2. **Merge gate.** Do not merge (d) into main before step 3. Without the memo, the residual shape is
   a measured regression: 14 ms on main to about 5 s. Upstream raises MemoryError there, so it is no
   loss against upstream, but it is against main.
3. **After C1 reaches main (F-A step 1e):** give the failed-call key the guard's read state (the
   (d) bits and texts, which replace C1's `MemoGroups` spans) and the reach bit per open call
   (section 4a). Then check whether C1's exclusion for a capture group inside a lookaround can be
   narrowed to a *call* inside one. fuzzy-a2's own note says the capture-list leak needs a call
   (`2026-09-27-recursion-failure-memo-design.md` section 2b: `(a)(?:(?!.(a))|.)+?b` does not leak,
   and `(a)(?:(?!.(?1))|.)+?b` does). If it can, the residual turns green (51 ms prototyped). If
   it cannot, the residual waits for the capture-list leak fix (queue item 9).
4. **Ledger 14:** replace "captures" in the key description with "what the callee reads". Record
   the survey table, and that PCRE2's interpreter and Perl refuse these shapes with an error.
5. **Separately, now:** add `.*z|(?:|(?R)a)` fullmatch `aa` (upstream `(0,2)`) and the section 6b
   grid row to OpenDefectTests as red rows. Give the reach gap its own design step. It is
   independent of (d), and neither blocks the other.

`MatchTimeout` stays the backstop throughout. A timeout never gives a wrong answer, and it is the
same policy C1's residuals already follow.

## 8. Probes, and how to rebuild them

All probes were in `.scratch`, and have been deleted.

- **Sources:** `git archive <sha> src Directory.Build.props Directory.Packages.props global.json`,
  plus `.editorconfig`, into a scratch folder. Add `TreatWarningsAsErrors=false` and
  `RunAnalyzers=false` to that copy's `Directory.Build.props`, because the prototypes do not meet
  the analyzers.
- **bmask:** in `CallCaptures.Take`, store 0 for a set group and -1 for an unset one, in place of
  its start and end.
- **(d):** add `GroupInfo.BackrefRead`, set next to `RecordRefGroup` in the backreference builder
  (`NodeCompiler.cs:1388`). In `Take`, store (length, `string.GetHashCode` of the text) for such a
  group and a set bit for the others.
- **c1:** add a `MemoKey` string to `CallCaptures`, left out of equality. At `GROUP_CALL`, build
  it from the call, the position, the captures and the open calls at or after the position (key,
  captures, reach equals current reach). Fail at once when the key is in a per-attempt set,
  cleared where `ActiveCalls` is cleared. In the `GROUP_CALL` backtrack arm, add the innermost open
  call's `MemoKey`. A call re-opened at `GROUP_RETURN` gets a new `CallCaptures` with no key, so a
  call that returned is never recorded.
- **Probes:** a file-based `probe.cs` (`#:project src/FuzzyRegex/FuzzyRegex.csproj`) that reads
  tab-separated rows (operation, pattern, subject) and prints span and time. Upstream ran one
  subprocess per timing row, and in-process with `timeout=0.5` for the grids. The grid generators
  are small random grammars over setters (`()`, `(?<g>)`, lookaround captures), readers (`\1`,
  `(?P=g)`, `(?(1)...)`), literals and `(?R)`, with a `.*z|` prefix on some patterns so the reach
  is already full when the calls happen.
- **PCRE2:** `pcre2.compile(p, jit=False)` for the interpreter, `jit=True` for JIT. **Perl:**
  `perl -e` with `qr//` and `eval`, reading `$-[0]` and `$+[0]`.
