# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S52b IS CLOSED (2026-09-16, one sitting), and the slice file is in `done/`. Ratchet GREEN,
6144 / 6144 / 0 skipped, 6036 distinct ids, baseline 6036** (+17 tests). Default oracle wave GREEN
at all three seeds. Two `src/` changes, both small and both explained below.

**THE SLICE FOUND A REAL DEFECT, AND IT WAS IN `Match`, NOT IN THE PATTERN.**
`Match.FuzzyChanges` cached into a `FuzzyChanges?` - a flag plus three list references, four fields
wide - published with `??=`. Not an atomic write, so a second thread could see the flag set and a
list still null. Reproduced, then fixed to a `StrongBox<FuzzyChanges>`: one reference, one atomic
publication. The pattern graph itself was already correct, and is now measured rather than assumed.

**THE PATTERN-GRAPH TESTS ARE PERMANENT AND THEY ARE AIMED AT PHASE 7.** Forty fields reachable
from a compiled `FuzzyRegex` are mutable, all written only by `PatternObject.Compile` inside the
constructor; they are allowlisted, and a snapshot of the whole graph taken **before any match** and
compared after two workloads is what proves the claim. A Phase 7 slice that adds a start
optimisation or a prefilter cache will turn these red on the day it lands. **Remove the shared
state or publish it as one reference - never add a line to the allowlist.**

**WHAT THE BLIND REVIEW CAUGHT IS THE LESSON WORTH CARRYING.** Both its findings were one mistake:
my snapshot tests warmed up *before* taking the baseline, which hid every first-match-only write -
exactly the shape of the Phase 7 cache the tests exist to catch. Both reproduced, both fixed
(snapshot before any match; salted subjects between the two static readings so a per-subject memo
cannot saturate). Six controls, A-F, are recorded in the slice file with the exact source edit and
the exact failure each must produce.

**ONE SCOPE ITEM WAS DELIBERATELY NARROWED:** the suite-wide debug pool wrapper would have needed a
settable static on the library, which this slice's own static audit forbids. `ByteStack` took an
instance-level pool parameter instead and is tested exhaustively; every `ArrayPool` call in `src/`
is inside that one class, verified twice.

**PROCESS: never tell a verifier to `git checkout -- <file>` while the slice's work is uncommitted.**
Mine did, discarding uncommitted `Match.cs` work; it reconstructed it and I checked both diffs line
by line. Commit a checkpoint first next time.

**NEXT: S52c (metamorphic invariants)**, then S52d. Nothing is blocked.

**Carried** (full list in S52 sitting 10's notes, untouched by this slice):
`upstream-bestmatch-free-answer.py`'s unguarded `fuzzy_changes` read; the `_regex.c` citation
reconciliation; `port-tests/SKILL.md`'s stale `FuzzyRegex.Search(...)`; `record-oracle.py
--self-check`; `run-controls.py`; control sites S32-B/S38-A/S35-A/S29-A/D; PORTMAP lines;
`quantifiers-long`'s filler margin; `oracle.yml`'s weekly sweep verdict rule; the
`pos`/`endpos`-versus-`codepointSlice` fix. **Open for the owner:** `slice-log.jsonl` marks S26
`failed`; `origin/main` needs a push.
