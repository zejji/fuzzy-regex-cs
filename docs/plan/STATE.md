# State

Rewritten at the end of every session. Never appended to. Thirty lines maximum.

**S50 IS CLOSED (2026-09-14, one sitting).** Suite 5,968 / 5,968 passing / **0 skipped**, ratchet
GREEN, baseline 5,860, oracle GREEN at seeds 7, 4242, 20260914 and 99991.
**Only 425 shipped. 554, 563, 564 and 589 are parked** - 563/564 and 589 were fixed, reviewed, and
REVERTED. **Next slice is S50b** (Version 1 default).

**Read this before re-attempting any of the three: the diagnosis is the slice's product, and it is in
`InheritedIssueTests.cs` and ledger 19-21, not here.** All five tests are un-skipped and pin the
INHERITED answers plus every row the failed attempts broke, so a fix cannot land silently.

**563 and 564 are ONE bug** - `_regex.c:10214`, `permit_insertion = !search || text_pos !=
search_anchor`, where `search_anchor` is set once per operation (`:3410`) so the rule fires at ONE of
the positions a scan visits. 564 reaches it through BESTMATCH's re-anchoring; the 563 change turned
564 green with no code of its own. Probe: `python tools/probes/issue-563-anchor-rule.py`.
**The RULE that survived both reviews: lift the prohibition only when an assertion held at the anchor
AND fails one character on** (without that, upstream's own `test_fuzzy` 51/52/54/56 redden).
**The DESIGN that failed: a bare `MatchState` flag.** Backtracking never saves or restores it, so it
under-clears (a repeat that gives up its body leaves the pin: ten of twelve probed shapes wrong) and
over-clears (an inert `(?:z|)` after `\m` discards a pin set outside it). It must be backtracking
state, or a compile-time "every path to this fuzzy item passes an assertion" analysis.

**589's sound fix is PCRE2's `hitend`**, not a predicate returning PARTIAL: that ENDS the match
before backtracking finishes, and truncated a capture group on `(\.+?)\1\b`. Its
`ExpectedDivergences` entry then HID the regression - a control went GREEN with the fault present.
**Its left-hand twin `(?r)\b$` over `''` is a PERMANENT pin resting on the maintainer's issue-589
reasoning, which ledger 21 rejects; the two want re-judging together.**

**554 is a performance gap, not a wrong answer**, and Phase 7 owns it: capturing doubles the
per-repetition cost, atomic and possessive do not avoid it, and clamping the 1GB bound would patch
the symptom. **425's two unnamed-first orderings need the maintainer's undecided option 3.**

**Owed maintenance (unchanged):** `tools/run-controls.py` cannot measure a control that mutates the
recorder, so **S42-2A is owed**; the two broken control sites S32-B and S38-A; `FOLD_TURKIC`'s share
of the `case-folding` rotation; S35-A and S29-A/D are thin; PORTMAP's `_regex.c` line references
stale after the sync; `record-oracle.py --self-check` exits 1 on a pre-S46 message.
**Open for the owner:** `slice-log.jsonl` marks S26 `failed`; `origin/main` needs a push.
**Housekeeping:** the `s48b-baseline` and `pre-s48b` worktrees can now go.
