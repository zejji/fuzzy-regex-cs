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
