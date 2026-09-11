# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S29, **PARKED not done**. **Blocker:** `(*SKIP)` does not commit the search
position. **Next:** an owner decision - finish S29 from the reproduction below, or start S30 and
come back. The slice file stays in `slices/`, so the driver will pick it up again.

**Where the port stands:** ratchet GREEN, nothing failing, 5729 tests with 5394 passing. Oracle
GREEN over the fifteen default generators. The sixteenth, `verbs`, is new and **deliberately not in
`run-oracle.ps1`'s default list** - it finds the blocker, and leaving it in would turn every later
slice's oracle run red for S29's reason and hide that slice's own result.

**What S29 landed.** `PRUNE` and `SKIP` as two forward cases in `Matcher.BasicMatch`, `top_bstack`
over a new `ByteStack.TopSize`, 32 tests un-skipped and passing, the `verbs` generator, four
controls, and gap tests. Every `pstack` site is accounted for in PORTMAP - the slice file's claim
that only one push was ported was stale, S27 and S28 had taken all ten.

**The blocker, reproducible in three lines.** Deterministic, stable across repeats, forward
direction, plain `search` - no `(?r)` and no multi-match operation needed:

    import regex
    regex.compile(r"(?:..(*SKIP)x|q)x").search("ab cd xx")    # None
    regex.compile(r"(?:..(*SKIP)x|q)x").match("ab cd xx", 4)  # (4, 8) - what our port returns

Upstream finds that match only when the search *starts* at 4. A failed attempt in which a `(*SKIP)`
fired commits the search past position 4; our port re-tries it. **Do not guess at a fix**: a hand
trace of upstream's `FAILURE` advance (`:15695`-`:15738`, whose `text_pos < slice_start` clamp *is*
ported, at `Matcher.cs:5334`) predicts upstream should match at 4, and it does not. Instrument
upstream on this reproduction first. The same cause is likely behind the four `(?r)` multi-match
divergences at `tools/run-oracle.ps1 -Generator verbs -Count 1200 -Seed 20260913`, which are **not**
upstream instability - each is stable whether or not the caller holds the previous match.

**A real bug S29 did fix, which S30 should know about.** `findall` and `finditer` are not the same
loop: only `pattern_findall` carries the `slice_start <= text_pos` guard (`:22415`). S25 recorded
them as identical on nine measured pairs, and they are - until a `(*SKIP)` moves `slice_start`.
`Iteration.Scan`/`Next` lost that guard and `MatchState.IsInSlice` is deleted.

**A trap in the tooling, found the hard way.** `run-oracle.ps1 -SkipRecord` silently **ignores**
`-Rows` and re-compares whatever wave is already on disk. It reported a confident GREEN against
S28's stale wave here. Use `-Rows` without `-SkipRecord`.

**Controls:** `python tools/run-controls.py --slices S29`. Cached waves in `.scratch/control-waves/`
must be deleted after any generator change, or you measure the old generator.
