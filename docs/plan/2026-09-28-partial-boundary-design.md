# D11: partial matches at the truncation point (design)

Status: design only, no engine change. Round 3 (2026-09-28): sections 4 onwards are a fresh design ("undetermined dominates") after rounds 1 and 2 failed review; sections 1-3 are as reviewed. Repair round 1 of round 3 (same day): fuzzy-keyed dedup, the lookaround memo, a per-attempt budget, the semantics rows re-judged, group calls in the port grid, the G1 gate and the build chunks (11.1). Known-defects row D11 (F2, S3-F1 and oracle cluster A).
Owner rulings it builds on: **Q1 = B**, decide in the engine what the unknown continuation allows,
instead of re-running the match with probe characters, and make the `(?w)` multi-character window
exact; **Q2 = B**, widen the definition so that a complete match whose decision read past the end
of the text is a partial. Both were given on condition of checked research and a blind review
(checklist 2026-09-26, lines 33, 77 and 81).

Evidence: `tools/probes/d11-partial-boundary.py` (upstream `regex` 2026.9.10, PCRE2 10.47 through
Git for Windows' `libpcre2-8-0.dll`, and a brute-force judge), its rows file
`tools/probes/d11-partial-boundary-rows.jsonl`, and the port probe (`builder-rules.md`) over that
file. Everything below was measured on 2026-09-28 against main `32df055` (review round 1 rows against `775450a`, whose engine is the same).

## 1. The rule

A call with `partial=True` asks: **could a longer text make this a match?** (`upstream/README.rst:270`,
"whether a complete match could be possible if the string had not been truncated").

1. If the held text already has a complete match, that is the answer (upstream's two passes,
   `_regex.c:18152-18168`; `Matcher.DoMatch`). Nothing here changes that first pass.
2. Otherwise the second pass asks about a **non-empty** continuation `w`. The empty continuation is
   the text ending where it does, which the first pass has already judged.
3. The answer is a partial at the leftmost start `s` from which some `w` gives a complete match of
   `t + w`, spanning `(s, |t|)`. Under `fullmatch` the match must end at the end of `t + w`. For a
   search, a match that could only begin inside `w` is reported at `(|t|, |t|)` (rows S1-S3);
   PCRE2 answers None there. It is decided by one more attempt, whose start is inside `w` with both
   of its sides unknown (section 4.3, "The search"), so a pattern no text can match is never
   reported: search `(*FAIL)` over '' is None, as upstream answers.
4. Q2 = B follows from (3) directly: the match found in `t + w` need not reach past `|t|`. It only
   has to depend on `w`. `(?w)a\B` over `'a:'` is a partial because `'a:a'` matches at (0, 1), and
   the span is (0, 2), as PCRE2 reports for `a(?=:a)` over `'a:'` (row C7).

Deciding (3) exactly is not possible in general (upstream issue 367; its example encodes
primality). The engine therefore computes an **over-approximation that never denies a real
completion**, which is the direction the owner's ledger-21 ruling protects:

- every choice about `w` is made **independently**: each decision is checked against the held text
  and against the set of continuations, but not against the other decisions (section 6, J1, J2);
- once a path **consumes** a character beyond `|t|`, the rest of the pattern is presumed
  satisfiable. That is what upstream, PCRE2 and this port do today (section 6, B4, Z3, A3;
  decision 4 says where it could be decided).

**What is proved, and how far** (review round 2 asked for the claim to go no further than the
evidence). Round 3 (section 4.6 for the proof sketch, section 8 for the measurements, 2026-09-28):

- **Soundness** (no refusal where a continuation completes the match, ledger 21): a proof sketch,
  and 0 misses on 120,000 grid rows judged by an independent brute force
  (`tools/probes/d11-brute-judge.py`: upstream without partial, every continuation of one to four
  characters over a nine-letter alphabet) against a model of the design
  (`tools/probes/d11-direction-model.py`, which shares no code with the judge).
- **Not exact.** 13,760 of the 120,000 rows are phantoms, in four classes (section 6). Three of the
  classes are limits the port already has today; the fourth, 589 rows, is this design's own
  over-approximation.
- **Not covered by the model:** `(?w)`, fuzzy sections, `\X`, `(?p)`, `(?b)`/`(?e)`, full folding,
  group calls and the search prefilter. 50 grid rows where the model's own matcher disagrees with
  upstream on a known text are left out of the gate (section 8). The build's grid on the port
  (section 8) covers the rest.

## 2. The repros, measured

`F` is a complete match, `P` a partial, spans in code units. "brute" is the definition above,
judged by upstream's non-partial matcher over every continuation of one to three characters (so a
brute None is a lower bound; no row here needs more). "design" is what section 4 gives.

| id | call | upstream | PCRE2 | port | brute | design | why |
|---|---|---|---|---|---|---|---|
| B1 | match `a\m` 'a' | None | P(0,1) | **P(0,1)** | None | None | 'a' before it decides `\m` false |
| B2 | match ` \M` ' ' | None | P(0,1) | **P(0,1)** | None | None | same, for `\M` |
| B3 | fullmatch `a\B` 'a' | None | P(0,1) | **P(0,1)** | None | None | the match must end where the pattern stops |
| B4 | fullmatch `(?w)(?:a\B)+` 'aaa' | None | P(0,3)* | P(0,3) | None | P(0,3) | limit: the next 'a' runs out (section 6) |
| B5 | fullmatch `(?fV1mi)(?:\p{Ll}(*SKIP)[^\d]\|\p{Ll})([[:alpha:]])\B$` 'aaA' | None | - | **P(0,3)** | None | None | wave 104:2843; fullmatch |
| B6 | match `a\b(?<=b)` 'a' | None | P(0,1) | **P(0,1)** | None | None | the lookbehind fails on held text |
| B7 | match `(?fai)^𝔘\M` '𝔘' | None | - | **P(0,2)** | None | None | wave 104:2839; not a word character under `(?a)` |
| C1 | match `aa\B` 'aa' | None | P(0,2) | P(0,2) | P(0,2) | P(0,2) | control (S57d) |
| C2 | fullmatch `(?!(True\|False)\b)(.*)` 'True' | None | P(0,4) | P(0,4) | P(0,4) | P(0,4) | control (ledger 21) |
| C3 | match `\X(?<!a)` 'a' | None | P(0,1) | P(0,1) | P(0,1) | P(0,1) | control: a combining mark extends the cluster |
| C4 | fullmatch `(?m)\b(\p{ASCII})\B([^\d])` 'a' | None | P(0,1) | P(0,1) | P(0,1) | P(0,1) | control (wave 104:2723) |
| C5 | match `(?mi)\p{Ll}?(.){1}?\b` '\r' | None | P(0,1) | P(0,1) | P(0,1) | P(0,1) | control (wave 102:1861) |
| C6 | match `(?r)\Ba` 'a' | None | - | P(0,1) | P(0,1) | P(0,1) | control (S57d, reversed) |
| C7 | match `a(?=:a)` 'a:' | P(0,2) | P(0,2) | P(0,2) | P(0,2) | P(0,2) | control: lookahead runs out |
| C8 | fullmatch `.*(?<=z)` 'ab' | P(0,2) | P(0,2) | P(0,2) | P(0,2) | P(0,2) | control: greedy repeat runs out |
| W1 | match `(?w)a\B` 'a:' | None | - | **None** | P(0,2) | P(0,2) | Q2 = B; WB6 reads past the end |
| W2 | search `(?w)a\B` 'xa:' | P(3,3) | - | **P(3,3)** | P(1,3) | P(1,3) | leftmost completable start |
| W3 | match `(?w)a\B` 'a:\u0301' | None | - | **None** | None† | P(0,3) | WB4 skips the mark to the end |
| W4 | fullmatch `(?w)a\B` 'a:' | None | - | None | None | None | control |
| W5 | match `(?w)1\B` '1,' | None | - | **None** | P(0,2) | P(0,2) | WB12 |
| J1 | match `a\b\B` 'a' | None | P(0,1) | P(0,1) | None | P(0,1) | limit: decisions are independent |
| J2 | match `a\B$` 'a' | None | P(0,1) | P(0,1) | None | P(0,1) | limit, same |
| L1 | search `(?=..)[ab]` 'x' | P(1,1) | P(0,1) | **P(0,1)** | P(1,1) | P(1,1) | S3-F1 |
| L2 | match `(?=..)[ab]` 'x' | P(0,1) | P(0,1) | **P(0,1)** | None | None | S3-F1: `[ab]` fails on 'x' |
| L3 | match `(?=a.)b` 'a' | None | None | None | None | None | control |
| L4 | match `(?(?=..)a\|b)` 'a' | P(0,1) | P(0,1) | P(0,1) | P(0,1) | P(0,1) | control |
| L5 | match `(?(?=..)a\|b)` 'c' | P(0,1) | P(0,1) | **P(0,1)** | None | None | neither branch matches 'c' |
| L6 | match `(?!.a\|x)x` 'x' | P(0,1) | P(0,1) | **P(0,1)** | None | None | the body's `x` matches held text |
| L7 | match `(?!a(?:\B(?<=b)\|))a` 'a' | None | P(0,1) | **P(0,1)** | None | None | an undetermined path backtracked out of |
| A1 | match `(?fai)(?r)(?:\W+?(*SKIP)\W\|[a\d])\M` '' | None | - | P(0,0) | P(0,0) | P(0,0) | cluster A, port right (ledger 21 family) |
| A2 | search `(?fi)\mİ(?(?<!\w)[^a-f]\|\p{Ll})$` 'ﬁ ' | P(2,2) | - | **None** | P(2,2) | P(2,2) | cluster A: prefilter decides `\m` |
| A3 | search `(?m)^\p{Lu}*?(?P<g1>[a])(?:(?(1)(?<=(?P>g1))\p{Lu})){3}(\d)\2$` '𐐀𐐀😀' | P(0,6) | - | P(6,6) | None‡ | P(6,6) | cluster A: port = the search convention |
| A4 | search `\ma` 'b ' | P(2,2) | P(2,2) | P(2,2) | P(2,2) | P(2,2) | control |
| A5 | match `(?r)[a\d]\M` '' | None | - | **None** | P(0,0) | P(0,0) | S57d's "consumed nothing" narrowing |
| Z1 | match `(?:a\m){i<=1}` 'a' | None | - | P(0,1) | P(0,1) | P(0,1) | control: an insertion takes 'b' after a space |
| Z2 | fullmatch `(?:a\B){i<=1}` 'a' | None | - | P(0,1) | P(0,1) | P(0,1) | control, same |
| Z3 | fullmatch `(?w)(?:a:){e<=1}\B` 'a:' | None | - | P(0,2) | None | P(0,2) | limit: an insertion runs out (section 6) |
| E1 | match `a\Zb` 'a' | P(0,1) | P(0,1) | **P(0,1)** | None | None | `\Z` needs `w` empty; open question 3 |
| E2 | match `a$\n` 'a' | P(0,1) | P(0,1) | P(0,1) | P(0,1) | P(0,1) | control |
| P1 | search `(?r)\b$` '' | P(0,0) | - | **None** | P(0,0)§ | P(0,0) | inverts a PERMANENT pin; open question 1 |
| P2 | match `(?r)\b$` '' | None | - | **None** | P(0,0) | P(0,0) | same |
| P3 | search `\b` '' | None | P(0,0) | **None** | P(0,0) | P(0,0) | S57d narrowing; open question 1 |
| S1 | search `(?<=y)a` 'x' | P(1,1) | None | P(1,1) | P(1,1) | P(1,1) | search convention (rule 3) |
| S2 | search `(?m)^a` 'x' | P(1,1) | None | P(1,1) | P(1,1) | P(1,1) | same |
| S3 | search `(?i)\mİ` 'b' | P(1,1) | - | **None** | P(1,1) | P(1,1) | same, through the prefilter |
| V1 | match `(?:a\b(*PRUNE)(?<=b)\|a.)` 'a' | None | P(0,1) | P(0,1)¶ | P(0,1) | P(0,1) | review D-1: 'ab' fails the `\b`, so the verb is never reached |
| V2 | match `(?:a\b(*SKIP)(?<=b)\|a.)` 'a' | None | P(0,1) | P(0,1)¶ | P(0,1) | P(0,1) | same with `(*SKIP)` |
| V3 | search `a\b(*SKIP)(?<=b)\|a.` 'a' | P(1,1) | P(0,1) | **P(1,1)** | P(0,1) | P(0,1) | same, search: the skip must not move the start |
| V4 | match `(?:a(*PRUNE)\b(?<=b)\|a.)` 'a' | None | P(0,1) | **P(0,1)** | None | None | control: the verb runs before the decision, so it prunes for every `w` |
| D1 | match `(?>x\b\|)(?<!x)x` 'x' | None | P(0,1) | P(0,1)¶ | P(0,1) | P(0,1) | review D-2: 'xa' fails the `\b`, the atomic takes its empty path |
| D2 | match `(?>ab\b\|a)(?<=a)b` 'ab' | None | P(0,2) | P(0,2)¶ | P(0,2) | P(0,2) | same, second path non-empty ('abc') |
| D3 | match `(?:a\b)?+(?<!a)a` 'a' | None | P(0,1) | P(0,1)¶ | P(0,1) | P(0,1) | possessive repeat, zero iterations under 'aa' |
| D4 | match `(?>a\Z\|)(?<!a)a` 'a' | None | P(0,1) | **None** | P(0,1) | P(0,1) | the second pass decides `\Z` differently from the first |
| R1 | match `(?r)(?<=b)\Aa` 'a' | P(0,1) | - | **P(0,1)** | None | None | reversed `\A` at the left edge is false once `w` is there |
| R2 | match `(?r)(?<=b)^a` 'a' | P(0,1) | - | **P(0,1)** | None | None | same for `^` |
| R3 | match `(?rm)(?<=\n)^a` 'a' | P(0,1) | - | P(0,1) | P(0,1) | P(0,1) | control: multiline `^` is undetermined there |
| W6 | match `(?w)א\B` 'א"' | None | - | **None** | None† | P(0,2) | WB7b: Hebrew letter, `"`, and a Hebrew letter to come |
| E3 | match `a$\n\b` 'a\n' | None | P(0,2) | **P(0,2)** | None | None | `$` at a final newline is false once `w` follows it |
| M5 | match `(?p)a\B` 'a' | None | - | P(0,1)¶ | P(0,1) | P(0,1) | POSIX: a second-pass success returns at once |
| M6 | match `(?b)(?:a\B){e<=1}` 'a' | None | - | P(0,1)¶ | P(0,1) | P(0,1) | best match: returned without refinement |
| N1 | match `(?>x\b\|x\|)(?<!x)x` 'x' | None | P(0,1) | **P(0,1)** | None | None | round 2: once reached, the atomic's second path commits |
| N2 | match `(?>a\Z\|a\|)(?<!a)a` 'a' | None | P(0,1) | None | None | None | a decided read needs no mark (round 1: phantom) |
| N3 | match `(?:a\Z(?<=b)\|a(*PRUNE)(?<=b)\|a)` 'a' | None | P(0,1) | None | None | None | the prune after a decided read prunes (round 1: phantom) |
| N4 | match `a\b(?:(*PRUNE)(?<=b)\|)` 'a' | None | P(0,1) | P(0,1) | None | P(0,1) | limit C4 (section 6): a blocked cut keeps the alternatives made after the decision |
| N5 | match `(?>a\b(?:\|()))(?(1)\|(?<=b))` 'a' | None | P(0,1) | P(0,1) | None | P(0,1) | same, atomic group |
| N6 | match `(?=a\b(?:\|()))(?(1)\|(?<=b))` 'a' | None | P(0,1) | P(0,1) | None | P(0,1) | same, lookahead |
| N7 | match `b(?!(?!\w.))` 'b' | P(0,1) | P(0,1) | P(0,1)¶ | P(0,1) | P(0,1) | barrier: found by the model, broke the review's version |
| N8 | match `.(?!(?>\wa\|(?<!b))(?<!a))` ' b' | P(0,2) | P(0,2) | P(0,2)¶ | P(0,2) | P(0,2) | third run-out outcome and the kept barrier: found by the model |

Bold marks the port answers the design changes. `-` means PCRE2 has no spelling for the row.
\* PCRE2 asked `(?:a\B)+`, the same shape without `(?w)`. † Upstream's `(?w)` differs from UAX #29
(ledger 40: WB4 applied only to the left, among others), so its brute force misses the
completions 'a:\u0301a' (W3) and 'א"א' (W6); the port's own non-partial matcher answers (0, 1) for
both. ‡ The pattern can never match (the second
iteration's lookbehind needs an 'a' before an uppercase letter), so the true answer is None; both
engines report a partial through the search convention, which is limit 2. § Reversed search is
brute-forced from round 1 on: every overlapped match of `w + t` is listed and the rightmost attempt
end inside `t` wins (`brute()` in the probe). ¶ Right today only through S57d's escalation, which
the design deletes; without the verb and commit rules of M2 these become false negatives.

Thirty rows are wrong in the port today and fixed by the design, and every one is a red test
(section 7). The nine ¶ rows plus N2 and N3 guard the design: V1, V2, D1-D3, N2, N3, N7 and N8 are
in the red tests, and M5 and M6 are witnesses. Seven rows (B4, Z3, J1, J2, N4, N5, N6) stay
over-approximated by the design; PCRE2 answers a partial on each of them it can spell (Z3 it
cannot). Round 3 adds the rows of
section 7 (Q1-Q3, T1-T8, G1, X1, X2), measured the same way.

## 3. Root causes

**RC1. A boundary at the truncation point escalates whatever its value and whatever follows.**
`NoteBoundaryAtTruncationPoint` (`src/FuzzyRegex/Engine/Matcher.cs:2243`, called at 2268, 2288,
2306, 2322, 2344, 2357, 2373) sets `MatchState.HitEnd` (`MatchState.cs:402`) whenever a word or
grapheme boundary is asked at the edge, and `DoMatch` (`Matcher.cs:12885-12906`) turns any failed
match into a partial when the flag is set. The flag is set on the question, not on the answer, and
not on the path: B1, B2 and B7 have a boundary the held text decides; B6, L7 have a path that fails
after it on held text; B3 and B5 need the text to end where the pattern stops. Its "the attempt
must have consumed something" clause (`Matcher.cs:2249`) then refuses A5, P1-P3, where a word
character after the edge completes the match. Upstream has no counterpart and resolves every
boundary against the held text (`try_match_BOUNDARY`, `_regex.c:7010`; `unicode_at_boundary`,
`:1460`), which is ledger 21's false negative (A1, C1-C6).

**RC2. The `(?w)` boundary treats the end of the text as a character that matches no rule.**
`AtDefaultBoundary` reads the character after next as property 0 when it lies past the end
(`Matcher.cs:1418-1420`) and answers WB2 "break" at the end (`:1350-1353`), so a decision whose
UAX #29 window reaches past `|t|` (WB6, WB7b, WB12, and WB4 skipping marks up to the end) is
resolved against the held text: W1, W3, W5 are None. Upstream the same (`unicode_at_default_boundary`,
`_regex.c:1531`), with its own WB4 defect on top (ledger 40).

**RC3. A lookaround body that runs out of text ends the whole match as a partial.** Every
consuming arm returns `MatchStatus.Partial` at the edge (for example `Matcher.cs:8090-8093`; upstream
`_regex.c:12127-12129`), and the status propagates out of `BasicMatch` from inside a lookaround or
a conditional's condition as well. So the pattern after the lookaround is never checked against the
held text: L1 (search reports start 0), L2, L5, L6. Upstream shares L2, L5 and L6 (its search
answers L1 correctly; which of its paths does that was not traced).

**RC4. The search prefilter decides the start test against the held text and has no partial arm.**
`SearchStartZeroWidth` (`Matcher.cs:6325`) asks `TryMatchZeroWidth` about `\m` at the edge, where
the plain answer is false, so it skips the only completable start (A2). When the prefilter finds no
start, `DoMatch2` returns its failure (`Matcher.cs:6949-6955`, "This port's prefilter has no partial
arms to forward"), while the slow path at `:7004-7007` (upstream `_regex.c:11863-11867`) answers the
search convention's `(|t|, |t|)` partial: the two paths disagree (S3 None, A4 and S1 partial).

**RC5. A fuzzy insertion at the edge is not a run-out.** The insertion arm of
`NextFuzzyMatchItem` passes the current position to `CheckFuzzyPartial` (`Matcher.cs:4499`,
upstream `_regex.c:10156`), whose test is `textPos > TextEnd` (`:4335`), so an insertion that needs
the character after `|t|` fails instead of reporting that the text ran out. Today HitEnd hides this
(Z1, Z2 are partials only through RC1's escalation); removing RC1 without fixing RC5 turns them into
false negatives, which is one of the ways the rejected `maint/boundary-partial` fix lost real
partials (handover 2026-09-26, item 6).

## 4. The fix

The second pass runs the ordinary backtracking search once, over `t + w` with `w` unknown. The
search depends on `w` in only two ways. It **reads** `w`: a zero-width test at the edge, or a
consuming item that needs the character after the edge (a *run-out*). And it **decides** on what it
read: a cut (atomic group, possessive repeat, `\X`, the end of a lookaround or of a condition's
test, `(*PRUNE)`, `(*SKIP)`) or a lookaround's or condition's outcome.

The rule of round 3 is **undetermined dominates**. A read is three-valued. A decision is taken only
when nothing it depends on read `w`; otherwise the engine keeps every outcome the decision could
have, as if it had not been made. That over-approximates (section 6 counts the phantoms) and never
refuses a real completion (section 4.6 sketches the proof, section 8 measures it).

| | mechanism | in one line |
|---|---|---|
| **M1** | three-valued predicates | a test at the edge is decided when every `w` gives the same answer, otherwise it is undetermined: it passes and leaves a touch mark |
| **M2** | w-marks and the cut rule | a construct cuts or decides only when no mark on the backtrack stack is newer than it; otherwise it keeps every outcome and taints itself |
| **M3** | a second-pass success is a partial | `match` and `search` report it; `fullmatch` backtracks; a search that finds no start answers `(\|t\|, \|t\|)` |
| **M4** | a fork budget | past 256 (\|t\| + 1) kept-both-ways decisions in one call, the call answers a partial at the current start |

Plus RC5's one-argument fix and the deletions (section 4.5).

### 4.1 M1. Three-valued predicates

In the second pass only (`state.PartialSide != PartialNone`; the
first pass runs with `PartialNone`, `Matcher.cs:12815-12817`), a predicate that reads outside the
text in the partial direction (forward: an index at or past `TextEnd`; reversed: before
`TextStart`, the ledger 24 "whole-string view" the predicates already keep) is either:

- **decided**, when every non-empty continuation gives the same answer. It answers that, and needs
  no mark even where the first pass answered otherwise (`\Z`, a final-newline `$`, reversed `\A` and
  `^`), because the answer holds for every continuation;
- **undetermined**, when some continuation gives each answer. It **passes** and pushes a touch mark
  (M2); its other side, the read failing, is plain backtracking.

This is every predicate that reads `w` (rule 10). All are reached through `TryMatchZeroWidth`
(`Matcher.cs:2489`), whose callers are the main loop (`:8195`), the fuzzy retry of a zero-width item
in the backtrack loop (`:10331-10347`), the prefilter (`SearchStartZeroWidth`, `:6331`) and
`AnchorIsPinned` (`:2554-2555`, which must use a variant that marks nothing because it is not on a
path):

| predicate | helper, `Matcher.cs` | reads `w` when | first pass | second pass |
|---|---|---|---|---|
| `\b` `\B`, ASCII, Unicode, locale | `TryMatchBoundary` :2266, `AtBoundary` :1230 | at the edge | the edge is a non-word side | undetermined |
| `\m` | `TryMatchStartOfWord` :2355, `AtWordStart` :1240 | at the edge | false | decided false after a word char, else undetermined |
| `\M` | `TryMatchEndOfWord` :2342, `AtWordEnd` :1250 | at the edge | true after a word char | decided false after a non-word char, else undetermined |
| `(?w)` `\b` `\B` | `TryMatchDefaultBoundary` :2286, `AtDefaultBoundary` :1340 (WB2 :1350, window :1418-1420) | at the edge, or the UAX #29 window (WB4 skip, WB6, WB7b, WB12) reaches it | WB2 break; property 0 past the end | decided when every Word_Break value (with Extended_Pictographic, RI parity) at the unknown positions gives the same answer, else undetermined |
| `(?w)` `\m` `\M` | `TryMatchDefaultStartOfWord` :2320, `TryMatchDefaultEndOfWord` :2304, `AtDefaultWordStartOrEnd` :1931 | as above | as above | as above |
| grapheme boundary, the node and `\X`'s own | `TryMatchGraphemeBoundary` :2371, `AtGraphemeBoundary` :1957 (GB2 :1961) | at the edge | GB2 break | decided when every Grapheme_Cluster_Break value (with ExtPict, InCB) gives the same answer, else undetermined |
| `$` | `TryMatchEndOfStringLine` :2427, `...U` :2435; `FinalNewline` `MatchState.cs:211`, set :946 | at the edge; at a final newline | true at both | at the edge undetermined (`w` = "\n"); at a final newline decided **false** (`w` makes it non-final) |
| multiline `$` | `TryMatchEndOfLine` :2405, `...U` :2413, `AtLineEnd` :1176 | at the edge | true | undetermined |
| `\Z` | `TryMatchEndOfString` :2420 | at the edge | true | decided **false** (decision 3) |
| `\A`, `^` under `(?r)` | `TryMatchStartOfString` :2476 | at the left edge | true | decided **false**, by decision 3's reasoning: with `w` on the left, 0 is not the start of the string (R1, R2) |
| multiline `^` under `(?r)` | `TryMatchStartOfLine` :2461, `...U` :2469, `AtLineStart` :1146 | at the left edge | true | undetermined (`w` ending in "\n", R3) |
| the reversed twins of the word and grapheme rows | same helpers | at the left edge | as forward | as forward |

The exact `(?w)` answer (the owner's condition on Q1) splits `AtDefaultBoundary`,
`AtDefaultWordStartOrEnd` and `AtGraphemeBoundary` into a core that takes property values rather
than text, and evaluates the core over the value set at each unknown position. Only the edge
positions enumerate, the sets are small (Word_Break has 19 values), and a WB6-style second position
is enumerated only when the first unknown value is MidLetter, MidNum, MidNumLetQ or Double_Quote.

### 4.2 M2. W-marks and the cut rule

**The idea, with a timeline.** Take `(?:(?>a\B|)(?<!a)a(*PRUNE)(?<=b)|a\B)` over 'a' (row T1). The
real completion is 'a' + 'a': the atomic group commits to `a\B`, `(?<!a)` fails, the group cannot be
retried, so the outer second branch `a\B` matches (0, 1). The second pass does this:

1. `a\B` reads `w` (undetermined), so the atomic group's cut is *blocked*: in the universes where
   `\B` is false the group's second branch runs, in the others it does not. The engine keeps the
   branch and continues. `(?<!a)` fails.
2. Backtracking reaches the group's empty branch. That path has read nothing, so a count of reads
   would call it untouched. `(?<!a)a(*PRUNE)` follows, `(?<=b)` fails, and backtracking reaches the
   `(*PRUNE)`.
3. If the prune fires, it removes the outer `|a\B`, and the second pass answers None: a miss. But
   the empty branch exists only in the universes where the group did **not** commit in step 1; in
   the others (the real completion), `|a\B` is still needed.

So "this path read nothing" is not enough. The path must also not have been reached through a cut
the engine skipped. That is the **taint**, the first of the five corrections round 3 makes to the
direction as first stated (section 11).

**State (second pass only).** `MatchState.WMarks`: a list of (Bstack position, kind) records sorted
by position, allocated on first use and reused from the state pool. Three kinds:

- **touch**: the path read `w`. Pushed by an undetermined predicate that passed (M1), a run-out
  inside a lookaround body or condition test, a negative lookaround or condition that passed
  undetermined, and a branch taken on an unknown group. The engine pushes a one-byte `WTouch` entry,
  whose backtrack arm only pops, and records its position;
- **cut taint**, at a construct's frame: its cut was blocked, or its body succeeded only in some
  universes. The alternatives inside it are then reached only in some universes;
- **verb taint**, at a verb scope's frame (the attempt itself for a top-level verb): a `(*PRUNE)` or
  `(*SKIP)` that unwinds to this scope was skipped.

A mark lives while its position is on the stack. The `WTouch` arm drops its own mark; the frame arms
(`Atomic` `Matcher.cs:10465`, `Lookaround` :11489, `Conditional` :10526) drop the marks at their
frame; the verb unwinding (`UnwindToTheVerbTarget` :3246) drops those above its target; `InitMatch`
clears the list. No other truncation may remove a live mark (assert A7), so the backtrack loop pays
nothing.

**The cut rule.** A construct E with frame position `e` may **cut** (discard the alternatives its
body left) or take a **definite decision** (a negative lookaround fails, a condition picks one
branch) only when no touch or cut-taint mark lies above `e` and no verb-taint mark lies at or above
`e`. Otherwise E is **blocked**. Each boundary of the rule has a witness (section 7):

- a touch above `e`: the path since E read `w`, so E's first body success differs between universes
  (D1-D3, V1-V3);
- a cut taint above `e`: the path since E came back through a construct inside E whose cut was
  blocked; in the universes where that construct committed, E's alternatives are still needed (T1;
  66 grid rows miss without it);
- a cut taint at `e` itself does not block E: in the universes where E committed earlier, nothing
  inside E is needed (N1 and L6 turn phantom if it does);
- a verb taint at `e` itself does block E: in the universes where the skipped verb fired, E's scope
  ended, so a later success inside E is not certain (Q1, Q3, T2 are misses if it does not).

**What each construct does in the second pass.** Unblocked, every construct behaves exactly as today.
Blocked:

| construct | forward arm, `Matcher.cs` | blocked |
|---|---|---|
| atomic group, possessive repeat, `\X` | `EndAtomic` :7211 (truncation :7227) | cut taint at the frame; pop the `Pstack` mark; push `Reopen`, whose arm pushes the mark back; continue after the group |
| positive lookaround | `EndLookaround` :7968 (truncation :7984) | the same, plus the position and slice restore it already does. A success whose captures and fuzzy counts (under `(?b)`/`(?e)` also the fuzzy change list) equal those of an earlier continued success of the same instance is not continued again (the *dedup*): blocked, it backtracks into the body; unblocked, the lookaround cuts and fails |
| negative lookaround | `EndLookaround` :7968 | cut taint at the frame and backtrack into the body: the success happens only in some universes. When the body is exhausted the `Lookaround` arm (:11489) passes the assertion as today, and pushes a touch if its frame carries a cut taint |
| condition, lookaround test | `EndConditional` :7279 (truncation :7295); `Conditional` arm :10526 | positive test: as a positive lookaround, then the yes-branch; on exhaustion, the no-branch, with a touch if the frame carries a cut taint. Negative test: as a negative lookaround; on exhaustion, the yes-branch, and if the frame carries a cut taint also the no-branch through a `NoBranch` entry, each with a touch |
| condition on a group, `(?(n)...)` | its forward arm | on an unknown group: push `NoBranch`, take the yes-branch, both with a touch |
| `(*PRUNE)`, `(*SKIP)` reached by backtracking | `Prune` :11679, `Skip` :11645 | verb taint at the target scope's frame; the verb does nothing and backtracking goes on |
| a consuming item at the edge inside a lookaround body or condition test | the edge arms (for example :8090-8093), the backreference arms | a run-out: (1) the captures of the groups inside that body become **unknown** (a reserved span written through the capture-change entries, so backtracking restores it); (2) a cut taint at every atomic, possessive and lookaround frame open inside the body (found through `Pstack`), and when the body holds a `(*PRUNE)` or `(*SKIP)` (a flag set at compile time) a verb taint at each of them and at the lookaround's own frame; (3) a touch; (4) control goes to the lookaround's end as if the body had matched. Outside any body a run-out returns Partial as today (M3) |
| a backreference to an unknown group | the backreference arms | a run-out |
| a repeat guard for a repeat inside a lookaround body or condition test | the recording sites :10922, :11642, :11139, :11174, :11323, :11327 | no guard is recorded while any taint lives (a flag per repeat, set at compile time) |
| every lookaround and condition test (the *memo*) | the lookaround and condition entry arms | the body's outcome at (node, position, captures in, fuzzy counts in) is recorded the first time and replayed in every later attempt of the call: each body success as its captures, fuzzy counts and whether it was blocked, and how the body ended (exhausted, or a verb unwound it). A replayed blocked success carries a touch. Second pass only; the table stops growing past 64 (\|t\| + 1) entries, which costs time, never soundness. **Off** for a body that holds a group call, recursion or `\G` (a flag on the node, set at compile time): those read state the key does not hold (4.6, step 8) |

Why the less obvious parts are there:

- **The run-out taint.** A run-out does not run the rest of the body. In the universes where `w`
  lets it go on, a construct still open at the edge may commit, and a verb after the edge may unwind
  its scope. N8 (`.(?!(?>\wa|(?<!b))(?<!a))` over ' b') and Q3 (`(?!a.(*PRUNE)b|a)a` over 'a') are
  misses without it, and so are 33 grid rows.
- **Unknown captures.** After the edge, a group inside the body can hold anything, or nothing. A
  condition on it takes both branches (Q2, `(?=a(.))(?(1)a|b)` over 'a'), and a backreference to it
  presumes the rest, as a run-out does (T3, `(?=a(.))\1`).
- **The dedup.** What follows a lookaround depends on the position (the lookaround's start), the
  captures, the fuzzy counts and the marks. A second continuation of the same instance with the
  same captures and counts carries no fewer of the marks that matter (section 4.6, step 7), so it
  fails where the first failed. Without it, `(a)(?:(?=.*z|)a)*b\1` over 'a' * n + 'c' is
  exponential (section 9), because a backreference turns repeat guards off. **The key must hold the
  fuzzy counts**: errors made inside a body count against the enclosing budget after it (upstream
  2026.9.10: `(?:(?=(?:ab){e<=1})ay){e<=1}` over 'ax' is None, and with `{e<=2}` it matches with
  counts (2, 0, 0)). Keyed on captures alone, F1, `(?:(?=(?:ax\b|ay\B){e<=1})az){e<=1}` over 'ay'
  (judge P(0, 2)), is a miss: the 0-error body success is dropped as a repeat of the 1-error one,
  and only the 0-error one leaves budget for `az`. Under `(?b)`/`(?e)` the change list is part of
  the continuation's state too, so it joins the key.
- **The memo.** In a search each attempt re-runs the same lookarounds at the same positions, and in
  the second pass each of them can run to the edge, so the second pass of `a*(?=.*z)b` over
  'a' * n + 'c' was cubic where the first is quadratic, and its forks (10,302 at n = 100, 40,602 at
  n = 200) crossed a per-call budget from about n = 256. A body's outcome does not depend on the
  path outside it (section 4.6, step 8), so it is computed once per (node, position, captures in,
  fuzzy counts in) and replayed. With the memo the second pass is back at the first pass's order
  (section 9). A body that holds a group call or recursion is never memoised: the call reads the
  open-call state (`MatchState.ActiveCalls` and `OpenCalls`, the ledger 14 recursion guard), so a
  body that calls its enclosing group can fail in one attempt and succeed in another from the same
  key, and a replayed "no success" would then refuse a real completion. Nor is a body that holds
  `\G`, which reads the attempt's start. Widening the key to the call stack was the alternative;
  the owner's delegate chose the flag (review round 3, re-review), because such bodies are rare and
  the flag cannot be wrong.
- **The lookaround guard rule.** After a blocked lookaround end, the engine runs what follows the
  lookaround *above* the body's entries. A guard then recorded for a repeat in the body would claim
  that everything after the repeat fails, including what follows the lookaround, which depends on
  where the lookaround started; the guard key does not hold that. G1
  (`(?:b|)(?=b*(?!b)\B)(?<!b)` over 'bb', truth P(0, 2)) is the engine witness. The model cannot
  show it: its lookaround bodies are closed sub-searches, so its guards are body-local by
  construction. The rule is therefore reasoned, not measured, and **proving G1 red in the port
  with the rule removed is the gate for landing it** (chunk 6, section 11): if G1 stays green
  without the rule, the builder stops and reports, because the reasoning about the engine's
  stack is then wrong somewhere.
- **Repeat guards outside lookaround bodies need no rule** (section 4.6, step 6). The direction's
  rule, "skip a guard recorded on a path that touched `w`", is not needed for soundness and is
  exponential: `(?:(?(?=.*z)a|a))*b` over 'a' * 10 + 'c' takes 4,094 forks and 30,685 node visits
  under it, against 22 forks and 255 with the guards kept (section 9).

### 4.3 M3. A second-pass success is a partial

At `Success` (`Matcher.cs:10218`) the second pass returns `MatchStatus.Partial` for `match` and
`search`, with span `(MatchPos, |t|)` through the arm every partial shares and the captures as the
path left them (an unknown capture is reported unset). For `fullmatch` it backtracks, because the
match would need `w` to be empty (B3, B5, W4). A run-out outside every body returns Partial as
today.

A second-pass path reaches `Success` only after touching `w`: an undetermined read, a decided read
that differs from the first pass, a run-out, or a start the prefilter admitted through an
undetermined test. The two passes run the same code over the same slice from the same start
(`DoMatch2` :12709, the slice restored at :12873-12875), so they execute identically until the first
step that depends on `PartialSide`; an untouched second-pass success would be a first-pass success,
and the first pass returned Failure. A decided read needs no mark: its answer holds for every
continuation, so the match it leads to exists for every `w` (D4, `(?>a\Z|)(?<!a)a` over 'a').

Searches that keep looking after a success: `(?p)` returns the partial at once; `(?b)`/`(?e)` return
it from `BasicMatch` unrefined, as run-out partials already are (:12336-12339; upstream
`_regex.c:17644`, `:17933`); `FoundMatch` is never set in the second pass (M5, M6).

**The search.** The prefilter calls the same `TryMatchZeroWidth`, so M1 makes it admit a start whose
test is undetermined (A2). When the second pass of a search finds nothing, one more attempt
decides section 1's rule 3, "a match that could only begin inside `w`" (build chunk 2's post-repair
design step, 2026-09-29; `Matcher.MatchFromInsideTheTextToCome`). Its start stands for every start
inside `w`, so both of its sides are unknown: the window is collapsed to the edge, anything consumed
runs out on either side, `\A` and a plain `^` are false forward (as `\Z` and a plain `$` are under
`(?r)`, and `\G` either way), and every other test is undetermined. A success or a run-out is the
partial `(|t|, |t|)`, with no groups. An attempt at the edge itself cannot decide this: `\Z` there
is false for every non-empty `w`, so `(?u)\Z(?:(?:\Bax|aa\b)|(\M))` over '' fails at 0, yet matches
at 1 of '' + 'x'. Upstream's slow path answers failure here, and so did this port's before D11.

### 4.4 M4. A fork budget

The second pass counts **forks**: a blocked construct that keeps more than one outcome (a blocked
cut, a skipped verb, a negative lookaround or condition that passes undetermined, a condition that
takes both branches), replayed ones included. The count is **per attempt**: a search makes one
attempt per start, and a per-call count grows with the number of starts (review round 3 measured
`a*(?=.*z)b` over 'a' * n + 'c' crossing a per-call budget from about n = 256, which answered a
wrong span). Past 256 (|t| + 1) forks in one attempt, the attempt stops and the call answers a
partial at that attempt's start; for a search that is the leftmost start not yet ruled out.
A partial is never a refusal, so the budget cannot break soundness; each firing is a phantom of
class C5 (section 6).

Measured: on the 83,140 grid rows that reach a second pass the most forks in a call is 14
(|t| + 1), and the per-attempt budget fires on none of the 120,000 rows. It fires on X1,
`(?:(?=(.*)z|())a)*b\1\2` over 'a' * n + 'c', at every n from 10, where every lookahead has two
successes with different captures and a backreference turns the guards off; with the budget the
second pass costs 9,664 node visits at n = 10 and 1,277,668 at n = 1000, against a first pass of
288 and 1,017,018 (section 9). Upstream answers X1 with the same phantom. On searches the per-call
count grows with the number of starts, which is why the budget counts per attempt (S4, section 9).

### 4.5 RC5, deletions and asserts

**RC5's fix** is one argument: the insertion arm passes the position after the insertion, as the
substitution arm already does (`Matcher.cs:4522`).

**Deleted**: `MatchState.HitEnd`, `HitEndMatchPos`, `NoteBoundaryAtTruncationPoint` and its seven
calls, and the escalation block in `DoMatch` with its `ClearGroups`. Every partial now comes from
one path, so its captures are attested by that path. **Not before chunk 9** (section 11): S57d's
escalation is what keeps the guard rows (V1, V2, D1-D3, N7, N8, Q1-Q3, T1-T6, G1) green today, so it
stays until the verb and cut chunks (4-8) have landed and each guard row is green without it.

**Debug.Asserts** (rule 10), second pass unless stated:

| id | where | asserts |
|---|---|---|
| A1 | `Success` arm | the attempt touched `w` (a Debug-only per-attempt flag set by every touch, every decided read that differs from the first pass, every run-out and every prefilter admission through an undetermined test) |
| A2 | `Success` arm | `!FoundMatch` |
| A3 | each undetermined read | the enumeration found both answers |
| A4 | each decided word or grapheme read | the answer equals the first-pass answer at that position (only `$`, `\Z`, reversed `\A` and `^` may differ) |
| A5 | start of each second-pass attempt | `WMarks` is empty |
| A6 | first pass and calls without partial | nothing is pushed on `WMarks`; no `WTouch`, `Reopen` or `NoBranch` entry is pushed; the fork count stays 0; every cut is taken |
| A7 | every `Bstack.Count` assignment (through one helper) | no live mark lies at or above the new count, except an unblocked construct's own-frame cut taint, which moves to the end entry the cut pushes, or is dropped when the construct fails (a negative lookaround's definite failure) |
| A8 | every change to `WMarks` | sorted by position, every position below `Bstack.Count` |
| A9 | `Reopen` arm | the mark it pushes back equals its frame's position |
| A10 | a run-out inside a body | the innermost open lookaround or test found through `Pstack` is the one whose body holds the item |
| A11 | guard recording, repeat flagged as inside a body | no taint lives |
| A12 | dedup | the captures and counts kept for an instance are dropped when its frame is popped |
| A13 | memo | in Debug, one hit in 64 also runs the body live and asserts the same records and the same end |

### 4.6 Soundness, a proof sketch

**Claim.** Fix the pattern, the held text `t` and the operation, with the first pass failed. For a
non-empty `w`, let `E_w` be the ordinary matcher on `t + w` (`w + t` under `(?r)`), which is what the
judge runs. Let A be the second pass. If some `E_w` matches from start `s`, A answers a partial from
a start no later than `s`, or the budget answers one first.

**Correspondence.** A state of `E_w` and a state of A correspond when they are at the same node and
text position, their stacks agree below the current point, their captures agree except where A's are
unknown, and every choice A made about `w` on its path is the one `E_w` made. A's steps are `E_w`'s
steps except at reads past the edge (M1, run-outs) and at blocked constructs (M2).

1. **Reads.** M1's answer set holds `E_w`'s answer; a decided answer is the same for every non-empty
   `w` (A3, A4). So every forward step of `E_w` from a corresponding state has one in A. At a run-out
   outside any body A succeeds. Inside a body A continues as if the body matched, with unknown
   captures, which corresponds to any body success of `E_w`; A's backtracking covers `E_w`'s failure.
2. **Pops.** Call the order in which a matcher pops backtrack entries its pop sequence. `E_w` departs
   from A in three ways only. It fails a read A passed: it skips a subtree of A and pops the next
   entry below, which A pops later. It goes on past a run-out inside a body. Or it takes a cut or
   decision that A, blocked, did not. The last two are **exits**: `E_w` discards a construct's entries
   without popping them. So `E_w`'s pop sequence is a subsequence of A's, provided A never discards an
   entry `E_w` later pops (step 3).
3. **Unblocked cuts discard nothing `E_w` needs.** Say A cuts, unblocked, at E and discards an entry
   `x` above E's frame, and some `E_w` later pops `x`. `E_w` pushed `x` in a corresponding state, and
   from there A's path read nothing from `w` (no touch above E), so `E_w` takes the same forward
   steps. It can leave that stretch only by an exit before the cut, and every exit is at a construct
   C that A marked: a blocked end (cut taint on C), a skipped verb with scope C (verb taint on C), a
   commit of a construct open at a run-out or a verb after the edge whose scope was open there (the
   run-out taint on C), or the end of the lookaround whose body ran out (the run-out's touch blocks
   that end, so C carries a cut taint). Entries `E_w` makes after the edge are not A's, so exits that
   discard only those do not matter.
   `x` lies inside C's region, because the exit discards it, so C's frame is still on A's stack at the
   cut and its mark is live. If C is newer than E, or C is E with a verb taint, the mark blocks E: a
   contradiction. Otherwise C's region contains E's frame and `x`, so `E_w`'s exit discarded `x`: a
   contradiction with `E_w` popping it. So `E_w` reaches the same cut in a corresponding state and
   discards `x` itself. A definite decision at E is the same argument: every `E_w` that reaches E
   either leaves E's region by an exit or reaches this body success, so its body succeeds too and it
   decides the same way. (For a negative lookaround, an exit at E itself through its own cut taint
   is an earlier body success of `E_w`, which decides the same way.)
4. **Blocked constructs keep every outcome `E_w` can take.** A blocked positive lookaround or positive
   test continues after every body success (the dedup aside, step 7). A blocked negative lookaround
   fails only on an unblocked success (step 3) and otherwise passes, and `E_w`'s failure there
   backtracks below it, where A goes after its continuation. Conditions keep both branches, and so
   do group conditions on unknown groups. A skipped verb is an exit for `E_w` (step 3) and more
   backtracking for A. The touch after each such decision makes every later construct see it as
   depending on `w`.
5. **Verbs whose prune is triggered by a later failure that depends on `w`.** A verb acts when
   backtracking reaches it. Everything above its entry, including the continuation whose failure
   triggers it, has then been explored by A, so every `E_w` in which that continuation succeeds has
   already been matched by an A success. Every `E_w` in which it fails backtracks into the verb in a
   corresponding state; the rule is checked at that moment against the path up to the verb, so if A
   fires the verb, every such `E_w` fires it with the same scope and the same `(*SKIP)` position.
   Hence a fired `(*SKIP)` moves every universe's next start to the same place.
6. **Repeat guards.** A records a guard for repeat R at position p when the subtree it explored from
   there has no success. Outside lookaround bodies what follows R is fixed by the path from (R, p). A
   guard is recorded only when R's entry is popped, so no construct older than R cut below it during
   the subtree: each one reached was blocked, the most permissive exploration there is. Marks only
   block, and a blocked construct keeps a superset of outcomes, so A's exploration grows with its
   marks (monotonicity), and this one covers the subtree for any arrival and, by steps 1-4, for
   every `E_w`. Captures matter only to backreferences and group conditions, where guards are off
   already (upstream's `RE_STATUS_REF`, `_regex.c:23400-23420`). So the subtree fails for every `w`
   and every arrival, and the guard prunes nothing real. Inside a lookaround body what follows a
   blocked end depends on where the lookaround started; M2 records no guard while a taint lives, and
   before any taint the subtree ended inside the body, which does not depend on the start. Fuzzy
   counts change nothing here: neither engine consults a guard in a fuzzy pattern (upstream
   `is_repeat_guarded`, `_regex.c:9563-9566`; the port, `MatchState.cs:1433`), because the same
   position can be reached with a different error budget.
7. **Dedup.** A second continuation of one lookaround instance with the same captures and fuzzy
   counts (and change list) starts at the same node, position, captures and counts as the first. Both block every construct older than the
   lookaround: the first through the mark that blocked the lookaround (a touch, or a taint inside
   it), the second through the cut taint the instance has carried since then. Marks newer than the
   lookaround are made by each continuation itself. So the two explore the same states, and the
   first failed.
8. **Memo.** A body's exploration reads only its own start state: the position, the captures and
   the fuzzy counts (and change list) coming in, **provided the body holds no group call, recursion
   or `\G`**, which the memo excludes. The rest of what an arm reads is fixed for the whole call:
   the node and its compiled flags and direction, the text and the slice (restored between the
   passes and moved only by a `(*SKIP)` that fires at the top level, which ends the attempt, not the
   body), and the pattern's guard switches. The open-call state is read only by call and recursion
   nodes and `\G`'s anchor only by `\G`, and a body without them never reads either. Every cut and
   definite decision inside it checks marks newer than its own frame, which are made inside the body; the body's verbs unwind no further than the lookaround
   itself; the lookaround's own cut taint does not block it (step 3). What does depend on the outside
   is which guards get recorded, and guards change the work, not the outcome (step 6). So the
   sequence of body successes, each with its captures, counts and blocked flag, and the way the body
   ends, are the same in every attempt that enters it with the same start state, and replaying them
   is running it. A replayed blocked success carries a touch, which blocks every construct older than
   the lookaround, as the original's inner mark did.
9. **Search and the rest.** A tries starts in the same order as `E_w`. At a start where A finds
   nothing, no `E_w` matches (steps 1-8), so the truth's start is not earlier than A's. M3 rejects for
   `fullmatch` only successes that end at |t|, and `E_w`'s must end at |t + w| > |t|. M4 answers a
   partial.

So the state in which `E_w` succeeds corresponds to one A visits, unless A answered earlier, and A's
answer is a partial from a start no later than `s`.

**What the sketch leans on and the build must check:** that A's steps are the engine's steps outside
the listed arms (A6, and the port grid of section 8 against the model); that every truncation in the
second pass goes through the helper of A7; and that `Pstack` finds the innermost open body (A10).

## 5. Rows and tests the design moves that are pinned today

The builder re-judges each of these with the probe before editing the pin; the design's answers are:

| test (`Gaps/Engine/PartialMatchingTests.cs`) | pinned | design |
|---|---|---|
| `A_reverse_search_for_a_boundary_at_the_end_of_an_empty_subject_finds_no_partial_here` (:383), `(?r)\b$` over '' | None on all three doors (PERMANENT) | search and match P(0, 0), fullmatch None (P1, P2) |
| `A_boundary_that_consumed_nothing_reports_no_partial` (:1979) | None on all doors | `\b`, `\b\b` over '': P(0, 0) on search and match; `\B` over 'a': search P(1, 1); fullmatch None throughout |
| `A_partial_found_the_ordinary_way_is_kept_in_place_of_the_boundary_one` (:1870) | `aa\B` search 'aa' (1, 2) | (0, 2) |
| `A_boundary_partial_reports_no_groups` (:1915) | groups unset | the path's groups: (0, 1) and (1, 2) |
| `A_reversed_boundary_at_the_start_of_the_text_reports_a_partial_upstream_denies` (:2072) | fullmatch `(?r)\Ba` 'a' P(0, 1) | fullmatch None (no text on the left can be matched by a one-character pattern); match unchanged |
| `A_reversed_boundary_partial_is_what_the_gate_wave_found` (:2108) | fullmatch P(0, 1), no groups | None, for the same reason |
| `A_boundary_partial_is_what_the_oracle_waves_found` (:2035) | its fullmatch rows partial | re-judge each row; `b\B(?(?<![\w\s])\p{Ll})` fullmatch 'b' becomes None |

Docs to change with the build: `docs/DIVERGENCES.md:60` (S57d's row, rewritten to this rule),
COMPARISON rows, ledger 21 and 27 notes, a new ledger entry for upstream's lookaround phantom (L2,
L5, L6 are upstream's too) with a draft report marked DRAFT - NOT FILED, DECISIONS, and
`ExpectedDivergences` entries for the rows where upstream resolves against the held text.

## 6. What the design deliberately does not decide

Every phantom the grids found falls into one of five classes. The counts are over the 120,000 rows
of section 8 (60,000 random, 60,000 shaped); a row counts once, in the first class that applies.

| class | rows | example (truth None, design partial) | shared by | register |
|---|---|---|---|---|
| **C1** search convention: a match that could begin inside `w` is reported at `(\|t\|, \|t\|)` even when the pattern can never match | 4,377 in the model; none since build chunk 2's attempt from inside `w` | search `(*FAIL)` over '' (the model's; the port now answers None) | nobody: upstream and PCRE2 answer None (corrected in build chunk 2) | section 1, rule 3 |
| **C2** limit 2: once a path consumes a character of `w`, the rest of the pattern (or of the body) is presumed to match | 8,731 | search `\n*+\n` over 'b'; B4, Z3, A3 | upstream, PCRE2, the port | D19 |
| **C3** limit 1: reads of `w` are independent, so two that no single `w` satisfies together still pass | 63 | match `\b$` over ''; J1, J2 | PCRE2, the port | D18 |
| **C4** this design: a blocked construct keeps every outcome, including the alternatives made after the read that blocked it | 589 | match `\m(*PRUNE)((?!^\w))` over ''; N4-N6, T8 | PCRE2 on N4-N6, the port | D18 |
| **C5** the fork budget fired | 0 | X1 | upstream on X1 | D18 |

1. **C1** is gone, and the earlier text was wrong to say upstream shares it: search `(*FAIL)` over ''
   and `(?r)a(?!)` over 'b' are None upstream (regex 2026.9.10, measured 2026-09-29) and in PCRE2,
   and a pattern no text can match must never report a partial, which follows from section 1. Build
   chunk 2 answered every exhausted search with the edge partial at first (50abbe7), then gated it
   on a read at the edge (70418ff), which left 62 phantoms in 3,000 random rows and a real
   completion refused. Its post-repair design step replaced both with the attempt from inside `w`
   (section 4.3): a start there is judged like any other, so the only phantoms it can add are C2
   (a run-out presumes the rest) and C3 (independent reads). Measured over the same 3,000 rows
   (model generator, seed 29) against 11ccae7, every row where the answers differ judged by the
   brute force: 0 new misses, 66 misses fixed, 72 new phantoms, 60 of them C2 (for example search
   `(?<= )^(*PRUNE)` over 'a', whose lookbehind consumes the text to come) and 12 C3 (for example
   match `\b$` over ''). The 4,377 rows the model counted under C1 predate this rule.
2. **C2** is issue 367's limit. Decision 4 (section 10) keeps it out of D11's scope.
3. **C3 and C4 are one SHORTCUT, registered as D18.** Both err towards a phantom, never a refusal.
   C3's ceiling is contradictory reads at the one edge position; its upgrade is a per-path class mask
   over the M1 property sets, saved with the touch marks. C4's ceiling is that a blocked construct
   discards nothing; its upgrade is a blocked cut that discards the entries above the newest mark
   (they belong to one universe only), which is what round 2 attempted. It needs its own proof, and
   round 2's barriers show how easily it goes wrong, so it is a separate slice after D11. The model's
   C4 count also holds some C2 rows: a run-out inside a negative lookaround's body leaves no tag on
   the path, so the classifier cannot see it.
4. **C5** answers a partial where the search would otherwise take exponential time. It fired on no
   grid row.

## 7. Tests

**Red tests** (`tests/FuzzyRegex.Tests/OpenDefects/OpenDefectTests.cs`, `[Explicit]`, added in
round 2). This design step does not edit them; the build makes three changes before its first engine
commit, and proves each moved row's state: N4, N5 and N6 leave the "refused" test for a new D18
test; X2 joins the "refused" test; round 3's guard rows join the "reported" test.

- `A_partial_is_refused_where_no_longer_text_can_complete_the_match`: B1, B2, B3, B5, B6, B7, L2,
  L5, L6, L7, E1, E3, V4, R1, R2, N1, N2, N3, and X2 (`(?:(?(?=.*z)a|a))*b` over 'a' * 20 + 'c':
  upstream and the port P(0, 21), truth None);
- `A_partial_is_reported_where_longer_text_completes_the_match`: W1, W2, W3, W5, W6, L1, A2, A5, P1,
  P2, P3, S3, V1, V2, V3, D1-D4, N7, N8, and round 3's rows below;
- new, D18 (stays red until D18 is built): `A_blocked_cut_keeps_the_alternatives_made_after_the_read`:
  J1, J2, N4, N5, N6, T8, X1.

Round 3's rows, measured on 2026-09-28 (judge: `d11-brute-judge.py`; port: main through the port
probe; upstream 2026.9.10). The port and upstream answer every one of them with a partial today, so
all the "reported" rows below are guards: green now only through S57d's escalation or RC3's run-out
partial, both of which the design removes.

| id | call | truth | upstream | port | design | guards |
|---|---|---|---|---|---|---|
| Q1 | match `(?!(?>a\b\|)(?<!a)a(*PRUNE)(?<=b)\|a)` 'a' | P(0,1) | None | P(0,1) | P(0,1) | verb taint blocks its own scope |
| Q2 | match `(?=a(.))(?(1)a\|b)` 'a' | P(0,1) | P(0,1) | P(0,1) | P(0,1) | unknown capture, both branches |
| Q3 | match `(?!a.(*PRUNE)b\|a)a` 'a' | P(0,1) | P(0,1) | P(0,1) | P(0,1) | run-out taint, verb after the edge |
| T1 | match `(?:(?>a\B\|)(?<!a)a(*PRUNE)(?<=b)\|a\B)` 'a' | P(0,1) | None | P(0,1) | P(0,1) | cut taint (the timeline of 4.2) |
| T2 | match `(?!a\B(*PRUNE)(?<=b)\|a)` 'a' | P(0,1) | None | P(0,1) | P(0,1) | verb taint at the negative's frame |
| T3 | match `(?=a(.))\1` 'a' | P(0,1) | P(0,1) | P(0,1) | P(0,1) | backreference to an unknown capture |
| T4 | match `(?(?=a\b)(?<=b)\|a)` 'a' | P(0,1) | None | P(0,1) | P(0,1) | positive test, blocked: both branches |
| T5 | match `(?(?!a\b)a\|(?<=b))` 'a' | P(0,1) | None | P(0,1) | P(0,1) | negative test, blocked: both branches |
| T6 | match `(?:(?(?=a\B(*PRUNE)(?<=b))x\|a\B))` 'a' | P(0,1) | None | P(0,1) | P(0,1) | verb skipped in a test |
| T8 | match `(?:(?=a\B\|)(?<!a)a(*PRUNE)(?<=b)\|a\B)` 'a' | None | None | P(0,1) | P(0,1) | C4 (D18 test) |
| G1 | match `(?:b\|)(?=b*(?!b)\B)(?<!b)` 'bb' | P(0,2) | P(0,2) | P(0,2) | P(0,2) | the lookaround guard rule (engine only) |
| X1 | match `(?:(?=(.*)z\|())a)*b\1\2` 'a' * 20 + 'c' | None | P(0,21) | P(0,21) | P(0,21) | C5 (D18 test); upstream 0.13 ms |
| X2 | match `(?:(?(?=.*z)a\|a))*b` 'a' * 20 + 'c' | None | P(0,21) | P(0,21) | None | guards kept on touched paths |
| F1 | match `(?:(?=(?:ax\b\|ay\B){e<=1})az){e<=1}` 'ay' | P(0,2) | None | P(0,2) | P(0,2) | the dedup key holds the fuzzy counts (port grid only, 8.1) |
| S4 | search `a*(?=.*z)b` 'a' * 1000 + 'c' | P(1001,1001) | P(0,1001) | P(0,1001) | P(1001,1001) | the memo and the per-attempt budget; the port 16 ms, upstream 0.28 ms today |

**Witnesses the build must add** (VERIFICATION rule 12). Each is proved red with its part removed.
The model has confirmed each "removed" answer (`d11-direction-model.py rows <variant>`):

| part | witness | with the part removed |
|---|---|---|
| M1 decided at the edge | B1, B2, B7; B6 for "decided later on held text" | phantom |
| M1 undetermined, simple | C1, C5, P3 | miss |
| M1 `(?w)` window | W1 (WB6), W3 (WB4 skip), W5 (WB12), W6 (WB7b) | miss |
| M1 `$` at the edge and at a final newline | E2 (undetermined), E3 (decided false) | miss / phantom |
| M1 decided false where the first pass said true | E1, D4, N2, N3, R1, R2; R3 control | phantom |
| M2 touch blocks a cut | D1, D2, D3 (atomic, possessive), V1, V2, V3 (verbs), C3 (`\X`) | miss |
| M2 an unblocked cut is taken | V4, N1, N3 | phantom |
| M2 cut taint | T1 (plain count of reads) | miss |
| M2 own-frame cut taint does not block | N1, L6 | phantom |
| M2 verb taint blocks its own scope | Q1, Q3, T2 | miss |
| M2 negative lookaround: definite / undetermined | L6, L7 / C2 | phantom / miss |
| M2 run-out in a body | L1, C7 (positive), L6, C2 (negative), N7 | miss or phantom |
| M2 run-out taint | N8 (open atomic), Q3 (verb after the edge) | miss |
| M2 unknown captures | Q2 (condition), T3 (backreference) | miss |
| M2 conditions | L4, L5, T4, T5, T6 | miss / phantom |
| M2 lookaround guard rule | G1, in the port (the model cannot show it, 4.2) | miss |
| M2 dedup | `(a)(?:(?=.*z\|)a)*b\1` over 'a' * 20 + 'c' in under 10 ms | exponential (budget) |
| M2 dedup key holds the fuzzy counts | F1 | miss (keyed on captures alone) |
| M2 memo | S4 in under twice the first pass's time | cubic, and the budget fires |
| M2 memo off for bodies with calls, recursion or `\G` | chunk 9, in the port grid: a lookaround whose body calls its enclosing group, reached at one position with the same captures once inside the call and once outside it (for example search `(a(?=(?1)?\b)b?)` shapes over 'aab'), and a body holding `\G` reached from two starts; each proved a miss with the flag removed, or a `file:line` argument (rule 12) that none can exist | miss |
| M3 fullmatch backtracks | B3, B5, W4 | phantom |
| M3 leftmost start | W2, `aa\B` search 'aa' (0, 2) | wrong span |
| M3 POSIX, best match | M5, M6 | wrong answer |
| M3 prefilter candidate / no candidate | A2 / S3, with A4 as the slow-path control | miss |
| M4 budget | X1 over 'a' * 1000 + 'c' answers in under 100 ms | exponential |
| M4 counted per attempt | S4 answers P(1001, 1001) | a wrong span (P at an earlier start) |
| RC5 insertion | Z1, Z2 (green today only through HitEnd) | miss |
| reversed | C6, A1, A5, P1, P2, R1, R2, R3 | miss or phantom |

## 8. Grid

### 8.0 Already run, on the model (2026-09-28)

`tools/probes/d11-direction-model.py grid 20000 SEED [--shapes]`, seeds 1-3 of each. The judge is
`tools/probes/d11-brute-judge.py`: upstream `regex` 2026.9.10 **without** partial, over every
continuation of one to four characters over `a b 1 \u05D0 space : " \n` and the pattern's own
letters. The model shares no code with it.

- **Random grid:** patterns of up to three items, two levels deep, from atoms (`a b . [ab] \w \W`,
  space, `\n`), the predicates `\b \B \m \M $ \Z ^ \A`, capture groups, backreferences, `(?:|)`,
  `(?>|)`, the four lookarounds, group conditions, lookaround conditions (positive and negative
  tests), the quantifiers `* + ? *+ ?+ ++ *? {1,2}` and the verbs `(*PRUNE) (*SKIP) (*FAIL)`
  anywhere, including inside repeats, conditions and lookarounds. Flags none, `(?r)`, `(?m)`,
  `(?rm)`. Subjects up to three characters over {a, b, space, \n}.
- **Shaped grid:** ten templates built on the failure modes of rounds 1-3 (a later cut or verb after
  a blocked one; a verb inside a body before or after the edge; a run-out inside an atomic inside a
  body; one lookaround reached from two starts; verbs in a condition's test; a condition in a
  repeat; a verb in a repeat; nested atomics; nested lookarounds), filled from 22 pieces that read
  the edge, fail on held text or capture.
- **Semantics check.** Before judging a row, the model's own concrete matcher is compared with
  upstream on the held text and on 24 sampled `t + w`. The 50 rows where they differ are left out of
  the gate (verbs inside `(?m)` lookbehinds and `(?(?!)...)`, among others); which side is right
  there is not claimed. On 9,000 random (pattern, text) pairs the two matchers differ on one.
  **The 50 re-judged** (review round 3): judged anyway, they give 13 agreements, 23 phantoms, 10
  rows where the model's first pass and upstream's disagree about a complete match, and 4 misses.
  All 4 come from the model's own verb semantics, not from the second pass: three are
  `(?m)(?<=$(*SKIP)(?>a\b|)|[ab]*)\b` over 'ab' and 'a ', where upstream finds a complete match
  on the held text and the model's first pass does not (D11 does not change the first pass), and
  one is search `(?rm)\n(?:((*SKIP).)|(*FAIL)((*PRUNE)(*FAIL))).` over 'a  ' (judge P(0, 2), model
  P(0, 1)), a reversed search with `(*SKIP)`, the case where the two matchers were already seen to
  differ on a known text. So the model cannot judge these rows; the port grid does (8.1). The three
  lookbehind rows are an upstream inconsistency, ledger entry 53 (DRAFT - NOT FILED): with `(?m)`,
  a `(*SKIP)` after `$` in a lookbehind stops acting. The port does not copy it; it answers those
  rows as the model does.

| grid | rows | miss | phantom | agree | semantics rows | misses with the cut taint removed | with the run-out taint removed |
|---|---|---|---|---|---|---|---|
| random, seeds 1-3 | 60,000 | **0** | 10,143 | 49,826 | 31 | 0 | 0 |
| shaped, seeds 1-3 | 60,000 | **0** | 3,617 | 56,364 | 19 | 66 | 33 |

Guards on and off give the same answer on all 120,000 rows. Rule 11, per construct (rows present,
rows where a second-pass mechanism fired, misses): every construct is non-zero, for example atomic
45,627 / 6,470 / 0, verb in a repeat 8,079 / 960 / 0, verb in a condition 14,955 / 2,718 / 0,
verb in a lookaround 28,165 / 4,477 / 0, look in look 30,881 / 5,347 / 0, atomic in look
19,917 / 3,416 / 0, group condition 2,982 / 562 / 0, backreference 8,096 / 1,301 / 0, `(?r)`
32,398 / 3,515 / 0, `\n` 39,841 / 4,280 / 0. Mechanism events, rows where each fired: undetermined
read 11,479; memo replay 1,302; run-out in a body 8,030; blocked cut 6,422; negative undetermined 4,724; condition kept
both ways 2,808; verb skipped 1,623; dedup 1,485; guard hit 834; backreference to an unknown capture
29; guard refused 26; group condition on an unknown capture 3.

### 8.1 The port's grid (the build)

1. **Brute-force grid on the port, Debug build** (rule 11). The same two generators, extended to
   what the model does not parse: `(?w)`, `\X`, `(?a)`, `(?p)`, the fuzzy forms `{i<=1}` and
   `{e<=1}` alone and under `(?b)` and `(?e)`, a Hebrew letter, `"`, `,`, and the subjects
   'a:\u0301', 'e\u0301', '\u05D0"'. Truth: `d11-brute-judge.py`, with the port's own non-partial
   matcher as the completion judge for `(?w)` (upstream's `(?w)` is ledger 40's). Three additions
   from review round 3 of this design:
   - **fuzzy sections inside lookaround bodies**, with an enclosing budget, in the shape of F1:
     `(?:(?=X{e<=1})Y){e<=1}` over the shape grid's pieces, alone and under `(?b)`/`(?e)`. The model
     has no fuzzy matching, so the fuzzy-keyed dedup and memo are judged here only. Adding fuzzy
     counting to the model was weighed and not done: to be a check, the model would need upstream's
     error ordering and its `(?b)`/`(?e)` refinement, validated against upstream, which is a larger
     piece of work than the one key field it would check, and the port's grid judges the real
     engine against the same brute force;
   - **group calls and recursion inside lookaround bodies**: `(?1)` and `(?R)` in the bodies of the
     shape templates, including a body run-out inside a called group. The run-out taint and A10
     find the open constructs through `Pstack`, and calls push `Pstack` entries of their own, so
     this is where A10 is tested. The model has no group calls;
   - **memo exclusions**: lookaround bodies holding `(?1)`, `(?R)` or `\G`, reached from several
     starts and inside and outside a call, for the witness of chunk 9;
   - **the model's semantics rows re-judged on the port**: the rows of 8.0 where the model's own
     matcher and upstream disagree are run on the port against the judge like any other row, and
     each one gets a verdict (port right, or a miss).
2. **What the gate counts.** The Debug build keeps one counter per mechanism event, read per row: M1
   undetermined, M1 decided and different from the first pass, touch pushed, cut blocked (per kind),
   negative undetermined, condition both ways, verb skipped, run-out in a body, run-out taint set,
   unknown capture set, dedup, guard refused, M3 success turned partial, M3 fullmatch backtrack,
   prefilter candidate, prefilter no-candidate partial, budget fired, RC5 insertion run-out. **Gate:
   every counter non-zero; 0 misses on every construct; the port equals the model on every row the
   model parses outside its semantics rows; phantoms only in C1-C5, each counted, and the C3-C5 rows
   copied to D18's row.**
3. **Before/after diff of the same grid**: every moved row is classified as fixed, limit class, or
   regression (none allowed).
4. **Oracle**: `tools/run-oracle.ps1` at 7, 4242 and 20260927, and the `partial`, `partial-sliced`,
   `interactions`, `verbs` and matrix generators at seeds 101-104, where cluster A came from. The
   expected movement is more port-right rows of the ledger 21 family; they get one
   ExpectedDivergences entry whose discriminator re-runs the row with an oracle-only ablation that
   resolves predicates against the held text (the precedent is ledger 40's `UpstreamDefaultBoundary`)
   and requires upstream's recorded answer back.
5. **The S57d and S57f wave rows** and ledger 27's rows, re-judged by the judge.

## 9. Performance

**A call without partial, and the first pass of a partial call** (`PartialSide == PartialNone`), pay
one predictable branch at each of these sites and nothing else:

- the cut arms `EndAtomic` :7211, `EndLookaround` :7968, `EndConditional` :7279, and the verb arms
  `Prune` :11679 and `Skip` :11645 (the rule is only consulted in the second pass);
- the start-frame arms `Atomic` :10465, `Lookaround` :11489, `Conditional` :10526 (mark drops and
  the negative's touch);
- the guard recording sites, where the new per-repeat flag joins the existing test;
- `Success` :10218.

The M1 helpers' edge branch exists today for S57d and the design deletes S57d's work behind it.
The run-out arms are partial-only code already. `WMarks`, the dedup list and the fork counter are
null or zero until the second pass uses them; no frame gains a field; `WTouch`, `Reopen` and
`NoBranch` are never pushed outside the second pass (A6). Marks are dropped only in the arms listed
in 4.2, so the backtrack loop is unchanged. The build measures an atomic-heavy, a lookaround-heavy
and a verb-heavy benchmark against main; the budget is 10% (builder rules) and within noise is
expected.

**The second pass**, measured on the model in node visits (first pass + second pass), 2026-09-28:

| pattern, subject | n = 10 | n = 100 | n = 1000 | forks at 1000 |
|---|---|---|---|---|
| `(?:(?=.*z)a)*b`, 'a' * n + 'c' | 31 + 222 | 211 + 11,112 | 2,011 + 1,011,012 | 2,002 |
| `(?:(?=.*z\|)a)*b`, 'a' * n + 'c' | 244 + 244 | 11,314 + 11,314 | 1,013,014 + 1,013,014 | 2,002 |
| `(?>a*)*b`, 'a' * n + 'c' | 23 + 23 | 113 + 113 | 1,013 + 1,013 | 0 |
| `(?>a*)*b`, 'a' * n | 23 + 16 | 113 + 106 | 1,013 + 1,006 | 0 |
| `(?:(?>a\|ab))*c`, 'ab' * n | 16 + 16 | 16 + 16 | 16 + 16 | 0 |
| `(?:(?>a(?=.*z)\|ab))*c`, 'ab' * n | 350 + 417 | 21,410 + 22,107 | 2,014,010 + 2,021,007 | 3,000 |
| `(?:(?>a\b\|a))*c`, 'a' * n | 98 + 85 | 908 + 805 | 9,008 + 8,005 | 1 |
| `(?:(?!.*z)a)*b`, 'a' * n + 'c' | 222 + 222 | 11,112 + 11,112 | 1,011,012 + 1,011,012 | 2,002 |
| `(?:(?(?=.*z)a\|a))*b`, 'a' * n + 'c' (X2) | 233 + 255 | 11,213 + 11,415 | 1,012,013 + 1,014,015 | 2,002 |
| `(?=(?:a*)*b)`, 'a' * n + 'c' | 114 + 114 | 5,559 + 5,559 | 505,509 + 505,509 | 0 |
| `(a)(?:(?=.*z\|)a)*b\1`, 'a' * n + 'c' | 215 + 215 | 11,105 + 11,105 | 1,011,005 + 1,011,005 | 2,000 |
| `(?:(?=(.*)z\|())a)*b\1\2`, 'a' * n + 'c' (X1) | 288 + 38,873 | budget | budget | - |

- Every row but X1 is within 1.1 times its first pass, or quadratic where every lookahead scans to
  the end: `(?:(?=.*z)a)*b` stops after one lookahead in the first pass, but in the second every
  lookahead passes, so each of the n iterations scans the rest of the text. That is the pattern's
  own cost on a completed text such as 'a' * n + 'z'. At n = 20 the second pass is 632 node visits;
  upstream answers the same call in 0.2 ms by ending the match at its first run-out, which is the
  RC3 behaviour the design removes.
- **Guards are what keep the second pass polynomial, and the dedup keeps it polynomial where guards
  are off.** With guards recorded only on paths that never touched `w` (the direction as first
  stated), X2 is exponential (4,094 forks and 30,685 visits at n = 10, the 5,000,000-visit cap at
  n = 20); with no guards, `(?=(?:a*)*b)` is too (6,147 visits at n = 10, the cap at n = 20).
  Without the dedup, `(a)(?:(?=.*z|)a)*b\1` takes 442,855 visits at n = 10.
- **X1** has two lookahead successes with different captures per iteration and no guards (a
  backreference follows): 2^n forks. The fork budget (M4) answers it, with the phantom upstream
  gives too.
- **Across the grid** (83,140 rows with a second pass): the second pass is 1.00 times the first at
  the median, 2.12 at the 99th percentile and 15 at worst; forks per (|t| + 1) are 0 at the median,
  3 at the 99th percentile and 14 at worst.
- The build measures with the `f9b67f9` harness (its eight cases, at 10 KB, 100 KB and 1 MB), the
  rows above in the port, a search over a long failing subject, and a verb-heavy partial search,
  against main.

**Searches** (review round 3). A search makes one attempt per start, and in the second pass every
attempt re-runs lookarounds that reach the edge. Measured on the model, node visits, first pass +
second pass, over 'a' * n + 'c':

| pattern | n = 100 | n = 1000 | n = 3000 | second / first at 3000 |
|---|---|---|---|---|
| `a*(?=.*z)b`, before the memo | 31,318 + 389,964 | 3,013,018 + 338,849,514 | not run | - |
| `a*(?=.*z)b` | 31,318 + 31,414 | 3,013,018 + 3,014,014 | 27,039,018 + 27,042,014 | 1.00 |
| `a*(?!.*z)b` | - | 3,514,520 + 3,014,014 | 31,543,520 + 27,042,014 | 0.86 |
| `a*(?(?=.*z)b\|b)` | - | 4,016,022 + 4,518,517 | 36,048,022 + 40,555,517 | 1.12 |

All three answer P(|t|, |t|), the truth (the judge at n = 20: P(21, 21), completion 'bz');
the port and upstream answer `a*(?=.*z)b` with P(0, 1001) today. Forks per call reach 9,009,002
at n = 3000, but no attempt makes more than about n of them, so the per-attempt budget does not
fire. Both passes are quadratic in n here; that is the first pass's own order in this model.

## 10. Open questions for the owner

1. **P1-P3 and A5 invert a pin marked PERMANENT** (`(?r)\b$` over '' None) and S57d's "consumed
   nothing" narrowing. Decided below (1).
2. **Limit 1**: decided below (2), now joined by C4 and C5 in D18.
3. **E1, `\Z` at the edge is false in the second pass**: decided below (3).
4. **Limit 2** stays with issue 367: decided below (4).
5. **C4 and C5, new in round 3.** Accept them into D18 as the same SHORTCUT (589 of 120,000 grid
   rows, and X1), or hold D11 for C4's upgrade? Recommendation: accept. The owner's ledger-21
   ruling allows a bounded, documented class of phantoms; C4 is bounded by construction (only
   constructs that are blocked), counted by the grid, and PCRE2 and the port answer the same way on
   N4-N6 today.
6. **The budget constant.** 256 (|t| + 1) forks never fired on the grid, whose worst is 14 (|t| + 1).
   Accept, or choose another?

### Decisions (2026-09-28, under the owner's delegation of semantic questions)

Measured in regex 2026.9.10 on 2026-09-28: `(?r)\b$` over '' search P(0, 0), match and fullmatch
None; `\b` over '' None on all three doors; `a\Zb` over 'a' P(0, 1) on all three; `a\b\B` over 'a'
search P(1, 1); `(?:a\B)+` over 'a' search P(1, 1).

1. **Invert the pin.** The PERMANENT pin came from S57d's "consumed nothing" narrowing, which the
   rule in section 1 replaces. `\b` over '' + 'a' matches at 0, so a longer text completes the match
   and refusing it breaks ledger 21's soundness ruling. Upstream already agrees for `(?r)\b$` on
   search; for `\b` over '' the port follows PCRE2 and the definition against upstream (pinned as a
   divergence).
2. **Accept limit 1 for D11, as a SHORTCUT, and register it as its own defect (D18).** It errs only
   towards a phantom partial, never a refused completion, so it cannot hide a match. But `a\b\B` over
   'a' is a known wrong answer and the no-known-bugs rule applies. The class mask is a separate
   slice after D11; the D11 grid counts the phantoms it accounts for and lists them in D18's row.
3. **Take the definition: `\Z` at the edge is false in the second pass.** The second pass asks only
   about non-empty continuations (section 1, rule 2), and with any continuation `|t|` is not the end
   of the string. `a\Zb` over 'a' is None; pinned as a divergence from upstream and PCRE2, with a
   ledger entry and a draft report (DRAFT - NOT FILED).
4. **Limit 2 is out of D11's scope, and it is registered (D19).** It is not declared unfixable,
   but it is decidable only for patterns with **no backreferences and no subroutine calls or
   recursion**. There "can the rest of the pattern still match some text" is an emptiness check on
   a regular language (lookarounds included, since regular languages are closed under the
   intersection and complement they add), and even that can need automata exponential in the size
   of the pattern. A group call or recursion makes the language context-free, and a lookahead then
   intersects two context-free languages, whose emptiness is undecidable. Backreferences are issue
   367's own hard case. D19 starts with research on that split and on the cost of the regular case.
   (Corrected by the coordinator in review round 1.)

## 11. The round 3 direction, tested

The orchestrator's direction was: keep M1 and M3; count a lookaround whose body touched `w` as
passed, with its captures unknown; let a condition whose test touched `w` take both branches; skip a
cut (atomic, possessive, `(*PRUNE)`, `(*SKIP)`, repeat guard) when the path since the cut's entry
touched `w`, using a restorable count. The core holds: "undetermined dominates" is sound and
simple, and it replaces round 2's barriers, refinements and third run-out outcome. Five parts of it
as stated were wrong, each shown by a row:

1. **A restorable count is not enough.** A path can have read nothing since a construct's entry and
   still exist only in some universes, because it was reached by backtracking through a cut the
   engine skipped. T1 is a miss under the count alone; the cut taint fixes it (66 shaped-grid misses
   without it).
2. **A run-out inside a body skips the body's own cuts and verbs.** N8 and Q3 are misses unless the
   run-out taints the constructs open at the edge and, where the body has a verb, the scopes.
3. **"Touched counts as passed" is too coarse for a negative lookaround.** A body success on a path
   that is not blocked is certain, and the assertion fails: L6 and L7 turn phantom otherwise.
4. **A skipped verb must block its own scope's later decisions, and a construct's own skipped cut
   must not.** Q1, Q3 and T2 are misses in the first case; N1 and L6 are phantoms in the second.
5. **Repeat guards must not be skipped on touched paths.** Soundness does not need it outside
   lookaround bodies (section 4.6, step 6), and it makes X2 exponential. Inside lookaround bodies
   the rule is the other way round: no guard while a taint lives (G1).

Performance needed three more parts than the direction had: the dedup, the memo, and the fork
budget with its partial fallback. They are counted inside M2 and as M4, which keeps the design at
four mechanisms.

### 11.1 Build chunks (adopted from review round 3)

Each chunk is under an hour, is reviewed before the next starts, and ends in a commit with the
ratchet green. S57d's escalation stays until chunk 9, so the guard rows stay green throughout.

| chunk | content | done when |
|---|---|---|
| 1 | the `Bstack.Count` helper, the `WMarks` list, asserts A5-A8, the Debug counters, the red-test moves of section 7 | every truncation goes through the helper; A5-A8 hold on the whole suite; the moved rows are red or green as section 7 says |
| 2 | M1 on the plain predicates (word, line and string anchors), with the touch mark | M1 witnesses red without it, green with it |
| 3 | M3: a second-pass success is a partial; `fullmatch` backtracks; the prefilter | M3 witnesses, A1, A2 |
| 4 | the cut rule for atomic groups, possessive repeats and `\X`, with `Reopen` | D1-D3, C3, N1, T1; A9 |
| 5 | the verb taint, and skipped `(*PRUNE)` and `(*SKIP)` | V1-V4, N3, N4 unchanged (C4), Q1, T2 |
| 6 | positive and negative lookarounds, the dedup keyed on captures and fuzzy counts, the lookaround guard rule | L1, L6, L7, C2, C7, N7, F1; **G1 proved red without the guard rule** before the rule lands (section 4.2) |
| 7 | conditions and `NoBranch` | L4, L5, T4-T6, X2 |
| 8 | run-outs inside bodies, unknown captures, the run-out taint, backreferences to unknown captures | N8, Q2, Q3, T3; A10 |
| 9 | the per-attempt fork budget, the lookaround memo, RC5, deleting HitEnd and S57d, the pin moves of section 5 | X1, S4, Z1, Z2; A11-A13; every guard row green without S57d |
| 10 | exact `(?w)` and grapheme M1 through property cores | W1-W6 |
| 11 | the port grid (8.1), the oracle at three seeds and the wave generators, the benchmarks (section 9) | the gate of 8.1; oracle green; no path over 10% slower |

## 12. Amendments

### Amendment 1 (2026-09-29): X2, why the second pass is exponential, and the fix

Chunk 7 left X2 (`(?:(?(?=.*z)a|a))*b` over 'a' * n + 'c') right but exponential in the second
pass. This amendment gives the cause, the options and the fix, measured on a copy of 62c3fe16
(Release, `.scratch/d11-x2/`, two environment switches, the rest of the engine unchanged).

**The cause has two halves, and each alone changes nothing.**

1. *The failure memo is off in the second pass.* `MatchState.KeepsFailureMemo`
   (`MatchState.cs:1846`) answers false whenever `PartialSide != PartialNone`, so
   `END_GREEDY_REPEAT` and `END_LAZY_REPEAT` (`Matcher.cs:10811`, `:11011`) mark every position
   where the body matched as unprotected, upstream's rule, and the failure found there later is
   never recorded. The pattern's half does not turn the memo off for X2:
   `Optimiser.KeepFailureMemosSound` (`Optimiser.cs:601`) clears it for a condition on a group
   (`GroupExists`), not for a lookaround condition, and X2's repeat qualifies
   (`NodeCompiler.cs:1634`: top level, not in a submatch, no maximum, minimum 0). Chunk 7's note
   that a conditional switches the memo off is corrected here.
2. *The condition's restore wipes the outer repeat's guards.* `CONDITIONAL` saves every repeat's
   guard lists (`PushRepeats`, `Matcher.cs:3606`, called at `:10384`), and its backtrack arms put
   them back (`CONDITIONAL` `:14344`, `END_CONDITIONAL` `:14396`, and the negative test's success
   `:10510`). The save exists for guards recorded *inside* the test (the remark at `:3598-3602`),
   but it also throws away what the outer repeat learned *after* the test ended. With both branches
   taken, the timeline at one position p is: the yes-branch runs, and the whole rest of the match
   above it fails, recording guards for the outer repeat at p + 1, p + 2 and so on; backtracking
   pops `END_CONDITIONAL`, whose restore deletes those guards; the no-branch then explores the same
   rest again from nothing. Two full explorations per position is 2^n.

Measured, match, 'a' * n + 'c' (truth None at every n):

| switches | n = 18 | n = 20 | n = 100 | n = 1000 |
|---|---|---|---|---|
| neither (62c3fe16) | 280 ms | 1,185 ms | timeout (30 s) | timeout |
| memo in the second pass only | 361 ms | - | timeout | timeout |
| guards kept across the restore only | 328 ms | - | timeout | timeout |
| both | 0.09 ms | 0.11 ms | 0.87 ms | 17.5 ms |

With both, the second pass is at the pattern's own order on a completed text: the same call without
partial over 'a' * n + 'zcb' takes 11.0 ms at n = 1000 and 83.6 ms at n = 3000, against 17.5 ms and
125 ms (quadratic, because each lookahead scans to the end, as section 9 says). As a search, X2
takes 1,443 ms at n = 1000 against 961 ms for the completed text; the lookaround memo of chunk 9 is
what brings a search down further.

**Is the partial exclusion still needed? No.** It came from the memo design
(`2026-09-26-backtrack-memoisation-design.md`, condition 8, "a partial result is neither success nor
failure") and was kept without a witness (`RepeatInfo.FailureMemo`'s remarks, `PatternObject.cs:76`,
list the four witnessed conditions; partial is not one). The memo needs one fact: entering the body
of repeat R at position p has the same outcome whatever path arrived there. What a second-pass path
carries beyond the position after chunks 1-7, and why none of it breaks that:

- *W-marks.* A mark blocks only constructs older than itself. The continuation of a memo repeat
  holds no cut by a construct older than R: R is not inside an atomic group, possessive repeat,
  lookaround or test (the compiler's half), and the pattern has no `(*PRUNE)` or `(*SKIP)` (the
  pattern's half). Marks newer than R are made inside the subtree. This is section 4.6, step 6,
  which already argues from "a guard is recorded when R's entry is popped", the memo's rule.
- *`AttemptTouchedTheTextToCome`* decides only what `Success` does (`Matcher.cs:13913`). A touched
  success returns a partial, or under `fullmatch` backtracks, the same for every arrival. An
  untouched second-pass success would be a first-pass success (A1), and the memo's patterns are not
  fuzzy, so A1's exception does not arise.
- *Run-outs.* Outside a body a run-out ends the attempt, so no guard is recorded over it. Inside a
  body it goes on as if the body matched, a function of the position; and a memo repeat is never
  inside a body.
- *Unknown captures* matter only to backreferences and group conditions, which switch the memo off.
- *The dedup records and the fork counter* (chunk 9) belong to newer frames or end the attempt with
  a partial; the lookaround memo (chunk 9) replays a function of the body's start state.

A differential grid backs this up: 42,240 partial rows (`(?:X)QY` for 16 bodies X, among them X2's
condition, alternations, lookarounds, atomic groups, `\b` and `\B`; five quantifiers Q; 11 tails Y;
eight subjects over {a, b, c} of length up to 5; match, search and fullmatch) give identical answers
with both switches on and off, 5,260 of them partials.

**Is keeping the guards across the restore sound?** Yes, for memo repeats. A guard at p for such a
repeat says the body entered at p fails for every arrival (above). It was recorded after the test
had ended, so it does not come from the question the test asked, which is all the restore protects.
The rest of the saved state (count, start, capture change) is still restored.

**The restore is exponential without partial too.** `(?:(?(?=a)a|a)|a)*b` over 'a' * n + 'cb' (the
'b' defeats the required-string screen): the port takes 1,910 ms at n = 22 and 8,388 ms at n = 24;
with the guards kept, 0.07 ms. Upstream 2026.9.10 takes 1,378 ms at n = 22 (measured 2026-09-29).
So the kept guards apply to every call, not only the second pass. A lookaround does not save the
repeats, and `(?:(?=a)a|a)*b` and `(?:(?>a)|a)*b` are already linear; the other callers of
`PushRepeats` (group call, group condition) are in patterns with no memo.

**Plain partial `(?:a|a)*b` is exponential in the port as well**, with no condition: over
'a' * n + 'c', 234 ms at n = 20 and 880 ms at n = 22 (second pass only; the first pass has the
memo), and upstream 97.5 ms and 396 ms. The memo half alone fixes it: 0.32 ms at n = 22 and 122 ms
at n = 100,000. This goes beyond upstream, which has no memo.

**The options.**

| option | soundness | cost on X2 | partial-off cost | verdict |
|---|---|---|---|---|
| A. The memo in the second pass, and memo repeats' guards kept across a condition's restore | the arguments above; 0 differences in 42,240 rows | 0.11 ms at n = 20, 17.5 ms at n = 1000 | nothing added: one test leaves `KeepsFailureMemo`, and `CONDITIONAL` saves and restores fewer bytes, since the memo repeats' lists are skipped | **recommended** |
| B. A per-attempt visited set of (node, position, relevant state) | needs its own argument for every node kind, and the relevant state (marks, touch, dedup, counts) is what A shows the memo repeats never read | a hash insert per node visit in the second pass, and memory of nodes times positions; polynomial, but slower than A wherever A applies | none if second-pass only | not needed now; the upgrade path if a pattern outside the memo's conditions proves exponential |
| C. Bound the second pass: the fork budget (M4) alone | sound (a partial is never a refusal) | X2 counts a fork per condition, so it stops at 256 (\|t\| + 1) forks and answers P(0, \|t\|), a phantom where the truth is None | none | a backstop only; it does not bound plain `(?:a\|a)*b`, which forks nowhere |
| D. Either half of A alone (chunk 7 tried keeping the guards) | sound | still exponential (table above) | - | rejected |

**Recommendation: A.** It is two small changes, each sound on its own, and together they fix X2,
plain partial repeats and a non-partial exponential that upstream shares.

- *Guards kept across a condition's restore:* its own defect row, since it is not a partial
  problem, fixed on main test-first and merged into D11. Witness: `(?:(?(?=a)a|a)|a)*b` over
  'a' * 40 + 'cb' within a step bound (`EngineWork.ShouldTakeAtMostSteps`, as in
  `FailureMemoTests`), red on main. The build measures a condition-heavy benchmark against main.
- *The memo in the second pass:* D11 chunk 9, with the fork budget, because the budget would
  otherwise turn X2 into a phantom and the two are measured together. X2 moves from chunk 7's
  "done when" to chunk 9's. Witnesses: X2 over 'a' * 40 + 'c' within a step bound, and the four
  `FailureMemoTests` patterns with `partial: true`, each red with the `PartialSide` test put back.
  Rule 11 needs a Debug count of guards consulted in second passes, non-zero on the grid.

**Left open.** A repeat outside the memo's conditions (a backreference, a group condition, a verb or
fuzzy matching anywhere in the pattern) keeps upstream's guards, so an X2-shaped body there stays
exponential in the second pass, and the fork budget answers it with a C5 phantom; option B is the
upgrade path. Found on the way, and not caused by this: `(x)?(?(?=.*z)a|a)b(?(1)y)` and
`(x)?(?(?=.*z)a|a)b\1` over 'ac', match, answer P(0, 2) in the port and in upstream, where the judge
says None; without the backreference or group condition they answer None. It needs triage, likely
with chunk 8's unknown captures.

### Amendment 2 (2026-09-29): what a blocked end leaves on the saved stack

Chunks 6 and 7 let a positive lookaround, or a condition's positive test, go on after a blocked end
with the body's entries kept (`EndALookaroundInTheSecondPass`, `Matcher.cs:4326-4339`). That path
leaves the saved stack (`Sstack`) as the body left it, and the constructs after the lookaround read
the wrong entries. Two repair rounds went to the symptom in called groups; this amendment derives
the rule from the normal ends instead. Measured on a copy of 9ee671b6 with the fix prototyped behind
environment switches (`.scratch/d11-call/`, Release and Debug), 2026-09-29.

**What a normal end leaves.** `LOOKAROUND` and `CONDITIONAL` push their parked state (node, slice
start, slice end, text position: four sizes, 32 bytes) onto `Sstack` and record the new count in
their frame (`Matcher.cs:10382-10393`). Call the count before that push S0. A normal positive end
(`:11228-11292`, `:10441-10502`) cuts the backtracking stack to the frame, sets `Sstack.Count` back
to the frame's count, closes the calls and fuzzy sections above it (`CloseFramesAbove`, `:3989`),
and pops the parked state. So the continuation always starts with `Sstack` at S0, holding exactly
what it held before the construct began, with no call or section opened in the body still open.
Everything the continuation pops from `Sstack` (a capture group's parked start, a called group's
return frame at `GROUP_RETURN`, `:11966`, an enclosing section's frame at `END_FUZZY`, `:10698`)
was pushed below S0.

**What a blocked end leaves today.** The same backtracking stack plus a `REOPEN` entry, which is
right: the body's other paths must stay. But `Sstack` is left at S0 + 32 or more: the parked state
is still on top, and after a run-out in the body (`RunOutInALookaroundBody`) so are the entries of
whatever the body had open. The continuation's first pop below S0 reads the parked state instead.
A short story, `(?(DEFINE)(?<c>(?=a$)))(?&c)a\n` over 'a': the call pushes its return frame, group
c parks its start, the lookahead parks its state; `$` reads the edge, so the end is blocked and
matching goes on. `END_GROUP` for c pops the lookahead's text position as c's start; `GROUP_RETURN`
pops the parked slice end as its return node, and `PopOpenCall` then indexes an empty list. So the
crash is a symptom, and calls are only where it is loudest. Without a call the same misread is
silent:

- `(x(?=a$))a\n` over 'xa', match: P(0, 2) with group 1 at (1, 1); the completion 'xa\n' gives (0, 1).
- `(a(?=b(cd)))` over 'ab' (a run-out in the body): group 1 at (2, 1), an end before its start.
- `(?:a(?=b\b)b){1<=e<=1}\n` over 'ab': match gives P(0, 1), a partial that stops short of the
  text's end; search gives P(2, 2) where the judge answers P(1, 2), so a start is denied (a miss).

**The rule.** A blocked positive end must leave the continuation exactly what a normal end leaves
(S0, and the calls and section link of that depth), and `REOPEN` must hand the body back exactly
what it had. The backtracking stack is where the engine keeps what it must undo, so the bytes move
there:

1. At the blocked end (`EndALookaroundInTheSecondPass`, after the dedup record is added): read the
   frame's saved count (`ReadSizeBelow(mark - 1)`, as `ReadPruningMark` does, `:4532`); S0 is that
   count less 32. Move the `Sstack` bytes from S0 to the top onto the backtracking stack, then push
   their length and `SectionFrame`, then the existing mark and `REOPEN`
   (`bstack: sstack_bytes length section_frame pstack_mark REOPEN`). Then call `CloseFramesAbove`,
   as a normal end does. Add a `ByteStack` method that moves the top n bytes of one stack onto
   another (beside `PushBlock`, `ByteStack.cs:126`), so the move allocates nothing.
2. In the `REOPEN` arm (`:15616`), for a lookaround or condition frame (the opcode below the mark,
   which the arm already reads): pop `SectionFrame`, the length and the bytes, and push the bytes
   back onto `Sstack`. An atomic frame's `REOPEN` is unchanged (`BlockAtomicCut`, `:4068`).
3. Nothing else changes. The negative and dedup arms of the same method backtrack into the body, or
   cut and fail, and never run a continuation; the frame's own backtrack arms (`:14328`, `:15394`)
   already restore `Sstack`.

**The options, measured.** Rows are partial `match` unless noted; the judge is
`tools/probes/d11-brute-judge.py`. A miss is a None (or an exception) where the judge finds a
completion; a phantom is a partial where it finds none, which the design allows (section 1).

| option | 2,400-row call grid: exceptions / misses | call-free captures (K1-K3), F1 | cost |
|---|---|---|---|
| today (9ee671b6) | 204 / 84 | wrong | - |
| A. Unwind as a normal end, park the bytes in `REOPEN` (the rule above) | 0 / 0 | right; F1 sound | a 32-byte move per blocked positive end, second pass only |
| B. Answer `Cut` for a pattern with group calls, as `RunOutInALookaroundBody` does (`:4386`) | 0 / 0 while S57d's escalation masks them; **19 misses** once chunk 9 deletes it | still wrong: B changes nothing without a call | none |
| A without the `REOPEN` restore (step 2) | 46 exceptions | - | - |

The grid is `tools/probes/d11-blocked-end-grid.py`: five tests (lookahead, condition, their
negatives, lookbehind) over six bodies and six tails, each inline, after a prefix, in a called
group, in a nested call, in a numbered call and under `(?R)`. B's misses are D11's own fixes undone
in called groups: `(?(DEFINE)(?<c>(?!a\b)))(?&c)a` over 'a' is P(0, 1) (the completion 'a:'), and B
answers None because a blocked negative end cuts again. B would also leave a called group answering
differently from its inlined copy for a reason no rule states. Further grids, all 0 exceptions and 0
misses under A: 1,280 rows of atomic groups and possessive repeats (in calls, `(?1)`, `(?R)`,
lookarounds and conditions; today 125 exceptions), and 1,482 fuzzy rows with minimum error counts
inside and around the lookaround (the restore is needed there too: without step 2,
`a(?=(?:bcd){1<=e<=1}|b)x` over 'ab' throws).

A changes no answer that is not a crash or a misread, with two exceptions, both sound:

- 8 of the 1,482 fuzzy rows change, all where the body ran out inside a fuzzy section with a
  minimum: 7 turn from None (the judge's answer) to a phantom partial and one phantom changes its
  span. For example `a(?=(?:bcd){1<=e<=1})(?:b|c){1<=e<=1}x` over 'aby'
  (P(0, 3), judge None). Today's None there comes from the run-out's residue: the lookaround's
  unfinished section stays `FuzzyNode`, and its minimum goes on constraining the continuation
  through a section link that A correctly steps out of. Chunk 8 owns that residue (run-outs inside
  bodies); a SHORTCUT comment at the move names it.
- Rows where S57d's HitEnd escalation answers a phantom (C4 and X1 below) keep it until chunk 9
  deletes the escalation, as section 11.1 plans.

**Recommendation: A.** It is the only option that fixes the root cause, it fixes the call-free
misreads that B leaves, it keeps D11's answers in called groups, and it costs nothing outside a
blocked end. `RunOutInALookaroundBody`'s group-call exclusion stays: it is what keeps a call opened
inside a body from being open at the end (assert 3). Lifting it needs `REOPEN` to re-open those
calls as well; that is chunk 8's, and on this grid it would remove only 6 of the 30 run-out phantoms
in called groups.

**Asserts** (rule 10; all held on the grids above in a Debug build of the prototype, whose own
asserts were shown to fire):

1. At a blocked positive end, `Sstack.Count` is at least the frame's saved count, and the parked
   state read there is the frame's (`look`).
2. After the move, `Sstack.Count` equals the frame's saved count less 32: the depth a normal end
   leaves.
3. After `CloseFramesAbove`, no call opened inside the body is open (`OpenCalls` is empty or its top
   depth is at most S0). It holds because a body that reaches its end has returned from its calls,
   and a run-out, which has not, is refused for patterns with calls (`:4386`).
4. In `REOPEN` for a lookaround or condition frame, `Sstack.Count` equals S0 before the bytes go
   back (the continuation has been undone in full), and S0 plus the length after.
5. At a blocked `END_ATOMIC` (`:10336-10346`), `Sstack.Count` equals the frame's saved count: an
   atomic group parks nothing and its body is complete. 441 blocked atomic ends on the atomic grid.

Rule 11 counters (`PartialEvent`, `WMark.cs:66`): blocked ends that moved bytes, `REOPEN`s that
restored them, and moves longer than 32 bytes (a run-out with open body entries: one row in the
3,146-row Debug set, K3, and 68 on the 450-row fuzzy grid, so the build's grid adds run-out rows).

**Witnesses** (rule 12; `tools/probes/d11-blocked-end-sstack-rows.jsonl`, judged by
`tools/probes/d11-judge-rows.py`, which also prints group 1 of the judge's completion). "Today" is
9ee671b6 in Release. Each test is red today, or red with step 2 removed where marked; controls are
green today and guard the paths that do not misread.

| id | pattern | subject | judge | today | A |
|---|---|---|---|---|---|
| C1 | `(?(DEFINE)(?<c>(?(?=a$)a\|a)))(?&c)\n`, match and search | a | P(0,1), g1 (0,1) | IndexOutOfRange | P(0,1), g1 (0,1) |
| C2 | `(?(DEFINE)(?<c>(?=a$)))(?&c)a\n`, match and search | a | P(0,1), g1 (0,0) | ArgumentOutOfRange | P(0,1), g1 (0,0) |
| C3 | `(?(DEFINE)(?<c>(?=a$)))(?&c)b`, match | a | None | ArgumentOutOfRange | None |
| C3s | the same, search | a | None | ArgumentOutOfRange | P(1,1): the edge attempt of section 1, rule 3, as `(?=a$)b` and upstream answer |
| C4 | `(?(DEFINE)(?<c>(?=a\b\|a)))(?&c)(b)` | a | None | IndexOutOfRange | P(0,1) until chunk 9 (S57d); None without it |
| C5 | `(?(DEFINE)(?<c>(?!a\b)))(?&c)a` | a | P(0,1) | P(0,1) | P(0,1); control, B's miss |
| C6 | `(?(DEFINE)(?<c>(?!a$)))(?&c)a\n` | a | P(0,1) | P(0,1) | P(0,1); control, B's miss |
| C7 | `(?(DEFINE)(?<c>(?(?!a$)b\|a)))(?&c)\n` | a | P(0,1), g1 (0,1) | P(0,1) | P(0,1); control |
| C8 | `(?(DEFINE)(?<c>(?<=a\b)))a(?&c)\n` | a | P(0,1), g1 (1,1) | IndexOutOfRange | P(0,1), g1 (1,1) |
| C9 | `(?(DEFINE)(?<c>(?>a\b\|)))(?&c)\n` | a | P(0,1), g1 (0,1) | P(0,1) | P(0,1); control (atomic, assert 5) |
| C10 | `(?(DEFINE)(?<c>(?=(?>a$\|a))))(?&c)(a)\n`, match and search | a | P(0,1), g1 (0,0) | IndexOutOfRange | P(0,1), g1 (0,0) |
| N1 | `(?(DEFINE)(?<c>(?=a$))(?<d>(?&c)))(?&d)a\n` | a | P(0,1), g1 (0,0) | IndexOutOfRange | P(0,1), g1 (0,0) |
| N2 | `(?(DEFINE)(?<c>a)(?<d>(?(?=(?&c)\b)(?&c)\|b)))(?&d)\n` | a | P(0,1), g1 (0,1),(0,1) | P(0,1) | P(0,1); control (a call inside the test) |
| N3 | `(?(DEFINE)(?<c>(?=a\b))(?<d>x(?&c)))(?&d)a\n` | xa | P(0,2), g1 (1,1) | ArgumentOutOfRange | P(0,2), g1 (1,1) |
| R1 | `(?:x(?R)\|(?=a\b)a)\n` | xa | P(0,2) | P(0,2) | P(0,2); control |
| R2 | `(?:x(?R)\|(?(?=a\b)a\|b))\n` | xa | P(0,2) | P(0,2) | P(0,2); control |
| R3 | `(?:x(?R)\|(?=a\b)a)\n`, match and search | xxa | P(0,3) | P(0,3) | P(0,3); control |
| R4 | `(?:x(?R)\|(?(?=(?>a$\|a))a\|b))(a)\n` | xa | P(0,2) | P(0,2) | P(0,2); control |
| G1 | `(?:((?=a\b)a)\|x(?1))\n` | xa | P(0,2), g1 (1,2) | IndexOutOfRange | P(0,2), g1 (1,2) |
| G2 | `(?:((?(?=a\b)a\|b))\|x(?1))\n` | xa | P(0,2), g1 (1,2) | ArgumentOutOfRange | P(0,2), g1 (1,2) |
| G3 | `(x(?1)?(?=a\b)a)\n` | xa | P(0,2), g1 (0,2) | ArgumentOutOfRange | P(0,2), g1 (0,2) |
| K1 | `(x(?=a$))a\n` | xa | P(0,2), g1 (0,1) | g1 (1,1) | g1 (0,1) |
| K2 | `(x(?(?=a$)a\|b))\n` | xa | P(0,2), g1 (0,2) | g1 (1,2) | g1 (0,2) |
| K3 | `(a(?=b(cd)))` | ab | P(0,2), g1 (0,1) | g1 (2,1) | g1 (0,1) (upstream reports no group) |
| F1 | `(?:a(?=b\b)b){1<=e<=1}\n`, match | ab | None | P(0,1), short of the edge | P(0,2), a sound phantom |
| F1s | the same, search | ab | P(1,2) | P(2,2), a miss | P(0,2) |
| X1 | `(?(?=(?:a\|ab)\b)a\|b)(a)(\n)` | ab | None | P(0,2) (S57d) | P(0,2) until chunk 9; NullReference without step 2 |
| X2 | `a(?=(?:bcd){1<=e<=1}\|b)x` | ab | None | None | None; InvalidOperation without step 2 |

32 rows. Each test asserts the span, and group 1 where the table gives it. The build also runs this
amendment's grid on a Debug build and requires 0 exceptions and 0 misses, and for called rows the
same answer as the inlined twin outside the 30 run-out rows named above.

**Performance.** Only a blocked positive end and its `REOPEN` do more: one move of 32 bytes in the
usual case, and two sizes, both behind the `state.WMarks.Count != 0` test that already guards the
second-pass path (`:10425`, `:11212`). A call without partial, and a first pass, pay nothing.

### Amendment 3 (2026-09-30): chunk 8's review, round 1

Four rules changed, each found by a row and each with a witness that fails without it (Release,
2026-09-30). Rows marked "port" are judged by the port's own non-partial matcher over every
continuation of up to three characters, where the port's verb or fuzzy rules differ from upstream's
(ledger entries 45, 47 and 51); the rest by `tools/probes/d11-brute-judge.py`.

1. **Which attempts count as touched (A1, section 4.3).** The two passes run the same code only
   from the same start. The next start is the ordinary bump-along's unless an attempt can move it:
   a `(*SKIP)` moves the slice, and a `\K` whose entry a cut discarded leaves the match position
   set (FAILURE's bump-along starts from it). So in a pattern holding either, an attempt that
   follows a touched attempt counts as touched too (`PatternObject.AnAttemptCanMoveTheNextStart`).
   A `(*PRUNE)` ends the attempt with the ordinary bump-along and needs nothing. Before, the flag
   was cleared at every start, and `(?=ab(*SKIP)c|a)a(*SKIP)x|b` search 'ab' answered the complete
   match (1, 2), where the port's non-partial search is None and 'ab' + 'c' completes it (port
   P(1, 2)); `ab\Z(*SKIP)(*F)|b` search 'ab' did the same (judge P(1, 2)). Witness
   `PartialVerbRuleTests.A_start_a_touched_attempt_chose_is_touched_too`.
2. **Which captures are unknown (section 4.2, the run-out row, part 1).** Only the groups the
   innermost body can still reach from the item that ran out: a group open at the edge, one a
   repeat can enter again, or one not reached yet (`Node.RunOutGroups`, computed at compile time
   for patterns that read captures). A group the body closed before the item and cannot reach
   again holds the same span in every continuation. `(?=a(.)b)\1` over 'aa' reported group 1 unset
   where every completion gives (1, 2). A group whose current capture is unknown is reported with
   no captures at all, so `Success` and `Captures` agree. Witnesses in `PartialUnknownCaptureTests`.
3. **Where the verb part of the run-out taint goes (section 4.2, the run-out row, part 2).** The
   design put a verb taint on the lookaround's own frame and on each atomic group open in the body.
   But a positive lookaround and an atomic group are transparent to verbs (ledger entry 47): a verb
   after the edge ends the scope `FindTheVerbTarget` finds from the run-out, which lies outside
   them. The taint now goes on that frame alone. `(?!(?=a.(*PRUNE)b)x|.)a` over 'a' was refused,
   where 'a' + 'xy' completes it (port P(0, 1)). No verb can have a positive lookaround or an atomic
   group as its scope, so their taints are dropped; the oracle's ablation
   `VerbsAreConfinedToTheInnermostGroup`, under which every group is a scope, keeps the atomic ones.
   Q3 was no witness for this part: its branches share their `a`, so the run-out is the branch's
   own test and the second branch is never reached. The witness is `(?!a.(*PRUNE)b|.)a` over 'a'
   (`PartialLookaroundRuleTests.A_run_out_taints_the_scope_of_a_verb_after_the_edge`). On a
   1,120-row grid of verbs after the edge (port-judged): 12 misses and 36 late starts fixed, 12 new
   phantoms of class C3.
4. **A run-out closes the fuzzy section its body left open.** Amendment 2's residue: the body's
   unfinished section stayed current for the continuation, with its own limits and without the
   enclosing section's counts. `(?:a(?=(?:bcd){e<=2})bx){e<=1}` over 'aby' answered P(0, 3) with two
   errors under a limit of one (judge None). Now the enclosing section (`Node.EnclosingSection`)
   becomes current, the body's errors are added to its counts, a path already over a limit fails,
   and a backtracking entry gives the body its section back. SHORTCUT: the counts added are the
   section's at the lookaround's entry and the innermost open section's own, a lower bound when
   sections nest in the body. On a 1,080-row fuzzy grid (port-judged): 40 phantoms fixed, 0 misses.

Amendment 2's eight parked fuzzy rows and its review's row, re-judged: four are C3 (fuzzy-free twin
`a(?=byd)byx` over 'aby', the same P(0, 3) with the same events), the `(*PRUNE)` row is C4 (twin
`(?:(?=aa$|a)|a)a(*PRUNE)b` over 'aa'), all five now in D18's test; one search is C2 (D19's test);
one match is fixed by rule 4; the two `(?:bcd|b)` rows are right on the port's own matcher (the
judge's None is upstream's minimum rule, ledger entry 51).

Left open: a repeat inside a body whose try-match test runs out at the edge still ends the whole
match as a partial, which chunk 6 changed for every other arm (register row D11-R1:
`(?=(?:(a))*b)[xy]` over 'aa', P(0, 2) vs None). The group-call exclusion stays, as amendment 2
decided.
