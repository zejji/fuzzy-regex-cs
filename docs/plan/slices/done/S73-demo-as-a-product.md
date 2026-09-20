---
slice: S73
phase: 9
title: Demo v3 - results above the fold, plain-English copy, one visual identity, and a C# snippet to paste
delivers: []
---

# S73 - the demo judged as a product

> **Owner brief (2026-09-19, first look at the live page).** Seven requirements, (i) to (vii) below.
> The verdict on v2: the match results sit below the fold on a laptop, which is unintuitive; the
> copy reads as machine-written; the visual design is flat; the match numbers look like links and
> appear to do nothing when clicked. **The owner reviews this draft before any sitting
> runs.** Nothing here starts until that review is done.

v1 built the page and v2 made it a feature tour. This slice makes it usable in ten seconds by a
stranger: the answer visible on load, words a human wrote, a look with a point of view, and a way to
take the case away as C#. No new engine behaviour, no new samples.

## What is wrong now, read from the code

- **Layout.** `App.vue:168` is a document: `mx-auto max-w-6xl px-4 py-8`, a header, then one card of
  six inputs, then the status row, then the highlighted subject, then the match table. At 768 px
  tall the answer starts below the input stack, and the page scrolls as a whole, so the inputs
  leave the screen when the reader goes looking for the result.
- **Selection looks broken.** `.row-select` (`styles.css:191`) is accent-coloured and underlined, so
  it reads as a hyperlink. `select(i)` does run, but there is no `scrollIntoView` anywhere in
  `demo/web/src`, and the visible effect of a selection is the group table further down the page.
- **The GitHub link** exists once, in the footer (`App.vue:667`), at the bottom of a page nobody
  scrolls to. **The copy** carries the tells listed under (ii): "so this is the documentation itself
  and not a second copy of it", "Upstream's language, not .NET's".

## Deliverables

**(i) The answer is above the fold at 1366x768 and 1440x900.** With the default case loaded, the
pattern field, the subject field, the primary action, the match count and the first three rows of
the match table sit inside the first viewport, with no page scrolling available. The input pane is
fixed; the results region scrolls on its own.

**(ii) Every user-facing string rewritten** against the banned list below: `App.vue`'s template and
computed labels, `demo.ts`'s status and error strings, `index.html`'s title and meta description,
every `title` and `note` in `examples.json`, the wrapper prose `tools/build-demo-help.ps1` emits
into `help.json` (the extracted `COMPARISON.md` prose belongs to Phase 8 and is fixed there if it
fails the same rules), `demo/README.md`'s first screen, and every `aria-label`. Record the page's
word count before and after.

**(iii) One visual identity**, tokens below. The test is a screenshot the owner calls interesting:
a page with a hierarchy rather than a form on white. **(iv) Application layout**: regions,
breakpoints and what scrolls are specified below, each choice tied to a source. **(v) A "C# for
this case" action**, specified below. **(vi) The GitHub link in the header**, a labelled control
carrying the repository name, with the footer link kept:
`https://github.com/zejji/fuzzy-regex-cs`.

**(vii) Two-way linking between a match and its row.** Clicking a row or its number scrolls the
subject pane so that match is in view and gives the highlight a selected state distinct from hover.
Clicking a highlight scrolls the match table to that row and selects it. Both work from the keyboard
with the existing roving tabindex, both use `scrollIntoView({ block: 'nearest' })` so a match
already in view does not jump, and both respect `prefers-reduced-motion`. Anything that is not a
link loses its link styling, starting with the underlined row number.

## Layout spec

Three regions inside one `100dvh` shell. The page body never scrolls; each region owns its overflow.

| Region | Wide (>= 1024 px) | Narrow (< 1024 px) |
| --- | --- | --- |
| Header, 48-56 px | Product name, mode control, GitHub link. Fixed. | Same, title only |
| Input pane | Left column, 380-440 px, fixed height, scrolls internally when replacement and named lists are open | Top pane: pattern and subject visible, the rest in a disclosure |
| Results region | Right column, fills the rest, scrolls on its own: highlighted subject on top, match table below with a sticky header row | Below the input pane, scrolls |
| Examples and help | Tabbed panel at the foot of the input pane at every width (owner decision: one layout to maintain) | Two tabs, "Examples" and "Help", collapsed by default |

Rules: at most two scroll regions reachable at once, since stacked scroll panes cause the
discoverability errors Smashing describes; the examples panel is a disclosure or a tab set, never a
hover panel, because a keyboard cannot reach hover; the tab set follows NN/g's rules (one row, one
always selected, labels of one or two words, panel below the tabs, two selection indicators).
Breakpoints follow Material's window size classes: one pane under 600 px, two from 840 px. No
third column at any width. Gate the two-pane layout on height too, since 1366x768 is expanded in
width and medium in height.

## Visual identity

Tokens extend `@theme` in `styles.css`; no new dependency.

- **Palette.** Keep the OKLCH tokens and S72's measured match hues. Add one ink surface for the
  header and input pane (`--color-shell: oklch(0.21 0.02 258)`) so the panes read
  as an application frame. The accent stays interactive-only.
- **Meaning in colour.** Give the three fuzzy edit types their own hues, on the counts chips and on
  the per-edit underlay inside a highlight: substitution amber, insertion green, deletion red, each
  at AA and each carrying its letter (s, i, d), so colour is never the only signal.
- **Type.** Two families: a grotesque for the interface, a monospace for pattern, flags, subject and
  snippet. Scale 12 / 14 / 16 / 20 / 28, product name at 28, section labels at 12 uppercase with
  tracking. Subject line length capped near 90ch. **Spacing:** Tailwind steps only, as today, with
  4 / 8 / 12 / 16 / 24 the working set.
- **Depth.** One elevation step for the results region over the shell (1 px border plus a soft
  shadow) and a second for the snippet panel. No gradients, no glass. One scheme, light, done well
  (owner decision 2026-09-19): the `prefers-color-scheme: dark` block is removed rather than left
  half-maintained, and every new colour pair is measured and recorded.

## The C# snippet

A pure function, `toCSharp(inputs: Inputs): string`, in `demo/web/src/lib/snippet.ts`, typed against
the existing `Inputs`. It prints the API `DemoEngine` itself calls (`DemoEngine.cs:324, 375, 451,
507`), signatures from `src/FuzzyRegex/PublicAPI.Unshipped.txt`. A walk with two flags prints:

```csharp
// dotnet add package FuzzyRegex
using Fuzzy.Text.RegularExpressions;

FuzzyRegex regex = new(
    @"(?:kitten){e<=2}",
    FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.BestMatch,
    TimeSpan.FromSeconds(2));

foreach (Match match in regex.EnumerateMatches("sitting kitchen"))
{
    FuzzyCounts counts = match.FuzzyCounts;
    Console.WriteLine($"{match.Index}+{match.Length} s={counts.Substitutions} i={counts.Insertions} d={counts.Deletions}");
}
```

One variation per input: no flags prints `FuzzyRegexOptions.None`; partial mode prints
`Match match = regex.Match(subject, partial: true);` with a `match.Success` check; replace mode
prints `string replaced = regex.Replace(subject, @"...");`; named lists print a
`Dictionary<string, IReadOnlyCollection<string>>` initialiser as the fourth constructor argument.
Literals: a verbatim string with `"` doubled, a raw string literal when the value holds a newline,
a longer fence when it holds three quotes. The two-second timeout is the demo's own
(`DemoEngine.cs:103`), and the comment beside it says so; the owner wants visitors to see that a
timeout is normal. The owner's one rule for the snippet: the method call and every option must
be obvious at a glance, so each option gets its own line and the flags enum is never abbreviated.

Presentation: a **revealed panel** docked to the foot of the results region, opened by a "C# for
this case" button, closed by Escape or the same button, with focus moved into the panel and returned
on close. A hover popover is rejected because a keyboard cannot open one; a permanent block is
rejected because it spends the space requirement (i) needs. Copy uses
`navigator.clipboard.writeText`, which works in secure contexts and rejects with `NotAllowedError`
when refused; the catch path selects the snippet text and shows "press Ctrl+C", and either outcome
is announced in the existing live region. Colour comes from a tokenizer of about 60 lines over five
classes (keyword, string, comment, number, other) rendered as spans from an array, never through
`v-html`; highlight.js, Prism and Shiki each dwarf the feature. S72 dropped regex101's code
generator; this is narrower, one language and this library's own API.

## Copy rules and the banned list

Plain English, the shortest exact form, and no sentence that tells the reader what the screen
already shows. Banned outright:

- **Triads.** Three adjectives or three clauses in a row. Wikipedia's list names the rule of three
  as the commonest tell.
- **Negative parallelism**: "not X but Y", "it's not X, it's Y", "no X, no Y, just Z"
  (WP:AIPARALLEL). This catches today's "Upstream's language, not .NET's".
- **Explaining what a thing is not**, or correcting a misconception the reader never had.
- **Rhetorical set-ups**: "What this means is", "Here's the thing", a question asked to answer it.
- **Puffery**: seamless, robust, powerful, rich, comprehensive, cutting-edge, blazing, elegant,
  intuitive, simply.
- **Corporate verbs**, from the GOV.UK words-to-avoid list: leverage, utilise, facilitate, deliver
  (of anything but a parcel), empower, streamline, foster, drive, transform, key (as an adjective),
  robust, deploy (of anything but software), going forward.
- **LLM vocabulary**: delve, dive into, crucial, pivotal, meticulous, intricate, tapestry,
  testament, showcase, garner, enhance, ensure, seamlessly, "designed to", "serves as", "stands as",
  "boasts", sentence-initial "Additionally", "It's important to note".
- **Trailing -ing summaries**: ", highlighting X", ", underscoring Y", ", ensuring Z".
- **Restating the obvious**: a hint that repeats its own label, a paragraph saying the page does
  what the page visibly does, a sentence that summarises the three above it.
- **Em dashes** in UI copy. Use a hyphen or a full stop.

Enforced by `demo/web/tests/copy.test.ts`, which walks every string in `examples.json`, the
generator's wrapper text and the extracted template strings, and fails on a banned word or pattern,
naming the string and the rule. Keep the rules in one exported array.

## Tests to add

- `snippet.test.ts`: one case per mode (walk, partial, replace); one per literal form (plain, quote,
  newline, both); flags to `FuzzyRegexOptions` including `None` and two combined; named lists to a
  dictionary initialiser; the emitted timeout matching the demo's constant. One expected snippet is
  pasted into a scratch console project and compiled during the sitting, its output quoted in the
  closing notes, because a snippet nobody has compiled is a guess.
- `copy.test.ts`: the linter above, plus a self-test that "seamlessly leverages our robust,
  powerful, intuitive engine" is rejected and names three rules.
- `clipboard.test.ts`: a resolving `writeText` reports success; a `NotAllowedError` rejection falls
  back to selecting the text and says so; neither path throws.
- `layout.test.ts` (Vitest and jsdom; the Playwright pass below is the real check): one scroll owner
  per region, and the body cannot scroll. `page.test.ts` additions for (vii): clicking a row selects
  that match, marks the highlight
  current and calls `scrollIntoView` on the highlight; clicking a highlight selects the match and
  calls `scrollIntoView` on the row. The two directions are asserted separately with a spy on
  `scrollIntoView`, and a third case proves Enter on a focused highlight does what a click does.
  Every existing suite stays green, and no test is deleted to make a layout change pass.

## Verification

- `pwsh -File tools/build-demo-web.ps1` green (type-check, Vitest, bundle). Then Playwright at
  1366x768 and 1440x900: load the default case, screenshot, and assert by bounding box
  that the pattern, the subject, the match count and three table rows sit inside the viewport, and
  that `document.body.scrollHeight <= window.innerHeight`. Repeat at 1920x1080, 1024x768, 390x844;
  screenshots into the closing notes.
- Keyboard pass end to end: header link, every field, mode, the examples tabs, a match row, a
  highlight, the snippet panel open, copy, Escape, focus returned. Contrast measured for every new
  pair, including the three edit hues. The snippet compiled for real, its printed spans compared
  against the demo's answer, and `prefers-reduced-motion: reduce` honoured by both scroll calls.

## Done when

- [x] At 1366x768 and 1440x900, with no page-level scrolling, the pattern, the subject, the match
      count and three match rows are in the first viewport, and the Playwright assertion proves it.
      (Chunk 2. The navigation, the `evaluate` and both answers are in the sittings notes.)
- [x] Every string in the scope list rewritten; `copy.test.ts` green and seen to fail on a planted
      violation; the page's word count recorded before and after. (Chunk 1, with the extractor's two
      defects fixed in 5f. 1,185 words before, 1,148 after, the table in the sittings notes.)
- [x] The visual tokens landed, the dark block removed, the contrast ratios recorded, and the
      owner has accepted the screenshots. Layout regions behave as the table says at all five
      widths, each decision traced to a source. (Chunks 5c to 5e. Seventeen tokens recorded as
      Chrome paints them, every pair at or above 4.5:1, and the five-width table measured in one
      scripted pass. **Owner acceptance of the screenshots is outstanding** - `docs/demo/` holds
      the two re-taken reference layouts and it is in STATE.md.)
- [x] The snippet panel generates, colours and copies for all three modes; the clipboard fallback
      exercised with permission denied; one snippet compiled and its output quoted. The header
      carries the GitHub link and the footer link still works. (Chunks 3 and 4.)
- [x] Both directions of match linking work by pointer and by keyboard, with a selected state
      distinct from hover, and nothing that is not a link is underlined. (Chunk 4, measured on the
      published page - the table in the sittings notes. By keyboard means Enter and Space on the
      focused half: an arrow moves the selection and the focus only, because a browser scrolls what
      it focuses and the counterpart reveal was measured being cancelled by it.)
- [x] `tools/build-demo-web.ps1` green, the Vitest suite green, the accessibility items met; then
      blind review and commit. (5f. Build gate green, 259 Vitest tests green, no focus stop off
      screen or without a ring at any of the five widths, and the answer reachable in two keystrokes
      from the top.) **Hunt:** a fixed shell that traps content on a short window (768 px
      tall with browser chrome, or 200 % zoom, where "no page scroll" becomes "cannot reach the
      button"); `100vh` on mobile Safari where the toolbar makes it wrong (`100dvh`); a snippet that
      compiles for the samples but not for a pattern holding three quotes or a trailing backslash; a
      copy rewrite that drops a validation message or a cap refusal; the linter passing because it
      never sees the strings it was written for; `scrollIntoView` moving the whole shell; hues for
      substitution and deletion that a red-green colour-blind reader cannot separate.

## Owner decisions (2026-09-19, on the draft)

1. Snippet imports the namespace `Fuzzy.Text.RegularExpressions` with a `dotnet add package
   FuzzyRegex` comment above it; clarity of the call and its options is what matters.
2. Examples and help are tabs at every width; no right rail.
3. Light scheme only, done well; dark can return later as its own slice.
4. The snippet prints the demo's two-second timeout.
5. `docs/plan/STATE.md` is amended by the sitting that starts S73, not before.

## Sources (all read 2026-09-19)

- <https://en.wikipedia.org/wiki/Wikipedia:Signs_of_AI_writing> - WikiProject AI Cleanup's catalogue
  of tells: negative parallelism (WP:AIPARALLEL), puffery (WP:AIPUFFERY), the vocabulary list
  (WP:AIVOCAB), the trailing -ing summary (WP:SUPERFICIAL), and the rule of three.
- <https://guidance.publishing.service.gov.uk/writing-to-gov-uk-standards/style-guides/a-to-z-style-guide/>
  - GOV.UK's words to avoid (leverage, robust, deliver, foster, streamline, key, utilise and the
  rest), under the rule that plain English is mandatory and abstractions slow comprehension.
- <https://www.nngroup.com/articles/scrolling-and-attention/> - Fessenden 2018, 120 participants and
  about 130,000 fixations: 57 % of viewing time above the fold, 74 % in the first two screens, and
  over 65 % of the first screen's attention in its top half. Put the answer in the first screen.
- <https://developer.android.com/develop/ui/compose/layouts/adaptive/window-size-classes> - Material
  window size classes: one pane under 600 dp, two from 840 dp, two wide panes from 1200 dp, and a
  window that is wide but short should not go multi-pane, which is the 1366x768 case.
- <https://www.nngroup.com/articles/tabs-used-right/> - tabs need mutually exclusive content, one
  row, one always selected, one or two-word labels, the panel below the list, and at least two
  selection indicators.
- <https://www.smashingmagazine.com/2023/05/sticky-menus-ux-guidelines/> - fixed and sticky panels:
  keep them small, and avoid stacking scroll panes, which cause discoverability errors and repeated
  actions. The two-scroll-region cap comes from here.
- <https://developer.mozilla.org/en-US/docs/Web/API/Clipboard/writeText> - secure contexts only,
  widely available since March 2020, returns a promise, and rejects with `NotAllowedError` when
  writing is refused, which is why the fallback path exists.

## Closed 2026-09-20

Eight sittings over two days, in six chunks; the per-sitting record is
`docs/plan/slices/notes/S73-sittings.md` and it is long, because the measurements live there.

**What landed.** The demo is a product rather than a test page: a shell that fits 1366x768 and
1440x900 with the answer in the first viewport, every string rewritten against a copy linter that
runs in the suite, a light visual identity whose seventeen tokens are pinned to what Chrome actually
paints, a C# snippet panel for all three modes with a clipboard fallback, two-way linking between
the highlighted subject and the match table by pointer and by keyboard, a per-edit underlay that
shows where a fuzzy match spent each error, a skip link that reaches the answer in two keystrokes,
and reference layouts taken by a script rather than by hand.

**Anything surprising.** Three things, each of which looked like something else first:

- **A test can be green because it is asking the wrong process.** `tests/dev-server.test.ts` failed
  five ways for a sitting and a half and the cause was not in the test's subject at all: Vite
  defaults its root to `process.cwd()`, and the suite built its server without one, so it was
  serving whichever directory the caller started in. The fix is one word in the config. The guard
  then went through two unfalsifiable shapes before the third, because `npm test` already runs from
  the right directory and nothing that stays there can tell the pin from its absence.
- **A wait can be satisfied by the wrong answer.** `s73-widths.mjs` typed its case in and waited for
  `mark.hit`, but the page arrives with a case of its own already answered, so the selector was
  true from the first paint and every box was measured mid-render. The independent verifier found
  it by failing to reproduce the numbers this slice had recorded.
- **A machine-wide Roslyn stall looks exactly like a slow test suite.** A hung `VBCSCompiler.exe`
  held by another checkout killed the ratchet at 1200 s with every .NET process at 0% CPU.
  `-p:UseSharedCompilation=false` is the non-destructive way past it.

**What the next slice should know.** The two `docs/demo/` reference layouts still need the owner's
eye - that is the one part of a Done-when box that this slice could not close itself. The demo is
not yet published: the owner has to push `phase9-demo` and set the Pages source. Four citations of
`_regex.c:20535-20537` outside this slice's files name the top of `match_fuzzy_changes` rather than
the deletion shift at `:20555-20558`; they are in STATE.md as maintenance. No oracle wave and no
negative control: this slice touched no engine code.

**Review.** Four blind passes and one independent verifier, all Opus, each briefed to hand over a
reproduction rather than an opinion. Across the slice: chunk 2's pass raised 3 findings, all 3
reproduced and fixed, with a second pass over the four fixes and a third over `toggleDisclosure`;
chunk 3's raised 2, both fixed; chunk 4's raised 3, all fixed. Chunk 5's pass covered two bodies of
unreviewed code at once - the chunk-5 diff and the chunk-1 fix delta no reviewer had seen - and
**raised 8, of which 8 reproduced and 8 are fixed**. A further pass over the one test rewritten
after that review found the rewrite unfalsifiable, with a reproduction; reproduced here and fixed by
deleting the test and making the whole file run from somewhere else, which is the condition the
regression needs. The verifier (amendment 16 limb (d)) re-ran roughly a hundred numbers from the
committed tree: five were wrong - one bad upstream line citation, one token count, three timings
that do not repeat, one claim attributed to the wrong file, and one probe that no longer produced
its own table - and all five are corrected in the notes, with the probe fixed in code. Chunks 3 and
4 had their own verifier runs at the time.
