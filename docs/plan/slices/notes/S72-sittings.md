# S72 sittings

The slice file (`docs/plan/slices/S72-demo-v2-features-and-help.md`) is the spec. This file is the
per-sitting record: what each sitting did, what it measured, and what the next one should pick up.

## Sitting 1 (2026-09-19)

**The Phase 8 block is lifted.** The slice file says "Blocked on Phase 8", and that block was real
when it was written. `docs/COMPARISON.md` now exists (41,836 bytes, written by S65, which is in
`docs/plan/slices/done/`), and its headings are held stable by
`tests/FuzzyRegex.Tests/Conventions/ComparisonCoversDivergencesTests.cs` (46 headings measured,
2026-09-18). So the slice starts.

**The eight features against what the engine can already do.** Read from the committed
`demo/FuzzyRegex.Demo.Wasm/DemoEngine.cs` (three strings in, one JSON string out) and
`src/FuzzyRegex/FuzzyRegexOptions.cs`:

| Feature | What the sample needs from the engine | Already there? |
| --- | --- | --- |
| Fuzzy budgets `{e<=2}`, `{i<=1,d<=1,s<=1}`, cost form | pattern only | yes |
| BESTMATCH / ENHANCEMATCH | `BestMatch`, `EnhanceMatch` flags | yes (flag names parse) |
| POSIX leftmost-longest | `Posix` flag | yes |
| Reverse searching | `RightToLeft` flag, or inline `(?r)` | yes |
| Timeouts | a pattern that fires `MatchTimeout` | yes |
| Named lists `\L<name>` | a `namedLists` dictionary on the constructor | **no input for it** |
| Partial matching | `partial: true` on the call, `Match.PartialMatch` on the way back | **no input, not reported** |
| Replace templates | `Replace(subject, replacement)` | **no input, not reported** |

So v2 needs three new inputs (replacement, named lists, partial) and two new answer members
(`partialMatch` per match, `replaced` for the whole subject). That is the widening the slice's
"plus a fourth, replacement, where the feature needs it" implies, and it is engine-side work before
any of it can be a sample.

**Planned chunks** (each under an hour, each ending green and committable, per the owner's
pause-anytime rule):

1. Engine contract v2: `DemoEngine.Run` gains replacement, named lists and partial; the answer gains
   `replaced` and `partialMatch`; new caps for the new inputs; `Interop.cs` and `worker.js` follow.
   Tests first in `tests/FuzzyRegex.Tests/Gaps/Demo/`.
2. `tools/build-demo-help.ps1` and `help.json`, keyed by feature, extracted from `docs/COMPARISON.md`,
   proven to fail on a renamed heading; wired into `ci.yml` and `pages.yml`.
3. `examples.json` v2: eight feature rows, each answer sourced from the test suite or `tools/probes/`,
   never from the demo's own output.
4. The page: the new inputs, help disclosures, the five borrowed regex101 interactions, the
   accessibility items, contrast measured.
5. Verification: wasm smoke, served page, keyboard pass, screenshots at 390 and 1280, blind review,
   verifier, close.

### What landed in sitting 1

Chunks 1, 2 and 3 of the five. Chunks 4 (the page) and 5 (verification, review, close) are the next
sitting's work, so this is a **checkpoint commit**: the slice file stays in `docs/plan/slices/`.

**Engine contract v2** (`DemoEngine.cs`, `Interop.cs`, `wwwroot/worker.js`). `Run` now takes six
strings - `mode`, `replacement` and `namedLists` after the original three - and the three-argument
overload delegates to it with three empty strings, so a browser still running the S71 page gets
byte-for-byte the S71 answer (`The_three_argument_call_still_answers_exactly_as_it_did`). The answer
gained `replaced` (the whole rewritten subject, replace mode) and a per-match `partialMatch`, both
omitted when they do not apply, which is what leaves the S71 wire format untouched. Three new caps:
`MaxReplacementLength` 1,000, `MaxNamedListsLength` 2,000, `MaxReplacedLength` 200,000.

**Partial matching is a mode, not a flag on the walk.** Upstream's scanning functions take no
`partial` argument and neither do this port's, because a partial match is only ever the LAST thing a
search finds; "every match, and the last one may be partial" is not a question the engine answers.
The mode asks `Match(subject, partial: true)`, which is the question that has one. Recorded in
DECISIONS.md.

**Named lists are typed as one list per line, `name: word, word`**, parsed engine-side into the
constructor's dictionary. A line with no colon, a list with no words and a list defined twice are
each their own error sentence.

**`tools/build-demo-help.ps1`** lifts the help panels out of `docs/COMPARISON.md`, keyed by the same
feature key `examples.json` uses, and emits blocks of plain/`code` runs rather than markdown - so the
page needs no markdown renderer and nothing in that file can inject markup. Generated at build time
and gitignored, never committed.

The fail-on-rename guarantee, proven rather than asserted (2026-09-19):

    # A copy of docs/COMPARISON.md with `### `FuzzyRegexOptions.Posix`` renamed to
    # `### `FuzzyRegexOptions.PosixMode``, written to .scratch/renamed.md, then:
    pwsh -File tools/build-demo-help.ps1 -Comparison .scratch/renamed.md -Destination .scratch/x.json
    build-demo-help: FAILED - 1 heading(s) missing from ...\.scratch\renamed.md
      key 'posix' wants a section headed: ### `FuzzyRegexOptions.Posix` / `(?p)`: leftmost-longest ...
    Either the heading was renamed (update the map in tools/build-demo-help.ps1) or the section was deleted.
    exit=1

and `.scratch/x.json` was not written. Wired into `tools/build-demo-web.ps1` (first, as the cheapest
gate), into `ci.yml` as its own step writing to `$env:RUNNER_TEMP` so it cannot dirty the tree before
the STATUS.md check, and into `pages.yml`'s path filter so an edit to `docs/COMPARISON.md` redeploys.

**Three sections added to `docs/COMPARISON.md`** under a new "Matching modes the built-in engine does
not have": `Posix`, partial matching and `RightToLeft`. They exist because the help generator needs a
source section per feature key, and they are real user documentation with runnable examples - the
doc-example checker compiles and runs all 37 blocks.

**`examples.json` is 18 rows**: 14 keyed feature rows (fuzzy x3, bestmatch x2, enhancematch,
namedlists, posix x2, partial, reverse, replace x2, timeout) and the 4 unkeyed syntax-tour rows S71
shipped. Every expectation comes from a real upstream run, never from this port's output:
`tools/probes/demo-examples-expectations.py` reads the shipped JSON and now understands the new
fields (`namedLists` become keyword arguments, `mode` chooses `search(partial=True)` or `sub`).
Its 2026-09-19 output against `regex 2026.9.10` is quoted in `DemoExamplesTests`'s remarks and is the
`_upstream` table. `tools/probes/demo-v2-expectations.py` is the same provenance for the three new
COMPARISON sections.

**The port agreed with upstream on every new row, first run** - including the two that were most
likely to diverge, `BestMatch` (`(foobar){e}` on `xirefoabralfobarxie`: spans `[[11,5],[16,3],[19,0]]`,
first match `fobar` at (0,0,1)) and `EnhanceMatch` on the same subject (`[[0,3],[4,5],[11,4],[15,1],
[16,3],[19,0]]`, first `xir` at (2,0,3)). 75 demo tests, 0 failed.

**Two pre-existing failures fixed in `docs/COMPARISON.md`**, both in the `maxCompiledNodes` block
S56b added on 2026-09-18 (dfa8767), both found by running `tools/check-doc-examples.ps1` here:
`untrustedPattern` was never declared, so the block did not compile; and its expected output ended in
`...`, which the checker compares literally. **`tools/check-doc-examples.ps1` has therefore been red
on `main` since dfa8767**, and the fix is in this commit rather than a separate one only because the
slice's own new sections could not be verified until it was green.

**A third pre-existing failure fixed the same way**: `dotnet csharpier check .` - the CI formatting
gate - has been red on `main` for `FuzzyRegex.Tests.csproj` and `FuzzyRegex.Demo.Wasm.csproj` since
e38f270 (S71). Both are whitespace only (one collapsed `<EmbeddedResource>` element, one blank line),
neither file differs from `main`, and reverting them leaves the gate red, so the reformat is in this
commit.

### Review (sitting 1)

One blind pass over the whole diff (Opus, reproduction-only brief, no fixes proposed). Six findings
raised, all six reproduced, five fixed here:

1. **Partial mode hid a clipped answer.** `Describe(match, MaxSpans, out _)` discarded the clip flag
   and the answer hardcoded `truncated: false`; `(\w)+` over 60,000 characters rendered 49,997
   captures and claimed to be complete. Fixed, and pinned by
   `A_partial_answer_whose_capture_list_was_clipped_says_truncated`.
2. **Replace spent the budget twice.** `regex.Replace(...)` and then `TryWalk` each started their own
   two-second clock (measured: 3.4 s for a subject the walk alone answered in 1.8 s). The deadline is
   now taken once, in `Deadline()`, and handed to both.
3. **Replace built the whole rewritten subject before the cap could clip it** - 695 MB peak working
   set for a 100,000-character subject and a 1,000-character template, both inside their own caps,
   which in a browser worker is the freeze the caps exist to prevent. Replace now stops at
   `MaxMatches` occurrences, the same cap the walk uses, and says `truncated`. Pinned by
   `Replace_stops_at_the_match_cap_and_says_so`.
4. **A bad replacement template leaked .NET plumbing into the page**: `unknown group (Parameter
   'replacement')`. The parameter clause is stripped now; pinned by
   `A_template_naming_a_group_that_does_not_exist_is_a_readable_error`.
5. **`The_three_argument_call_still_answers_exactly_as_it_did` was vacuous** - the three-argument
   overload IS the six-argument call with three empty strings, so it compared a value with its own
   definition. Replaced by `An_empty_mode_is_the_ordinary_walk`, which pins the literal S71 answer.

The sixth is real and is chunk 4's work, not a defect in this commit: the shipped page still posts
only pattern, flags and subject, so the four rows that need `mode`, `replacement` or `namedLists`
would answer wrongly **if this tree were deployed**. `demo/web` is not touched by this commit and
`pages.yml` deploys from `main`, so nothing is live; the next sitting must land the page before this
branch merges.

Not findings, checked and dropped by the reviewer: worker.js, `Interop.cs` and the engine agree on
argument order; the new expectations come from `regex 2026.9.10` under `VERSION1`, not from the port;
the help generator reddens the front-end build on a renamed heading; the generated `help.json` cannot
dirty the tree.

**Still owed, and the first thing the next sitting does:** a blind pass over the five fixes above -
`Deadline()`, the new `TryWalk` signature, the replacement cap, the `ArgumentException` catch and the
four new tests - which no reviewer has seen. The independent verifier belongs to the closing sitting.

### What the next sitting picks up

1. The page (chunk 4): inputs for mode, replacement and named lists; help disclosures reading
   `help.json`; the five borrowed regex101 interactions; the accessibility items. Keep every S71
   pattern listed in `S71-sittings.md` "Post-landing review fixes".
2. `demo/web/src/types.ts` and `shapes.ts` still describe the S71 contract - `replaced`,
   `partialMatch`, `key`, `mode`, `replacement` and `namedLists` are not in them yet, and every JSON
   boundary is validated there.
3. `demo/README.md` does not yet mention `help.json` or the six-argument `Run`.
4. The timeout sample has not been run anywhere yet. Under the memory rule it needs
   `DOTNET_GCHeapHardLimit=0x40000000`, a timeout and a subject under 100 characters (it is 36); the
   right home is `tools/run-wasm-smoke.ps1` or the served-page check, where the two-second budget is
   the subject. `The_timeout_example_is_the_one_row_with_no_upstream_answer` pins only that it is the
   single row with no upstream expectation.
5. Chunk 5: wasm smoke, served page, keyboard pass, screenshots at 390 and 1280, blind review,
   independent verifier, close.
