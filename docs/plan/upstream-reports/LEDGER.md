# Upstream defect ledger

**Nothing here is filed.** Owner decision, 2026-09-12: no report goes to `mrabarnett/mrab-regex` until
absolutely everything else in the plan is done, and then only with the owner's approval of the text.
Until then this file is a *ledger* of defects identified in this port's work that would need fixing
upstream - kept correct, re-verified against each newer upstream release at every sync, never queued.
`gh` is deliberately outside the driver's allowlist so an unattended session cannot post.

Each entry keeps what a report would need so it can be sent later without re-deriving anything: a
minimal runnable reproduction pinned to a version, the faulting function, the port's answer and why
it is right, a second engine's actual answer where one exists, and a proposed fix. Entries are in the
shape the maintainer acts on fastest (measured 2026-08-31: #607 and #608 fixed the same evening).

**Verified against 2026.9.10 on 2026-09-12** (`tools/probes/`, venv `.venvs/regex-2026.9.10`): every
entry below reproduces unchanged on the newest release except where an entry says otherwise. Issue
614's fix changed only the reversed group-call span (entry 7's case G analogue: now `(3, 6)`, the
port's answer) and did **not** fix the `(?<=(?&a))c` row S30 pinned.

Original drafting notes: written by S33 and S34 out of `docs/plan/2026-09-12-divergence-research.md`
against `regex` 2026.7.19 (submodule `1760a20647f1c2ddcc025128407fe6f7edb905a1`), Windows 11,
CPython 3.13/3.14; PCRE2 10.47 via `tools/probes/pcre2-partial-and-skip.py`. Line numbers are
`upstream/src/_regex.c` at that commit.

---

## 1. `(*SKIP)` retries at a position below the one it committed past

**Title:** `(*SKIP)` can match at a position earlier than the one it skipped to

**Body:**

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> regex.compile(r'..(*SKIP)xx').search('cd xxx')
<regex.Match object; span=(1, 5), match='d xx'>
```

The expected answer is `(2, 6)`. At start position 0 the pattern consumes `cd`, reaches `(*SKIP)`,
and then fails on `xx` against ` x`. `(*SKIP)` means the next attempt starts at the position the verb
was reached at, which is 2 - and an attempt at 2 succeeds:

```python
>>> regex.compile(r'..(*SKIP)xx').match('cd xxx', 2)
<regex.Match object; span=(2, 6), match=' xxx'>
```

Instead the search answers `(1, 5)`, which is an attempt at position 1 - below the position the verb
had already committed past.

PCRE2 10.47 answers `(2, 6)`, with its start optimisations on and with `PCRE2_NO_START_OPTIMIZE`,
and `pcre2pattern` states the rule the answer follows: on failure the bumpalong goes to the position
where `(*SKIP)` was encountered, and a retry below that is not permitted.

**Where it comes from - shown, not argued.** It is `locate_required_string` (`:11082`), which moves
the *first* attempt to `found_pos - req_offset`. `_regex.compile` is handed `req_offset=2,
req_chars=(120, 120)` for this pattern; neutralise exactly those two arguments and the same build
answers correctly:

```python
>>> inner = regex._regex.compile
>>> def nofilter(*a):
...     a = list(a); a[7] = -1; a[8] = None; return inner(*a)
...
>>> regex._regex.compile = nofilter
>>> c = regex.compile(r'..(*SKIP)xx', cache_pattern=False)
>>> regex._regex.compile = inner
>>> c.search('cd xxx')
<regex.Match object; span=(2, 6), match=' xxx'>
```

So the first attempt starting somewhere other than 0 is what leaves a start position below the verb's
commit reachable afterwards; a pattern with no required string never shows it.

**Proposed fix.** Whichever way the two are ordered, no start position below the one `(*SKIP)` moved
`slice_start` to may be tried. Clamping the next start position to `slice_start` after a verb has
moved it would do that, and it is the bound `basic_match`'s own slow path already honours - the
prefilter-free run above is that path answering.

**Not the same as** the family of `(*SKIP)` differences PCRE2 documents under "Optimizations that
affect backtracking verbs", where turning the optimiser off changes the answer. This one is the other
way round: PCRE2 gives the same answer with the optimiser on and off, and it is `regex` that differs
from both of PCRE2's.

---

## 2. A bounded lazy repeat at its maximum reports a partial match that cannot be completed

**Title:** `partial=True` reports a partial match for a subject that cannot be extended into one

**Body:**

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> regex.compile('ba??x').match('baa', partial=True)
<regex.Match object; span=(0, 3), match='baa', partial=True>
```

`ba??x` matches `bx` or `bax` and nothing else, so no string beginning `baa` can ever match it. The
README defines a partial match as one "that matches up to the end of string, but that string has been
truncated and you want to know whether a complete match could be possible if the string had not been
truncated", and no continuation of `baa` completes this pattern.

The greedy twin answers correctly, which is what isolates the lazy path:

```python
>>> regex.compile('ba?x').match('baa', partial=True)
None
```

So does a bound of two against a three-`a` subject, and the difference between these two lines is the
clearest statement of the bug - the repeat reports a partial exactly when the subject reaches its
maximum and one character more:

```python
>>> regex.compile('ba{0,2}?x').match('baaa', partial=True)
<regex.Match object; span=(0, 4), match='baaa', partial=True>
>>> regex.compile('ba??x').match('baaa', partial=True)
None
```

PCRE2 10.47 answers no match for `ba??x` against `baa`, soft and hard, anchored and unanchored, while
answering a partial for `ba??x` against `ba` and for `ba*?x` against `baa` - so it distinguishes the
extendable cases from this one.

**Where it comes from.** The specialised `LAZY_REPEAT_ONE` backtrack arms (`:16546`, `:16583`,
`:16621`, `:16659` for a `CHARACTER` tail and `:16699`, `:16754`, `:16809`, `:16868`, `:16925`,
`:16982` for a `STRING` tail) each return to the top of their loop and test `partial_side` before
their own limit check, and each also caps `limit` per tail op - `min(limit, slice_end - 1)` for a
`CHARACTER` tail at `:16542`. When the repeat has reached its maximum the extension loop is entered
with nothing left to extend, and the partial arm fires on the position the cap left behind rather
than on a position the pattern could actually have consumed.

**Proposed fix.** Take the partial only when the repeat could still have grown - that is, when
`rp_data->count < max_count` - which is the condition the equivalent guards elsewhere in
`basic_match` already carry (compare `:5005`, `count == (text_end - text_pos) && count < max_count
&& state->partial_side == ...`).

**Not the same as** the two partial-matching bugs fixed in 2024.7.24 and 2024.11.6 (#539, #546),
though #546 was in the same lazy-quantifier neighbourhood: both of those were partials that were
*missed*, and this is a partial that is reported where none exists.

---

## 3. A reversed search reports a partial that the same pattern's `match` and `fullmatch` deny

**Title:** `(?r)\b$` reports a partial match on the empty string from `search` but not from `match`

**Body:**

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> c = regex.compile(r'(?r)\b$')
>>> c.search('', partial=True)
<regex.Match object; span=(0, 0), match='', partial=True>
>>> c.match('', partial=True)
None
>>> c.fullmatch('', partial=True)
None
```

Three doors on to the same question at the same position, and one of them disagrees with the other
two. The forward twin agrees with `match` and `fullmatch`:

```python
>>> regex.compile(r'\b$').search('', partial=True)
None
```

`(?r)\m$`, `(?r)a\b$` and `(?r)([abz]{1})\b$` all behave the same way; `(?r)$`, which has no boundary
and so no `search_start_*` start test, agrees on all three doors.

Which of the two answers is right depends on a rule this report does not try to settle: by the model
in #589, `\b` is evaluated against the real string, the empty string contains no word character, and
there is no boundary at 0 to be partial about. What is a defect either way is that the three doors
answer differently.

**Where it comes from.** `search_start` (`:8385`) is taken whenever the start test has a
`search_start_*` twin (`:11819`), and every one of those scanners can report `RE_ERROR_PARTIAL` of its
own (`:8662`-`:8960`). `basic_match`'s slow path has no such arm, and `match` and `fullmatch` never
consult `search_start` at all, so the fast path is answering a question the slow path is not being
asked.

**Proposed fix.** Make the `search_start_*` partial arms agree with whatever the slow path would
answer at the same position - or, if the fast path's answer is the intended one, give the slow path
the same arm so `match` and `fullmatch` answer it too.

**Related:** #589, which is open and is about the same `\b`-under-partial question from the language
side rather than from the two-doors side. This may belong there as a comment.

---

## 4. A reversed `fullmatch` of a repeat fails on a slice that is exactly the match

**Title:** `(?r)` `fullmatch` with `pos > 0` fails when the pattern contains a general repeat

**Body:**

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> regex.compile(r'(?r)(ab)+').fullmatch('xabz', 1, 3)
None
```

The slice is exactly `ab`, and the pattern matches `ab`. Every neighbouring case answers:

```python
>>> regex.compile(r'(?r)(ab)+').match('xabz', 1, 3)      # same slice, not a fullmatch
<regex.Match object; span=(1, 3), match='ab'>
>>> regex.compile(r'(?r)(ab)+').fullmatch('abz', 0, 2)   # same slice text, pos 0
<regex.Match object; span=(0, 2), match='ab'>
>>> regex.compile(r'(ab)+').fullmatch('xabz', 1, 3)      # same slice, forward
<regex.Match object; span=(1, 3), match='ab'>
>>> regex.compile(r'(?r)a+').fullmatch('xaaz', 1, 3)     # a *_ONE repeat rather than a general one
<regex.Match object; span=(1, 3), match='aa'>
```

So it needs all four at once: `(?r)`, `fullmatch`, `pos > 0`, and a repeat that compiles to
`GREEDY_REPEAT`/`LAZY_REPEAT` rather than to the `*_ONE` form. `(?r)(ab)*`, `(?r)(ab)+?` and
`(?r)(a)+` fail the same way; `(?r)ab`, `(?r)(ab)`, `(?r)(?:ab)` and `(?r)(ab){1}` do not.

**Where it comes from.** There are three `match_all` checks and they do not agree:

| site | bound |
|---|---|
| `try_match`, `RE_OP_SUCCESS` arm, `:7832` | `text_pos > state->text_start` |
| `basic_match`, `RE_OP_SUCCESS` opcode, `:15167` | `text_pos != state->slice_start` |
| `basic_match`, after a successful match, `:11880` | `text_pos == state->slice_start` |

`do_match` sets `text_start` to 0 and `slice_start` to `pos` (`:18435`), so the first disagrees with
the other two whenever `pos > 0`. `GREEDY_REPEAT` and `LAZY_REPEAT` are the only constructs that ask
`try_match` whether their tail could match (`:13237` and `:13617`, each via `node->nonstring.next_2`),
so they are told the match may not end where the slice ends, `try_tail` is false, and the whole
attempt gives up.

**The same defect has a second symptom**, where the match is not lost but is reported as partial:

```python
>>> c = regex.compile(r'(?r)([\p{L}\p{N}])??', regex.F | regex.I)
>>> c.fullmatch('AAB', 2, 2, partial=True)
<regex.Match object; span=(2, 2), match='', partial=True>
>>> c.match('AAB', 2, 2, partial=True)
<regex.Match object; span=(2, 2), match=''>
>>> c.search('AAB', 2, 2, partial=True)
<regex.Match object; span=(2, 2), match=''>
```

`try_match` refuses the tail for the reason above, the repeat's body answers `RE_ERROR_PARTIAL` from
its own `try_match_STRING_FLD_REV` at `slice_start`, and `LAZY_REPEAT` returns that partial
(`:13632`) instead of the complete zero-width match its slow path finds - which is what the other two
doors return.

**Proposed fix.** Change `:7832` from

```c
if (text_pos > state->text_start)
```

to

```c
if (text_pos > state->slice_start)
```

matching `:15167` and `:11880`. The forward half of the same arm (`text_pos < state->text_end`) needs
no change, because `do_match` sets `text_end` and `slice_end` to the same `endpos`.

**No second opinion available:** PCRE2, Perl and Boost have no reverse matching, so there is nothing
to compare against. What settles it is `regex` disagreeing with itself - `match` on the same slice
answers `(1, 3)`, and `fullmatch` on the same slice text at `pos=0` answers `(0, 2)`.

---

# Added by S34, 2026-09-12

Two more to file and two that need no report. Same rules as above: nothing goes upstream until the
owner has approved the text. Everything below was run the same day, against the same
`regex` 2026.7.19 from PyPI.

**Read this first.** S34 fetched upstream's history rather than working from the pinned submodule,
and upstream has moved five releases past our pin. Two of the divergences S33 left for this slice
turn out to be bugs the maintainer has **already fixed**, so reporting them would waste his time:

- **Issue 614**, `build_GROUP()` does not propagate the match direction, fixed 2026-08-30 by commit
  `9398a6d` (one line, `subargs.forward = forward;`) and released in 2026.8.30. This is the reversed
  group call that records a capture whose end precedes its start. Nothing to file.
- **Issue 613**, `(*SKIP)` inside an atomic group plus an equality-only scan stop, fixed the same day
  by commit `b77694a` and released in 2026.8.30. Its second half clamps `GREEDY_REPEAT_ONE`'s
  backtrack limit down to the current position, which is one consequence of the stale slice issue 5
  below is about - so **issue 5 must be re-checked against 2026.9.10 before it is filed**. That check
  needs the newer wheel, which this session could not install; the Phase 6 sync is where it happens.

---

## 5. An overlapped scan with `(*SKIP)` contradicts the same pattern's own `match`

**HOLD lifted 2026-09-12:** re-run against 2026.9.10, identical output, so issue 613's fix does not cover it.

**Title:** overlapped `finditer` with `(*SKIP)` returns a match shorter than the pattern's minimum
width

**Body:**

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> [m.span() for m in regex.compile(r'(?:[^\d](*SKIP)){2}').finditer('abcde', overlapped=True)]
[(0, 2), (1, 3), (2, 3), (3, 5)]
```

`(2, 3)` is a one-character match of a pattern that must match two. The same compiled pattern's own
`match` at that position disagrees with its scanner:

```python
>>> regex.compile(r'(?:[^\d](*SKIP)){2}').match('abcde', 2)
<regex.Match object; span=(2, 4), match='cd'>
```

A longer form shows the same thing across a whole scan, and also shows that the answer is not
stable:

```python
>>> p, s = r'(?:[^\d](*SKIP)){2,3}', '\r\naabb '
>>> [m.span() for m in regex.compile(p, regex.M).finditer(s, overlapped=True)]
[(0, 3), (1, 4), (2, 4), (3, 4), (4, 7), (5, 7)]
>>> import gc
>>> out = []
>>> for m in regex.compile(p, regex.M).finditer(s, overlapped=True):
...     out.append(m.span()); gc.collect()
>>> out
[(0, 3), (3, 6), (4, 7), (5, 7)]
```

Three different answers come out of the same call according to what runs between iterations - an
`open()` in the loop gives `[(0, 3), (3, 6)]` - while the pattern's own `match` at positions 0 to 5
gives `(0,3) (1,4) (2,5) (3,6) (4,7) (5,7)` every time.

**Where it comes from - shown, not argued.** `RE_OP_SKIP` (`:14553`) assigns
`state->slice_start`/`slice_end` and nothing restores them: `init_match` (`:3404`) resets the stacks,
the groups and the guards but not the slice, `do_match` (`:18121`) does not either, and
`scanner_search_or_match` (`:20874`) keeps one `RE_State` for the whole scan. The only other writer
is `state_init` (`:18438`), once per scanner. So the slice a `(*SKIP)` moved in match *n* is still
moved for match *n+1* - and the overlapped branch (`:20903`) then sets
`state->text_pos = state->match_pos + 1`, which can be **below** `slice_start`, a relation no other
path produces on a forward match.

**Proposed fix.** Save `slice_start`/`slice_end` in `scanner_search_or_match` before `do_match` and
restore them after, the way `do_best_fuzzy_match` (`:17899`, `:17994`) already does around its own
loop; or reset them in `init_match` alongside the guards. The `GREEDY_REPEAT_ONE` clamp added by
`b77694a` fixes one place the stale slice is read, and this would fix the class.

**The reversed half, added by S35 on 2026-09-12, and it is the sharpest version of this report.**
Under `(?r)` the verb moves `slice_end` instead (`:14545`) and the same carry-over moves every later
span to the right. Two rows of the S29 `verbs` wave, seed 20260913:

```python
>>> pat = r'(?r)(?:\p{L}+(*SKIP)\w|A)(?P<g1>(?:[a-f]{1,3}?(*SKIP)A|[\w\s]))'
>>> [(m.span(), m.span('g1')) for m in regex.compile(pat).finditer('AAAA00', overlapped=True)]
[((0, 6), (5, 6)), ((0, 5), (5, 6))]
>>> regex.compile(pat).search('AAAA00', 0, 5)      # its own single-shot door
<regex.Match object; span=(0, 5), match='AAAA0'>   # ... with g1 at (4, 5)
```

The second match spans `(0, 5)` and reports `g1` at `(5, 6)` - **outside the match it belongs to**,
in a pattern with no lookaround that could put a capture there - where the same pattern's own
`search` over the same slice puts it at `(4, 5)`.

**And it is memory-unsafe, not merely wrong.** Printing each match as it arrives segfaults the
interpreter, where the quiet list comprehension above completes:

```
$ python tools/probes/upstream-reversed-overlapped-skip.py
regex 2026.7.19
overlapped scan: [((0, 6), (5, 6)), ((0, 5), (5, 6))]
   match (0, 5) capture (5, 6) inside: False
search(0, 5): ((0, 5), (4, 5))

$ python tools/probes/upstream-reversed-overlapped-skip.py --crash
   (0, 6) (5, 6)
Segmentation fault (exit 139)
```

That is the same instability as the `gc.collect()` above, at the next level of severity, and it puts
this report in the same class as the 2026 fuzzing fixes 611-614.

**Second opinion.** PCRE2 10.47 has no overlapped scan, so the comparison here is `regex` against
itself: its scanner against its own `match` at the same positions, against its own answer with a
`gc.collect()` in the loop, and - reversed - against its own `search` over the same slice.

**Verified against 2026.9.10 on 2026-09-12:** the reversed rows reproduce span for span, `g1`
included.

**A third symptom, added by S36 on 2026-09-12: the carried slice does not only move a span, it
invents whole matches.** Three rows of a 2000-row `verbs` wave (seeds 4242 and 7) have upstream's
reversed overlapped scan reporting matches this port does not, and each one refutes itself.

*The assertion form*, minimised to three characters:

```python
>>> [m.span() for m in regex.finditer(r'(?r)(?:.{2}(*SKIP)A|x)$', 'bxA', regex.M, overlapped=True)]
[(0, 3), (1, 2)]
```

`(1, 2)` needs `$` to hold at index 2 of `'bxA'`, where the subject has an `A`. Ask upstream whether
it can, with no verb in the pattern, and it says no - `regex.finditer(r'(?r)x$', 'bxA', regex.M,
overlapped=True)` finds nothing there. The controls isolate the verb: the same pattern with the
`(*SKIP)` deleted, and with it replaced by `(*PRUNE)`, both give `[(0, 3)]`. So the extra match is
`$` reading the `slice_end` the verb moved to 2, which is this report's mechanism seen through an
assertion rather than through a span.

*The capture form*, which is the reversed twin of the `(5, 6)` capture above:

```python
>>> p = r'(?r)([^a]{2,4}(*SKIP)[a\d])((?:[^\d]++(*SKIP)\s|\ ))'
>>> [(m.span(), m.span(2)) for m in regex.compile(p).finditer('b0 0\n A', overlapped=True)]
[((0, 6), (4, 6)), ((0, 5), (4, 6))]
>>> regex.compile(p).search('b0 0\n A', 0, 5), regex.compile(p).match('b0 0\n A', 0, 5)
(None, None)
```

The second match ends at 5 and carries a capture ending at 6, and upstream's own doors over that
slice deny the match outright.

Both rows need the required-string prefilter neutralised to reproduce - the oracle records `verbs`
that way (`tools/record-oracle.py:223`) - which is worth stating in the report, because a reader who
pastes them into a plain interpreter will see fewer matches and conclude the report is wrong.

**Verified 2026-09-12** against `regex` 2026.7.19; pinned in this port by
`BacktrackingVerbTests.An_overlapped_reversed_scan_of_a_skip_stops_where_upstreams_own_extra_matches_refute_themselves`
and classified by `ExpectedDivergences.overlapped-skip-extra-match-reversed`.

**A fourth symptom, added by S40d on 2026-09-13, and the one that completes the set: the carried
slice also LOSES a match.** Where the third symptom has the moved `slice_end` too far right, here it
is too far left, so upstream's next attempt runs in a view of the subject that cannot hold the match
and its scan stops one short. Found at seed 20260914, `verbs`, 6000 rows - a seed no slice had used.

```python
>>> p, s = r'(?r)\p{Lu}*(*SKIP)B(?P<g1>(?:\D{2,4}(*SKIP)a|.))', 'B_\ra'
>>> [m.span() for m in regex.compile(p).finditer(s, overlapped=True)]   # prefilter-free
[(0, 4)]
>>> regex.compile(p).search(s, 0, 4), regex.compile(p).search(s, 0, 3)
((0, 4), (0, 2))
```

Upstream's own reversed search, asked one match at a time from a fresh state, finds `(0, 2)` at
`endpos` 3 - the very position its overlapped scan steps to next, since a reversed overlapped scan
resumes at `match_pos - 1` (`:20903`). The controls isolate the verb again: both verbs made
`(*PRUNE)`, and both deleted, give upstream `[(0, 4), (0, 2)]`.

The walk is a legitimate question for THIS pattern because it holds no `$`, `\Z`, `\b`, `\B`, `\m`,
`\M` or lookahead, so truncating the subject with `endpos` changes the meaning of nothing in it.

**Verified 2026-09-13** against `regex` 2026.7.19; pinned by
`BacktrackingVerbTests.An_overlapped_reversed_scan_of_a_skip_keeps_the_match_upstreams_own_stepwise_door_still_finds`
and classified by `ExpectedDivergences.overlapped-skip-missing-match-reversed`. Re-runnable:
`python tools/probes/upstream-reversed-skip-scan-shapes.py`.

---

## 6. `IndexError` out of `regex.compile` on a reversed, case-folded pattern

**Title:** `IndexError: tuple index out of range` compiling `(?r)^İﬁ` with `I|F`

**Body:**

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> regex.compile('(?r)^İﬁ', regex.I | regex.F)
Traceback (most recent call last):
  ...
  File ".../regex/_main.py", line 646, in _compile
    fs_code = _compile_firstset(info, parsed.get_firstset(reverse))
  File ".../regex/_regex_core.py", line 4036, in get_firstset
    return set([Character(self.characters[pos],
IndexError: tuple index out of range
```

Each part is needed: dropping `(?r)`, dropping `^`, dropping `F`, or replacing either character with
an ASCII one compiles cleanly. Both characters are ones whose full case folding is longer than one
character - `U+0130` folds to `i` plus a combining dot, `U+FB01` to `fi`.

**Where it comes from - found by S35, 2026-09-12, and it is not only a crash.**
`Sequence._fix_full_casefold` (`_regex_core.py:3636`) finds its chunks in the **folded** text
(`:3643`) and then slices the **unfolded** `characters` tuple with those offsets (`:3661`, `:3666`).
The two are the same length only while nothing in the run expands, and finding what expands is the
whole job of the function, so every expansion shifts every later offset right. With one expansion it
works out; with two it does not.

`İﬁ` is the crashing shape. `characters` is two long, the folded text `i̇fi` is four, `fi` is found at
2, and `characters[2:4]` is empty - so `_flush_characters` (`:3627`) builds a zero-length `String`,
and `String.get_firstset` (`:4036`) then indexes `characters[0]` on it. `(?r)` puts that node first
in the first-set walk and `^` is what makes the walk happen at all
(`_main.py:643`, `if not parsed.has_simple_start()`), which is why all four parts are needed.

**The same arithmetic answers wrongly without crashing, which is the more serious half:**

```python
>>> regex.compile('ﬁaﬁ', regex.I | regex.F).fullmatch('fiafi')   # None
>>> regex.compile('ﬀaﬃ', regex.I | regex.F).fullmatch('ffaffi')  # None
>>> 'ﬁaﬁ'.casefold() == 'fiafi'.casefold()
True
```

The second ligature lands in the simple-`IGNORECASE` chunk, so it never matches its own expansion.
`ﬁa`, `aﬁ` and `ﬁﬁ` are all correct, which is what isolates the second expansion as the trigger.

**Proposed fix.** Record where each character's fold begins and move each chunk's start back to the
character containing it, so the slice indices are in character space. Both the folding and the
`.lower()` are per-codepoint, so the per-character folds concatenate to exactly the whole-run fold
this function already computes. The chunk's *end* can be left alone: it drifts the same way, so it
only ever takes in trailing characters that did not need the full fold, and a character whose full
fold differs from its simple fold is by definition one that expands and has a chunk of its own.
Guarding `String.get_firstset` against an empty node would stop the traceback and leave the wrong
answers.

**Why it is worth filing even though the first symptom only raises.** It is reachable from
`regex.compile` on a user-supplied pattern, and the exception is an `IndexError` rather than a
`regex.error`, so a caller that guards against bad patterns the documented way does not catch it -
and the silent wrong answers need no anchor and no `(?r)` at all.

**Verified against 2026.9.10 on 2026-09-12:** both the traceback and both wrong answers reproduce
unchanged.

---

## 7. Full case folding never applies to `U+0130`, because the expansion list is not lower-cased

**Title:** `İ` does not match `i̇` under `FULLCASE | IGNORECASE`

**Body:**

```python
>>> import regex
>>> regex.compile('İ', regex.I | regex.F).fullmatch('i̇')
None
>>> 'İ'.casefold() == 'i̇'.casefold()
True
```

Every other expanding character matches its expansion: `ß`/`ss`, `ﬁ`/`fi`, `ﬃ`/`ffi` all do.
`U+0130` is in `_regex.get_expand_on_folding()`, so the module knows it expands.

**Where it comes from.** `Sequence._fix_full_casefold` builds its inventory of expansions with
`fold_case` alone (`:3639`) and then looks for them in a text that has been through
`fold_case(...).lower()` (`:3643`). `U+0130` is the one character the two disagree about:

```python
>>> _regex.fold_case(FULL_CASE_FOLDING, 'İ')            # 'İ' - unchanged
>>> _regex.fold_case(FULL_CASE_FOLDING, 'İ').lower()    # 'i̇'
```

So its expansion is never found in the folded text, no chunk is ever marked for it, and the
character compiles to `CHARACTER_IGN` instead of reaching the full fold at all.

**Proposed fix.** Lower-case the inventory the same way the text is lower-cased - `[_regex.fold_case(
FULL_CASE_FOLDING, c).lower() for c in _regex.get_expand_on_folding()]`. That alone is not enough:
the matcher's `STRING_FLD` folds the subject with the same `fold_case`, so `U+0130` would have to
expand there too, which is a change to the folding table rather than to this function. The
maintainer's call is whether `U+0130` is excluded from full folding on purpose - CaseFolding.txt
gives it an `F` mapping of `0069 0307` and a Turkic-only `T` mapping of `0069`, and no `C` or `S`
mapping at all.

**Not fixed in this port either, and deliberately** - see `Sequence.FixFullCasefold`'s remarks in
`src/FuzzyRegex/Parsing/Nodes.cs`. This port follows upstream's folding tables, so `İ` behaves the
same way here; the half-fix would make the parser and the matcher disagree with each other.

**Where it goes, decided at the Phase 4 close (S36, 2026-09-12).** It is an inherited bug, and the
owner's rule is that every conclusively identified bug is fixed here before 1.0, inherited or not. It
needs the folding tables changed rather than the parser, so it is a slice of its own in **Phase 6's
opening sweep** - the first item of the sweep's third slice, recorded in ROADMAP. It is the only
ledger entry with a fix scheduled in this port; the other six are upstream's to fix and stay pinned
as divergences until they do.

---

## 8. A group call inside a lookaround that runs the other way loses the match, even on a path that never enters the call

**Title:** A `(?&name)` call inside a lookaround of the opposite direction makes the whole pattern
fail, where deleting the optional piece that holds it succeeds

**Body:**

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> pat = r'\b(?(?![\w\s])[[:digit:]])(\w)(?P<g2>[^\d]{3})(?:(?(2)(?<!(?&g2))[a-f]|[^a]))*'
>>> flags = regex.I | regex.M | regex.V1 | regex.F
>>> [m.span() for m in regex.finditer(pat, 'İİ\nİİﬁﬁ ', flags, overlapped=True)]
[(0, 4)]
>>> shorter = r'\b(?(?![\w\s])[[:digit:]])(\w)(?P<g2>[^\d]{3})'
>>> [m.span() for m in regex.finditer(shorter, 'İİ\nİİﬁﬁ ', flags, overlapped=True)]
[(0, 4), (3, 7)]
```

The piece deleted between the two is `(?:...)*` - a repeat that can take **zero** iterations - so the
first pattern's language contains the second's, and it cannot have fewer matches. It has fewer.

The same thing happens with a `??` optional, a `?` optional, and under `(?r)` with a lookahead
instead of a lookbehind, which is the mirror arrangement:

```python
>>> regex.compile(r'(?r)\b(?P<g1>[A])(?:(?(1)(?=(?&g1))\S)){3}(\p{Nd}+?)?', regex.I | regex.M)
>>> [m.span() for m in _.finditer('AA..0', overlapped=True)]
[]
>>> [m.span() for m in regex.finditer(r'(?r)\b(?P<g1>[A])(\p{Nd}+?)?', 'AA..0', regex.I | regex.M,
...                                  overlapped=True)]
[(0, 1)]
```

Here the `{3}` is not optional, and it does not need to be: its body is a conditional with no
no-branch whose group is not yet set at that point, so each iteration matches empty. Upstream says so
itself - write the call out as the class it calls and the same pattern matches (0, 1), one character
for a `{3}` that would have to consume three if its body were not empty:

```python
>>> [m.span() for m in regex.finditer(r'(?r)\b(?P<g1>[A])(?:(?(1)(?=[A])\S)){3}(\p{Nd}+?)?',
...                                   'AA..0', regex.I | regex.M, overlapped=True)]
[(0, 1)]
```

Five rows of a 6000-row generated wave show it, in four different shapes - a `finditer` that finds
nothing, an overlapped scan one match short, a `subf` that replaces nothing and a `split` that does
not split. All five are re-runnable:
`python tools/probes/upstream-group-call-loses-matches.py`.

**Where it comes from.** The same area as #614: `build_GROUP()` and the direction a called group's
body is compiled with. That issue was fixed on 2026-08-30 by `9398a6d`
(`subargs.forward = forward;`) and released in 2026.8.30. **This is not fixed by it.** All five rows
were replayed against 2026.9.10 on 2026-09-12 and answer exactly as 2026.7.19 does, as does the row
S30 minimised by hand:

```python
>>> regex.compile(r'(?(DEFINE)(?<a>a))(?<=(?&a))c').match('ac', pos=1)   # None, on 2026.9.10 too
```

**What this port answers.** The match, in every case - the one the shorter pattern finds. Pinned by
`GroupCallTests.A_group_called_from_a_lookbehind_with_anything_after_it_matches_here_and_not_upstream`
and `.A_zero_width_piece_holding_a_group_call_cannot_remove_a_match_here`, and classified in the
oracle as `group-call-loses-the-match`.

**What is NOT established, and the report must say so.** Shrinking these rows while keeping upstream
self-contradictory produces patterns on which *this port answers what upstream answers* -
`(?P<g1>a)((?<!(?&g1)))*` over `'a'`, `(?r)(?P<g1>[A])((?(?=(?&g1))S))` over `'A'`. So "a call
through an opposite-direction lookaround" is not on its own sufficient for the divergence, and what
else the longer shapes supply is unknown. The reproduction above stands on its own - it needs no
second engine, only upstream's answer to a pattern and to the same pattern with a zero-width-capable
piece removed - but a report that claimed the minimal form would be wrong.

**Proposed fix.** Unknown, beyond "the same area as #614". Establishing it needs the minimal form,
which is open work.

**Related:** #614 (fixed, and does not cover this).

# Added by S38, 2026-09-13

## 9. A POSIX search of a fuzzy pattern crashes the C engine

**Status:** not filed. Nothing is filed until everything else in the plan is done (owner decision,
2026-09-12); this entry is drafted here and re-verified against the then-current release first.

**Reproduction**, on `regex` 2026.7.19 (CPython 3.14, Windows), measured 2026-09-13:

```python
>>> import regex
>>> regex.search(r'(?p)(?:[ab][bc]){e<=1}', 'ax')
Segmentation fault
```

The process dies; there is no exception to catch. `python tools/probes/upstream-posix-fuzzy-crash.py`
re-runs it, and cases 4 and 5 of that probe are the two controls that isolate it:

```
'(?:[ab][bc]){e<=1}' on 'ax'   -> ((0, 2), (1, 0, 0), ([1], [], []))   # no (?p): fine
'(?p)(?:[ab][bc])'   on 'ab'   -> ((0, 2), (0, 0, 0), ([], [], []))    # no fuzzy: fine
```

So it needs BOTH the POSIX flag and a fuzzy section. `match` is not affected, only `search` (and
`finditer`, which searches).

**Where it comes from, at the precision the evidence supports.** `save_best_match` (`:11493`) and
`restore_best_match` (`:11565`) copy `state->best_fuzzy_counts` alongside the groups, and
`check_posix_match` (`:11602`) is what drives them. `do_simple_fuzzy_match` (`:18027`) sets
`state->max_errors` to `PY_SSIZE_T_MAX` and does not touch the best-match fields that
`do_exact_match` leaves alone too. Which of those is the faulting access has NOT been established -
no debugger was attached and no ASAN build was made - so a report must either establish it or say
it does not know. It is the same family as the four 2026 memory-safety fixes (issues 611-614) and
plausibly the same fuzzing campaign would have found it.

**What this port answers.** The row, without crashing. Its *counts* are not yet trustworthy there:
`best_fuzzy_counts` is deliberately unported until S42, so a POSIX fuzzy match reports the counts of
whichever attempt ran last rather than of the best one. That is a known gap with a slice against it,
not a divergence - and it cannot be pinned as one either way, because upstream cannot be asked.
Pinned only as "does not crash" by
`FuzzyMatchingTests.A_POSIX_search_of_a_fuzzy_pattern_answers_where_upstream_crashes`.

**Consequence for the oracle.** The `fuzzy` generator draws no `(?p)`, and must not until this is
fixed upstream: a recorder row that kills the interpreter takes the whole wave with it. Noted here
rather than only in the generator so the next slice that widens it knows why.

**Proposed fix.** Unknown. Establishing it needs a debug build of the C extension, which this
project has deliberately not set up (design spec amendment 7: releases and PyPI wheels only).

**Related:** issues 611-614, the 2026 memory-safety group.

# Added by S40a, 2026-09-13

## 10. `(*SKIP)` inside an atomic group after an optional item loops for ever - ALREADY FIXED UPSTREAM

**Status: nothing to file, and nothing to fix here.** Recorded so the next session does not
re-derive it, and because it is the defect that forced the recorder's per-row deadline.

Found by S40 at 6000 rows a generator - `verbs` row 5944, seed 4242 - where it killed the whole wave
silently: `tools/record-oracle.py` waited for ever, so no file was written at all, no error was
raised, and about forty minutes went on bisecting it by hand. Minimised to four characters:

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> regex.compile('.?x(?>a(*SKIP)z)').search('xzxa')   # never returns
```

All three parts are needed. `(*PRUNE)` in place of `(*SKIP)`, a non-atomic `(?:...)`, and dropping
the leading `.?` each answer `None` at once. The original row was
`[^a]?\U0001f600(?>[a\d]{1,3}(*SKIP)\p{Ll})` on `'\U00010400\r_\U0001f600\xdf\U0001f600aA'`.

**Upstream has already fixed it**, which is why this is a ledger note and not a report. Measured
2026-09-13 with `tools/probes/upstream-skip-in-atomic-hang.py`:

| release | the minimised row | the 1296-call grid (`--grid`) |
|---|---|---|
| 2026.7.19 (our pin's era) | never returns | 70 calls hang |
| 2026.9.10 (newest) | `None` | 0 calls hang |
| this port | `None`, in 17ms | 0 calls hang |

The fix is commit `b77694a`, "Git issue 613: `(*SKIP)` inside an atomic group, plus an equality-only
scan stop", released in 2026.8.30 - the same commit the note above entry 5 already flags. It adds
one clamp per direction to the `GREEDY_REPEAT_ONE` backtrack arm (`:15859`), holding the retreat
limit down to the current position. Without it a `(*SKIP)` that has moved `slice_start` above the
position the retreat starts from leaves the arm's `pos == limit` stop unreachable, and the retreat
loop runs away. It is the only engine commit between `b77694a` and upstream's head that touches this
construct at all - the rest of that window is `9398a6d` (group-call direction, entry 8's area) and
the #615-#618 Python-API error-propagation PRs.

**What this port answers, and the honest limit on it.** `None`, on the row and on all 1296 grid
calls. But this port still carries the PRE-FIX arm - `Matcher.cs`'s `GreedyRepeatOne` backtrack has
upstream's slice clamp and not the `pos` clamp - so "does not hang on these rows" is not "cannot
hang", and the grid is what makes even the first claim worth anything: a grid that never reached
the shape would give this port the same zero, and upstream's 70 is the proof that it does reach it.
Pinned by
`BacktrackingVerbTests.A_skip_inside_an_atomic_group_after_an_optional_item_answers_where_upstream_loops_for_ever`,
whose assertions are bounded by a `MatchTimeout` so a regression fails one test instead of hanging
the suite.

**Where the two clamps get ported: the Phase 6 sync**, test-first, as ROADMAP and entry 5's note
already say. This entry adds the reproduction they lacked.

**Consequence for the oracle, and the reason it stays true after the sync.** The recorder now gives
every upstream call a ten-second deadline and records a row it misses as a `timeout` outcome the
consumer skips and counts (S40a, DECISIONS 2026-09-13). That machinery is not this bug's workaround
and does not retire with it: upstream had two other runaway shapes on the open tracker when this was
written (issues 551 and 554), and a generative tester that one hanging row can silence is a tester
that goes quiet without saying so.

---

## 11. A fuzzy match reports change positions that contradict its own change counts

**Status: not filed, and INHERITED BY THIS PORT.** Upstream's answer contradicts itself, this port
reproduces it faithfully, and the owner's rule (2026-09-12) is that an inherited bug is fixed here
before 1.0 - so this is an item for Phase 6's inherited-bug sweep, alongside entry 7, and not a
divergence to pin.

**Reproduction**, `regex` 2026.7.19, measured 2026-09-13 and re-run unchanged on 2026.9.10
(`python tools/probes/upstream-fuzzy-restart-leak.py`):

```python
>>> import regex
>>> m = regex.search(r'(?:[ab][bc](*PRUNE)[wx]){e<=2}', 'qab')
>>> m.fuzzy_counts, m.fuzzy_changes
((0, 0, 1), ([0], [], []))
```

One **deletion** counted, one **substitution** reported. The two attributes are documented as two
views of the same errors, so whichever is right the pair cannot both be, and no caller can tell which
to trust. `(?:[ab](*SKIP)[bc][wx]){e<=2}` over the same subject answers identically.

**Where it comes from.** `basic_match`'s `start_match` clears the counts and not the change list:

```c
/* Clear the fuzzy counts. */
if (state->is_fuzzy)
    memset(state->fuzzy_counts, 0, sizeof(state->fuzzy_counts));
```

(`:11790-11792`.) `record_fuzzy` pushes onto `state->fuzzy_changes` at `items[count++]` (`:9793`) and
`unrecord_fuzzy` pops (`:9802`), so the list is a stack - but a search attempt that is abandoned
without unwinding, which is exactly what `(*PRUNE)` and `(*SKIP)` do when they cut the backtracking,
leaves its entries on it. `match_fuzzy_changes` then reports the **first** `sum(fuzzy_counts)` entries
(`:20522`), so a stale entry does not merely sit there unread: it **displaces** the change the winning
attempt recorded. The counts survive because they are cleared; the list does not because it is not.

**Proposed fix.** Clear the change list beside the counts at `:11791`, which is what the cleared
counts already assert - a fresh attempt has used no errors, so it can have no changes. The one-line
alternative, resetting `state->fuzzy_changes.count = 0` there, is the same edit in upstream's own
idiom.

**What this port answers, and why it is not fixed here yet.** The same, exactly - both rows are pinned
by `FuzzyMatchingTests.A_search_that_restarts_does_not_carry_the_abandoned_attempt_s_errors_into_the_next_one`
(S38). S40a made the one-line fix, measured it, and **reverted it**: clearing the list turns those two
pinned rows red and makes this port diverge from upstream on rows it currently agrees on. Fixing an
inherited bug is a decision about what the right answer is plus a permanent oracle divergence to
carry, which is a slice of its own - the shape entry 7 already has.

**A second symptom, and the reason this was found at all.** This port reaches the leak on shapes
upstream's optimiser keeps it away from, because it has no start prefilter until Phase 7:

```python
>>> regex.search(r'(?<=(?:[ab][cd]){e<=1})$', 'axc').fuzzy_changes
([2], [], [])          # this port: ([], [], [1])
```

`$` has a `search_start_*` twin, so upstream makes ONE attempt, at the end of the subject, and its
first attempt is its winning one. This port attempts positions 0, 1, 2 and 3; the attempt at 1
succeeds *inside the lookbehind*, records a deletion and then fails on the `$`. Replace the `$` with a
literal and upstream walks every position too - and then the two engines agree again, because
upstream's winning attempt is still its first. Found by S40's blind review, which read it as a port
defect; it is the same defect as above, reached by a different door. Pinned by
`FuzzyMatchingTests.A_search_attempt_that_fails_after_a_lookaround_carries_its_change_into_the_next_one`.

**Related:** entry 7, the other inherited bug on Phase 6's list.

