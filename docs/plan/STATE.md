# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S53b IS A CHECKPOINT, NOT CLOSED (sitting 1, 2026-09-16).** All four scope items and the records
are in the tree and green. Ratchet GREEN 6226 / 6226 / 0, baseline 6047 to 6118. Oracle GREEN at
all three default seeds over every generator. Release solution build clean. The slice file is still
in `docs/plan/slices/` on purpose.

**THE ONE THING LEFT IS THE INDEPENDENT VERIFIER.** It was dispatched at 15:31 with eleven claims
and had not reported when the deadline landed. Re-dispatch it against this commit; the full brief
is reproducible from `docs/plan/slices/notes/S53b-sittings.md`, which lists every claim, both
negative controls with their four numbers, and the three probes. Then move the slice file to
`done/` with the closing notes and tick its five boxes. **Nothing else is outstanding.**

**What landed:** `EnumerateMatches`/`EnumerateSplits` (one engine state per STEP, so the walk
cannot leak a rented buffer - and so its `timeout` bounds each step, which is a DIVERGENCES row);
`Ascii`/`Unicode`/`Word` as options, which moved ten assertions TOWARDS upstream because `Options`
now reports the `UNICODE` every text pattern carries; `GroupCollection` as an
`IReadOnlyDictionary<string, Group>`; `beginning`/`length` on `Replace`/`ReplaceFormat`.

**Three slice-file claims were wrong and are corrected in DECISIONS.** .NET's `GroupCollection.Keys`
is TOTAL, not named-only (`[0, 1, a, c]`, measured); `EnumerateSplits` can take no slice, because
upstream's `split` takes none; and upstream's inverted `pos>endpos` is unspellable through
`beginning`/`length`.

**The dictionary view is a SOURCE BREAK** - two `IEnumerable<T>` faces make `Groups.Select(...)`
ambiguous, exactly as on the built-in collection since .NET 5. Eleven call sites in this repo
proved it; `Groups.Values` is the fix and is documented.

**Review: blind pass over the whole diff, 0 findings, no second pass needed** (nothing was fixed).

**NEXT: finish S53b's verifier, then S54.**

**Carried, newest first:** a PRE-EXISTING `Options` disagreement the reviewer surfaced - with
upstream's `DEFAULT_VERSION` at `VERSION1`, `regex.compile('(?V0)a').flags` is `0x6020` against our
`0x2020`, upstream reporting `FULLCASE` for an inline `(?V0)` and not for the `V0` flag; the
`FullCase` bit predates this slice. Plus S53's list: `check-ratchet.ps1` can hang on a wedged
MSBuild node (`MSBUILDDISABLENODEREUSE='1'` plus `DOTNET_CLI_USE_MSBUILD_SERVER='0'`; owner call);
run `dotnet build` on the SOLUTION before committing a slice that adds a project. Plus S52c/S52d's
list: two unjudged oracle rows (seeds 99991 row 3825, 31415 row 3756, both `interactions`, both
proven pre-existing); `record-oracle.py --self-check` RED on one pre-existing guard;
`upstream-bestmatch-free-answer.py`'s unguarded `fuzzy_changes`; the `_regex.c` citation
reconciliation; `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`; `run-controls.py`; control
sites S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines; `quantifiers-long`'s filler margin; `oracle.yml`'s
weekly sweep verdict rule. **Open for the owner:** `slice-log.jsonl` marks S26 `failed`;
`origin/main` needs a push.
