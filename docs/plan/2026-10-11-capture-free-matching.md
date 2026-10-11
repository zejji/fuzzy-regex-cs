# Capture-free matching when nobody can see the captures

Date: 2026-10-11. Status: design for the owner, nothing built. Main at a1191e65. Read-only session:
nothing was built, run or timed for this note; engine claims come from reading the code
(file:line below), and estimates are labelled.

## The idea

Every time the engine passes a capturing group it records where the group started and ended, and
pushes an undo entry so backtracking can take the record back. When the caller only wants a yes or
no (`IsMatch`) or a count, and nothing inside the pattern reads a group, that work changes no
answer. Rust's `regex-automata` backtracker skips it in exactly this case: a capture state writes
its slot only `if slot.as_usize() < slots.len()`, and an `is_match` call passes no slots
(`regex-automata/src/nfa/thompson/backtrack.rs`, master, lines 1539-1546, read 2026-10-11).

We already pass the hint in. `FuzzyRegex.Test` sets `visibleCaptures: false`
(`FuzzyRegex.cs:740-760`) and `MatchState.Init` stores it (`MatchState.cs:1449`), but nothing reads
it, and upstream never reads its own `visible_captures` either (`FuzzyRegex.cs:708-712`).

Two terms matter, because upstream's flag means the narrower one:

- **Capture list**: every span a group took during the match (`captures()` in Python). This is
  what `visibleCaptures` describes.
- **Group span**: the span a group holds at the end (`group(1)`, `\1` in a replacement). Skipping
  the work loses both, so the switch we need is "nothing reads any group", not `visibleCaptures`.

## Which public calls cannot see captures

| Call | Path | Reads group spans or lists? | Capture-free? |
|---|---|---|---|
| `IsMatch`, `IsMatchAtStart` and the other bool tests | `FuzzyRegex.Test` -> `Execute(..., visibleCaptures: false, ...)`; the result is only the status (`FuzzyRegex.cs:740-760`) | No | Yes |
| `Count` | `Iteration.Count` -> `Scan(..., visibleCaptures: false, onMatch: null)` (`Iteration.cs:115`) | No; the next start depends only on the match's span | Yes |
| `EnumerateMatches` over a span | `ValueMatch` exposes `Index` and `Length` only (`ValueMatch.cs:29`, `:32`); its state is rented with Count's arguments (`ValueMatch.cs:94`) | No | Yes |
| `Replace` with a literal template | `IsLiteralTemplate` (`Substitution.cs:107`, `:292-293`) | No | Yes |
| `Replace` with a compiled template using only `\g<0>` | items are strings and group 0, which reads `MatchPos`/`TextPos` (`Substitution.cs:316-321`) | No | Yes |
| `Replace` with `\1` etc. | `GetSubReplacement` reads `state.Groups[index - 1]` (`Substitution.cs:324-330`) | Spans, yes | No |
| `Replace` with an evaluator or `subf` format | builds a `Match` (`Substitution.cs:255-263`) | Yes | No |
| `Split` | yields every group's text after each piece (`Iteration.cs`, the `for (int g = 1; g <= regex.GroupCount; g++)` loop and `GetGroup`) | Yes, whenever the pattern has a group | No. A pattern without groups has nothing to skip |
| `Match`, `Matches`, `Enumerate` | build `Match` objects (`Iteration.cs:83`, `:307`, `:389`) | Yes | No |

`Split` passes `visibleCaptures: false` (`Iteration.cs:490`) because it does not read capture
*lists*; it still reads spans, which is the distinction above.

None of these calls can ask for a partial match: `Test` passes `partial: false`, `Count` takes no
`partial` argument (`Iteration.cs:104-107`), and `Replace` and `Split` rent with `partial: false`.

## Which pattern features make captures visible to the engine itself

If any of these is in the pattern, skipping the work can change an answer, so the skip must be off:

| Feature | Where the engine reads a group | Rule |
|---|---|---|
| Backreferences (`\1`, named, folded, reverse) | `RefGroup*` arms read `refGroup.Current` (`Matcher.cs:15437`, `:15533`, `:15611`, `:15694`, `:15939`, `:16189`) | off; these mark the group `Referenced` (`NodeCompiler.cs:1530`) |
| Group-exists conditionals `(?(1)...)` | `Matcher.cs:14966-15010` | off; `Referenced` (`NodeCompiler.cs:1289`) |
| Capture-change counter | bumped only for a `Referenced` group (`Matcher.cs:13509`); read by the repeats' zero-width progress test | safe once `Referenced` groups are excluded: the counter never moves |
| D17 empty-iteration states and the repeat memo key | keyed on tested groups' spans (`Matcher.cs:4205-4254`, `RepeatMemoKey`, `GuardList.cs:506-514`, `PackedSpan`) | off with tested groups (`PatternObject.TestedGroups`, built from `Referenced`, `Optimiser.cs:739-751`) |
| Group calls and recursion | the call memo compares the groups a call reads (`Matcher.cs:5275-5285`, `CallReadGroups`); calls save and restore captures (`Matcher.cs:4380-4446`, `:14921`, `:17941`) | off for any pattern with a group call in the first slice. The open-call guard itself is keyed on call and position only (`ActiveCallKey`, `Matcher.cs:4828`), not captures |
| Partial matching (D11 unknown captures, lookaround success dedup) | `UnknownSpan` records (`Matcher.cs:6037-6131`); `LookaroundSuccess` snapshots group spans (`Matcher.cs:5899-5903`) | never coincides: no capture-free call can be partial. Assert it |
| Fuzzy change lists | not captures; `END_FUZZY` does not read groups. BESTMATCH copies groups for its best result (`CopyGroups`), wasted but harmless | safe in principle; leave fuzzy patterns out of the first slice anyway, because the fuzzy group arms carry D11 and ledger-33 extras not reviewed here |
| `lastindex`, `lastgroup` | computed after the match from `Current` (`Matcher.cs:20690-20705`) | invisible to a bool or count caller |
| Verbs `(*PRUNE)`, `(*SKIP)`, `\K` | read positions and `MatchPos`, not groups | safe |
| Failure memo (Option A, and B if built) | keys on position; switched off by the same `Referenced` features | safe |

So the condition is: the call reads no group, `TestedGroups` is empty, the pattern has no group
call, and (first slice) the pattern is not fuzzy. `ReadsCapturesWhileMatching`
(`PatternObject.cs:367`, `:1150-1163`) is a subset of the `Referenced` test.

## Exactly what would be skipped

Forward arm, `START_GROUP`/`END_GROUP` (`Matcher.cs:13471-13537`). Of the pair, the half met first
parks the position, the other records the capture:

- park half: `Sstack.PushSize` (8 bytes), `Bstack.PushBool` + `PushUInt8` (2 bytes);
- capture half: `Sstack.PopSize` (8 bytes), `PushGroupStateData` (3 x 8 + 2 x 4 = 32 bytes,
  `Matcher.cs:7127-7134`), `PushBool` + `PushUInt8` (2 bytes), a `GroupInfoAt(...).Referenced`
  lookup, `SaveCapture` (a bounds check, an occasional `Array.Resize`, an 8-byte write,
  `MatchState.cs:2382-2402`), and the `Current` write.

Backtrack arm (`Matcher.cs:18061-18101`), per half: pop the bool; for the capture half pop 32
bytes, restore `CaptureChange` and `Current`, push the parked position back, `UnsaveCapture`; for
the park half drop 8 bytes.

Per pass through a group and back: 4 dispatches, 36 bytes pushed and popped on the backtrack
stack, 8 bytes pushed and popped twice on the saved stack, one capture-list append and removal.

Also skippable once groups are never written: `PushCaptures`/`PopCaptures` (16 bytes per group)
at atomic groups (`Matcher.cs:13152`), conditionals (`:13246`) and lookarounds whose body has
groups (`:15362`). Not needed for the two target rows, which have none.

The change in the arms: `if (state.SkipsCaptures) { node = node.Next1.Node!; break; }` at the top
of the forward arm, with nothing pushed, so the backtrack arm is never reached for that group. The
flag is set in `MatchState.Init` from the call and the pattern. A cheaper alternative removes the
dispatch too: compile a second node graph without group nodes, used only by capture-free calls.

## Expected gain (estimates)

Both rows are `IsMatch` with a group inside the repeat.

- **PathologicalWordsToEnd** (`(\w+\s?)+$`): 5.38 million steps per call over about 400,800
  repeat iterations (one `BODY_START` backtrack each, `.scratch/perf-profile/notes.md`), so about
  13 steps per iteration, of which up to 4 are group arms. Group arms are cheap next to character
  tests and guard inserts. **Estimate: 10 to 20 percent** of today's 96.5 ms. If the TryMatch
  failure fix lands first (5.38M to 3.78M steps), the share rises slightly.
- **PathologicalNestedRepeat** (`(a+)+c`): about n^2 group passes per call (20,000 to 40,000 at
  n = 200) against 400,604 steps and 1,472,802 guard checks, which dominate. **Estimate: 5 to 10
  percent.**
- If failure memo Option B lands (`docs/plan/2026-10-11-memo-option-b.md`), both rows drop to
  around a millisecond and this change no longer matters for the gate.
- No other gate row benefits: `ValidateEmails`, `CaseFoldAccented`, `RedactDigits`, `ClassScan`,
  `CaseFoldedScan` and the fuzzy `IsMatch` rows have no capturing group
  (`bench/FuzzyRegex.Benchmarks/WorkloadBenchmarks.cs`, `ManyInputsBenchmarks.cs:67-80`). The gain
  is for user patterns like `^(\w+)@(\w+)\.com$` tested with `IsMatch` or counted with `Count`.

## Divergence from upstream

The runtime flag is a small divergence: one guarded early exit in one arm, one flag in `Init`, a
`sync-divergence:` comment at the arm and one ledger row. The group arms rarely change upstream.
The second node graph is a major structural divergence (a second compile, a second set of repeat
indexes and guards, every compile-time analysis run twice) and would need the owner's case-by-case
decision with a large measured gain, which these estimates do not promise.

## Test plan

1. **Oracle of equivalence, inside our own engine:** for generated patterns and subjects, `IsMatch`
   equals `Match().Success`, `Count` equals `Matches().Count`, span `EnumerateMatches` equals
   `Matches` spans, and literal-template `Replace` equals `Replace` with an evaluator that returns
   the same literal (which forces captures on). Bias the generator to groups inside repeats,
   groups in lookarounds and atomic groups, nested and empty-matching groups, reverse, `\K`,
   verbs, and the excluded features (to prove the flag stays off).
2. **Witness tests** for each exclusion, red when the exclusion is deleted: a backreference
   (`(a)(?:\1|b)*c`), a group-exists conditional (`(?:(a)|b)+(?(1)c|b)` over `abbb`, (1, 4) in
   regex 2026.9.10), a group call reading a group.
3. **Differential against main** with `tools/probes/port-probe.cs` (tens of thousands of rows, never
   `--no-build`), comparing `IsMatch`, `Count` and literal `Replace` results. Zero differences.
4. Oracle at three seeds, ratchet, AOT tests and smoke, `AllocationTests` (still 0 bytes warm).
5. **Before building:** a Debug `WorkCounter` count of group-arm steps on the two rows, to replace
   the estimates above with counts.

## Options

| Option | Pros | Cons |
|---|---|---|
| Runtime flag in the group arms (Rust's approach) | Small, local, cheap to sync; no compile-time change; easy to switch off | Still dispatches the two group nodes; gain on the target rows is an estimated 5 to 20 percent |
| Second capture-free node graph | Also removes the dispatch | Major structural divergence; double compile cost and memory; every analysis must agree across two graphs |
| No change | Zero risk | Leaves known waste in `IsMatch` and `Count` for patterns with groups |

## Recommendation

Do the runtime flag, after failure memo Option B and not instead of it, and only after the
`WorkCounter` count confirms that group arms are a real share of the steps. It is a general win for
`IsMatch` and `Count` on patterns with groups, cheap to keep in sync, and Rust's engine shows the
shape is ordinary. Do not build the second node graph. If the count shows group arms under about 5
percent of the work, record it as a negative result in OPTIMISATION-NOTES instead.
