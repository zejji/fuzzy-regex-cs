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

- [ ] Upstream's per-repetition bytes are established to a line, with the instrumented build's
      output quoted.
- [ ] `FullMatch("(ab)*", "ab" * 6_000_000)` succeeds; `InheritedIssueTests` asserts it.
- [ ] Ledger entry 18's status rewritten.
- [ ] The O(1) question put to the owner with numbers, and the answer recorded in DECISIONS.md.
- [ ] Ratchet, oracle at three seeds and AOT green; blind review; commit.
