# S59 sittings

## Sitting 1, 2026-09-19 (checkpoint, allowance window closing at 93%)

### What landed, all green

`src/FuzzyRegex/PatternCache.cs` - a bounded most-recently-used cache of compiled patterns, and
`FuzzyRegex.CacheSize` over it, default 15. Twelve static conveniences go through it: seven that take
no `namedLists` argument, and five that take one and read the cache when it is null, which is their
default. The bypass is per CALL, not per overload - a call that actually carries a dictionary
compiles per call, as every convenience did before. The constructors never consult it.

The key is the pattern text, the RAW options integer the caller passed, the default version and the
instance match timeout - raw, never the `Options` property, which folds in what the pattern's own
inline prefix set and masks off the bits this port does not name, so two different compiles would
share one entry (the S53b item-2 trap). `"(?i)a"` with `None` against `"(?i)a"` with `IgnoreCase` is
the worked example, pinned in `PatternCacheTests`.

Tests: `PatternCacheTests` (20 tests), `PatternCacheStressTests` (2, four threads per core), and two
edits to `ThreadSafetyTests` - `PatternCache` is classified thread-safe under the rule's own third
category, and `StaticTableSnapshot` skips it EXPLICITLY rather than letting the `Convert.ToString`
fall-through render it as a line that can never move. Ratchet GREEN, 6432/6432, baseline 6324.

Oracle: 1 divergence of 6380 at seed 20260919, row 3655, the already-triaged
`docs/plan/2026-09-19-oracle-divergence-fuzzy-edit-attribution.md`; the other two seeds clean.
(Sitting 1 wrote "byte-identical" and sitting 2's verifier disproved it: four of the row's five
lines matched character for character, and the fifth, the pattern, differed because the DOC had
silently un-doubled the report's backslashes. The doc is corrected; the row itself always agreed.)

### Measured, per workload, never an average

`bench/FuzzyRegex.Benchmarks/PatternCacheBenchmarks.cs`, `--job medium` (15 iterations, 2 launches,
10 warmups), 19:05-19:46, artifacts at `artifacts/bench/2026-09-19-S59-after`.

The before and after were taken IN ONE PROCESS rather than across two trees: each `Static*` row has
an `Uncached*` twin whose body is HEAD's own convenience verbatim, so the pair differs in the cache
and in nothing else - no second build, no cross-run drift - and an `Instance*` row gives the ceiling.

| Workload | Time | Allocated | Against its twin |
| --- | ---: | ---: | --- |
| `StaticIsMatch` vs `UncachedIsMatch` | 634.9 ns vs 7,315.3 ns | 1,816 B vs 29,840 B | **11.5x faster, 16.4x less** |
| `StaticMatch` vs `UncachedMatch` | 648.9 ns vs 7,398.2 ns | 1,816 B vs 29,840 B | **11.4x faster, 16.4x less** |
| `StaticFuzzyIsMatch` vs `UncachedFuzzyIsMatch` | 8,744.8 ns vs 11,775.7 ns | 992 B vs 9,656 B | **1.35x faster, 9.7x less** |
| `StaticIsMatch` vs `InstanceIsMatch` | 634.9 ns vs 621.4 ns | 1,816 B vs 1,816 B | 1.02x - the same, to the noise floor |
| `StaticFuzzyIsMatch` vs `InstanceFuzzyIsMatch` | 8,744.8 ns vs 8,386.9 ns | 992 B vs 992 B | 1.04x - the same |
| `StaticIsMatchAlwaysMissing` vs `UncachedIsMatch` | 7,858.1 ns vs 7,315.3 ns | 29,784 B vs 29,840 B | 1.07x, under S58's 1.13 time floor: a pure miss costs what no cache cost |
| `StaticMatchWithNamedLists` vs `UncachedMatch` | 8,062.9 ns vs 7,398.2 ns | 31,920 B vs 29,840 B | the bypass, compiling per call as before; the extra 2,080 B is the named list itself |

Two readings beyond the headline. A cached static call now costs what a pre-compiled instance costs
(1.02x): the convenience has stopped being the slow way to do it. And the always-missing row - 20
distinct patterns cycled against a bound of 15, so every call evicts and misses - lands inside the
noise floor of having no cache at all, so the cache costs nothing when it never hits. The fuzzy rows
move least because matching dominates them: `(?:needle){e<=1}` spends about 8.4 us matching and 3 us
compiling.

The rest of the suite against this machine's committed baseline: GREEN, nothing more than 1.25x
slower or allocating 1.25x more. **No baseline was updated**, deliberately: the machine was shared
for the run. `tools/probes/sample-machine-load.ps1` ran beside it, 162 samples; the busy samples are
the benchmark's own children bar six foreign ones (`python` 8.0-8.5 s in three, `msedge` and
`VBCSCompiler` in three more).

### Review

One blind pass (Sonnet, reproduction-only brief at `.scratch/s59-review-brief.md`, hunt list: a key
omitting the timeout, the version or an inline-prefix flag; an entry mutated after publication; the
MRU order touched without the lock; a cached instance shared while a caller's `namedLists` mutates;
`CacheSize = 0` leaving entries alive; any change to a non-convenience path).

Findings raised: 1. Reproduced: 1. Fixed: 1 - `tests/parity-baseline.json` still carried the old
name of the test renamed to `Raising_the_size_from_zero_starts_caching_again`, so the ratchet went
RED on a missing baselined id; re-run with `-AcceptRemovals -UpdateBaseline`, GREEN at 6324 ids. The
reviewer ran `PatternCacheTests` (31 cases), `PatternCacheStressTests` (2) and `ThreadSafetyTests`
(10) green and reproduced nothing against hunt items 1-7. A second pass over unreviewed delta is
still owed for the docs and the bench file (see below).

### Was still open at the end of sitting 1 (all four done in sitting 2, below)

1. **The AOT test gate is RED and the failure is very likely mine, not the slice's.**
   `tools/run-aot-smoke.ps1` is GREEN - 29 cases, 0 misses, binary 6,982,144 bytes against S58's
   6,972,928, so the cache costs 9,216 bytes of native image (0.13%) and `src/FuzzyRegex` is clean.
   `tools/run-aot-tests.ps1` then failed in ilc: `error MSB3077 ... ilc ... exited with return value
   0, but errors were detected`, with exactly one trim analysis error in the log, `IL2065` at
   `tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs(62)` - a file S59 does not
   touch, in the TEST project, not the library. Before the gate ran I had built that project with
   `dotnet build tests/FuzzyRegex.Tests -c Release -r win-x64` (to prove a wedged compiler was not
   blocking builds), WITHOUT `PublishAot`, into the same `obj/Release/net10.0/win-x64`. Start the
   next sitting by deleting `tests/FuzzyRegex.Tests/obj` and `bin` and re-running the gate clean; if
   it still fails, the IL2065 is real and predates this slice, and the question is why S58 was green.
2. The second blind pass over what the first reviewer never saw: `PatternCacheBenchmarks.cs`, the
   `ThreadSafetyTests` edits, DIVERGENCES, PORTMAP, DECISIONS, and the `CacheSize` XML docs.
3. The fresh-Opus independent verifier (amendment 16 limb (d)), with the verbatim no-git-revert
   clause from `docs/VERIFICATION.md`.
4. `git mv` the slice file to `done/`, closing notes (this file is the draft of them), tick the
   "Done when" boxes.

### Two wedged processes, NOT killed (owner rule: report and wait)

`dotnet publish tests/FuzzyRegex.Tests` PID 33360, started 18:47:10, and its child `csc.exe` PID
37192, started 18:47:28. The csc had burned 105.5 s of CPU and then stopped: two samples 60 s apart
read 105.46875 both times. They do NOT block other builds - a fresh Release build of the same
project completed in 17 s beside them - but they are the first sitting's attempt at the AOT gate and
they are still there. The script's own hang fix (`MSBUILDDISABLENODEREUSE=1`,
`DOTNET_CLI_USE_MSBUILD_SERVER=0`, `tools/run-aot-tests.ps1:46-56`) was in force, so this is a NEW
failure mode: the hang moved from the MSBuild node to `csc.exe` itself.

### Probes run this sitting

- `tools/probes/bcl-regex-cachesize.ps1` - `Regex.CacheSize` is 15 on .NET 10.0.10; reducing it
  evicts at once; 0 clears and disables; -1 throws `ArgumentOutOfRangeException` on `value` and
  leaves the value unchanged. That is where the default of 15 and the `0` semantics come from.
- `tools/probes/s59-named-lists-rebind.py` - regex 2026.9.10 printed `no-match: None` and
  `match: <regex.Match object; span=(0, 4), match='beta'>`, which are the two expected answers in
  `PatternCacheTests.A_caller_s_named_lists_dictionary_bypasses_the_cache`.

## Sitting 2, 2026-09-19 (closing)

### The AOT gate: sitting 1's diagnosis was wrong, and the failure is not this slice's

Ran it from a genuinely clean intermediate directory, as sitting 1 asked:

```
rm -rf tests/FuzzyRegex.Tests/obj tests/FuzzyRegex.Tests/bin     # both confirmed absent
pwsh -File tools/run-aot-tests.ps1 > .scratch/aot-gate.log 2>&1  # exit 1
```

**Identical failure**: exactly one trim analysis error in the whole log - `IL2065` at
`tests/FuzzyRegex.Tests/Conventions/PublicApiDocumentationTests.cs(62)`, on a
`System.Type.GetMembers(BindingFlags)` over types that are not statically known - and then
`MSB3077` out of `ilc`. So the polluted-`obj` hypothesis is dead: clearing `obj` and `bin` changes
nothing.

Sitting 1 also asked "why was S58's gate green?". **It was not.** S58's own closing notes say so:
"AOT tests red on a pre-existing IL2065, ... reproduced byte-identically against an untouched
`src/`" (`docs/plan/slices/done/S58-measurement-method-and-noise-floor.md:124`), with the detail at
`S58-sittings.md:255-260` and a second run at `:327`. The gate has been red since the convention
test arrived in S65, and S58 deliberately left it: the fix is a real choice between annotating,
suppressing with a reason and excluding that test from the native publish, and that is a `.cs`
change no measurement slice may make.

**So S59 did not fix it either** - same reasoning, and the skill forbids widening a slice's scope.
What S59 did instead is make sure it stops being rediscovered: it is now a **scope bullet and a
"Done when" box in `docs/plan/slices/S57-coverage-backstop-and-phase-close.md`**, with the
reproduction, the three options and the note that the record alone was not enough. It cost S58 a
write-up and S59 a full AOT publish to learn the same fact twice.

`tools/run-aot-smoke.ps1` is GREEN, so `src/FuzzyRegex` itself is clean under AOT. That is the half
of the gate S59's "Done when" box actually claims.

### Second blind pass, over what the first reviewer never saw

Brief at `.scratch/s59-review2-brief.md` (Sonnet, reproduction-only): `PatternCacheBenchmarks.cs`,
the `ThreadSafetyTests` edits, DIVERGENCES, PORTMAP, DECISIONS, OPTIMISATION-NOTES,
`PublicAPI.Unshipped.txt`, the `CacheSize` XML docs and both probes. Eight hunt items.

**Three findings, one survived.**

1. **REPRODUCED and fixed - a doc claim that contradicted the code.** DECISIONS and DIVERGENCES
   both said the five `namedLists` conveniences "compile per call as before", full stop. They do
   not: `Cached(pattern, options, namedLists)` is
   `namedLists is null ? Cached(pattern, options) : new FuzzyRegex(...)` (`FuzzyRegex.cs:1552-1559`),
   so `FuzzyRegex.Match(subject, pattern)` with the argument defaulted - the normal call - **is
   cached**. The bypass is per CALL, not per overload. The reviewer proved it with a scratch test
   asserting `Cache.Contains(...)` false and getting true. The public XML docs on `CacheSize` had
   it right all along ("A call that carries a `namedLists` dictionary is not cached"); the two
   records and this notes file had flattened it. All three corrected, and the count corrected with
   them: **twelve conveniences consult the cache - seven that take no `namedLists` argument and
   five that take one and read the cache when it is null** - where sitting 1 wrote "twelve that do
   not take named lists", which double-counted.
2. **NOT REPRODUCED** - "`PatternCacheBenchmarks.cs:84` cites line 1497 of HEAD, which is not the
   `IsMatch` body". The comment names commit `b6e82db`, and at `b6e82db` line 1497 IS that body,
   verbatim. Same for the second at line 1518. The reviewer had resolved `HEAD` against the
   current tree, where the file has grown. The finding is wrong, but it caught a real trap: both
   comments now name `b6e82db` explicitly and say why a doc comment must not cite `HEAD`.
3. **NOT REPRODUCED** - same as 2.

Items 1, 3, 4, 6, 7 and 8 of the hunt list produced nothing: `update-public-api.ps1` left no diff,
no orphaned `ponytail:` comment survives the deleted OPTIMISATION-NOTES row, the `ThreadSafetyTests`
addition matches `PatternCache`'s actual locking, and both probes' real output matches the notes.

### Independent verifier (amendment 16 limb (d)), fresh Opus, no-git-revert clause verbatim

Brief at `.scratch/s59-verifier-brief.md`, ten items. **Seven CONFIRMED, two DIFFERENT, four
sub-items COULD NOT RUN.** It ran the ratchet, both probes, the AOT smoke gate and the full
three-seed oracle itself.

CONFIRMED: ratchet GREEN 6432/6432, 6324 distinct ids against baseline 6324. All five
`bcl-regex-cachesize.ps1` claims on .NET 10.0.10 (15; dict=15 list=15; dict=5 on reduction;
dict=0 list=null at zero and still zero after a call through it; `ArgumentOutOfRangeException`
naming `value` with 15 intact). `s59-named-lists-rebind.py` verbatim, regex 2026.9.10. The call-site
counts, 7 + 5 = 12, and no constructor reaching `Cache`. AOT smoke GREEN, 29 cases, 0 misses,
**6,982,144 bytes** exactly. Oracle: seed 7 `diverge 0 of 6380`, seed 4242 `diverge 0 of 6380`,
seed 20260919 `diverge 1 of 6380` at row 3655 only. **All fourteen numbers of the benchmark table**
against the artifact at `artifacts/bench/2026-09-19-S59-after`, plus the derived ratios. Both
`b6e82db` line quotations.

DIFFERENT, both fixed:

- **The convention test did not arrive in `3b09b76`.** That commit adds the file with the same
  subject and author date, but `git merge-base --is-ancestor 3b09b76 HEAD` rejects it: it is a
  pre-rebase duplicate. The ancestor is **`148c3bf`**. This is the `dfa8767` trap that DECISIONS
  recorded on the same day, sprung again within hours. The S57 bullet now cites `148c3bf` and says
  why.
- **Row 3655 is not "byte-identical" to the triaged doc.** Four of its five lines are; the pattern
  line differs because the doc had un-doubled the report's backslashes. Fixed in the doc, which
  now quotes `report.txt` as it reads and says the escaping is the report's rendering.

While fixing that doc, its `git diff dfa8767 -- src` claim turned out to rest on two pre-rebase
SHAs as well - `dfa8767` and `e30a4e8`, neither an ancestor - so the diff it called empty in fact
had two doc-comment lines and its "28 commits" was a count over a branch nobody is on. Re-derived
on ancestors: `git diff 2c1e747 b6e82db -- src` IS empty, `git rev-list --count 2c1e747..b6e82db`
is **41**, and `... -- src` is **0**. All four numbers are in the doc now.

COULD NOT RUN, and what was done about each:

- **Three gap-test assertions with no provenance** (`PatternCacheTests.cs`). Two were real gaps and
  now carry it, from a real upstream run recorded as `tools/probes/s59-cache-answers-upstream.py`:
  a literal that does not occur in the subject (`regex.search(...)` -> `None`) and `regex.compile("(")`
  raising `error - missing ) at position 1`. The third, `"(?i)a"` compiled `None` versus
  `IgnoreCase` having EQUAL `Options`, is this port's own `Options` property semantics and owes
  upstream nothing - the S53b trap is the right citation and it already carries it.
- **The deleted-`obj` precondition is not evidenced by `.scratch/aot-gate.log`**, which records no
  delete step. Correct, and the reason the two commands are written out verbatim at the top of this
  section: the log alone cannot prove it and the notes must.
- **S58's 6,972,928-byte baseline was not re-derived**, needing the earlier tree. The 9,216-byte
  growth is arithmetic over one measured and one quoted number, and is labelled as such.

### Probes added this sitting

- `tools/probes/s59-cache-answers-upstream.py` - regex 2026.9.10 prints `no-match: None` and
  `compile-error: error - missing ) at position 1`, the provenance for the two `PatternCacheTests`
  assertions that assert a matching or compiling answer rather than cache mechanics.

### The two wedged processes from sitting 1 are gone

`tasklist //FI "PID eq 33360"` and the same for `37192` both print "No tasks are running which
match the specified criteria". Neither was killed by this session; they ended on their own or with
the owner. No process was killed at any point in this slice.
