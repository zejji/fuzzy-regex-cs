# S73 sittings

Spec: `docs/plan/slices/S73-demo-as-a-product.md`. Per-sitting record only.

## The chunk plan (sitting 1, 2026-09-19)

S70 took four sittings, S71 five, S72 two, and the overrun was all browser work. S73 is larger than
any of them, so it is cut into five chunks, each sized to finish inside an hour with a green commit,
because the owner closes the laptop without notice.

1. **Copy.** `copy.test.ts` - the banned-list linter - written first and seen to fail on today's
   strings, then every user-facing string rewritten. Word count before and after. Deliverable (ii).
2. **Shell.** The `100dvh` three-region layout, the tab set for examples and help, the visual
   tokens, and the `prefers-color-scheme: dark` block removed. Deliverables (i), (iii), (iv).
3. **Snippet.** `snippet.ts`, its tests, the revealed panel, the clipboard fallback. One snippet
   compiled for real. Deliverable (v).
4. **Linking.** Two-way match-to-row linking, the header GitHub link, and the link styling removed
   from everything that is not a link. Deliverables (vi), (vii).
5. **Verification and close.** `tools/build-demo-web.ps1`, the published page at five widths under
   Playwright, keyboard pass, contrast measurements, screenshots, blind review, independent
   verifier, closing notes.

Chunk 1 runs the linter before the rewrite so that every string written in chunks 2 to 4 is checked
as it is typed, rather than being rewritten twice.

## Sitting 1 (2026-09-19)

**Starting state.** Tree clean at `7b335b6`; `demo/web` suite green, **91 tests in 7 files**, before
anything was touched. `demo/web/node_modules` is populated here (79 entries) and PID 34120, the dev
server S72 reported as holding it, is gone - so `npm ci` and `tools/run-wasm-smoke.ps1` are
available again to chunk 5.

**STATE.md** amended to say S73 is in flight, per the owner's decision 5 on the draft.

### What chunk 1 landed

**The linter, as data.** `tests/copy-rules.ts` holds nine rules - triad, negative-parallelism,
says-what-it-is-not, rhetorical, puffery, corporate, llm-vocabulary, trailing-ing, em-dash - each
with the source that asked for it (Wikipedia's "Signs of AI writing", GOV.UK's A to Z). Every rule
fires on a *form*. "Restating the obvious" is in the slice's ban list and is deliberately absent
here: no pattern separates a sentence that repeats its label from one that adds the fact the label
leaves out, so that stays a review question rather than becoming a rule that gets switched off.

**The strings, extracted rather than listed.** `tests/copy-sources.ts` reads them out of the files
that hold them, so a string added tomorrow is linted without anyone remembering to add it. The risk
in that is a linter passing because its extraction found nothing, so each source carries a guard
naming a string it must contain and a floor for how many it must find, and the extraction itself
has unit tests (comments out, `${...}` out, `{{ }}` out).

**The rewrite.** 18 notes and 4 titles in `examples.json`, the header, every hint, the status pills,
the group-capture note, the help footer, `demo.ts`'s refusals, `index.html`'s description and
`<noscript>`. Patterns, flags and subjects were not touched; the four renamed titles carried
through to `DemoExamplesTests`'s `_upstream` keys and its doc-comment table.

**Word count.** 1,185 words before, 921 after over the same six sources: a fifth of the page gone.
The full method, and why the printed total is 1,078 rather than 921, is in the doc comment of
`tests/word-count.test.ts`; both numbers are reproducible with the command it names.

### Review (chunk 1)

One blind pass over the chunk-1 diff. **Six findings raised, six reproduced, six fixed** - unusually
high for this project's four-in-five kill rate, and the reason is that four of them were about the
linter's *reach* rather than about the copy, which is exactly what a first pass over a new tool
should find.

1. **Unlinted user-facing text.** `src/lib/shapes.ts` and `DemoEngine.cs` write into the same
   `role="status"` paragraph the linted strings appear in, and neither was a source. Added both.
   They immediately went red on four strings - see 3.
2. **Bound attributes invisible.** `:aria-label="'Groups in match ' + (selected + 1) + ', scrollable
   sideways'"` is read out to a screen reader exactly as a static label is, and the extraction only
   read static ones. Four such labels in `App.vue` were unlinted; they are now.
3. **The four strings finding 1 exposed**, all tripping `says-what-it-is-not`. Rewritten into the
   form that tells the reader what to do instead of what they did wrong: `Unknown flag '{x}'.
   Flags are FuzzyRegexOptions member names.`, `Unknown mode '{x}'. The modes are ...`, `Line
   '{x}' needs a colon. ...`, and `the engine's reply could not be read: it arrived in the wrong
   shape`. Two tests pin those messages and were updated with them:
   `DemoEngineContractTests.An_unknown_mode_is_an_error_naming_it` and seven assertions in
   `shapes.test.ts`. Neither assertion was weakened - each still pins a specific refusal.
4. **`rhetorical` over-fired.** Its `/\?\s+\S/` limb read `The method takes a TimeSpan? timeout
   argument.` as a rhetorical question, and C# is a linted source from finding 1 onwards. Narrowed
   to a sentence that *opens* with a question word; the self-test for `Why a budget? Because ...`
   still passes and a nullable-type sentence was added to the "leaves alone" list.
5. **A hedge dropped.** The rewrite turned "An alternation normally takes the first branch..." into
   an absolute that the very next sample in the sidebar (POSIX leftmost-longest) disproves.
   Restored as "By default an alternation takes...".
6. **`demo/README.md` contradicted itself**: line 4 was updated to "six inputs" and line 24 still
   said three. Fixed. The README is deliberately *not* a linted source, and the reason is recorded
   in `copy-sources.ts`: a visitor never reads it, and interface-copy rules would ban the words
   that belong in maintainer prose ("deploy", "key difference").

**The fix delta is itself unreviewed** - findings 1 to 6 touched `copy-rules.ts`, `copy-sources.ts`,
`shapes.ts`, `DemoEngine.cs` and two test files after the reviewer saw the diff. Chunk 5's pass
covers the whole slice diff, so that delta gets its first reading there; it must not be skipped on
the grounds that chunk 1 was already reviewed.

### State at the checkpoint

`tools/check-ratchet.ps1`: **GREEN**, 6,399 passing, 6,291 distinct ids, baseline updated with
`-AcceptRemovals` for the four renamed sample ids. `demo/web`: **124 tests in 9 files** green
(91 before the chunk), `vue-tsc --build` clean. `.vite/` at the repository root - a dependency
cache a root-level vitest run writes - is now gitignored; an unignored directory there fails the
driver's clean-tree check.

**Chunk 2 starts here:** the `100dvh` shell, the tab set, the visual tokens, and the removal of the
`prefers-color-scheme: dark` block. Every string it writes is already under the linter, so write
them to the rules rather than fixing them afterwards.

## Chunk 2 - the shell (2026-09-19)

Deliverables (i), (iii) and (iv): the three-region shell, the tab set, the visual identity.

**What landed.** `.shell` is a header, a `main` of two panes and a footer. Above
`(min-width: 64rem) and (min-height: 600px)` it is `100dvh` with `html, body { overflow: hidden }`
and the two panes own the only scrolls; below either line the whole thing releases and the page is
an ordinary scrolling document. The examples and the help are one ARIA tab set at the foot of the
input pane - roving tabindex, ends hold, automatic activation - and loading a sample that has a
documented section switches to the Help tab. On one column the secondary inputs and the whole tab
set fold behind two disclosures. The palette is the ink shell plus one raised white island, with
three edit hues that each carry their letter (`2 s`, `1 i`, `1 d`) so nothing rests on colour alone.
`prefers-color-scheme: dark` is gone, per the owner's decision 3.

**Measured in a real browser** (Chrome 153 under Playwright, serving `.scratch/demo-publish/wwwroot`
from `tools/run-wasm-smoke.ps1`):

| Window | Document scrolls | Header | Where the answer is |
|---|---|---|---|
| 1366x768 | no (`scrollHeight` 768 = `innerHeight`) | 48 px | count at y=92, third match row bottom at y=425 |
| 1440x900 | no | 48 px | same, and the fourth row is inside too |
| 1366x500 | yes, and neither pane does | 48 px | below the gate, as intended |
| 390x844 | yes | 88 px (the tagline wraps) | count at y=513, table and its hint inside the first screen |

The done-when box asks for three match rows, so the two wide runs used a four-match case. Both are
one navigation and one `evaluate`:

    http://localhost:8181/#p=%28%3F%3Acolour%29%7Be%3C%3D2%7D&f=&s=the+color+of+the+collar+and+the+colour+coller&m=&r=&l=

    const inside = (sel) => { const r = document.querySelector(sel).getBoundingClientRect();
                              return r.top >= 0 && r.bottom <= innerHeight; };
    const rows = [...document.querySelectorAll('tbody.match-rows tr')];
    ({ pageScrolls: document.documentElement.scrollHeight > innerHeight,
       pattern: inside('#pattern'), subject: inside('#subject'), count: inside('.answer-count'),
       threeRows: rows.slice(0, 3).every((el) => el.getBoundingClientRect().bottom <= innerHeight) })

Both answered `{ pageScrolls: false, pattern: true, subject: true, count: true, threeRows: true }`,
with `4 matches` on the page and the engine reporting 280-300 ms.

**Three faults the browser found that the tests could not.** All three are now pinned by a test that
fails without the fix:

1. **The mode radios had invisible labels.** `body` painted the ink shell but set the white side's
   `text-slate-900`, so every word on the ink that did not name its own colour was near-black on
   near-black. Inverted: `body` is `text-shell-text`, `.results-pane` names `text-slate-900`.
   Pinned by `contrast.test.ts > body inherits a measured pair ...`, which requires the `bg-`/`text-`
   pair of each inheriting region to be in the measured table.
2. **Three stacked scroll regions at 1366x500.** The panes kept `lg:overflow-y-auto`, which asks
   about width alone, so below the height gate the document scrolled and both panes scrolled inside
   it. The two `overflow-y: auto` declarations moved into the gated block. Pinned by
   `layout.test.ts > the panes scroll only inside the gate that fixes the shell`.
3. **The one-column disclosures did not look like controls.** Written with the `.tab` class they
   rendered as two lines of prose. Now `.disclosure`: a full-width row, a chevron that rotates, and
   `min-h-11` (44 px, over 2.5.8's 24x24). Pinned by
   `layout.test.ts > a disclosure is a control and not a line of text`.

**Colour, measured rather than asserted.** `tests/colour.ts` converts OKLCH to sRGB (CSS Color 4
sections 9 and 10) and computes the WCAG 2.2 ratio; `tests/contrast.test.ts` holds 22 pairs with the
floor each use asks for, and fails if a token moves. Four tokens had to change to clear their floor,
and **three tokens already in the tree were outside the sRGB gamut** - `accent-soft` (chroma 0.03),
`hit-a` (0.11) and `hit-edge` (0.13). A browser gamut-maps rather than clips, so S72's recorded hit
ratios were measuring a colour nobody sees. Reduced to 0.018 / 0.095 / 0.11 and the reason is in a
comment beside them. The converter itself is checked against what Chrome actually paints: every one
of the 17 tokens agrees to within one 8-bit step, read back off a 1x1 canvas, and the snippet that
produced those bytes is in the comment above the table so it can be re-run.

The changed values, and what they now measure: `accent-bright` 0.72/0.12 (7.12:1 on the shell,
6.05:1 on raised - it was outside the gamut at chroma 0.15), `shell-edge` L 0.56 (3.81:1 and 3.24:1,
1.4.11's 3:1 - it was 2.28:1), `edit-sub` 0.52/0.1 on a 0.94/0.035 tint (4.7:1 - it was 4.23:1).

**Word count.** 946 over the six original sources, against 921 at the end of chunk 1: the shell put
25 words back - two disclosure labels, two tab labels and the line the Help tab shows before a
sample is loaded. The printed total is 1,103. The doc comment of `tests/word-count.test.ts` carries
the history and the command.

**Not in this chunk, and why.** The spec's per-edit underlay inside a highlight is not buildable
yet: `types.ts`'s `Counts` carries only totals, and `Match.FuzzyChanges` would have to be threaded
through `DemoEngine.cs`, `types.ts` and `highlight.ts` first. The edit hues are delivered on the
count chips instead. The mode radios stayed in the input pane rather than moving to the header: the
layout table's "mode control" most plausibly meant the colour-scheme toggle that decision 3 removed,
and the input pane has its own scroll, so moving them buys deliverable (i) nothing.

### The verdict page, back to green

`checks.html`, run against the fresh publish on 8181, went eight of nine: **"a subject over the cap
is refused, not truncated"** was red. Not a chunk-2 fault and not a page fault - chunk 1 rewrote the
refusal to "the demo's limit is 100,000" and the check matched the literal `"limit of"`. Reproduced
in the browser, `window.__demo` with a 100,001-character subject:

```
{"answerNull":true,
 "failure":"The subject is 100,001 characters and the demo's limit is 100,000, so nothing was sent
            to the engine. Shortening it here would move every offset in the answer."}
```

The check now matches `demo.maxSubjectLength.toLocaleString()`. That is the number the visitor
needs, it cannot be reworded away, and both of the check's clauses are proven by the probe above.

### Review (chunk 2)

One blind pass over the whole chunk-2 diff. **Seven findings, none yet fixed** - the sitting's
allowance ran out at the report. The reviewer left the tree byte-identical and its own suite run
green (181 passing). Each finding carries a reproduction, so the next sitting's job is to confirm
and fix, not to re-derive:

1. **`App.vue:489` - the unselected tab's `aria-controls` dangles.** Only the selected panel is
   rendered, so `#tab-help` points at `panel-help`, which is not in the DOM. `layout.test.ts:234`
   checks `tabs[0]` only, so the suite cannot see it.
2. **`App.vue:467` - the "Examples and help" disclosure hard-codes `aria-controls="panel-examples"`**
   while the rendered panel id follows the selected tab; and when either disclosure is collapsed its
   target does not exist at all (`App.vue:358`, `:467`). The APG disclosure keeps the element in the
   DOM and hides it.
3. **`styles.css:145` - the shell is sized in `vh` after all.** `.shell { @apply flex min-h-screen
   flex-col }` compiles to `min-height: 100vh`, and the gate adds `height: 100dvh` to the same
   selector; where `100vh > 100dvh`, `min-height` wins - exactly the case the comment at `:445` says
   `dvh` avoids. Confirmed here against the real Vite build, not a re-implementation: `npm run build`
   then read `.shell` out of `wwwroot/assets/index-*.css`. `layout.test.ts:80` asserts "never vh"
   against the *source* string, which is why it passes. Fix is `min-h-dvh`, and the test should read
   the built CSS.
4. **`App.vue:501-506` - the Help panel with no loaded sample is not keyboard-reachable**: its only
   content is a `<p>` and the `tabpanel` carries no `tabindex="0"`.
5. **`App.vue:473` - focus drops to `<body>` when the media gate narrows** while a tab has focus, the
   `v-if` removing the focused button. Lowest value of the seven; judge it against the cost.
6. **`layout.test.ts:43, 67, 93, 108` read `styles.css` as a string** and break on CSS edits that
   change nothing: swapping two rules inside the media block fails two tests, and merging the two
   identical `overflow-y` rules (what a minifier does) fails two more. Same fix as 3 - assert over
   the built stylesheet.
7. **`contrast.test.ts:60`** describes `accent-soft` as "the tab panel"; `.tab-panel` is
   `bg-shell-raised`. `accent-soft` is used at `styles.css:367` only, the hovered row.

Checked and **not** defects: the `useWide` listener is removed exactly once on unmount; the
`App.vue:73-75` claim that jsdom has no `matchMedia` holds; the word-count figures are right.

**The independent verifier has not run on chunk 2.** It is owed before the slice closes, over this
chunk's numbers as well as chunk 3's.

### State at the checkpoint (chunk 2)

`demo/web`: **181 tests in 11 files** green, `npm run typecheck` clean, `npm run build` clean.
`tools/check-ratchet.ps1`: **GREEN, 6,399 passing** against a 6,291 baseline - nothing in the .NET
suite changed this chunk, so the baseline was not updated. `tools/run-wasm-smoke.ps1` green, 29
files published. The browser work was done against that publish on
`http://localhost:8181`, not against the dev server: the source `wwwroot` has no `_framework`, so
only a publish runs the engine.

## Chunk 2's review findings, fixed (2026-09-19, sitting 3)

The seven findings above, confirmed here before anything was touched, and five of them fixed. The
sitting was cut short by the seven-day allowance (the orchestrator's 96 % gate), so this is a
checkpoint commit and two findings are still open.

**Finding 3, and the reason the test could not see it.** Confirmed against the real Vite build, not
a reimplementation: `npm run build` and then the built stylesheet read back.

    .shell{flex-direction:column;min-height:100vh;display:flex}
    .shell{height:100dvh}

`min-h-screen` compiles to `min-height: 100vh`, and where the browser chrome makes `100vh` the
larger number it beats the `height: 100dvh` the gate sets on the same selector. Now `min-h-dvh`, and
the same read-back gives `.shell{flex-direction:column;min-height:100dvh;display:flex}` with **zero
occurrences of `100vh` in the whole built stylesheet**.

**A third thing the built stylesheet showed.** `@media (width>=64rem){.lg\:overflow-y-auto{...}}` was
in the shipped CSS, generated from a *sentence in `layout.test.ts`* explaining why the page no longer
uses that class - Tailwind scans the test files too. `@source not '../tests'` in `styles.css` stops
it: the bundle went 25.32 kB to 24.14 kB and the dead utility is gone (`grep -c` gives 0).

**Findings 1, 2 and 4, all one change.** Both tab panels are now in the page with the unselected one
`hidden`, because APG's Tabs pattern requires that "Each element with role `tab` has the property
aria-controls referring to its associated `tabpanel` element" (w3.org/WAI/ARIA/apg/patterns/tabs,
read 2026-09-19) and an id that resolves to nothing refers to nothing. `hidden` keeps the closed
panel out of the accessibility tree, which is what the old "not rendered" comment wanted. The
"Examples and help" disclosure now names `#examples-and-help`, the box it actually opens, and both
disclosures hide their region rather than dropping it, so what `aria-controls` names is always
there. The Help panel takes `tabindex="0"` only while `helpSections` is empty - APG's note 4, "When
the tabpanel does not contain any focusable elements ... the tabpanel should set `tabindex=0`" -
because with sections loaded there are summaries and code boxes to tab to.

Three tests pin this and each was seen to fail without it: the tab-set test now walks **both** tabs
(the version that checked `tabs[0]` alone is why the suite could not see finding 1), the narrow-window
test asserts each region is `hidden` and that every `aria-controls` resolves, and a new test takes
the Help panel from empty to loaded and back over `tabindex`.

**Finding 7** was right: `accent-soft` is `.row-select`'s hover box, not the tab panel, which is
`shell-raised`. The label says so now.

**Still open, with the work already scoped.**

- **Finding 6, the four `layout.test.ts` assertions over the stylesheet SOURCE.** The route is
  proven, not written: an in-process `vite build` of `src/styles.css` with the Tailwind plugin and
  `configFile: false`, `build.write: false`, run from a test, produced **byte-identical output to
  `npm run build`** - same content hash `DqmTS2AV`, same 25,324 characters - in **127 ms**. So a
  `tests/built-css.ts` helper can hand the four tests the CSS the browser receives, and they stop
  breaking on harmless source edits. Two of the four assertions would need the utility-class exclusion
  above to be in place first, which it now is.
- **Finding 5, focus dropping to `<body>` when the media gate narrows.** Not attempted. The cheap
  shape, if the next sitting wants it: a `watch(wide, ...)` with the default pre-flush, which still
  sees the focused element, opening whichever disclosure contains it so the region never hides.
  Two template refs, no focus juggling.

**State.** `demo/web`: **182 tests in 11 files** green (181 before), `npm run typecheck` clean,
`npm run build` clean. `tools/check-ratchet.ps1`: **GREEN, 6,399 passing** against the 6,291
baseline - no .NET file was touched, so the baseline was not updated.

## Chunk 2 closed: findings 6 and 5 (2026-09-19, sitting 4)

The two the last sitting left open. Both fixed, both pinned by a test that was seen to fail first.

**Finding 6, the assertions that read the CSS source.** Confirmed before anything was touched, with
the edit the finding names - merging

    .input-pane { overflow-y: auto }
    .results-pane { overflow-y: auto }

inside the gated block into one selector list, which is identical CSS and what a minifier does
anyway. Two tests failed on it:

    × exactly two regions own a vertical scroll
      AssertionError: expected [ '.results-pane' ] to deeply equal [ '.input-pane', '.results-pane' ]
    × the panes scroll only inside the gate that fixes the shell

`tests/built-css.ts` now compiles the stylesheet the way the production build does - Vite's
JavaScript API, the same Tailwind plugin, `write: false` so nothing reaches `wwwroot`, and
`configFile: false` so the shipping config's `outDir` and dev middleware stay out of a test's way -
and caches one compile per test file. **Byte-identical to `npm run build`**, and this is the probe
that says so rather than a description of one: `tools/probes/demo-built-css-matches-production.test.ts`
carries the commands.

    LENGTH 24144 MD5 ebc4e84e067ef6dbfc926c70e5a4984a       (the in-process compile)
    24144 FuzzyRegex.Demo.Wasm/wwwroot/assets/index-cz1TZNmf.css
    ebc4e84e067ef6dbfc926c70e5a4984a *...index-cz1TZNmf.css  (what npm run build wrote)

Four assertions moved onto it, each now asking the question in the form the browser answers: the
scroll owners are read off compiled rules through one `scrollOwners` helper, so a selector list and
two separate rules give the same answer; the gate's block is extracted by brace-matching; and
`.shell` must compile to `min-height: 100dvh` with **no `100vh` anywhere in the shipped
stylesheet**, which is the assertion chunk 2's finding 3 needed and could not have. The
script-against-stylesheet gate test has to translate one into the other's spelling, because the
build rewrites `min-width: 64rem` into the range syntax `width>=64rem`; the translation is three
replacements and it is commented where it sits. After the move, the merged-rule edit above passes
all 14 tests, and reverting it passes all 14 too.

**Finding 5, focus dropping to `<body>`.** A `watch(wide, ...)` on the default pre-flush timing:
it runs before the DOM update, so `document.activeElement` is still the focused element, and opening
whichever region contains it means the element is never hidden and nothing has to be re-focused.
Two template refs, no focus juggling, as the last sitting scoped it. WCAG 3.2.2 On Input is the
rule a layout change that moves the focus breaks.

Two tests, one per region, because each region has its own ref and one test would have left the
other half free to be a typo. Both were seen to fail first - the samples one before the watcher
existed, and the inputs one with `ref="advancedRegion"` renamed to `ref="advancedRegionTypo"`:

    × narrowing the window opens the secondary inputs when the focus is in them
      AssertionError: expected true to be false

Each asserts three things: the region is not `hidden`, the focused control is still rendered, and
the *other* region stays shut, so the fix is by containment and not a blanket open. Why the middle
one is phrased that way rather than as `document.activeElement` is finding 1 below.

### Review (sitting 4)

One blind pass over the sitting's diff, briefed with the reproduction commands and nothing else.
**Four findings raised, four reproduced here before anything was touched, four fixed.** A higher
survival rate than this project's usual one in five, and for the same reason chunk 1's was: a first
pass over a NEW test mechanism finds things about its reach, which is what it is for. Three of the
four are about what the new assertions cannot see.

1. **The focus assertions could not fail.** jsdom does not implement the focus fixup rule, so an
   element stays `document.activeElement` after an ancestor gets `hidden`. Reproduced with the
   watcher disabled (`if (1 > 0) return;` after the `isWide` guard):

       PROBE hidden= true activeElementIsTab= true insideHidden= true

   The tests did fail without the fix, but through their `.hidden` assertion; the line that named
   the thing the test is about proved nothing. Both now assert `closest('[hidden]')` is null - the
   focused control is still RENDERED, which is the condition that decides what the browser does,
   and the one thing that differs between the two versions. The doc comment says so, with the
   measurement, so nobody re-adds the assertion that reads better and tests less.
2. **An SFC `<style>` block ships and the built-CSS tests cannot see it.** The in-process build's
   entry is the stylesheet; an SFC's styles reach the bundle through the JavaScript graph from
   `index.html`. Reproduced: `.probe-third-scroller { overflow-y: auto }` appended to `App.vue`
   came out in `assets/index-D2ryK5O7.css` (`grep -c` gives 1) with **"exactly two regions own a
   vertical scroll" still green**. Closed at the other end by a new test - no component declares
   styles - because compiling the whole module graph would cost a JavaScript build per test file to
   police a convention the project already keeps. Seen to fail on the planted block and pass once it
   was removed.
3. **A region the gate opened stayed open.** The watcher returned early on widening, so maximising
   and restoring a window left the samples panel expanded above the answer with nothing focused in
   it - the exact screenful this slice removes. It now remembers which region IT opened and closes
   that one when the window widens; a region the visitor opened is left alone. Two tests, and the
   second (the visitor's own click surviving a resize) passed before the fix, which is what says the
   fix did not take the sticky behaviour away with it.
4. **The helper's recorded verification named the previous sitting's numbers** (`DqmTS2AV`, 25,324
   characters, from before `@source not '../tests'` shrank the bundle). Corrected to the measured
   24,144 and `ebc4e84e...`, pointing at the probe rather than repeating it.

Checked by the reviewer and sound, each with its own reproduction: the built-CSS assertions do
catch a third scroll region, a scroll utility in the markup **including the `:class` array form the
source-reading version could not see**, a shell compiled to `100vh`, and a gate that drifts from the
script's copy; the watcher does not leak or double-fire and survives `unmount`; the cached promise
is assigned before its first `await`, so concurrent callers share one build and a build error
reaches all four tests without an unhandled rejection; and `gateBlock` fails rather than passes if
its prelude is ever ambiguous.

### Second pass, over the four fixes (the first reviewer never saw them)

**One finding, reproduced and fixed.** `openedByGate` was written only by the watcher, so ownership
stuck to the gate for as long as the window stayed narrow: gate opens the samples region because the
focus was in it, visitor collapses it, visitor expands it again *because they want it* - and the
next widening still closed it. The visitor loses a panel they asked for, and it contradicted the
comment sitting three lines above the code.

The press is now `toggleDisclosure(which)`, which hands the region back to the visitor before it
toggles. Pinned by a test that walks that exact sequence and was seen to fail first
(`expected true to be false`). Three tests now surround the state: the gate closes what the gate
opened, the visitor's own press survives a resize, and a press after the gate's open takes
ownership. The reviewer checked the first two discriminate in both directions - breaking the widen
branch fails one and leaves the other green, and closing both regions unconditionally does the
reverse.

Checked by that reviewer and sound: the mirror hole does not exist (ownership is cleared only in the
branch that closes), repeated `matches` values with the same value do not fire the watcher at all,
the gate opening one region while the visitor holds the other survives a round trip, and the
`closest('[hidden]')` assertions do fail on their own when the narrow branch is disabled -
`expected <div id="examples-and-help" ...> to be null`.

That reviewer saw five `dev-server.test.ts` tests fail in its own shell with empty response bodies.
Not reproduced here: this sitting's runs are **188 passing in 11 files**, `dev-server.test.ts`
included, before and after the fix. The dev server does not come up inside a subagent's sandbox.

**A third pass covers `toggleDisclosure` itself**, for the same reason: it is code no reviewer had
seen, and the second pass had just found a real hole in the same state machine.

### Third pass, over `toggleDisclosure` and the watcher

It found nothing wrong with the logic - 18 interleavings of gate-open, press, widen and narrow, all
correct - and five things wrong with the tests around it. Each one was reproduced the same way: apply
the mutant, `npx vitest run tests/layout.test.ts`, revert.

| Mutant | Result then | Now |
|---|---|---|
| `src/App.vue:128` drop the `=== which` guard (`!== null`) | 20 passed | 2 failed |
| `src/App.vue:128` narrow it to `&& which === 'samples'` | 20 passed | 1 failed |
| `src/App.vue:412` revert one call site to `@click="advanced = !advanced"` | 20 passed | 1 failed |
| `src/App.vue:135` delete `if (openedByGate.value === 'advanced') advanced.value = false;` | 20 passed | 2 failed |
| `src/App.vue:149` delete `openedByGate.value = 'samples';` | new test still green | killed by its pair |

One cause behind all five: every rule about ownership was written for the samples panel, and the
`advanced` region has its own template ref, its own `@click` argument and its own line in the
watcher. So the four rules are now `test.each` over a two-row table, and the fifth is new - *pressing
one disclosure leaves the other with the gate*, which is the only test that can tell a per-region
ownership from a single "somebody has touched something" flag. 188 tests became 193, and the table is
`tests/layout.test.ts > disclosures` with the mutation evidence in its comment. Reverting each mutant
restored 193 passed; `npm run build` is clean and the stylesheet is unmoved (`index-cz1TZNmf.css`,
24.14 kB).

One mutant is equivalent, not surviving: `src/App.vue:137` (`openedByGate.value = null` in the wide
branch) is defensive - the two lines above it have already cleared whichever region it named.

**Out of scope, reproduced, and left for chunk 5's keyboard pass:** the mirror of the fault this
sitting fixed. The disclosure buttons are `v-if="!wide"`, so *widening* removes the button that has
the focus and the reviewer measured `after widening, activeElement = BODY`. It is pre-existing, it is
the same WCAG 3.2.2 case, and the fix needs a decision about where focus should land (the region it
controlled, which is visible when wide), so it is a chunk of its own rather than an addition here.
