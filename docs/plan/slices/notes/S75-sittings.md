# S75 sittings

The slice file is the spec. This is what each sitting did, what it measured, and what the next one
needs to know.

## Sitting 1 - 2026-09-20

Branch `phase9-demo`, demo worktree. S75's only earlier commits are `e3f47e2` and `7c2b294`, both
the owner's review edits to the spec and to ROADMAP; the two commits at the head of the branch,
`dd1af7d` and `99b31fc`, belong to other work. No S75 code had been written.

### Done

Item 1, in part: the counted deletion gap and the marker row.

- `highlight.ts` gives every `EditRun` a `count`, and groups neighbouring errors of one kind into
  one run. Deletions in one place are ONE run carrying how many characters are missing, and six
  substituted characters are one run rather than six, so one letter marks each.
- The page draws that count from a `data-count` attribute, so the label is CSS `content` and copying
  the subject still copies the subject.
- `.subject-pane.has-markers` opens a taller line only on a result that has markers in it, and the
  letter moved from 8 px under the run to 13 px under it.
- `demo.ts` gained a `markers` computed, read off the runs the page will paint rather than off the
  counts. It also drives the legend, which sitting 2 builds.

### The owner's case, measured before anything was drawn

`(foobar){e}` on `xirefoabralfobarxie`, both engines, 2026-09-20:

| # | span | text | s | i | d |
|---|------|------|---|---|---|
| 1 | (0,6) | xirefo | 6 | 0 | 0 |
| 2 | (6,12) | abralf | 6 | 0 | 0 |
| 3 | (12,18) | obarxi | 6 | 0 | 0 |
| 4 | (18,19) | e | 1 | 0 | 5 |
| 5 | (19,19) | (empty) | 0 | 0 | 6 |

`regex 2026.9.10` and this port agree on every span and every count, so the spec's claim that the
engine is right and the drawing is wrong holds. Eleven deletions, all at subject index 19 once
`DemoEngine.Edits` un-shifts them, which is why they stacked.

Re-run: `python tools/probes/s75-stacked-deletions.py` and
`dotnet run tools/probes/s75-stacked-deletions.cs`.

### The marker geometry, measured in a real browser

`tools/probes/s75-marker-row.mjs`, against a served publish, at `deviceScaleFactor` 4. It measures
each viewport twice: "before" injects the geometry S73 shipped over the built stylesheet, so the
owner's finding is a number rather than a memory.

To re-run:

    pwsh -File tools/run-wasm-smoke.ps1 -OutDir .scratch/s75-publish
    node tools/probes/serve-demo-publish.mjs --root .scratch/s75-publish/wwwroot
    # then browser_run_code_unsafe, filename = tools/probes/s75-marker-row.mjs

The probe asks for `http://localhost:8213`, which is the serve script's default port; serving
anywhere else means editing the probe.

All distances in CSS pixels. The first three are the same number at 1366x768 and at 390x844, and
for both the `(?:colour){e<=3}` subject and the owner's `(foobar){e}` one:

| | line height | underline to letter | letter to border | letter to next line |
|---|---|---|---|---|
| before (S73) | 28 px | -2 | **-2.8** | 3.2, and 4 on one shape |
| after (S75) | 40 px | 3 | **2.2** | 10.2, and 11 on that shape |

A negative "letter to border" is the finding: the letter's top was 2.8 px ABOVE the bottom of the
highlight's border, so the border's dark line was drawn through it, and under a wavy underline that
is what read as a tick. It now sits 2.2 px clear of the border and about 10 px clear of the line
below.

"Letter to next line" is the one distance that moves, because it depends on where the line below
starts. The probe reports it per group of markers that share a line: 3.2 before and 10.2 after for
the group of eight, at both widths; 4 and 11 for the group of two, which at 390 wide is pushed onto
a line of its own; and `null` wherever the markers are on the last line and there is nothing below
them, which is every measurement of the deletion case. The gain is the same 7 px throughout, which
is the 12 px of extra line height less the 5 px the letter moved down.

### Two faults the measuring found that reading would not have

Both were in code that passed every test at the time.

1. **The counted label wrapped.** An absolutely positioned box with no width shrinks to fit its
   containing block, and the containing block here is the 6 px gap. "5 d" broke after the digit,
   became 20 px tall instead of 10, and its first line was drawn back through the border - the very
   collision the slice is fixing. Fixed with `white-space: nowrap` on `.edit::after`.
2. **Two counted labels overlapped by 6 px.** The owner's case ends with a match missing five
   characters immediately followed by an empty match missing six, so the two gaps are 8.4 px apart
   and each label is 14.3 px wide. A counted gap is now 20 px and closed on both sides; the labels
   sit 8.1 px apart. Measured, not estimated.

### Review

One blind pass over the whole working tree before the checkpoint commit. Four findings raised, four
reproduced, four fixed, one labelled UNREPRODUCED by the reviewer and left alone.

1. **Neighbouring errors of one kind were a run each**, against the spec's "one letter per run, not
   per character" (spec line 40), which says they are "already grouped" - they were not. The owner's
   first match drew six `s` letters under one six-character word. Reproduced as a failing test
   (`expected 'sub:x|sub:i|sub:r|sub:e|sub:f|sub:o' to be 'sub*6:xirefo'`), then fixed in
   `editRuns`. A gap breaks a run, because the two halves have a hole between them.
2. **A vacuous assertion.** `layout.test.ts` asserted the built sheet has no `.subject-pane{...}`
   rule with the taller line height. The build groups that selector with `.replaced-pane`, so the
   literal `.subject-pane{` is never in the output and the assertion could not fail. It now reads
   the `.subject-pane,.replaced-pane` rule and holds it at 28 px. Nothing asserted the
   `has-markers` binding either; `page.test.ts` now does, both ways.
3. **A claim the code does not support.** The `.edit-del[data-count]` comment said 20 px leaves
   "room for the two-digit count an unbounded budget can reach". `(?:a{1000}){d<=1000}` against an
   empty subject is 1,000 deletions in one place (`DemoEngine.cs:624`), so the count is not bounded
   at two digits. The comment now says what 20 px is for - the gap is in the text flow, so its
   width is paid by the subject - and that a longer count overflows its gap.
4. **The commit claim at the top of this file was wrong**, corrected above.

The reviewer could not run the browser probe. Its arithmetic was read instead and agrees with the
table: `bottom = run.bottom - after.bottom` with `top = bottom - height` gives 13-10=3 and 8-10=-2,
matching the two "underline to letter" numbers, and the 12 px line-box change is exactly 40-28.

A second blind pass covered the grouping change, which the first reviewer never saw. It built 32
cases through `segments` - runs at either edge of a match, alternating kinds, gaps inside a run,
surrogate pairs whole and straddling both edges, duplicate and out-of-range positions, a zero-length
match - and found nothing. Four invariants held on every case: `count` equals the characters in the
run's text for a substitution or an insertion, a deletion run's text is empty, the runs still
concatenate to the segment's text, and no two neighbouring runs share a kind.

### The verifier

A fresh session re-ran every number above from the committed files: both engine probes, a publish
built with `run-wasm-smoke.ps1`, the served page and the browser probe, the compiled stylesheet, the
`DemoEngine.cs:624` citation and the ratchet. Everything CONFIRMED except two, both fixed here: the
"identical at both viewports" claim, which the table now states properly, and a web-test count of
305 in STATE.md, which is 309. The label separations were confirmed to the second decimal (14.34 and
14.36 px wide, 8.05 px apart at a 20 px gap, where this file rounds to 14.3 and 8.1).

### Left for sitting 2

Item 1: the legend under the subject, the note on hover/focus/tap naming the exact position, the
unbounded-budget line under the pattern, and the alignment view. Items 2 to 5 untouched.

`markers` in `demo.ts` is already the condition the legend needs. The note mechanism wants
generalising out of `flags.ts`, because item 2's six heading notes need the same thing and the spec
asks for one help mechanism rather than three.

No screenshots are committed yet: the reference shots are meant to show the legend and the alignment
view, and neither exists.

## Sitting 2 - 2026-09-20

Checkpoint, not the end of the slice: the five-hour allowance ran to 92% with items 3 to 5 still
open. Everything below is green - 363 web tests, `vue-tsc` clean, the copy linter included.

### Done

- **Item 2 in full.** `HeadingHelp.vue` carries the `(?)` row for all six input headings, and
  `lib/help-notes.ts` holds the six notes, their link text and their button names, all linted.
- **Item 1 finished.** The unbounded-budget line, the legend, the note on a marked run, and the
  alignment view.

### The unbounded budget, measured rather than parsed from memory

`lib/budget.ts` reads the pattern text, because the engine exposes no compiled constraint and the
answer is wanted while the pattern is being typed. Which budgets actually run away came from
`tools/probes/s75-fuzzy-budget.py` (22 cases, regex 2026.9.10) and `s75-fuzzy-budget.cs` (the same
22 against this port, row for row identical), with `s75-fuzzy-defaults.py` settling the one that
reading the grammar gets wrong: `{s<=1,e}` is BOUNDED. Naming any kind puts every kind nobody named
at zero, so the unbounded `e` beside `s<=1` allows one error in total and the page says nothing.

### No pattern row in the alignment, and why

The spec asks for the pattern's characters above the subject's. The engine cannot supply them.
`fuzzy_changes` is three lists of subject positions and carries no pattern-side information, and a
pattern is not a sequence of characters to begin with - `(?:colou?r|couleur){e<=2}` against "calor"
answers `fuzzy_changes=([1], [], [4])` and never says which branch matched
(`tools/probes/s75-alignment-inputs.py`, regex 2026.9.10, 2026-09-20). So the view is the subject's
side: a cell per character, each labelled with its position and what happened to it, plus a cell per
place where characters are missing. The owner should say whether a pattern row is wanted enough to
pay for engine work.

### Three faults the browser found that the tests did not

1. The legend's sample character was `a`, so the key read "a substitution" - the sample became the
   article. It is `ab` now.
2. `.alignment-index` at `mt-2` drew the position through the kind's letter, which `.edit::after`
   positions 13 px below the character and out of the flow. `mt-5` clears it (1366x768).
3. A cell holding a space was a blank square labelled "at index 5", which names nothing. Spaces,
   tabs and newlines now draw a stand-in glyph and are named in words.

### Left for sitting 3

- **Item 3.** The example is measured and ready to write:
  `(?:colour){e<=2:[a-z]}` over `"colour, color, col our and col0ur"` gives (0,6) "colour" with no
  errors and (8,13) "color" with one substitution and one deletion; "col our" and "col0ur" are
  searched on their own in the probe and give no match at all
  (`tools/probes/s75-example-test-set.py`, regex 2026.9.10, 2026-09-20). Still to do: run it against
  this port, add the entry with key `fuzzy`, and lint the note.
- **Items 4 and 5** untouched.
- **The whole slice's blind review and verifier** are outstanding, over both sittings' changes.
- Reference screenshots at 1366x768 and 390x844 once item 3 lands.

## Sitting 3 - 2026-09-21

Items 3, 4 and 5, the reference screenshots, and the slice's reviews. Green at the end: 413 web
tests, `vue-tsc` clean, 123 Pester tests, ratchet GREEN at 6487, WASM smoke GREEN over 58 published
endpoints.

### Done

- **Item 3.** The worked example - `(?:colour){e<=2:[a-z]}` over "colour, color, col our and
  col0ur" - is the nineteenth example button, keyed `fuzzy-test-set`, and its help note is linted.
  Run against this port as well as against Python: `tools/probes/s75-example-test-set.py` and
  `tools/probes/s75-example-test-set.cs`, same spans row for row.
- **Item 4.** The C# snippet's identifiers are pinned by a probe that compiles the snippet the page
  writes rather than comparing it to a stored string: `tools/probes/demo-snippet-compiles.mjs`.
- **Item 5.** The copy linter reads the seven documents and the public XML doc comments as well as
  the page. It found 43 phrasings in the documents and 18 in the doc comments; both are 0 now, and
  the allow list is still empty.

### How to re-run this sitting's measurements

All three need a published build served on port 8213:

```powershell
pwsh -File tools/run-wasm-smoke.ps1 -SkipWebBuild     # writes the publish
node tools/probes/serve-demo-publish.mjs --root demo/FuzzyRegex.Demo.Wasm/bin/Release/net10.0/publish/wwwroot
```

Then `browser_run_code_unsafe` with `filename` set to each probe:

| Probe | What it answers | This sitting's numbers |
|---|---|---|
| `tools/probes/s73-widths.mjs` | tab stops to the answer, clipping, focus rings | 32 tabs at 1920, 1440, 1366 and 1024; 9 on the phone; `aboveTheFold:true`, `clipped:0`, `ringless:[]`, `offscreen:[]` |
| `tools/probes/s75-reference-screenshots.mjs` | the two screenshots in `docs/demo/` | `alignment-1366.png`, `alignment-390.png` |
| `tools/probes/s75-hover-travel.mjs` | can the pointer reach a hover-revealed note | `gap:4`, `travelMs:330`, `onArrival:true`, `whileReading:true`, `afterLeaving:false` |

The tab count rose from 26 to 32 on the desktop and from 6 to 9 on the phone. Six of the new stops
are the heading `(?)` buttons and the nineteenth example button; re-measure after any control is
added, because the count is quoted in `App.vue`'s comments.

### The dead tab stop the browser found

A note opened by the focus put its "read more" press in the tab order, and the `(?)` shuts a peeked
note the moment it loses the focus - so Tab chose the press as the next stop, the note closed, and
the press was removed from the page before the focus arrived. One Tab that appeared to do nothing
(Chrome, 2026-09-21). The press is now a tab stop only while the note is pinned, which is the state
that survives the focus leaving.

### Reaching a note with the pointer (WCAG 2.2 SC 1.4.13)

None of these notes sits against its `(?)`: each is a line under the heading row, so a pointer
travelling to it is over neither for a few frames, and a note that closed on the button's
`mouseleave` was gone before the pointer arrived - failure F95 of that criterion
(https://www.w3.org/WAI/WCAG22/Understanding/content-on-hover-or-focus.html, read 2026-09-21).
Measured before the fix, on a publish of the same page: `onArrival:false` after a 332 ms journey
across a 4 px gap.

Two rules fix it, both in `App.vue`. `PEEK_GRACE_MS` (400 ms) delays the close the button's
`mouseleave` asks for, and the note's own `mouseenter` cancels it. `pointerOnNote` then refuses a
close while the pointer is resting on the sentence, which is the case the grace alone misses: Tab
to the `(?)`, read the sentence with the pointer over it, Tab on, and the button's `blur` shut a
note under the pointer 400 ms later.

### The budget line, and two ways it was wrong

`budgetNote` bounded only the first unbounded kind. `(?:colour){i<=2,d}` matches 19 times in nine
characters, exactly as `{i,d}` does (regex 2026.9.10, 2026-09-21), so the advice did not work; it
now bounds every unbounded kind and says "at most two of each".

A cost equation was treated as bounding everything, and then, for one round, as bounding nothing it
did not price. It does not bound a kind it never prices:
`(?:colour){d,1i+1s<3}` deletes the whole pattern (`fuzzy_counts=(0, 0, 6)`), so `d` is named. A
kind the equation prices at zero - `{0d+1i<3}` - is unbounded too, and there the page stays silent
on purpose: the only advice it could give is a re-pricing of somebody's equation.

A kind can also carry both a price and a constraint. Upstream allows that, although two
constraints on one kind are the parse error "re-use of fuzzy constraint", and the price binds the
kind either way round: `{i}` inserts six characters into "czozlzozuzzr" and neither
`{i,1i+1d<3}` nor `{1i+1d<3,i}` matches it at all (regex 2026.9.10, 2026-09-21,
`.scratch/s75-priced-and-named.py`, the cases are pinned in `budget.test.ts`). Getting this wrong
in the first attempt at the fix also broke the advice: the line bounds a letter at its first
occurrence in the budget text, so `{1i+1d<3,i}` came out as `{1i<=2+1d<3,i}`, which upstream
refuses to compile.

### Review

Three blind passes, each over the changes the one before it had not seen.

1. **Pass 1**, over sittings 1 to 3: four findings raised, three reproduced and fixed - the budget
   line bounding only the first kind, the unreachable hover-revealed note, and two copy-linter
   guards that asserted nothing because the allow list was empty. The fourth (an alignment index
   landing between the halves of a surrogate pair) was rejected: the reviewer's own note said the
   engine never emits one, and only a hand-written worker reply could.
2. **Pass 2**, over the fixes: four findings, all four reproduced and fixed - a note closing under
   the pointer when its `(?)` lost the focus, two travel tests that raced a real 400 ms timer or
   passed with the grace reverted, and the cost-equation case above.
3. **Pass 3**, over those fixes: two findings, both reproduced and fixed, both in `budget.ts` - a
   kind holding a price and a constraint at once, and the advice that case printed. Everything
   else the pass tested survived, including all four hover and tab-order tests, which it checked
   by removing each guard in turn.

Still outstanding, and the reason this is a checkpoint: the independent verifier, and one blind
pass over the last `budget.ts` fix.
