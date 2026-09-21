---
slice: S57e
phase: 6
title: The insertion position a reversed best match reports, and the generator waiting on it
delivers: []
---

# S57e - one unjudged oracle row, and `fuzzy-anchored` back on the default list

S57c added the `fuzzy-anchored` generator (`fuzzy` with a zero-width assertion in front of the
whole pattern) and it immediately found a row S57c did not judge. The row is nothing to do with
S57c's rule, so it was left for its own slice, and the generator is off the default list in
`tools/run-oracle.ps1` until this slice closes.

**The row.** `pwsh -File tools/run-oracle.ps1 -Generator fuzzy-anchored -Count 2000 -Seeds 1234567`,
row 1982, measured 2026-09-21:

```
DIVERGE row 1982 (fuzzy-anchored) finditer flags=0x0 version=V0
  pattern  '(?b)(?r)\m(?:.fo){e<=2}'
  subject  'x fx'
  upstream matches 1 | match 0:(0,4)[(0,4)] last=-1/- fuzzy=(1,1,0)[s:4][i:2][d:]
  port     matches 1 | match 0:(0,4)[(0,4)] last=-1/- fuzzy=(1,1,0)[s:4][i:1][d:]
```

Same span, same error counts, same substitution position. The engines disagree only on where the
inserted character is recorded: upstream index 2, this port index 1.

**What is already known.**

- S57c's fix is not the cause. Under `(?r)` the leading `\m` is not at the head of the reversed
  graph, so `PatternObject.AnchorGuards` is empty on this row and `Matcher.AnchorIsPinned` never
  runs. An ablation that empties the field leaves both answers unchanged.
- The `(?b)` flag is what moves upstream's answer. Measured on regex 2026.9.10, same span and
  counts either way:

  ```
  (?r)\m(?:.fo){e<=2}     over 'x fx' -> changes=([4], [1], [])
  (?b)(?r)\m(?:.fo){e<=2} over 'x fx' -> changes=([4], [2], [])
  ```

  So upstream itself reports two different insertion positions for the same span, the same counts
  and the same pattern, according to whether the best-match pass re-ran the match. That is the
  first place to look, and it is the same re-anchoring machinery that produced issue 564.
- Only the `(?b)` + `(?r)` + leading-`\m` combination diverges. Dropping `(?b)`, dropping `\m`,
  swapping `\m` for `\b`, or using the subject `'xfx'` all agree.

## Scope

1. **Judge the row to amendment 16's standard.** Which engine is right about the recorded position,
   with the mechanism named in upstream's source, a second engine or upstream's documentation where
   either can speak to it, the blind review and the independent verifier.
2. **Then either fix the port or pin the divergence**, with a permanent minimised test carrying its
   provenance and, if it is a deliberate difference, a `docs/DIVERGENCES.md` row.
3. **Put `fuzzy-anchored` back on the default generator list** in `tools/run-oracle.ps1`, and delete
   the paragraph there and in `tools/record-oracle.py` that explains why it was held out.
4. **Nothing else.** `match.fuzzy_changes` ordering questions that this row does not raise belong to
   whatever finds them.

## Verification

- `pwsh -File tools/run-oracle.ps1 -Generator fuzzy-anchored -Count 2000 -Seeds 1234567` GREEN, and
  the same at 7, 4242 and one seed this slice has not used.
- `pwsh -File tools/run-oracle.ps1` GREEN at three seeds with the generator back on the list, and
  `-Count 6000` GREEN at three seeds.
- `pwsh -File tools/check-ratchet.ps1` GREEN.

## Done when

- [ ] The row judged, with the mechanism and the verifier's CONFIRMED lines.
- [ ] Fixed or pinned, with a permanent test and a `DIVERGENCES.md` row if it is a pin.
- [ ] `fuzzy-anchored` on the default list, both hold-out paragraphs deleted.
- [ ] Both waves green at three seeds; blind review; commit.
