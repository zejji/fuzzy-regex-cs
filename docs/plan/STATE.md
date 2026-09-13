# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S39 is next - fuzzy strings, backreferences and the `*_REPEAT_ONE` loops. Launch:
`pwsh -File tools/launch-slice.ps1 s39`. S38 landed the fuzzy spine: a section of one-character and
zero-width items now matches with substitutions, insertions and deletions and reports its counts and
change positions, and the oracle has a `fuzzy` generator, GREEN at 6000 rows on three seeds.

**Read this before writing an S39 test or widening the generator.** `(?:foo){e<=1}` is **not** in
S38's subset. `Sequence.pack_characters` (`upstream/regex/_regex_core.py:3526`) packs a run of two or
more literal characters into one `STRING` node, so a fuzzy `foo` is a fuzzy string. Every S38 test
and generated pattern therefore avoids two adjacent literals - S39 only has to stop avoiding them.

**What S38 found.** Two port defects, both fixed: `match_fuzzy_changes` reports only the first
`Total` entries of the change list, not the whole list (found by the wave, 4 rows over 3 seeds); and
`start_match`'s fuzzy-counts clear (`_regex.c:11790-11792`) was unported, so a `(*PRUNE)`/`(*SKIP)`
search restart carried the abandoned attempt's errors (found by the blind review). One new upstream
crash: `regex.search(r'(?p)(?:[ab][bc]){e<=1}', 'ax')` segfaults 2026.7.19 - **ledger entry 9**, and
the generator must draw no `(?p)` until it is fixed. `fuzzy_guards` joins `group_call_guard_list` as
never-to-be-ported: written four times upstream, read nowhere.

**Two things the next slices must not read as settled.** Control E (nested section inherits the outer
counts) is silent at both seeds because the generator cannot reach the shape - proved observable by
hand, 21 of 24 rows, see `tools/probes/fuzzy-nested-rows.py`; **S43 should widen, not trust the
zero.** And the `search-start-partial` family becomes reachable the moment fuzzy meets lookaround,
which is S43's `interactions` widening: it will need an `ExpectedDivergences` entry.

**Blockers:** none.

**Where the port stands:** ratchet GREEN, 5791 tests, 5608 passing, parity **90.8%**, 29 areas at
100%. 183 skipped, all fuzzy; 20 of the 27 `Seam.For(Opcode.Fuzzy)` sites remain, all of them
`STRING*`, `REF_GROUP*` or `*_REPEAT_ONE`. No `needs:fuzzy-*` tag went green in S38 - the probe found
136 of 139 un-skipped tests still at a seam and none answering wrongly.

**Oracle:** `pwsh -File tools/run-oracle.ps1` (three seeds; `fuzzy` is now in the default list).
Rows per generator is **`-Count`**; `-Rows` is a path to a JSONL file. Controls:
`python tools/run-controls.py --slices S38 --seeds 3`. Upstream 2026.9.10 for probes is in
`.venvs/regex-2026.9.10` (git-ignored); the oracle itself runs the PATH python, regex 2026.7.19.

**Upstream is a ledger, not a queue** (`docs/plan/upstream-reports/LEDGER.md`, nine entries): nothing
filed until everything else in the plan is done. Entry 7 is on Phase 6's fix list.

**Still open for the owner:** `slice-log.jsonl` marks S26 `failed` though its commit is real;
`origin/main` trails local and needs a push.
