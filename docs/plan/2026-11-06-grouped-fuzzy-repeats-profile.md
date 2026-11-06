# Grouped fuzzy repeats: profile and a proposed screen (2026-11-06)

Point-in-time record for phase 11 item 8. The checklist lives in `STATE.md`. Profiled and
measured; nothing built.

## The workload

`(?:(\w+) jumps over){e<=2}` and its relatives: a fuzzy section that opens with a repeat (here a
group around `\w+`), so the fuzzy literal filter (`FuzzyLiteralFilter`, which needs a section made
of literals only) does not apply and no other prefilter does either. Harness
`.scratch/p11-item8/grouped.cs` on the integration branch (2c45e127, Release), median of 15 batches;
every answer checked against Python `regex` 2026.9.29 first (all six agree):

| Row | Subject | ns per call | B per call | Answer |
|---|---|---|---|---|
| `(?:(\w+) jumps over){e<=2}` | `the quick brown fox jumps over the lazy dog` | 37,694 | 208 | (4,30), 2 substitutions |
| same | `the quick brown fox jumsp ovr the lazy dog` | 312,380 | 120 | no match |
| `(?:(\w+) jumps over){e<=1}` | the first subject | 25,726 | 200 | (10,30) |
| `(?:\w+ jumps over){e<=2}` (no group) | the first subject | 26,431 | 160 | (4,30) |
| `(?:(\w+) jumps (\w+)){e<=2}` | the first subject | 37,908 | 224 | (4,30) |
| `(?:(\w+?) jumps over){e<=2}` | the first subject | 40,415 | 208 | (4,30) |

The near miss costs 312 us for a 42-character subject: about 7.4 us per start position. The group
adds about 43% over the same section without it (37.7 against 26.4 us).

## Where the time goes

WPR CPU traces (the owner's scheduled tasks, 8 s per row, `.scratch/wpr/etl-top.cs`), exclusive
samples. The two rows have the same shape:

| Frame | near miss (11,328 samples) | hit (23,180) |
|---|---|---|
| `Matcher.BasicMatchCore` (the dispatch loop) | 27.0% | 23.9% |
| `Matcher.AdvanceEndGreedyRepeat` | 11.9% (20.3% inclusive) | 14.7% |
| `Matcher.RetryFuzzyMatchString` | 9.4% | 7.8% |
| `Matcher.FuzzyMatchString` | 6.6% | 7.3% |
| `Matcher.FuzzyIterationStands` | 5.9% | 7.2% |
| `GuardList.Guard` | 4.2% | 4.6% |
| `Matcher.DeletionRoom` | 2.3% | 3.2% |
| backtracking pops (`PopMatchBodyTailStateData`, `BacktrackTailStart`, `BacktrackMatchTail`, `BacktrackBodyStart`) | 9.7% | 11.0% |

No single hot spot: the time is the search itself, a fuzzy greedy repeat explored with two errors
to spend at every start position. The repeat memo's `HashSet` (item 12, about 3% in the
2026-11-04 profile) does not appear in either trace. Micro-optimising these frames could win
perhaps 10-20%; skipping start positions that cannot match wins in proportion to how many there
are, which on a no-match subject is all of them.

## Proposal: a required-literal screen

Most fuzzy patterns of this kind contain a literal that every match must pass through (here
` jumps over`, 11 characters). The fuzzy literal filter's argument applies to that literal alone.
The text any match aligns with the literal lies inside the match and is within `k` edits of it,
`k` being the most errors the section allows (`FuzzyLiteralFilter.MostErrors`), since every edit
there is one of the section's errors. So:

- A slice holding no stretch within `k` edits of the literal holds no match: the search can answer
  no match after one pass of the filter's bit-parallel scan (`LiteralScan`, Myers' algorithm, C3),
  with no attempt.
- A forward match starting at `p` holds such a stretch starting at or after `p`, so no match starts
  after the last such stretch's start: the search can stop there.
- The `k + 1` pieces (one edit damages at most one, Navarro 2001, section 8.1) are only a cheaper
  first test: a slice with no untouched piece has no stretch either. They do not decide the
  near-miss row, checked with Python `regex` 2026.9.29: the cut is `" ju"`, `"mps "`, `"over"`, and
  `" ju"` occurs in `... jumsp ovr ...`. The scan decides it: `(?: jumps over){e<=2}` finds no
  occurrence in that subject, and finds `ox jumps over` (17,30) in the hit subject.

Unlike the existing filter it gives no lower bound on the start, because the part before the
literal (`\w+`) can be any length; it only rejects and cuts the tail. The existing filter's other
rules carry over unchanged: ASCII literal and ASCII-checked subject stretches (the case-insensitive
pairs KELVIN SIGN, LONG S, `ss`/`ß`, `fi`/U+FB01), no partial search (a partial match can stop
before the literal), `k < m`.

Shape for a first version, so the argument stays the whole argument:

1. The pattern's start node is the `FUZZY` node and its `END_FUZZY` leads to `SUCCESS` (as the
   existing filter requires), so every match is one run of the section.
2. The literal is a `STRING` node (or a chain of one-character literal nodes) that every path from
   `FUZZY` to `END_FUZZY` passes through: not inside a branch, an optional item, a repeat with a
   minimum of 0, a lookaround or a condition. A dominator walk over the section's nodes decides it.
3. No nested fuzzy section, no group call, no backreference inside the section (each can make the
   text aligned with the literal depend on something other than the literal).
4. Forward first; reversed patterns mirror the bound (no match ends before the first piece
   occurrence's end) and can follow once the forward screen is proved.

## How it would be proved and measured

- **Tests first:** the rows above as gap tests against Python's answers; a differential that runs
  every pattern of the oracle's fuzzy generators with the screen on and off over the same subjects
  and requires identical results (the screen only skips, so any difference is a bug); shape tests
  that each rule above refuses its counter-example (a literal inside a branch, under `?`, in a
  lookahead, after a backreference).
- **Gain:** the harness rows above plus a records workload (`(?i)(?:(\w+) lantern works){e<=2}` over
  the usage corpus's 100,000 records), A-B-B-A with an A-A control; WPR before and after.
- **Cost on patterns it does not apply to:** one shape check at compile time; zero at match time.
  On patterns it applies to whose subjects do hold a match, one scan of the slice per search
  (Myers' algorithm: one word operation per character for a literal up to 64): measure on the
  hit row, where it must
  not lose.

## Recommendation

Build the screen, test-first, as the next phase 11 engine item. It turns the no-match case of a
whole class of fuzzy patterns (a word or group before a fixed phrase, the shape of most "find this
phrase with a name in front" searches) from O(n) attempts, each a fuzzy backtracking search, into
one bit-parallel scan of the subject. Leave the frame-level work (`AdvanceEndGreedyRepeat`,
`FuzzyIterationStands`) until the screen's after-profile shows what is left.
