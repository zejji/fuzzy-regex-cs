# C3: a bit-parallel reject stage for fuzzy literals (design note, 2026-10-13)

Status: design only, nothing built. Stage C3 of `.claude/driver/next-session-optimisation-brief-2026-10-13.md`.
Machine rule while this was written: a quiet-machine benchmark held the machine, so no build, test
or grid ran. Every claim about our code cites file:line on main `a3d79986`; every claim about
upstream quotes a run of `regex` 2026.9.10 (`tools/probes/c3-fuzzy-literal-upstream-claims.py`,
under a second). The oracle experiment in section 6 has been calibrated on 10,537 rows but its full
run is still to do.

## 0. The decision in brief

- A Myers bit-vector scan is admissible **only as a reject filter**. It can say "no match can
  start before position s*", never "the match is (s, e, counts)". Upstream's end and error mix
  are the first its backtracker reaches, which no distance rule reproduces (section 6 has rows
  where upstream reports two errors on a span whose distance is one).
- Recommended: **option B**, a forward scan to the first possible match end, then a short reverse
  scan that finds the exact earliest possible start, s*. It is a stand-alone filter where the
  Navarro pieces would be shorter than 3 characters, and a second stage behind them otherwise.
  It is limited to one ASCII literal of at most 64 characters, with a budget under its length.
- Expected gain: FuzzyBudgetThree goes from 55 full fuzzy attempts to 1, so about 74 us becomes
  an estimated 2-5 us. EnhanceMatch and BestMatch should land at an estimated 5-15 us. The
  single-phrase FuzzyPhrase rows should gain 1.3-1.7x (estimated). Every other row should be
  unchanged.

## 1. Terms

- **Literal** `P`, of length **m**: the characters the fuzzy section must match, for example
  `haystack` in `(?:haystack){e<=3}` (m = 8).
- **Budget k**: the most errors any match of the section can carry (derived in section 2.2).
- **Subject** `T`, of length **n**; `T[s:e]` is the text from position s up to, not including, e.
- **Distance**: the Levenshtein distance, the fewest single-character insertions, deletions and
  substitutions that turn `T[s:e]` into `P`. Every error counts as 1.
- **Occurrence**: a span `[s, e)` whose distance to `P` is at most k. Its length is between m - k
  and m + k.
- **Admissible start**: a position s from which at least one occurrence starts. **s\*** is the
  leftmost admissible start at or after the search position.
- **w** = 64, the machine word. **Peq** is Myers' table that maps each character to a word
  holding a 1 at each position of `P` where that character matches.
- **Forward scan**: Myers' algorithm (JACM 46(3), 1999) with a free start. After reading `T[x:j]`
  it knows the least distance of any `T[i:j]` with `x <= i <= j`. It costs O(1) word operations
  per character when m <= w.
- **Reverse scan**: the same scan run on the reversed literal over the reversed text. At each
  start s it knows the least distance of any occurrence that starts at s. This is how edlib finds
  start positions (`edlib.cpp:230-260`, recorded in `.scratch/perf-libs/notes.md`).
- **Attempt**: one run of the backtracking matcher from one start position.

## 2. Admissibility: why a reject-only Levenshtein scan is safe

### 2.1 The relaxation argument

Take any match upstream (or this port) returns for a pure fuzzy literal: span `[s, e)` with
counts (substitutions, insertions, deletions) = (a, b, c). Each counted error is one edit of one
character, and the exact characters in between are aligned one to one. So the errors form an
alignment of `T[s:e]` against `P` with a + b + c edits, and

    distance(T[s:e], P)  <=  a + b + c  <=  k.

So every real match is an occurrence, and every real match start is an admissible start. A filter
that skips only positions that are **not** admissible starts skips no real match. It never picks
one either: the engine still makes every attempt at every position it is not told to skip, in
order (the same argument as the existing filter's, `FuzzyLiteralFilter.cs:34-39`).

The converse fails, which is fine for a filter: per-kind limits, cost equations, the insertion
class (`{e<=1:[a-z]}`) and upstream's "no insertion at the search anchor" rule (`_regex.c:10448-10450`,
"it's better just to start searching one character later") can all make an admissible start fail.
The filter then lets the engine attempt there for nothing, and that is all.

Upstream evidence that the counts can exceed the distance, so only the inequality holds
(oracle calibration witness, `regex` 2026.9.10):

    (?:abcbab){e<=2}  'bcbaba'  ->  span (0, 5)  counts (1, 0, 1)    distance('bcbab', 'abcbab') = 1

### 2.2 The budget k, from the section's constraints

The stage reuses `FuzzyLiteralFilter.MostErrors` (`FuzzyLiteralFilter.cs:553-578`) unchanged. It is
the tightest of three bounds, each enforced on every single error by `Matcher.ThisErrorPermitted`
(`Matcher.cs:8258-8271`) and `InsertionPermitted` (`Matcher.cs:8283-8294`):

1. `e<=k`: the total limit, directly.
2. `i<=a,d<=b,s<=c`: at most a + b + c errors. If any kind permitted is unlimited, this bound is
   infinite.
3. A cost equation `Xi+Yd+Zs<=K`: every error costs at least the cheapest permitted kind, so there
   are at most `floor(K / cheapest)` errors. If a permitted kind costs 0, this bound is infinite.

If all three are infinite, `MostErrors` returns -1 and the filter refuses (`FuzzyLiteralFilter.cs:164`).

Dividing by the **cheapest** kind matters. With `{2i+2d+1s<=4}` the bound is 4 / 1 = 4, not 4 / 2:

    (?:abcd){2i+2d+1s<=4}  'wxyz'  ->  span (0, 4)  counts (4, 0, 0)

A k of 2 would reject `wxyz` (distance 4) and lose this match. A witness test pins this row.

Per-kind limits give a sound but loose bound: `{i<=3,d<=0,s<=0}` gives k = 3 although only
insertions are allowed. A weighted or kind-restricted distance would be tighter, but no
bit-parallel form exists for it. Plain Levenshtein is the relaxation we can scan fast.

**Tighter k at run time** (BESTMATCH). `ThisErrorPermitted` also checks `state.MaxErrors` and
`state.MaxCost` (`Matcher.cs:8268, :8270`), which `DoBestFuzzyMatch` lowers each round
(`Matcher.cs:20119`). So `k_eff = min(k, state.MaxErrors, state.MaxCost / cheapest)` is also sound,
and prunes more on later rounds. It changes the scan's cache key, so it is deferred to v2
(section 7).

### 2.3 What keeps the fast path, and what must refuse it

The stage sits inside `FuzzyLiteralFilter`, so it inherits that class's shape check first: the
pattern must be exactly `FUZZY -> string nodes and branches -> END_FUZZY -> SUCCESS`
(`FuzzyLiteralFilter.cs:151-160`, `:239-334`). Anything else returns null before the stage is
considered. The table lists each construct the brief asked about.

| Construct | Verdict | Why, and the evidence |
|---|---|---|
| BESTMATCH `(?b)` | keeps it | It chooses among real matches, and every real match starts at an admissible start (2.1). Its walk calls `BasicMatch(state, search)` (`Matcher.cs:20013`), where the filter is consulted. `(?b)(?:abcd){e<=2}` over `abxabcd` gives (3, 7) with no errors; the filter must leave it so. |
| ENHANCEMATCH `(?e)` | keeps it | Same argument. It narrows the slice and searches again (`Matcher.cs:19585`). The scan's cache is keyed on the slice end, as the piece memory already is (`FuzzyLiteralFilter.cs:596-600`). |
| Partial matching | **refuses** | A partial match can be a prefix of the literal at the end of the subject, far from any occurrence: `(?:abcdef){e<=1}` over `xxabc` with `partial=True` gives (1, 5), counts (0, 1, 0), `partial` True. Distance from `xabc` to `abcdef` is 3. Already withheld at `Matcher.cs:12777-12778`. Oracle calibration: 291 of 291 partial matches would have been rejected. |
| Reverse `(?r)` | v1: piece filter only | The relaxation holds mirrored, since distance does not care about direction. But today's reverse use is a single refusal (`Matcher.cs:12921-12930`). v1 leaves reverse exactly as it is; a mirrored scan is a SHORTCUT (section 7). |
| Verbs, `(*SKIP)`, `(*PRUNE)` | refuses (by shape) | A verb is not a string or branch node, so `Collect` returns false (`FuzzyLiteralFilter.cs:311-312`). |
| Lookaround, groups, anchors, repeats | refuses (by shape) | Same default arm. |
| Several fuzzy sections | refuses (by shape) | The start node must be the only `FUZZY`, and `END_FUZZY` must be followed by `SUCCESS` (`FuzzyLiteralFilter.cs:151`, `:327`). |
| Character classes in the literal | refuses (by shape) | Only `CHARACTER`/`STRING` nodes are walked (`FuzzyLiteralFilter.cs:291-310`). A class could go into Peq as an equality set, as edlib's `additionalEqualities` does. That is v2. |
| Insertion class `{e<=1:[a-z]}` | keeps it | It only forbids some insertions, so it narrows what matches. `(?:abcd){e<=1:[a-z]}` over `ab9cd` gives None. |
| POSIX `(?p)` | keeps it | Leftmost-longest still starts at a real match start. **Upstream crashes on fuzzy POSIX** (`docs/plan/upstream-reports/LEDGER.md:1364-1366`; reproduced tonight with `(?p)(?:abc){e<=1}` over `xabcz`, process exit 139), so the differential against main is the only check. |
| Case-insensitive, ASCII both sides | keeps it | Peq is built from the engine's own `Matcher.SameCharIgn` over the 128 ASCII characters, so its equality is exactly the engine's on ASCII text. That is the same rule as the piece filter (`FuzzyLiteralFilter.cs:42-51`). |
| Full case folding `(?f)` with a non-ASCII literal (ledger entry 49's `ß` unit) | **refuses** | The piece filter accepts a lone non-ASCII unit (`FuzzyLiteralFilter.cs:53-62`), but a scan over literal code units would not. `(?if)(?:straße){e<=1}` over `strasse` gives (0, 7) with no errors, while the code-unit distance from `strasse` to `straße` is 2. Rule: every literal value must be ASCII. |
| Non-ASCII subject text | **refuses** for the rest of the call | Kelvin sign: `(?i)(?:kabc){e<=1}` over `Kabx` gives (0, 4), counts (1, 0, 0). An ASCII Peq sees K as a mismatch, so the distance is 2 and the start would be rejected. The stage reuses `ScanMemory.IsAscii` (`FuzzyLiteralFilter.cs:789-822`) and returns `CannotTell` at the first non-ASCII character, as the piece search does (`:625-628`). |
| Astral text (UTF-16 against code points) | **refuses** (covered by the ASCII rule) | `(?:abc){e<=1}` over `a\U0001F600c` gives (0, 3), counts (1, 0, 0): one code point, one substitution. In UTF-16 that is two code units, so the distance is 2 and the start would be rejected. A future BMP-wide stage must refuse surrogates. |
| Budget k >= m | **refuses** (cost, not soundness) | Every position is then an occurrence end: `(?:ab){e<=2}` over `zzz` gives (0, 2), counts (2, 0, 0). The filter could reject nothing. No witness can exist: with k >= m, the empty span is already within k, so a correct scan never rejects. |
| m > 64 | v1 **refuses** | One `ulong` holds the column. A 65-character literal would silently wrap the shifts; a witness test with a 65-character literal and a real match guards the refusal. Multi-word Myers (Hyyrö 2003 blocks, or edlib's band) is the upgrade (section 7). |
| Several literals (an alternation, `\L<name>`) | v1: piece filter only | Each literal would need its own scan, or several packed into one word. That is v2, measured on the FuzzyPhraseThree rows. |

## 3. Where it plugs in, and which window it scans

### 3.1 The plug

Nothing new in the matcher. `BasicMatch` already asks `FuzzyLiteralFilter.NextStart(text, textPos,
sliceEnd, memory)` before each forward attempt (`Matcher.cs:12932-12947`). The contract there is
"the earliest position at or after textPos where a forward match could start, or NoMatch, or
CannotTell" (`FuzzyLiteralFilter.cs:581-591`). The new stage answers exactly that question, more
sharply. The changes all live in `FuzzyLiteralFilter`:

- `TryCreate` (`:146-210`): when the pieces would be under `MinPieceLength` (`:187`) or too few
  (`:177`), build a filter with **no pieces and a scan** instead of returning null. That is the
  **stand-alone** mode. When the pieces are fine, also attach the scan; that is the **second-stage**
  mode. Attach it only if there is one literal, every value is ASCII, 0 < k < m <= 64, and the
  literal is forward.
- The scan's data is built once at compile time: `Peq` as a `ulong[128]`, plus m and k. About 1 KB
  per pattern, with no work per call.
- `ScanMemory` (`:719-822`) gains the scan's facts, each stored with the range it is true over, as
  D14 requires (`:704-718`).

### 3.2 Three ways to use the scan

**Option A: end bound only.** Run the forward scan from x (the piece filter's answer, or textPos in
stand-alone mode) to the first end e1 where the distance is <= k. Every occurrence starting at or
after x ends at or after e1, and is at most m + k long, so

    s* >= e1 - m - k,   and the filter returns max(x, e1 - m - k).

If no end is found before the slice end, return `NoMatch`.

**Option B: exact start (recommended).** Do option A, then run the reverse scan over the window
`[max(x, e1 - m - k), min(sliceEnd, e1 + 2k))` and return the leftmost start in it whose distance is
<= k. That is s* exactly. Why the window is enough: s* is at most the start s1 of the occurrence that
ends at e1, and s1 <= e1 - (m - k). Any occurrence starting at or before s1 ends by s1 + m + k, which
is at most e1 + 2k. So the window holds every occurrence that could start at s* or earlier. The
window is at most m + 3k characters long.

**Option C: start bitmap.** Run the reverse scan once over the whole slice and keep a bit per
position for "admissible start". This is exact and simple, but it pays O(n) up front even when the
first match is at position 0. On the FuzzyLong row (1 MB, match near the start) that would turn
about 0.1 ms into an estimated 1.5 ms. **Rejected.**

### 3.3 Second-stage mode: a window, not a stretch

The piece filter's `IndexOf` (vectorised) skips text at far less than a nanosecond per character,
while the scan costs an estimated 1-2 ns per character. So in second-stage mode the scan must only
run near piece hits, never across stretches the pieces already ruled out. The rule:

1. Ask the piece filter for x. Pass `NoMatch` and `CannotTell` straight back.
2. Scan forward from x, but only up to `L = min(sliceEnd, x + 2(m + k))`.
3. If an end e1 <= L is found, do option B's reverse window and return s*.
4. Otherwise no occurrence starts at or after x and ends by L. Any occurrence starting at or after
   x therefore starts after `L - m - k`. Set textPos to `L - m - k + 1` and go back to step 1. If L
   was the slice end, return `NoMatch`.

Each round moves the position forward by at least m + k + 1, for at most 2(m + k) characters
scanned, so the second stage reads each character at most about twice. It also has a way out:
step 1 jumps straight past any stretch with no piece.

### 3.4 Caching between attempts

After an attempt at s* fails, `BasicMatch` asks again from s* + 1 (and `Matches` asks from the end
of the last match). The memory keeps:

- `(From, Start)`: the last query position and the s* it found. For any later query p with
  `From <= p <= Start`, the answer is Start, because an occurrence starts there and nothing in
  `[From, Start)` is admissible.
- `(From, Clear)`: a stretch found to hold no occurrence starting at or after From and ending by
  Clear. Any query in `[From, Clear - m - k]` jumps straight to `Clear - m - k + 1`.
- Both are cleared whenever the slice end changes (as `:596-600` does for pieces). `DoBestFuzzyMatch`
  restores the caller's slice for every candidate (`Matcher.cs:19981-19982`), so its walk keeps the
  cache; ENHANCEMATCH's narrowing resets it.

A query past Start restarts the forward scan at the new position. A scan cannot resume from the old
state, because its "start at or after x" condition has changed. Consecutive scans overlap by at most
m + k characters, since the new query starts after Start, which is after e1 - m - k.

### 3.5 Cancellation

The piece search does not poll for cancellation, and neither will the scan. But a stand-alone scan
of a long subject must not run unbounded. The scan stops after 65,536 characters with no end found
and returns `chunkEnd - m - k + 1`, which section 3.3's lemma shows is a valid start bound. The
engine then makes one attempt there, and its main loop polls `SafeCheckCancel` (`Matcher.cs:8096`)
as usual. The cost is one wasted attempt per 64 K characters.

## 4. Worst-case cost on paper

Notation: n the slice length; m <= w = 64, so one scan step is O(1) word operations (about 15);
k < m; A the number of attempts the engine makes; T_att the cost of one fuzzy attempt; c the scan
cost per character. Estimates to confirm with the prototype: c of about 1-2 ns. T_att is about
1.3 us for m = 8, k = 3 (FuzzyBudgetThree's 73.75 us over 55 attempts, `.claude/driver/perf-gap-table-2026-10-11.md:22`,
an unpinned run and an upper bound, since it includes the fixed cost per call).

| | Scan cost | Attempts | Changes a complexity class? |
|---|---|---|---|
| Today, pieces under 3 characters (no filter) | 0 | n + 1, so (n + 1) x T_att | - |
| Today, with pieces | `IndexOf` over the slice | at most (m + k + 1) for each piece hit | - |
| A, stand-alone | O(n + A(m + k)) x c | admissible starts, plus up to m + k before each cluster of them | no; it can only remove attempts |
| **B, stand-alone** | O(n + A(m + 3k)) x c | **admissible starts only** | no |
| B, second stage | at most 2 x (characters the pieces did not skip) x c, plus O(A(m + 3k)) x c | admissible starts only | no |
| C | O(n) x c up front, on every call | admissible starts only | O(1) becomes O(n) when the match is early: **rejected** |

**How few attempts B makes.** For plain `{e<=k}` the oracle calibration found **no** row where
upstream's match started after s* except at the search anchor, the one start where upstream
forbids a leading insertion. Elsewhere upstream's start equals s* (`start>s*-elsewhere`: 0 of 2,736
plain matches; 23 were one start late at the anchor). This follows from a standard edit-distance result: when the two characters are
equal, matching them is always optimal. So the backtracker, which never tries an error where the
characters match (`_regex.c:14741-14760`), still finds an alignment from every admissible start.
So for plain budgets, **B makes one attempt per match, plus at most one wasted attempt at the anchor**.
Per-kind limits, cost equations and BESTMATCH rounds can still fail at admissible starts
(`start>s*-elsewhere` was non-zero for those forms), and each such failure costs one more query of
O(m + 3k) x c.

**What one query costs against what it saves.** A query costs about (m + 3k) x c, around 20-40 ns
at m = 8, k = 3. One avoided attempt saves about 1.3 us. So the stage pays for itself if it saves
one attempt in every 30 or so queries.

## 5. Adversarial inputs

| Input | What happens | Bound |
|---|---|---|
| **Every window within k**: `(?:aaaaaaaa){e<=3}` over `aaaa...`, or any literal with k close to m over a 2-letter alphabet | The scan never rejects. Each attempt is preceded by a query that finds s* = textPos within m - k + 3k characters. | The overhead is about 30 ns per 1.3 us attempt, so about 2-3%. On top of that, a **self-disable** rule: after 40 queries (aho-corasick's MIN_SKIPS = 40, `prefilter.rs:184-261` as recorded in `.scratch/perf-libs/notes.md`), if the positions skipped are fewer than 2 per query (MIN_AVG_FACTOR = 2), switch the scan off for the rest of the scan. Switching off is always sound. |
| **Pieces everywhere, no occurrence**: `(?:needle){e<=1}` over `nee nee nee ...` | Today the piece filter lets through a candidate at every hit, and the engine makes up to m + k + 1 attempts per hit, so close to n attempts. B rejects each window. | At most about 2 characters scanned per position (3.3). This is where the second stage earns its place. |
| **Long subject, no piece** (FuzzyNoMatchLong) | The piece filter returns `NoMatch`; the scan never runs. | Unchanged: must stay within the noise floor. |
| **Stand-alone, long subject, no occurrence**: `(?:haystack){e<=3}` over 1 MB of prose with no near-miss | Today: about a million attempts, roughly 1.3 s (estimate). B: one forward scan. | n x c, about 1.5 ms (estimate). |
| **Stand-alone, early match in a long subject** (FuzzyLong shape) | The forward scan stops at the first end. | O(position of the match), not O(n). This is why C is rejected. |
| **Dense matches with `Matches`** | One forward scan plus one reverse window per match. | O(m + 3k) x c per match against T_att per match: a few per cent. |
| **ASCII prefix, then non-ASCII** | The scan reads the ASCII part, then `CannotTell` switches the filter off for the rest of the call (`Matcher.cs:12940-12943`). | No more than the piece filter's own `IsAscii` read of the same stretch. |
| **ENHANCEMATCH narrowing the slice repeatedly** | Each new slice end clears the cache. | One forward scan plus window per pass, against passes that each make at least one attempt. |
| **k near m** (`m - k` of 1 or 2) | Almost everything is an occurrence. | Self-disable as above. Also consider a static gate (refuse when m - k < 2) if the A/B shows a loss. |

## 6. The oracle experiment

Script: `.scratch/c3/oracle-exp.py` in this worktree. Run it with
`.venvs/regex-2026.9.10/Scripts/python.exe -I .claude/worktrees/c3-myers/.scratch/c3/oracle-exp.py --out .claude/worktrees/c3-myers/.scratch/c3/oracle-exp-summary.json`
from the repo root. The run-time estimate is written at the top of the script: 2.05 M rows, about
4-5 minutes on one core. The literals are canonical up to renaming the symbols, 3-8 characters over
`abc`. The rows come in three tiers:

1. Every subject up to length 9 (`--t1-len`, 10 gives 5 M rows in about 12 minutes) for every
   literal of length 3-4, with k = 1..3.
2. 250 k sampled rows with literals of length 5-8 and subjects of length 0-12, plus 5% of length
   16-24.
3. 120 k sampled rows in the other forms: per-kind limits, cost equations, `(?b)`, `(?e)`, `(?r)`
   and partial.

Per row it compares upstream's `(start, end, fuzzy_counts, fuzzy_changes)` with three models:

- (a) argmin distance, then leftmost;
- (b) leftmost, then argmin distance;
- (c) leftmost, then the first result in backtracking order. This is a model of upstream's STRING
  op (`_regex.c:14719-14760`, `:10431`, `:10116`): exact characters push nothing; at a mismatch it
  tries a substitution, then an insertion, then a deletion; there is no insertion at the search
  anchor; and deletion positions are reported with the offset upstream adds (`:20553-20557`).

On every row it also checks the filter's soundness: the matched text is within k_bound, and the
start is >= s*, or for `(?r)` the end is <= the rightmost occurrence end. It also checks that the
Python Myers forward and reverse scans equal the plain DP, column by column, so they serve as a
reference for the C# code.

**Calibration results** (10,537 rows, `--calibrate` plus `--t1-len 0 --t2-rows 4000 --t3-rows 3000`,
1.2 s in total):

| Model | Plain `{e<=k}` matches | Agrees | Span differs | Same span, different error total |
|---|---|---|---|---|
| (a) argmin, then leftmost | 2,736 | 1,109 | 1,410 | 217 |
| (b) leftmost, then argmin | 2,736 | 1,275 | 1,234 | 227 |
| (c) backtracking order | 3,200 (all forms) | **3,200** | 0 | 0 |

The soundness checks found no violations in any form, and the Myers scans matched the DP on every
row. Partial matching had 291 of 291 partial matches outside any occurrence, which is the witness
for refusing it.

Two witnesses show that no tie-break rescues (b). First, `(?:abbcb){e<=1}` over `babbccc`: upstream
gives (1, 6) with one substitution, while (b) gives (1, 5) with one deletion, at the same distance.
Second, `(?:abcbab){e<=2}` over `bcbaba`: upstream reports two errors on a span whose distance is
one. Only (c), a backtracking model, reproduces upstream; a distance scan cannot. **This confirms
the 2026-09-18 expectation, pending the full run.**

## 7. Recommendation, expected gains, and the test plan

**Build option B**, in two slices, each with its own A/B:

1. **Stand-alone mode** for the literals the pieces refuse. This is where the three ~1.08x rows are.
2. **Second-stage mode** behind the pieces, for the FuzzyPhrase rows.

v2, each only if measured: the mirrored reverse scan; k_eff from `state.MaxErrors` for BESTMATCH;
several literals (one scan each, or packed into one word when their lengths sum to 64 or less);
class equalities in Peq; multi-word scans for m > 64. Each v1 limit carries a `SHORTCUT:` comment
naming this list.

**Expected gain per row** (estimates; the before numbers are the unpinned 2026-10-11 table):

| Row | Before (port / Python) | Mechanism | Expected after |
|---|---|---|---|
| FuzzyBudgetThree | 73.75 / 67.70 us | 55 attempts become 1 (s* = 54 = upstream's start; first occurrence end 61) | 2-5 us |
| EnhanceMatch | 74.57 / 69.40 us | same first search; the narrowed re-runs attempt only admissible starts (5 in this subject) | 5-15 us |
| BestMatch | 76.03 / 70.50 us | same first search; the walk's rounds from 54 attempt only admissible starts | 5-15 us |
| FuzzyPhraseOne{Match,IsMatch,Enhanced} | 48.9-53.5 ms | second stage rejects records that hold a piece but no occurrence. The 2026-09-23 prototype measured 63 to 31 ms per 100 k (`.claude/driver/optimisation-handover-2026-09-23.md:86-89`) | 1.3-1.7x faster |
| FuzzyPhraseThree* | 141.6-144.3 ms | three literals: v1 does not apply | unchanged |
| FuzzyShort, FuzzyLong, FuzzyNoMatchLong | 0.30 us, 112 us, 123 us | pieces `nee`/`dle` already filter; few or no hits | unchanged (within the noise floor) |

**Test plan.** Test first, and each test must be shown red with the guard it pins removed.

1. **Witness tests**, one per refusal or bound, each red without its guard:
   - partial: `(?:abcdef){e<=1}`, `xxabc` gives (1, 5) partial;
   - Kelvin text: `(?i)(?:kabc){e<=1}`, `Kabx` gives (0, 4);
   - astral text: `(?:abc){e<=1}`, `a\U0001F600c` gives (0, 3);
   - non-ASCII literal: `(?if)(?:straße){e<=1}`, `strasse` gives (0, 7);
   - cheapest-cost division: `(?:abcd){2i+2d+1s<=4}`, `wxyz` gives (0, 4);
   - per-kind sum: `(?:abcd){i<=1,d<=1,s<=1}`, `xbd` gives (0, 3), counts (1, 0, 1);
   - m > 64: a 65-character literal with a real match;
   - endpos: `(?:abcd){e<=1}` with `search('xxabcd', 0, 5)` gives (2, 5) and `search('xxabc', 0, 4)`
     gives None;
   - the anchor rule: `(?:abc){e<=1}`, `xabc` gives (1, 4);
   - BESTMATCH and ENHANCEMATCH: the bench subject gives (56, 63), counts (0, 0, 1), and
     `(?b)(?:abcd){e<=2}`, `abxabcd` gives (3, 7);
   - the scan's cache across a `Matches` walk over dense hits;
   - self-disable, counted with `WorkCounter` in Debug, with the answers unchanged.

   k >= m has no possible witness (section 2.3); record that argument at the guard
   (VERIFICATION rule 12). Each engine assumption gets a `Debug.Assert`, for example that Peq agrees
   with `SameCharIgn` on all 128 ASCII characters.
2. **Differential against main**, at least 50 k rows through `tools/probes/port-probe.cs` on both,
   never with `--no-build`, with zero differences. The generator is biased to fuzzy literals:
   lengths 2-12, 64 and 65 over 2-4-letter alphabets with case variants; k from 1 to m; the forms
   e, per-kind and cost; the flags `(?b)`, `(?e)`, `(?i)`, `(?if)`, `(?r)`, `(?p)`, partial,
   pos and endpos; subjects with planted near-misses, plus non-ASCII and astral ones.
3. **Oracle at three seeds** (7, 4242, 20260927) through `tools/run-oracle.ps1`.
4. **Pinned BDN rows** (P-cores, `compare-benchmarks` defaults): every row in the gains table, plus
   two new adversarial rows: `(?:needle){e<=1}` over a megabyte of `nee nee ...` (dense pieces) and
   `(?:haystack){e<=3}` over `Corpus.LongNoMatch` (stand-alone, no match). Gate: no row more than
   10% slower; allocation delta reported. AOT tests and smoke with the binary size.
5. **Bookkeeping**: a `sync-divergence:` comment and SYNC-DIVERGENCE.md row (extend the existing
   S60b item 10 row), the OPTIMISATION-NOTES record, and a KNOWN-DEFECTS row only if a defect turns
   up.

## 8. What needs the machine

1. The full oracle experiment (section 6): about 4-5 minutes, one core. Its summary JSON replaces
   the calibration table above.
2. Before building slice 1: a toy timing of the C# forward scan per character (c) and of one
   (m = 8, k = 3) attempt (T_att), in a scratch console project. That takes seconds, and it settles
   the two estimates every number in sections 4 and 7 rests on.
