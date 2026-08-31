---
slice: S24
phase: 3
title: Substitution - Replace, ReplaceFormat, Match.Result, and the template application
delivers: [substitution, format]
---

# S24 - Substitution: Replace, ReplaceFormat, Match.Result, and template application

Needs S18 (group values feed templates). S12 ported the *parsing* of replacement templates; this
slice applies them to real matches - the half DECISIONS 2026-08-31 recorded as S12's one
undelivered scope item ("`Match.Result` is not wired to the template compiler").

## Scope

Line references are `upstream/src/_regex.c` unless marked `_main.py`.

- **The sub loop**: `pattern_subx` (`:21726`) - iterate matches, apply the replacement, join. It
  contains its own scan-advance including the empty-match policy (advance by one after a
  zero-width match, with the V0/V1 difference), which S25's iterators share - port it here, reuse
  there. `get_sub_replacement` (`:21667`), `check_replacement_string` (`:19865`),
  `get_match_replacement` (`:19635`), `_compile_replacement_helper` (`_main.py:687` - already
  mostly ported in S12; wire, don't re-port). The join-list machinery (`:19687-19864`) collapses
  to a `StringBuilder`; record it in PORTMAP as such.
- **Expansion on a match**: `match_expand` (`:19902`) behind `Match.Result`; `match_expandf`
  (`:20045`) with `make_capture_dict` (`:19983`) behind `Match.ResultFormat` and
  `ReplaceFormat` - upstream's `subf`/`expandf` format-string path, where `{1}`, `{name}` and
  format specs apply to group values.
- **Public**: all four `Replace` overloads (string replacement, `MatchEvaluator`, each with and
  without `out int replacements` - upstream `sub`/`subn`), `ReplaceFormat` x2 (`subf`/`subfn`),
  `Match.Result`, `Match.ResultFormat`. `count`/`replacements` semantics per upstream (`count=0`
  upstream means unlimited; our `count: -1` default maps to it - S01's surface decided this,
  follow what the ported tests assert).
- **The deferred error rows**: `SubTemplateNumericEscapeTests`' 12 invalid-group-reference rows
  (DECISIONS 2026-08-31: a group-*number* check happens during expansion and needed a match) -
  un-skip them now, plus the unknown-group `ArgumentException` mapping DECISIONS records.

## Verification

- **Un-skip** `needs:substitution` (98) and `needs:format` (11), reading each skip's prose
  first - the fuzzy-substitution tests stay on their fuzzy tags; stragglers retag with prose.
- **Oracle wave**: generated (pattern, subject, template) triples - group references in all
  forms (`\1`, `\g<1>`, `\g<name>`, `{1}`, `{name}` on the format side), unmatched groups in
  templates, zero-width matches (the empty-match advance policy is where sub bugs live - probe
  V0 and V1 both), `count` limits, and literal-only templates. Compare the returned string and
  the replacement count. Zero divergences; negative control.

## Done when

- [ ] Both tags delivered or stragglers retagged; the 12 deferred error rows un-skipped; counts
      in closing notes.
- [ ] Oracle wave green including zero-width V0/V1 probes; counts quoted.
- [ ] `docs/PORTMAP.md` updated.
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: the empty-match advance applied
      before the replacement instead of after, an unmatched group expanding to "None" or
      throwing where upstream yields empty, `count` counting scans instead of replacements),
      commit.
