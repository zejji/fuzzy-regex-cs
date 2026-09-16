---
slice: S72
phase: 9
title: Demo v2 - an editable sample per major feature, help generated from COMPARISON.md, regex101 polish
delivers: []
---

# S72 - v2, where the demo becomes the feature tour

The owner's ask on 2026-09-16 was that every major feature be experimentable, that the samples be
editable rather than read-only, and that contextual help come from the documentation rather than
from a second set of words. v1 (S71) is the small page; this is the widening, and it is the last
demo slice before polish is called done.

**Blocked on Phase 8.** The help panels are generated from `docs/COMPARISON.md`, which Phase 8
writes and which does not exist yet. Do not start this slice until that file's section headings are
stable, and do not write a stand-in copy of the prose here: two sources is precisely the failure the
owner's single-source instruction is aimed at.

## Scope

- **One editable sample per major feature**, each a row in `demo/wwwroot/examples.json` carrying a
  feature key, a title, one sentence of why it matters, and the three inputs (plus a fourth,
  replacement, where the feature needs it). Loading a sample fills the inputs and the user can edit
  any of them - the sample is a starting point, never a fixed demonstration. The eight:
  **fuzzy budgets** (`{e<=2}`, `{i<=1,d<=1,s<=1}` and a cost expression), **BESTMATCH and
  ENHANCEMATCH** as a pair on the same subject so the difference is visible in one screen,
  **named lists**, **POSIX leftmost-longest matching**, **partial matching**, **reverse searching**,
  **timeouts** (a pattern that actually fires `MatchTimeout`, and the error the page shows for it),
  and **replace templates**. Version 0 against version 1 is already the README's headline
  divergence and gets a ninth row only if it fits without crowding.
- **The fuzzy and BESTMATCH samples show the counts**, not just the span: the per-error-type
  substitution, insertion and deletion totals are the reason this library exists, and a demo that
  highlights a span without them has demonstrated nothing an existing .NET regex cannot do.
- **Contextual help generated at build time from `docs/COMPARISON.md`.** A
  `tools/build-demo-help.ps1` extracts the named sections into `demo/wwwroot/help.json`, keyed by
  the same feature key the examples use. It **fails** when a referenced heading is missing, which is
  what makes single-source a guarantee rather than an intention: rename a heading in Phase 8's file
  and the demo build goes red instead of the panel going blank. Wire it into `pages.yml` ahead of
  the publish, and run it in `ci.yml` too so a documentation rename is caught on the pull request
  that makes it.
- **UI polish, with regex101 as the named reference** (owner, 2026-09-16). Borrowed, specifically:
  flags edited in their own control beside the pattern rather than buried in the pattern string;
  live re-matching as you type, debounced, with the previous result left in place until the new one
  arrives so the pane never flashes empty; adjacent matches highlighted in alternating tones so two
  touching matches are distinguishable; hovering a highlighted span highlights its row in the match
  table and hovering a row highlights the span; and the parse error shown inline under the pattern
  field with a caret at the reported position. **Not borrowed, deliberately:** the token-by-token
  pattern explanation pane, the step debugger and the code generator. Each is a substantial feature
  in its own right, none of them shows anything about *this* library that the samples do not, and
  the ROADMAP's "scope, deliberately small" is still in force.
- **Accessibility kept, not traded for the polish.** Every input has a real `<label>`; the help
  panels are disclosure widgets operable from the keyboard, never hover-only; every sample in the
  sidebar is reachable and activatable by keyboard alone with a visible focus ring; the result pane
  is an `aria-live="polite"` region that announces the match count so a screen reader is told the
  answer changed; and the highlight colours meet WCAG 2.2 AA contrast **and** are never the only
  signal - a highlighted span also carries a border, because alternating tones alone are invisible
  to a portion of the audience this demo is trying to impress.
- **The caps and the worker design from S71 are untouched**, including under the new samples: the
  timeout sample in particular must exercise the `MatchTimeout` path and the `terminate()` path in
  the same page without either masking the other.

## Verification

- `tools/build-demo-help.ps1` green against the real `docs/COMPARISON.md`, and proven to **fail**:
  rename one heading in a scratch copy, confirm the script reports that key by name, restore it. A
  generator nobody has seen fail is not a guarantee.
- `tools/run-wasm-smoke.ps1` green; `dotnet build FuzzyRegex.slnx --configuration Release` green;
  `dotnet run --project tests/FuzzyRegex.Tests` green.
- Each of the eight samples exercised on the served page (`python -m http.server 8080` from the
  published `wwwroot`) and its answer checked against the library's own test suite or
  `tools/probes/` output - **not** against the demo's own output, which would be the demo grading
  its own homework. Quote the eight expected answers in the closing notes with where each came from.
- Keyboard pass: tab from the pattern field through every sample, every help panel and the result
  table without touching the mouse. Contrast pass: measure the highlight tones against their
  backgrounds and quote the ratios.
- The deployed URL re-checked after `gh workflow run pages.yml`, cache disabled.

## Done when

- [ ] Eight editable samples landed, each with its counts or spans visible and its answer sourced
      from outside the demo.
- [ ] Help generated from `docs/COMPARISON.md` by a script that has been seen to fail on a rename;
      no second copy of the prose anywhere under `demo/`.
- [ ] The five borrowed interactions in place, the three rejected ones named in the closing notes
      with the reason; accessibility items all met with the measured contrast ratios recorded.
- [ ] Ratchet GREEN, blind review, commit. **Hunt:** a pathological pattern that freezes the page
      despite the worker - the new live-as-you-type path spawning a request per keystroke so the
      terminate-and-respawn cycle never keeps up, or a sample whose subject is long enough that
      building the highlight DOM blocks the main thread on its own; a help panel that silently
      renders empty because the extractor matched a heading that moved rather than failing; an
      integrity failure after line-ending conversion, which the new generated `help.json` reopens
      if it is ever committed rather than generated in CI; a "BESTMATCH" sample whose answer is the
      same with and without the flag, which demonstrates nothing and would pass any assertion
      written against the demo's own output; alternating highlight tones that pass contrast against
      the page background but not against each other.
- [ ] Phase 9 closed in `docs/plan/STATE.md`, with the remaining v3 polish ideas listed there or
      dropped, and the ROADMAP's estimate for the phase compared against what the three slices
      actually cost.
