---
slice: S50b
phase: 6
title: Version 1 becomes the default behaviour - nested sets and full case-folding out of the box, Version0 kept for re-compatible patterns
delivers: []
---

# S50b - Version 1 by default

Owner decision, 2026-09-14 (spec amendment 24): this port has no `re` users to stay compatible
with, so it defaults to upstream's "new behaviour", `VERSION1`, rather than mirroring upstream's
`DEFAULT_VERSION = VERSION0`.

## What actually changes - measured, not read from the README

Run on regex 2026.9.10, CPython 3.14.6 and .NET 10 on 2026-09-14 by the orchestrator (the probe is
item 1 below, so the numbers are re-established in the slice from a committed file):

| Behaviour | V0 today | V1 | .NET `Regex` |
|---|---|---|---|
| zero-width `split` / `sub` | correct, identical to V1 | correct | correct |
| inline flag scoping and `(?-i)` turn-off | works, identical to V1 | works | works |
| nested sets and set operations `[[a-z]--[aeiou]]` | unsupported; `[` is literal inside a set | supported; an unescaped `[` inside a set is "unterminated character set" | subtraction only, `[a-z-[aeiou]]` |
| case-insensitive folding | simple (`ß` vs `SS` false) | full (`ß` vs `SS` and `ﬁ` vs `fi` true) | simple |

Upstream's README lists four differences; two of them no longer exist because V0 tracks Python
`re` 3.7+. The live differences are nested sets and full case-folding, both of which upstream
documents as the better behaviour and both of which are the reason to use this library over
`System.Text.RegularExpressions`.

## Scope

1. **The probe first.** `tools/probes/upstream-version-defaults.py` (regex V0 and V1) and a
   `.ps1`/C# twin for `System.Text.RegularExpressions` print the table above; the notes quote their
   output with versions. Any cell that differs from the table is a finding, recorded before the
   default moves.
2. **Flip the default.** `PatternCompiler.DefaultVersion` becomes `Version1`. The `Info` and
   parser paths already consult it; nothing else in the engine should need to change. Anything that
   does is a hidden dependency on V0 and is named in the notes.
3. **The oracle compares under upstream's default, explicitly.** Wave headers already carry
   `defaultVersion`. `OracleComparer` ORs that version into the port-side options for every row
   whose flags carry no version bit, so every existing wave stays valid and the comparison keeps
   asking upstream the question it was asked. Test-first: a row without a version bit must compile
   on the port side with V0 and the header's V0 must be visible in the row description. Re-run the
   default wave at three seeds and 99991 - the counts must not move.
4. **Ported upstream tests pin V0 where upstream assumed it.** Upstream's `test_regex.py` is
   written against `DEFAULT_VERSION = VERSION0`; the port's `Ported/` tree has 129 constructions
   with no version bit and 63 patterns spelling `(?V0)`/`(?V1)`. A shared helper (`Upstream.Compile`
   or the existing test factory, whichever the tree already has) passes `Version0` for every ported
   test unless the test names a version itself. The ratchet must stay at the same passing set: a
   ported test that starts failing under the pinned V0 has found a bug in the pinning, not in the
   engine. Gap tests written by this port keep the new default and are reviewed for any that
   silently relied on V0 - the reflection is cheap: run them once with the default flipped and
   read the failures.
5. **The loud edge gets a helpful error.** `[[]` and `[a[b]` are legal under V0 and .NET and fail
   under V1. The parse error for an unterminated set inside a set names `FuzzyRegexOptions.Version0`
   as the way to get the `re`/.NET reading, and says to escape `[` otherwise. Test-first.
6. **`FullCase` interaction documented and tested.** Under V1 full folding is on by default;
   `(?-f)` and the absence of `FullCase` still turn it off where upstream lets them. Tests for
   `ß`/`SS`, `ﬁ`/`fi`, Kelvin sign, and the S45 Turkic four under the new default.
7. **Docs.** README, the `FuzzyRegexOptions` XML docs (`Version0` is "compatibility with `re` and
   .NET set syntax and simple folding"; `Version1` is the default) and the API section state the
   default and the two behaviours it changes. `docs/PORTMAP.md` notes that `DEFAULT_VERSION` maps to
   a different constant here and why.
8. **What does not change.** The engine flag bits, the meaning of `(?V0)`/`(?V1)` in a pattern,
   the oracle's upstream side, and the recorded waves. No global mutable default is introduced
   (S52b's structural test would fail it): the default is a compile-time constant plus an explicit
   option.

## Verification

- Ratchet GREEN at the same passing set; default wave at three seeds and 99991 with unchanged
  counts; probe output quoted; the new tests red-first as described.
- Blind review (hunt: a ported test whose V0 pin hides a real engine change; an oracle row whose
  port-side options gained V0 while upstream's did not; a gap test that passed under V0 for the
  wrong reason), then the verifier pass re-running the probes and the wave summaries.

## Done when

- [ ] Probe committed and quoted; default flipped; oracle explicit about the version it compares.
- [ ] Ported tests pinned to V0 through one helper; ratchet passing set unchanged.
- [ ] Helpful error for `[` inside a set under V1; `FullCase` tests under the new default.
- [ ] README, XML docs, PORTMAP updated; DECISIONS entry.
- [ ] Ratchet GREEN, blind review, verifier, commit.
