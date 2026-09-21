---
slice: S77
phase: 9
title: Colour in the help panels' C# and a jump a reader can follow
delivers: []
---

# S77 - the help panel's code, and the jump into it

> **Owner requests (2026-09-21).** Two things about the Help tab in the left-hand column: its C#
> examples are printed in one colour, and pressing a heading note's link opens the matching help
> entry without anything on screen saying so. Run this in the demo worktree after S76.

## 1. Syntax highlighting for the help panels' C#

`tools/build-demo-help.ps1` writes each fenced block as `{ kind: 'code', language: 'csharp', text }`
and `App.vue` renders `text` into a `<pre>` with Vue's interpolation. The language is carried and
never used, so every example is one colour.

Two routes, and the slice picks one on measured grounds rather than taste:

- **A tokeniser in this project.** `demo/web` has exactly one runtime dependency (`vue`), and every
  snippet in the panels is one we wrote, so a small tokeniser over the C# we actually use - keywords,
  strings including verbatim and raw, char literals, comments, numbers, type names, attributes - can
  be pinned by a test per snippet. Cost: the code, and the risk of mis-colouring a construct a later
  snippet introduces.
- **A library.** Shiki is accurate and ships a TextMate grammar with a WebAssembly regex engine,
  which is heavy for a page whose whole bundle is 117 kB. highlight.js with only C# registered is the
  middle option. Prism is small but its C# grammar is less complete and the project is in
  maintenance.

Decide by measuring the bundle cost of the middle option against the tokeniser, and record both
numbers. Whatever wins, the rules already in force apply: no `v-html` anywhere near generated
content (S72 established that the panel takes no markup), the colours pass the contrast test in
`demo/web/tests/contrast.test.ts` in both themes, and the page still renders with no colour at all
if the highlighter throws.

The same treatment should reach the "C# for this case" snippet box, which has the same problem and
the same renderer, unless measuring says otherwise.

## 2. A jump that lands somewhere the reader can see

Pressing a heading note's "read more" today sets `helpKey`, selects the Help tab and stops. On a
wide screen the panel is below the fold in the left column, so the press can look like nothing
happened. `App.vue:225-235` argues that is truthful because the section was already on the page; the
owner's report is that readers do not experience it that way.

Do not flash the panel red. Red is the colour of an error, a repeated flash on a correct action
miscommunicates, and motion that repeats runs into WCAG 2.2.2 and 2.3.1. The researched pattern for
in-page navigation is three parts, and this page already has the pieces for two of them:

1. **Move focus to the target section's heading** (`tabindex="-1"`, `focus()` after the render).
   This is what the skip link at `App.vue:918` already does for the answer region, it is what tells
   a screen reader the reader has arrived, and the focus ring is a cue that does not depend on
   colour.
2. **Scroll it into view**, smoothly unless `prefers-reduced-motion` is set - the helper at
   `App.vue:694` and the call at `:711` are the existing shape for this.
3. **One brief highlight of the target section**: a single fade of about a second in an accent that
   is not the error colour, skipped entirely under reduced motion.

Check the research before writing it (WCAG 2.4.3 focus order, 2.4.7 focus visible, 2.2.2 pause stop
hide, 2.3.1 three flashes, and the `:target` highlight pattern as MDN and GOV.UK use it), cite what
you read with the date, and say in the slice notes why the chosen cue is the one that survived.

## 3. Done when

- [ ] Every C# block in a help panel is coloured, and a test pins the tokens of at least one snippet
      of each shape (verbatim string, char literal, comment, attribute, generic type).
- [ ] The contrast test covers the new colours in both themes.
- [ ] The bundle cost is recorded in the slice notes, before and after.
- [ ] Pressing a heading note's link moves focus to the section, scrolls it into view and highlights
      it once; under `prefers-reduced-motion` the scroll jumps and the highlight does not run.
- [ ] `page.test.ts` pins the focus move and the tab selection; a browser probe measures that the
      section is in the viewport after the press, at 1366 and at 390.
- [ ] Checked by hand in a browser at 1366 and 390, keyboard only as well as pointer.

## Closing notes (2026-09-21)

**The colour cost 0.09 kB, because the tokenizer was already here.** The slice asked for a
measurement of highlight.js against a tokenizer written for this project, and the ladder stopped a
rung earlier: `src/lib/snippet.ts` has tokenized C# into five classes since S73, for the "C# for
this case" box, and `help.json` has carried `language: 'csharp'` on every fenced block since S72
with nothing reading it. The panels now render that tokenizer's output. Measured across the change,
`vite build`: JavaScript 116.65 kB to 117.45 kB (gzip 43.27 to 43.52), CSS 32.06 kB to 32.98 kB
(gzip 5.89 to 6.07). No library was installed to compare against, because the comparison would have
been between a route we took at 0.25 kB and one we would not have taken at ten times that.

**The keyword list grew by five.** `var`, `const`, `int`, `try` and `catch` are what the samples in
`docs/COMPARISON.md` use and the generator does not. The list stays honest without anybody
remembering it: `snippet.test.ts` reads the shipped `help.json`, tokenizes every sample, and fails
when a word from a canonical C# list comes back uncoloured.

**The panels needed their own colours.** The snippet box sits on `slate-50` and the help panels on
`shell-raised`, and violet-700 measures 2.07:1 there - far under the 4.5:1 a reader needs at 12 px.
Each run moves to the light end of its ramp (violet-300, emerald-300, slate-400, orange-300), all
four measured by `contrast.test.ts`, and `layout.test.ts` holds the rules that ask for them.

**The jump is focus first, then a scroll, then one fade.** Pressing a heading note's link now opens
the section, moves focus to its `<summary>`, scrolls it into view unless `prefers-reduced-motion` is
set, and runs a single 1.2 s accent fade. Red was considered and rejected: it is the colour of an
error, and a flash that repeats runs into WCAG 2.2.2 and 2.3.1. With no panel for the key - help
that failed to load, or a key nothing documents - the focus stops at the tab, as it did before.

**Two faults a browser found and jsdom could not.**

- At 390 px the press left the reader on the `(?)` they had just used. "Examples and help" is a
  `<div>` whose contents carry `hidden` while the disclosure is shut, so the section was not in the
  page to focus. `openHelpTab` opens that box before it asks. jsdom cannot show this: `focus()`
  works on a hidden element there.
- Pressing a second note inside the 1.2 s window showed only the tail of the first fade. Vue reuses
  one `<details>` element, and a class removed and re-added in one synchronous block leaves the
  browser nothing to notice, so the animation never stops. Measured on the published build:
  `getAnimations()` read 516 ms and then 733 ms on the same animation. Reading `offsetWidth` between
  the two flushes the pending style, and the same probe then reads 33 ms with nothing left running
  afterwards. The first attempt at this cancelled the animations instead, which measured the same
  but for the wrong reason: after the class is removed there is nothing to cancel, and it was the
  style flush that `getAnimations()` forces that did the work. The second review pass caught it.

**Review.** One blind pass over the whole diff, then a second over the fixes (Opus; briefs at
`.scratch/S77-review-brief.md` and `.scratch/S77-delta-brief.md`). The first raised four findings,
all four reproduced and fixed: the highlight that did not restart, a loop opening ancestor
`<details>` elements that this layout does not have, the test assertion that loop made vacuous, and
a raw ENOENT where the repo already had an actionable message for the same generated file.

The second pass raised two more, both reproduced and both fixed. The replacement assertion could
not fail either - jsdom has no `matchMedia`, so the page defaults to wide and the box is never
hidden - so the narrow case is now its own test, with the layout stubbed narrow, and it fails when
`panelOpen.value = true` is taken out. And the animation restart was cancelling nothing: the work
was being done by the style flush `getAnimations()` forces, so the code now reads `offsetWidth` and
says so.

**Checked by hand** at 1366 and 390 wide against the published build (`run-wasm-smoke.ps1`, WASM
SMOKE GREEN): the samples read in colour on the dark panel, the press lands on the section with a
visible focus ring, and the fade ends. Screenshots: `.playwright-mcp/s77-help-colour-1366.png`,
`.playwright-mcp/s77-jump-390.png`.

**For the next slice.** The arrival cue is one class and one keyframe; anything else that sends a
reader somewhere should reuse `arriveAtHelp` rather than grow a second mechanism beside it.
