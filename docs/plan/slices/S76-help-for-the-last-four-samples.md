---
slice: S76
phase: 9
title: A help panel for the last four samples, and an empty state that tells the truth
delivers: []
---

# S76 - help for the last four samples

> **Owner finding (2026-09-20, evening).** Four of the eighteen samples open a Help tab with
> nothing in it: "Named groups and every capture", "Set operations", "Unicode properties" and
> "Case-insensitive, with a flag". Run this in the demo worktree after S75 has landed and
> `phase9-demo` has merged into main.

## 1. What the page does today

The orchestrator traced it before filing this slice, so the slice starts from the cause rather than
the symptom:

- `demo/FuzzyRegex.Demo.Wasm/wwwroot/examples.json` holds eighteen rows. Fourteen carry a `key`;
  the last four carry none. Those four are precisely the four the owner named.
- `demo/web/src/demo.ts:367` resolves the panel as `help.value?.entries[helpKey.value] ?? []`, so a
  row with no key has no sections, and the Help tab renders empty.
- `demo/web/src/App.vue` then shows the empty state, "Load a sample to read what it shows." That
  sentence is true before the first sample is loaded and false in exactly this case, where a sample
  **is** loaded and simply has no help. The reader is told to do the thing they have just done.
- `tests/FuzzyRegex.Tests/Gaps/Demo/DemoExamplesTests.cs:352` tolerates the gap on purpose today:
  it filters with `Where(key => key.Length > 0)` before comparing against the nine feature keys.

So there are two defects: four rows with no help, and an empty state that misreads the one case it
is left to cover.

## 2. Give each of the four a key

`tools/build-demo-help.ps1` holds the whole contract: a map from a feature key to the headings of
`docs/COMPARISON.md` that explain it, checked at build time so a key with no section fails the
build. Two of its eleven keys (`indices`, `version`) are opened by a heading note rather than by a
sample, which is why the map is longer than the sample list.

Candidate sections that already exist, with what each one would and would not answer:

| Sample | Nearest existing section | Does it answer the sample? |
| --- | --- | --- |
| Named groups and every capture | "Match.Groups is an IReadOnlyDictionary as well as a list" (COMPARISON.md:521) | Yes. It is about this exact behaviour: a repeated group keeps every capture. |
| Set operations | "Version 1 is the default" (:311) | Partly. It mentions nested sets and `[[a-z]--[aeiou]]` in passing, but the section is about a version default, not about set operations. |
| Unicode properties | "The Unicode data is version 17.0.0" (:682) | Barely. That section is about which Unicode version the tables come from, not about `\p{...}`. |
| Case-insensitive, with a flag | "Version 1 is the default" (:311) | Partly; "The Turkic I pairings are not applied by default" (:654) says what is deliberately not done. |

**The decision this slice makes.** Map the first sample to the section that already answers it. For
the weak three, do not stretch an unrelated section over a sample just to fill the panel - a help
panel that does not answer the question is worse than one that admits it has nothing. Write the
missing sections in `docs/COMPARISON.md` instead, where they belong: all three are real differences
between Python's `regex`, `System.Text.RegularExpressions` and this port, which is what that
document is for.

Before writing a word of those sections, **prove the behaviour by running it** - upstream through
`tools/record-oracle.py`, .NET through a scratch program, this port through a test. Three claims
worth checking, none of which may be asserted from memory:

1. Whether `System.Text.RegularExpressions` spells character-class subtraction `[\w-[\d]]` with one
   dash where upstream and this port take `--`, and what each engine does with the other spelling.
2. Whether `\p{Greek}` names a script in each engine, and what `\p{IsGreek}` does.
3. What `IgnoreCase` folds in each engine, and where full case folding changes the answer.

Record the evidence and the date beside the prose, as the other sections do.

Use one key per sample (`namedgroups`, `setops`, `unicodeprops`, `ignorecase`) rather than reusing
`version` for two of them: the map allows several keys to name the same heading, and distinct keys
keep "which panel does this sample open" a one-line answer.

## 3. The empty state

Once every sample carries a key, "Load a sample to read what it shows." is reachable only before
the first sample is loaded, which is what it says. Keep the sentence, and add a test that pins the
reason it is now true: with a sample loaded, the Help tab always has at least one section.

If the slice finds a sample it genuinely cannot document, the empty state needs a second sentence
for that case instead. Do not leave one message covering both.

## 4. Tests, and what to check by hand

- `DemoExamplesTests`: drop the `Where(key => key.Length > 0)` filter, extend the `features` list to
  thirteen keys, and assert every row has a non-empty key. Prove it fails before the fix.
- `BuildDemoHelp.Tests.ps1`: the three lists it compares (map, heading notes, examples) must still
  agree once four keys are added.
- A web test that walks all eighteen samples and asserts the Help tab renders at least one section
  for each.
- In the browser, at 1366 and 390 wide: load each of the four, open Help, and read what comes up.
  A panel that renders is not the same as a panel that answers the question - judge the prose as a
  reader who has just met the sample.
