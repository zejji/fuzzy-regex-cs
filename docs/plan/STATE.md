# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**PHASE 4 IS AUTHORED AND AWAITING THE OWNER'S APPROVAL (2026-09-11).** Seven slice files sit in
`docs/plan/slices/`: S27 lookaround, S28 conditional-with-lookaround, S29 `(*PRUNE)`/`(*SKIP)`,
S30 recursion, S31 partial matching, S32 POSIX, S33 phase close. Approve or adjust them, commit
them with the ROADMAP and spec amendment 15, then `pwsh -File tools/launch-slice.ps1 s27`.

**Current slice:** none. **Blockers:** none.

**Where the port stands:** ratchet GREEN, nothing failing, overall parity **76.5%** (was 71.8% at
the S26 close) with 22 areas at 100% - read `docs/STATUS.md` for the figures. The jump is commit
`8ae8607`, not engine work: 92 skipped tests already passed under stale skip reasons (inline
flags, version flags, comments, branch reset, possessive, `(*FAIL)`, 17 named-list tests). The
S26 handover's two owner decisions are closed by that evidence - ROADMAP's 2026-09-11 note.

**What is actually left before fuzzy**: lookaround 63 + lookbehind 17, partial 82, recursion 60,
verbs 32, conditionals 15, POSIX 8. Every one fails on a `NotImplementedException` seam, none on
a wrong answer, so the seams are exactly where the S26 handover said (`Matcher.cs:4567`, `:5196`).

**One correction to the S26 handover, for S27's author:** there are no `STRING_SET` opcodes
anywhere in `_regex.c` - named lists match today because `StringSet` is a `Branch`.

**Oracle:** `pwsh -File tools/run-oracle.ps1` before committing any engine slice. Thirteen
generators; each Phase 4 slice adds one and puts it on the default list.

**One oddity for the owner:** `slice-log.jsonl` records S26 as `failed` (145.8M tokens) with its
own commit `b778b07` as the abandoned SHA; the reflog shows the driver rolled back at 18:04:57 and
main was fast-forwarded to `b778b07` by hand at 18:10. The commit is real; the log row is stale.
