# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S49 IS CLOSED (2026-09-14, one sitting).** Suite 5,968 / 5,963 passing / 5 skipped, ratchet GREEN,
baseline unchanged at 5,855. No `src/` file changed, so no oracle wave and no control were owed.
**Next slice is S50** (fix the inherited issues), and its list is exactly five.

**The live re-triage is `docs/plan/upstream-issues/2026-09-14-triage.md`: 74 open, A=36 B=28 C=10,
no class D.** Five issues closed upstream since 2026-08-31 (609, 611-614), **none opened**, and not
one still-open issue's text changed - newest `updatedAt` 2026-08-28, newest comment 2026-03-25.

**S50 fixes 425, 554, 563, 564, 589** - ledger entries 17-21, five failing tests skipped
`needs:issue-<n>` in `Gaps/UpstreamIssues/InheritedIssueTests.cs`, all watched failing before being
parked. **554 first:** this port is WORSE than upstream, giving up at n=4,000,000 where upstream
reaches 6,000,000 and stdlib `re` reaches 10,000,000; the bytes go to one
`MatchBodyTailStateData` block per repetition (`Matcher.cs:2679`, `ByteStack.cs:290`).

**Four of the old triage's class C are gone: 334, 551 and 596 do not reproduce, and 367 is not a
bug** - PCRE2 answers as upstream does, and the defect is upstream's documented promise, which is
undecidable. **589 is the reverse and IS a bug**, decided by PCRE2 answering PARTIAL where upstream
answers None. Do not conflate the two; ledger 21 says why a merged report would be rejected.
**397 was already fixed here** by S47's entry-14 guard, and DIVERGENCES now names the issue.

**The lesson of the sitting, and it cost three separate corrections: numbers were quoted more
precisely than the instrument justified.** Two blind passes and the independent verifier each found
one. The port's per-repetition allocation varies with the ORDER of calls, not just run to run - the
backtracking buffer comes from a process-wide pool, so 2,000,000 reps cost 611 B/rep cold and
343 B/rep warm - so only the first call in a fresh process is quotable. Quote a ratio or a range;
find the cause before quoting a figure that moves.

**Owed maintenance (unchanged, none of it S50's scope by default):** `tools/run-controls.py` cannot
measure a control that mutates the recorder, so **S42-2A is owed** and S48b's controls C and D live
in its closing notes; the two broken control sites S32-B and S38-A; `FOLD_TURKIC`'s share of the
`case-folding` rotation; S35-A and S29-A/D are thin; PORTMAP's `_regex.c` line references stale
after the sync; `record-oracle.py --self-check` exits 1 on a pre-S46 message.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
**Housekeeping:** the `s48b-baseline` and `pre-s48b` worktrees can now go.
