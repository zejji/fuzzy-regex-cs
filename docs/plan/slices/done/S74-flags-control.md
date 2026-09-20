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

- [x] The text field is gone; every `FuzzyRegexOptions` member is reachable by checkbox or radio,
      the two exclusive pairs cannot both be on, and the summary row shows the selection when closed.
- [x] Help sentences for all members generated from the enum doc comments, shown on hover, focus and
      tap, and passing the copy linter.
- [x] Shared links and all worked examples load their flags into the control; no example names an
      unknown flag.
- [x] The fold check at the two laptop sizes holds with the panel closed; the open panel stays in
      flow on the phone width.
- [x] Tests above green; typecheck, vitest, build and `build-demo-web.ps1` green; contrast pairs
      recorded.
- [x] Blind review (hunt: a flag that can be set by URL but not by the control; a pair the engine
      rejects that the control allows; a help sentence that drifted from the enum comment; a
      popover that clips at 390 px; a summary row that lies after a radio change), fix, commit.

## Sources (read 2026-09-20)

- https://www.nngroup.com/articles/listbox-dropdown/ - multi-selection: a listbox with checkboxes,
  never a dropdown; 5 to 15 options: either, dropdown if space is limited.
- https://www.nngroup.com/articles/drop-down-menus/ - dropdowns conserve space but are overused;
  keep the label in view when open; grey out unavailable options rather than removing them.
- https://design-system.service.gov.uk/components/checkboxes/ - checkboxes for several-of-many,
  radios for one-of-many, alphabetical by default, an exclusive option unticks the others.

## Closing notes (2026-09-20)

### What landed

The text input is gone. `demo/web/src/lib/flags.ts` is the new state layer and `App.vue` holds a
native `<details>` panel: a summary row reading `Flags: none` or the selection, ten checkboxes in a
grid, two radio fieldsets, and a `(?)` help button per flag that opens on hover, focus and tap and
closes on Escape. `demo/web/tests/flags.test.ts` (22 tests) pins the state layer; `page.test.ts`
pins the wiring; `DemoSnippetTests` pins the three things that could drift silently - the member
list, the help sentences and the way a flags string is split and trimmed.

**The string stays the state.** The `f=` fragment key is handed to `DemoEngine.Run` unchanged and
the panel is a pure view over it, so every shared link and every `examples.json` row written before
the panel loads into it untouched, and no mapping exists at any edge to rot. The invariant the
review hardened: *what the panel shows equals what the engine is given, for every string, including
the ones the engine refuses.*

### Deliberate deviations from the sketch above

- **Two options per radio group, not three.** The sketch had `Default / Ascii / Unicode`. Measured
  (`tools/probes/demo-flag-pair-exclusivity.ps1`): naming a default is identical to omitting it -
  `Options` came back `Unicode, Version1, FullCase` for a pattern compiled with nothing, with
  `Version1`, with `Unicode` and with both - so a third "Default" option would be a second control
  for a state `Unicode` already names. The default option is pre-selected and writes nothing into
  the string.
- **Exactly two radio groups.** All 91 unordered pairs of the 14 members were compiled by the same
  probe; exactly two are refused (`Unicode + Ascii`, `Version1 + Version0`). So the checkbox grid
  cannot reach a combination the engine rejects, and no third group was needed.
- **Help sentences committed as TypeScript, not generated into `help.json`.** `help.json` is a build
  artefact and the page is written to survive its absence by leaving the help tab shut - which would
  be a `(?)` button that explains nothing on a page served straight from the source tree. They live
  in `FLAG_HELP` and `DemoSnippetTests.The_flag_help_is_the_librarys_own_words` re-reads
  `FuzzyRegexOptions.cs` and fails the build when a doc comment and a sentence stop agreeing.
- **One column, not "two columns from 480 px".** Measured in the browser: the input pane is 383 px
  wide at 1366, 1440 and 1920, and 318 px at 390; the widest checkbox row is 253 px, so a second
  column needs 528 px. That width does not exist in this layout. The grid is
  `repeat(auto-fit, minmax(16rem, 1fr))`, so it becomes two columns by itself if the pane ever gets
  wider, and `layout.test.ts` asserts the shut row is one line at both widths rather than a column
  count that cannot happen.
- **Two screenshots, not six.** `flags-panel-1366.png` and `flags-panel-390.png` show the panel
  open; the shut state is already in the re-taken `page-1280.png` and `page-390.png`, so four more
  files would have shown the same row twice.
- **`FLAG_NAMES` moved into `flags.ts`** from `snippet.ts`, which now imports it. One list, still
  pinned to `Enum.GetNames<FuzzyRegexOptions>()`.
- The slice prose says "13 members plus `None`"; the enum has 14 plus `None`, which is what the
  tests count.

### Evidence, and how to re-run it

- `tools/probes/demo-flag-pair-exclusivity.ps1` - the 91 pairs and the default equivalence.
  `pwsh -File tools/probes/demo-flag-pair-exclusivity.ps1`.
- `tools/probes/s74-flags-panel.mjs` - the panel in a real browser: the shut row on one line, the
  tab order, the help popover at 390 px.
- `tools/probes/s74-flags-string-agreement.mjs` - the post-review probe, expectations recorded in
  its header. Both browser probes want a served publish:
  `node tools/probes/serve-demo-publish.mjs` on port 8213, then
  `browser_run_code_unsafe` with the probe's filename.
- **The fold, with the panel SHUT, measured rather than asserted** (2026-09-20, the fix pass;
  S73's case in the fragment, a Release publish served on 8213, Chrome through Playwright). The
  numbers are the distance from the top of the viewport to the bottom of each thing, in CSS pixels,
  and they are identical at 1366x768 and at 1440x900 because the input pane is a fixed width and
  the results pane scrolls inside itself:

  | | 1366x768 | 1440x900 |
  | --- | --- | --- |
  | pattern field | 121 | 121 |
  | subject field | 269 | 269 |
  | flags row, shut | 356 | 356 |
  | `5 matches` | 120 | 120 |
  | last of the five match rows | 507 | 507 |
  | page scrolls | no (768 of 768) | no (900 of 900) |

  So every part of S73's claim holds with the panel in the page: the whole answer for the default
  case is inside the first viewport at both laptop sizes, and nothing has to be scrolled to.
  The one correction to S73's wording is that there is no "primary action" to measure - the page
  answers as the boxes are typed in, and the only button in that row is `Stop`, which exists while
  a run is in flight. Taken with the fold block of `tools/probes/s73-widths.mjs` run inline at the
  two laptop sizes rather than the whole five-width walk. Re-run: publish with
  `pwsh -File tools/run-wasm-smoke.ps1 -OutDir .scratch/s74-publish`, serve that `wwwroot`, then
  `tools/probes/s73-widths.mjs`, whose default state is the shut panel.

  One thing to know before reading a rect out of that page: a `<details>` that is SHUT still hands
  back plausible geometry for what is inside it. Measured in the same session -
  `getComputedStyle(panel, '::details-content').contentVisibility` is `hidden`, not `display: none`
  - and a skipped subtree keeps its boxes, so `.flags-body` reported 625 px of height and
  `.flag-help-button` a 24x24 box at y=372 while the shut panel ended at 356. Nothing of it is
  painted (the screenshot shows the `Mode` fieldset at that point, and `elementFromPoint` agrees).
  A probe asking whether the panel is shut must read `details.open`, never a rect.
- `.NET` and upstream anchor positions behind the reworded `Multiline` sentence (2026-09-20):
  `/$/ on "a\nb\n"` gives `[3,4]` with no flags and `[1,3,4]` with `Multiline`; `/^/` gives `[0]`
  and `[0,2,4]`. Identical from `regex 2026.9.10` under `finditer`. Re-run: a file-based
  `dotnet run` over `FuzzyRegex.Matches`, and
  `python -c` over `regex.finditer` (both were scratch; the numbers are quoted here because the
  sentence on the page rests on them).
- Suite: ratchet GREEN at 6461 tests (6353 distinct ids, baseline updated), 297 web tests in 14
  files, `npm run typecheck` and `npm run build` clean.

### Review

**Three blind passes, because two of them found real defects and the fixes were code the previous
pass had never seen.**

*First pass, over the whole slice.* Two findings raised, both reproduced, both fixed.
(1) A link naming both sides of a refused pair - `f=Unicode,Ascii` - showed a legal single-sided
state: the row said `Ascii`, the radio claimed `Ascii`, and the engine beside it said the pair was
incompatible. Worse, the repair was a no-op, because a radio that already claims to be checked
fires no `change` when it is pressed. Fixed by keeping the conflict visible end to end:
`selectionFrom` keeps both sides, `chosen` returns `null` for a conflicted group, `formatFlags`
names the default only when the group is conflicted, and one press now repairs it.
(2) `selectionFrom` did not trim, while `TryParseFlags` splits with `StringSplitOptions.TrimEntries`,
so `f=IgnoreCase%0A` applied the flag with its box unticked and the row reading `none` - no error
anywhere, because both halves thought they agreed. Fixed with `trimToken`.

*Second pass, over that delta.* One finding, reproduced: a `token.trim()` mutant kept all 293 web
tests and all 5 C# tests green, because `char.IsWhiteSpace` and JavaScript's `String.trim` differ in
both directions. Measured with pwsh: `[char]::IsWhiteSpace((char)0x85)` is True and `0xFEFF` is
False. Fixed by mirroring `char.IsWhiteSpace` as a code-point set, adding the NEL/BOM test (it fails
under the mutant), and pinning both files from C# so the two cannot drift.

*Third pass, over the copy-linter delta* (the last Done-when box: the help sentences must pass the
copy linter, and `flags.ts` was not among the linted sources). Three findings, all three reproduced,
all three fixed.
(1) The reworded `Multiline` sentence said something the library does not do. To clear the
`says-what-it-is-not` rule I had rewritten "not just of the subject" as "by default they match only
at the start and end of the subject" - but `$` without `Multiline` also matches before a trailing
newline (`[3,4]`, not `[4]`, on `"a\nb\n"`). Fixed by dropping the claim about the default:
"^ and $ match at the start and end of every line, as well as of the whole subject", in the enum
doc comment and in `FLAG_HELP`.
(2) Three strings a visitor reads were still unlinted - the `Version` legend and the shut row's
`none` - because `literals()` needs two words to tell a sentence from an identifier. Fixed by
importing `FLAG_HELP`, `RADIO_GROUPS` and `summaryText` in `copy-sources.ts` rather than scanning
the file, with a guard test naming the two one-word strings.
(3) The help pin was blind to a TypeScript escape: `Unescaped` handled `\\` and the quote only, so
`'\b ...'` (a backspace character on the page) and `'\\b ...'` (the class the sentence is about)
both satisfied `The_flag_help_is_the_librarys_own_words`. Reproduced by planting the single
backslash - the pin passed. Fixed by reading escapes the way JavaScript does, including the
unknown-escape rule and `\uXXXX`; the planted typo now fails the pin, and
`The_flag_help_reader_reads_an_escape_the_way_javascript_does` covers the decoder.

No finding was rejected on reproduction in the third pass; in the first two, the reviewers' style
and speculative items were out of scope by the brief and were not read.
