# Ledger entry 24: where does a reversed partial match "run out of text"?

A briefing for the owner's ruling. Written 2026-09-15 by the orchestrator from ledger entry 24
(S52 sitting 10), the committed probes, and real runs of regex 2026.9.10 made the same day.
Every behaviour quoted below was measured, not assumed; the commands are at the end.

## 1. The two features involved, in plain terms

**Slicing.** Every matching call in upstream `regex` (and in this port) takes two optional
bounds, `pos` and `endpos` (our `beginning` and `length`). They restrict where a match may be
found: the matched text must lie inside `[pos, endpos)`. Python's `re` documentation describes
the two bounds differently, and upstream inherits that description:

- `endpos`: "it will be as if the string is `endpos` characters long". The string is truncated.
- `pos`: "an index in the string where the search is to start ... This is not completely
  equivalent to slicing the string; the `^` pattern character matches at the real beginning of
  the string ... but not necessarily at the index where the search is to start." The string is
  not truncated; the search merely begins later.

Measured on regex 2026.9.10, that asymmetry is real and deliberate: a lookbehind can see the
character before `pos` (`(?<=a)b` over `"ab"` with `pos=1` matches), `^` refuses `pos=1`, and a
word boundary `\b` looks at the character before `pos`. But the matched text itself may never
extend before `pos`: `(?r)ab` over `"abc"` with `pos=1` finds nothing, because `a` sits at
index 0. So `pos` is a hard bound for what can be matched and a soft bound for what can be seen.

**Partial matching.** Upstream's README: "A partial match is one that matches up to the end of
string, but that string has been truncated and you want to know whether a complete match could be
possible if the string had not been truncated." The canonical use is validating input as it is
typed: `\d{4}` against `"12"` with `partial=True` returns a partial match, meaning "not complete,
but nothing is wrong yet". A partial is reported when the pattern still needs characters and the
text has run out. Forwards, "run out" means reaching `endpos`, and upstream honours the slice
there: `ab` over `"xab"` with `pos=1, endpos=2, partial=True` returns a partial at (1, 2).

**Reverse matching.** The `(?r)` flag makes the pattern consume text right to left. A reversed
pattern that needs more characters runs out on the LEFT. The question of this entry is where
that left edge is when the caller passed a `pos` greater than zero.

## 2. What upstream actually does: two answers

Upstream's engine holds two different bounds and asks the "have we run out?" question against
different ones in different places (`upstream/src/_regex.c`):

```c
/* init_match, :18435-18446 */
/* The documentation says that the end of the slice behaves like the end of
 * the string. */
...
/* Open start and closed end bounds, like in re module. */
state->text_start = 0;          /* the real string start */
state->text_end = end;          /* the SLICE end */
state->slice_start = start;
state->slice_end = end;
```

- **Rule A, the node handlers** (nine sites of the form
  `text_pos <= state->text_start && partial_side == RE_PARTIAL_LEFT`): a reversed match has run
  out only at position 0 of the whole string. At a non-zero `pos` it simply fails: no match.
- **Rule B, the optimiser** (`search_start` at :8400-8405 and the three reversed string-search
  helpers at :8335-8382): the bound is `slice_start`, and running out there returns a partial
  match positioned at `pos`.

Which rule runs depends on whether the optimiser chose the pattern's leading literal as its
search test. That is a speed decision. It must never change an answer, and here it does.

The reproduction, three patterns over the same single visible character, measured on 2026.9.10:

| Call (`partial=True`) | Whole string `"a"` | Same `a` as slice (2, 3) of `"xya"` |
|---|---|---|
| `(?r)ya` | partial (0, 1) | **None** |
| `(?r)ya(.*?)\b` | partial (0, 1) | **partial (2, 3)** |
| `(?r)ya(.*)\b` | partial (0, 1) | **None** |

The middle column is consistent. The right column contradicts itself twice:

- Greedy against lazy: `(.*)` and `(.*?)` differ only in which of several matches is preferred.
  A preference cannot decide whether any match exists. Over one character both can only match
  the empty string, yet one gets a partial and the other gets nothing.
- Minimum width: at the empty slice (2, 2) of `"xyz"`, `(?r)a` needs one character and gets
  None while `(?r)ab(.*?)\b` needs two and gets a partial. Needing more text cannot make it run
  out less.

Upstream's own test suite makes 72 `partial=True` calls and none passes a `pos`, so the two rules
have never met in its tests. The forward direction has no split because `text_end` is the slice
end: a forward partial fires at `endpos` on every path.

**This port inherited both rules** (`Engine/Matcher.cs`, nine `TextStart` sites under a partial
check, plus its own copy of the optimiser's bound). On the 33-cell probe grid the two engines
agree on 23 cells and differ on 10, split both ways. Neither engine is self-consistent, so the
row cannot be pinned by "which engine is right"; a rule has to be chosen first.

## 3. The options

### Option A: the whole string is the bound (position 0)

Make every path follow Rule A. A reversed partial is reported only when the match reaches index 0
of the whole string; at a non-zero `pos` the answer is "no match".

For:
- It follows the letter of Python's `pos` description: the string is not truncated by `pos`, the
  search merely starts there, and text before `pos` exists. "Run out of text" could be read as
  "run out of string".
- Smallest engine change in upstream terms: the optimiser paths would be brought into line with
  the handlers.

Against:
- It denies a partial for text the engine is forbidden to use. The match may not consume anything
  before `pos` (measured: `(?r)ab` over `"abc"`, `pos=1`, finds nothing), so for the purpose of
  matching the text does end at `pos`. Reporting "no match" says "this can never complete", which
  is false: it could complete if the caller widened the window.
- It is asymmetric with the forward direction, where a partial fires at `endpos` even though the
  string continues past it. Both bounds are hard limits on matching; treating one as a truncation
  and the other as a wall is not a principle, it is an accident of `re`'s wording about `^`.
- It makes incremental reversed matching over a window impossible: a caller feeding a buffer
  right-to-left through `pos` would never learn that more text is needed.
- It contradicts upstream's own comment beside the code: "the end of the slice behaves like the
  end of the string". For a reversed pattern the slice's end is its start.
- It contradicts upstream's optimiser, its three string helpers, and its forward side.

### Option B: the slice start is the bound (recommended)

Make every path follow Rule B. The "run out of text" question for a reversed match is asked
against `pos`. Nothing else changes: `^` and `\A` still refuse a non-zero `pos`, lookbehind and
`\b` still see the character before `pos`, exactly as `re` documents. Only the partial
run-out check moves from `text_start` to `slice_start`.

For:
- It matches the definition of a partial match: the pattern still needs characters and the text
  it is allowed to match has run out. That is true at `pos` because the match cannot extend below
  it.
- It makes the two directions symmetric: forward partials fire at `endpos`, reversed partials at
  `pos`, both being the hard limits of the matchable text.
- It is what upstream's comment says, what upstream's optimiser and string helpers do, and what
  upstream's forward side does. Three of upstream's four mechanisms agree with it; only the node
  handlers do not.
- It satisfies the two invariants the current behaviour breaks (greedy and lazy agree on
  existence; needing more text never runs out less), which is what S52c's automatic checker will
  test on every wave row.
- Precedent: .NET's `Regex.Match(input, beginning, length)` documents "The behavior is exactly as
  if the input was effectively `input.Substring(beginning, length)` ... any anchors or zero-width
  assertions at the start or end of the pattern behave as if there is no input outside of this
  range", and with `RightToLeft` the scan runs "from the character at index `beginning + length -
  1` to the character at index `beginning`". .NET treats both ends of a slice as the ends of the
  text. PCRE2's partial match is defined as "the end of the subject string is reached
  successfully, but either more characters are needed to complete the match, or the addition of
  more characters might change what is matched"; PCRE2 has no reverse mode and no `endpos`, so its
  only edge is the subject end, and a partial fires there. Neither library ever answers "no match"
  for a pattern that ran into a caller-imposed edge.

Against:
- It is a change to nine sites of the port's engine and to whatever the port's own optimiser
  bound turns out to be, so it needs its own slice, test-first, with the oracle rows re-judged.
- Upstream's node handlers, the majority of its sites by count, currently do the opposite, so the
  port would disagree with upstream's most common path until upstream fixes it. The ledger and a
  permanent pin record why. Nothing is filed upstream until Phase 8.
- The Python `re` description of `pos` can be read the other way. The reply is that `re` has no
  partial matching and no reverse matching, so its wording was never written with this question
  in mind, and upstream's own comment resolves the ambiguity in favour of the slice.

### Option C: treat the slice as the whole string for everything

Go further than B and adopt .NET's substring semantics wholesale: `^`, `\A`, `\b` and lookbehind
would all stop seeing text outside `[pos, endpos)`.

For:
- The simplest mental model, and the one .NET callers know from `Match(input, beginning, length)`.

Against:
- It changes documented, tested behaviour that upstream, Python `re` and this port share: 1,967
  ported upstream tests assume `^` refuses a non-zero `pos` and lookbehind sees before it. The
  oracle would diverge on every such row.
- It is not what upstream does anywhere, so it is a deliberate API divergence of a much larger
  kind than the bug fix in question, and it would belong in DIVERGENCES.md as a design decision
  rather than in the ledger as a fix.
- It is not needed to resolve the contradiction. Listed for completeness and not recommended.

## 4. Recommendation

**Option B.** Rule that, for a reversed match with `partial=True`, the engine has run out of text
when it reaches `pos`, and report a partial there. Under spec amendment 16 this is outcome (c):
upstream is wrong (it contradicts itself and its own comment), this port inherited the fault, so
it is fixed here, recorded in ledger entry 24 with the mechanism already traced to the line, and
not filed until Phase 8.

Execution: slice S52d after S52c, so that the metamorphic invariant checker proves the fix over
the whole wave rather than over the hand-built 33-cell grid. Scope: the nine `TextStart` partial
sites in `Matcher.cs`, the port's own optimiser bound, red-first tests from the grid, the oracle
rows re-judged, `ExpectedDivergences.cs` pinning upstream's node-handler answers as upstream's
fault with the mechanism, and a DIVERGENCES.md row. Two sittings budgeted.

If you rule for Option A instead, the port keeps the handlers' reading, the optimiser paths are
brought into line with them, and the same rows are pinned the other way; the cost is similar.

## 5. Sources

- Upstream README, "Partial matches" (definition and the `\d{4}` example) and "Reverse
  searching": `upstream/README.rst`.
- Upstream C source, `upstream/src/_regex.c`: `init_match` :18435-18446; node handlers :6747,
  :12173, :13854, :13964, :14206, :14502, :14922, :15065 and others; `search_start` :8400-8405;
  reversed string helpers :8335-8382.
- Python `re` documentation, `Pattern.search(string[, pos[, endpos]])`, quoted in section 1
  (https://docs.python.org/3/library/re.html, read 2026-09-15).
- .NET `Regex.Match(String, Int32, Int32)` remarks, quoted in Option B
  (https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regex.match, read
  2026-09-15).
- PCRE2 `pcre2partial` documentation, definition quoted in Option B
  (https://www.pcre.org/current/doc/html/pcre2partial.html, read 2026-09-15).
- Probes: `tools/probes/upstream-reversed-partial-ignores-the-slice-start.py` (33 cells) and
  `tools/probes/port-reversed-partial-ignores-the-slice-start.ps1`.
- Measurements made for this briefing on regex 2026.9.10 (2026-09-15):

```python
import regex
regex.compile('(?r)ab').search('abc', 1)                                   # None: cannot use text before pos
regex.compile('(?<=a)b').search('ab', 1)                                   # (1, 2): lookbehind sees before pos
regex.compile('^b').search('ab', 1)                                        # None: ^ refuses pos > 0
regex.compile('ab').search('xab', 1, 2, partial=True)                      # (1, 2) partial: forward fires at endpos
regex.compile('(?r)ab').search('xab', 2, 3, partial=True)                  # None            <- rule A
regex.compile(r'(?r)ab(.*?)\b').search('xab', 2, 3, partial=True)          # (2, 3) partial  <- rule B
regex.compile(r'(?r)ab(.*)\b').search('xab', 2, 3, partial=True)           # None            <- rule A
```
