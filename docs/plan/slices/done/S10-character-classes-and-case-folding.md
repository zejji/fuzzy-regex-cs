---
slice: S10
phase: 2
title: Character classes, properties, case folding and inline flags
delivers: []
---

# S10 - Character classes, properties, case folding and inline flags

Needs S09. The biggest slice of the phase by corpus rows: case-insensitive matching alone is in
most of the 32 flag combinations the corpus records.

## Scope

`src/FuzzyRegex/Parsing/`, mirroring `upstream/regex/_regex_core.py`.

- **Sets**: `parse_set`, `parse_set_union`, `parse_set_symm_diff`, `parse_set_inter`,
  `parse_set_diff`, `parse_set_imp_union`, `parse_set_member`, `parse_set_item`,
  `parse_posix_class`, `SET_OPS` (`:1511-1688`, `:183`); `SetBase`, `SetDiff`, `SetInter`,
  `SetSymDiff`, `SetUnion` (`:3715-3987`) including `_handle_case_folding` and `max_width`;
  `Range` (`:3372-3449`) including its `optimise`, which is where `get_expand_on_folding` and
  `fold_case` are consulted for ranges.
- **Properties**: `Property` (`:3310-3366`), `parse_property`, `parse_property_name`,
  `lookup_property`, `standardise_name`, `_POSIX_CLASSES`, `_BINARY_VALUES`,
  `float_to_rational`, `numeric_to_rational` (`:1453-1511`, `:1688-1800`); `make_property`
  (`:445`); the property escapes `\d \D \w \W \s \S \p{..} \P{..}` in `parse_escape`
  (`:1256-1339`); `\N{name}` via `parse_named_char` (`:1437`) on the S09 name table.
- **Case folding** everywhere it is consulted: `_fold_case`, `is_cased_i`, `is_cased_f`,
  `make_case_flags`, `make_character` (`:354-437`), `CASE_FLAGS_COMBINATIONS` (`:205`),
  `Character` and `String` under `IGNORECASE` and `FULLCASE` (the `_IGN` and `_FLD` opcode
  variants), `Sequence._fix_full_casefold` (`:3637`), `Branch._is_folded` and `_is_full_case`
  (`:2487-2517`, replacing the S08 placeholder), `String.folded_characters` (`:4021`) and its use
  in `_get_required_string`.
- **Flags**: `parse_flag_set`, `parse_flags`, `parse_subpattern`, `parse_flags_subpattern`,
  `parse_positional_flags`, `REGEX_FLAGS` (`:1133-1225`, `:193`): `(?i)`, `(?i:...)`,
  `(?-i:...)`, `(?x)` with `Source.ignore_space`, `(?s)`, `(?m)`, `(?f)`, `(?w)`, `(?r)`, `(?p)`,
  `(?b)`, `(?e)`, `(?V0)`/`(?V1)`, `(?a)`/`(?u)`/`(?L)`, and the `_UnscopedFlagSet` retry that
  S07 wired. Settle against the oracle what `(?L)` does with a `str` pattern and port exactly
  that. `AnyU`, `EndOfLineU`, `StartOfLineU`, `EndOfStringLineU` and `DefaultBoundary` now have
  the flags that select them.

## Verification

Corpus rows using sets, escapes, properties, case-insensitive flags and inline flags now pass.
This is where a wrong Unicode table would first show as a wrong byte, so a corpus failure here
has two candidate causes: check the S09 fixture tests are still green before suspecting the
parser.

`Range.optimise` and `SetBase._handle_case_folding` build lists from `get_all_cases` and
`get_expand_on_folding` in a specific order; that order is upstream's table order, not set order,
so it must reproduce exactly. If a row disagrees only in the order of set members, suspect a
third set-order leak (S06 found two) before suspecting the folding tables.

## Done when

- [x] Corpus rows within scope pass; no row fails.
- [x] `docs/PORTMAP.md` updated; `(?L)` behaviour recorded in PORTMAP's not-ported table or
      DECISIONS, whichever it turns out to be.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: `str.upper()`/`lower()` semantics
      where upstream folds a name, a `frozenset` membership test ported as a linear scan on a
      hot path, a hex escape whose value exceeds 0x10FFFF), commit.

## Closing notes

**What landed.** Sets and set operators, POSIX classes, properties, `\N{...}`, `(?#...)`, case
folding everywhere the parser consults it, and the whole inline-flag surface. `Nodes.cs` grew by
~1,200 lines (`Property`, `Range`, the four set types over `SetBase`, `Sequence.FixFullCasefold`
and `MergeChunks`, `Branch.IsFolded` and `FlushSetMembers`, `Character.Folded`,
`String.FoldedCharacters`) and `ParseFunctions.cs` by ~790 (`ParseSet` and its six helpers,
`ParsePosixClass`, `ParseProperty`, `ParsePropertyName`, `LookupProperty`, `StandardiseName`,
`TryNumericToRational`, `CharsetEscape`, `ParseNamedChar`, `ParseComment`). Every symbol is in
`docs/PORTMAP.md`. `character-classes` drops from 573 waiting tests to 114, `unicode-properties`
from 160 to 95, `case-folding` from 101 to 69; passing tests go 851 to 1447 and overall parity
0.5% to 0.7%. Final ratchet: `Tests: 3843  passing: 1447  baseline: 1447` / `Ratchet: GREEN`,
`dotnet build` `0 Warning(s) 0 Error(s)`.

**Surprises.**

- **`Sequence._fix_full_casefold` calls Python's `str.lower()`, which folding does not subsume.**
  `fold_case` leaves `I` alone (the Turkic triple), so only `.lower()` turns `fI` into `fi`, which
  is the folded ligature U+FB01 and therefore what makes the literal a full-case-folding chunk.
  Upstream's tables carry no lowercase mapping and .NET's is a different Unicode version (67
  codepoints disagree with CPython 3.14.6), so this slice added the port's second and last UCD
  file: `tools/build-lowercase.py` -> `Unicode/UnicodeLowercase.g.cs`, 1,488 mappings, verified
  against the host's own `str.lower()` for all 1,109,309 codepoints it knows, and wired into
  `oracle.yml` with `--check`.
- **Three patterns on which upstream itself raises a Python internal error** (`[^\s\S]`,
  `(?V1)[a--[\s\S]]`, an inline `(?V1)` under the `V0` flag). The middle one nearly compiled here:
  the first draft gave every node an ignored `inSet` parameter, where upstream declares it on six
  classes only. `RegexBase` now has two `Optimise` overloads, the three-argument one throwing in
  the base. Pinned by `Gaps/Parsing/UpstreamInternalErrorTests.cs`.
- **`standardise_name` reads every name as a number**, so its acceptance rule is Python's
  `float()`, not `double.TryParse`, and `\p{Infinity}` propagates an `OverflowError` out of
  `compile` rather than failing to parse. Pinned by `Gaps/Parsing/PropertyNameTests.cs`.
- **`(?L)` on a `str` pattern does nothing until casing is consulted.** Settled against the oracle
  and recorded in PORTMAP's not-ported table; only `(?Li)` on a *run* of two or more characters
  reaches the `needs:locale-flag` seam. Ten measured patterns in `Gaps/Parsing/LocaleFlagTests.cs`.
- **The set-member sort PORTMAP has promised since S06 landed here**, as `RegexBase.RenderKey`.

**Review.** One blind pass over the whole diff, briefed per `docs/VERIFICATION.md`. It raised one
defect and it reproduced: `LookupProperty` tested whether a qualifier existed *before*
standardising it, where upstream standardises first and every later `if property` tests the
standardised value - so a qualifier that standardises to the empty string (`standardise_name`
strips `_`, `-` and spaces) was wrongly rejected, and `\p{_:Lu}` raised where upstream compiles.
Fixed test-first, pinned by `A_qualifier_that_standardises_to_nothing_is_not_a_qualifier` in
`Gaps/Parsing/PropertyNameTests.cs` (12 rows), and the differential wave was widened with a
qualifier x value x separator block that re-ran clean. Because the fix touched code the reviewer
had not seen in its final form, a **second blind pass** was run over that delta only - the opening
of `LookupProperty` and the new test - per VERIFICATION rule 4. It found no defect in
`LookupProperty`, and confirmed every expected value in `PropertyNameTests.cs` against the oracle
(31 `StandardiseName` rows, 12 code arrays, 3 `OverflowError` rows) plus a 1,536-pattern and
12,750-name differential probe of its own with zero semantic divergences. It raised three defects
in the test file, all reproduced and all fixed: the `if property:` citation pointed at upstream's
standardisation line 1734 rather than 1743 (`grep -n "^    if property:"` gives 1743); the
`.NotBeOfType<FuzzyRegexParseException>()` tail on the `OverflowException` assertion could never
fail, because that type derives from `Exception`; and two of the three
`A_numeric_property_value_resolves_through_its_rational_form` rows (`1/2`, twice) are fixed points
of the strip-and-upper-case fallback and so had no teeth - `\p{nv=1.5}` and `\p{nv=1e-1}` were
added, both confirmed to compile upstream, and the whole method was watched go red (12 failures)
with `StandardiseName`'s numeric branch neutered. Totals across both passes - findings raised: 4,
reproduced: 4, fixed: 4. No third pass: the three fixes are in the file the second reviewer had
just read in full, add no public API and touch no tooling, so VERIFICATION rule 4 does not bite
and rule 5 does.

**For the next slice.** S11's scope lists `parse_comment` (`:978`); S10 already ported it, so that
bullet is done. `parse_paren` still throws for every `(?` form S11 owns. The differential-wave
recorder is `.scratch/s10_diff_record.py` (scratch, gitignored) - the C# comparison side was
deleted with the rest of the harness, so re-running the wave means rebuilding that half.
