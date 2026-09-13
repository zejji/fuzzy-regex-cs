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

- [ ] Definitive-source table written; fix landed test-first; blast radius zero outside the dotted I.
- [ ] Divergence entry, pinned tests, control, ledger update.
- [ ] Ratchet GREEN, blind review (hunt: a fold applied to U+0131 the definition does not support;
      a generated table edited by hand), commit.
