# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**Phase:** 3, the engine. **S17 is DONE** and committed; the tree is clean. Pending queue: S18-S26.

**Current slice:** none in flight. **Next is S18** (`alternation, groups and captures`): `BRANCH`,
`START_GROUP`/`END_GROUP`, the group span stacks, and `Match.Groups`/`Captures` for groups 1+.

**Where the port stands:** ratchet GREEN, 3917 passing, 5516 total, nothing failing; overall parity
**18.8%** (was 6.5%). `Escapes` is at 100%, `UnicodeProperties` 70%. The matcher has literals, `.`,
`STRING`, the plain anchors, and now `PROPERTY`, `RANGE` and the four `SET_*` operators with the
scoped `(?a:)`/`(?u:)` encoding. Groups 1+, `Matches`, `Replace`, `Split` and `partial: true` are
still seams. The three biggest waiting tags are `quantifiers` 211, `ignore-case` 154, `find-all` 132.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Latest:
`agree 6000  unsupported 0  diverge 0` over `literals`, `literal-dot`, `anchors` and the new
`classes` generator, 1500 rows each. Two negative controls fired (6 and 133 divergences).

**Blockers:** none.

**S18 inherits these duties:**

- **Add a generator per capability it delivers**, and run a negative control on it. `classes` found
  nothing this time, but the two controls proved it can - a generator nobody has broken on purpose
  is a generator nobody knows works.
- **Retag, do not just un-skip.** S17 un-skipped 84 attributes, 156 tests still failed at another
  slice's seam, and every one had to be retagged with prose. Six fan-out methods needed splitting
  because only some rows were blocked; `git show HEAD:<path>` and compare the `[Arguments]` rows
  afterwards, because a split is where test data gets silently altered.
- **Put `advance:` back** in `BasicMatch` with the first real backtrack case; S16 removed it because
  C# rejects an unreferenced label. **`push_pointer` is still not ported** and S18 must decide it: a
  node reference cannot go in a byte array on a managed heap.

**Worth knowing before the next slice:**

- **Do not reason about upstream, run it.** `ENCODING_KIND` looks dead by grep and is not; the bits
  come from the Python compiler. One `_regex.compile` intercept settled it. DECISIONS 2026-08-31.
- **Working-tree line endings are LF** (`.gitattributes` sets `* text=auto eol=lf`), so the older
  "CRLF, do not use Python's `write_text`" warning was wrong; what *is* true is that the `Read` tool
  renders a unicode escape in the source it shows you as the character itself, so echoing
  that back through `Write` puts a raw control byte in the file. Count the bytes afterwards.
- **Run the ratchet AFTER committing as well as before** - the pre-commit CSharpier hook rewrites
  files (DECISIONS 2026-08-31).
- **Scratch lives in `.scratch/`** (gitignored). The driver fails any slice whose tree is dirty.
