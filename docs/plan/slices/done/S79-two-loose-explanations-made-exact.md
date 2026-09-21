---
slice: S79
phase: 9
title: Two loose explanations made exact - the test set with budgets, and what the named-lists box accepts
delivers: []
---

# S79 - the two explanations a reader cannot act on

> **Owner findings (2026-09-21).** Two pieces of help stop short of the question a reader actually
> has. The test-set section shows `{e<=n:[set]}` and never says how to write it with per-kind
> budgets or with a cost equation. And the named-lists box says "one list per line, as
> `name: word, word`", which does not say how commas, semicolons or spaces are treated.

## 1. The test set with the other budget forms

`docs/COMPARISON.md`, "`{e<=n:[set]}`: constrain which characters an edit may touch", covers the set
with a single total budget and stops. The demo's Help tab shows that section for the "Edits limited
to letters" sample, so the gap is on the page as well as in the document.

Measured on 2026-09-21, `regex` 2026.9.10 under version 1 and this port, which agreed on every row
(`.scratch/s78b-test-set-combinations.py` and a scratch console program):

- The set is the last thing in the braces, after every budget and after any cost equation:
  `{i<=1,d<=1,s<=1:[a-z]}` and `{i<=1,d<=2,s<=3,2d+1s<4:[a-z]}` both work, and so does a cost
  equation on its own, `{2i+1d+1s<4:[a-z]}`.
- Writing it first is silently wrong rather than an error. `{[a-z]:i<=1,d<=1,s<=1}` compiles on both
  engines and matched nothing in "colur", where the correct spelling matches.
- An insertion tests the character it brings in from the SUBJECT, and a substitution tests the
  character it puts in. A deletion is not tested at all: `(?:col-our){d<=1:[a-z]}` matches "colour",
  deleting a hyphen that `[a-z]` does not hold.

Write those three facts into the section, with one example that shows a per-kind budget and one that
shows a cost equation. The section is rendered by the demo's Help tab, so it may hold paragraphs,
inline code and fenced code, and nothing else (`tools/build-demo-help.ps1` enforces that).

## 2. What the named-lists box actually accepts

`DemoEngine.TryParseNamedLists` (`demo/FuzzyRegex.Demo.Wasm/DemoEngine.cs:792`) is the contract, and
neither the box's hint nor the Subject note describes it:

- One list per line. Lines split on CRLF, LF or CR; blank lines are skipped.
- The FIRST colon on the line separates the name from the words, so a word may contain a colon.
- The name is trimmed. Names are compared ordinally, so `Fruit` and `fruit` are two lists, and
  defining the same name twice is refused.
- Words are separated by a comma OR a semicolon, and each word is trimmed.
- A space does not separate words, so `hot dog` is one word of seven characters. This is the part
  the current hint hides, and it is the one a reader is most likely to get wrong.
- A line with no colon, and a list with no words, are both refused with a message naming the line.

Prove each rule with a test before writing the sentence - the rules above are read off the code, and
a rule nobody ran is a rule that may already be false. Then say them in the two places a reader
looks: the hint under the box and the Subject heading note. Keep the hint to one line; the note can
carry the rest.

## 3. Done when

- [ ] The test-set section answers both combinations, with a run example for each.
- [ ] A test pins the demo's named-list parsing rules, including the space-inside-a-word case and
      both refusals.
- [ ] The box's hint and the heading note say what the parser does, in a reader's words.
- [ ] `demo/web/tests/copy.test.ts` passes over the new strings, and the help build stays green.
- [ ] Read on the published build at 1366 and 390, because both strings live in the input pane.

## Closing notes (2026-09-21)

**What landed.** The `{e<=n:[set]}` section now answers the question a reader with a per-kind budget
or a cost equation actually has, and the named-lists box says what it does with commas, semicolons,
spaces and case. Both were measured on `regex` 2026.9.10 and this port, which agreed on every row.

**The set constrains two of the three kinds, not all three.** An insertion is allowed only if the
character it brings in is in the set, a substitution only if the character it puts there is, and a
deletion is not constrained at all - it removes a character and adds none. Measured three ways:
`(?:a--b){i<=1,d<=2,s<=0:[a-z]}` on "ab" answers identically to the same pattern with `[a-z-]` and
with no set at all, deleting two hyphens the set does not hold. The set's job does not change with
the budget form: `{i<=2,d<=0,s<=0,2d+1s<4:[a-z]}` still refuses to insert a digit, though `2d+1s`
prices no insertions.

**The section's opening sentence had been wrong since it was written**, saying an edit that
"removes" a character from the set counts against the budget. The blind review caught it as a
contradiction with the new paragraphs rather than on its own, which is the argument for writing the
detail down: a vague sentence cannot be contradicted, and a precise one can.

**Named lists, read off `TryParseNamedLists` and now pinned:** one list per line, blank lines
skipped; the first colon ends the name, so a word may contain one; names are compared ordinally, so
`Fruit` and `fruit` are two lists; words are separated by a comma or a semicolon and each is
trimmed; a space does not separate, so `hot dog` is one word of seven characters; and a list the
pattern never names is refused rather than ignored.

**Two tests were weaker than they looked, and mutation is what showed it.** The mixed
"apple; banana, cherry" row passed with the semicolon removed from `_wordSeparators`, because the
comma carried it; and the case-sensitivity test exercised `FuzzyRegex.ToCompilerNamedLists` rather
than the demo's own comparer, so it passed with `DemoEngine`'s `StringComparer.Ordinal` turned to
`OrdinalIgnoreCase`. Both are now written so that the mutant fails them: a semicolon-only row, and a
pattern naming both `\L<Fruit>` and `\L<fruit>` over two lists. Each mutant was run and seen to fail
the right test, and the file was restored.

**Review.** One blind pass over the diff, then a second over the fixes (Opus; briefs at
`.scratch/S79-review-brief.md` and `.scratch/S79-delta-brief.md`). The first raised five findings and
all five reproduced: two wrong generalisations in prose I had written, one pre-existing wrong
sentence they contradicted, and the two weak tests above.

**Green at the close.** 41 documentation examples, 443 web tests, `vue-tsc` clean, the help build
GREEN at 16 keys, ratchet GREEN at 6,508 tests against a baseline of 6,400, and the box hint and
heading note read correctly on the published build at 1366 and 390.
