# The quadratic count-to-position conversion: background, options and recommendations

Written 2026-09-01 to support the owner's decision on a defect found while verifying S24, and kept
as the record of why `MatchState.OneUnitPerCharacter` and `Engine/CharacterIndex.cs` exist and why
the alternatives to them were rejected. Every measurement below was taken on this machine on that
date unless a source is cited.

**Outcome (decided by the owner, 2026-09-01):** both recommendations accepted and shipped.

| Decision | Chosen | Where it lands |
|---|---|---|
| A. BMP subjects | Restore upstream's arithmetic behind a one-pass surrogate scan | `1d9f71c` |
| B. Astral subjects | A sampled position table, `CharacterIndex` | `d39a3f1` |
| Rejected | Carry the position (Java's shape), transcode to a codepoint array, rank-and-select now, defer to Phase 7, adopt .NET's code-unit semantics | - |

---

## 1. The words this document uses

### 1.1 How text is stored, and why it matters here

Take the three-character string `a😀b`.

- Unicode gives every character a number, a **code point**: U+0061, U+1F600, U+0062. Three of them.
- A .NET `string` is a sequence of 16-bit **UTF-16 code units**, which reach only U+0000 to U+FFFF.
- `a` and `b` fit in one unit each. U+1F600 does not, so UTF-16 encodes it as **two** units, and
  `"a😀b".Length` is **4**, not 3.
- Those two units are a **surrogate pair**: a **high surrogate** (U+D800-U+DBFF) then a **low
  surrogate** (U+DC00-U+DFFF). Neither half means anything alone.

Two names for the halves of the code point range:

- The **Basic Multilingual Plane** (**BMP**), U+0000-U+FFFF, is what fits in one code unit: nearly
  everything in daily use, including Latin, Greek, Cyrillic, Arabic, Hebrew, Devanagari and common
  Chinese, Japanese and Korean.
- The **astral planes** (formally the *supplementary planes*), U+10000-U+10FFFF, need a surrogate
  pair: emoji, historic scripts, musical notation, rarer CJK extension blocks.

So a **BMP subject** has the same number of characters as code units; an **astral subject** does not.

A `string` is not validated, so it can also hold a **lone surrogate** with no partner. That is one
character *and* one code unit, so it behaves like a BMP character. Python permits the same in a
`str`, so it is testable against upstream.

### 1.2 Cost notation

`O(1)` constant, cost does not grow with input. `O(n)` linear, double the input and double the cost.
`O(n^2)` quadratic, double the input and *quadruple* it. On a 60,000-character subject that is the
difference between roughly 60,000 steps and 3,600,000,000.

### 1.3 The two structures named later

- **Sampled position table** - an array holding the position of every 32nd character. To find where
  character 1,000 starts, read entry 31 (character 992) and step forward 8. The step is the same
  walk the table exists to avoid, but bounded at 31 rather than 1,000.
- **Rank and select** - the textbook alternative: one bit per code unit saying whether a surrogate
  pair starts there, plus running totals per block, queried with the CPU's population-count
  instruction. Truly constant time and about half the memory, at the price of more bit arithmetic.

---

## 2. The defect

The repeat opcodes' backtrack arms convert between a character count and a UTF-16 position four
times per entry, and are entered once per repeat position. Until this change each conversion was a
loop walking the subject one character at a time. Four linear conversions inside an `n`-entry loop
is quadratic.

It applied to any pattern whose repeat is a single character followed by something: `.*?cd`, `a+b`,
`\d{2,}x`. That is most real patterns.

Measured by counting walk steps, which unlike a clock does not depend on machine load. Pattern
`.*?cd` over `'abc' * n + 'de'`:

| subject, UTF-16 code units | walk steps | that divided by n^2 |
|---|---|---|
| 6,002 | 107,999,997 | 2.998 |
| 12,002 | 431,999,997 | 2.999 |
| 24,002 | 1,727,999,997 | 3.000 |
| 48,002 | 6,911,999,997 | 3.000 |

Exactly `3n^2` at every size: `n^2/2` for the position, `n^2` for the remaining-characters count,
and both again on the way out. The `GREEDY_REPEAT_ONE` arm has the same shape.

**On timings in this document.** The machine was under heavy memory pressure that day and became
unresponsive once, so the same suite measured anywhere between 53 seconds and 3 minutes 16. That
variance is itself a symptom - quadratic work is what makes a suite sensitive to load - and it is
why the walk-step counts above, not any wall clock, are the evidence for the complexity claim. An
earlier note recorded 372 seconds for the worst single test; re-measured in isolation it was 69.

---

## 3. Why upstream does not have this problem

A Python `str` is not UTF-16. Since PEP 393 CPython stores a `str` as a *fixed-width* array of code
points, choosing the width from the largest code point present: one byte if every character is
Latin-1, two if every character is BMP, four otherwise. mrab-regex reads that buffer directly
(`_regex.c:18230-18232`) and every character access goes through `state->char_at(text, pos)` where
`pos` is a **code point index** (`:500`, implementations at `:763-798`).

Because the array is fixed width, the k-th character sits at index k. A character count and a
position are the same number, so upstream's arm is four machine instructions:

```c
pos       = state->text_pos + (Py_ssize_t)count * step;
available = step > 0 ? state->slice_end - state->text_pos
                     : state->text_pos - state->slice_start;
max_count = min_size_t((size_t)available, node->values[2]);
limit     = state->text_pos + (Py_ssize_t)max_count * step;   /* _regex.c:16485-16489 */
```

That is not an optimisation upstream chose. It is the only thing there is to write. **So upstream
has no equivalent of `OneUnitPerCharacter`, because upstream has no UTF-16 anywhere.** S19 introduced
`StepBy` and `CountBetween` to bridge the gap, correctly, but as loops.

---

## 4. Does upstream pay the memory cost that rules out a codepoint array?

Worth checking, since option C below is rejected partly on memory. CPython chooses a string's width
from its widest code point, so the cost is per *string*, not per character. Measured, one million
characters each:

| content | bytes | per character |
|---|---|---|
| all ASCII | 1,000,041 | 1.00 |
| all Latin-1 (U+00E9) | 1,000,057 | 1.00 |
| all BMP (U+4E2D) | 2,000,058 | 2.00 |
| all astral (U+1F600) | 4,000,060 | 4.00 |
| **999,999 ASCII plus one emoji** | **4,000,060** | **4.00** |

The last row is the point: one astral character anywhere quadruples the storage of the whole string,
1 MB to 4 MB. .NET's UTF-16 form of that string is 2 MB.

So upstream does pay it. Two differences stop that being an argument for option C. Python pays it
**once per string, at creation, shared by every operation**, where C would allocate a second array
per matching *operation* on top of the .NET string that already exists. And it is the **language's**
storage: a Python program that never imports `regex` pays it too, whereas C would make this library
alone triple the footprint of any astral subject handed to it.

---

## 5. What other UTF-16 engines do

**.NET's own `Regex` counts code units**, so the problem does not arise for it. Measured on four
U+1F600 (4 code points, 8 code units): `.{3}` matches with length 3 and `.{8}` matches all four
emoji. Python's `regex` gives 3 code points for `.{3}` and no match for `.{8}`. Adopting .NET's rule
would dissolve the conversion entirely and is option F below.

**Java is the closest precedent**: UTF-16 strings, code point semantics. Its `Curly` node, which
implements `{n,m}` and therefore `*`, `+` and `?`, carries the position and the count *side by side*
and never converts one into the other (`Pattern.java:4522-4622`, JDK 25):

```java
boolean match1(Matcher matcher, int i, int j, CharSequence seq) {   // reluctant
    for (;;) {
        if (next.match(matcher, i, seq)) return true;
        if (j >= cmax) return false;
        if (!atom.match(matcher, i, seq)) return false;
        if (i == matcher.last) return false;
        i = matcher.last;   // position comes from the atom, never from j
        j++;
    }
}
```

**ICU** documents the same tension from the other side: its text iterator API notes that "direct
arithmetic on index positions is complicated by the fact that character size in native units depends
on the underlying representation", and that moving by a character count "may perform slowly because
an iterator implementation may need to count UTF-16 characters". Its answer is to expose the cost
rather than hide it.

---

## 6. The options

### A. Take upstream's arithmetic when the subject is BMP-only - **chosen**

One flag per matching operation: does this subject contain a high surrogate anywhere? If not, no
character occupies two code units, a count and a position are the same number, and upstream's
arithmetic is exactly correct.

**For.** About 30 lines. Provably equivalent rather than approximately: stepping to the next
character advances two units only across a well-formed pair, and a pair needs a high surrogate.
Restores upstream's own expression, so the fast path is a transcription of `_regex.c:16485` and can
be reviewed against it side by side. Fixes both repeat arms and every future caller of the two
helpers at once. Walk steps on a BMP subject drop to **zero**.

**Against.** An astral subject still walks, which is why B is not optional. Adds one linear pass to
every matching operation, even one that never touches a repeat.

The test is deliberately conservative: a lone high surrogate is one character *and* one code unit,
so the fast path would be safe for it, but telling a lone surrogate from a pair costs a second test
and buys nothing.

**Cost of the scan.** `IndexOfAnyInRange` is SIMD-accelerated: 60 ns at a thousand characters, 40 us
at a million, 35 ms at a hundred million. One short `Match` through the public API costs about 10 us
on a 37-character subject, so the scan is under 1% of it.

### B. Carry the position rather than reconstruct it, the way Java does - rejected

Store the current position alongside the count in the per-repeat record; cache the arm's upper bound
per repeat.

**For.** Fixes astral subjects too, linear for every input, no extra structure and no scan. It is
what a mature UTF-16 engine does.

**Against.** Not smaller: carrying the position removes half the cost, and the other half,
`available`, still needs the upper bound cached with a rule for when that cache goes stale, because
one of the arm's own branches rewrites the repeat's start and the same record is reused on every
re-entry. The cached state must stay correct as the engine saves and restores repeat records on its
backtrack stack, and a stale cached position produces a wrong answer rather than a crash.

**The decisive point is testability, not size.** Option D is a pure function of the subject and the
walk it replaces is a complete reference implementation of that same function, so it can be checked
by running both over every input in an enumerable space. B's state exists only part-way through a
backtracking search: there is no reference to compare it against. Java can afford B because its loop
was positional from the start; our arm is a line-for-line port of C that converts because C could do
it for free.

### C. Transcode the subject to a code point array - rejected

Convert to an `int[]` once per operation and index that. Literally what CPython does; it would
delete both conversion helpers.

**For.** The port becomes *more* faithful: every position would mean what upstream's means, and a
class of translation bugs disappears. Linear for all subjects.

**Against.** Four bytes per character allocated per matching operation, on top of the string that
already exists - 400 MB on a 100 MB subject. The public API reports UTF-16 positions, so every
reported index needs converting back, which needs a lookup table, which is option D's structure
anyway; so C is D plus a large array, not an alternative to it. And it reverses a documented decision
(DECISIONS 2026-08-31) and would touch essentially all of S15-S24.

Kept on file as the "if we ever regret UTF-16 indexing" path.

### D. A sampled position table - **chosen**

`Engine/CharacterIndex.cs` records the UTF-16 position of every 32nd character boundary. A conversion
is one array read, or a binary search over the table, plus at most 31 steps of the ordinary walk.

**Measured**, subject `x` + U+1F600 + `y` repeated then `cd`, pattern `.*?cd`, counting the engine's
single-character step:

| characters | before | after | reduction |
|---|---|---|---|
| 6,002 | 108,030,003 | 290,653 | 372x |
| 12,002 | 432,060,003 | 582,005 | 742x |
| 24,002 | 1,728,120,003 | 1,164,005 | 1,485x |
| 48,002 | 6,912,240,003 | 2,328,005 | **2,969x** |

`3n^2` before, a flat 48.5 steps per character after. In wall clock, 6.25 s to 0.034 s.

**Memory** is one `int` per 32 characters, about 3% of an all-astral subject against option C's 200%.
Built lazily on the first conversion, so a subject holding a surrogate pair matched against a pattern
with no single-character repeat pays nothing, and built only as far as `TextEnd`, so
`Match(hugeSubject, 0, 10)` does not index the whole subject.

**The stride is a tuning knob, not a correctness property.** Halving it halves the residual walk
exactly - 96.5 steps per character at 64, 48.5 at 32, 24.5 at 16, 12.5 at 8 - but below 32 the wall
clock stops improving, because the binary search and call overhead dominate what is left. Setting it
to 33 leaves every test passing, which is the check that a future change to it cannot break anything
silently.

### E. Rank-and-select instead of the sampled table - rejected for now

Truly constant time and about half the memory. It would be no harder to trust, since it is the same
pure function and the same exhaustive comparison would check it. Rejected only because it is more
code doing bit arithmetic to buy something the clock says nothing needs. Named in `CharacterIndex`'s
own documentation as the upgrade path.

### F. Adopt .NET's code-unit semantics - rejected

Section 5. It breaks parity with Python `regex`, which is the project's purpose, and Unicode
Technical Standard #18 requires a surrogate pair to be treated as one code point when matching.

### G. Defer to Phase 7 - rejected

Phase 7 is where optimisation lives, so deferring is the usual answer. Not here, for three reasons.
It is a **porting defect, not a missing optimisation**: upstream's arm is constant-time and ours was
linear, and nothing was traded for anything. Every remaining slice pays for it, and Phases 4-6 are
perhaps thirty slices. And `RepeatTests` already contains a test whose only failure signal is the
suite's runtime going up, so a permanently slow engine does not merely cost time, it disables a test.

---

## 7. How the result compares with upstream

Upstream never answers these questions at all, so the fair comparison is whole match against whole
match. Release build, `.*?cd`, minimum of three runs, same answers from both:

| characters | port, BMP | port, astral | Python `regex`, BMP | Python `regex`, astral |
|---|---|---|---|---|
| 6,002 | 0.0015 s | 0.0054 s | 0.00003 s | 0.00002 s |
| 24,002 | 0.0068 s | 0.0160 s | 0.00009 s | 0.00007 s |
| 48,002 | 0.0130 s | 0.0341 s | 0.00019 s | 0.00015 s |

Three things follow. **Upstream costs the same for astral as for BMP** - that is what a fixed-width
codepoint array buys, and it is a standard this port cannot reach while a .NET `string` is UTF-16.
**Our astral path is now about 2.6 times our BMP path**, not thousands of times. And **against
upstream we are about 68 times slower on BMP and 227 on astral**, where astral was roughly 41,000
times and growing with the square. What remains is a constant factor: the missing prefilters and the
absence of a compiled backend, both Phase 7's business.

---

## 8. Evidence

- **Tests.** Ratchet GREEN. The suite went from a 53 s to 3 min 16 range to a steady 4-6 s.
- **Differential oracle.** Four waves at the time of the change, three at previously unused seeds,
  totalling 24,200 rows: zero divergences.
- **The index is checked exhaustively against the walk it replaces.** Every string of up to four
  symbols from an eight-symbol alphabet - ASCII, Latin-1, BMP, two astral characters, a lone high
  surrogate, a lone low surrogate, a high surrogate that pairs with nothing - is 4,681 subjects. For
  each, index and walk are compared at every position and count, and for shorter subjects at every
  pair of slice bounds including bounds that split a surrogate pair. No random number generator:
  enumerating beats sampling when the space is small enough to enumerate.
- **That test found two real defects in the first draft.** `CountBetween(p, p)` answered 1 instead of
  0 when `p` was inside a surrogate pair; and an assumption that forward and backward stepping agree
  everywhere, which they do not above `TextEnd`.
- **Negative controls**, each a one-line deliberate fault run against the full suite: forcing the
  flag true fails the two astral tests and nothing else; forcing it false fails the BMP complexity
  guard at its 20-second ceiling; `RankCeiling` returning the floor fails all eight index tests; the
  residual walk one step short fails nine including a ported ZWJ family-emoji test; and changing the
  stride from 32 to 33 correctly fails nothing.
- **Two permanent complexity guards** are in `RepeatTests`, one BMP at 240,002 characters and one
  astral at 180,002, both using the engine's own 20-second match timeout as their ceiling so a
  regression fails in twenty seconds rather than hanging the suite for an hour.

---

## 9. What this does not fix

- The constant factor against upstream, about 68x on BMP subjects and 227x on astral. Phase 7.
- The missing prefilters, still deferred to Phase 7 (DECISIONS 2026-08-31). They were never the cause
  here: the test is anchored at the start of the subject, so `locate_required_string` is not what
  makes upstream fast on this input. That hypothesis is now positively ruled out rather than doubted.
- `NextPos` and `PrevPos` disagree about character boundaries above `TextEnd`, because `NextPos`
  stops pairing at that bound and `PrevPos` does not. Surfaced by the exhaustive test sweeping
  positions the engine cannot reach, proven unreachable, and settled by S26: the asymmetry stays,
  documented on `PrevPos`, with the public behaviour pinned by
  `MatchSpineTests.A_length_that_cuts_a_surrogate_pair_leaves_a_lone_surrogate_that_matches_as_one_character`.

---

## Sources

- Upstream, pinned commit `1760a20647f1c2ddcc025128407fe6f7edb905a1` (regex 2026.8.12):
  `upstream/src/_regex.c` lines 500, 763-798, 16485-16489, 18230-18232.
- [PEP 393 - Flexible String Representation](https://peps.python.org/pep-0393/). The per-string width
  choice it describes is what section 4 measures.
- [OpenJDK `java.util.regex.Pattern`](https://github.com/openjdk/jdk/blob/master/src/java.base/share/classes/java/util/regex/Pattern.java),
  read from JDK 25 `src.zip`, lines 4522-4622.
- [`Pattern` API documentation, Java SE 21](https://docs.oracle.com/en/java/javase/21/docs/api/java.base/java/util/regex/Pattern.html)
  and [Unicode Technical Standard #18](https://www.unicode.org/reports/tr18/).
- [ICU `uiter.h`](https://unicode-org.github.io/icu-docs/apidoc/dev/icu4c/uiter_8h.html).
- Python `regex` 2026.7.19 (PyPI build) and CPython `sys.getsizeof`, run locally 2026-09-01.
- .NET 10 `System.Text.RegularExpressions`, run locally 2026-09-01.
