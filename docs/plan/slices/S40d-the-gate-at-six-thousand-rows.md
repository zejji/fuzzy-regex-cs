---
slice: S40d
phase: 5
title: The last two reversed-(*SKIP) shapes, and the 6000-row three-seed gate S40a could not reach
delivers: []
---

# S40d - the gate

**S40a's exit gate, held back until S40b and S40c have cleared their eleven rows.** Running it
before then measures nothing: a wave red on three mechanisms cannot tell a new divergence from an
old one, which is the same argument S37 made for clearing `interactions` before S43 widened it.

## Scope

### 1. The two reversed-`(*SKIP)` shapes S40a measured and deliberately left red

Both are the carried slice of ledger entry 5, and both are refuted - but not by anything the ROW
carries, which is why neither was classified. `python tools/probes/upstream-reversed-skip-scan-shapes.py`
re-runs all three rows of the family, including the one that is classified, so the difference
between them is visible in one output.

**Seed 20260913 row 116388, a substitution.** `(?r)(?:\d*?(*SKIP)𝔘|a)$` over `'aa𝔘𝔘'`: upstream
replaces 3 times and this port once. A `SubOutcome` carries a string and a count and no match
positions, so the `$` tell has nothing to read. Asked separately, upstream replaces at (3,4), (2,3)
and (1,2) where `$` is true at the end alone, and `(*PRUNE)` gives this port's single replacement.
The honest options are a tell that works on a substitution row, or the recorder recording the spans
a substitution replaced at.

**Seed 4242 row 117071, an overlapped scan with no `$` and no groups.**
`(?r)\w{1,3}?(*SKIP).(?:\p{L}(*SKIP)){2,3}` over `'_ ___𐐀𐐀𐐀'`: upstream reports (3,8), (3,7), (3,6)
in codepoints and this port the first alone. Neither tell exists. What refutes it is upstream's own
reversed search over the truncated subject - `search(S, 0, 7)` is None for a match it reports as
ending at 7 - and that is legitimate HERE because this pattern holds no end-sensitive item.

**So the real work is the reversed `anchoredScan` the recorder refuses** (`_anchored_scan`,
tools/record-oracle.py). It refuses because moving `endpos` truncates the subject and changes what
`$`, `\Z`, `\b` and `\B` mean - but that is a property of the PATTERN, not of reversal, and a
pattern holding none of them can be walked safely. Narrow the refusal to patterns that carry an
end-sensitive item, record the walk for the rest, and the row itself then carries its refutation.
Judge the widening by what it would classify, not only by what it unblocks.

### 2. The gate

`pwsh -File tools/run-oracle.ps1 -Count 6000` - three seeds, 126,000 rows a seed. S40a's last
measurement, with the recorder timeout in and the per-match slice reset in:

| seed | agree | expected | timeout | diverge |
|---|---:|---:|---:|---:|
| 7 | 125,960 | 37 | 0 | 3 |
| 4242 | 125,944 | 50 | 1 | 5 |
| 20260913 | 125,942 | 51 | 0 | 7 |

S40a session 2 classified two of the fifteen and then **re-ran the whole gate on the committed
code**: 3 + 5 + 5 = **thirteen**, with rows 93133 and 116766 now `EXPECTED`. Four are S40b's, seven
are S40c's, two are this slice's, and the per-seed rows are named in those files.

**The run itself is cheap and the slice should not budget for it.** Measured 2026-09-13: 126,000
rows records in 17 seconds and consumes in 6, so all three seeds together are about a minute. The
forty minutes S40 lost was the hanging row, which the recorder's timeout now records instead. What
this slice costs is judging two rows, not running a wave.

## Done when

- [ ] Rows 116388 and 117071 are classified or fixed, with the reason, and the recorder change (if
      that is the route) has a control that fires.
- [ ] The default wave is GREEN at three seeds at 6000 rows a generator.
- [ ] Ratchet GREEN, baseline updated, blind review, commit.
