# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S18 is DONE** and committed; the tree is clean. Pending queue: S19-S26.

**Current slice:** none in flight. **Next is S19** (`quantifiers and repeat guards`): the
`GREEDY_REPEAT`/`LAZY_REPEAT`/`*_ONE` families, `BODY_*`/`MATCH_*`/`TAIL_START`, and `reset_guards`.

**Where the port stands:** ratchet GREEN, 3987 passing, 5526 total, nothing failing; overall parity
**21.8%** (was 18.8%). `Escapes` and `Api` and `Basics` are at 100%, `UnicodeProperties` 70%,
`Various` 31.7%, `Groups` 30.8%. The matcher has literals, `.`, `STRING`, the plain anchors, S17's
classes, and now `BRANCH`, `START_GROUP`/`END_GROUP` with the full public group surface -
`Groups[n]`, `Groups[name]`, `Group.Captures`, `LastGroupNumber`/`LastGroupName`. `needs:quantifiers`
is worth **253 tests**, far the biggest remaining win; then `ignore-case` 154 and `find-all` 132.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Latest:
`agree 7500  unsupported 0  diverge 0` over five generators (the new `groups` one included) at 1500
rows each. Three negative controls fired: 9, 79 and 195 divergences. The wave now records
`lastindex`/`lastgroup` too, so the oracle sees the whole group surface.

**Blockers:** none.

**S19 inherits these duties:**

- **Add a generator per capability it delivers**, and run a negative control on it. S18's three
  controls all fired; a generator nobody has broken on purpose is a generator nobody knows works.
- **Retag, do not just un-skip.** S18 removed 61 attributes and had to re-skip 28 methods with
  prose, 4 of them only found by a *second* full run. One fan-out method needed splitting; check
  `git show HEAD:<path>` and compare the `[Arguments]` rows afterwards, because a split is where
  test data gets silently altered.
- **`reset_guards` (`:3383`) is S19's**, and both call sites - `init_match` and the `FAILURE`
  backtrack case - carry a comment naming it. The `advance:` label is back and `BRANCH` jumps to it.

**Worth knowing before the next slice:**

- **Do not reason about upstream, run it.** S18's slice file claimed a repeated capture group
  exercises `save_captures`/`restore_groups`; it does not - those have only Phase 5 callers, and a
  repeat uses `push_captures`/`pop_captures`, whose callers are all Phase 4. Two greps settled it.
- **A node reference on the byte stack is an index into `PatternObject.NodeList`**
  (`ByteStack.PushNode`, `Node.Index`), assigned at the end of `PatternObject.Compile` because the
  optimiser prunes that list. DECISIONS 2026-08-31.
- **Do not use Python's `write_text` to rewrite a source file**: it translates `\n` to CRLF on
  Windows and the working tree is LF, which rewrites the whole file and fails IDE0055. Use
  `write_bytes`. Cost one build during S18.
- **Run the ratchet AFTER committing as well as before** - the pre-commit CSharpier hook rewrites
  files (DECISIONS 2026-08-31).
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
