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

Suggested feature areas (namespace `Fuzzy.Text.RegularExpressions.Tests.Ported.<Area>`): `Basics`, `Substitution`,
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

---

## Closing notes (2026-08-29)

**What landed.** 50 new test files under `tests/FuzzyRegex.Tests/Ported/`, 241 test methods
expanding to 432 skipped test cases across 13 feature areas. 53 of the 63 upstream test methods in
lines 1-1007 are ported; the other 10, and every assertion omitted from a method that is otherwise
ported, are listed in `docs/PORTMAP.md` with a reason. Ratchet GREEN, baseline unchanged at 34 -
correct, since every new test is skipped.

**Capability tags coined: 22**, from a vocabulary of 24. `anchors`, `alternation`, `backrefs`,
`basic-matching`, `case-folding`, `character-classes`, `conditionals`, `escape-function`,
`escapes`, `find-all`, `fuzzy-matching`, `groups`, `ignore-case`, `inline-flags`, `lookaround`,
`named-groups`, `parse-errors`, `pattern-properties`, `quantifiers`, `right-to-left`, `splitting`,
`substitution`. `named-lists` and `version-flags` were reserved and turned out unused - S03-S05
should reuse this vocabulary before coining anything new.

**Two public-API decisions were forced by the port and approved by the owner**, both in
`DECISIONS.md`: replacement templates speak upstream's language (`\1`, `\g<name>`) rather than
`Regex`'s `$1`, and `Split` returns `string?[]` with `null` for a group that did not participate.
Neither could be deferred - roughly 60 assertions in this slice cannot be written without knowing
the answer. `Match.Result`'s S01 doc comment claimed `$1` and was corrected; S01's archived slice
record carries a supersession note rather than being rewritten.

**One test was deleted**: `Ported/Anchors/AnchorTests.cs`, S01's conventions placeholder. Its body
was `Assert.Fail(...)`, its provenance cited `test_bigcharset` (which contains no `^` assertion),
and it was tagged `needs:anchors` - so the anchors slice would have un-skipped a guaranteed-red
test with no upstream basis. S02 ports 11 real anchor tests into that same area, so it is
superseded rather than merely removed. It was skipped and therefore never in the passing baseline,
so the ratchet's removal gate did not fire; recorded here because nothing else would show it.

**Surprises worth carrying forward.**

1. **Literal `\uXXXX` text does not survive the authoring toolchain.** Writing `\u000A` into a file
   through the agent tool path silently decodes it into an actual newline before it reaches disk.
   Reproduced independently in this session, not just reported. It corrupted one PORTMAP row here
   and was caught. **S03 ports `test_properties` and is Unicode-dense, so this will bite harder:**
   build backslashes from `chr(92)` when scripting, prefer literal UTF-8 characters or `\xNN`/`\UXXXXXXXX`
   forms, and byte-scan the result. A scan for stray control characters across the ported tree is
   cheap and worth repeating.
2. **A capability tag is a scheduling contract, and getting it wrong breaks a future slice.** Five
   `Split` tests using `(?r)` and three using `\b`/`\m`/`\M` were tagged `needs:splitting`; the
   splitting slice would have un-skipped tests that cannot pass. Worse, `right-to-left` had no other
   test anywhere, so the mis-tag made the capability invisible on the status board. See the rule
   recorded in `DECISIONS.md`.
3. **Both review passes had unusually high finding survival: 6 of 6 confirmed**, against the ~1 in 5
   the verification rules lead you to expect. Every finding arrived with a quoted upstream line or
   oracle output, as the brief demanded. The value came from one reviewer machine-checking ~330
   expected values against the installed `regex` module - a mechanical oracle sweep, not opinion.
   **Do that again for S03-S05**; it is the only gate that can catch a wrong expected value in a
   test that never executes.
4. Delegation to four parallel agents worked, but each verified its own files in isolation and so
   missed 7 analyzer errors that only appear in a whole-project build. Integrate and build centrally.
