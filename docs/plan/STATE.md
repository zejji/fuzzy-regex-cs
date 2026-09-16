# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S53b IS CLOSED (sitting 2, 2026-09-16).** Slice file in `docs/plan/slices/done/` with its closing
notes and five ticked boxes. Ratchet GREEN 6226 / 6226 / 0, baseline 6118, no baseline move.

**The one outstanding claim is CONFIRMED.** A fresh verifier ran `pwsh -File tools/run-oracle.ps1`
with every default (seeds 7, 4242, 20260916; 22 generators; 300 rows; Release): 0 diverge at each
seed, agree 6342 / 6347 / 6354 of 6380, "Oracle: GREEN ... at all 3 seeds". All eleven of S53b's
claims are now CONFIRMED across the two sittings; nothing was removed or weakened. Sitting 2 changed
no code, no test and no instrument, so sitting 1's negative-control numbers stand as recorded.

**Sitting 1's notes file said the verifier never reported and its commit message said it confirmed
ten of eleven.** The commit message was right. Lesson recorded in the notes: the message is written
last, so update the notes file in the same edit.

**NEXT: S54** (`docs/plan/slices/S54-benchmark-baselines-and-edge-pins.md`), the first slice of
Phase 7. The public API is frozen from `f3c1135`, so S54 measures the shape S53b settled, and its
workload list already names `EnumerateMatches` beside `Matches` on the 1 MB subject.

**Carried, newest first:** a PRE-EXISTING `Options` disagreement - with upstream's `DEFAULT_VERSION`
at `VERSION1`, `regex.compile('(?V0)a').flags` is `0x6020` against our `0x2020`, upstream reporting
`FULLCASE` for an inline `(?V0)` and not for the `V0` flag; the `FullCase` bit predates S53b. The C
comment at `_regex.c:22091` is wrong about `text_length` being truncated to the slice end
(`state_init` sets the whole subject's length, `:18439`); this port follows the code. Plus S53's
list: `check-ratchet.ps1` can hang on a wedged MSBuild node (`MSBUILDDISABLENODEREUSE='1'` plus
`DOTNET_CLI_USE_MSBUILD_SERVER='0'`; owner call); run `dotnet build` on the SOLUTION before
committing a slice that adds a project. Plus S52c/S52d's list: two unjudged oracle rows (seeds 99991
row 3825, 31415 row 3756, both `interactions`, both proven pre-existing); `record-oracle.py
--self-check` RED on one pre-existing guard; `upstream-bestmatch-free-answer.py`'s unguarded
`fuzzy_changes`; the `_regex.c` citation reconciliation; `port-tests/SKILL.md`'s stale
`FuzzyRegex.Search(...)`; `run-controls.py`; control sites S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines;
`quantifiers-long`'s filler margin; `oracle.yml`'s weekly sweep verdict rule.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
