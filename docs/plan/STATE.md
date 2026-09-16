# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S53 IS CLOSED (one sitting, 2026-09-16).** Ratchet GREEN 6155 / 6155 / 0, baseline 6045 to 6047
(two new tests, no removals). Closing notes in `docs/plan/slices/done/`.

**THE AOT GATE IS GREEN AND `src/` WAS NOT TOUCHED.** `tools/run-aot-tests.ps1` publishes the whole
suite Native AOT and runs it: 6,155 total, 0 failed, 3 skipped. `tools/run-aot-smoke.ps1` publishes
`samples/FuzzyRegex.AotSmoke`, a consumer app: 29 of 29. Both are the `native-aot` CI job on
windows-latest and ubuntu-latest. The library produces zero trim and zero AOT warnings of its own.

**PHASE 7's BASELINE** (consumer binary, win-x64): **6,972,928 bytes (6.65 MB)**, byte-exact on
re-publish - that is the solid figure. The timings are wall clock on a busy machine and are RANGES
over seven observations: **28-54 ms to `Main`**, **30-57 ms to the first answer**, 83-110 ms for all
29 cases. The library's own first compile and match is the gap, **1-3 ms**. Quote the WARM run only:
a freshly written binary's first execution cost 552-566 ms to `Main`, which is image load and an
antivirus scan of new bytes, not this library. Re-measure on a quiet machine before optimising.

**Five real defects, all outside `src/`**: `BeEquivalentTo` cannot run under AOT (1,547 tests failed
on the first publish; twelve sites now use `Equivalence`); the S52b object-graph walk cannot either
and is skipped natively only; assertion failures were reported as the wrong exception, twice
(satellite cultures, then a reflectively constructed `AssertionException`); `Lazy<>.IsValueCreated`
is not reflectable; and a wedged MSBuild node hung two publishes for 600 s each.

**Three tests were passing over NOTHING** - reflection scans whose subset assertions hold when the
scan finds nothing. New measured floors in `MutableFieldsInThePatternGraph`, `ThreadSafetyTests` and
`PortedTestConventionsTests`. One floor was written from a misreading (1,967 test CASES versus 892
test METHODS) and failed at once, which is the guard working before it was committed.

**Review: first pass 4 findings, 4 reproduced, 4 fixed** - including a sample that did not compile
in CI's own `dotnet build` step, which nothing the slice ran would have caught. **Second pass over
the fix delta: 2 findings, 2 reproduced, 2 fixed** (both evidence accuracy). **Verifier: 11 of 12
CONFIRMED**, the one DIFFERENT being a wall-clock timing quoted to four digits, now a range.

**NEXT: S53b** (API completeness before optimisation), then S54.

**Carried, and the first one is new and affects the driver:** `tools/check-ratchet.ps1` can hang on
a wedged MSBuild node exactly as the publishes did - the workaround is
`$env:MSBUILDDISABLENODEREUSE='1'` plus `$env:DOTNET_CLI_USE_MSBUILD_SERVER='0'`; whether the script
should set them is an owner call. Also: run `dotnet build` on the SOLUTION before committing a slice
that adds a project. Plus S52c/S52d's list: two unjudged oracle rows (seeds 99991 row 3825, 31415
row 3756, both `interactions`, both proven pre-existing); `record-oracle.py --self-check` RED on one
pre-existing guard; `upstream-bestmatch-free-answer.py`'s unguarded `fuzzy_changes`; the `_regex.c`
citation reconciliation; `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`; `run-controls.py`;
control sites S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines; `quantifiers-long`'s filler margin;
`oracle.yml`'s weekly sweep verdict rule. **Open for the owner:** `slice-log.jsonl` marks S26
`failed`; `origin/main` needs a push.
