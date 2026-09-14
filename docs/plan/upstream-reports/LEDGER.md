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

**S44 made 2026.9.10 the PIN on 2026-09-13**, so "the newest release" and "the version the oracle
records against" are now the same thing, and that re-verification is no longer a separate act - the
oracle itself performs it on every wave. Two consequences for this file:

- **Entry 10 is CLOSED.** Its fix is in the pinned release and both of its clamps are now ported
  here, so there is nothing outstanding on either side. It stays in the file as history.
- Every other entry was re-checked the same day by re-recording all 42 divergence example rows
  against the new pin: 37 reproduced byte for byte, 3 differed only by an `anchoredScan` field the
  recorder did not write when they were first drawn, and the 2 that stopped diverging were issue
  614's, which this ledger already recorded as fixed upstream. **No entry below changed status.**
  In particular entry 5 is unaffected by the issue 613 clamps landing here, which is the thing
  `ExpectedDivergences`'s header warned could be assumed and is not true.
- **And the sync ADDED one: entry 15, a regression in 2026.9.10 itself**, caused by entry 10's own
  fix. It is the first entry here that upstream did not have when the previous slice closed, and
  the gate found it by re-running rather than by anyone looking for it.

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

**Status, S48 2026-09-14: SIX DOORS, all of them still open upstream, and this port now diverges from
upstream on every one.** Five were already closed here when they were written up - S40a's per-match
reset and S40b's restore before the partial pass, each with its own `ExpectedDivergences` entry. The
sixth, below, is the one this port SHARED, and it is fixed here (owner's rule of 2026-09-12: an
inherited bug is fixed before 1.0). The entry's own proposed fix needs one correction for it; the
sixth door says which.

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

**A FIFTH DOOR, added by S43 on 2026-09-13, and it is not a scanner at all - it is a single
`search`.** Every symptom above carries the stale slice from one match of a scan into the next.
This one carries it between the TWO PASSES of one match attempt. A `partial` request runs a
non-partial pass and then, only if that fails, a partial one from the same `text_pos` (`do_match`,
`:18160`); upstream restores `text_pos` and nothing else, so a bound the verb moved in the first
pass is still moved in the second. That makes the report's scope wider than "overlapped scans" and
is worth stating, because the proposed fix above - resetting the slice in `init_match`, or saving
and restoring it around `do_match` - is what closes this door too.

Found by the seed-99991 `fuzzy,interactions` wave, row 6897, and minimised to the one cut that held:

```python
>>> p = r'\b(?:(?:\ _(\W)){e<=1}(*SKIP)[A-Z]|[^a])(?:.?(?:(\w+?)){i<=1:.}){e<=2,s<=1:[^a-z]}\W'
>>> m = regex.compile(p, regex.M).search('\U0001f600ß_ ', partial=True)
>>> m.span(), m.fuzzy_counts, m.span(1)
((1, 4), (0, 1, 0), (-1, -1))                      # an insertion, group 1 never reached
>>> q = p.replace('(*SKIP)', '(*PRUNE)', 1)        # same pruning, NO bound moved
>>> m = regex.compile(q, regex.M).search('\U0001f600ß_ ', partial=True)
>>> m.span(), m.fuzzy_counts, m.span(1)
((1, 4), (1, 0, 0), (3, 4))                        # a substitution, and the capture
```

**The two engines agree on the SPAN here**, which is what makes this symptom different from the four
above and why it needs saying separately: forwards the moved bound is `slice_start` rather than
`slice_end`, and what it costs is which ALTERNATIVE the second pass can still enter - so the
difference surfaces as the error spent and the group captured, not as a span or a missing match.
Deleting the verb gives the `(*PRUNE)` answer too, and asking the same three patterns WITHOUT
`partial` gives `None` on all of them, which is the cleanest statement that the second pass is where
this lives. The pattern carries a second verb, a `(*PRUNE)`, and deleting it changes nothing - a
control this family has not had before.

**Verified 2026-09-13** against `regex` 2026.7.19; pinned by
`PartialMatchingTests.A_forward_skip_does_not_move_the_slice_start_the_partial_pass_searches` and
classified by `ExpectedDivergences.partial-retry-carried-slice-forward`. The reversed twin of this
same door is `ExpectedDivergences.partial-retry-reversed-slice`, found by S40b. Re-runnable:
`python tools/probes/upstream-skip-carried-slice-forward.py`.

**A SIXTH DOOR, added by S48 on 2026-09-14, and it is the first one this port SHARED** - every door
above it was already fixed here when it was written up, and this one was not. It is neither a scan
nor a two-pass partial: the stale slice crosses from one CANDIDATE of a single `(?b)` match to the
next.

`do_best_fuzzy_match` walks `start_pos` across the slice, one `basic_match` per candidate, holding
the next run to strictly fewer errors than the last. Its loop guard reads the LIVE bounds
(`:17625`):

```c
    while (state->slice_start <= start_pos && start_pos <= state->slice_end) {
        state->text_pos = start_pos;
        state->must_advance = must_advance;

        /* Initialise the state. */
        init_match(state);
```

`init_match` is this report's own subject: it does not reset the slice. And `start_pos` is set to
`state->match_pos` at the foot of the loop (`:17680`) - the START of the match the candidate just
found - so a `(*SKIP)` that consumed anything leaves `slice_start` above it and the guard is false
on the next turn. **The walk ends on its first successful candidate**, and every better match
further along the subject is never attempted. The second pass reads the same stale bound again, in
its `max_offset` (`:17721`) and in every anchored re-run.

Minimised to four ASCII characters and no flags:

```python
>>> regex.compile(r'(?b)(?:a(*SKIP)b){e<=1}').search('axab')
<regex.Match object; span=(0, 2), match='ax', fuzzy_counts=(1, 0, 0)>
>>> regex.compile(r'(?b)(?:a(*SKIP)b){e<=1}').match('axab', 2)
<regex.Match object; span=(2, 4), match='ab', fuzzy_counts=(0, 0, 0)>
```

The search answers a one-error match; the same compiled pattern's own `match` finds a PERFECT one
two characters later. `(*PRUNE)` in the verb's place, and the verb deleted, both give `(2, 4)` with
no errors.

**What judges it is the verb's own definition, and it is sharper here than anywhere else in this
entry.** `(*SKIP)` sets a skip point, and what a skip point forbids is a later attempt *below* it
(pcre2pattern, "Verbs that act after backtracking"). Here the skip point is 1 and the candidate the
walk never reaches starts at 2 - which the verb permits outright. `BESTMATCH` then promises the
fewest errors among the matches that exist, so losing a zero-error match its own anchored door finds
needs no appeal to any other engine. **No second engine is available in any case: PCRE2 has no fuzzy
matching at all** (`tools/probes/pcre2-has-no-fuzzy-matching.py`), which is why the self-refutation
and the `(*PRUNE)` control carry the whole judgement.

**Not rare.** Over a small alphabet of 11,340 `(?b)`-plus-`(*SKIP)` shapes, 1,861 answer differently
with `(*SKIP)` than with `(*PRUNE)`
(`python tools/probes/upstream-bestmatch-walk-truncated-by-a-skip.py --hunt`).

**Proposed fix.** This entry's own - reset the slice in `init_match`, or save and restore it around
the walk - but **with one correction this report must state, because the `init_match` form is wrong
for this door**: `do_enhanced_fuzzy_match` (`:17871`) and `do_best_fuzzy_match`'s own widened-slice
fallback (`:17807`) narrow the slice deliberately and then call `init_match`, and a reset there
would throw their narrowing away. Save and restore around each candidate instead.

**FIXED HERE, S48, 2026-09-14**, that second way: `Matcher.DoBestFuzzyMatch` restores the caller's
slice before each candidate in both passes. Pinned by
`Gaps.Engine.FuzzyBestMatchTests.Bestmatch_looks_past_the_candidate_whose_own_skip_moved_the_slice`,
with `.Bestmatch_still_lets_a_skip_prune_a_candidates_own_alternatives` holding the line the fix
must not cross - the restore is once per candidate and restores the slice only, so a verb still cuts
the backtracking of the attempt it fired in, on a row where the pruning decides the answer and the
moved bound does not (`(?b)(?:\w(*SKIP)a|a){e<=1}` over `'axab'`, both engines `(1, 3)`) - and
classified by `ExpectedDivergences.bestmatch-walk-truncated-by-a-skip`. Re-runnable:
`python tools/probes/upstream-bestmatch-walk-truncated-by-a-skip.py` and
`pwsh -File tools/probes/port-bestmatch-walk-cases.ps1`.

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

## 7. The default case-folding tables carry CaseFolding.txt's Turkic-only rows - FIXED HERE (S45)

**Title:** Turkic case folding is applied by default, so `I` matches `ı` and `İ` never reaches its
full fold

**THIS ENTRY WAS WRONG UNTIL 2026-09-14, in both its cause and its proposed fix, and S45 rewrote it
after settling the question against the definitive source.** The symptom it recorded is real; the
explanation - "the expansion list is not lower-cased" - described a consequence, not the cause, and
the fix it proposed would have papered over the wrong layer. What is below replaces it. The original
diagnosis is left nowhere else, on purpose: a ledger entry that states a wrong cause is worse than no
entry, because the next reader spends the slice re-deriving it.

**Body:**

```python
>>> import regex
>>> regex.compile('İ', regex.I | regex.F).fullmatch('i̇')     # the F mapping is lost
None
>>> regex.compile('I', regex.I).fullmatch('ı')                # the T mapping is applied
<regex.Match object; span=(0, 1), match='ı'>
>>> regex.compile('i', regex.I).fullmatch('İ')                # ... and its other half
<regex.Match object; span=(0, 1), match='İ'>
```

`CaseFolding.txt` (17.0.0) gives these four rows, and no others, for the four codepoints:

```
0049; C; 0069; # LATIN CAPITAL LETTER I
0049; T; 0131; # LATIN CAPITAL LETTER I
0130; F; 0069 0307; # LATIN CAPITAL LETTER I WITH DOT ABOVE
0130; T; 0069; # LATIN CAPITAL LETTER I WITH DOT ABOVE
```

and its header says what to do with them:

```
# T: special case for uppercase I and dotted uppercase I
#    - For non-Turkic languages, this mapping is normally not used.
#    - For Turkic languages (tr, az), this mapping can be used instead of the normal mapping
#      for these characters.
#
# Usage:
#  A. To do a simple case folding, use the mappings with status C + S.
#  B. To do a full case folding, use the mappings with status C + F.
#
#    The mappings with status T can be used or omitted depending on the desired case-folding
#    behavior. (The default option is to exclude them.)
```

UTS #18 RL1.5 requires "at least the simple, **default** Unicode case-insensitive matching" and "at
least the simple, **default** Unicode case folding"; the core specification section 5.18.2 calls the
Turkish rule "a case mapping that depends on the locale". Neither the pattern nor the subject carries
a locale, so the default applies.

**Where it comes from.** `tools/build_regex_unicode.py` merges the `T` rows into **both** default
tables - `kind in {'S', 'C', 'T'}` at `:455` and `kind in {'F', 'C', 'T'}` at `:459` - and hard-codes
the Turkic pairing into the all-cases table at `:1071-1074`:

```python
all_cases[0x49] = {0x49, 0x69, 0x131} # Dotless capital I.
all_cases[0x69] = {0x69, 0x49, 0x130} # Dotted small I.
```

Every `_IGN` opcode reads `re_get_all_cases`, so that hard-coding is what makes `(?i)I` match `ı`.
The folding half is then papered over by `unicode_possible_turkic` (`_regex.c:1984`), which passes
all four codepoints through the fold functions **unchanged** - which is not the default mapping
either. It loses `0049; C; 0069`, so `I` folds to `I`; and it loses `0130; F; 0069 0307`, so `İ`
never reaches the full fold. That second loss is the symptom this entry was originally filed under.

**Three second engines were run on the whole 25-cell grid on 2026-09-14** (`.scratch/s45-definition.py`,
`.scratch/s45-perl.pl`, `.scratch/s45-dotnet.ps1`). PCRE2 10.47 under `PCRE2_UTF | PCRE2_UCP |
PCRE2_CASELESS` and .NET 10.0.10 under `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant`
agree cell for cell with default **simple** folding; Perl 5.42.2's `/i` under `(?u:...)`, which folds
fully, agrees cell for cell with default **full** folding - `İ` matches `i̇` there and nowhere else.
`regex 2026.9.10` is the only one of the four that answers the Turkic way.

**Reproduce:** `python tools/probes/upstream-turkic-definition.py`,
`python tools/probes/upstream-turkic-fold-sweep.py`, `perl tools/probes/upstream-turkic-grid.pl`
and `pwsh -File tools/probes/upstream-turkic-grid.ps1`. Measured 2026-09-14, regex 2026.9.10,
PCRE2 10.47 2025-10-21, Perl 5.42.2, .NET 10.0.10.

**The fix upstream would need.** Drop `'T'` from both sets at `:455` and `:459`, delete the four
`all_cases` overrides at `:1071-1074`, and delete `unicode_possible_turkic` and its two call sites -
it exists only to work around the merge. The assertion at `:468` that the Turkic set is exactly
`{(0x49, (0x131,)), (0x130, (0x69,))}` should stay: it is the guard that would catch a future UCD
adding a third `T` row. If a Turkic mode is ever wanted it needs a flag, because the module exposes
no locale.

**Fixed in this port, S45, 2026-09-14.** `src/FuzzyRegex/Unicode/TurkicDefaults.cs` substitutes the
default simple folding, full folding and case set for exactly those four codepoints, and
`Encodings.AllCases`, `.SimpleCaseFold` and `.FullCaseFold` consult it before the generated tables.
The generated `.g.cs` is untouched - it is transliterated from upstream's C and must stay so. The
divergence is classified for the oracle as `turkic-default-folding` and pinned by
`Gaps.Engine.CaseFoldingTests`, which asserts all 25 cells under `(?i)` and `(?fi)`.
`Sequence.FixFullCasefold` needed no change at all: with `fold_case` expanding `U+0130` on its own,
the inventory and the text it is sought in agree, which is why the original "lower-case the
inventory" fix was aimed at the wrong layer.

**Nothing filed.** Design spec amendment 16 outcome (c): both this port and upstream were wrong, the
fix lands here, the entry is corrected, and no report goes to mrab-regex until the owner approves the
upstream ledger as a whole.

---

## 8. A group call inside a lookaround that runs the other way loses the match, even on a path that never enters the call

**Title:** A `(?&name)` call inside a lookaround of the opposite direction makes the whole pattern
fail, where the same lookaround written out succeeds

**Body:**

```python
>>> import regex
>>> regex.__version__
'2026.7.19'
>>> regex.search(r'(?P<g1>\w)(?<=(?&g1))\W', 'aa ')
None
>>> regex.search(r'(?P<g1>\w)(?<=\w)\W', 'aa ')
<regex.Match object; span=(1, 3), match='a '>
```

Three items and a three-character subject, and the only difference between the two patterns is
whether the lookbehind names the group or spells out the class that group contains. The second is
what upstream should answer to both.

**The isolation is complete** (`python tools/probes/upstream-group-call-loses-matches.py`, all over
`'aaaa '` unless stated):

| variant | upstream |
|---|---|
| `(?P<g1>\w)(?<=(?&g1))\W` | None |
| `(?P<g1>\w)(?<=(?P>g1))\W` - the other call syntax | None |
| `(?<=(?&g1))\W(?P<g1>\w)` - the call before the group | None |
| `(?P<g1>\w)(?<=\w)\W` - the class written out | (3, 5) |
| `(?P<g1>\w)(?<=[a-z])\W` - a different class that also matches | (3, 5) |
| `(?P<g1>\w)(?=\W)\W` - a lookAHEAD, so the directions agree | (3, 5) |
| `(?P<g1>\w)(?=(?&g1))\w` - a lookahead that CALLS, directions agreeing | (0, 2) |
| `(?P<g1>\w)(?&g1)\W` - the call where it CONSUMES, no lookaround, **over `'aaa '`** | (1, 4) |

So it is neither "a call" nor "a lookbehind" on its own: a call that consumes is fine, a lookbehind
that does not call is fine, and a call inside a lookaround running the SAME way as the pattern is
fine. It is a call inside a lookaround of the OPPOSITE direction, which is this entry's title.

It is not a subject-length threshold either. Upstream answers None at every length tried while the
inline copy matches at every one:

| subject | `'a '` | `'aa '` | `'aaa '` | `'aaaa '` | `'aaaaa '` | `'aaaaaa '` | `'aaaaaaa '` |
|---|---|---|---|---|---|---|---|
| the call | None | None | None | None | None | None | None |
| written out | (0, 2) | (1, 3) | (2, 4) | (3, 5) | (4, 6) | (5, 7) | (6, 8) |
| **this port, the call** | **None** | (1, 3) | (2, 4) | (3, 5) | (4, 6) | (5, 7) | (6, 8) |

**THE SUBJECT MUST BE THREE CHARACTERS, AND THE REASON IS A SECOND DEFECT SITTING ON TOP OF THIS
ONE** - which is why the reproduction above uses `'aa '` and not the shorter `'a '`. At two
characters this port answers None as well, and NOT because it shares this bug: a call counts towards
`min_width` at the width of the group it calls even inside a zero-width lookaround, so `min_width`
here is 3, and `do_exact_match`'s width early-out refuses a two-character subject before matching
starts. That inflation is upstream's, this port reproduces it deliberately (S40c, and
`GroupCallTests.A_group_call_counts_towards_min_width_even_inside_a_zero_width_lookaround`), and it
MASKS this entry's defect at exactly one subject length. One more character separates them.

A report should carry `'aa '` for that reason, and a reader who tries `'a '` and sees agreement has
met the other defect rather than refuted this one.

**It is not fixed by #614.** The minimal form and both call syntaxes answer identically on
2026.9.10, the newest release, measured 2026-09-13.

**How it shows in the wild**, and the shapes a wave draws it in - these were the whole of the
reproduction until S43 minimised it, and they are kept because they are what the oracle actually
meets:

```python
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

**What this port answers.** The match - the one the shorter pattern finds - in every case except the
two-character subject named above, where the unrelated `min_width` inflation refuses it on both
engines. Pinned by
`GroupCallTests.A_group_called_from_a_lookbehind_with_anything_after_it_matches_here_and_not_upstream`
and `.A_zero_width_piece_holding_a_group_call_cannot_remove_a_match_here`, which asserts the mask as
well as the divergence, and classified in the oracle as `group-call-loses-the-match`.

**THE MINIMAL FORM IS ESTABLISHED, AND THIS PARAGRAPH USED TO SAY IT WAS NOT.** Until S43
(2026-09-13) every known row of this family was wave-sized and resisted shrinking: the cuts that kept
upstream self-contradictory produced patterns on which *this port answers what upstream answers* -
`(?P<g1>a)((?<!(?&g1)))*` over `'a'`, `(?r)(?P<g1>[A])((?(?=(?&g1))S))` over `'A'` - so the entry
recorded "a call through an opposite-direction lookaround is not on its own sufficient" and told a
report not to claim a minimal form. **That reading was wrong, and it was wrong because every attempt
had shrunk a row along the wrong axis.** Those rows all hold the call inside a *conditional* inside a
*repeat*, and the cuts removed the repeat or the conditional - the pieces that made the surrounding
match possible - rather than the call's own setting. Seed 99991's row 10201 arrived with the call in
a bare lookbehind instead, and cutting *that* one went all the way down without ever losing the
divergence: `(?P<g1>\w)(?<=(?&g1))\W`, three items.

**What is genuinely still open** is narrower, and one part of it was answered by being got wrong
first. A draft of this paragraph claimed the minimal form diverged on `'a '`; it does not - both
engines answer None there - and the pinned test failed on exactly that, which is how the
`min_width` mask above was found rather than assumed. The remaining open question is why the OTHER
earlier minimisations agreed: `(?P<g1>a)((?<!(?&g1)))*` is a NEGATIVE lookbehind whose body is the
group, and this family's minimal form is a positive one; whether the negative form is a second,
unaffected path, or the same defect masked the way `'a '` masks it, is not measured here.

**Proposed fix.** Still not established, beyond "the same area as #614" - but the minimal form is now
small enough to step through, which is what that needs, and a report can carry it.

**Related:** #614 (fixed, and does not cover this); entry 5, whose own minimal form arrived the same
way, from a later seed rather than from more cutting of an early row.

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

**S43 SHARPENED THE CONDITION AND HALF OF THAT SENTENCE IS WRONG, 2026-09-13.** It is not "a fuzzy
section" but **a fuzzy match that actually SPENT an error**, and `match` is affected exactly as
`search` is. Fourteen rows, `python tools/probes/upstream-posix-fuzzy-changes-crash.py`: every row
whose `fuzzy_counts` are `(0, 0, 0)` is safe and every row with a non-zero count faults, POSIX
present - any error kind, with or without an alternation, inline `(?p)` or the flag.

```
'(?p)(?:abc){e<=1}' on 'axc'   span (0, 3) counts (1, 0, 0) | *** CRASH 0xC0000005 ***
'(?p)(?:abc){d<=1}' on 'ac'    span (0, 2) counts (0, 0, 1) | *** CRASH 0xC0000005 ***
'(?p)(?:abc){i<=1}' on 'abxc'  span (0, 4) counts (0, 1, 0) | *** CRASH 0xC0000005 ***
'(?p)(?:abc){e<=1}' on 'abc'   span (0, 3) counts (0, 0, 0) | changes ((), (), ())     # safe
'(?p)(?:aa|a){e<=1}' on 'aa'   span (0, 2) counts (0, 0, 0) | changes ((), (), ())     # safe
'(?:abc){e<=1}'     on 'axc'   span (0, 3) counts (1, 0, 0) | changes ((1,), (), ())   # no POSIX
```

That matches the mechanism this entry already names exactly - a count with no change behind it -
and it sharpens a report: the earlier "needs a fuzzy section" reads as though any POSIX fuzzy
pattern is unsafe, and a maintainer who tried an exact-matching one would not reproduce it.

The probe prints the span and the counts BEFORE touching the changes and flushes, which is why the
faulting rows above still report them. Those are upstream's own values, and they are what makes this
port's expected answers measured rather than derived.

**S41 narrowed the faulting access, 2026-09-13, and it makes `match` affected too.** The crash is in
reading `Match.fuzzy_changes`, not in matching. Each of these runs in its own interpreter:

```python
>>> regex.match(r'(?p)(?:cat){e<=1}', 'caz').span()          # (0, 3)
>>> regex.match(r'(?p)(?:cat){e<=1}', 'caz').fuzzy_counts    # (1, 0, 0)
>>> regex.match(r'(?p)(?:cat){e<=1}', 'caz').fuzzy_changes   # Segmentation fault
```

One substitution counted and no substitution recorded. `match_fuzzy_changes` (`:20504`) walks
`fuzzy_counts[i]` entries of the changes list, so a count of 1 over an empty list reads past the end
- which is the over-read, and it needs no `search` to reach. The cause is the asymmetry this entry
already names, seen from the other side: `save_best_match`/`restore_best_match` copy
`best_fuzzy_counts` and there is **no `best_fuzzy_changes` beside it**, where
`do_best_fuzzy_match` and `do_enhanced_fuzzy_match` each carry both. POSIX saves a match and then
fails on purpose to look for a longer one, and the backtracking unrecords the changes; restoring the
counts without the changes is what leaves the two contradicting each other. The probe's `search`
spelling crashes for the same reason and not a different one.

**So the fix upstream is one line of kind, not of code**: give `save_best_match` and
`restore_best_match` the `best_changes_list` pair (`save_fuzzy_changes`/`restore_fuzzy_changes`,
`:9899`, `:9930`) that the two fuzzy ranking modes already use. That upgrades this entry's
"Proposed fix: unknown" below for the `fuzzy_changes` half; whether `search` has a second,
independent fault on top is still unestablished, because a `search` that never reaches
`fuzzy_changes` was not tried.

**And it is why S41 did not port `best_fuzzy_counts` even though it ported the helpers.** Porting
the counts copy alone would import exactly this contradiction into a memory-safe language, where it
surfaces as a match reporting one substitution and no substitution positions rather than as a crash.
S42 takes the pair together or neither.

**S43 TOOK THE PAIR, AND THE PORT SIDE OF THIS ENTRY IS CLOSED (2026-09-13).**
`Matcher.SaveBestMatch` and `Matcher.RestoreBestMatch` now copy the fuzzy counts AND the change list
both ways - `MatchState.BestFuzzyCounts` and `MatchState.BestFuzzyChanges`. Upstream copies only the
counts, so a faithful port would have reproduced the contradiction memory-safely and answered a
match whose changes belong to a candidate that lost; design spec amendment 20 says an inherited bug
is fixed here rather than carried, so both are saved and both are restored.

It was not a theoretical gap. Before the fix, `(?p)(?:abc){e<=1}` over 'axc' reported
`(0, 3)` with counts `(0, 0, 0)` here - the substitution it had really spent erased by the POSIX
restore - where upstream reports `(1, 0, 0)` for the same span. The two engines now agree on every
row of the probe that upstream survives, and on `(?p)(?:a|aa){e<=1}` over 'aa' they agree on
`(0, 2)` with one insertion. **No oracle wave could ever have caught this**, which is the part worth
carrying into Phase 6: upstream dies rendering the changes of exactly the rows that would have shown
it, so the comparison the oracle exists to make is unavailable for the whole family. It was found by
hand, writing the pinned tests. Pinned by `Gaps/Engine/FuzzyPosixTests.cs`, six tests.

**Where it comes from, at the precision the evidence supports.** `save_best_match` (`:11493`) and
`restore_best_match` (`:11565`) copy `state->best_fuzzy_counts` alongside the groups, and
`check_posix_match` (`:11602`) is what drives them. `do_simple_fuzzy_match` (`:18027`) sets
`state->max_errors` to `PY_SSIZE_T_MAX` and does not touch the best-match fields that
`do_exact_match` leaves alone too. Which of those is the faulting access has NOT been established -
no debugger was attached and no ASAN build was made - so a report must either establish it or say
it does not know. It is the same family as the four 2026 memory-safety fixes (issues 611-614) and
plausibly the same fuzzing campaign would have found it.

**What this port answers.** The row, without crashing, with the right span, the right counts and the
right changes - see the S43 paragraph above. The earlier text here said the counts were "not yet
trustworthy" and named a slice against it; that slice was S43 and the gap is closed. Pinned as
"does not crash" by
`FuzzyMatchingTests.A_POSIX_search_of_a_fuzzy_pattern_answers_where_upstream_crashes`, and as the
actual answers by `Gaps/Engine/FuzzyPosixTests.cs`.

**Consequence for the oracle.** The `fuzzy` generator draws no `(?p)`, and **S43 made
`interactions` suppress POSIX on any row carrying a fuzzy section** for the same reason: a recorder
row that kills the interpreter takes the whole wave with it, and no `except` clause can see it.
A narrower exclusion was considered and rejected - the faulting condition is a spent error, which
depends on the subject rather than on the pattern, so nothing the generator can read off the pattern
is a safe test. **S46 sitting 2 removed that suppression** (see below); the crash is unchanged and
still upstream's, but the recorder no longer reads the attribute that triggers it, so the generator
draws the cell again and the wave compares everything about it but the change positions.

**S46 RE-MEASURED ALL OF THAT ON THE PINNED 2026.9.10 AND FOUND THE WAY TO LIFT THE EXCLUSION
CHEAPLY, 2026-09-14.** Three facts, each run rather than reasoned:

1. **The crash still kills the recorder outright.** `python tools/record-oracle.py --rows` over a
   two-row file holding `(?p)(?:abc){e<=1}` on `'axc'` exits 139 (SIGSEGV under Git Bash) and writes
   no output file at all. So the slice's own gate - "upstream's crash must arrive as a recorded
   `error` or `timeout` row, not a dead recorder" - is NOT met as the recorder stands.
2. **Entry 9's spent-error rule holds exactly on 2026.9.10** (`tools/probes/upstream-posix-fuzzy-spent-error.py`, each
   case in its own child): `(?p)(?:abc){e<=1}` faults on `'axc'` (counts `(1,0,0)`) and on `'abcd'`
   (counts `(0,1,0)`), and is safe on `'abc'` (counts `(0,0,0)`); `(?p)(?:aa|a){e<=1}` on `'aa'` is
   safe. **And the `'abcd'` row is the one to carry into a report**: it looks like an exact match
   and is not, because POSIX leftmost-longest stretches it to spend an insertion. That is the
   sharpest available demonstration that no predicate over the pattern OR the subject is a safe
   test, which is the claim the paragraph above makes.
3. **`fuzzy_counts` is safe on a faulting row; only `fuzzy_changes` faults**, exactly as this entry
   says - the probe prints the span and the counts of every faulting row before it dies.

Fact 3 is the lever, and it makes the exclusion liftable without any per-row process isolation:
`_describe_match` reads `fuzzy_changes` only when the counts are non-zero, so a recorder that records
`fuzzyCounts` and OMITS `fuzzyChanges` on a POSIX row never touches the faulting access. The cost is
that change POSITIONS cannot be compared on those rows - which is not a loss, because upstream has no
answer to give for them - so both sides must render the fuzzy half without positions.

**S46 SITTING 2 LIFTED THE EXCLUSION, 2026-09-14, AND IT WAS ONE GUARD RATHER THAN FOUR.** The
design above feared "every path that reads a match - `finditer`, `sub`, `split` as well as the
single-match door". They all call `_describe_match`: the single-match door at `:684`, `finditer` at
`:639`, a `(*SKIP)` substitution's `subMatches` at `:607` and `_anchored_scan` at `:896`. So the
guard sits in that one function, which is also the only place the recorder ever reads the attribute.
`sub` and `split` answer a string and a list of parts and read no match at all.

Four measurements stand behind it, each run rather than reasoned, on regex 2026.9.10:

1. **`compiled.flags` is a safe test for POSIX, on every spelling** - the flag, a leading `(?p)`,
   one written mid-pattern and one inside a group all set the bit
   (`tools/probes/upstream-posix-flag-is-visible-on-compiled.py`). So the guard can be read off the
   compiled pattern, where the row's own `flags` field would miss an inline `(?p)` entirely.
2. **Every other read `_describe_match` makes is safe on a faulting match** - `span(n)` and
   `spans(n)` over the whole group range, `lastindex`, `lastgroup`, `partial`, and the `finditer`,
   `subn` and `split` doors (`tools/probes/upstream-posix-fuzzy-safe-attributes.py`, one child
   process per read). Only `fuzzy_changes` dies, with 0xC0000005.
3. **Before and after, on the same sixteen rows.** `git show HEAD:tools/record-oracle.py` over a
   file holding `(?p)(?:abc){e<=1}` on `'axc'` and fifteen neighbours exits 139 and writes nothing;
   the guarded recorder writes all sixteen, and this port agrees with upstream on every one of them
   - 16 of 16, spans, groups and error counts.
4. **The cell was genuinely blind and now is not.** Control `S46-D` reverses the counts copy in
   `Matcher.RestoreBestMatch`. Against an `interactions` wave recorded by HEAD's recorder it moves
   nothing at all (5 divergences against an unmutated 5 at seed 31337, and that wave holds **zero**
   POSIX-and-fuzzy rows); against the same wave recorded by the guarded one it gives 37 against 6.

**And lifting it found a bug in THIS PORT on its first run** - which is what the cell was blind to,
and it is not upstream's. Seed 31337 row 3343, minimised:

    regex.compile(r'(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}', regex.POSIX)
      .fullmatch('+ aBA')
    # upstream: (0, 5) fuzzy_counts (0, 1, 1)     - two errors
    # this port: (0, 5) fuzzy_counts (1, 1, 1)    - three, for the same span

Self-refuting on this port's own behaviour: drop POSIX and this port answers `(0, 1, 1)` too, so it
is not a ranking difference but POSIX losing an error count. It needs POSIX **and** `(?e)`, which is
where this port's own cost ranking lives (`DoEnhancedFuzzyMatch`, `IsBetterFuzzyMatch`); `(?b)`,
`(?r)` alone, and the same row without POSIX all agree. The standing hypothesis, not yet proven, is
that `RestoreBestMatch` puts back `FuzzyCounts` and `FuzzyChanges` but not `state.TotalErrors` or
`state.TotalCost`, and this port's ranking - unlike upstream's, which keeps the last successful run -
reads both. Upstream leaves `total_errors` stale too (`restore_best_match`, `:11565`), so the
staleness is inherited and the cost field is not.

**S48b PROVED THAT HYPOTHESIS AND CLOSED IT, 2026-09-14; THE PORT SIDE OF THIS ENTRY IS NOW WHOLLY
CLOSED.** The mechanism was established by instrumenting the enhanced walk rather than argued, and
these are the walk's own numbers, one line per run of `DoEnhancedFuzzyMatch`:

```
POSIX     run 1  TotalErrors=3 TotalCost=3  counts=(1,1,1)  errorsFromCounts=3  changes=3
POSIX     run 2  TotalErrors=3 TotalCost=3  counts=(0,1,1)  errorsFromCounts=2  changes=2  <- stale
no POSIX  run 1  TotalErrors=3 TotalCost=3  counts=(1,1,1)  errorsFromCounts=3  changes=3
no POSIX  run 2  TotalErrors=2 TotalCost=2  counts=(0,1,1)  errorsFromCounts=2  changes=2
```

Run 2 under POSIX restores the RIGHT counts - `(0, 1, 1)`, two errors, which is upstream's answer -
and reads `TotalErrors == 3` from the candidate that lost, because the POSIX `FAILURE` arm's
`RestoreBestMatch` never touched it. `state.TotalErrors >= fewestErrors` is then `3 >= 3`, the walk
breaks before keeping run 2, and the match reports run 1's three errors for the same span. Without
POSIX the identical run reports 2, is kept, and answers `(0, 1, 1)`. **One stale field: the
self-refutation was the walk ranking a restored candidate's counts against a discarded candidate's
total.**

**The fix is four lines and does NOT touch the ranking rule**, which is an owner decision (S41/S42)
and which the slice's own guard forbids widening into: `SaveBestMatch` records `TotalErrors` and
`TotalCost` into `BestTotalErrors`/`BestTotalCost`, and `RestoreBestMatch` puts them back beside the
counts and the changes it already restored. `IsBetterFuzzyMatch` is unchanged.

Re-measured on the committed code: `pwsh -File tools/probes/port-fuzzy-counts-and-changes.ps1` gives
`(0, 5)` counts `(0, 1, 1)` changes `([], [4], [0])` with POSIX and without, and
`python tools/probes/upstream-fuzzy-counts-and-changes.py` gives upstream `(0, 5)` counts `(0, 1, 1)`
under POSIX and the same change positions `([], [4], [0])` without it - so the port now agrees with
upstream on the counts and on every position. Pinned by `Gaps.Engine.FuzzyCountsAndChangesTests
.A_posix_enhancematch_fullmatch_spends_no_more_errors_than_the_same_span_needs`, whose second
assertion IS the self-refutation: the POSIX answer must equal the flagless one. Negative control
**S48b-B**, `posix-restore-leaves-the-running-totals-stale`, takes the two lines out again.

**UPSTREAM STILL HAS THE STALE-TOTALS BUG, and S48b's second sitting pinned it (2026-09-14).** The
port side is closed; the same over-charge is visible in upstream at seed-7 gate row 76983,
`(?e)(?r)(?p)(?:[^\d][a\d]\p{L}){s<=1,i<=1,d<=1}(\p{Lu})+\b` over `' ﬀS'`, flags `0x8`, overlapped
`finditer`. Both engines answer the span `(0, 3)` and the same group; upstream charges `(1, 0, 1)`
where its own POSIX-free engine fits that very span in `(0, 0, 1)`, which is this port's answer.
Two more rows of the same family are pinned with it - seed-7 row 73895, where upstream's own
POSIX-free `fullmatch` over the span it reported under POSIX answers with NO errors, and
seed-20260914 row 76101, a `subf` whose replacement TEXT moves. Oracle entry
`posix-fuzzy-contradicts-its-own-flagless-answer`; probe
`python tools/probes/upstream-posix-and-atomic-free-answers.py`; pinned by
`Gaps.Engine.FuzzyPosixTests.A_posix_enhancematch_span_costs_no_more_than_the_same_span_costs_without_posix`
and `.A_posix_fuzzy_match_spends_what_the_flagless_engine_spends`.

**Proposed fix, for the upstream half that remains** - the `fuzzy_changes` over-read. Unknown.
Establishing it needs a debug build of the C extension, which this
project has deliberately not set up (design spec amendment 7: releases and PyPI wheels only).

**Related:** issues 611-614, the 2026 memory-safety group.

# Added by S40a, 2026-09-13

## 10. `(*SKIP)` inside an atomic group after an optional item loops for ever - CLOSED

**Status: CLOSED by S44 on 2026-09-13.** Fixed upstream in 2026.8.30, which is now the pinned
release's ancestor, and both clamps are ported here. Nothing to file and nothing outstanding.
Kept in full because it is the defect that forced the recorder's per-row deadline, and because
the grid below is still the sweep that makes any claim about this shape mean anything.

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

**What this port answers, and what the honest limit on it WAS.** `None`, on the row and on all 1296
grid calls. Until S44 this port still carried the PRE-FIX arm - `Matcher.cs`'s `GreedyRepeatOne`
backtrack had upstream's slice clamp and not the `pos` clamp - so "does not hang on these rows" was
not "cannot hang", and the grid is what made even the first claim worth anything: a grid that never
reached the shape would give this port the same zero, and upstream's 70 is the proof that it does
reach it. **S44 ported both clamps**, so the limit is gone; it changed none of the 1296 answers, and
the grid is now re-runnable from tracked code with
`python tools/probes/upstream-skip-in-atomic-hang.py --oracle-rows` piped into
`pwsh -File tools/run-oracle.ps1 -Rows`. Pinned by
`BacktrackingVerbTests.A_skip_inside_an_atomic_group_after_an_optional_item_answers_where_upstream_loops_for_ever`,
whose assertions are bounded by a `MatchTimeout` so a regression fails one test instead of hanging
the suite.

**The two clamps were ported by the Phase 6 sync, S44**, as ROADMAP and entry 5's note said they
would be. What the slice measured, because "ported" and "changed something" are different claims:
the branch the clamp guards IS reached - a probe throwing there was hit by this entry's own test,
at `pos=2, limit=4, sliceStart=4` - and the unclamped retreat does go on to find a tail match below
that limit, so the clamp is not decoration. It still moved no answer: the 1296-row grid agrees with
2026.9.10 before and after, and so does the whole suite.

**Consequence for the oracle, and the reason it stays true after the sync.** The recorder now gives
every upstream call a ten-second deadline and records a row it misses as a `timeout` outcome the
consumer skips and counts (S40a, DECISIONS 2026-09-13). That machinery is not this bug's workaround
and does not retire with it: upstream had two other runaway shapes on the open tracker when this was
written (issues 551 and 554), and a generative tester that one hanging row can silence is a tester
that goes quiet without saying so.

---

## 11. A fuzzy match reports change positions that contradict its own change counts

**Status: not filed. ALL FOUR MECHANISMS ARE NOW FIXED HERE - A and B by S47, C and D by S48b
(2026-09-14).** The table below keeps S47's wording for A and B; C and D are rewritten under it.
Upstream's answer contradicts itself, this port reproduced it faithfully, and the owner's rule
(2026-09-12) is that an inherited bug is fixed here before 1.0. S47 found that "the change list and
the counts drift apart" is not one defect with two doors, as this entry said, but **one defect class
with at least four mechanisms**, because upstream saves and restores the COUNTS as a block and
unwinds the CHANGES one item at a time, and nothing keeps the two in step across any construct that
abandons a sub-attempt without backtracking through it.

| # | Mechanism | Which half is wrong | S47 |
|---|---|---|---|
| A | A search restart clears the counts and leaves the list (`start_match`, `:11790-11792`) | the list | FIXED |
| B | A partial match returns from inside a nested section, so the counter holds the innermost section's errors alone | the counts | FIXED |
| C | `POSIX` and `BESTMATCH` candidates leave the list polluted or empty against the saved counts | the list | FIXED (S48b) |
| D | A lookaround under `(?e)` restores a counts block whose changes were unwound item-wise | one of them | FIXED (S48b) |
| E | An ATOMIC GROUP's abandoned sub-attempt leaves its change entry behind | the list | NOT SHARED - pinned (S48b) |
| F | Under `(?r)`, a fuzzy lookahead followed by a general repeat reports its change at the MATCH START | the list | NOT SHARED - pinned (S48b) |

E and F were found by S48b's second sitting and are written up under the table; the "four
mechanisms" count above is A to D, which are the four this port ever shared.

**THE FLAGS ARE PART OF EACH REPRODUCTION** - every one below comes from a wave row, and none of
them reproduces without its flag bits.

C's worst measured case is
`(?r)(?p)(?!(?:[^[\p{L}--[a-z]]]\w([\p{L}||\p{N}])){s<=1})(?:([a]+?)(?P<g3>\p{L})){1i+2d+1s<=3:[^a-z]}`
over `'ﬃﬃ𐐀𐐀𐐀'`, **flags 258** (`0x102`), overlapped `finditer`, whose second match carries a
**fourteen-entry** change list against counts of `(0,0,1)`; with no flags the same pattern gives
`(1,2,0)` and is not the same row. Its twin is
`(?b)(?r)(?p)(\w)(?:\s(?:([\p{L}\p{N}]{2,})){e<=2,s<=1}){1<=e<=2}` over `'A\rAßß aaa'`, **flags
16642** (`0x4102`), overlapped, whose fifth match has counts `(1,0,1)` against an **empty** list. D
is `(?e)([abz])[a\d]{0,}?(?<=(?:(\d?)[A-Z]😀){s<=1,i<=1,d<=1})\b` over `'😀\r\n😀AA'`, **flags 130**
(`0x82`), `search`: counts `(1,0,0)` against a list holding one DELETION at 7. With no
flags that row does not match at all. D was found by S47's own new wave property at seed 4242, on
its first run, which is what that property is for. **Both engines agree on C and D**, so the oracle
cannot see them; only the property can - and it cannot see C either, because a POSIX row has no
positions to count on either side (entry 9), which is why C is written out here by hand.

**HOW S48b FIXED C AND D, 2026-09-14, AND WHAT IT COST.** The fix is the one this entry predicted -
the change list is saved and restored wherever the counts are - and it turned out to need **one
edit, not nineteen**. `PushFuzzyCounts` pushes the change list's LENGTH beside the counts block, and
`PopFuzzyCounts` truncates the list back to it. The nineteen sites then reduce to a single
judgement, made once per site and named in the code: eight pops are **restoring** (`ATOMIC` and
`END_ATOMIC`, `CONDITIONAL` and `END_CONDITIONAL`, `LOOKAROUND` and `END_LOOKAROUND`) and take the
truncation, and three are **merging** and keep a separate `PopFuzzyCountsMerging` that leaves the
list alone - the two `END_FUZZY` arms, where the inner section's changes are part of the answer, and
the `FUZZY` backtrack arm, where backing out of the section means every item in it has already
unwound its own change.

The truncation never GROWS the list: a restore whose sub-attempt unwound below the push point has
nothing to put back, and inventing entries would turn a contradiction into a wrong answer.

**Measured, before and after, on this entry's own rows** (`pwsh -File
tools/probes/port-fuzzy-counts-and-changes.ps1`):

```
C worst, match 2   before  (4,6) counts (0,0,1) changes sub[4]   <- a deletion counted, a substitution reported
                   after   (4,6) counts (0,0,1) changes del[4]   <- agrees, and the span and counts did not move
D                  before  (6,8) counts (1,0,0) changes del[7]   <- a substitution counted, a deletion reported
                   after   (6,8) counts (1,0,0) changes sub[8]   <- agrees, and the span and counts did not move
C twin, match 5    before  (0,4) counts (1,0,1) changes []       <- two errors counted, none reported
                   after   see below: the truncation did not fix this one, the entry 9 fix did
```

**The rows have moved since they were recorded and the contradiction had not**: this entry wrote C's
worst case up as a fourteen-entry list against counts of `(0, 0, 1)`, and on the committed code it
was a one-entry list of the wrong KIND against the same counts. Both are the same desynchronisation;
only the depth changed, because S47's own fixes had already drained most of the stale list.

**C's twin was not the truncation's to fix, and that is the finding worth carrying.** Its list was
too SHORT for its counts, and truncation can only shorten. What fixed it was entry 9's stale-totals
fix, because the twin carries `(?b)` as well as `(?p)` and the `BESTMATCH` walk was ranking on the
discarded candidate's totals. **Two of the three items in S48b's scope were one bug.**

**And fixing it found a NEW upstream defect - see entry 16.** With the twin repaired this port
answers a SEVENTH match, `(0, 9)`, that upstream's `POSIX`+`BESTMATCH` scan drops; upstream's own
`fullmatch` at the identical flags answers `(0, 9)`, and so do its three other doors onto the same
subject.

**What remains, honestly stated.** On D the port and upstream now DISAGREE on the change list, and
there is no upstream reference to settle it: upstream answers one substitution counted against one
deletion reported on the anchored retry as well as on the search, so S47's leak-free question
(`_leak_free_fuzzy`) cannot arbitrate it. The port's list agrees with the port's own counts, which
is the only standard available. The position it reports, `sub@8`, sits one past the end of an
eight-unit subject inside a reversed lookbehind; that is an observation, not a claim that it is
right, and nothing in this slice established it.

**The stopgap STAYED, with its justification changed.** `Match.FuzzyCounts` is still tallied from
the change list only on a partial match - that is S47's fix for mechanism B, a deliberate divergence
in its own right, and unrelated to C and D. `SplitFuzzyChanges`'s `Math.Min(FuzzyCounts.Total, ...)`
bound also stays, but it is now upstream's own line (`for (i = 0; i < count; i++)`, `:20522`) and
nothing else: with C and D fixed the two views agree on every path, so the bound is a no-op here
rather than the thing holding an arbitrary answer down.

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

**What this port answers now (S47).** `(0, 0, 1)` with a **deletion at 3**, which is the edit script:
the winning attempt at position 1 matched `'ab'` and deleted the `[wx]` it had run out of subject for.
The proof that this is upstream's own answer with the leak taken away is upstream's own control -
delete the verb, so the abandoned attempt unwinds the ordinary way, and upstream agrees:

```
'(?:[ab][bc](*PRUNE)[wx]){e<=2}'  'qab' -> (1, 3) (0, 0, 1) ([0], [], [])   # leaked
'(?:[ab](*SKIP)[bc][wx]){e<=2}'   'qab' -> (1, 3) (0, 0, 1) ([0], [], [])   # leaked
'(?:[ab][bc][wx]){e<=2}'          'qab' -> (1, 3) (0, 0, 1) ([], [], [3])   # no verb, and this is ours
```

S40a made the same one-line fix, measured it, and **reverted it**, because clearing the list alone
reddened the two rows S38 had pinned. S47 fixed the two together - the pinned rows were pinning the
contradiction, and the right move was to re-judge them rather than to keep them. Both are pinned by
`FuzzyMatchingTests.A_search_that_restarts_does_not_carry_the_abandoned_attempt_s_errors_into_the_next_one`,
now with that no-verb control beside them.

**A second symptom, and the reason this was found at all.** This port reached the leak on shapes
upstream's optimiser keeps it away from, because it has no start prefilter until Phase 7:

```python
>>> regex.search(r'(?<=(?:[ab][cd]){e<=1})$', 'axc').fuzzy_changes
([2], [], [])          # this port BEFORE S47: ([], [], [1]); after S47: ([2], [], []), so it AGREES
```

`$` has a `search_start_*` twin, so upstream makes ONE attempt, at the end of the subject, and its
first attempt is its winning one. This port attempts positions 0, 1, 2 and 3; the attempt at 1
succeeded *inside the lookbehind*, recorded a deletion and then failed on the `$`. Found by S40's
blind review, which read it as a port defect; it was the same defect as above, reached by a
different door, **and fixing mechanism A removed this divergence rather than adding one**. Pinned by
`FuzzyMatchingTests.A_search_attempt_that_fails_after_a_lookaround_leaves_nothing_behind_for_the_next_one`.

**What the fix cost the oracle, and how S47's second sitting paid it (2026-09-14).** Once this port
stopped reproducing the leak, every wave row on which upstream leaks became a divergence. Measured on
the default wave at 6000 rows and three seeds: **39 rows where upstream's counts are the innermost
section's and ours are the whole match's** (mechanism B, always a partial - 35 carrying positions and
4 POSIX), and **19 rows where the counts agree and the change POSITIONS differ** (mechanism A). The
two are accounted for separately, because only one of them needed a new question put to upstream.

**Mechanism B needed no second question, because upstream's own answer is the evidence.** Its counts
are componentwise no larger than this port's and its reported positions are a PREFIX of this port's,
per kind and in record order - which is what a change stack truncated to a wrong total looks like,
and is not what an engine computing different positions looks like. Measured across all 35 rows that
carry positions: the prefix relation holds on every one, with none failing. Entry
`fuzzy-counts-of-a-partial-are-the-innermost-sections`. Where it is wide is written into the entry:
on 18 of the 35 upstream reports no errors at all, so the prefix it must be is the empty one.

**Mechanism A did.** No predicate over the two answers is narrow enough - upstream's leaked positions
are structurally indistinguishable from a port that computed a position wrongly, and "same counts,
different positions" would swallow exactly the defect the oracle is there to catch. The recorder now
asks upstream the same row again ANCHORED at the span it reported (`match(pos=start, endpos=end)`),
where the winning attempt is upstream's FIRST attempt and no earlier one exists to leak from, and
records that fuzzy half per match as `leakFreeFuzzy` (`_leak_free_fuzzy` in `tools/record-oracle.py`,
the shape `bestmatchFreeOutcome` and `searchOnlyPartial` already have). **On all 15 diverging matches
upstream would answer, its leak-free answer is this port's answer exactly** - counts, kinds and
positions, with no exception. Entry `fuzzy-changes-leaked-from-an-abandoned-attempt`.

**The anchored question cannot always be asked, and that is the weak half of the entry.** Eight of
the 23 diverging matches, in four rows, are shapes where anchoring destroys the question: a fuzzy
section inside a LOOKAHEAD, which has to read past `endpos`; a `\K`, whose reported start is not
where the attempt began; and a scan's second match at a position an earlier match already used. On
those the entry accepts this port's positions with nothing to hold them to. It fires about 1.3 times
per 126,000-row seed, and what covers it instead is
`OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts` and the minimised rows in
`FuzzyMatchingTests`.

**The result.** The default wave is GREEN at three seeds, and the 6000-row three-seed gate went from
24 / 23 / 25 diverging rows to 3 / 2 / 9 - the 14 that remain being exactly the untriaged rows that
were already red at HEAD before S47 touched anything, and none of them a fuzzy-reporting divergence.

### Two more doors, found by S48b's second sitting (2026-09-14) - E and F, and NEITHER is shared

The defect class is the same - upstream abandons a sub-attempt without unwinding its change entries
- but these two are doors mechanism A's anchored question cannot open, because the leak is inside
ONE attempt rather than left by an earlier one. **This port does not share either**, so each is
pinned in the oracle rather than fixed, and each carries a permanent test.

| # | Mechanism | Upstream's own control | Oracle entry |
|---|---|---|---|
| E | An ATOMIC GROUP's abandoned sub-attempt leaves its change entry behind | spell the `(?>` as `(?:` | `atomic-group-leaks-a-change-position` |
| F | Under `(?r)`, a fuzzy section inside a LOOKAHEAD followed by a GENERAL REPEAT reports its change at the MATCH START | match the same pattern FORWARD | `reversed-lookahead-change-at-the-match-start` |

**E**, row 74033 of the seed-20260914 6000-row gate:
`^(?:\p{Ll}\w??[a-f]){1i+2d+1s<=3}(?>(?:\p{Ll}(?:\p{L}){s<=1,i<=1,d<=1}){d<=1})$` over `'AA𝔘𝔘'`, no
flags, `search`. Both engines answer the span, the counts `(2,1,1)`, the same two substitutions and
the same insertion; upstream puts the DELETION at codepoint 2 and this port at 3. Replace the `(?>`
with `(?:` and upstream moves to 3. The row's own `leakFreeFuzzy` agrees with upstream's drawn
answer, which is mechanism A's question failing to see this one in a single field.

**F**, row 73463 of the seed-7 gate, minimised to four constructs, three characters and no flags at
all. `A(?=[^A]{e<=1})A+\D` over `'AAA'`: matched FORWARD upstream answers `(0, 3)` with the
substitution at 1 - where `[^A]` was tested, one past the leading `A` - and with `(?r)` added, which
picks the same candidate at the same span and count, it answers **0**. The condition is a GENERAL
repeat after the lookahead and was measured, not guessed: with `A+` made a fixed `A` and nothing
else changed, both directions answer 1; and `AA(?=[^A]{e<=1})A+\D` over `'AAAA'` answers 2 forward
and **0** reversed, which is what killed the first draft's claim that the shift was the lookahead's
offset.

**F is the one S48b itself moved, and that is worth saying plainly.** Before S48b this port
reproduced upstream's answer on the wave row exactly, leak and all; the `PopFuzzyCounts` truncation
that fixed C and D moved it onto upstream's own forward answer. Verified by bisecting against a
worktree at `c8165b5` and by flipping the truncation's comparison, which restores upstream's answer
at every prefix length. The drawn row also carries an earlier attempt's leak on top of F, so the
oracle entry judges the positions rather than claiming the whole row reduces to one line of
upstream.

Both re-runnable: `python tools/probes/upstream-posix-and-atomic-free-answers.py` and
`python tools/probes/upstream-reversed-lookahead-change-position.py` (the second exits non-zero if
upstream stops behaving this way). Pinned by
`Gaps/Engine/FuzzyCountsAndChangesTests.An_atomic_group_reports_the_deletion_the_cut_free_pattern_reports`
and `.A_reversed_lookahead_reports_its_substitution_where_the_lookahead_tested`.

**Related:** entry 7, the other inherited bug on Phase 6's list, and entry 9, whose POSIX
`fuzzy_changes` crash is mechanism C seen from the C side.

---

## 12. `BESTMATCH` loses a match that plain fuzzy matching finds, when the best fit needs two trailing insertions - FIXED HERE (S46)

**Status:** not filed. Nothing is filed until everything else in the plan is done (owner decision,
2026-09-12); this entry is drafted here and re-verified against the then-current release first.

**FIXED IN THIS PORT ON 2026-09-14 (S46), AND ONE SENTENCE BELOW WAS WRONG ABOUT THE SYMPTOM.** The
mechanism this entry names is right to the line and was confirmed on the pinned release; what was
wrong is "*n* trailing insertions need `max_errors` above *2n-1*", which reads as though a large
enough budget buys the match back. It does not. Under `(?b)` the user never sets `max_errors`: the
second pass sets it to `fewest_errors` = *n* itself, so the doubled guard needs *n > 2n-2*, which is
false for every *n >= 2* **at every budget**. Measured on `regex` 2026.9.10, 2026-09-14,
`tools/probes/upstream-bestmatch-trailing-insertions.py`, over `fullmatch` of `(?:x){e<=N}` against `'x'` plus *k* trailing
characters:

```
              N=0   N=1   N=2   N=3   N=4   N=5   N=6        (i<n> = matched, n insertions)
plain  k=2      -     -    i2    i2    i2    i2    i2        matches exactly when N >= k
(?e)   k=2      -     -    i2    i2    i2    i2    i2        matches exactly when N >= k
(?b)   k=2      -     -     -     -     -     -     -        never matches, at any N
(?b)   k=1      -    i1    i1    i1    i1    i1    i1        k <= 1 is the whole of what survives
```

Leading insertions and substitutions are unaffected under `(?b)`, which is what places the defect in
the trailing-insertion arm rather than in the flag.

**The fix here is this entry's own proposed fix**: `Matcher.cs`'s `END_FUZZY` backtrack arm now reads
`TotalErrors(state.FuzzyCounts) < state.MaxErrors`, with upstream's second term dropped.

**There is no second engine to ask, and that was MEASURED rather than asserted** - amendment 16 asks
for a real run of one, so the absence has to be evidence too. `python
tools/probes/pcre2-has-no-fuzzy-matching.py`, on the `pcre2` binding 0.7.1 over libpcre2 10.47,
2026-09-14: PCRE2 does not merely lack fuzzy matching, it reads the suffix as **literal text**, which
is worse than an error because a comparison built on it would answer confidently and wrongly -
`pcre2.compile(r'(?:x){e<=3}').match('xyz')` is `None` and `.match('x{e<=3}')` is `(0, 7)`. Only
`(?b)` fails loudly, and only because PCRE2 has no such flag. Perl and .NET have no approximate
matching either; TRE and agrep do, and neither implements upstream's `{...}` syntax or its
`BESTMATCH` ranking, so neither would be answering this question.

So the judgement rests on upstream's own definition and on self-refutation: `BESTMATCH` is documented
as a ranking flag ("By default, fuzzy matching searches for the first match that meets the given
constraints ... The `BESTMATCH` flag will make it search for the best match instead",
`upstream/README.rst:592`), so it chooses among the flagless engine's candidates and cannot destroy
them all, and the same engine answers the match the moment the flag is deleted. **A report must say
all of this**: a maintainer who reads "no second engine" as "nobody checked" will dismiss it.

**Every row the fix moved lands on upstream's own flagless answer.** Nine rows across the three
default seeds of the 6000-row gate - four where upstream lost the match outright, three where both
engines match and the error mix differs, one `sub` and one `finditer` - and on all nine this port's
answer is upstream's BESTMATCH-free answer EXACTLY, groups, counts and change positions included
(`tools/probes/upstream-bestmatch-free-answer.py`, 2026-09-14). Pinned by
`Gaps/Engine/FuzzyBestMatchTests.Bestmatch_keeps_a_match_that_needs_two_trailing_insertions`, the
`(k, N)` matrix test beside it and
`.Bestmatch_still_refuses_a_trailing_insertion_the_budget_cannot_afford`; accounted for in the oracle
by `bestmatch-loses-a-candidate`, whose discriminator is a new recorded field,
`bestmatchFreeOutcome`. Controls S46-A and S46-B in `tools/controls.json`.

**Reproduction**, on `regex` 2026.7.19 (CPython 3.14, Windows), measured 2026-09-13, and re-measured
unchanged on `regex` 2026.9.10 on 2026-09-14:

```python
>>> import regex
>>> regex.fullmatch(r'(?b)(?:x){e<=3}', 'xyz')
None
>>> regex.fullmatch(r'(?:x){e<=3}', 'xyz').fuzzy_counts
(0, 2, 0)
```

`BESTMATCH` is documented as finding the *best* fuzzy match rather than the first. Here it finds
none, while the same pattern without the flag matches the whole subject with two insertions. The
budget is not the limit - `{i<=2}` and `{e<=3}` both fail, and `{e<=9}` fails too.

**The boundary is exactly two**, which is what names the mechanism:

```python
>>> regex.fullmatch(r'(?b)(?:x){e<=1}', 'xy').fuzzy_counts   # one insertion: (0, 1, 0)
>>> regex.fullmatch(r'(?b)(?:x){e<=3}', 'xyz')               # two: None
```

**Faulting mechanism: `do_best_fuzzy_match` (`:17584`) meeting the trailing-insertion guard in
`basic_match`'s `RE_OP_END_FUZZY` backtrack arm (`:15515-15517`).** That guard is

```c
if (insertion_permitted(state, inner_node, inner_counts) &&
  total_errors(state->fuzzy_counts) + total_errors(inner_counts) <
  state->max_errors && fuzzy_ext_match(state, inner_node, state->text_pos)) {
```

and it DOUBLE-COUNTS. For a pattern with a single fuzzy section, `END_FUZZY` has already merged the
inner counts into `state->fuzzy_counts` (`:12473-12484`), so the two terms are the same errors added
twice: *n* trailing insertions need `max_errors` above *2n-1* rather than above *n-1*.

The first pass never sees it, because it runs with `max_errors` at `PY_SSIZE_T_MAX`; it finds the
two-insertion match and records `fewest_errors = 2`. The second pass then climbs `max_errors` only
from 1 to `min(fewest_errors, RE_MAX_ERRORS)` = 2 (`:17730-17733`), and the widened-slice fallback
uses `fewest_errors` = 2 as well (`:17823`). At 2 the guard refuses the second insertion, so neither
can re-find the match the first pass just found, and the whole call returns no match.

**Proposed fix:** drop one of the two terms, so the guard reads
`total_errors(state->fuzzy_counts) < state->max_errors`, matching every other `max_errors` test in
the file (`any_error_permitted` `:9672`, `this_error_permitted` `:9690`, `insertion_permitted`
`:9708`), each of which asks about ONE set of counts. `insertion_permitted` on the line above already
applies the section's own limits to `inner_counts`, so nothing is lost.

**This port reproduced it faithfully until S46** - `Matcher` carried upstream's line unchanged - and
pinned the behaviour in a test asserting NO match, because that is what both engines answered. The
oracle was blind to it for the reason the roadmap gives: a bug reproduced faithfully shows up as
agreement. That test is now
`.Bestmatch_keeps_a_match_that_needs_two_trailing_insertions` and asserts the match.

**Found by S42's second sitting, 2026-09-13**, while choosing an `ExpectedDivergences` example row
for `bestmatch-ranks-by-cost`. The cost budget does not cause it and does widen its reach: on
`tools/probes/enhancematch-cost-rows.py` at seed 777, nine of 2500 rows match nothing with the cost
walk on and none do with it off, and all nine are `fullmatch` rows whose cheapest fit is
insertion-heavy. That is the honest price of the ranking change until this is fixed, and it is why
the example row for that entry uses substitutions.

**Related:** entries 9 and 11, the other inherited fuzzy bugs the oracle cannot see, and Phase 6's
inherited-bug sweep.


# Added by S43, 2026-09-13

## 13. `BESTMATCH` loses a partial match that the same pattern's own `match` still finds

**Status:** not filed. Nothing is filed until everything else in the plan is done (owner decision,
2026-09-12); this entry is drafted here and re-verified against the then-current release first.
**Mechanism established to the line by S47c on 2026-09-14** - see "Faulting mechanism" below. This
entry is no longer amendment 16 outcome (d).

**Reproduction**, on `regex` 2026.7.19 (CPython 3.14, Windows), measured 2026-09-13:

```python
>>> import regex
>>> p = regex.compile(r'(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)')
>>> p.search('ab.', partial=True)
None
>>> p.match('ab.', 2, partial=True)
<regex.Match object; span=(2, 3), match='.', partial=True>
```

The same compiled pattern answers nothing from `search` and a partial from its own anchored `match`
at a position inside the searched region. **The judgement needs no second engine**, and it rests on
two arguments of different strength that a report must keep apart:

* **The strong form, on this minimised shape and on FOUR of the five wave rows**: a search that
  finds nothing where its own anchored `match` finds something is wrong however the ranking rule is
  defined. 76681 and 76593 answer by `pos`; 74938 and 77937 are `(?r)` and answer by `endpos`, which
  is where a reversed pattern anchors - 74938 at endpos 1, 77937 at endpos 2, 4 and 5.

  **This entry has stated that scope wrongly three times, and the sequence is worth keeping**: the
  first draft claimed all five with no evidence; a blind review cut it to two; a second review found
  that cut was an artefact of sweeping `pos` alone, because a reversed row has no `pos` answer to
  find; and a third draft then over-corrected back to all five. The measurement is four. A report
  that overclaims here is a report that gets dismissed, which is why the count is now carried beside
  every statement of the argument rather than summarised once.

  **The caveat that makes it four, and the report must carry it**: truncating a FORWARD pattern's
  subject with `endpos` changes what a trailing `$`, `\Z` or lookahead means, so an endpos hit is a
  contradiction only for a pattern that reads nothing at the end. Row 76251 is forward and its only
  anchored answer is the degenerate empty slice at endpos 0, which this caveat disqualifies, so that
  row rests on the weak form alone.
* **The weak form, on all five and on this shape**: deleting `(?b)` gives upstream a match it
  refused with the flag present. `BESTMATCH` is documented as choosing the *best* match rather than
  the first; it is not a filter that removes matches, so a flag that turns a match into no match is
  upstream contradicting its own documentation.

  **On four of the five that flagless answer is this port's answer in FULL - groups, counts and all
  - and on row 77937 only the SPAN agrees.** Upstream flagless spends no errors and captures nothing
  there, where this port answers the same (0, 7) span with `fuzzy=(1,1,1)` and group 2 set. So 77937
  rests on "upstream refused a match it finds without the flag" and NOT on "upstream's flagless
  answer is ours". The probe compared spans alone while this entry claimed whole answers; it now
  prints the groups and the counts, so the claim and the measurement are the same thing.

Two of the five are not `search` rows at all - 74938 is a `match` and 76251 a `fullmatch` - so the
defect is not confined to the search loop.

The weak form, shown:

```python
>>> regex.compile(r'(?:ab){e<=1}(?:\S(*SKIP)\w|\W)').search('ab.', partial=True)
<regex.Match object; span=(0, 3), match='ab.', partial=True>
```

**Four conditions, each necessary on this shape** (`python tools/probes/upstream-bestmatch-loses-a-partial.py`):

* `(?b)` - `(?e)` in its place keeps the match, so it is `do_best_fuzzy_match` and not the fuzzy
  ranking modes in general;
* a fuzzy section;
* a `(*SKIP)` - the same pattern without the verb keeps its match under `(?b)`;
* `partial=True`.

Upstream's anchored walk under `(?b)` is also SHORTER than the same walk without it - on `'ab.'` the
flag drops the partials at starts 0 and 1 and keeps 2 and 3 - so the loss is not only in the search
loop.

**Faulting mechanism: ESTABLISHED TO THE LINE, S47c, 2026-09-14.** A `/Od /Zi` build of the pinned
2026.9.10 source, instrumented with `fprintf` and run. It is a LEAK ACROSS THE TWO ATTEMPTS THAT
MAKE ONE MATCH, and not a ranking rule at all:

1. **`_regex.c:14555`** - `RE_OP_SKIP` sets `state->slice_start = state->text_pos`. On the minimised
   shape the trace reads `SKIP :14555 slice_start 0 -> 3`, and it happens during the NORMAL
   (non-partial) attempt, which then fails.
2. **`_regex.c:18170`** - `do_match`'s partial fallback restores `text_pos` ALONE. The slice stays
   where `(*SKIP)` left it, so the second attempt runs with `slice=[3,3]` and `text_pos=0`.
3. **`_regex.c:17625`** - `do_best_fuzzy_match`'s scan loop is guarded by
   `state->slice_start <= start_pos && start_pos <= state->slice_end`. With `slice_start=3` and
   `start_pos=0` that guard is FALSE, the body never runs once, and `status` keeps the
   `RE_ERROR_FAILURE` it was initialised with at `:17599`. **This is the discard: the partial is not
   ranked and rejected, it is never attempted.**

The trace, from `python tools/probes/upstream-bestmatch-lost-candidate.py --trace`, for the `(?b)`
search of the minimised shape and then for the flagless one:

```
[LC] ENTER best search=1 text_pos=0 slice=[0,3] partial_side=-1
[LC]   scan :17625 GUARD PASSED start_pos=0 slice=[0,3]
[LC]     SKIP :14555 slice_start 0 -> 3
[LC]   scan :17641 basic_match -> status=0 total_errors=0 match_pos=3 text_pos=3
[LC]   scan :17648 break on FAILURE
[LC] RETURN :17857 status=0 (0 is RE_ERROR_FAILURE)
[LC] do_match :18170 partial retry: text_pos restored to 0, slice LEFT at [3,3]
[LC] ENTER best search=1 text_pos=0 slice=[3,3] partial_side=1
[LC] RETURN :17857 status=0 (0 is RE_ERROR_FAILURE)
ANSWER: None

[LC] ENTER simple search=1 text_pos=0 slice=[0,3] partial_side=-1
[LC]     SKIP :14555 slice_start 0 -> 3
[LC] do_match :18170 partial retry: text_pos restored to 0, slice LEFT at [3,3]
[LC] ENTER simple search=1 text_pos=0 slice=[3,3] partial_side=1
[LC]     SKIP :14555 slice_start 3 -> 3
ANSWER: <regex.Match object; span=(0, 3), match='ab.', partial=True>
```

Read the second call of each pair. Under `(?b)` there is no `GUARD PASSED` line at all - the loop
never ran. **The hypothesis this entry carried before S47c named the right guard for the wrong
reason:** it blamed the retry's `start_pos = state->match_pos`, and the trace shows the loop is
refused on its FIRST iteration, before any retry exists.

**Why the flag matters, and it is not a filter.** `do_simple_fuzzy_match` is handed the SAME leaked
`slice=[3,3]` - the trace prints it - and still answers, because it has no such guard: it calls
`basic_match` from `text_pos` and lets the match walk. `(?b)` does not remove the match; it routes
the retry through the one entry point whose loop guard the leaked bound falsifies. That also
explains this entry's own observation above, that the anchored walk under `(?b)` is shorter: starts
0 and 1 fall outside the leaked slice and start 2 does not.

**`do_enhanced_fuzzy_match` already restores the slice before every return that is not a hard error
(`:18003`; the `goto error` at `:18001` skips it, and that path aborts the whole match anyway)**,
which is upstream's own statement that the slice is per-attempt state. `do_best_fuzzy_match` restores
it only inside its `found_match && fewest_errors > 0` branch (`:17848`), so an attempt that merely
fails leaks. That asymmetry between siblings is the defect in one sentence.

**The mechanism has two arms**, because `RE_OP_SKIP` writes `slice_end` rather than `slice_start`
when the node is `RE_STATUS_REVERSE` (`:14553`). Which arm each pinned row fires is printed by
`... --trace`: the two `(?r)` wave rows, 74938 and 77937, take the reversed arm and the other three
take the forward one. The reversed twin of the minimised shape,
`(?b)(?r)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)` over `'.ab'`, loses its partial the same way and is the
MINIMISED witness of that arm; it is now row 7 of this entry's pin.

**Proposed fix - two, both measured, and upstream's own suite is 101 run / 0 failed under each.**

* **A.** `do_match` saves `slice_start`/`slice_end` beside `text_pos` at `:18155-18159` and restores
  them at `:18170`. **This is what this port already does**, at `Matcher.cs:10098-10100`, chosen in S40b
  on self-refutation grounds before the upstream mechanism was known. It also reaches the
  non-BESTMATCH retry, so it changes one flagless answer: row 77937 gains `fuzzy=(1,1,1)` and group
  2.
* **B.** `do_best_fuzzy_match` saves the slice at entry and restores it on every return, as its
  sibling does. Narrower: it changes nothing outside `(?b)`.

Both fix the minimised shape, its reversed twin and all five judged wave rows. **Recommend A**,
because it puts the per-attempt reset where the two attempts actually meet, and because this port
has run it since S40b with the oracle evidence to show for it. B is the smaller diff and the easier
sell if upstream would rather not touch the flagless path.

**With either fix, upstream's answer UNDER `(?b)` is this port's answer IN FULL - span, groups and
fuzzy counts - on all five judged rows, 77937 included.** That retires this entry's long-standing
caveat that 77937 agreed on the span alone. The flagless answer was only ever a stand-in for what
upstream would say without the defect, and there is now a build without the defect to ask instead.

**Reproduce:** `python tools/probes/upstream-bestmatch-lost-candidate.py` (the plain contradiction,
no compiler needed), `--trace` (the instrumented build and the trace above), `--fix` (stock, fix A
and fix B built side by side, each asked the minimised shape, the five wave rows and upstream's own
suite). The builds need MSVC and `REGEX_VCVARS` overrides the search; everything is built under
`.scratch/` and `upstream/` is never written to.

**What this port answers, and why it is right.** The partial upstream's own anchored `match` reports,
and the same answer upstream gives once `(?b)` is removed. Pinned by
`Gaps/Engine/FuzzyBestMatchTests.Bestmatch_keeps_a_partial_that_upstreams_own_search_loses_beside_a_skip`
and its negative control
`.Bestmatch_without_the_verb_keeps_its_match_on_both_engines`.

**Found by S43's composed `interactions` wave**, at the Phase 5 close, in the first three-seed
6000-row run that drew fuzzy beside Phases 3 and 4. Five rows of that wave are this family - seed 7
rows 74938 (`match`) and 77937 (`search`), seed 4242 rows 76251 (`fullmatch`) and 76681 (`search`),
seed 20260913 row 76593 (`search`) - and all five carry `(?b)`, a fuzzy section, a `(*SKIP)` and
`partial=True`, and all five are recorded `nomatch`. All five are now classified in
`ExpectedDivergences` as `bestmatch-loses-a-partial`, keyed on the row and on this port's answer to
it, and the default wave is green at 6000 rows at all three seeds.

**S46 CHECKED THE PORT HALF AND IT WAS ALREADY CLOSED, 2026-09-14.** The slice was written to "fix so
the port answers what its own `match` answers"; it already did, and had since S43 - this port never
reproduced this defect, which is exactly what the five classified rows above say. Nothing was
changed for this entry.

**S47c CLOSED THE UPSTREAM HALF, 2026-09-14.** Mechanism established to the line, fix proposed and
proven, probe committed, pin widened by one row. Still not filed: the owner rule stands. A draft
report is at `docs/plan/upstream-reports/entry-13-bestmatch-loses-a-partial.md`. **Why this port
never reproduced it is now a fact rather than a coincidence:** S40b restored the slice beside
`text_pos` at `Matcher.cs:10098-10100`, which IS fix A, on this port's own self-refutation evidence
and before anyone knew what it corresponded to upstream.

**What S46 did add is a second instance nobody had judged, and a machine-checkable form of the weak
argument.** Seed 20260914 row 76345 -
`(?b)(?e)\b(?:\p{Ll}(*SKIP)[^\d]|\W)(?=(?:(\p{ASCII}+)([^\d]*)a){e<=2,s<=1})` over `'aaa'`, upstream
no match against this port's `(0, 3)` partial - carries this entry's four conditions exactly and was
diverging UNCLASSIFIED at HEAD before S46. It is now accounted for by `bestmatch-loses-a-candidate`,
whose predicate is this entry's own **weak form** made checkable: the recorder asks upstream the same
row with `(?b)` deleted and records the answer, and the entry fires only where this port's answer IS
that answer. That is strictly narrower than the predicate this entry considered and rejected - "this
port answered a partial where upstream answered nothing", which a port INVENTING a partial also
satisfies - because it demands the whole answer, groups and counts included. This entry keeps its
five judged rows and its own arm above the new one, so it still takes them first.

**Related:** entry 12, the other `BESTMATCH` match-loss, which this port reproduced faithfully until
S46 fixed it, where it never reproduced this one; entries 1 and 5 for `(*SKIP)`.

## 14. A self-recursive call round a fuzzy section that can match empty exhausts memory

**Status:** not filed. Nothing is filed until everything else in the plan is done (owner decision,
2026-09-12); this entry is drafted here and re-verified against the then-current release first.
**FIXED HERE ON 2026-09-14 (S47 sitting 3), AND THE FIX IS A DELIBERATE DIVERGENCE FROM UPSTREAM.**
The characterisation is settled - BOTH unbounded depth and unbounded branching, and a progress guard
bounds only the first - and PCRE2's positional guard is now ported, with one deliberate difference
from PCRE2. See "The fix, as made" below. This is amendment 16's third outcome: an inherited bug
fixed here rather than reproduced, so the report drafted below still stands for upstream.

**Reproduction**, on `regex` 2026.7.19 (CPython 3.14, Windows), measured 2026-09-13:

```python
>>> import regex
>>> regex.search(r'(?:(?R)){e<=1}', 'ab')
MemoryError                                   # in 0.48s
>>> regex.search(r'(?:a(?R)?b){e<=1}', 'aabb')
MemoryError                                   # in 0.87s, base case and all
>>> regex.search(r'(?:a(?R)?b)', 'aabb')
<regex.Match object; span=(0, 4), match='aabb'>   # the same recursion, no fuzzy section, 0.00s
```

**The rule, which accounts for most of what is measured.** A group that calls itself makes progress
only if its body must consume something. A fuzzy section can match the empty string whenever its
budget permits as many DELETIONS as the section has atoms, so a budget reaching n deletions of an
n-atom section is the dangerous one; `{s<=n}` and `{i<=n}` never delete anything, whatever n is.
Whole-pattern recursion is the degenerate case: `(?:(?R)){e<=1}` is a section whose only content is
the recursion, so it matches empty at any budget.

`python tools/probes/upstream-fuzzy-recursion-blowup.py`, on `(?P<g1>(?:Ab){C}(?&g1)?)` over 'AbAb'
and `(?P<g1>(?:Abc){C}(?&g1)?)` over 'AbcAbc', covering every constraint the composed generator can
draw:

```
atoms=2  MemoryError:  {e<=2}  {1<=e<=2}  {2i+1d+1s<=2}   (and {d<=2}, which it does not draw)
         ok:           {e<=1} {s<=1} {i<=1} {d<=1} {e<=2,i<=1} {e<=2,s<=1}
                       {s<=1,i<=1,d<=1} {1i+2d+1s<=3}
atoms=3  ok: all eleven, and {e<=3} MemoryError
```

`{e<=2}` blows up on the two-atom shape and is safe on the three-atom one, which is what makes this
a rule about the budget against the atom count rather than a list of unlucky constraints.
`{2i+1d+1s<=2}` prices a deletion at 1 against a budget of 2 and so reaches two of them, while
`{1i+2d+1s<=3}` prices one at 2 and reaches only one.

**IT IS A PREDICTOR AND NOT A PROOF, and a report must say so.** Two of the eleven defeat it:
`{e<=2,i<=1}` and `{e<=2,s<=1}` cap the total at two, cap no deletions, and are nevertheless safe in
0.00s where the bare `{e<=2}` blows up. Why a compound constraint behaves differently has not been
established - no mechanism was measured.

**Faulting mechanism.** Not established beyond the rule above; it is upstream's known
resource-blowup family (issues 551 and 554) reached by a new shape. A report should carry the table,
which is the part that is new.

**Proposed fix, and it is now a NAMED one - PCRE2 has the guard already (S47, 2026-09-14).** Design
spec amendment 16 asks for a real run of a second engine rather than an argument, and PCRE2 cannot be
shown the pattern itself, because it has no fuzzy matching at all and reads `{e<=2}` as literal text
(measured for entry 12). It CAN be shown the same mechanism with the fuzzy section replaced by an
ordinary optional atom - a group that calls itself with a body that need not consume anything - and
`python tools/probes/pcre2-bounds-an-unbounded-recursion.py`, pcre2 0.7.1 over libpcre2 10.47,
2026-09-14:

```
'(?P<g1>(?:a?)(?&g1)?)'    -> LibraryError: nested recursion at the same subject position
'(?P<g1>(?:a*)(?&g1)?)'    -> LibraryError: nested recursion at the same subject position
'(?P<g1>(?:ab)?(?&g1)?)'   -> LibraryError: nested recursion at the same subject position
'(?:(?R))'                 -> LibraryError: nested recursion at the same subject position
'(?:a(?R)?b)'              -> (0, 4)          # the progressing recursion, which everyone answers
```

So the mature answer is neither an allocator that keeps asking nor a generic memory bound: it is a
**specific match-time guard** - `PCRE2_ERROR_RECURSELOOP` - that detects re-entering a recursion at a
subject position it is already at, which is precisely the condition under which no progress is
possible. It costs microseconds and it names the fault, where a memory bound costs a second and a
gigabyte and names only itself. Note that PCRE2's guard is *positional* rather than a progress proof,
so porting it is not a transcription: a shape where the same position is re-entered and a DIFFERENT
branch would still have succeeded would change answer. **That was the measurement sitting 3 made, and
it is why this port fails the path where PCRE2 fails the match** - see "The fix, as made".

**The fix, as made (S47 sitting 3, 2026-09-14).** `Matcher`'s `GROUP_CALL` refuses a call that would
re-enter call-ref index `i` at text position `p` while a call of `i` at `p` is still open. The state
is `MatchState.ActiveCalls`, a set keyed on `(index, position)` and kept in step with the sstack -
which IS the call stack - by saving the key beside the caller's frame and taking it off again at
`GROUP_RETURN`; `start_match` clears it, which covers a verb that truncates the backtracking rather
than unwinding it.

**One deliberate difference from PCRE2, and it is the whole of the design judgement.** PCRE2 fails
the MATCH with `PCRE2_ERROR_RECURSELOOP`; this port fails the PATH and carries on. Refusing one
infinite path cannot cost an answer that any other path reaches, and it demonstrably keeps answers
PCRE2 throws away: `(?P<g1>(?:ab)?(?&g1)?)` over `'abab'` is an error there and (0, 4) here, and
`(?P<g>(?&g)a|b)` over `'ba'` gets left recursion's one-step unrolling, (0, 2), rather than nothing.
The re-entry is refused, the alternative branch is not.

**What it changed, measured rather than argued.** Every shape in the tables above now answers in
microseconds, and answers what its non-vanishing sibling answers - `(0, 4)` for the two-atom family,
`(0, 6)` for the three-atom one. The degenerate `(?R)` rows answer `no match`, which is right: a
pattern whose only content is a call to itself has an empty language. On the oracle, the 6000-row
three-seed gate (126,000 rows a seed) gives the IDENTICAL row-for-row result with the guard and
without it at seeds 7 and 4242, and one row FEWER diverging at seed 20260914 - row 72179 of
`interactions`, a drawn self-recursive call this port used to exhaust its stack on, where upstream
answers `no match`. That row is pinned as
`FuzzyRecursionTests.A_drawn_wave_row_upstream_only_escapes_through_its_prefilter`, and its own
footnote matters: upstream's `no match` there is its required-string prefilter refusing the subject
before the engine runs, not an answer from its engine. Put the required character into the subject
and upstream blows up like every other row here.

**The 1GB bound stays, and is still the backstop.** The guard bounds the DEPTH of a recursion and
does nothing about the BRANCHING a fuzzy section offers at every position of every level, so
`ByteStack.Grow`'s `InvalidOperationException` is still reachable - by
`(?P<g1>\p{L}*)+?(?:ab){e<=1}` over `'bb.a\r.'`, which holds no group call at all and which upstream
also cannot answer. Pinned by
`FuzzyRecursionTests.The_stack_bound_is_still_what_catches_a_blowup_the_guard_cannot_see`, because
otherwise nothing in the suite would reach that path any more.

**Consequence for the oracle, now the other way round.** `INTERACTION_FUZZY_WRAPPERS` gained its
`call` arm on 2026-09-14, so the generator draws a self-recursive call round a fuzzy section again -
14 rows of 600 at seed 1, three of which upstream answers `resource` and the consumer skips. Both of
the old objections went with the guard: the cost one, because this port now answers those rows in
microseconds instead of spending a second and a gigabyte, and the blindness one, because the rows
upstream CAN answer are exactly the ones the guard must not touch and drawing them is the only
instrument that shows it does not. The earlier attempt to keep the cell by forcing progress
(`(?P<g1>A(?:Ab){C}(?&g1)?)`) is still not sufficient on its own and is not what landed: progress
bounds the depth and does nothing about the branching.

**Related:** issues 551 and 554, the resource blowups on Phase 6's triage list.

---

# Added by S44, 2026-09-13

## 15. `(*SKIP)` blocks the one repeat retreat a partial match needs - A REGRESSION IN 2026.9.10

**Title:** Issue 613's retreat clamp loses a partial match that 2026.8.12 and PCRE2 both find

**This one is new, and it is upstream's newest release that is wrong.** Every other entry in this
file describes something upstream has had for a while. This appeared between 2026.8.12 and
2026.9.10, and it is a side effect of the fix for issue 613.

Found by S44's own sync gate rather than by a probe: moving the pin re-ran the three-seed 6000-row
default wave against the new release, and exactly **one row of 378,000** changed answer - row 98050
of seed 20260913, `partial` generator.

**Reproduction**, minimised by hand from the wave's
`\b([İ]+)\1(?:.{3}?(*SKIP)[^[\p{L}--[a-z]]]|\S)` over `'İİSsS'` (flags 266) to three ASCII
characters and no flags:

```python
>>> import regex
>>> regex.compile(r'(a+)\1x(*SKIP)b').search('aax', partial=True)
# 2026.7.19 (== the 2026.8.12 pin, byte for byte under src/ and regex/):
#   <regex.Match object; span=(0, 3), match='aax', partial=True>, group 1 == (0, 1)
# 2026.9.10:
#   <regex.Match object; span=(3, 3), match='', partial=True>,    group 1 unset
```

The lost match is plainly reachable. `(a+)` takes `'aa'`; `\1` cannot match `'aa'` at 2; the repeat
**retreats** to `'a'`; `\1` matches `'a'` at 1; `'x'` matches at 2; and `'b'` runs off the end of
the subject - which is what a partial match is.

**Faulting function.** `basic_match`'s `RE_OP_GREEDY_REPEAT_ONE` backtrack arm, `_regex.c:15859`.
Commit `b77694a` (issue 613) added `if (pos < limit) limit = pos;` and its reversed twin, so that
the arm's equality-only stop is reachable when a `(*SKIP)` has raised `limit` above `pos`. That
stops the runaway retreat it was written for - see entry 10 - and it also stops the single
legitimate retreat step this match needs, because the clamp makes `pos == limit` true immediately
and the arm gives up on the repeat entirely instead of retreating to a smaller count.

**Why this port is right, four ways:**

| evidence | answer |
|---|---|
| `(*PRUNE)` instead of `(*SKIP)` - same pruning, no bound moved | `(0, 3)` on **both** releases |
| the verb deleted | `(0, 3)` on **both** releases |
| **2026.9.10 on `'aaxb'`, where the match completes** | **`(0, 4)`, group 1 `(0, 1)` - the identical retreat, taken** |
| PCRE2 10.47, run not read | `PARTIAL (0, 3)`, and `MATCH (0, 4) (0, 1)` on `'aaxb'` |

The third row is on its own decisive: **2026.9.10 contradicts itself.** It takes the `a+` retreat
to finish a complete match and refuses the same retreat to report a partial one, on the same
pattern one character apart. The first two rows say the moved bound is the cause rather than the
pattern's meaning, which is the argument entries 1, 3 and 5 rest on. The fourth puts a second
engine on record.

**Proposed fix.** Not "revert `b77694a`" - the runaway it fixes is real. The clamp is right about
the walk and wrong about the outcome: when `pos` is already past `limit` the repeat cannot be
retreated *within the current slice*, but the slice is only narrow because a `(*SKIP)` moved it in
a previous pass. Upstream's `do_match` restores `text_pos` between the non-partial and partial
passes (`:18161` at this file's commit, `:18170` at the 2026.9.10 pin - the PRs in the sync range
moved it) and not `slice_start`/`slice_end`; restoring both, as this port does, removes the
precondition and leaves `b77694a` doing only the job it was written for. That is a one-place change
in `do_match` rather than a change to the arm the clamp is in.

**What this port does.** Answers `(0, 3)` with group 1 at `(0, 1)`, which is 2026.7.19's answer and
PCRE2's. It carries **both** of `b77694a`'s clamps - S44 ported them - and keeps the match anyway,
because S40b already restores both slice bounds before the partial pass. So this port is not
diverging by omitting upstream's fix; it has the fix and does not have the precondition.
Pinned by `Gaps/Engine/PartialMatchingTests.A_skip_does_not_block_the_repeat_retreat_a_partial_needs`,
with all four controls, and classified in the oracle as `skip-blocks-a-repeat-retreat-partial`.

**Reproduce:** `python tools/probes/upstream-skip-blocks-a-repeat-retreat.py` and
`... --pcre2`. Measured 2026-09-13, PCRE2 10.47 2025-10-21.

**Related:** entry 10 (the fix that caused it), entries 1, 3 and 5 (the `(*SKIP)` bound-moving
family), and `partial-retry-carried-slice-forward` (the same two-pass restore, seen from the port's
side).

---

# Added by S48b, 2026-09-14

## 16. A `POSIX` overlapped scan of a `BESTMATCH` fuzzy pattern drops its LONGEST match

**Status:** not filed. Nothing is filed until everything else in the plan is done (owner decision,
2026-09-12); this entry is drafted here and re-verified against the then-current release first.

**Found while fixing entry 11's mechanism C**, not by a wave: once this port stopped desynchronising
the counts from the change list, the twin row answered a seventh match that upstream does not, and
checking which engine was wrong turned up upstream contradicting itself four ways.

**Reproduction**, `regex` 2026.9.10, measured 2026-09-14
(`python tools/probes/upstream-fuzzy-counts-and-changes.py`, the middle section). Flags 16642
(`0x4102`, `FULLCASE | VERSION1 | IGNORECASE`); the subject is `'A\rA\xdf\xdf aaa'`, nine
characters, no astral codepoint, so these indices are UTF-16 and codepoint indices alike:

```python
>>> import regex
>>> base = r"(\w)(?:\s(?:([\p{L}\p{N}]{2,})){e<=2,s<=1}){1<=e<=2}"
>>> subj = "A\rA\xdf\xdf aaa"
>>> [(m.start(), m.end()) for m in regex.compile("(?b)(?r)(?p)" + base, 16642)
...                                      .finditer(subj, overlapped=True)]
[(0, 8), (0, 7), (0, 6), (0, 5), (0, 4), (0, 3)]          # no (0, 9)
>>> [(m.start(), m.end()) for m in regex.compile("(?b)(?r)" + base, 16642)
...                                      .finditer(subj, overlapped=True)]
[(0, 9), (0, 8), (0, 7), (0, 6), (0, 5), (0, 4), (0, 3)]  # (0, 9) is there without (?p)
>>> regex.compile("(?b)(?r)(?p)" + base, 16642).fullmatch(subj).span()
(0, 9)                                                    # and upstream's own anchored door finds it
```

**Upstream contradicts itself four ways on one subject, which is the whole of the evidence.**
`(0, 9)` - the whole subject, and the longest match there is - is answered by the scan with
`BESTMATCH` and no `POSIX`, by the scan with `POSIX` and no `BESTMATCH`, by the scan with neither,
and by `fullmatch` at the very flags the failing scan uses. Only `POSIX` **and** `BESTMATCH`
together drop it. The counts agree everywhere it is answered: `(1, 0, 0)`, one substitution at 6.

**Why it is a defect rather than a ranking choice.** `POSIX` is leftmost-**longest**. The single row
the flag exists to guarantee is the longest match at the leftmost start, and that is the one row it
removes here. No reading of the flag makes dropping `(0, 9)` while keeping `(0, 8)` the intended
answer, and upstream's own documentation defines `POSIX` as "leftmost longest" with no exception for
`BESTMATCH`.

**Where it comes from, at the precision the evidence supports.** Not established to a line. The
suspected area is the same desynchronisation family as entry 11: `check_posix_match` (`:11602`)
compares only the LENGTH of a candidate and `restore_best_match` (`:11565`) puts back
`fuzzy_counts` and neither `total_errors` nor the change list, while `do_best_fuzzy_match`
(`:17584`) walks candidates against a budget derived from `total_errors`. This port reproduced the
symptom until `SaveBestMatch`/`RestoreBestMatch` were given the two running totals (entry 9's
port half, S48b), which is suggestive and is not proof about upstream's C. **A report must say it
does not know.** The `/Od /Zi` MSVC build S47c installed is the instrument that could settle it and
was not used here.

**What this port answers.** `(0, 9)` at all four doors and at `fullmatch`, with counts `(1, 0, 0)`
and the substitution at 6 - which is upstream's own answer on the three doors that give one.
`pwsh -File tools/probes/port-fuzzy-counts-and-changes.ps1` prints both halves side by side.

**Related:** entry 11 (mechanism C, the fix that exposed this), entry 9 (the stale running totals
that this port shared), entry 12 and entry 13 (the other two ways `BESTMATCH` loses a candidate).

---

**Entries 17-21 come from S49's upstream issue sweep (2026-09-14) and differ in kind from 1-16.**
Everything above was found by this port's own oracle or by reading upstream's C, so each entry is a
place the two engines *disagree*. These five are the opposite: they are bugs both engines share, so
the oracle reports agreement and sees nothing (design spec amendment 13). They come from the live
tracker instead, re-triaged in `docs/plan/upstream-issues/2026-09-14-triage.md`, and each already
has an upstream issue number, a reporter and in two cases a maintainer's own "it looks like a bug" -
so the report a filing would need is mostly written. What this ledger adds is the measurement that
this port reproduces it, the test that pins the correct answer, and the bar S50 has to clear.

Every reproduction below is re-runnable from the committed tree:
`python tools/probes/upstream-issue-sweep.py` (upstream 2026.9.10),
`pwsh -File tools/probes/port-issue-sweep.ps1` (this port, after a Debug build), and
`python tools/probes/pcre2-partial-truncation-assertions.py` (PCRE2 10.47, entry 21 only).
Each has a failing test skipped `needs:issue-<n>` in
`tests/FuzzyRegex.Tests/Gaps/UpstreamIssues/InheritedIssueTests.cs`, which S50 un-skips.

## 17. Branch reset gives two groups in the same branch the same number (upstream issue 425)

**Status:** not filed; inherited here and **FIXED HERE (S50)** for the shape the issue reports, with
two other orderings parked. Upstream issue 425 is open since 2021-09-28 with two maintainer comments.

**What S50 changed.** `Info.OpenGroup` now skips a number that a reused name has already claimed in
the branch being parsed, which is the maintainer's own **option 2**. `ParseCommon` scopes the set to
one branch and restores it afterwards, because branch resets nest. The pattern above answers
`bug='BUG'`, `groups=('BUG', '!')`. It is a no-op outside a branch reset, where numbers are handed
out in order and never reused, and **no compile-parity corpus row changes** - corpus row 613,
`(?|(?<a>a)(?<b>b)|(?<b>c)(d))(e)`, is the one that would, and it is green.

**Two orderings are PARKED and this entry is explicit about them,** because an earlier draft of the
fix broke upstream's own `test_branch_reset#16-17` by advancing the counter to the reused name's
number instead of merely skipping it. Where the UNNAMED group comes first the collision is the other
way round and option 2 cannot reach it, because the name's number is already fixed by an earlier
branch:

```
(?|(?P<bug>xxx)(!)|(!)(?P<bug>BUG))  over '!BUG'  -> groups=('BUG', None), '!' lost
(?|(?P<n>a)(b)|(c)(?P<n>d))          over 'cd'    -> groups=('d', None),   'c' lost
```

Both reproduce on upstream 2026.9.10 and both still reproduce here. Only **option 3** - "skip group
numbers that have been used anywhere in that branch" - fixes them, and that needs the branch's later
named groups known before its earlier unnamed ones are numbered, which the single-pass parser cannot
do without a source-level pre-scan. The maintainer has not chosen between options 2 and 3, so
building option 3's machinery would be inventing semantics upstream may contradict. Carried as a
named blocker in STATE.md.

**Reproduction**, `regex` 2026.9.10 and this port, measured 2026-09-14:

```python
>>> import regex
>>> p = regex.compile(r'(?|(?P<bug>xxx)(!)|(?P<bug>BUG)(!))')
>>> m = p.match('BUG!')
>>> m.groupdict()['bug'], m.groups(), dict(p.groupindex), p.groups
('!', ('!', None), {'bug': 1}, 2)
```

This port answers `bug='!' bugIndex=1 | g1='!' g2=None` - identical.

**The mechanism.** In the second branch, `(?P<bug>BUG)` takes number 1 because the branch reset
restarts the numbering, and `(!)` *also* takes number 1, because the reset does not skip a number
the branch has already consumed. Two distinct groups then write to one slot and the later write
wins, so 'BUG' is unrecoverable from the match object at all.

**Why it is a defect even though the maintainer asked "is it a bug?", stated carefully, because an
earlier draft of this entry over-claimed and a blind review caught it.** His second comment offers
three candidate rules, and **option 1 is explicitly labelled "(current behaviour)"** - so it is not
true that the status quo matches none of them:

> 1. Number consecutively (current behaviour).
> 2. Number consecutively, but skip group numbers that have been used up to that point in the branch.
> 3. Number consecutively, but skip group numbers that have used anywhere in that branch.

Options **2 and 3 both** give branch 2 the numbers 1 and 2, so both make `bug` reach 'BUG'. Option 1
keeps today's answer. So this entry, and the test, do commit a fixer to rejecting option 1, and the
grounds are not "two options out of three":

- **Under option 1 a capture group's text is unreachable through any API.** `(?P<bug>BUG)` matches
  'BUG' and nothing can retrieve it - not by name, not by number, not through `captures`.
- **Worse, the name resolves to another group's text.** `groupindex` maps `bug` to 1 and group 1
  holds '!', so `m.group('bug')` returns text matched by a different group in the pattern. That is
  not a numbering convention anyone chose; it is two groups writing to one slot.
- **The maintainer calls it the problem himself**, in his first comment: "The problem here is that
  the branch reset is restarting the numbering and it's not skipping over group numbers that have
  already been used in that branch. The question is whether it should."

So what is genuinely open upstream is the choice between options 2 and 3 - they differ only on a
branch's *later* groups - and both fix this row.

**What the test asserts,** deliberately narrower than the issue: `bug` reaches 'BUG', and '!' lives
in some other group. Options 2 and 3 agree on that, so S50 picks between them freely; what the test
does rule out is leaving the behaviour as it is.

**Proposed fix (for the eventual report).** In the branch-reset handler, start each branch's counter
at the same value but advance past any number the branch has already assigned - the maintainer's
own option 2 - so a named group that resolves to an earlier number does not leave its slot free for
the next unnamed group in the same branch.

## 18. A repeated capture group costs hundreds of bytes per repetition (upstream issue 554)

**Status:** not filed; inherited here **and amplified**. Upstream issue 554 is open since
2025-02-17 with no maintainer comment. **PARKED BY S50** - see the end of this entry for why, and
what was ruled out before parking it.

**Reproduction and bisection**, measured 2026-09-14, `fullmatch('(ab)*', 'ab' * n)`:

| n | stdlib `re` | `regex` 2026.9.10 | this port |
|---|---|---|---|
| 1,000,000 | ok, 99 B/rep | ok, 192 B/rep | ok, **611 B/rep** (583 MB) |
| 2,000,000 | ok, 96 B/rep | ok, 192 B/rep | ok |
| 4,000,000 | ok, 94 B/rep | ok, 192 B/rep | **`InvalidOperationException`: 1GB backtracking bound** |
| 6,000,000 | ok, 98 B/rep | ok, 161 B/rep | (not reached) |
| 10,000,000 | ok, 92 B/rep | **`MemoryError`**, 0.6 s | (not reached) |

The `re` and `regex` columns are `tracemalloc` peaks printed by
`tools/probes/upstream-issue-sweep.py`; the port column is `GC.GetTotalAllocatedBytes` from
`tools/probes/port-issue-sweep.ps1`. **All three exclude the subject string** - each probe builds
the subject before it starts measuring, which was checked rather than assumed
(`get_traced_memory()` reads `(0, 0)` at `start()` with a 2,000,041-byte subject already live). So
these are engine allocations, and this port costs about three times `regex`, which costs about
twice `re`.

**Only the port's n=1,000,000 figure is quoted, and that is a correction an independent verifier
forced.** Two earlier drafts of this table quoted per-n figures for the port that would not
reproduce - first a `GetTotalMemory` live delta the verifier measured as 537 B/rep flat where the
draft said 574 then 343, then an allocation counter that still gave 611 on one run and 343 on the
next. The cause was found rather than averaged away: the engine rents its backtracking buffer from
a **process-wide** pool, so a later call in the same process may reuse the earlier one's buffer and
allocate nothing for it, and a fresh pattern object does not help. Cold - the first call in a fresh
process - is 611 B/rep every time. Timings are not evidence here either; the failing call has taken
between 3.4 s and 8.6 s. The 1GB bound is the one figure that is deterministic by construction,
because it is a fixed constant rather than a measurement, and it is what the test asserts.

**This port is worse than the thing it reproduces**, which is the useful finding: it gives up at
4,000,000 where upstream still manages 6,000,000. The failure mode is better - a clear exception
naming a documented 1GB bound rather than a `MemoryError` - but the bound arrives sooner.

**Where the bytes go here**, from the stack of the failing run: `Matcher.BasicMatch`
(`Matcher.cs:5406`) pushes one `MatchBodyTailStateData` block per repetition through
`PushMatchBodyTailStateData` (`:2679`) into `ByteStack.PushSize` -> `PushBlock` -> `Grow`
(`ByteStack.cs:290`), and nothing pops them while the repeat is still running.

**Where they go upstream**, at the precision the evidence supports: **not established.** The shape
is the same - a repeat body that captures must record enough to restore the group on backtracking -
but no line has been identified in upstream's C and a report must say so. The `/Od /Zi` MSVC build
S47c installed is the instrument that would settle it.

**Why it is a defect rather than a fact about backtracking.** `(ab)*` is deterministic: at every
position either `ab` matches or the repeat ends, so there is nothing to backtrack into. An engine
that recognised the body as having no alternative would need O(1) state per repetition, and stdlib
`re` - which is not a sophisticated engine - reaches 10,000,000 where `regex` does not.

**What the test asserts.** `FullMatch("(ab)*", "ab" * 4_000_000)` succeeds - upstream's own ceiling,
not a byte figure that would vary by machine. The 1GB bound is a fixed constant, so the test is
deterministic rather than a race against the machine.

## 19. `\m` before a fuzzy section does not match at position 0 (upstream issue 563)

**Status:** not filed; inherited here. **PARKED BY S50**, together with entry 20, which S50 proved is
the same bug. The mechanism is now established to a line, and two fix designs were built and
reverted - see "What S50 built and why it went back" at the end of this entry, which is the most
useful thing in it. Upstream issue 563 is open since 2025-04-17, and the maintainer's own comment is
"It looks like a bug, but I'm not sure whether I want to fix it in case I break something in the
current codebase."

**Reproduction**, `regex` 2026.9.10 and this port, measured 2026-09-14 - identical on both:

```python
>>> regex.findall(r'\m(?:Y){i}\M', 'XY YX')
['YX']                       # 'XY' is missing
>>> regex.findall(r'\m(?:X){i}\M', 'XY YX')
['XY', 'YX']                 # the same shape, literal 'X', both found
>>> regex.findall(r'\m(?:Y){i}\M', ' XY YX')
['XY', 'YX']                 # the same pattern, one leading space, both found
```

**The three rows are the whole argument.** Changing the literal so the first word *starts* with it
fixes it, and prepending one space to the subject fixes it. Position 0 is the only difference, so
this is `\m` (start-of-word) failing to hold at the start of the subject when what follows is a
fuzzy section that must insert a character before the literal - not a fact about `{i}` or about
the subject's content.

**Where it comes from - ESTABLISHED TO A LINE by S50, and the earlier draft of this paragraph, which
said "not established", was wrong about the area as well.** It is nothing to do with the word
boundary having no preceding character. It is `_regex.c`:10214, with upstream's own comment two lines
above it:

```c
/* Permit insertion except initially when searching (it's better just to
 * start searching one character later).
 */
data.permit_insertion = !search || state->text_pos != state->search_anchor;
```

`search_anchor` is set once per matching operation (`init_match`, :3410) and never per candidate
start position, so **the rule fires at exactly ONE of the positions a scan visits.** The isolating
probe is the same subject and the same winning span answered two different ways purely according to
where the search was told to begin (`python tools/probes/issue-563-anchor-rule.py`, section 2):

```
>>> a = regex.compile(r'\m(?:Y){i}\M')
>>> a.search(' XY', 0)      # span (1, 3) 'XY'
>>> a.search(' XY', 1)      # None
```

**Upstream's premise is false exactly when a zero-width assertion before the fuzzy item holds at the
anchor and NOT one character on.** Starting one character later is then a *different* match rather
than this one minus an insertion, and the match is lost outright.

**The fix is a generalisation of upstream's own behaviour, not a new rule, and that is the strongest
thing in this entry.** `^` and `\A` already escape the rule, because `basic_match` turns a
start-anchored pattern into an anchored match and stops searching - so upstream **keeps** a match
that begins with an inserted character:

```
findall(r'^(?:abc){i<=1}', 'xabc')      -> ['xabc']
findall(r'\A(?:abc){i<=1}', 'xabc')     -> ['xabc']
findall(r'(?m)^(?:abc){i<=1}', 'xabc')  -> []        # the same pattern, no anchoring
findall(r'(?=x)(?:abc){i<=1}', 'xabc')  -> []
```

**Upstream also already allows a runaway leading insertion everywhere except the anchor.** These two
subjects differ by one leading space, and the answer at the same relative position differs:

```
findall(r'\m(?:Y){i}\M', 'q XY YX')   -> ['XY', 'YX']
findall(r'\m(?:Y){i}\M', ' q XY YX')  -> ['q XY', 'YX']
```

## What S50 built and why it went back

**Two designs, both reverted, and each was killed by a different blind review finding.** Neither was
killed by its rule: the one-step-on rule below is right and survived both. What does not work is
holding the answer in a bare field.

**The rule, which the next attempt should keep.** Lift upstream's prohibition at the anchor only when
a zero-width assertion held there AND fails one character on - because only then is "start searching
one character later" a *different* match rather than this one minus an insertion. The narrowing is
not optional: a first version that lifted the rule whenever any assertion had held reddened upstream's
own `test_fuzzy` rows 51, 52, 54 and 56, which is how it was found rather than argued.

**The design, which does not work.** A `MatchState` flag set where the assertion succeeds at the
anchor and read by `AtInsertionAnchor`. It is bare mutable state that the backtracking engine never
saves or restores, so it is wrong in BOTH directions and each review found a different half:

- **Under-clearing.** An assertion that held only on a path the engine then abandoned still pinned
  the anchor. `(?:\bq|)(?:abc){i<=1}` and `(?!\bz)(?:abc){i<=1}` over 'xabc' answered `'xabc'` where
  upstream says `'abc'`.
- **Clearing it in the `Branch` and failed-lookaround backtrack arms fixed those two shapes and left
  the repeats.** `(?:\bq)*`, `(?:\bq)?`, `(?:\bq){0,3}`, `(?:\bq)*+` and `(?>(?:\bq)*)` before
  `(?:abc){i<=1}` over 'xabc' all still answered `'xabc'`; ten of twelve probed shapes diverged.
- **Over-clearing, by the same two clears.** They also discard a pin set BEFORE and OUTSIDE the
  construct, so a semantically inert group after the assertion threw the fix away again:
  `\m(?:z|)(?:Y){i}\M` and `\m(?!q)(?:Y){i}\M` over 'XY' went back to no match, while
  `\m(?:Y){i}\M` matched.

**So the pin has to be part of the backtracking state rather than a field beside it** - pushed and
popped with the frames that abandon a path - **or be replaced by a compile-time analysis**: "every
path from the start node to this fuzzy item passes a position assertion", combined with the same
dynamic one-step-on test. Either is a slice's work with its own oracle pass, and neither should be
improvised at the end of one.

**All of it is pinned** in `Gaps/UpstreamIssues/InheritedIssueTests.cs`, which asserts the inherited
answer plus every row the two attempts broke, so the next attempt has to keep them. Re-run the
evidence with `python tools/probes/issue-563-anchor-rule.py`.

## 20. Loosening a fuzzy budget loses a match (upstream issue 564)

**Status:** not filed; inherited here. **PARKED BY S50 with entry 19, which S50 proved is the same
bug.** Upstream issue 564 is open since 2025-04-17, same date and same maintainer comment as entry 19
("It looks like a bug").

**The reporter's suspicion that the two are related was right, and this is now a finding rather than
a hypothesis:** entry 19's one-clause change turned this row green with no code of its own, so both
budgets answered `['XY', 'Z']`, and reverting entry 19 took it straight back. The route is
`BESTMATCH`'s re-anchoring - the
looser budget makes the candidate walk restart with the anchor at the match start, where the tighter
one never does, and `\m` then holds at an anchor that entry 19's rule had frozen. That is why the
LOOSER budget was the one losing the match.

**Reproduction**, `regex` 2026.9.10 and this port, measured 2026-09-14 - identical on both:

```python
>>> regex.findall(r'(?b)\m(?:Y){1i+1d+1s<=1}\M', ' XY Z')
['XY', 'Z']
>>> regex.findall(r'(?b)\m(?:Y){1i+1d+1s<=2}\M', ' XY Z')
['Z']                        # the LOOSER budget finds fewer matches
```

**Why it is a defect and not a ranking choice.** Fuzzy budgets are monotone by construction: every
candidate that satisfies `<=1` also satisfies `<=2`, because the cost equation is the same and the
bound is weaker. A looser budget may legitimately return a *different, better* match at a given
position - that is what `(?b)` is for - but it cannot make a position stop matching altogether.
'XY' does not reappear in any other form; it is simply gone.

**What the test asserts:** monotonicity - the `<=2` result contains everything the `<=1` result
found - rather than an expected span list. That is true whatever the right answer set turns out to
be, so it does not commit S50 to spans this slice has no authority to fix.

## 21. A partial `fullmatch` denies a prefix whose completion exists (upstream issue 589)

**Status:** not filed; inherited here. **S50 FIXED IT AND THEN REVERTED THE FIX** - see the end of
this entry, which is the most useful thing in it for whoever picks this up. Upstream issue 589 is
open since 2025-10-08 with six comments; the maintainer considers the behaviour correct. **This
entry disagrees with him, on his own documentation and on a second engine**, and nothing below
weakens that - what was wrong was the mechanism, not the verdict.

**Reproduction**, `regex` 2026.9.10 and this port, measured 2026-09-14 - identical on both:

```python
>>> a = regex.compile(r"(?!(True|False)\b)(.*)")
>>> a.fullmatch("Truest")
<regex.Match object; span=(0, 6), match='Truest'>     # a complete match exists
>>> a.fullmatch("True", partial=True)
None                                                  # yet its own prefix is denied
```

**Upstream's documented definition is the first half of the case.** `upstream/docs/Features.html`
line 576: "A partial match is one that matches up to the end of string, but that string has been
truncated and you want to know **whether a complete match could be possible if the string had not
been truncated**." 'True' is a prefix of 'Truest', 'Truest' is a complete match, so a complete match
is possible and the documented answer is a partial.

**A second engine is the other half.** PCRE2 10.47
(`python tools/probes/pcre2-partial-truncation-assertions.py`):

```
(?!(True|False)\b)(.*)  over 'True'    SOFT=PARTIAL (0,4)   HARD=PARTIAL (0,4)
True\b                  over 'True'    SOFT=match   (0,4)   HARD=PARTIAL (0,4)
True\B                  over 'True'    SOFT=PARTIAL (0,4)   HARD=PARTIAL (0,4)
```

The `True\b` row is the mechanism: under SOFT, PCRE2 reports a definite match when it has one, and
under HARD it reports the same span as PARTIAL - so it treats the boundary at the end of the
available text as **unresolved** and escalates, instead of deciding it against text the caller has
declared truncated. Upstream resolves it, which is coherent but produces a false negative.

**Why the false-negative direction matters.** Upstream's own documentation advertises partial
matching for incremental input ("if you wanted a user to enter a 4-digit number and check it
character by character"), and there a false negative tells the user "it'll never match" about input
that will. That is the failure the feature exists to prevent.

**Not the same as issue 367, and this entry is careful about that.** 367 reports the opposite
complaint - a partial that no continuation can complete - and S49 **dismissed** it: PCRE2 answers
PARTIAL on 367's identical rows, and deciding satisfiability in general is not possible (367's own
example encodes primality). 367's false positive is shared by every engine and undecidable; 589's
false negative is shared by no second engine and is decidable at the truncation point. A report
that conflates them will be rejected, and so would a fix that tried to solve both.

**Proposed fix (for the eventual report).** Under `partial`, a word or grapheme boundary evaluated
at the end of the available text should not be resolved against it: it should yield "unresolved",
which fails the match into a partial rather than into a no-match. PCRE2's SOFT semantics are the
model - a definite complete match still wins.

## WHAT S50 BUILT, AND WHY IT WAS REVERTED

**S50 wrote that fix, measured it green, and its own blind review broke it.** The attempt made all
seven word and grapheme boundary predicates answer `PARTIAL` at the right-hand truncation point,
through one helper. It turned the issue's row green and survived two rounds of narrowing. It is
still reverted, and the reason is worth more than the code was.

**Returning `PARTIAL` from a predicate ENDS the match, and the engine had not finished
backtracking.** Measured against the reverted build and against `HEAD`:

```
search(r'(\.+?)\1\b', '..',   partial=True)  -> group 1 was (0, 1); upstream and HEAD give (0, 2)
search(r'(\.+?)\1\b', '....', partial=True)  -> group 1 was (0, 2); upstream and HEAD give (0, 3)
```

The lazy repeat's FIRST try reached the boundary, escalated, and returned before the repeat could
grow - so a partial this port previously got exactly right came back with a truncated capture group.
A live `partial-sliced` wave row at seed 20260915 showed the same thing.

**And the `ExpectedDivergences` entry written for the fix hid it.** The entry classified the
regression as expected, because its predicate compared overall spans and never looked inside a
capture group. An entry that swallows a regression in the fix it accounts for is the rot that file
exists to prevent, and it went with the revert.

**The sound fix is PCRE2's model, and it is not a predicate tweak.** PCRE2 does not return "partial"
from the assertion; it sets a `hitend` flag meaning "the end of the subject was reached while
deciding", lets matching and backtracking run to completion, and only turns a FINAL failure into a
partial. That keeps a definite complete match winning and keeps backtracking whole. Implementing it
here means new match state, a decision about which span a hitend-derived partial reports, and its own
oracle pass - a slice, not a tail-end fix. Carried as a named blocker in STATE.md.

**Two of the reverted attempt's narrowings are worth keeping for whoever does it**, because both were
forced by measurement rather than chosen:

1. **The attempt must have consumed something.** Escalating at a position the match has not reached
   turns every `\b`-leading pattern into a zero-width partial on a short or empty subject: without
   the clause a default three-seed 6300-row wave went from 0 divergences to **8, 5 and 5**, and every
   one of the eight at seed 7 was that shape. It is also the answer this port has already judged
   WRONG - a zero-width partial at the truncation point is what the `search-start-partial` pin
   refuses.
2. **The LEFT-hand twin needs deciding at the same time.** `(?r)\b$` over `''` is the mirror image
   and is a PERMANENT divergence decided 2026-09-12 - on reasoning that quotes the maintainer's
   issue-589 argument, which this entry rejects. Fixing one side and not the other is not a
   principled place to stop, and S50's attempt did exactly that.

**The test now pins the inherited answer** in
`Gaps/UpstreamIssues/InheritedIssueTests.cs`, together with the two `(\.+?)\1\b` rows the reverted
attempt broke, so the next attempt has to keep them.
