---
slice: S57d
phase: 6
title: A partial fullmatch that denies a prefix whose completion exists - PCRE2's hitend model, and the left-hand twin decided with it
delivers: []
---

# S57d - Ledger entry 21

The second of the two inherited bugs Phase 6's fix list still holds, and the one whose fix is a
design rather than a patch. S50 wrote a fix, measured it green, and its own blind review broke it;
the verdict survived and the mechanism did not.

**The symptom.** Under `partial`, a word or grapheme boundary at the end of the available text is
decided against that text, so a prefix whose completion exists is denied. Measured 2026-09-14,
identical on `regex` 2026.9.10 and here:

```python
>>> a = regex.compile(r"(?!(True|False)\b)(.*)")
>>> a.fullmatch("Truest")            # a complete match exists
<regex.Match object; span=(0, 6), match='Truest'>
>>> a.fullmatch("True", partial=True)
None                                 # yet its own prefix is denied
```

**The verdict is settled and is not this slice's to re-litigate.** Upstream's own definition
(`upstream/docs/Features.html:576`) asks whether a complete match would be possible had the string
not been truncated, and one is. PCRE2 10.47 answers PARTIAL on all three probe rows and, on
`True\b`, reports a definite match under SOFT and a partial under HARD - so it treats a boundary at
the end of available text as unresolved and escalates rather than deciding it against text the
caller has declared truncated. The maintainer considers the behaviour correct; ledger entry 21
disagrees, with his documentation and a second engine, and S50's failure did not touch that.

## Scope

1. **Implement PCRE2's model, not a predicate tweak.** A boundary that reaches the end of the
   available text sets a `hitend`-style flag meaning "the end of the subject was reached while
   deciding". Matching and backtracking then run to completion, and only a FINAL failure becomes a
   partial. A definite complete match still wins.
2. **Decide which span a hitend-derived partial reports**, and write the reasoning down. This is the
   design question the model forces and the reason the entry calls it a slice.
3. **Keep both of S50's narrowings, which measurement forced rather than taste:**
   - **The attempt must have consumed something.** Escalating at a position the match has not reached
     turns every `\b`-leading pattern into a zero-width partial on a short or empty subject: without
     the clause, a default three-seed 6300-row wave went from 0 divergences to 8, 5 and 5, and all
     eight at seed 7 were that shape. It is also the answer this port has already judged wrong - the
     `search-start-partial` pin refuses a zero-width partial at the truncation point.
   - **The left-hand twin is decided in the same slice.** `(?r)\b$` over `''` is the mirror image and
     is currently a PERMANENT divergence, decided 2026-09-12 on reasoning that quotes the
     maintainer's issue-589 argument - the argument entry 21 rejects. Fixing one side and leaving the
     other is not a principled place to stop, and S50's attempt did exactly that. **Re-judge that pin
     to amendment 16's standard**: it is the one place this slice may overturn an earlier verdict,
     and it must do so explicitly, with the second-engine run and the blind review, or say in writing
     why it stands.
4. **The regression that killed the first attempt is the acceptance test.** Returning `PARTIAL` from
   a predicate ended the match before backtracking finished and truncated a capture group:

   ```
   search(r'(\.+?)\1\b', '..',   partial=True)  -> group 1 became (0, 1); upstream and HEAD give (0, 2)
   search(r'(\.+?)\1\b', '....', partial=True)  -> group 1 became (0, 2); upstream and HEAD give (0, 3)
   ```

   Both rows are already pinned in `Gaps/UpstreamIssues/InheritedIssueTests.cs`. They stay green
   throughout, and a design that cannot keep them is the wrong design.
5. **An `ExpectedDivergences` entry that looks inside a capture group.** The entry S50 wrote for the
   reverted fix classified that regression as expected, because its predicate compared overall spans
   only. Write the negative control first (DECISIONS 2026-09-12), and make the discriminator reach
   the captures.
6. **The inherited pin inverts.** `A_partial_fullmatch_still_denies_a_prefix_whose_completion_matches`
   asserts the bug; rewrite it to assert the fixed answer with its provenance, and add a
   `docs/DIVERGENCES.md` row, since this port then answers where upstream does not.
7. **Nothing else.** Entries 19 and 20 are S57c's and entry 18 is S61's.

## Verification

- `python tools/probes/pcre2-partial-truncation-assertions.py` re-run, with the three rows quoted,
  and the port's answers beside them.
- `dotnet run --project tests/FuzzyRegex.Tests -c Release -- --treenode-filter
  "/*/*/InheritedIssueTests/*"`, and the same for `PartialMatchingTests` and `ReverseMatchingTests`.
- `pwsh -File tools/check-ratchet.ps1` GREEN.
- `pwsh -File tools/run-oracle.ps1` GREEN at three seeds, with `partial-sliced` run explicitly -
  that generator is what caught the reverted attempt - and `-Count 6000` GREEN at three seeds.
- A negative control that restores the fault, recorded with its snippet, generator, row count and
  two seeds. S50's own 8/5/5 figure is the shape to beat, not to reuse.

## Done when

- [ ] The hitend model implemented, with the reported-span decision written down and argued.
- [ ] Both narrowings kept, each with its measurement re-run rather than quoted.
- [ ] The left-hand `(?r)\b$` twin re-judged to amendment 16's standard, overturned or upheld in
      writing, with the verifier run over the verdict.
- [ ] The two `(\.+?)\1\b` capture rows green throughout; the inherited pin inverted with provenance.
- [ ] `DIVERGENCES.md` row and an `ExpectedDivergences` entry whose discriminator reads captures, the
      negative control written first.
- [ ] Ratchet, both waves green at three seeds; blind review (hunt: a predicate that ends the match
      instead of flagging it; a zero-width partial at the truncation point; an expected-divergence
      predicate that cannot see inside a group), commit.
