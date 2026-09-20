---
slice: S74
phase: 9
title: The flags box becomes a collapsible checkbox panel with a summary row and per-flag help
delivers: []
---

# S74 - the flags control

> **Owner decision (2026-09-20, after the first S73 review).** The free-text flags field is an
> inheritance from S71's regex101 layout, never a decision. The owner asked for a control that uses
> little space on a laptop or a phone, shows what is selected when closed, enforces the pairs that
> cannot both be on, and explains what each flag means. The design below was agreed in
> conversation; no further review round before the sitting.

Today `FuzzyRegexOptions` names are typed into a text field, separated by commas or spaces, and a
typo is an error. The set is closed and small (13 members plus `None`), so an open text input is
the wrong control: it hides which flags exist, it needs an error path that checkboxes cannot
trigger, and it gives no room for an explanation.

## The control

**Collapsed**, one row inside the input pane, same footprint as a select:

```
Flags: IgnoreCase, BestMatch                        v
```

Reads `Flags: none` when nothing is ticked. The row is a native `<details>`/`<summary>` (the same
show/hide element the pane already uses for named lists), so it is focusable, announces open and
closed, and needs no custom keyboard code.

**Open**, a panel directly beneath the row, in the page flow (it pushes the content below it down
and scrolls with the pane; it never floats over the results):

```
Flags: IgnoreCase, BestMatch                        ^
  [x] IgnoreCase   (?)     [ ] Multiline               (?)
  [x] BestMatch    (?)     [ ] Singleline              (?)
  [ ] EnhanceMatch (?)     [ ] Word                    (?)
  [ ] RightToLeft  (?)     [ ] FullCase                (?)
  [ ] Posix        (?)     [ ] IgnorePatternWhitespace (?)
  Character set:   (o) Default   ( ) Ascii   ( ) Unicode
  Version:         (o) Version1  ( ) Version0
```

- Checkboxes for the independent flags, two columns from 480 px, one column below.
- Radio groups for the pairs that cannot both be on: `Ascii`/`Unicode` (with a Default that sets
  neither) and `Version0`/`Version1` (Version1 is the library default and is pre-selected, per the
  existing hint). Confirm the exclusivity list against `FuzzyRegexOptions.cs` and mrab-regex's
  rules before building; if the engine rejects any other combination, that pair becomes a radio
  group too.
- Order: the flags the worked examples use first (IgnoreCase, BestMatch, EnhanceMatch, RightToLeft,
  Posix), then the rest alphabetically. GOV.UK's rule: alphabetical by default, most-used first
  when that helps.
- Every flag has a `(?)` help button. On hover and on keyboard focus it shows one sentence; on a
  touch screen a tap shows the same sentence and a second tap or Escape hides it. The sentence is
  generated at build time from the `<summary>` doc comment of the enum member in
  `src/FuzzyRegex/FuzzyRegexOptions.cs` by `tools/build-demo-help.ps1`, into `help.json`, so the
  demo cannot drift from the library's own words. The help tab keeps the fuller text.
- The summary row and the C# snippet both derive from the same selected set; the snippet prints
  one enum member per line as before.

## What changes in the code

- `demo/web/src/App.vue`: the flags text input, its hint and its typo error path go; the
  `<details>` control, the checkbox grid, the two radio groups and the help buttons come in. State
  is a typed `Set<FlagName>` where `FlagName` is a union derived from `FLAG_NAMES` (already pinned
  to `Enum.GetNames<FuzzyRegexOptions>()` by `DemoSnippetTests`).
- `demo/web/src/demo.ts` and `shapes.ts`: the engine request carries the flags string built from
  the set (unchanged wire format, so `DemoEngine.cs` does not change), and the URL fragment keeps
  its `flags=` key so shared links from S72 still load.
- `demo/FuzzyRegex.Demo.Wasm/wwwroot/examples.json`: unchanged; the loader maps each example's
  flags string onto the set and reports any name it does not know as a test failure, not a runtime
  error.
- `tools/build-demo-help.ps1`: emits `flags: [{ name, summary }]` into `help.json` from the enum
  doc comments; a test asserts every `FLAG_NAMES` entry has a non-empty summary.
- Styles: the small checkbox size from the existing scale; the panel takes the same one elevation
  step as the named-lists section; contrast pairs for the help popover measured and recorded like
  the S73 tokens.

## Tests to add

- `flags.test.ts`: set to string round trip for every member; the radio groups never yield both
  members of a pair; the summary row text for none, one and several flags; alphabetical order after
  the examples-first block.
- `page.test.ts`: ticking a box re-runs the match and updates the snippet; a shared link with
  `flags=IgnoreCase,BestMatch` opens with both ticked; the help button shows and hides by hover,
  focus, tap and Escape; the panel opens in flow (the results region's offset moves, no element is
  `position: fixed` or `absolute` outside the help popover).
- `help.test.ts`: every flag has a one-sentence summary from the enum comments; the copy linter
  runs over those sentences too.
- `layout.test.ts` (built stylesheet): the collapsed row is one line high at 1366x768 and 390 px
  wide; the open panel has two columns at 480 px and one below.

## Verification

Screenshots at 1366x768, 1440x900 and 390x844 with the panel closed and open, committed beside
the S73 references. The fold check from S73 re-taken with the panel closed: pattern, subject,
action, count and first rows still above the fold. `npm run typecheck`, `npm test`,
`npm run build` and `tools/build-demo-web.ps1` green.

## Done when

- [ ] The text field is gone; every `FuzzyRegexOptions` member is reachable by checkbox or radio,
      the two exclusive pairs cannot both be on, and the summary row shows the selection when closed.
- [ ] Help sentences for all members generated from the enum doc comments, shown on hover, focus and
      tap, and passing the copy linter.
- [ ] Shared links and all worked examples load their flags into the control; no example names an
      unknown flag.
- [ ] The fold check at the two laptop sizes holds with the panel closed; the open panel stays in
      flow on the phone width.
- [ ] Tests above green; typecheck, vitest, build and `build-demo-web.ps1` green; contrast pairs
      recorded.
- [ ] Blind review (hunt: a flag that can be set by URL but not by the control; a pair the engine
      rejects that the control allows; a help sentence that drifted from the enum comment; a
      popover that clips at 390 px; a summary row that lies after a radio change), fix, commit.

## Sources (read 2026-09-20)

- https://www.nngroup.com/articles/listbox-dropdown/ - multi-selection: a listbox with checkboxes,
  never a dropdown; 5 to 15 options: either, dropdown if space is limited.
- https://www.nngroup.com/articles/drop-down-menus/ - dropdowns conserve space but are overused;
  keep the label in view when open; grey out unavailable options rather than removing them.
- https://design-system.service.gov.uk/components/checkboxes/ - checkboxes for several-of-many,
  radios for one-of-many, alphabetical by default, an exclusive option unticks the others.
