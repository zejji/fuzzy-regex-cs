# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Current slice:** none. S29 is **closed** and in `done/` (2026-09-11, second session). Next is S30,
recursion and group calls.

**Blockers:** none.

**What S29's close changed, and it is not what the last session predicted.** The finishing scope said
recording `verbs` against a prefilter-free upstream would make the wave green. Measured: it changes
**0 rows of 3600** across five seeds, and the four wave divergences survive it untouched. They are a
second, different mechanism - **upstream's `search_start`** (`_regex.c:8385`), which this port does
not implement. Its scanners bound themselves with `text_end`/`text_start` where the `try_match_*`
predicates `basic_match` consults bound themselves with `slice_end`/`slice_start`; only a `(*SKIP)`
moves the slice mid-attempt, and then upstream's fast path skips a start position its own slow path
accepts. Minimised to `regex.finditer(r"(?r)(?:a*(*SKIP)b|[^a-f])$", "\nb", regex.M)` - upstream one
match, this port two - and confirmed by emulating the scanner in front of each attempt.

**Where the port stands:** ratchet GREEN, 5731 tests, 5396 passing, tree clean. Oracle GREEN over the
fifteen default generators. **`verbs` stays off the default list until Phase 7**, now for a named and
permanent reason rather than an open question; run it explicitly.

**For Phase 7's author:** porting **either** `locate_required_string` **or** `search_start` turns the
`verbs` wave red on purpose. Delete the `prefilter-free` wrapper in `record-oracle.py`, invert the two
gap tests in `Gaps/Engine/BacktrackingVerbTests.cs` (both marked), put `verbs` back on the default
list, and rewrite control S29-A, whose mutation neutralises the same asymmetry and runs *negative* at
one seed. PORTMAP's two deferral rows carry the detail.

**For S30's author:** `findall` and `finditer` are not the same loop once `(*SKIP)` moves
`slice_start` (S29 notes); `push_repeats`/`pop_repeats` are ported (S28), so recursion ports
`push_groups`/`pop_groups` and the group-call guard list only.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Controls:
`python tools/run-controls.py --slices S29`. Delete `.scratch/control-waves/` after a generator
change. Read control figures against the honest engine's own baseline, not as totals.

**Still open for the owner:** `slice-log.jsonl` marks S26 and S29 `failed` though both commits are
real. And S29 closed with its "oracle wave green" box ticked as *superseded* - see its last section.
