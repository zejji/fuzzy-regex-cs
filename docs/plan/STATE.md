# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**PHASE 3 IS COMPLETE.** S26 is done and committed, the pending queue is empty, and
`tools/run-slices.ps1` refuses to cross a phase boundary - so **this is the owner checkpoint**.
Review `docs/STATUS.md`, skim the recent commits, then open a fresh session and paste the prompt in
`docs/plan/OPERATIONS.md`: *"Read docs/plan/STATE.md and the spec. Phase 3 is complete. Author the
slice files for phase 4 per the roadmap, present them for my review, and wait."*

**Current slice:** none. **Blockers:** none.

**Where the port stands:** ratchet GREEN, nothing failing, overall parity **71.8%** with fifteen
areas at 100% - but read `docs/STATUS.md` for the figures rather than quoting them from here. The
engine matches everything in Phase 3's scope: literals, classes, quantifiers, groups, backrefs,
boundaries, `\K`, case folding, reverse, substitution and iteration. **No test is skipped on a tag
Phase 3 delivered.**

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Thirteen
generators now - S26 added `interactions`, which composes the other twelve's constructs instead of
running one family at a time - and each row is bounded at ten seconds
(`OracleComparer._rowTimeout`), because an unbounded row used to hang the run rather than fail it.

**Two things for the owner to decide before Phase 4 is authored:**

- **Four capability families are claimed by no phase**: `inline-flags` (29 tests),
  `backtracking-verbs` (34), `version-flags` (11), `comments` (4). Widen Phase 4 to 10-14 slices, or
  give them their own. ROADMAP states both options; a slice cannot verify what no phase claims.
- **`docs/plan/budget.json`'s `maxSlicesPerWeek` of 12** binds after two and a half busy days.

**Phase 4's author should read the S26 closing notes' handover section first.** In one line: both
seams are `default:` arms in `Matcher.BasicMatch` (`:4567` advance, `:5207` backtrack);
`state_init_2` deliberately allocates no fuzzy or group-call guards; the `push_groups` /
`push_repeats` families are unported with their call sites listed in PORTMAP, and their *backtrack*
halves are what an opcode-by-opcode port misses; and **partial matching is the cheapest 82 tests on
the board**, because the matcher's partial arms already exist and the seam is a four-line guard.
