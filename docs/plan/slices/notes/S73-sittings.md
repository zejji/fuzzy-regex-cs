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

## Chunk 3 - the C# snippet (2026-09-19, sitting 5)

`demo/web/src/lib/snippet.ts` turns `Inputs` into the C# the demo itself runs, and a revealed panel
at the foot of the results region shows it, coloured, with a Copy button. `toCSharp` is a pure
function of the inputs and nothing else - no DOM, no engine, no clipboard - which is what lets
`snippet.test.ts` assert whole snippets and `page.test.ts` assert the panel without owning the
language rules. `tokenize` is eighty lines and five classes, not a highlighter: highlight.js, Prism
and Shiki each weigh more than this whole page, and every character comes back out, which the round
trip test pins.

`demo/web/src/lib/clipboard.ts` is separate because it is asynchronous and browser-shaped.
`navigator.clipboard` is undefined outside a secure context and rejects with `NotAllowedError` when
the browser refuses, and neither is something the visitor did wrong, so `copyText` never throws and
returns `'copied'` or `'select'`; the fallback selects the `<pre>` so the keyboard's own copy works.

**Compiled for real, not reasoned about.** `tools/probes/demo-snippet-compiles.mjs` writes each
emitted snippet into a throwaway `dotnet new console` project outside the repository (the repo's own
`Directory.Build.props` would compile it under analyzer rules a visitor does not have) and runs it.
Eight cases, all green on 2026-09-19:

| Case | What `dotnet run` printed |
|---|---|
| the default case | `3+6 s=0 i=1 d=1` and `17+6 s=2 i=0 d=0` - the page's own two matches |
| two flags, named in full | `11+5 s=0 i=0 d=1`, `16+3 s=3 i=0 d=3`, `19+0 s=0 i=0 d=6` |
| partial | `0+7 partial=True` |
| replace | `09/2026 and 12/1999` |
| named lists | `0+4 s=0 i=0 d=1`, `5+7 s=0 i=1 d=0`, `13+6 s=0 i=0 d=0` |
| quotes and a trailing backslash | `pattern OK`, `subject OK` |
| newlines, a blank line, a whitespace-only line, three quotes | `10+1 s=0 i=0 d=0`, then `pattern OK`, `subject OK` |
| a carriage return, U+2028, U+2029 and U+0085 (added by the review's finding) | `pattern OK`, `subject OK` |

A second probe, `tools/probes/demo-trim-matches-dotnet.mjs`, settles the trim the same way: it
compiles and runs the C# that enumerates `char.IsWhiteSpace` over the BMP (25 code points), drives
every reachable code unit through `toCSharp` (65,531, zero disagreements), and times the index walk
against the regex it replaced.

The `OK` lines are a round trip: the probe compares the literal the generator built with one built by
`JSON.stringify`, so the check does not use the code it is checking. `DemoSnippetTests.cs` holds the
two halves that can rot silently - the printed timeout is `DemoEngine.MatchTimeout`, `FLAG_NAMES` is
`Enum.GetNames<FuzzyRegexOptions>()`, and the default case's spans are the ones the compiled snippet
printed above.

**In a real browser**, not only in jsdom: published with `tools/run-wasm-smoke.ps1`, served over
HTTP, and driven in Chrome 153 at 1366x768. Enter on the toggle opens the panel and the focus lands
on the `<pre>`; the page does not grow a scrollbar (`scrollHeight === innerHeight`); the four token
colours compute (keyword `oklch(0.491 0.27 292.581)`, string `oklch(0.508 0.118 165.612)`, comment
`oklch(0.446 0.043 257.281)`, number `oklch(0.553 0.195 38.402)`); Tab reaches Copy, Enter writes to
the real clipboard (`readText()` returned the snippet) and the live region reads "2 matches / in 263
ms / copied to the clipboard"; Escape closes it and the focus returns to the toggle.

**Colour.** The panel border wanted slate-300 and could not have it: measured 1.42:1 on slate-50 and
1.48:1 on white against WCAG 1.4.11's 3:1, and slate-400 is 2.51:1. slate-500 passes and is what
ships. Every new colour is a Tailwind palette value rather than a new `--color-` token, because
`contrast.test.ts` requires a measured pair and a browser-recorded byte triple for each token.

**Two mutants** over the code written alongside its tests: removing `select(fallback)` from the
clipboard's catch, and removing the focus return from `closeSnippet`. Four tests failed, both
reverted, 223 green again (229 with this sitting's new tests).

### Review (chunk 3)

**First pass, over the whole chunk: five findings raised, five reproduced, four fixed.**

| Finding | Reproduced as | Outcome |
|---|---|---|
| a carriage return is dropped from a raw literal | `literal("a\r\nb")` is `"""\na\nb\n"""`; the compiled snippet printed `DIFFERENT` | fixed - a third literal form |
| U+2028/U+2029 in a raw literal will not compile | `error CS8999: Line does not start with the same whitespace as the closing line of the raw string literal` | fixed by the same form |
| the mode is compared exactly, the engine trims and lower-cases it | `mode: 'Partial'` and `' replace '` both emitted the walk | fixed - `trimmed(...).toLowerCase()` |
| a trailing backslash in a verbatim string swallows the rest | one string token held the constructor and the whole walk | fixed - `\` is an escape only when the string is not verbatim |
| `Replace` carries no `count: MaxMatches` | `DemoEngine.cs:488` | not a defect - see the decision below |

The fifth is the page's display cap, and the answer is the claim rather than the code: 1,000 is what
this page shows, not what the library does, and a visitor's own `Replace` should rewrite the whole
subject. The comment above the branch says so and the whole-output test is what holds it.

The first three are one bug with one cause. C# ends a line on a carriage return, U+0085, U+2028 and
U+2029, so any of them inside a raw string literal makes a literal whose lines are not the lines the
generator laid out. `literal()` now has a third form - an ordinary escaped literal, one line, every
character spelt out - and `tools/probes/demo-snippet-compiles.mjs` has a case with all four
terminators in one value, which compiles and prints `subject OK`.

**Second pass, over the fixes, which no reviewer had seen: one finding, reproduced, fixed.**
JavaScript's `trim()` is not `String.Trim()`: .NET trims U+0085 and JavaScript does not, and
JavaScript trims U+FEFF where .NET does not, so `#m=partial%C2%85` was a partial answer on the page
with a walk in the panel. `char.IsWhiteSpace` is true for 25 BMP code points - printed by
`tools/probes/demo-trim-matches-dotnet.mjs`, which compiles and runs the C# that enumerates them -
and the two sets differ in exactly those two characters. `snippet.ts` now has a `trimmed()` of its
own at all four places `DemoEngine` trims (`DemoEngine.cs:623,675,730,734`). The same pass killed a
test of mine:
`not.toContain('count:')` cannot fail unless the whole-output test fails first, so it is gone and
its reasoning moved into that test's comment.

**Third pass, over `trimmed()` itself: one finding, reproduced, fixed.** Written as
`^[ws]+|[ws]+$` the trailing alternative restarts inside every interior run of whitespace, and the
panel recomputes the snippet on every keystroke into a flag box nothing caps. The probe times both
forms over the same inputs - 12.5k, 25k, 50k and 100k characters of U+00A0 in the flag box. Three
runs on this machine on 2026-09-19, one of them the verifier's: the regex took 101-422 ms at 12.5k
and 5.4-10.3 seconds at 100k, roughly four times per doubling, while the whole snippet with the
index walk took 0-2 ms at every size. The absolute figures move with what else the machine is
doing; the shape does not. `snippet.test.ts` holds a 200 ms budget: put the regex body back into
`trimmed()` for one run and the committed test fails, measured at 3,723 ms and 3,882 ms here and at
11,938 ms and 18,650 ms in the verifier's run. Equivalence re-checked afterwards by
driving every BMP code unit through `toCSharp` as a list name - 65,531 of them (a colon, a comma, a
semicolon and the two line separators are read by the block parser before any trim), zero
disagreements with the .NET set the probe measured.

**Fourth pass, over `trimmed()` and its budget test, which the third pass's fix created: no
findings.** That is the pass the chunk ends on - every line of code in it has now been read by a
reviewer who did not write it.

### The independent verifier (chunk 3)

A fresh Opus, given the tree and these notes and told to re-run every number. Thirty-one claims
CONFIRMED, four COULD NOT RUN (the browser session, the two pre-fix compiler errors, the mid-sitting
mutation counts - each needs a machine state this tree no longer has), and four DIFFERENT, all four
of them the notes being wrong rather than the code:

- the eight-case table dropped case 7's match line, `10+1 s=0 i=0 d=0`. Added.
- `tokenize` is 79 lines, not sixty. Corrected here and in STATE.md.
- `DemoEngine`'s flag trim is line 623, not 622. Corrected.
- the timing figures were a single run and reproduce only in shape, not in value. The claim now
  gives the range over three runs, the verifier's among them.

The fourth of those is the useful one, and it has a cause worth keeping: the verifier read the
100,000-character filler in the budget test as U+0020 and concluded the mutant was harmless, because
a raw U+00A0 in a source file renders as an ordinary space. Two readers in a row have now made that
mistake. Every non-ASCII whitespace character in `snippet.ts`, `snippet.test.ts` and the trim probe
is therefore written as `\u{a0}`, `\u{2028}` and so on, where the reader can see which character it
is; 229 tests still green and the probe still prints 0 disagreements afterwards.

## Chunk 4 - two-way linking and the header link (2026-09-19, sitting 6)

Deliverables (vi) and (vii). Starting tree clean at `c807f61`; the chunk adds 5 front-end tests, 234
at the end.

### What chunk 4 landed

**One pair of selectors for both halves.** `HALVES` in `App.vue` names, for the subject and for the
table, the container, the element the keyboard focuses and the element worth scrolling to. They
differ on the table side on purpose: the keyboard's control is the 24 px number button, the thing
worth revealing is the whole row. Every lookup is by `data-match`, never by position, because under
RightToLeft the highlights are in subject order and the rows are in the answer's order.

**A click in either half selects the match and brings the other half to it**, with
`scrollIntoView({ block: 'nearest', inline: 'nearest' })` and `behavior` from
`prefers-reduced-motion`. The row's own `@click` is the only handler: the press on the number
bubbles to it, so the scroll is asked for once.

**The header carries the repository**, `zejji/fuzzy-regex-cs` beside the GitHub mark, `ms-auto` and
`whitespace-nowrap` so it cannot make the header two rows tall; the footer link stays. The colour
and the underline come from the existing link rules - it is a link and it reads as one. No
`rel="noopener"`: it does nothing without `target="_blank"`, and the footer links carry neither.

**Chunk 2 had already de-underlined the row number**, so the rest of (vii)'s last sentence was a
regression pin rather than a fix: `layout.test.ts` now reads the compiled stylesheet and fails if
any rule that underlines something has a selector that is not a link.

### Measured in Chrome, not in jsdom

jsdom 30.1.0 has no `scrollIntoView` at all - `node -e` in `demo/web` prints `proto: undefined` and
`TypeError: e.scrollIntoView is not a function` - so the tests install a recorder on
`Element.prototype` and the page keeps its unguarded call. That makes the unit tests evidence about
arguments and nothing at all about scrolling, so the published page was driven directly: the
`tools/run-wasm-smoke.ps1` output served by `python -m http.server 8199`, Chrome at 1366x768.

**To re-run it**, because every pixel below is a function of the subject: default pattern
`(?:colour){e<=2}`, subject = 20 copies of `the color of the collar in colur and collor`. That gives
**80 matches** in a results pane of `scrollHeight` 4,285 inside `clientHeight` 655. The numbers in
the table are the independent verifier's re-run of 2026-09-19 against that subject; the sitting's own
first run used a subject it failed to record and read 100 to 150 px lower throughout.

| What was done | What the pane did |
|---|---|
| clicked row 75's number, pane at 3,489 | scrolled to 630, highlight 75 in view, `hit-current`, the only tab stop |
| clicked highlight 70, pane at 0 | scrolled to 2,972, row 70 in view, `aria-current="true"` on both halves |
| Enter on focused highlight 4 | scrolled 126 to 279, row 4 in view, focus unmoved |
| Enter on row 60's focused number | scrolled 2,772 to 518, highlight 60 in view |
| reduced motion on, clicked highlight 70 | first animation frame already at 2,972, one distinct value over 30 frames |
| reduced motion off, same click | 30 distinct values over 30 frames, 0, 2, 7, 18 ... still climbing - the control that makes the row above mean something |

**The arrows do not chase the counterpart, and that is the finding of this chunk.** The first
version had every arrow reveal the other half. In Chrome, ArrowRight scrolled the pane to the
counterpart row and the `focus()` on the next tick scrolled it straight back: both halves live in one
scroll container, so when they are a screen apart only one can be on screen, and the one that must
be is the one holding the focus (WCAG 2.4.3). The reveal was a cancelled animation. `rove()` now
selects and focuses only; Enter, Space and the pointer are how the keyboard asks for the other half,
and those keep the focus where it is, so their reveal survives. The unit test asserts no reveal from
an arrow and says why.

### For chunk 5: the column header does not stick

Found while checking a comment that claimed it did. `.table-scroll` is `overflow-x: auto`, and CSS
computes the other axis to `auto` with it, so `.table-scroll` - not `.results-pane` - is the
scrollport the sticky `th` sticks to, and it never scrolls vertically. Measured on the published
page with the subject above and the pane scrolled to 1,500: `overflowX` and `overflowY` both compute
to `auto`, `.table-scroll` has `scrollHeight` 3,297 equal to its `clientHeight`, so it can never
scroll and the sticky `th` has no scrollport to stick in - it sits at y = -697 with the pane top at
y = 64, that far off screen. The stylesheet's own comment at `styles.css:382` names this
exact hazard. Spec line 69 asks for a sticky header row, so chunk 5 owes it: try `overflow-y: clip`
beside `overflow-x: auto` and measure whether the `th` then sticks to the pane, and pin whichever
answer the browser gives.

### Mutants

Six, each planted, run and reverted:

| Mutant | Result |
|---|---|
| `block: 'nearest'` becomes `'center'` | 2 tests fail |
| the reveal looks up the half it was called from | 2 tests fail |
| `behavior` hard-coded to `'smooth'` | the reduced-motion test fails |
| the row's `:data-match="i"` removed | 2 tests fail, reveal list empty |
| `rove()` reveals the counterpart again | the arrow test fails |
| `.row-select` underlined with the `text-decoration` shorthand | the underline pin fails, after the review's fix |

### Review (chunk 4)

**First pass, over the whole diff: three findings raised, three reproduced, three acted on.**

| Finding | Reproduced as | Outcome |
|---|---|---|
| the underline pin reads only the `text-decoration-line` longhand | `.row-select { text-decoration: underline }` compiled into the stylesheet, suite 234/234 green | fixed - both spellings, solid only |
| the "once per activation" block dispatches on the `<tr>`, so the duplicate handler it names never runs | the mutant is killed 22 lines earlier; relaxing that line lets the block pass with two handlers | fixed by deleting the block and moving its reasoning into the assertion that does catch it |
| two new comments say a 24 px button can be left "under the sticky header", and no header sticks | the live measurement above | comments corrected, and the stylesheet bug written up for chunk 5 |

**Second pass, over the widened underline pin, which no reviewer had seen: seven findings, all
reproduced, two fixed and five declined on the merits.** Fixed: a rule inside `@media (hover: hover)`
was read with the media prelude as its selector, so every media-wrapped rule went unchecked - the
at-rule preludes are dropped first now; and `split(',')` cut inside `:where(.shell-header,
.input-pane, .shell-footer) a`, so the file's own scoped link rule would have failed the moment it
carried the underline itself - a depth-aware split fixes it, proved by adding `underline` to that
rule and watching the suite stay green. Declined, and written into the test as a `SHORTCUT:` with
its lift: a keyword hidden behind `var(--deco)`, a `dotted` inside a `var()` fallback, and a
selector that merely contains a link compound (`a:hover ~ .row-select`, or the same thing as a
Tailwind arbitrary variant) all get past a regular expression. They are what someone writes to
evade the test, not what someone writes by accident; the habit this pin guards is
`class="underline"`, which it catches. A real parser is the lift and `postcss` is already in the
tree. The reviewer also reported that `hover:underline` on a genuine link fails the test: it does,
because `.hover\:underline:hover` says nothing about what wears it, and the assertion message now
says to underline links in `styles.css` against `a`.

### Verifier (chunk 4)

A fresh Opus verifier re-ran every number above from the commit-ready tree: the suite and typecheck,
the ratchet and the demo build, the jsdom probe, all six mutants (each planted, run and reverted,
with `git diff --stat` identical afterwards), every Chrome measurement in a live browser, and the
depth-aware-split proof. Nineteen claims CONFIRMED. Three were not, and all three are corrected
above rather than kept:

- **the pane pixels.** The sitting did not record its subject, and the verifier's subject gives a
  pane 140 px taller and every scroll offset 100 to 150 px lower. The table now carries the
  verifier's numbers and the subject that produces them.
- **`overflow` computes to `["auto", "auto"]`.** It serialises to the single token `auto`. Right
  substance, wrong reading; rewritten as `overflowX`/`overflowY`, and the finding restated in the
  stronger form the verifier measured - `scrollHeight` equals `clientHeight`, so no scrollport.
- **"40 distinct values".** Every frame of the control is distinct, so the count is the sample
  length, not a property of the page; the row now says 30 of 30 and what that means.

Two more, kept with the claim rewritten: the pre-fix arrow measurement (pane to 2,873, focus back to
171) cannot be re-run from the committed code, so `App.vue`, `page.test.ts` and DECISIONS.md now
describe the cancelled animation without the two figures and point at the mutant that reproduces it;
and "229 tests at `c807f61`" needed a checkout the verifier was barred from, so the line now states
the +5 the diff actually shows. The verifier also added a negative control the sitting had not run:
with the scoped link rule underlined and `selectorList` replaced by a plain `split(',')`, the pin
fails - the depth-aware split is load-bearing, not decoration.

### Doing the rest of S73 in one sitting

This is sitting 6, so: what is left is chunk 5, and it is one sitting if it is run as one pass
rather than five. The order that does it - the sticky-header fix first, because it changes the
layout every later measurement is taken against; then one Playwright pass that walks 1920x1080,
1440x900, 1366x768, 1024x768 and 390x844 in a loop, taking the bounding-box assertions, the
screenshot and the keyboard walk at each width from the same script rather than by hand; then the
contrast measurements in that same browser session; then the blind review, the verifier and the
closing notes. Two things must be written as files before the browser opens, because doing them by
hand is what made chunks 2 and 3 take two sittings each: the width loop as a probe in
`tools/probes/`, and the checklist of what each width must prove.

## Chunk 5 (2026-09-19/20, sitting 7)

Run as sub-chunks against `.scratch/chunk5-checklist.md`, each one committed on its own. 5a (the
sticky column header, `6878d63`) and 5b (a widening window keeping the focus, `a637290`) have their
measurements, mutants and controls in their own commit messages; what follows is 5c onwards.

### 5c - the per-edit underlay

The demo said how many errors a fuzzy match spent and never where. It does now: `DemoEngine.cs`
puts an `edits` object on a match that spent any, `highlight.ts` breaks the highlight into runs, and
the page paints the character each error was spent on in the hue of its kind, with the kind's letter
under it.

**What a deletion's position means, proved rather than read.** `regex 2026.9.10` under Python
3.14.6, 2026-09-20:

| call | `fuzzy_changes` |
|---|---|
| `compile(r"(?:kitten){e<=3}").search("sitting")` | `([0, 4], [], [])` |
| `compile(r"(?:foobar){i<=1,d<=1,s<=1}").search("xfoobat")` | `([0], [1], [6])` |
| `compile(r"(?:abcdef){d<=2}").search("abef")` | `([], [], [2, 3])` |

A substitution and an insertion are subject positions. A deletion is not: upstream reports where the
missing character would sit in a string that had every deletion put back, so the i-th is shifted by
i (`_regex.c:20535-20537`, and our `Match.cs:448`). Two deletions in one place come back as `[2, 3]`
and are both at subject position 2. The library keeps upstream's answer; the demo un-shifts, because
the subject on screen is the string the page slices. All three cases are in
`tools/probes/demo-json-contract-expectations.py`, which now prints the un-shifted positions beside
the raw ones.

**Seven mutants, all killed** (`tools/probes/s73-edit-mutants.mjs`, each planted, run and reverted,
`git status` identical afterwards):

| Mutant | Killed by |
|---|---|
| a deletion at the very end of the match is dropped | 2 tests |
| two deletions in one place draw one mark | 1 |
| a position outside the match is drawn anyway | 1 |
| a position on the low half of a surrogate pair is sliced where it lands | 1 |
| every character is one code unit wide | 1 |
| upstream's deletion positions are passed through unshifted | 1 |
| an exact match carries an empty `edits` object | 3 |

The last one cannot be the obvious mutant - deleting the guard leaves `counts` unused, which is an
analyzer error, not a test failure - so it is a guard that never fires. That is written at the line.

**Two defects the tests could not have found, both from one screenshot at 4x**
(`tools/probes/s73-edit-underlay.mjs`, Chrome 153, 1366x768). The mark's box ends 1px below the
character, and both annotations were landing on it: an underline 3px below the baseline and a letter
6px below it were drawn with the highlight's own dark border through the middle of them. The
underline went inside the amber (offset 1px) and the letter below it (`-8px`). Separately the
deletion caret, an `inline-block`, sat on the baseline and so stood 3px proud of the highlight it is
inside - box `[564, 187, 6, 16]` against a mark at `[514, 190, 57, 18]`. With `align-text-bottom` it
is `[564, 191, 6, 16]`: the same box the other two marks have.

**The colours.** The three edit tokens were darkened (`sub` to `oklch(0.45 0.085 70)`, `ins` to
`oklch(0.41 0.105 150)`, `del` to `oklch(0.43 0.16 25)`) because the underlay puts them on the match
fill rather than on white, and at their chunk-4 values they were 4.3, 3.42, 3.76 and 4.01 against
`--color-hit-a` and `--color-hit-b`. Six new pairs in `contrast.test.ts` hold them at 4.5, and the
chroma of each was the thing that had to come down to stay inside sRGB. Chrome paints them
`[115, 76, 23]`, `[14, 89, 41]` and `[148, 21, 29]`, re-recorded in the `BROWSER` table from the
probe above.

250 tests in `demo/web`, 61 in `DemoEngineContractTests`.

### 5d - one scripted width pass

`tools/probes/s73-widths.mjs`, Chrome 153 through Playwright, against the served build on port 8213.
One run, five viewports, a fresh load each: 1920x1080, 1440x900, 1366x768, 1024x768 and 390x844.
Per width it records sideways scroll, page scroll, where the answer's heading ends, the disclosure
buttons, the pane boxes, anything clipped, the skip link, and a forty-stop tab walk. It ends with
every `--color-` token painted through a canvas.

| | 1920x1080 | 1440x900 | 1366x768 | 1024x768 | 390x844 |
|---|---|---|---|---|---|
| sideways scroll | none | none | none | none | none |
| page scroll | none | none | none | none | 1358 of 844 |
| answer heading ends | 262 | 262 | 262 | 262 | 721 |
| above the fold | yes | yes | yes | yes | yes |
| disclosures | 1 | 1 | 1 | 1 | 3 |
| clipped panes | 0 | 0 | 0 | 0 | 0 |
| focus stops off screen | 0 | 0 | 0 | 0 | 0 |
| focus stops with no ring | 0 | 0 | 0 | 0 | 0 |

Above the gate the shell is exactly the window - `scrollHeight` equals `clientHeight` at all four
widths - and the two disclosures collapse into the pane. Below it the page scrolls, the three
regions become disclosures, and the answer is still the first thing on screen. The data table is
wider than the phone (479px in a 375px column) and that is the deliberate scroll region with the
"Scroll the table sideways" hint under it, not a clip: the document itself does not scroll sideways.

**The one defect the pass found: twenty-six tabs to the answer.** At every width above the gate the
first control inside the answer was the twenty-sixth stop of the walk, eighteen of them the example
buttons, which sit in the left pane and are always on screen there. The answer is what the page is
for. A skip link now sits before the header, off screen until it takes the focus, and the walk is:
stop 1 the skip link, Enter, and the next Tab is `hit hit-current` - the selected match.

It is the demo's own navigation, not the browser's. The case lives in the URL fragment, so letting
`href="#results"` navigate would replace a URL holding everything typed with one holding an anchor.
Measured before the handler existed: the probe run ended at `?v=...#results`. With
`@click.prevent="skipToAnswer"` the same run ends at
`?v=...#p=%28%3F%3Akitten%29%7Be%3C%3D2%7D&f=&s=sitting+kitten+mitten+bitten+kitty&m=&r=&l=`. The
`href` stays for semantics and as the no-JS fallback. `page.test.ts` pins both halves: the focus
moves to `#results` and the hash is untouched.

**The colour table re-checked.** All eighteen `--color-` tokens the browser painted in this run equal
the `BROWSER` record in `contrast.test.ts` byte for byte, including the three 5c darkened.

**Three probe artefacts, each measured rather than reasoned about.** They are written into the probe
at the lines they affect, because every one of them looked like a defect first:

- Playwright compensates a 125% Windows display by zooming the page to 0.8, so the stylesheet's 2px
  focus outline computes to 1.6px and the first run called every stop ringless. The probe now
  derives the scale from a synthetic `outline: 2px` element and compares against `2 * scale`.
- The first run measured the phone with its examples region open and the answer 2,799px down,
  because the walk at 1024 had left the focus on an example button and chunk 2's narrowing fix opens
  the region the focus is in rather than losing it. Fixed with a fresh load per width.
- Chrome resumes sequential focus from the last focused element, so `blur()` does not restart a walk
  at the top and the first two stops were missing. `document.body` with `tabindex="-1"`, focused,
  moves the origin back to the document.

`layout.test.ts`'s shell-children pin was rewritten rather than relaxed: the first child is the skip
link, and the flow rows after it are still header, main, footer.

251 tests in `demo/web`.
