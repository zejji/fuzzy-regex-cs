# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** S29, re-opened to finish (2026-09-11). Its engine code is committed (`8e80b21`);
what remains is the closing scope in the slice file's last section: record the `verbs` generator
against a prefilter-free upstream, put it back on the default oracle list, re-run wave and
controls, pin two gap tests, PORTMAP note, and **`git mv` the slice file to `done/`** - the step the
first session missed, which made the driver roll back a green commit.

**Blockers:** none. The blocker the first session parked on is resolved: upstream's `None` on
`(?:..(*SKIP)x|q)x` / `ab cd xx` is its required-string prefilter moving the first attempt to 3
(`req_offset=3`, `locate_required_string`, `_regex.c:11082`), Perl-identical and PCRE2-documented,
not a verb defect on either side. With that prefilter neutralised upstream gives the port's (4, 8).
Details and the finishing steps: S29 slice file, last two sections; DECISIONS 2026-09-11.

**Where the port stands:** ratchet GREEN, 5729 tests, 5394 passing, parity 81.3%, tree clean.
Oracle GREEN over fifteen default generators; `verbs` rejoins the list when S29 closes.

**For S30's author:** `findall` and `finditer` are not the same loop once `(*SKIP)` moves
`slice_start` (S29 closing notes); `push_repeats`/`pop_repeats` are ported (S28), so recursion
ports `push_groups`/`pop_groups` and the group-call guard list only.

**For Phase 7's author:** porting `locate_required_string` will turn the `verbs` wave red on
purpose. Remove the prefilter-free wrapper in `record-oracle.py` and invert the two gap tests that
pin (4, 8) and (2, 6); both are marked.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Controls:
`python tools/run-controls.py --slices S29`. Delete `.scratch/control-waves/` after a generator
change or you measure the old generator.

**Still open for the owner:** `slice-log.jsonl` records S26 as `failed` with its own commit as the
abandoned SHA, and S29 likewise (`8e80b21`, rolled back for the unmoved slice file, restored by
hand). Both commits are real; both rows are the driver's verdict at the time.
