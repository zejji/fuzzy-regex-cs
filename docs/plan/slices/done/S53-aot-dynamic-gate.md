---
slice: S53
phase: 6
title: The Native AOT dynamic gate - a published consumer that asserts real matches, in CI
delivers: []
---

# S53 - The AOT gate's dynamic half

`<IsAotCompatible>true</IsAotCompatible>` has enforced the static half since Phase 2 (amendment 12).
Static analysis cannot see a runtime-only failure, so the dynamic half is owed: a consumer published
with `PublishAot=true` that runs real matches.

## Scope

- **First, the whole suite natively (owner question 2026-09-16).** TUnit is source-generated and
  documents Native AOT as `<PublishAot>true</PublishAot>` plus `dotnet publish -c Release`
  (tunit.dev, engine modes; MSTest 4.4 shipped the same on 3 Sept 2026 for `net10.0`, so no
  .NET 11 is needed). Publish `tests/FuzzyRegex.Tests` with `-r win-x64` and `-r linux-x64` and run
  the produced executable: all 6,144 tests under the same trimming and reflection constraints a
  consumer's AOT app enforces, which is stronger evidence than any sample. Expected friction, and
  how it is handled: AwesomeAssertions is a FluentAssertions fork with reflection inside and is not
  marked AOT-compatible, so trim warnings will come from it. Handle them in the test project ONLY
  (replace the six `BeEquivalentTo` uses, or a suppression scoped to the test csproj with the
  warning code and reason recorded); `src/` keeps zero suppressions. If AwesomeAssertions cannot
  run natively at all, record the finding and the trade-off (TUnit's own assertions are AOT-clean)
  in STATE.md for the owner rather than switching libraries inside this slice. The oracle test
  project is NOT published (it parses JSON reflectively by design). Wire the native run into CI as
  the gate on both platforms.
- **Then `samples/FuzzyRegex.AotSmoke/`**, reduced to what only a consumer can prove: a project
  reference from a separate app, binary size and startup time for Phase 7's baseline, and a
  handful of feature cases as a smoke check. Original scope, kept for reference:
- **`samples/FuzzyRegex.AotSmoke/`**: a console app referencing `src/FuzzyRegex` by project, with
  `PublishAot=true`, exercising one case from every feature area: literals, classes, Unicode
  properties (which pull the transliterated tables), full case folding, named lists, lookaround,
  recursion, conditionals, verbs, partial, POSIX, fuzzy with counts and changes, `(?e)`, `(?b)`,
  substitution with templates and callbacks, `Split`, `Matches`, and a `MatchTimeout` that fires.
  Each case asserts the exact answer and the app exits non-zero on the first miss, printing which.
- **Publish and run in CI** on `windows-latest` and `ubuntu-latest` (the ILCompiler is
  platform-specific, so one platform is not evidence for the other), as a job in `ci.yml` that
  fails the build. Record binary size and startup time in the closing notes as Phase 7's baseline.
- **Trim warnings are errors** already (`TreatWarningsAsErrors`); confirm the published app has no
  `IL2xxx`/`IL3xxx` suppressions anywhere in `src/`, and that `Directory.Build.props` does not
  relax them for the sample.
- If the publish fails on a runtime-only path, that is the slice's finding: fix it in `src/`
  test-first, never by a `[DynamicDependency]` band-aid in the sample.

## Verification

- Both CI jobs green with the published binary's assertions; size and startup quoted.

## Done when

- [x] Sample committed and asserting every feature area; CI jobs on two platforms.
- [x] Zero suppressions; any runtime-only failure fixed in `src/`.
- [x] Ratchet GREEN, blind review (hunt: an assertion that would pass on a wrong answer; a feature
      area whose table is only reached under a flag the sample never sets), commit.

---

# Closing notes (one sitting, 2026-09-16)

**THE LIBRARY NEEDED NO CHANGES. `src/` IS UNTOUCHED BY THIS SLICE** - `git diff --stat -- src/`
is empty - and that is the headline, not a footnote. The static half has been on since Phase 2,
and the dynamic half found nothing in `src/FuzzyRegex` to fix: it produces zero trim and zero AOT
warnings of its own, and every feature area answers correctly in a published native binary. Every
problem this slice fixed was in the TEST project, in the assertion library, or in the build.

## What landed

- **`tools/run-aot-tests.ps1`** publishes `tests/FuzzyRegex.Tests` with `PublishAot=true` and runs
  the produced executable. **AOT GREEN on win-x64: 6,155 total, 6,152 passed, 0 failed, 3 skipped,
  38.2 MB.** The native test run itself takes 10-19 s; a full ILCompiler publish plus the run is
  about 90 s to two minutes.
- **`samples/FuzzyRegex.AotSmoke/`** plus **`tools/run-aot-smoke.ps1`**: a consumer console app,
  project reference, nothing rooted, 29 cases over every feature area. **AOT SMOKE GREEN: 29 of 29,
  0 misses.**
- **`native-aot` job in `ci.yml`** on `windows-latest` and `ubuntu-latest`, both steps failing the
  build on a red gate. macOS is deliberately out: it is the third leg for the managed suite, and a
  third native compile doubles the job for a platform no Phase 7 measurement targets.
- **`tools/probes/aot-smoke-expectations.py`** - upstream's answer to all 28 parity cases, so no
  expected value in the sample comes from this port's own output.
- **`tools/probes/aot-smoke-slow-patterns.cs`** - how the timeout case's pattern was chosen. Needs
  `-c Release`: a file-based app defaults to Debug, where the rows are several times slower.
- Two new tests: `AotAssertionConventionTests` and
  `ThreadSafetyTests.The_library_assembly_this_file_scans_has_not_been_trimmed_away`. Baseline
  6,045 to 6,047, no removals.

## Phase 7's baseline

From `tools/run-aot-smoke.ps1`, win-x64, 2026-09-16. **The consumer binary, not the test binary**:
the test publish roots three assemblies and its 38.2 MB says nothing about what a user ships.

**The binary size is the solid number; the timings are wall clock and must be read as ranges.**
Seven warm observations were taken across this sitting and the verifier's, on a machine that was
building throughout, and they spread by a factor of two. Phase 7 should re-measure on a quiet
machine rather than treat any single figure below as a target.

| | |
|---|---|
| Binary | **6,972,928 bytes (6.65 MB)**, trimmed, nothing rooted. Byte-exact on re-publish - the verifier confirmed the same count independently. |
| To `Main` | **28-54 ms** (runtime init, before a line of the app runs; 7 observations) |
| To the first answer | **30-57 ms** - the library's own first compile and match is the gap, and that is consistently **1-3 ms** |
| All 29 cases | **83-110 ms**, of which the timeout case spends 50 ms on purpose |

**Quote the WARM run and nothing else.** The first execution of a freshly written binary reported
**552-566 ms to `Main`** against 28-54 warm - image load plus an antivirus scan of bytes the
operating system has not seen before, confirmed by running the same unchanged file three times
(28.5, 30.9, 27.9 ms). The script now runs the binary twice and labels the second. A Phase 7 slice
comparing against a cold number would be chasing Defender. The verifier added the sharp edge: an
incremental re-publish that rewrites byte-identical content does NOT reproduce the penalty (40.2 ms
to `Main`), so only genuinely new bytes measure it.

## The five things that were actually broken, all outside `src/`

1. **`BeEquivalentTo` cannot run under Native AOT.** Its equivalency engine reaches
   `MethodInfo.MakeGenericMethod()`. First published run: **1,547 of 6,153 tests failed**, every
   one at a `BeEquivalentTo` call, 1,543 of them from the one data-driven compile-parity test.
   Twelve call sites now use `Equivalence`, which renders to sorted lines and compares with
   `Should().Equal(...)`. The slice's other option - a csproj suppression - would not have helped:
   these are run-time failures, not warnings.
2. **The object-graph walk cannot run under Native AOT, and cannot be made to.** S52b's three
   thread-safety rules read BCL private fields such as `List<T>._items`. By default the walk
   returned **one slot** and all three subset assertions passed over nothing; with
   `IlcGenerateCompleteTypeMetadata=true` it threw `NotSupportedException: This object cannot be
   invoked because no code was generated for it: '...List`1[...FuzzyRegex]._items'` and cost
   **5.3 MB** (38.2 to 43.5 MB). Skipped in a native binary only, via
   `SkipWhereTheObjectGraphWalkCannotRun`, and skipped rather than weakened: rewriting the walk to
   enumerate through `IEnumerable` would also stop it seeing a list's `_size` and `_version` move.
   They run under the JIT on all three operating systems.
3. **Every assertion failure in the published binary was reported as the wrong error** - twice
   over. First `CultureNotFoundException: cs is an invalid culture identifier`, because
   Microsoft.Testing.Platform's Czech satellite assemblies end up inside the native binary and
   AwesomeAssertions walks the assembly list building `AssemblyName`s under
   `InvariantGlobalization=true`; fixed with `SatelliteResourceLanguages=en`. Then
   `MissingMethodException: No parameterless constructor defined for type
   'TUnit.Assertions.Exceptions.AssertionException'`, because AwesomeAssertions constructs the host
   framework's exception reflectively; fixed by rooting `TUnit.Assertions`. Both mattered: a gate
   that goes red on a substituted error is worse than useless for diagnosing one.
4. **A `Lazy<>`'s `IsValueCreated` cannot be read reflectively** in a native binary -
   `field.FieldType.GetProperty("IsValueCreated")` returns null, and `StaticTableSnapshot`'s `!`
   threw. The value was discarded by every caller anyway (all three wrapped the snapshot in
   `NonLazy`, which stripped exactly those entries), so the fix deleted the reflection, the entry
   and `NonLazy` together.
5. **A wedged MSBuild node hung two publishes for 600 s each**, with no `ilc.exe` ever starting and
   an ordinary `dotnet build` of the same project hanging beside them; `dotnet build-server
   shutdown` hung too. The identical build with `-nodeReuse:false -p:UseSharedCompilation=false`
   took 31 s. Both AOT scripts now pass those flags. **This is not fixed in
   `tools/check-ratchet.ps1`, which the driver runs on every slice and which hung the same way** -
   carried to STATE.md rather than changed unreviewed inside this slice.

## Three tests that were passing over nothing, and the guards that now stop that

The AOT run exposed a class of defect that has nothing to do with AOT: **a reflection scan that
finds nothing makes a subset assertion pass.** All three of S52b's graph rules and the ported-test
conventions scan had that shape. New floors, each measured rather than guessed:

- `slots > 1000` and `mutable > 30` in `MutableFieldsInThePatternGraph`, which S53 extracted so the
  two rules that had a copy each share one guard. The allowlist names 40 and the JIT walk finds
  exactly those 40.
- `_library.GetTypes() > 200` and `LibraryStaticFields() > 300` (measured: **281 types, 465
  declared static fields**).
- `found > 700` ported test methods. **Measured 892 methods across 204 types - not the 1,967 the
  status board reports**, which counts one row per `[Arguments]` set. The first floor was written at
  1,500 from that misreading and failed immediately, which is the guard earning its place before it
  was even committed.
- `sources > 50` in the convention test (measured 214 files).

## Review

**First pass: 4 findings raised, 4 reproduced, 4 fixed, no false positives.** An unusually good
hit rate, and the first one was serious.

1. **The sample did not compile in CI's own build step.** `dotnet build --configuration Release`
   failed with two `IDE0055` errors on an interpolation alignment specifier that CSharpier had just
   written as `{name, -14}` - with a space, which IDE0055 rejects. The two formatting gates
   contradict each other on that construct, so the line could not satisfy both. **Nothing the slice
   ran would have caught it**: `check-ratchet.ps1` builds only the test project, and
   `csharpier check` called the line clean. `Mark` now uses `PadRight`/`PadLeft` and concatenation.
   The lesson is a step, not a comment: **run `dotnet build` over the whole solution before
   committing a slice that adds a project.**
2. **The app's own "did I run natively?" line claimed a Native AOT run under `dotnet run`** - the
   exact false evidence the check was added to prevent. `<PublishAot>true</PublishAot>` in the
   csproj makes the SDK write `IsDynamicCodeSupported: false` into the ORDINARY build's
   runtimeconfig. Fixed at the root by moving the property to the publish command line, as the test
   project already did, which makes `RuntimeFeature.IsDynamicCodeSupported` honest again. (An
   intermediate fix using `Assembly.Location` was abandoned: it tripped `IL3000`, and the slice's
   own rule is that the sample carries no IL suppression.)
3. **The `BeEquivalentTo` ban was evadable**: the needle `.BeEquivalentTo(` does not match
   `.BeEquivalentTo<string>(`, which is legal C# reaching the same engine. Needle is now the member
   name alone.
4. **The ban fired on prose**, so documenting the rule failed the build - which had already cost
   two rounds during the slice. Comment lines are now skipped by `IsProse`.

Both 3 and 4 were proven by a throwaway probe file carrying the generic call on one line and a
comment mention on another: the test reported `ZzS53ReviewProbe.cs(9)` and said nothing about the
comment. Probe deleted; the test is green.

**Second pass over the fix delta (the four fixes above are code no reviewer had seen): 2 findings,
2 reproduced, 2 fixed.** Both about evidence rather than behaviour, and both fair.

1. **The slow-pattern probe's documented command did not reproduce its own recorded numbers** - it
   omitted `-c Release`, and a file-based app defaults to Debug, where the last two rows are
   24,312 ms and 103,251 ms rather than ~2,500 and ~12,000. Command and note corrected.
2. **`len=33` is 32 a's plus a 'b', not 33 a's**, and the smoke app was asserting 33 a's - a
   subject one character longer than the one measured. The app now uses the measured string exactly.

The second pass also independently confirmed what it was asked to check: the publish output is a
native exe with no managed dll or runtimeconfig; `EnableAotAnalyzer`, `EnableTrimAnalyzer`,
`EnableSingleFileAnalyzer` and `PublishTrimmed` are all still `true` when `PublishAot` comes from
the command line; and `IsProse`'s one blind spot (a call after a leading block comment) is
unwritable, because both formatting gates reject every spelling of it.

## Verifier

A fresh Opus subagent, briefed with the commit-ready tree and nothing else, re-ran twelve claims:
**eleven CONFIRMED, one DIFFERENT, three historical COULD NOT RUN.**

CONFIRMED: `src/` untouched; zero IL warnings from the publish; the AOT suite gate (6,155 / 6,152 /
0 / 3, 38.2 MB, run twice); the smoke gate (29 cases, 0 misses, **6,972,928 bytes byte-exact**,
warm 28.0 / 30.1 / 82.9 ms against the claimed 29.7 / 31.5 / 84.2, all within 6%); cold-versus-warm
(**566.5 ms** to `Main` against the claimed 564.8, then 28.0 / 28.8 / 30.7 warm); the ratchet; the
whole-solution build and CSharpier; **all 28 upstream expectation rows**; every floor's measured
count (281 types, 465 static fields, 892 ported methods across 204 types, 214 sources, 40 allowlist
entries) with every floor below its value; and the three skips being exactly the walk rules, which
the JIT run reports as `skipped: 0`.

**DIFFERENT, and the claim is now a range rather than a digit.** The verifier got **3,280 ms** for
the slow-pattern probe's `(a|aa)+$` row against the 2,531 ms first recorded - 30% out, and outside
the "~10%" the header then claimed. Five observations now exist (2,402 / 2,531 / 2,589 / 2,698 /
3,280) and the probe and the smoke app both quote **2,400-3,300 ms** and "a margin of at least
48x". The row-6 figure was also re-measured at 11,689 ms against 13,143, inside its own recorded
spread. Nothing about the choice of pattern changes; a wall-clock number on a loaded machine was
being quoted to four digits, which was the real defect.

**Two reproduction caveats the verifier found, both worth more than the claims they qualify:**

- **The "zero IL warnings from `src/`" check only works from a cleared `obj`.** An incremental
  re-publish does not re-emit trim warnings at all, so a green-looking log proves nothing unless
  `obj/Release/net10.0/<rid>` was deleted first. The verifier cleared it and got a real
  `Generating native code` pass with no warnings of any kind.
- **The cold-startup penalty needs genuinely NEW bytes.** An incremental re-publish that rewrites
  byte-identical content reported 40.2 ms to `Main`, not 566 ms. The 500-odd milliseconds is the
  operating system meeting a file it has not seen before, which is exactly why it must not be
  quoted as this library's startup cost.

COULD NOT RUN, all three anticipated and all three historical - the code that produced them has
since been fixed, and reproducing them would mean reverting the commit-ready tree: the 1,547-of-6,153
first published run; the `IlcGenerateCompleteTypeMetadata` experiment; and the MSBuild node hang,
which is a transient machine state that did not recur.

## No negative control, and why

This slice ran no oracle wave and no negative control, and neither is owed: `src/` is untouched, so
VERIFICATION.md rule 7 ("any slice that touches the engine") does not apply. The gate's own
equivalent of a control is the record above - the gate was **red 1,549 tests** before the fixes and
the three vacuous tests were caught failing rather than passing. A gate nobody has seen fail is not
evidence, and this one has been seen to fail in six distinct ways.

## What the next slice should know

- **`tools/check-ratchet.ps1` can hang on a wedged MSBuild node.** It is the driver's own success
  test. The workaround is `$env:MSBUILDDISABLENODEREUSE='1'` plus
  `$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'`, or `-nodeReuse:false -p:UseSharedCompilation=false`.
  Deciding whether that belongs in the script is an owner call, not a mid-slice edit.
- **Run `dotnet build` on the SOLUTION, not just the test project**, before committing. Finding 1
  above reached a green ratchet with a project that did not compile.
- **`\u` escapes do not survive this harness's editing tools** - they are silently rewritten to the
  character they denote. `Equivalence._unitSeparator` is `(char)0x1F` for that reason, and the
  reason is in its own remarks. Anything needing an escape in source needs checking with `cat -A`.
- **CSharpier and IDE0055 disagree about interpolation alignment specifiers.** Do not write
  `{x,-14}` in this repo; pad explicitly.
- S53b is next, then S54, which will consume the baseline table above.
