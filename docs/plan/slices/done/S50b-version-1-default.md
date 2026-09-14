---
slice: S50b
phase: 6
title: Version 1 becomes the default behaviour - nested sets and full case-folding out of the box, Version0 kept for re-compatible patterns
delivers: []
---

# S50b - Version 1 by default

Owner decision, 2026-09-14 (spec amendment 24): this port has no `re` users to stay compatible
with, so it defaults to upstream's "new behaviour", `VERSION1`, rather than mirroring upstream's
`DEFAULT_VERSION = VERSION0`.

## What actually changes - measured, not read from the README

Run on regex 2026.9.10, CPython 3.14.6 and .NET 10 on 2026-09-14 by the orchestrator (the probe is
item 1 below, so the numbers are re-established in the slice from a committed file):

| Behaviour | V0 today | V1 | .NET `Regex` |
|---|---|---|---|
| zero-width `split` / `sub` | correct, identical to V1 | correct | correct |
| inline flag scoping and `(?-i)` turn-off | works, identical to V1 | works | works |
| nested sets and set operations `[[a-z]--[aeiou]]` | unsupported; `[` is literal inside a set | supported; an unescaped `[` inside a set is "unterminated character set" | subtraction only, `[a-z-[aeiou]]` |
| case-insensitive folding | simple (`ß` vs `SS` false) | full (`ß` vs `SS` and `ﬁ` vs `fi` true) | simple |

Upstream's README lists four differences; two of them no longer exist because V0 tracks Python
`re` 3.7+. The live differences are nested sets and full case-folding, both of which upstream
documents as the better behaviour and both of which are the reason to use this library over
`System.Text.RegularExpressions`.

## Scope

1. **The probe first.** `tools/probes/upstream-version-defaults.py` (regex V0 and V1) and a
   `.ps1`/C# twin for `System.Text.RegularExpressions` print the table above; the notes quote their
   output with versions. Any cell that differs from the table is a finding, recorded before the
   default moves.
2. **Flip the default.** `PatternCompiler.DefaultVersion` becomes `Version1`. The `Info` and
   parser paths already consult it; nothing else in the engine should need to change. Anything that
   does is a hidden dependency on V0 and is named in the notes.
3. **The oracle compares under upstream's default, explicitly.** Wave headers already carry
   `defaultVersion`. `OracleComparer` ORs that version into the port-side options for every row
   whose flags carry no version bit, so every existing wave stays valid and the comparison keeps
   asking upstream the question it was asked. Test-first: a row without a version bit must compile
   on the port side with V0 and the header's V0 must be visible in the row description. Re-run the
   default wave at three seeds and 99991 - the counts must not move.
4. **Ported upstream tests pin V0 where upstream assumed it.** Upstream's `test_regex.py` is
   written against `DEFAULT_VERSION = VERSION0`; the port's `Ported/` tree has 129 constructions
   with no version bit and 63 patterns spelling `(?V0)`/`(?V1)`. A shared helper (`Upstream.Compile`
   or the existing test factory, whichever the tree already has) passes `Version0` for every ported
   test unless the test names a version itself. The ratchet must stay at the same passing set: a
   ported test that starts failing under the pinned V0 has found a bug in the pinning, not in the
   engine. Gap tests written by this port keep the new default and are reviewed for any that
   silently relied on V0 - the reflection is cheap: run them once with the default flipped and
   read the failures.
5. **The loud edge gets a helpful error.** `[[]` and `[a[b]` are legal under V0 and .NET and fail
   under V1. The parse error for an unterminated set inside a set names `FuzzyRegexOptions.Version0`
   as the way to get the `re`/.NET reading, and says to escape `[` otherwise. Test-first.
6. **`FullCase` interaction documented and tested.** Under V1 full folding is on by default;
   `(?-f)` and the absence of `FullCase` still turn it off where upstream lets them. Tests for
   `ß`/`SS`, `ﬁ`/`fi`, Kelvin sign, and the S45 Turkic four under the new default.
7. **Docs.** README, the `FuzzyRegexOptions` XML docs (`Version0` is "compatibility with `re` and
   .NET set syntax and simple folding"; `Version1` is the default) and the API section state the
   default and the two behaviours it changes. `docs/PORTMAP.md` notes that `DEFAULT_VERSION` maps to
   a different constant here and why.
8. **What does not change.** The engine flag bits, the meaning of `(?V0)`/`(?V1)` in a pattern,
   the oracle's upstream side, and the recorded waves. No global mutable default is introduced
   (S52b's structural test would fail it): the default is a compile-time constant plus an explicit
   option.

## Verification

- Ratchet GREEN at the same passing set; default wave at three seeds and 99991 with unchanged
  counts; probe output quoted; the new tests red-first as described.
- Blind review (hunt: a ported test whose V0 pin hides a real engine change; an oracle row whose
  port-side options gained V0 while upstream's did not; a gap test that passed under V0 for the
  wrong reason), then the verifier pass re-running the probes and the wave summaries.

## Done when

- [x] Probe committed and quoted; default flipped; oracle explicit about the version it compares.
- [x] Ported tests pinned to V0 through one helper; ratchet passing set unchanged.
- [x] Helpful error for `[` inside a set under V1; `FullCase` tests under the new default.
- [x] README, XML docs, PORTMAP updated; DECISIONS entry.
- [x] Ratchet GREEN, blind review, verifier, commit.

---

## Closing notes (2026-09-15, one sitting)

**Version 1 is the default.** `PatternCompiler.DefaultVersion` is `Version1`. Suite 6,030 / 6,030
passing / 0 skipped, ratchet GREEN at baseline 5,922, oracle GREEN at seeds 7, 4242, 20260914 and
99991 with expected counts **5, 1, 1, 2 - identical to S50's**, which is the "counts must not move"
the Verification section asked for.

### What the probes actually measured, and the two cells the slice file did not have

`python tools/probes/upstream-version-defaults.py` and `pwsh -File
tools/probes/dotnet-version-defaults.ps1`, run 2026-09-14 on regex 2026.9.10 / CPython 3.14.6 /
.NET 10.0.10. Rows 1-4 of the slice's table are confirmed exactly: zero-width `split`/`sub` and
inline-flag scoping are **identical** in V0 and V1 (the README still lists them), nested sets and
full folding are the live pair, and .NET is V0-like on both. Two findings on top:

1. **A fifth live difference, in neither upstream's README nor the slice's table: a backreference to
   an OPEN group.** V0 raises "cannot refer to an open group" and V1 compiles it - `(a\1)` and
   `(?P<x>a(?P=x))` both. **.NET agrees with V1 here**, so on this one cell the new default moves
   the port *towards* `Regex`, not away. It is also the only cell in the whole suite that notices
   the ported-test pin (see control A below).
2. **`version_0` is dead in upstream and dead here.** `state->version_0` is set at `_regex.c:18489`
   and read nowhere else in the file - the zero-width behaviour it once selected went when V0 was
   brought in line with `re` 3.7+. `MatchState.Version0` mirrors it faithfully and is equally inert,
   so nothing at MATCH time depends on the version at all; every live difference is in the parser.

### The ported suite: 784 call sites, one helper, and a guard that took two reviews to get right

`tests/FuzzyRegex.Tests/Ported/Upstream.cs` mirrors `FuzzyRegex`'s surface and passes `Version0` as
the **default version, not as a flag** - `Version0` in the flags beside an inline `(?V1)` leaves both
bits set and is rejected as "VERSION0 and VERSION1 flags are mutually incompatible". It needed one
new `internal FuzzyRegex(...)` overload carrying `defaultVersion`. 129 constructor calls and 655
static calls - 784 in the 140 files that held one - were rewritten mechanically **before** the
default flipped, and the ratchet stayed at exactly 5,968 / 5,968 - that is the evidence the rewrite
was behaviour-neutral. (144 files under `Ported/` show a diff: the other four are reflow alone.)

`Conventions.PortedTestConventions.FindDirectEngineUses` enforces it. **Both halves of the final rule
were forced by a blind review reproducing a bypass**, and the sequence is worth keeping:

- listing call SHAPES (`new FuzzyRegex(`, `FuzzyRegex.X(`, `FuzzyRegex x = new(`) leaked, because
  `private static readonly FuzzyRegex[] _p = [new("a")];` matches none of them - the target-typed
  `new` names no type at all, so only the declaration can catch it;
- "names the type and does not say `Upstream.`" leaked too, because
  `[Upstream.Compile("a"), new("b")]` says `Upstream` and still compiles one pattern directly, and so
  does a direct construction with `Upstream.Compile` in its trailing comment.

The rule now is: after cutting the comment tail, a line that names the `FuzzyRegex` **type** and
either constructs, calls `FuzzyRegex.`, or is the bare receiver. Requiring the `new` also killed two
false positives the second review found - the type named inside a string literal and inside a `/* */`
tail.

### The bug the flip made live - ledger entry 22, fixed here

**Under `DEFAULT_VERSION = VERSION1` upstream contradicts itself about what `VERSION0` means.**
`compile('a', regex.V0).flags` is `U|V0` and `compile('(?V0)a').flags` is `F|U|V0`; the second folds
`ss` against `ß` and the first does not. A leading global flag makes `_compile` parse twice, and
`Info.__init__` assigns `global_flags` AFTER OR-ing `DEFAULT_FLAGS` (`_regex_core.py:4359-4361`), so
attempt 1's guess at the version leaks its implied `FULLCASE` into attempt 2, which knows better and
has `DEFAULT_FLAGS[VERSION0] == 0` to undo it with. Unreachable in upstream's shipped configuration;
reachable the moment this port's default moved. `Parsing.Info` assigns `GlobalFlags` before the
`DEFAULT_FLAGS` line. Under `Version0` that is bit-for-bit upstream, and all 1,659 compile-parity
rows are unmoved - which is the proof. Reproduction:
`python tools/probes/upstream-inline-v0-under-a-v1-default.py`.

### The error message, and why it is a second compile rather than a flag

`[[]` and `[a[b]` are legal in `re`, in `Regex` and under `Version0`, and stop compiling under the
new default. The message names `FuzzyRegexOptions.Version0` and the `\[` escape - **but only after
recompiling the failed pattern under `Version0` and finding that it works**. Three cheaper
discriminators were tried and each was killed by a review reproducing a pattern where the advice was
false: a `try`/`catch` around the nested parse missed `[a[b]` (the nested set closes; the OUTER one
runs off the end); a parse-wide "we read a nested set" flag advised `Version0` on
`[[a-z]--[aeiou]]x[`; scoping that flag to one top-level set still advised it on `[[a]--[b`. Version
0 rejects all three. The retry costs one extra parse of an already-failed pattern, cannot recurse,
and let `Info.SawNestedSet` be deleted again. **An error message that tells the user what to do is an
assertion about their input and wants the same evidence as any other assertion.**

### The oracle

`OracleWave.Load` stamps the header's `defaultVersion` onto every `OracleRow` and `OracleComparer.Run`
passes it as the compile's default version; every divergence block now prints `version=V0` or
`version=V1`. Threading it through the ROW rather than through each call site was deliberate:
`OracleComparer.Run(row)` has a dozen callers and an optional parameter defaulting to the port's own
version would be a footgun that fails silently, one row at a time. The synthetic rows tests build
default to `Version0`, which is what upstream's recorder ran under.

### Negative controls - all four re-run against the exact tree being committed

Every figure below is from the final run, after the last code change.

> **Control A, the ported-suite version pin.** In `tests/FuzzyRegex.Tests/Ported/Upstream.cs`, in the
> four-parameter `Compile`, change
> `) => new(pattern, options, matchTimeout, namedLists, Parsing.RegexFlags.Version0);` to
> `) => new(pattern, options, matchTimeout, namedLists, Parsing.RegexFlags.Version1);`. Suite run:
> **2 failures of 6,030** - `Api.GetAttrTests.Options_reports_the_inline_flag_and_the_default_version`
> and `Various.VariousParseErrorTests.Invalid_pattern_is_rejected(((·)\1+))`, the second being the
> open-group backreference V0 rejects and V1 accepts.

> **Control B, the `Info` flag-ordering fix.** In `src/FuzzyRegex/Parsing/Info.cs`, replace
> ```csharp
>         GlobalFlags = flags;
>         Flags =
>             flags
>             | RegexFlags.DefaultFlags(
>                 (flags & RegexFlags.AllVersions) != 0 ? flags & RegexFlags.AllVersions : defaultVersion
>             );
> ```
> with upstream's own ordering
> ```csharp
>         flags |= RegexFlags.DefaultFlags(
>             (flags & RegexFlags.AllVersions) != 0 ? flags & RegexFlags.AllVersions : defaultVersion
>         );
>         Flags = flags;
>         GlobalFlags = flags;
> ```
> Suite run: **9 failures of 6,030** - five in `DefaultVersionTests`, three in
> `CaseInsensitiveMatchingTests.Simple_folding_matches_where_upstream_does`, and
> `PatternPropertyTests.The_default_version_is_Version1_where_upstreams_front_end_sets_Version0`.
> The three folding rows redden because that test's own `(?V0)` prefix only works BECAUSE of this
> fix, which is the coupling working.

> **Control C, the error message's version-0 check.** In `src/FuzzyRegex/Parsing/PatternCompiler.cs`,
> change `if (!CompilesUnderVersion0(pattern, flags, namedLists))` to
> `if (CompilesUnderVersion0(pattern, flags, namedLists))`. Suite run: **12 failures of 6,030**, all
> twelve in `Gaps/Parsing/DefaultVersionTests.cs` - three `An_unescaped_bracket_...` rows and nine
> `A_set_that_is_merely_unclosed_...` rows. **No compile-parity row moves, and cannot**: the corpus
> compiles with `Corpus.DefaultVersion`, which is `Version0`, and the re-wording arm is guarded on
> `defaultVersion == RegexFlags.Version1`, so the corpus never reaches it.

> **Control D, the ported-source guard.** Not a source mutation - the honest fault is a ported test
> that bypasses the pin. Create `tests/FuzzyRegex.Tests/Ported/Various/ZzHoleTests.cs` holding
> `private static readonly FuzzyRegex[] _patterns = [new("(?i)ss")];` and a `[Test]` asserting
> `_patterns[0].FullMatch("ß").Success` is true, then run
> `dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter
> "/*/*/PortedTestConventionsTests/No_ported_test_source_compiles_outside_Upstream"`: **1 failure**,
> naming `ZzHoleTests.cs:8`. Against the FIRST version of the guard the same file passed both the
> guard and its own test, which is how the hole was found.

**No control was run against the oracle, and that is a finding rather than an omission.** The wave
compares under the RECORDER's version, which is V0, and under V0 the `Info` reordering is provably a
no-op - so no generator at any seed can see this slice's engine change. The instrument that can is
the compile-parity corpus, 1,659 rows under V0, and it is green - which is what holds the `Info`
reordering to upstream's bytecode. The corpus does NOT cover the new error text, because it
compiles under `Version0` and the re-wording arm only runs under `Version1`; control C's twelve
failures are all new tests, and that is the whole of the cover. Seeds: the four controls are suite runs and carry none. The four oracle seeds are above, and
99991 is the one the default list does not use.

### Review

**Two blind passes, six findings raised, five reproduced, five fixed; the second pass was needed and
was a first pass over unreviewed code rather than a second opinion.**

Pass 1 saw the whole diff and raised **2 findings, both reproduced and both fixed**: the nested-set
error advising `Version0` on `[[a-z]--[aeiou]]x[`, where version 0 rejects the pattern too; and the
conventions guard missing a target-typed `new(...)` inside a collection expression, demonstrated with
a ported test that compiled under `Version1` while all 31 convention tests stayed green. It also
confirmed four hunts clean with its own evidence, including re-recording the whole wave under
`DEFAULT_VERSION = VERSION1` (22/22 green) and running upstream's own `test_regex.py` under
`VERSION1` to check control A's count from the other side.

Pass 2 covered only the fixes, which pass 1 never saw, and earned its keep: **3 findings, all three
reproduced, all three fixed**. It showed the re-scoped flag still advising `Version0` on `[[a]--[b` -
one level below the case pass 1 found - which is what moved the check from a flag to the second
compile; and it showed the revised guard still letting through `[Upstream.Compile("a"), new("b")]`
and a construction excused by a comment tail, plus two false positives (the type named inside a
string literal and inside a block comment). Requiring a `new` on the line fixed all three of those
together.

**What the two passes have in common is worth carrying:** every finding was a heuristic that looked
equivalent to the exact thing and was not, and every one was killed by a reproduction rather than by
argument. The slice ends with both heuristics replaced by exact checks - compile it under version 0
and see; require a `new` on the line - and both are smaller than what they replaced.

### For the next slice

- **`.claude/skills/port-tests/SKILL.md` still shows `FuzzyRegex.Search(...)` in its worked example
  and does not mention `Ported.Upstream`. The edit was refused** - this session had no write
  permission under `.claude/skills/` - **so it is owed.** Anyone writing ported tests before it is
  fixed should read `tests/FuzzyRegex.Tests/Ported/Upstream.cs`'s own remarks instead; the
  conventions test catches the mistake either way.
- **S51 (timeouts) inherits one thing from here:** `PatternCompiler.Compile` can now parse a failed
  pattern twice. It is on the compile path, not the match path, and only for a pattern that has
  already thrown, so no match deadline is involved - but a slice adding a compile-time budget should
  know before it measures.
- The `FullCase` grid under the new default is in `Gaps/Parsing/DefaultVersionTests.cs`, checked
  cell by cell against upstream V1: the seven non-Turkic rows agree, and the five that differ are
  exactly S45's pinned divergence - the Turkic four plus `İ`'s full expansion to `i` + U+0307.
- **The V0 pin is load-bearing for only 2 tests today** (control A), because upstream's suite spells
  `(?V0)`/`(?V1)` wherever the version matters. Its value is prospective: it stops a future
  `sync-upstream` slice's new tests from silently measuring this port instead of upstream. The guard,
  not the current failure count, is what makes it hold.
