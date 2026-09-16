# S53b sittings

Per-sitting record. The slice file stays its spec; this is what each sitting did.

## Sitting 1 - 2026-09-16

**All four scope items and item 5's records are in the tree and green.** Ratchet GREEN 6226 / 6226
/ 0, baseline 6047 to 6118. Oracle GREEN at all three default seeds (7, 4242, 20260916) over every
generator, with the two widened generators in it. Solution builds in Release, 0 warnings.

**Not done: the independent verifier had not reported when the deadline landed.** It was dispatched
at 15:31 with the eleven claims below and is the only outstanding step. This commit is therefore a
CHECKPOINT: the slice file stays in `docs/plan/slices/`.

### What landed

1. **Lazy enumeration.** `FuzzyRegex.EnumerateMatches(input, beginning, length, overlapped,
   partial, timeout, cancellationToken)` and `EnumerateSplits(input, maxSplits, timeout,
   cancellationToken)`, plus static conveniences. `Engine/Iteration.Enumerate` is a `yield` loop
   over a new `Iteration.Step`, which is `Next`'s body generalised - so `Match.NextMatch` and the
   lazy walk are now one method, and `Next` is a four-line wrapper.
   - **`EnumerateSplits` takes NO `beginning`/`length`, against the slice file's sketch.** `Split`
     has none because upstream has none: `pattern_split`'s kwlist is `string, maxsplit, concurrent,
     timeout` (`_regex.c:22235`). A slice on the lazy twin that the eager one lacks would break the
     one property the item exists for.
   - **One state per STEP, not per walk**, and the reason is disposal, not fidelity: a state owns
     rented buffers and one held across a `yield return` is one an abandoned `foreach` never
     returns. Two consequences documented rather than hidden - the per-match pass over the subject
     (`OPTIMISATION-NOTES.md`, two rows) and the per-step rather than per-walk `timeout`
     (`DIVERGENCES.md`, API shape).
   - Argument validation is eager: the null check, `LimitsFor` and `Limits` run in the
     non-iterator body, so a bad timeout is reported from the call and not from the first
     `MoveNext`.
2. **`Ascii` (0x80), `Unicode` (0x20), `Word` (0x800) on `FuzzyRegexOptions`.** `_unexposedFlags`
   is derived from the enum, so it shrank by itself; `LOCALE`, `DEBUG` and `TEMPLATE` stay hidden.
   - **Ten assertions across five files moved, and every one moved TOWARDS upstream.**
     `new FuzzyRegex("a").Options` now reports `Unicode`, because `_main._compile` ORs it into
     every text pattern; the ported `test_getattr` had been recording that as a divergence in its
     own comment and now asserts upstream's number, `0x2022`.
   - `ApiSurfaceTests.Options_hides_the_upstream_flags_this_port_does_not_expose` had nothing left
     to hide, since no plain compile sets LOCALE/DEBUG/TEMPLATE. Rewritten to compile `(?L)a` and
     require the LOCALE bit masked off - measured, `regex.compile('(?L)a').flags` is `0x2004`.
3. **`GroupCollection : IReadOnlyDictionary<string, Group>`.**
   - **The slice file's description of .NET was wrong and the probe says so.**
     `tools/probes/dotnet-groupcollection-dictionary.ps1` on .NET 10.0.10: `Keys` for
     `(?<a>a)(b)(?<c>c)?` is `[0, 1, a, c]` and `Count` is 4 - TOTAL, not "named groups only". So
     `Keys` is exactly `FuzzyRegex.GroupNames`, and upstream's `groupdict` is a filter over it.
   - One `NumberForKey` serves the string indexer, `ContainsKey` and `TryGetValue`, because a key
     `Keys` yields that `ContainsKey` denies is a broken dictionary. The numeric spelling is a
     ROUND TRIP through `GroupNameFromNumber`, so `"2"` resolves and `"1"` does not on a pattern
     whose group 1 is named - which is the built-in's answer too.
   - **It is a source break, and eleven call sites in this repo proved it.** Two `IEnumerable<T>`
     faces make `Groups.Select(...)`/`Groups.Skip(...)` fail to infer (CS0411), exactly as on the
     built-in collection since .NET 5. `Groups.Values` is the fix and is the documented answer.
4. **`beginning`/`length` on `Replace` and `ReplaceFormat`**, all eight overloads plus the three
   statics. `Substitution.Subx` takes the pair and clamps it with `ClampIndex` where upstream calls
   `get_limits` (`:21791`) - **before** the too-short shortcut, which therefore reads the clamped
   slice. Everything else was already right: `lastPos` starts from `state.TextLength` and the
   trailing segment ends at `input.Length`, both of which are the WHOLE subject, which is what
   keeps the text outside the slice.
   - The `out int replacements` overloads keep `replacements` in place and take the slice after it;
     every other overload takes it after `count`. An `out` parameter that moves is a source break
     for no gain.
   - **Upstream's inverted slice is unspellable here**, because any negative length means "the rest
     of the subject". `(3, 0)` is the empty slice a caller wants and gives upstream's answer.

### Oracle and instrument changes

- `record-oracle.py`: `pos`/`endpos` are now legal on `sub` and `subf` (`_SLICEABLE`), passed to
  `subn`/`subfn` in CODEPOINTS while the recorded pair stays UTF-16; `_generate_substitution` draws
  a slice on `SUB_SLICED_PROBABILITY = 0.25` of its ordinary rows. Docstring figures re-measured
  from scratch (158/370/72/250/247 plus 131 sliced, 96 past the front) with the pre-slice figures
  kept beside them so the RNG shift is visible rather than mysterious.
- `record-oracle.py`: the `boundaries` generator asks half its flagged rows through a **flags
  integer** instead of the inline prefix (`BOUNDARY_FLAG_VALUES`), keyed on the row index so the
  rng stream is untouched and only the SPELLING moves. That is the one path the compile-parity
  corpus cannot check for `WORD`, upstream's own suite never passing it as a flag.
- `OracleComparer.Run` gained a `lazy` flag and hoisted the `beginning`/`length` reading above the
  operation switch. `OracleWaveTests.The_lazy_walks_answer_exactly_what_the_eager_ones_do` runs
  every `finditer`/`finditer-overlapped`/`split` row of a wave both ways and requires the rendered
  answers to match.

### Negative controls - how to re-run them

Both were run against the code and generators being committed, after the last engine change.

> **Control A, `sub-keeps-only-the-slice`**: in `src/FuzzyRegex/Engine/Substitution.cs`, the block
>
> ```csharp
>         // The segment following the last match.
>         int endPos = state.Reverse ? 0 : input.Length;
>         if (lastPos != endPos)
>         {
>             joined.Add(state.Reverse ? input[..lastPos] : input[lastPos..]);
>         }
> ```
>
> becomes
>
> ```csharp
>         // The segment following the last match.
>         int endPos = state.Reverse ? start : end;
>         if (lastPos != endPos)
>         {
>             joined.Add(state.Reverse ? input[start..lastPos] : input[lastPos..end]);
>         }
> ```
>
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator substitution -Count 600 -Seeds 7`.
> Result: 563 agree, **37 diverge** of 600. Re-run at seed 55: **23 diverge** of 600.
> Unbroken, the same waves are 0 of 600 at both seeds.

> **Control B, `lazy-walk-forgets-overlapped`**: in `src/FuzzyRegex/Engine/Iteration.cs`, inside
> `Enumerate`'s `while` loop, the `Step(...)` call's fifth argument `overlapped` becomes the
> literal `false`.
> Wave: `pwsh -File tools/run-oracle.ps1 -Generator iteration -Count 600 -Seeds 7`.
> Result: the ordinary comparison stays **0 diverge of 600** - the eager answer is untouched - and
> `The_lazy_walks_answer_exactly_what_the_eager_ones_do` fails with **28 of 600** iteration rows
> disagreeing. Re-run at seed 55: **36 of 600**. Unbroken, it passes at both.

**There is deliberately no control for the `boundaries` flags-integer widening.** The flags integer
and the inline prefix reach the same compiler through `(int)options` with no second code path, so
any break would fire on both spellings and the control would measure nothing. The evidence for that
item is bytecode identity (`Gaps/Api/EncodingAndWordOptionTests`) plus the measured upstream
answers, not a control.

### Review

**Blind pass over the whole diff: 0 findings.** The reviewer reported "no reproducible findings"
after 20,850 lazy-vs-eager comparisons, 69,168 `sub`-with-a-slice rows against upstream, a full
`IReadOnlyDictionary` contract sweep over 13 patterns including branch reset and duplicate names,
and the six `boundaries` prefixes checked both spellings on both sides. **No second pass was needed
because no finding was fixed**, so nothing went unreviewed.

Two things the reviewer surfaced that are not findings and are worth carrying:

- **The C comment at `_regex.c:22091` is wrong.** It says `text_length` "is truncated to
  `slice_end`"; `state_init` sets `state->text_length = str_info->length` (`:18439`), the whole
  subject. This port follows the code, not the comment, which is what makes the text outside a
  slice survive.
- **A pre-existing `Options` disagreement, not introduced here**: with upstream's
  `DEFAULT_VERSION` set to `VERSION1`, `regex.compile('(?V0)a').flags` is `0x6020` against this
  port's `0x2020` - upstream reports `FULLCASE` for an inline `(?V0)` and not for the `V0` flag.
  The `FullCase` bit was already exposed before this slice, so the old mask gives the same answer.
  Carried to STATE.md.

### Left for the next sitting

1. **The independent verifier** (spec amendment 16 limb (d)). Dispatched at 15:31 with eleven
   claims - the three probes' outputs, the four upstream flag/findall measurements, the recorder's
   seven row statistics, the ratchet, the full three-seed oracle, and both negative controls with
   their four numbers. It had not reported when the deadline landed. Re-dispatch it against the
   committed tree; the brief is reproducible from this file.
2. Then move the slice file to `done/`, copy the closing notes into it, and tick its five boxes.

**Correction, read this before the section above.** The verifier DID report before the sitting-1
commit was made, and the commit message records what this file does not: it confirmed ten of the
eleven claims, both negative controls at both seeds included, and only the full three-seed oracle
was out of reach of its deadline. This file was written earlier in the sitting and was left stale.

## Sitting 2 - 2026-09-16

**One claim, then the close.** No code changed; no test changed; no instrument changed.

1. **Ratchet on the closing tree** (which had moved on from the checkpoint - the public API freeze,
   `f3c1135`, and the planning commits after it): GREEN, 6226 / 6226 / 0, 6118 distinct ids against
   a baseline of 6118. No baseline update was needed.
2. **The outstanding verifier claim.** A fresh Opus verifier, briefed with nothing but the committed
   tree and forbidden to pass `-Seed`, `-Seeds`, `-Generator`, `-Count` or `-SkipRecord`, ran
   `pwsh -File tools/run-oracle.ps1` end to end and reported **CONFIRMED**:

   ```
   ===== seed 7 =====        agree 6342  unsupported 0  expected 32  timeout 2  resource 4  diverge 0  of 6380 rows
   ===== seed 4242 =====     agree 6347  unsupported 0  expected 28  timeout 0  resource 5  diverge 0  of 6380 rows
   ===== seed 20260916 ===== agree 6354  unsupported 0  expected 21  timeout 0  resource 5  diverge 0  of 6380 rows
   Oracle: GREEN - no row diverged from upstream, at all 3 seeds.
   ```

   All eleven claims are now CONFIRMED across the two sittings, so nothing was removed or weakened.
3. Slice file moved to `done/` with the closing notes and its five boxes ticked; STATE.md rewritten.

**One process lesson worth carrying.** Sitting 1 left two records that disagreed - this file said
the verifier never reported, the commit message said it confirmed ten of eleven - and sitting 2 had
to read both to find out which was true. The commit message is written last, so it is the one to
trust; but the cheap fix is to update the notes file in the same edit that writes the message.
