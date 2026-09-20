---
slice: S75
phase: 9
title: Edit markers redrawn, a help note on every input heading, the test-set example, identifier pins for the snippet, and the copy linter over the docs
delivers: []
---

# S75 - edit markers, help notes and the prose linter

> Per-sitting notes, measurements and what is left: `docs/plan/slices/notes/S75-sittings.md`.

> **Owner decisions (2026-09-20, from the first look at S73 and S74).** Five findings from the
> owner's review of the demo and its code, plus a writing rule that now applies to the whole repo.
> Each was agreed in conversation; the owner reviews this spec before a sitting runs. Run it in the
> demo worktree after S74 has merged into main.

## 1. The edit markers

Today a fuzzy match paints each edited run with a colour, one of three underline styles, and a
10 px letter (`s`, `i` or `d`) drawn by CSS beneath the run. S73 chose three signals on purpose:
colour is the fast path, the line style survives colour blindness, and the letter says which kind
without learning the line styles. The owner's finding is about the geometry, not the idea: the
letter sits about 8 px below the baseline, inside the space the mark's border and the next line
use, so it collides with the outline and, under a wavy line, reads as a tick.

**The letters stay.** The owner read them without difficulty, and the guidance agrees: a visible
text cue beats a symbol the reader must decode or a meaning hidden behind hover, which raises the
interaction cost and does not exist on a touch screen (NN/g, "Icon Usability"). WCAG 1.4.1 is met by
colour plus line style alone, so the letter is the part that makes the kind readable at a glance.
What changes is where it is drawn:

- **A marker row under the text.** The subject block gets enough line height for the letter to sit
  centred beneath its run, clear of the underline above it and of the line below. Measure the
  three distances (underline to letter, letter to border, letter to next line) at 4x, as S73 did,
  at 1366 and 390 wide, and record them in the closing notes.
- **No character moves.** The letter remains a `::after` pseudo-element, absolutely positioned, so
  every subject character stays where the engine indexed it and a copy of the subject copies the
  subject and not "xsfiooba". Do not switch to inline text or to `<ruby>`, which would move the text
  or be copied with it.
- **One letter per run, not per character.** Adjacent edits of one kind are already grouped into a
  run in the template; the letter marks the run. Confirm in the browser that a run of two
  substitutions shows one `s`.
- **The deletion marker** (a dashed gap the width of a narrow character, because a deletion has no
  character of its own) keeps its `d` on the same row as the others.
- **A one-line legend under the subject**, shown only when the current result contains a fuzzy
  match: three chips, each a sample character drawn with that kind's colour, underline and letter,
  followed by the word ("substitution", "insertion", "deletion"). The chips are not buttons. This is
  what makes an abbreviation acceptable: the label is in view, one line away.
- **Meaning on hover, focus and tap.** Each marked run keeps its `title` and gains the note
  mechanism S74 built for the `(?)` buttons, reading "substitution at index 3", "insertion at index
  7", "deletion before index 12", closing on Escape. A third layer for the exact position, never the
  only one.
- **Deletions at the end of the subject get one marker.** A deletion has no character, so a run
  of them past the last character stacks at the same spot. The owner's case, `(foobar){e}` on
  `xirefoabralfobarxie`, ends with a match of `e` (one substitution, five deletions) and an empty
  match at the end (six deletions): eleven `d` markers on top of each other. Upstream and this port
  agree on every span and count (probe run 2026-09-20, `regex 2026.9.10`), so the engine is right
  and the drawing is wrong. Draw the deletions of one run as a single gap with a count when there
  are two or more ("5 d"), and give an empty match at the end of the subject a visible marker of its
  own, because a zero-width highlight is invisible.
- **Say when the budget is unlimited.** `{e}`, `{s}`, `{i}` and `{d}` with no bound allow any
  number of errors, so every position matches and the page fills with markers; a visitor who meant
  `{e<=1}` sees nonsense and blames the engine. When the pattern compiles and its fuzzy budget has
  no bound, one line under the pattern says so and names the bounded form. Detect it from the
  pattern text with the same parser the highlight uses, or from the engine if it exposes the
  constraint; do not guess from the match count.
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
- `page.test.ts`: every edit run carries its letter and one run of two adjacent substitutions
  carries one; hovering, focusing and tapping a run opens its note and Escape closes it; the legend
  appears only with a fuzzy match; the owner's `(foobar){e}` case shows one counted gap and an end
  marker, not eleven letters; `{e}` shows the unbounded-budget line and `{e<=1}` does not; each of
  the six headings has a help button whose note names the heading and whose
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

- [ ] The `s`/`i`/`d` letters sit in a marker row under the text, clear of the underline, the
      border and the next line, measured at 4x at both widths; no character moves and a copy stays
      clean; note on hover, focus and tap; legend under the subject when a fuzzy match is shown;
      alignment view for the selected match.
- [ ] Stacked deletions drawn once with a count; an empty match at the end of the subject visible;
      an unbounded budget named in one line under the pattern, with a test for each of the four
      letters and for a bounded pattern that must show nothing.
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

## Sources (read 2026-09-20 unless stated)

- https://www.w3.org/WAI/WCAG21/Understanding/use-of-color.html (read 2026-09-20) - 1.4.1: colour
  is never the only visual means; an underline or line style is an accepted second cue; a text cue
  (G14) is the sufficient technique when the reader must know which category a colour means.
- https://www.nngroup.com/articles/icon-usability/ and https://www.nngroup.com/articles/bad-icons/
  (read 2026-09-20) - a text label visible at all times beside any symbol; do not rely on hover to
  reveal labels, which raises interaction cost and fails on touch.
- https://www.nngroup.com/articles/recognition-and-recall/ (read 2026-09-20) - a label on the item
  is recognition; a legend elsewhere is recall.
- https://support.microsoft.com/en-us/office/check-spelling-and-grammar-in-office-5cdeced7-d81d-47de-9096-efd0ee909227
  (read 2026-09-20) - Word's convention: colour plus a distinct line style per category (wavy,
  double, dotted), meaning on interaction.
- https://www.mediawiki.org/wiki/Codex/Design/Diffs and https://gitlab.com/gitlab-org/gitlab/-/issues/28482
  (read 2026-09-20) - diff views that relied on colour alone failed audit; the fix was a visible
  text cue beside the colour.
- `docs/plan/slices/done/S73-demo-as-a-product.md`, "Copy rules and the banned list", and its
  sources (Wikipedia:Signs of AI writing; GOV.UK A to Z style guide), read 2026-09-19.
- `docs/plan/slices/done/S74-flags-control.md`, closing notes, for the help mechanism this slice
  reuses.
