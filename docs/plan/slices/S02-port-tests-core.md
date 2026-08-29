---
slice: S02
phase: 1
title: Port upstream tests - core matching and the API surface
delivers: []
---

# S02 - Port upstream tests: core matching (test_regex.py lines 1-1008)

Follow the `port-tests` skill. It holds all the conventions; this file holds only the scope.

## Scope

`upstream/regex/tests/test_regex.py`, from the top of the file to line 1008 (`test_properties`
begins there and belongs to S03). Roughly 45 test methods covering the API surface and the
basics of matching.

Notable methods in range: `test_search_star_plus`, `test_basic_regex_sub`,
`test_sub_template_numeric_escape`, `test_symbolic_refs`, `test_re_subn`, `test_re_split`,
`test_qualified_re_split`, `test_re_findall`, `test_re_match`, `test_re_groupref_exists`,
`test_re_groupref`, `test_groupdict`, `test_expand`, `test_repeat_minmax`, `test_getattr`,
`test_special_escapes`, `test_bigcharset`, `test_anyall`, `test_non_consuming`,
`test_ignore_case`, `test_case_folding`, `test_not_literal`, `test_re_escape`, `test_flags`,
`test_sre_character_literals`, `test_sre_character_class_literals`, `test_scanner`,
`test_finditer`, `test_inline_flags`, `test_dollar_matches_twice`, `test_subscripting_match`,
`test_new_named_groups`, plus the numbered `test_bug_*` regression methods in range.

Suggested feature areas (namespace `FuzzyRegex.Tests.Ported.<Area>`): `Basics`, `Substitution`,
`Splitting`, `FindAll`, `Groups`, `Quantifiers`, `Escapes`, `CharacterClasses`, `CaseFolding`,
`Flags`.

## Explicitly not ported (record each in PORTMAP.md)

- `test_weakref` - Python object model.
- `test_re_escape_byte`, `test_bytes_str_mixing` - `bytes` patterns; this port is `char`-based.
- `test_constants` - asserts Python-level flag integers.
- `test_empty_array` - Python buffer protocol.

## Done when

- [ ] Every applicable assertion in range is a TUnit test, skipped with a `needs:<capability>`
      reason and carrying its `[Property("Upstream", ...)]` provenance.
- [ ] The line range is fully accounted for: every method in range is either ported or listed in
      PORTMAP.md as not ported, with a reason. No silent gaps.
- [ ] `dotnet build` clean; the conventions test passes.
- [ ] `tools/check-ratchet.ps1` GREEN (new tests are skipped, so the baseline does not move).
- [ ] Slice file moved to `done/`, closing notes appended: the capability tags you coined, and
      the count of tests ported versus skipped-as-not-applicable.

## Notes

Coin capability tags carefully in this slice: S03-S05 will reuse them, and every tag you invent
becomes a row on the status board. Keep the vocabulary small and obvious - `quantifiers`,
`backrefs`, `named-groups`, `substitution`, `splitting`, `case-folding`, `inline-flags`.
