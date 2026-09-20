---
slice: S75
phase: 9
title: Edit markers redrawn, a help note on every input heading, the test-set example, identifier pins for the snippet, and the copy linter over the docs
delivers: []
---

# S75 - edit markers, help notes and the prose linter

> **Owner decisions (2026-09-20, from the first look at S73 and S74).** Five findings from the
> owner's review of the demo and its code, plus a writing rule that now applies to the whole repo.
> Each was agreed in conversation; the owner reviews this spec before a sitting runs. Run it in the
> demo worktree after S74 has merged into main.

## 1. The edit markers

Today a fuzzy match paints each edited character with a colour and one of three underline styles,
and prints a small `s`, `i` or `d` after it. The owner's finding: the letters sit at text size, so
they overlap the outline and the next character, and the wavy underline under a letter reads as a
tick. Nobody can tell what they mean without guessing.

What replaces it, grounded in how diff tools and track changes show edits (colour plus an
underline or strikethrough, the meaning on hover, a legend close by, never colour alone, which is
WCAG 1.4.1):

- **Colour and the three underline styles stay; the inline letters go.** Remove the `::after`
  content and its spacing. Confirm in the browser that the outline no longer clips.
- **Meaning on hover and focus.** Each marked run is a focusable span with a help note in the
  mechanism S74 built for the `(?)` buttons: "substitution at index 3", "insertion at index 7",
  "deletion before index 12". The same note opens on tap and closes on Escape.
- **A legend of three chips under the subject**, shown only when the current result contains a
  fuzzy match: each chip is a sample character drawn with that kind's colour and underline, followed
  by the word. One line. The chips are not buttons.
- **A letter-by-letter alignment for the selected match**, in the groups area: the pattern text
  that matched on one row, the subject on the next, and the edit kinds in the row between them, one
  cell per character, so a substitution is visibly the pattern's `u` above the subject's `o`. Only
  for the selected match, and only when it has at least one edit; otherwise the area is unchanged.
  Build it from `FuzzyChanges` (the indices the engine already returns), not by re-diffing.

## 2. A help note on every input heading

The owner could not tell what "Named lists" is for. Every input heading gets the `(?)` button S74
introduced: Pattern, Subject, Flags, Mode, Replacement template, Named lists. Each note is one or
two sentences, and ends with a link that opens the help tab at the matching section of
`docs/COMPARISON.md` (the section keys `tools/build-demo-help.ps1` already maps).

The named-lists note, to set the standard for the rest: "A named list is a set of words the
pattern can match as one alternative, written `\L<name>`. Give the list here, one word per line,
and the pattern refers to it by name. Fuzzy budgets apply to the list as a whole." Then the link.

Write the six notes in plain English under the copy rules, and lint them with the rest of the
strings. The heading text itself does not change.

## 3. The worked example the demo is missing

The owner asked whether an edit can be forbidden from touching whitespace, or limited to letters.
It can: the fuzzy test set, `{e<=2:[^\s]}` and `{e<=2:[a-zA-Z]}` (COMPARISON.md, "constrain which
characters an edit may touch"). No worked example shows it. Add one to `examples.json`:

- key `fuzzy-test-set`, title "Edits limited to letters"
- pattern `(?:colour){e<=2:[a-z]}`
- a subject with one case that matches because both edits are letters ("color") and one that does
  not because an edit would have to be a space or a digit ("col our", "col0ur"); the note names
  both and says why the second fails.
- Verify the example against the engine before committing it, with the spans quoted in the closing
  notes, and give it the `fuzzy` help key so the help tab opens on the test-set section.

## 4. Identifier pins for the C# snippet

`demo/web/src/lib/snippet.ts` writes the library's type and member names as string literals,
because TypeScript has no `nameof`. Two things are pinned today: the flag names (against
`Enum.GetNames<FuzzyRegexOptions>()`) and the timeout (against `DemoEngine.MatchTimeout`). Not
pinned: `FuzzyRegex`, `EnumerateMatches`, `Match`, `Replace`, `FuzzyCounts`, `Substitutions`,
`Insertions`, `Deletions`, `FuzzyRegexOptions` and the `partial` parameter name. A rename of any
of them would ship a snippet that does not compile.

Add to `DemoSnippetTests`: one test that reads `snippet.ts` and asserts that every identifier the
snippet emits appears in the file, with the expected list built from `nameof(...)` on the real API
and the `partial` parameter name read by reflection from the method's `ParameterInfo`. A rename
then fails the build, which is the property the owner asked for. Keep the list of identifiers in
one place in `snippet.ts` (an exported record) so the test reads data, not the template text.

Then decide about the compile probe. The 2026-09-19 probe pasted one snippet into a console
project and compiled it, once, by hand. Wire it as an opt-in test (`[Explicit]` or an environment
variable, whichever the test project already uses for slow tests) that writes the snippet for the
first worked example to a scratch project and runs `dotnet build` on it. If the test project has no
such convention, add none: record the manual command in the closing notes instead, and say so.

## 5. The copy linter reaches the docs

Owner rule of 2026-09-20 (now in the port-slice skill under "Writing for a reader"): every piece of
prose in the repo reads as a skilled human wrote it. S73's copy linter enforces the banned list, but
only over the demo's strings. Extend it to the reader-facing documents:

- `README.md` and the six pages under `docs/` (`COMPARISON.md`, `DIVERGENCES.md`,
  `ORACLE-INVARIANTS.md`, `PORTMAP.md`, `STATUS.md`, `VERIFICATION.md`). Not `docs/plan/`, which
  is working notes for the port and not for readers of the library.
- One rule set. The docs are a new entry in `copy-sources.ts`: read the file, drop fenced code
  blocks, inline code and tables (a table cell is not a sentence), then lint each remaining
  paragraph. `COPY_RULES` is not forked or softened for the docs.
- A per-file allow list, each entry the exact text and the reason it is allowed (a quoted upstream
  sentence, a flag name in prose), so an exception is visible and reviewed, never silent.
- Guard tests as for every other source: a string each file must contain and a floor for how many
  paragraphs the extraction must find, so an extraction that finds nothing cannot pass.
- **Fix what it finds.** The first run will fail on today's prose; that is the slice's work, not a
  reason to widen the allow list. Rewrite each flagged sentence, keeping the fact and dropping the
  tell. Quote the before-and-after count in the closing notes.
- Measure the public XML doc comments in `src/FuzzyRegex` with the same extraction (comment text,
  not string literals). If the violations number under fifty, fix them and add the source to the
  linter in this slice; if more, commit the count and the list to the closing notes and hand the
  fix to a follow-up slice, so a large rewrite of library comments is a decision the owner takes and
  not a side effect of this one.

The linter runs in Vitest, as today, so it runs on `npm test` and in `build-demo-web.ps1`. It does
not run in the C# test suite.

## What changes in the code

- `demo/web/src/App.vue` and its styles: edit-run markup and CSS (item 1), the legend, the
  alignment view, six heading help buttons (item 2).
- `demo/web/src/lib/`: a small `alignment.ts` that turns a match's `FuzzyChanges` into rows of
  cells, pure and tested on its own; the heading notes beside `FLAG_HELP` in `flags.ts` or a new
  `help-notes.ts`, whichever keeps one help mechanism.
- `demo/FuzzyRegex.Demo.Wasm/wwwroot/examples.json`: one entry (item 3).
- `demo/web/src/lib/snippet.ts`: identifiers moved into one exported record (item 4).
- `tests/FuzzyRegex.Tests/Gaps/Demo/DemoSnippetTests.cs`: the identifier pin and the opt-in compile
  test (item 4).
- `demo/web/tests/copy-sources.ts` and `copy.test.ts`: the docs sources, allow lists and guards
  (item 5); `README.md` and `docs/*.md` edited where the linter fires.

## Tests to add

- `alignment.test.ts`: a substitution, an insertion and a deletion each produce the expected three
  rows; a match with no edits produces nothing; indices from a real `DemoEngine` result round-trip.
- `page.test.ts`: the inline letters are gone (no `::after` content on an edit run); hovering,
  focusing and tapping a run opens its note and Escape closes it; the legend appears only with a
  fuzzy match; each of the six headings has a help button whose note names the heading and whose
  link opens the help tab at the right section; the new example loads and its match spans are the
  ones the closing notes quote.
- `copy.test.ts`: every new note and the new example pass; README and each docs page pass; the
  guards hold; the allow list is non-empty only where a reason is written.
- `DemoSnippetTests`: the identifier pin fails when one name in the record is changed (prove it by
  editing a copy of the record in the test, not the file); the opt-in compile test is skipped by
  default and green when enabled.

## Verification

Screenshots at 1366x768 and 390x844 of a fuzzy match with the legend and the alignment view, and
of one open heading note, committed beside the S73 and S74 references. The fold check from S73
re-taken: pattern, subject, action, count and first rows still above the fold with the legend
present. `npm run typecheck`, `npm test`, `npm run build`, `tools/build-demo-web.ps1` and the C#
test suite green. The new example run in the browser and its spans compared with the engine's.

## Done when

- [ ] No inline `s`/`i`/`d` letters; colour and underline kept; note on hover, focus and tap; legend
      under the subject when a fuzzy match is shown; alignment view for the selected match.
- [ ] Six heading help notes, each linking to its COMPARISON.md section through the help tab, all
      passing the copy linter.
- [ ] The test-set example in `examples.json`, verified against the engine, spans quoted.
- [ ] Every identifier the snippet emits pinned by `nameof` or reflection; the compile probe either
      an opt-in test or a recorded command with the reason no test convention fit.
- [ ] The copy linter runs over `README.md` and `docs/*.md` with guards and a reasoned allow list;
      all seven pass; the XML doc comments measured and either included or handed on with a count.
- [ ] Tests above green; screenshots and the fold check committed; typecheck, vitest, build,
      `build-demo-web.ps1` and the C# suite green.
- [ ] Blind review (hunt: a run whose note names the wrong index; an alignment that drifts when an
      insertion and a deletion are adjacent; a heading link that opens the help tab at the wrong
      section; an allow-list entry with no reason; a docs paragraph the extraction skipped because
      of a table or a fence it did not recognise; a snippet identifier not in the record), fix,
      commit.

## Sources

- https://www.w3.org/WAI/WCAG21/Understanding/use-of-color.html - success criterion 1.4.1: colour
  is never the only means of conveying information. Cited from memory when this spec was written;
  the sitting re-reads it and records the date.
- GitHub's split and unified diff views and Word's track changes: colour plus underline or
  strikethrough, meaning on hover, a legend or key nearby. Observe and record during the sitting.
- `docs/plan/slices/done/S73-demo-as-a-product.md`, "Copy rules and the banned list", and its
  sources (Wikipedia:Signs of AI writing; GOV.UK A to Z style guide), read 2026-09-19.
- `docs/plan/slices/done/S74-flags-control.md`, closing notes, for the help mechanism this slice
  reuses.
