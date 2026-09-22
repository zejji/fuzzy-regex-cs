---
slice: S80
phase: 8
title: A user guide for a .NET reader - every flag and every entry point described outside the migration tables, with a convention test that keeps it complete
delivers: []
---

# S80 - the library described to someone who has never used Python `regex`

> **Owner finding (2026-09-21).** "The markdown docs outside of the demo don't appear to describe
> all aspects of the library e.g. all the available flags, methods, options etc. On the face of it,
> it looks as though everything of interest to a new user not coming from Python is located in
> README.md."

Measured the same day, against `README.md` and `docs/COMPARISON.md`:

- **Four flags are named in neither file**: `Multiline` `(?m)`, `Singleline` `(?s)`,
  `IgnorePatternWhitespace` `(?x)` and `FullCase` `(?f)`. Eight more appear only inside
  `COMPARISON.md` (`Ascii`, `RightToLeft`, `Word`, `BestMatch`, `EnhanceMatch`, `Posix`,
  `Version1`, and the inline-only `(?L)`), which presents them as answers to "what is upstream's
  flag called here?".
- **Four public members are named in neither file**: `IsMatchAtStart`, `IsFullMatch`,
  `MaxCompiledNodes` and `NamedLists`. Another dozen, `Escape` and `ReplaceFormat` among them,
  exist only as rows in Table A.
- The flag surface is an enum **and** a parser table: `FuzzyRegexOptions` has 14 members plus
  `None`, while `RegexFlags.InlineFlags` accepts 15 letters, `L` among them with no enum member.
  A reader looking only at the enum misses the inline set and the other way round.
- `PublicApiDocumentationTests` asserts every public member has an XML doc entry, and
  `ComparisonCoversDivergencesTests` asserts every SHIPPED divergence row has a section. Neither
  says anything about whether the markdown describes the library, so this gap was invisible.

`COMPARISON.md` is doing its job: it is a migration and divergence document, Table A from Python,
Table B from `System.Text.RegularExpressions`, then "Behaviour that differs and why". What is
missing is the document a .NET developer with no Python background reads first, and the gate that
keeps such a document complete as the surface grows.

Runs in the `docs` worktree. `src/` is read-only for this slice; `DIVERGENCES.md` and `PORTMAP.md`
are read-only. Sonnet drafts, Opus reviews under `docs/VERIFICATION.md`.

## Scope

- **`docs/GUIDE.md`**, written for a reader who knows `System.Text.RegularExpressions` and has
  never seen Python `regex`, in this order:
  1. What the library is and when to reach for it, in two paragraphs, with the first fuzzy match in
     ten lines.
  2. **Choosing an entry point**, grouped by the question asked rather than by upstream's names: is
     there a match at all (`IsMatch`, `IsMatchAtStart`, `IsFullMatch`); where is the first
     (`Match`, `MatchAtStart`, `FullMatch`); all of them (`Matches`, `EnumerateMatches`); how many
     (`Count`); cut it up (`Split`, `EnumerateSplits`); rewrite it (`Replace`, `ReplaceFormat`,
     `MatchEvaluator`); and the one-line rule for when the static overloads are the wrong choice
     (`CacheSize`, and `new FuzzyRegex` for anything reused).
  3. **Every flag in one table**: enum member, inline letter, what it does in one line, whether it
     is on by default, and where the detail lives. All 15 inline letters and all 14 enum members
     appear, `(?L)` included with its one-line note that it has no enum member.
  4. **Error budgets and named lists**, short, with the reader sent to `COMPARISON.md`'s "Fuzzy
     syntax in one page" rather than a second copy of it.
  5. **Timeouts and cancellation**: the per-call `timeout`, `MatchTimeout`, `InfiniteMatchTimeout`,
     the `CancellationToken` on every input-dependent method, and what a lazy walk times.
  6. **Reading a result**: `Match`, `Group`, `Groups` as list and dictionary, `FuzzyCounts`.
  7. **Limits and failure**: `MaxCompiledNodes`, `DefaultMaxCompiledNodes`, `CacheSize`,
     `FuzzyRegexParseException`, and which .NET exception each bad input raises.
  8. **Thread safety**, one paragraph and a link, no restatement of the README.
- **`README.md`** gains the link where "Where the docs are" already lists the others. Nothing else
  in the README changes: it stays the first contact and the nupkg readme.
- **Samples pinned**: every ```csharp block in the guide compiles and prints what the block shows.
  Add `docs/GUIDE.md` to the default `$Files` in `tools/check-doc-examples.ps1`, and the guide's
  samples to `tests/FuzzyRegex.Tests/Docs/`, following `ComparisonSamples.cs`.
- **Copy linter**: add the `docs/GUIDE.md` row to the source list in `demo/web/tests/copy.test.ts`,
  with its minimum word count and canary sentence, so the guide is held to the same prose rules as
  everything else.
- **The gate that keeps it complete**: a new convention test, sibling to
  `PublicApiDocumentationTests`, asserting that every public member name in
  `src/FuzzyRegex/PublicAPI.Unshipped.txt` and every key of `RegexFlags.InlineFlags` (visible to the
  test assembly through `InternalsVisibleTo`) is named somewhere in the user documentation set. The
  set is a list in the test, starting `README.md` and `docs/GUIDE.md`, so splitting the guide later
  adds a filename rather than rewriting the gate. It matches names rather than signatures, so
  overloads do not multiply the work, and its failure message names the missing symbol. Any
  exclusion is a constant in the test with a written reason beside it.

  **Read Unshipped, not Shipped** - this spec said Shipped until 2026-09-21 and that was wrong.
  Nothing has been released, so `PublicAPI.Shipped.txt` is a single line and `PublicAPI.Unshipped.txt`
  carries all 147 members: a gate over Shipped enumerates nothing and passes while documenting
  nothing, which is the exact failure it exists to catch. Assert a floor on the count of members the
  scan finds, the way `ComparisonCoversDivergencesTests` does, so a parse that finds nothing fails
  instead of passing. When the 1.0 release moves those lines into Shipped, the gate must read both
  files.

- **One file or several, decided on measured length** (owner, 2026-09-21: "you can split the docs
  between multiple files - up to you"). Write `docs/GUIDE.md` first, with a table of contents at
  the top. If it passes **700 lines**, split the flag reference out to `docs/FLAGS.md` and link it
  from the guide and the README: the flags are the one part that is reference rather than
  narrative, they are what people link to directly, and they are the part that grows when an option
  is added. Nothing else splits - a guide scattered over five files costs the reader more than a
  long page with a contents list, and GitHub renders no sidebar to make up for it. Record the
  measured length and the decision in the closing notes.

Not in scope: no `src/` change; no divergence prose, which `COMPARISON.md` owns; no generated API
reference - the DocFX-style site stays a parked candidate, because a generated signature list
answers "what does this take" and never "which one do I want".

## Verification

- `tools/check-doc-examples.ps1` green across `README.md`, `docs/COMPARISON.md` and
  `docs/GUIDE.md`, and every flag's default state in the table is taken from a run, not from
  reading the enum.
- The new convention test fails when a member is deleted from the guide and when an inline letter
  is dropped from it; prove both once and revert.
- The copy linter passes over the guide, and the demo build is unaffected.
- Ratchet green.

## Done when

- [x] `docs/GUIDE.md` covers the eight sections above, every sample pinned by a test.
- [x] Every public member and every inline flag letter is named in `README.md` or `docs/GUIDE.md`,
      enforced by the new convention test rather than by reading.
- [x] `README.md` links the guide (and `docs/FLAGS.md` if the split happened); no other README
      change.
- [x] Closing notes: what the guide asserts that Phase 7 could change, and anything the flag table
      had to state as "measured" because the enum comment and the behaviour disagreed.

## Closing notes

**Length and split.** `docs/GUIDE.md` measured 299 lines finished - well under the 700-line
threshold, so the flag reference stayed inline rather than moving to a separate `docs/FLAGS.md`.

**Shipped vs Unshipped.** The spec's own correction (2026-09-21, recorded above) was carried into
the gate as written: `UserDocumentationCompletenessTests` reads `PublicAPI.Unshipped.txt` and
asserts a floor on both the raw line count (>140, measured 146) and the distinct extracted name
count (>65, measured 75), so a broken path or an accidental read of the near-empty
`PublicAPI.Shipped.txt` fails loudly instead of passing over nothing.

**What Phase 7 could invalidate.** The guide states default values and behaviour measured against
the current implementation, not the public contract: `CacheSize`'s default, the enum's default
flag combination, and the "on by default" column of the flag table are all read from a live run
(`tools/check-doc-examples.ps1`), not from a comment, so they will only go stale if the ratchet
stops catching a real behaviour change. None of the corrected claims below are optimisation-shaped
- they are static facts (a syntax form, which methods have a static overload, clamping vs an
exception, a parameter's polarity) - so Phase 7 should not need to touch this file at all.

**Blind review.** One reviewer pass on the full S80 diff (the new convention test, `docs/GUIDE.md`,
`README.md`, the copy-linter row, `tools/check-doc-examples.ps1`) per `docs/VERIFICATION.md`. It
raised four findings, all in `docs/GUIDE.md` prose, none in the test or tooling:

1. The named-list example used an invented syntax, `(?:%(colour)e<=1)`. The real syntax is
   `\L<name>{budget}`. Reproduced against the reviewer's own working example
   (`@"(?:\L<colour>){e<=1}"` matching `"rad"` against `["red","blue"]`), then independently
   re-verified the unwrapped form the guide actually uses, `\L<colour>{e<=1}`, with a throwaway
   console program: `success=True value=rad`. Fixed the prose and its cross-reference to
   `COMPARISON.md`'s "Fuzzy syntax in one page".
2. "Every reader method has both a static and an instance form" is false: `IsFullMatch` and
   `IsMatchAtStart` are instance-only. Fixed the section's intro sentence and added a note to each
   affected table row.
3. The Exceptions paragraph claimed `beginning`/`length` out of range throws
   `ArgumentOutOfRangeException`. They clamp instead (`Engine.MatchState.ClampIndex`, Python-slice
   negative-index semantics); the real trigger for that exception is an undefined group number.
   Fixed the paragraph.
4. `literalSpaces` was described backwards - it means "leave spaces unescaped", not "also escape
   spaces". Fixed the parameter description.

All four were reproduced against source or a live run before the fix, per VERIFICATION.md rule 1.
All four fixes are corrections to already-reviewed prose (no new public API, no tooling change, no
code the reviewer had not seen), so per rule 4 no second blind pass was needed; the full test
suite, doc-examples check, copy linter and ratchet were re-run after the fixes and are all green.
