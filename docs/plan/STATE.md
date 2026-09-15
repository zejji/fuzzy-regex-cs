# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52 IS A CHECKPOINT, NOT CLOSED (2026-09-15, sitting 1).** Suite 6,089 / 6,089 / 0 skipped, ratchet
GREEN, baseline 5,975 -> **5,981**. The slice file stays pending. **Oracle is RED on 11 rows, every
one triaged below** - the widened generators found them and judging them is sitting 2's first job.

**Landed.** (1) `tools/sweep-seeds.ps1` - N fresh seeds (default 20, 2000 rows), drawn from a master
seed printed and logged, resumable, per-seed and per-generator summary, and it tells a HARNESS
failure from a divergence. Weekly `sweep` job on `oracle.yml`, Thursdays, 6 seeds, master seed = run
date. (2) **The oracle could not classify ANY family on an answer with no spans** - `sub`, `subf`,
`split`, and a row upstream failed while matching. That was the whole of the pre-existing red at
2000 rows. Recorder now records `scanMatches` (upstream's own `finditer`), and a new ROW-KEYED entry
`turkic-default-folding-without-spans` reads it. (3) All Unicode planes: one `ASTRAL_*` block, SMP
digits, emoji modifier and ZWJ into every generator's alphabets, astral atoms and `\X` into the
seven atom lists whose patterns held nothing above U+FFFF. Measured before and after in
`tools/probes/generator-plane-coverage.py`: `fuzzy` went 118 astral subjects / 0 astral patterns of 1200 to
512 / 477.

**The 11, with the control already run for each** (`tools/run-oracle.ps1 -Count 2000`, 42,000 rows a
seed). **Eight are upstream's `(*SKIP)` carried slice**, and on every one `(*PRUNE)` - same pruning,
no bound moved - gives THIS PORT's answer exactly: seed 7 rows 24018, 24737, 25854, 38151 and seed
20260915 rows 24224, 38101. Seed 4242 row 24256 and seed 7 row 24916 are POSIX (clearing the flag
gives the port's answer). Seed 7 row 24430 is a POSIX fuzzy-counts row - do NOT read
`fuzzy_changes` on one, it crashes upstream (known, `tools/probes/upstream-posix-fuzzy-changes-crash.py`).
Seed 20260915 row 25482 is **Turkic reached through a `\L<name>` list** - the predicate's own
doc predicted this false negative; removing the Turkic member gives the port's answer. Seed 20260915
row 34508 is **`search-start-partial`, NOT Turkic** - measured: same answer with the `İ` replaced and
with IGNORECASE off - and rows 34463/34955/34970 of the same shape DO classify, so the predicate is
the gap.

**Owed, carried:** `.claude/skills/port-tests/SKILL.md` still teaches `FuzzyRegex.Search(...)`;
`record-oracle.py --self-check` exits 1 (pre-existing on HEAD - S43 turned that guard's `SystemExit`
into a recorded `resource` outcome and left the check stale); `tools/run-controls.py` cannot measure
a control that mutates the recorder (S42-2A); broken control sites S32-B and S38-A; S35-A and S29-A/D
thin; PORTMAP `_regex.c` lines stale. **Not started in S52:** long-subject generators and timeout
rows. **Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
