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
