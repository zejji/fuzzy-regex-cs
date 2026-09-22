# S57f sittings

## Sitting 1 - 2026-09-22, 06:14 to 07:29

### The ten rows, copied off disk

`TestResults/` is gitignored and each seed's report is overwritten by the next run at that seed, so
the ten rows the slice is about are recorded here verbatim. The default wave had already overwritten
`report-20260922.txt` by the time this sitting started; these came from re-running the consumer over
the 6000-row gate wave still on disk.

Recorded with (the pre-S57e generator list, `fuzzy-anchored` removed, as the slice file specifies):

```
pwsh -File tools/run-oracle.ps1 -Count 6000 -Seeds 20260922 -Generator literals,literal-dot,anchors,
classes,groups,quantifiers,boundaries,backrefs,case-folding,reverse,substitution,iteration,
interactions,lookaround,conditionals,recursion,partial,partial-sliced,posix,verbs,fuzzy,timeout
```

That wave is 126080 rows. The tally: `agree 125520  unsupported 0  expected 488  timeout 3
resource 59  diverge 10`. Upstream `regex 2026.9.10` pinned, upstream commit
`7dd71c15c4fb5c94206bed1763abd4c2bd2f1b33`.

A note for the next sitting: the Release `obj` tree under `src/FuzzyRegex` was held open by another
process, so the first launch died with `error : Error writing to source link file
'obj\Release\net10.0\FuzzyRegex.sourcelink.json': The process cannot access the file`. Setting
`$env:IntermediateOutputPath = 'obj/s57f-release/'` before the run steps around it without touching
the process that holds the lock.

```
DIVERGE row 73420 (interactions) sub flags=0x2 version=V0
  pattern  '(?b)(?r)(?p)^\\L<w1>{1<=e<=2}(?:\\p{Lu}(\\p{ASCII})\\s){s<=1,i<=1,d<=1}$'
  subject  '𝔘𐐀𝔘𐐀𐐀'
  template '\\1\\t\\x41'   [count 1]
  lists    w1=['𐐀','𝔘']
  upstream sub 0 '𝔘𐐀𝔘𐐀𐐀'
  port     sub 1 '𐐀\u0009A'

DIVERGE row 74120 (interactions) split flags=0x4102 version=V0
  pattern  '(?r)(?(?=[\\p{ASCII}&&\\p{L}])[\\w\\s]|\\p{Ll})(?(?=.)[\\p{ASCII}&&\\p{L}])(?(?=[a-f])[^a-f]|[\\p{L}||\\p{N}])\\b'
  subject  '\u000dıı ﬁﬁ'
  upstream split 2 ' ﬁﬁ' ''
  port     split 1 '\u000dıı ﬁﬁ'

DIVERGE row 74222 (interactions) finditer flags=0x8 version=V0
  pattern  '(?e)(?r)(?>(?:\\w*\\p{Lu}?){e<=2:\\d})(?P<g1>[0b]*)(?:(?(1)(?=(?P>g1))[\\w\\s]))+?'
  subject  '00\u000d\u000aabb'
  upstream matches 5 | match 0:(4,3)[(4,3)] 1:(5,2)[(5,2)] last=1/g1 || match 0:(4,0)[(4,0)] 1:(4,0)[(4,0)] last=1/g1 || match 0:(3,0)[(3,0)] 1:(3,0)[(3,0)] last=1/g1 fuzzy=(0,0,2)[s:][i:][d:4,5] || match 0:(0,2)[(0,2)] 1:(0,2)[(0,2)] last=1/g1 || match 0:(0,0)[(0,0)] 1:(0,0)[(0,0)] last=1/g1
  port     matches 5 | match 0:(4,3)[(4,3)] 1:(5,2)[(5,2)] last=1/g1 || match 0:(4,0)[(4,0)] 1:(4,0)[(4,0)] last=1/g1 || match 0:(3,0)[(3,0)] 1:(3,0)[(3,0)] last=1/g1 fuzzy=(0,0,2)[s:][i:][d:3,4] || match 0:(0,2)[(0,2)] 1:(0,2)[(0,2)] last=1/g1 || match 0:(0,0)[(0,0)] 1:(0,0)[(0,0)] last=1/g1

DIVERGE row 74554 (interactions) match flags=0x100 version=V0
  pattern  '(?:(?:A(?:\\W){i<=1}){1<=e<=2}(*SKIP)\\D|[a\\d])\\K([[:alpha:]]){3,}$'
  subject  '\u000a AAA'
  upstream match 0:(2,3)[(2,3)] 1:(4,1)[(2,1),(3,1),(4,1)] last=1/- partial fuzzy=(0,0,1)[s:][i:][d:0]   [python codepoints 2,5]
  port     match 0:(3,2)[(3,2)] 1:(4,1)[(3,1),(4,1)] last=1/- partial fuzzy=(1,0,0)[s:0][i:][d:]

DIVERGE row 76118 (interactions) finditer flags=0x1000a version=V0
  pattern  '(?e)(?r)^[^a-f]?(?(?=\\w)\\D)(?:A[A-Z]?){e<=2,s<=1}$'
  subject  ' A'
  upstream matches 1 | match 0:(0,2)[(0,2)] last=-1/- fuzzy=(1,0,0)[changes unavailable upstream]
  port     matches 1 | match 0:(0,2)[(0,2)] last=-1/-

DIVERGE row 76484 (interactions) sub flags=0x4102 version=V0
  pattern  '(?r)\\b(?(?<=[[a-f]~~[d-k]])[[a-f]~~[d-k]]|[A-Z])(?:(?:ßß[\\p{L}\\p{N}]){2i+1d+1s<=2}(*SKIP)[a-f]|[\\w--[0-9]])'
  subject  'İİİ\u000d\u000aİßßß'
  template '\\g<0>\\g<0>'   [count 0]
  upstream sub 2 'İİİİİ\u000d\u000aİßİßßß'
  port     sub 0 'İİİ\u000d\u000aİßßß'

DIVERGE row 77887 (interactions) finditer-overlapped flags=0x0 version=V0
  pattern  '(?e)(?r)(?p)\\b(\\d+)*(?:(?:(\\p{Lu})[A-Z]*){e<=2:[a-z]}(*PRUNE)\\s|\\S)(?:\\p{ASCII}[^a]{2,}?𝟮){s<=1,i<=1,d<=1}'
  subject  '𝟮🏻A😀\u000d Aaa'
  upstream matches 6 | match 0:(0,12)[(0,12)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(1,1,0)[changes unavailable upstream] || match 0:(0,11)[(0,11)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(1,1,0)[changes unavailable upstream] || match 0:(0,10)[(0,10)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(1,0,0)[changes unavailable upstream] || match 0:(0,9)[(0,9)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(1,0,0)[changes unavailable upstream] || match 0:(0,8)[(0,8)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(0,0,1)[changes unavailable upstream] || match 0:(0,7)[(0,7)] 1:unset 2:unset last=-1/- fuzzy=(1,0,1)[changes unavailable upstream]
  port     matches 5 | match 0:(0,11)[(0,11)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(1,0,0)[changes unavailable upstream] || match 0:(0,10)[(0,10)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(1,0,0)[changes unavailable upstream] || match 0:(0,9)[(0,9)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(1,0,0)[changes unavailable upstream] || match 0:(0,8)[(0,8)] 1:(0,2)[(0,2)] 2:unset last=1/- fuzzy=(0,0,1)[changes unavailable upstream] || match 0:(0,7)[(0,7)] 1:unset 2:unset last=-1/- fuzzy=(1,0,1)[changes unavailable upstream]

DIVERGE row 97332 (partial) fullmatch flags=0xa version=V0
  pattern  '(\\S??)\\.\\b'
  subject  '.\u000d'
  upstream match 0:(0,2)[(0,2)] 1:unset last=-1/- partial   [python codepoints 0,2]
  port     no match

DIVERGE row 103000 (partial-sliced) search flags=0x108 version=V0
  pattern  '(?:[\\p{L}\\p{N}](*SKIP)[a-f]|[[a-f]~~[d-k]])(?:\\p{ASCII}(*SKIP)[[a-z]--[aei]]|[\\w--[0-9]])\\b'
  subject  '𐐨A𐐨A𐐨😀a\u000d\u000a'
  slice    utf16 [0, 11)
  upstream match 0:(0,11)[(0,11)] last=-1/- partial   [python codepoints 0,7]
  port     match 0:(10,1)[(10,1)] last=-1/- partial

DIVERGE row 116428 (verbs) sub flags=0x400a version=V0
  pattern  '(?r)\\b(?:[\\w\\s]+(*SKIP)[A-Z]|ı)'
  subject  'ııﬀﬀ'
  template ' \\x41\\t>'   [count 1]
  upstream sub 1 ' A\u0009>ﬀﬀ'
  port     sub 1 ' A\u0009>ıﬀﬀ'
```

### First grouping, by what the row's shape suggests

Not verdicts - a reading order. Each group still needs its own probe or ablation before anything is
judged.

- **A reversed match starting in the wrong place** (73420, 74554, 103000, 116428): the port's match
  starts later than upstream's, or replaces less of the subject. Three of the four carry `(*SKIP)`,
  and 73420 and 74554 are reversed.
- **Fuzzy change positions off by the match direction** (74222, 76118): the same three deletions at
  `d:4,5` against `d:3,4`, and a fuzzy count the port does not report at all.
- **One fewer overlapped match** (77887): the port is missing upstream's longest, `0:(0,12)`.
- **A conditional group in a reversed split** (74120), and **a partial fullmatch the port refuses**
  (97332). Nothing in common with the others yet.

The families the same wave already tallies EXPECTED, which is the vocabulary to check a candidate
against before inventing a new one: `turkic-default-folding`,
`fuzzy-changes-leaked-from-an-abandoned-attempt`,
`fuzzy-counts-of-a-partial-are-the-innermost-sections`, `boundary-at-the-end-of-the-text`,
`group-call-loses-the-match`, `search-start-partial`, `reversed-partial-runs-out-at-the-slice-start`,
`reverse-fullmatch-narrowed-slice` and `reversed-partial-answers-the-cut-subject`. 488 rows of this
wave land in them.

### Row 97332 judged: upstream reports a partial that can never complete

**Verdict: the port is right, and this is pinned as a deliberate divergence.** Upstream's answer
contradicts the partial-matching semantics its own README defines.

Minimised to a row with no flags, then re-recorded and re-run through the port
(`tools/run-oracle.ps1 -Rows .scratch/candidate.jsonl`, 5 rows, 2 diverge):

```
DIVERGE row 2 (rows) fullmatch flags=0x0 version=V0
  pattern  '(\\S??)\\.'
  subject  '.a'
  upstream match 0:(0,2)[(0,2)] 1:unset last=-1/- partial   [python codepoints 0,2]
  port     no match
```

The original row, `(\S??)\.\b` on `'.\r'` under IGNORECASE|MULTILINE, diverges the same way; the
`\b`, the flags and the carriage return are all inert. Dropping the lazy repeat (`\.` on `'.a'`)
agrees - both sides say no match - so the repeat is what produces it.

**Why upstream is wrong.** `upstream/README.rst:270` defines a partial match as one where "the
string has been truncated and you want to know whether a complete match could be possible if the
string had not been truncated", and its worked example at `:288` makes the negative case explicit:
`regex.compile(r'\d{4}').fullmatch('a', partial=True)` is `None`, commented "It'll never match."

No string beginning `.a` can ever be fullmatched by `(\S??)\.`. The pattern is at most two
characters wide, so any fullmatch is either `.` or `<non-space>.`; a two-character one must end in
`.`, and `.a` does not. The text cannot be completed, so the documented answer is `None`, which is
what the port gives.

Upstream also disagrees with itself as the subject grows, which is the shape of a bug rather than a
rule. Each line is `regex.compile(PATTERN).fullmatch(SUBJECT, partial=True)` under upstream regex
2026.9.10, no flags:

```
'(\S??)\.'   on '.a'    span=(0, 2) partial=True groups=(None,)
'(\S??)\.'   on '.ab'   none
'(\S??)ab'   on 'aba'   span=(0, 3) partial=True groups=(None,)
'(\S??)ab'   on 'abab'  span=(0, 4) partial=True groups=(None,)
```

`.ab` and `abab` are both longer than their pattern's maximum width, so neither can complete; one
gets `None` and the other a partial.

**What is not yet known.** Which upstream code path sets the partial has not been located. It is
not the required-string search: a character class tail, which is not a string node, behaves
identically (`(\S??)[.]` on `'.a'` is also partial (0,2)). It is not the repeat-count partial at
`upstream/src/_regex.c:5006`, whose condition is `count == text_end - text_pos && count <
max_count`, and the repeat here consumes one character of a two-character subject. Locating it
needs a traced build, and the verdict above does not depend on it.

**Still to do for this row:** a permanent test in `tests/FuzzyRegex.Tests/Gaps/`, a
`docs/DIVERGENCES.md` entry, and a draft for the upstream-report ledger. They land with the rest of
the batch, so that the independent verifier sees all ten judged rows at once (spec amendment 34).

### Row 116428, part-judged: it is the Turkic folding family in a span-less operation

The whole of the difference is that upstream's `[A-Z]` reaches U+0131, dotless small i, under
IGNORECASE and this port's does not - the `turkic-default-folding` family that
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` already pins. Upstream-side ablations over
`(?r)\b(?:[\w\s]+(*SKIP)[A-Z]|ı)` on `'ııﬀﬀ'`, listing every match right to left:

```
row pattern, row flags (IGNORECASE|MULTILINE|FULLCASE)  (0, 2) 'ıı'
(*SKIP) removed                                         (0, 2) 'ıı'
\b removed                                              (0, 2) 'ıı'
the (*SKIP) arm alone, second arm removed               (0, 2) 'ıı'
the literal-ı arm alone                                 (0, 1) 'ı'
IGNORECASE alone                                        (0, 2) 'ıı'
no flags                                                (0, 1) 'ı'
```

So `(*SKIP)`, `\b`, MULTILINE and FULLCASE are all inert, and IGNORECASE is the whole variable.
With it, upstream's first arm matches two characters: `[\w\s]+` takes the ı at 0 and `[A-Z]` takes
the ı at 1. Without it, upstream falls back to the literal arm and gives the one-character match
this port gives. The subject needs both ı: `'ıﬀﬀ'` matches nothing at all.

**Why it reddened the gate rather than being tallied EXPECTED.** The entry it belongs to,
`turkic-default-folding-without-spans`, is keyed on an explicit list of rows, not on a predicate,
because a `sub` carries no spans for a span-keyed entry to read. The recorder's gate
(`tools/record-oracle.py:1656`, `_may_turn_on_a_turkic_rule`) admits this row - IGNORECASE is in
force and a `T` codepoint is in the subject - so its `scanMatches` are on the wave; it is the
consumer-side row list that has never seen it. A new row of a known family is reported rather than
classified by design: "a row it refuses is reported rather than classified, which is the safe
direction" (`:1646`).

**Still to do:** confirm through the port, not by inference, that the minimised pair gives
one-character-versus-two under IGNORECASE alone (an `-Rows` run), then add the row to
`_turkicWithoutSpansRows` with the judged answer on both sides, the way S52's ninth sitting added
rows 7 and 8.

### Row 74120, part-judged: the Turkic folding family again, this time in a split

Same shape as 116428 and the same one-variable test. The row is a reversed `split` under
IGNORECASE|VERSION1|FULLCASE over `'\rıı ﬁﬁ'`; upstream hands back two parts,
the port one. Upstream's own answers, with one flag changed at a time:

```
row flags                 [' ﬁﬁ', ''] - match span (0, 3)
IGNORECASE removed        ['\rıı ﬁﬁ']   the whole subject, one part
FULLCASE removed          [' ﬁﬁ', '']             unchanged
```

With IGNORECASE removed upstream gives the port's answer exactly, so the difference is the fold and
nothing else: upstream's `[\p{ASCII}&&\p{L}]` reaches U+0131 and this port's does not. FULLCASE and
the conditional groups are inert. `split` carries no spans either, so this row wants the same
treatment as 116428: a port-side `-Rows` confirmation, then the row list.

### Row 76484 judged: the Turkic folding family, and this one is confirmed on both sides

A reversed `sub` under IGNORECASE|VERSION1|FULLCASE over `'İİİ\r\nİßßß'`, with a
fuzzy section and a `(*SKIP)`: upstream makes two replacements, the port none. Three rows through
`tools/run-oracle.ps1 -Rows`, which records upstream and runs the port over the same pair - `agree 2
unsupported 0 diverge 1 of 3 rows`:

| Row | Change | Result |
| --- | --- | --- |
| 1 | the row as drawn | DIVERGE - upstream 2 replacements, port 0 |
| 2 | every U+0130 swapped for `A` | agree - both engines make the same replacement |
| 3 | IGNORECASE removed | agree - both engines match nothing at all |

So neither the fuzzy section nor the `(*SKIP)` nor the conditional is involved: the port makes the
same match as upstream the moment the letter is one whose fold it shares, and refuses every match
when the letter is U+0130. Upstream's own spans say the same - `[(5, 7), (0, 2)]` with the row's
flags, `[]` with IGNORECASE removed, `[(5, 7)]` with the letter swapped for `A` or `b`.

### Rows 74120 and 116428 now have the same two controls, and they hold

Six rows in one `-Rows` run - each of the two rows as drawn, with IGNORECASE removed, and with its
Turkic letter swapped for one whose fold the port shares (`b` for 116428, in the pattern's literal
arm as well as the subject; `g` for 74120). `diverge 2 of 6 rows`, and the two are rows 1 and 4, the
originals. Every control agrees:

```
row 1  116428 as drawn                    DIVERGE  upstream ' A\t>ﬀﬀ'   port ' A\t>ıﬀﬀ'
row 2  116428, IGNORECASE removed         agree
row 3  116428, ı swapped for b            agree
row 4  74120 as drawn                     DIVERGE  upstream [' ﬁﬁ', '']  port ['\rıı ﬁﬁ']
row 5  74120, IGNORECASE removed          agree
row 6  74120, ı swapped for g             agree
```

So all three rows are judged on both sides, and the fold is the only variable in any of them.

That makes four of the ten read and three of them the same known family, so the next sitting starts
by testing the remaining six for it: IGNORECASE off, then the letter swapped, and see whether
upstream moves to this port's answer. Only three of the ten rows carry IGNORECASE at all, and
76484 was one of them - the other two are 73420, whose subject is astral with no Turkic character in
it, and 76118 over the subject `' A'`, so neither is likely to fall to this test.
