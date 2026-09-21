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
