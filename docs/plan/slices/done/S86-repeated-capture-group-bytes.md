---
slice: S86
phase: 6
title: A repeated capture group costs a bounded number of bytes per repetition
delivers: []
---

# S86 - Ledger entry 18: bytes per repetition

S57 assigned ledger entry 18 to S61. S61 measured it and handed it here in writing (2026-09-23),
because the fix is an engine change to the repeat opcodes, not the per-match allocation work S61
was about. Phase 6's inherited-bug list does not close until this lands.

## The defect

`FullMatch("(ab)*", "ab" * 4_000_000)` throws the 1 GB backtracking-bound
`InvalidOperationException`. Upstream `regex` 2026.9.10 still matches at 6,000,000 and gives
`MemoryError` at 10,000,000 (`docs/plan/upstream-reports/LEDGER.md` entry 18).

## What S61 measured (2026-09-23)

Backtrack-stack (`Bstack`) bytes per repetition in this port, read off the stack's length after a
full match. `Sstack` stays 0 and `Pstack` 8 throughout.

| Pattern | Bytes per repetition |
|---|---|
| `(ab)*` | 151 |
| `(?:ab)*` | 107 |
| `(a)*` | 151 |
| `(?:a\|b)*` | 34 B in total, not per repetition |

So the capture adds 44 B a repetition, and an uncaptured repeat still costs 107 B.

Upstream's `END_GREEDY_REPEAT` (`upstream/src/_regex.c:12525-12660`) pushes the same three records
the port does: BODY_END (32 + 1 B), MATCH_TAIL (56 + 1 B, because `try_match` at `:7671` takes its
default case for the tail) and BODY_START (a 4-byte `RE_CODE` index + 8 + 1 = 13 B). That is 103 B a
repetition against the port's 107. Upstream has the same 1 GB cap on the doubled capacity
(`RE_MEMORY_LIMIT` 0x40000000 at `:40`, checked at `:2357`).

The puzzle: to reach 6,000,000 repetitions under a doubling 1 GB cap, upstream must use under 89 B
a repetition on its backtrack stack, and the push count above says it uses 103. Something in
upstream is cheaper than this reading of the source, and it has not been found. Do not design a fix
until it is.

## Scope

1. **Find where upstream's bytes go.** Instrument the `/Od /Zi` MSVC build S47c installed: log the
   backtrack stack's size and capacity per repetition of `(ab)*` and `(?:ab)*`, and set a
   breakpoint on the push that grows it. Quote the numbers. This settles whether the port has a
   port bug (a push upstream does not make, or a bigger record) or upstream has an optimisation the
   port lacks.
2. **Fix to parity at least.** If step 1 finds a port difference, port upstream's behaviour and
   record the symbol in `docs/PORTMAP.md`. Then `FullMatch("(ab)*", "ab" * 6_000_000)` succeeds, as
   `InheritedIssueTests.cs:116-118` says the test should be rewritten to.
3. **Then decide on O(1).** A body with no alternative has nothing to backtrack into, so the
   ledger's case for O(1) state per repetition stands. That is a divergence from upstream and
   needs the owner's decision before any code. Bring the measured numbers from steps 1 and 2 to it.

## Done when

- [x] Upstream's per-repetition bytes are established to a line, with the instrumented build's
      output quoted.
- [x] `FullMatch("(ab)*", "ab" * 6_000_000)` succeeds; `InheritedIssueTests` asserts it.
- [x] Ledger entry 18's status rewritten.
- [ ] The O(1) question put to the owner with numbers (done, DECISIONS 2026-09-24), and the answer
      recorded in DECISIONS.md (open: the owner has not answered).
- [ ] Ratchet, oracle at three seeds and AOT green; blind review; commit.

## Closing notes (2026-09-24, one sitting)

**What landed.** The port now puts exactly upstream's bytes on its backtracking stack for a repeat:
82 B a repetition for `fullmatch('(ab)*')` (was 151) and 46 B for `(?:ab)*` (was 107).
`FullMatch("(ab)*", "ab" * 6_000_000)` succeeds in about 2 s, and 10,000,000 fails at the 1GB
bound where upstream raises `MemoryError`.

**Where upstream's bytes go.** `python tools/probes/upstream-repeat-bytes.py 1000` builds a
`/Od /Zi` copy of the pinned source whose four byte-stack primitives count every push and pop by
call line, and logs each growth of `state->bstack`. Its output, trimmed:

    == (ab)* over 'ab' * 1000              == (?:ab)* over 'ab' * 1000
    PEAK bstack 82055                      PEAK bstack 46055
    ROW b + line 2503 bytes 1 calls 4004   ROW b + line 2503 bytes 1 calls 2004
    ROW b + line 2526 bytes 4 calls 1001   ROW b + line 2526 bytes 4 calls 1001
    ROW b + line 2514 bytes 8 calls 1001   ROW b + line 2514 bytes 8 calls 1001
    ROW b + line 12784 bytes 32 calls 1000 ROW b + line 12678 bytes 32 calls 1000
    ROW b + line 12678 bytes 32 calls 1000

Line numbers are in the patched copy; 12678 is BODY_END (`_regex.c:12616`), 12784 is END_GROUP's
record (`:12722`), 2526 is `push_code`, 2514 `push_ssize`. No `MATCH_TAIL` push appears at all,
which is S61's puzzle solved: under a full match the tail is the SUCCESS node, and `try_match`'s
SUCCESS arm (`:7828`) refuses it short of the end. Ledger entry 18 has the per-record table.

**The three port differences, and the fixes.**
1. `TryMatch` had no SUCCESS arm, so each repetition parked a 57-byte `MATCH_TAIL` record. Ported;
   it fails exactly where the port's SUCCESS opcode would (slice bounds, not upstream's
   `text_start` in reverse), so it cannot change an answer.
2. BODY_START and TAIL_START pushed the `RE_CODE` index 8 bytes wide. `ByteStack.PushCode`/`PopCode`
   port `push_code`/`pop_code`; the 10 pushes and 2 pops match upstream's 10 and 2.
3. The group record was five 8-byte words; it now has upstream's widths, 32 bytes.

**For the next slice.** S60b's `TryMatch` rewrite (`52ba79b` on `slice/s60b`) sends SUCCESS to the
default arm with a comment saying entering is always right. It is right for answers and wrong for
memory. Whichever of S60b and S86 merges second must keep this arm; `RepeatTests`'s byte test goes
red if it is lost (DECISIONS 2026-09-24).

**Timing, for the orchestrator's quiet run.** Allocated and stack bytes are deterministic and are
judged above. The one hot-path change is a compare on `test.Op` in `TryMatch`, which every branch
and repeat entry calls. Filter for the quiet gate: `*WorkloadBenchmarks*`. No `--job medium` run was
made here, as instructed.

**The O(1) question is open.** Put to the owner in DECISIONS 2026-09-24 with the numbers; no code.
That box stays unticked until the owner answers.

**Suite red check.** The new byte test failed before any engine change (151,059 and 107,059
against 82,055 and 46,055), then at 94,059 and 50,059 with only the SUCCESS arm, and passes with all
three fixes.

**Gates.** Ratchet GREEN, 6783 passing. Default oracle wave GREEN at seeds 7, 4242 and 20260924.
AOT GREEN, 6780 passing and 3 skipped.

**Review.** One blind pass (Opus, the VERIFICATION.md brief) over the whole diff: "No defects
found." It checked every BODY_START, TAIL_START and group-record push against its pop, the stack
save and restore of atomic groups, lookarounds, conditionals and group calls (all by byte count,
so they never read these records), and the uint casts. It also compared this code against a copy
with every SUCCESS test node nulled out, which behaves as the code did before the change: 65,340
cases over 128 patterns (reversed, POSIX, BESTMATCH, ENHANCEMATCH, fuzzy, verbs, recursion), 23
subjects, four pos/endpos pairs, partial on and off, and fullmatch, match and search. Zero
differences. It could not run Python or the probe, so the upstream byte figures rest on this
session's probe run, quoted above. No findings, so no fixes and no second pass. No divergence was
judged, so there is no verifier step.
