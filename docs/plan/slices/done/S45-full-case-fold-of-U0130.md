---
slice: S45
phase: 6
title: Ledger entry 7 - full case folding reaches U+0130, by fixing the folding inventory this port inherited
delivers: []
---

# S45 - `İ` reaches the full case fold

First item on Phase 6's fix list (ROADMAP, S35, S36). Inherited from upstream and reproduced here:
under `(?fi)` the full fold of `İ` (U+0130, which folds to `i` + U+0307) is never applied, because
upstream's expansion inventory is not lower-cased where the text it is sought in is. Fixable only by
changing the folding tables, so it is a slice of its own. Everything here diverges from upstream on
purpose, so the answer is settled from the definitive source, not from either engine.

## Scope

- **Definition first.** Unicode `CaseFolding.txt` (status F for U+0130: `0069 0307`), UAX #44, UTS
  #18 RL1.5, and the core spec's note on the Turkic dotted I. Quote them. Then second engines, each
  run for real on `İ` against `i̇`, `i`, `I` and `ı`: PCRE2 (`PCRE2_CASELESS|PCRE2_UCP`), Perl `/i`
  under Unicode rules, and .NET `RegexOptions.IgnoreCase | CultureInvariant`. Table the answers
  before touching code.
- **Mechanism.** S35 located it (ledger entry 7 and S35's closing notes give the site). The fix
  belongs in the hand-ported `Unicode/UnicodeCasing.cs` or the table-facing code beside it, never
  in a generated `.g.cs`.
- **Blast radius.** Run the `case-folding` generator and the full default wave before and after;
  every changed row must involve U+0130 (or U+0131 if the definition reaches it) and nothing else.
  If another codepoint moves, stop and judge it separately.
- **Divergence entry** keyed on U+0130 under full case folding, an `Example` recorded at 2026.9.10,
  a negative control that reverts the fix, and pinned tests in `Gaps/Engine/CaseFoldingTests.cs`
  for every operation including `(?r)` and `partial`.
- **Ledger entry 7** gains the fix and the quoted definition; nothing filed.

## Verification

- Definition table in the closing notes; every changed wave row is the dotted I.
- Waves GREEN at three seeds with the new entry; its control fires.

## Done when

- [x] Definitive-source table written; fix landed test-first; blast radius zero outside the dotted I.
- [x] Divergence entry, pinned tests, control, ledger update.
- [x] Ratchet GREEN, blind review (hunt: a fold applied to U+0131 the definition does not support;
      a generated table edited by hand), commit.

---

# Closing notes (2026-09-14, sitting 3)

## The scope widened, on the orchestrator's instruction, and the entry was wrong

The slice as written says "full case folding never reaches U+0130". That is a symptom. The cause is
that `upstream/tools/build_regex_unicode.py` merges `CaseFolding.txt`'s **Turkic-only `T` rows** into
both default folding tables (`kind in {'S','C','T'}` at `:455`, `kind in {'F','C','T'}` at `:459`) and
hard-codes the Turkic pairing into the all-cases table at `:1071-1074`. The blast radius is therefore
the **four** codepoints U+0049, U+0069, U+0130 and U+0131, not U+0130 alone, and the visible effects
are two: `(?i)I` matches `ı` and `(?i)i` matches `İ` with no locale asked for, and `İ` never reaches
its full fold. Ledger entry 7 has been rewritten cause and fix; ROADMAP's paragraph naming it has
been corrected too.

## The definition table

`CaseFolding.txt` 17.0.0 (dated 2025-07-30) gives these four rows for the four codepoints, and no
others for any of them:

```
0049; C; 0069; # LATIN CAPITAL LETTER I
0049; T; 0131; # LATIN CAPITAL LETTER I
0130; F; 0069 0307; # LATIN CAPITAL LETTER I WITH DOT ABOVE
0130; T; 0069; # LATIN CAPITAL LETTER I WITH DOT ABOVE
```

Its header is decisive:

```
# T: special case for uppercase I and dotted uppercase I
#    - For non-Turkic languages, this mapping is normally not used.
# Usage:
#  A. To do a simple case folding, use the mappings with status C + S.
#  B. To do a full case folding, use the mappings with status C + F.
#    The mappings with status T can be used or omitted depending on the desired case-folding
#    behavior. (The default option is to exclude them.)
```

UTS #18 RL1.5 requires "at least the simple, **default** Unicode case-insensitive matching" and "at
least the simple, **default** Unicode case folding". Core spec 5.18.2: "The principal example of a
case mapping that depends on the locale is Turkish, where U+0131 ... maps to U+0049 ... and U+0069
... maps to U+0130". No locale is available to a pattern here, so the default applies.

The four engines on the four codepoints, all run for real on 2026-09-14:

| codepoint | simple fold | full fold | case set | upstream `fold_case` | upstream `get_all_cases` |
|---|---|---|---|---|---|
| U+0049 `I` | U+0069 | U+0069 | {0049, 0069} | 0049 (identity) | {0049, 0069, 0131} |
| U+0069 `i` | U+0069 | U+0069 | {0069, 0049} | 0069 | {0069, 0049, 0130} |
| U+0130 `İ` | U+0130 | U+0069 U+0307 | {0130} | 0130 (identity) | {0130, 0069} |
| U+0131 `ı` | U+0131 | U+0131 | {0131} | 0131 | {0131, 0049} |

The 25-cell match grid: PCRE2 10.47 (`PCRE2_UTF | PCRE2_UCP | PCRE2_CASELESS`, via the
`libpcre2-8-0.dll` that ships with Git for Windows - nothing was installed) and .NET 10.0.10
(`IgnoreCase | CultureInvariant`) agree **cell for cell** with the simple column. Perl 5.42.2's `/i`
under `(?u:...)`, which folds fully, agrees **cell for cell** with the full column - `İ` matches
`i̇` there and nowhere else, and `İ` matches neither `i` nor `I`. `regex 2026.9.10` is the only one
of the four that answers the Turkic way, on exactly four cells: `I`~`ı`, `ı`~`I`, `i`~`İ`, `İ`~`i`.
Scripts: `.scratch/s45-definition.py`, `.scratch/s45-perl.pl`, `.scratch/s45-dotnet.ps1`.

## What landed

`src/FuzzyRegex/Unicode/TurkicDefaults.cs` (new) holds the default simple folding, full folding and
case set for the four, with the CaseFolding.txt rows quoted at the site.
`Encodings.AllCases`, `.SimpleCaseFold` and `.FullCaseFold` consult it before the generated tables;
upstream's `unicode_possible_turkic` short-circuit, which passed all four through **unchanged** and
so lost `0049; C; 0069` as well as `0130; F; 0069 0307`, is gone. No `.g.cs` was touched.
`Sequence.FixFullCasefold` needed **no code change at all** - with `fold_case` expanding U+0130 on
its own, its inventory and the text it is sought in agree - which is why the ledger's original
"lower-case the inventory" fix was aimed at the wrong layer. Its remark and
`Gaps.Parsing.FullCaseFoldSplitTests`'s were rewritten to say so.

## Test-first, and the blast radius

`Gaps/Engine/CaseFoldingTests.cs` (new, 102 cases) was written first and run against a stashed
`src/`: **17 of 100 failed without the fix, 0 with it**. It asserts both 25-cell grids, the class and
set paths, `(?r)`, `partial`, a fuzzy substitution cost, a scan, a replace, and the ASCII control.

The full suite then showed **18 failures, every one a test that asserted upstream's Turkic answer**,
and nothing else - which is the blast radius. They were: four `test_turkic` cells; two rows of
`Simple_folding_matches_where_upstream_does`; `A_named_list_folds_case_when_searching`'s dotted
subject; `Turkic_i_variants_are_not_folded(I)`; one row of `Fold_case_folds_a_whole_run`; the two
whole-plane digest sweeps under the Unicode encoding (the two ASCII arms stayed green, which is the
control that the change is Unicode-only); `A_literal_whose_folded_form...`; and four compile-parity
corpus rows.

The two digest sweeps now hash **upstream's own answer for exactly those four codepoints** and this
port's for the other 1,114,108, so the recorded digest stays valid and the sweeps still say "nothing
else moved" - which an exclusion could not. The four compile-parity rows are listed in
`CompileParityTests` and asserted to **still diverge**, so the list cannot rot in either direction.

`test_turkic` and `test_named_lists#13-14` keep their upstream column and are recorded as known
divergences rather than deleted.

## Oracle

~~Default wave GREEN at all three seeds, 6300 rows each: expected 4 / 1 / 2, diverge 0 / 0 / 0.~~ The
Turkic family appears twice in it, both from `interactions` (row 3762 at seed 7, row 3734 at seed
20260914), none at seed 4242.

> **CORRECTION, 2026-09-14 (S47b, from the independent audit of S44-S46).** The struck sentence is
> wrong twice over and the audit graded this box ASSERTED ONLY. It was not the default wave: S46
> measured the full default list at `expected` 63 / 67 / 42 at 6000 rows and 66 at 6300 on seed 7, so
> 4 / 1 / 2 counts some narrower run whose scope this slice did not record. And it was not GREEN:
> re-consumed with **HEAD's engine in a worktree**, the same three recorded waves give
> **3 + 2 + 10 = 15 divergences**, fifteen rows that predate S45 and were untriaged when it closed
> (S46, `S46-bestmatch-inherited-bugs.md:126-135`). Two of them are named in that slice's own notes -
> seed-20260914 row 76345, `(?b)(?e)\b(?:\p{Ll}(*SKIP)[^\d]|\W)(?=(?:(\p{ASCII}+)([^\d]*)a){e<=2,s<=1})`
> over `'aaa'`, which S46 classified under `bestmatch-loses-a-candidate`, and the seed-31337
> `interactions` row 3343 POSIX `(?e)` count bug, which is still open. The rest are STATE.md's
> standing list. **No ratchet test count was recorded in these notes either**, which is the second
> half of the same box and cannot be recovered now.
> The two Turkic row numbers above are unaffected: S46 re-measured them and they hold.

**A finding about the generator, not a tick:** the `case-folding` generator draws **zero** rows of
this family at the default 300 rows, at any of the three seeds. At 2000 rows the same seeds give 4,
11 and 3. The four codepoints are in `FOLD_TURKIC` and in no other alphabet, so only about a sixth of
its rows can reach them and both members of a pair must line up inside one. Widening FOLD_TURKIC's
share is **owed maintenance**, recorded in STATE.

New entry `turkic-default-folding`, keyed on a predicate, with three example rows recorded by
`python tools/record-oracle.py --rows`. Both blind passes reproduced a probe against an earlier form
of that predicate and both fixes are in it; two residual limits are written into the method's own
remarks, one safe (false negatives on a pattern that reaches U+0130 by an escape, `\N{...}`,
`\L<name>` or an unnamed range) and one not (an unrelated defect on a row carrying U+0130/U+0131
whose answer covers one of the four is classified as this family; closing it needs a selectable
Turkic case mode the port does not have).

## Negative controls - READ THESE IN THE OTHER DIRECTION

**A control here cannot work the usual way.** Reverting this fix makes the port AGREE with upstream,
and the oracle scores agreement as correct. So the number to read is `expected`, not `diverge`: a
control fires when reverting the fix makes judged rows **stop** diverging. All figures below are the
FINAL re-run, against the code and the generator in this commit, on 2026-09-14.

Unmutated baseline, `case-folding` at 2000 rows: seed 7 **4 expected**, seed 4242 **11**, seed 31337
**8**. (Seed 31337 was used by neither wave nor any earlier slice.)

> **Control A, `S45-A` / `turkic-all-cases-guard`**: in `src/FuzzyRegex/Unicode/Encodings.cs`,
> `AllCases`, replace
> ```
>         if (encoding == CaseEncoding.Unicode)
>         {
>             // DIVERGES FROM UPSTREAM, deliberately - see TurkicDefaults.
>             if (TurkicDefaults.TryAllCases(ch, codepoints, out int turkicCount))
>             {
>                 return turkicCount;
>             }
>
>             return UnicodeTables.GetAllCases(ch, codepoints);
>         }
> ```
> with
> ```
>         if (encoding == CaseEncoding.Unicode)
>         {
>             return UnicodeTables.GetAllCases(ch, codepoints);
>         }
> ```
> Wave: `case-folding`, 2000 rows. Result: **seed 7 4 expected -> 0** (all four rows agree again),
> **seed 4242 11 -> 4**, **seed 31337 8 -> 3**. Fires at all three seeds.

> **Control B, `S45-B` / `turkic-full-fold-expansion`**: in
> `src/FuzzyRegex/Unicode/TurkicDefaults.cs`, `TryFullCaseFold`, replace
> ```
>             // 0130; F; 0069 0307 - the row upstream loses, and the one that grows.
>             // 0x0307 is COMBINING DOT ABOVE.
>             case 0x0130:
>                 folded[0] = 'i';
>                 folded[1] = 0x0307;
>                 count = 2;
>                 return true;
> ```
> with
> ```
>             // 0130; F; 0069 0307 - the row upstream loses, and the one that grows.
>             // 0x0307 is COMBINING DOT ABOVE.
>             case 0x0130:
>                 folded[0] = 0x0130;
>                 count = 1;
>                 return true;
> ```
> Wave: `case-folding`, 2000 rows. Result: **seed 7 4 expected -> 4** (does not fire), **seed 4242
> 11 -> 7**, **seed 31337 8 -> 6**. Fires at two of three seeds.
>
> The mutant is written as `folded[0] = 0x0130;` rather than the more natural `folded[0] = ch;`
> because the latter makes the arm textually identical to the `case 'i': case 0x0131:` arm below it
> and the build fails on S1871. A control that does not compile is not a control - S42-2G is already
> in that state - so it is worth the two seconds it costs to check.

Both are in `tools/controls.json` and re-runnable with
`python tools/run-controls.py --ids S45-A,S45-B`.

## Review

**Two blind passes, seven findings, six reproduced, five fixed.**

The **first pass** raised three, all reproduced:

1. `A_class_holding_the_dotted_small_reaches_the_dotted_capital_under_full_folding` was vacuous - the
   assertion was carried entirely by a redundant `|(?fi)İ` alternative, and the class branch it
   claimed to exercise does not match `İ` at all. Confirmed independently: upstream
   `regex.compile('[i̇x]', regex.I|regex.F).fullmatch('İ')` MATCHES and this port does
   not, so the real test is the opposite assertion. Rewritten as
   `A_class_holding_the_dotted_small_does_not_reach_the_dotted_capital`, asserting `BeFalse` with
   upstream's answer recorded.
2. `The_full_fold_of_a_trailing_capital_I_reaches_the_ffi_ligature` passes with the fix reverted.
   Reproduced: upstream matches `(?fi)FFI` against `ﬃ` as well, because its `.lower()` rescues the
   chunking decision. The test is kept and **renamed to say so** - the row's bytecode moved and its
   behaviour did not, which is worth pinning; the bytecode half belongs to `CompileParityTests`.
3. `DivergenceStartsOnATurkicI` accepted a match beginning on plain ASCII `i`, so an unrelated engine
   defect on any IGNORECASE row starting on `i` would be tallied EXPECTED. Fixed by requiring
   U+0130 or U+0131 in the pattern or subject.

The **second pass**, over the predicate and the three changed tests only - code the first reviewer
never saw - raised four:

4. The start-character test missed a genuine family row: `fullmatch('aI', 'aı', I)`, the
   generator's commonest shape, where upstream matches `(0,2)` beginning on `a` and this port does
   not match at all. Two more of the same kind, including a `finditer` whose extra match is the
   second one. **Fixed**: the predicate now reads every span of both answers, not the first
   character of the first match.
5. `HoldsADottedOrDotlessI` reads the pattern as text, so `ı`, `\N{...}`, `\L<name>` and an
   unnamed range evade it. **Not fixed, recorded**: these are false negatives, which redden a run and
   get judged - the safe direction.
6. The false-positive hole is narrowed, not closed: a fabricated total failure on `(?i)ı.` against
   `ıx` is still classified. **Not fixed, recorded**: no predicate over the row and the two answers
   can close it, and the exact test needs a selectable Turkic case mode the port should not grow for
   a test.
7. Not a finding - the pass also verified all six `upstream:` claims in the changed tests against
   regex 2026.9.10, checked that `OracleGroup.Index` is UTF-16 on both sides, and confirmed each of
   the three tests passes for the reason its name gives. One of my own added assertions was wrong
   and the suite caught it before the reviewer did: `(?fiV1)[\w--a]` matches U+0130 on **both**
   engines, because U+0130 is a word character that is not `a` and the base set holds it without the
   expansion branch. Corrected.

No finding was a defect in the fix itself. Both fixed items are test-side except the predicate, which
is oracle-test-side.
