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

- [ ] Corpus rows within scope pass; no row fails.
- [ ] `docs/PORTMAP.md` updated; `(?L)` behaviour recorded in PORTMAP's not-ported table or
      DECISIONS, whichever it turns out to be.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: `str.upper()`/`lower()` semantics
      where upstream folds a name, a `frozenset` membership test ported as a linear scan on a
      hot path, a hex escape whose value exceeds 0x10FFFF), commit.
