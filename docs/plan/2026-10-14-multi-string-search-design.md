# C6: multi-string search for literal alternations (design, 2026-10-14)

Stage C6 of the 2026-10-13 optimisation brief: give a pattern that reaches the search loop with no
screen a `SearchValues<string>` screen when a set of literals is known. A **screen** (or prefilter)
is a vectorised search that skips start positions where no match can begin; it may only skip, never
choose the branch. .NET's `SearchValues<string>` already picks Teddy, Aho-Corasick or Rabin-Karp per
set, so nothing here hand-rolls a string matcher.

Evidence: probe `.scratch/c6/probe/Program.cs`, Release, run under the test lock on 2026-10-14,
outputs `.scratch/c6/run1.txt` (shapes, semantics), `run-c.txt` (timings, `DOTNET_TieredCompilation=0`,
pinned with `/affinity FFF8`, min of 5) and `run-d.txt` (node dumps). The corpus is 1 MB of
word-frequency-shaped text: frequent short function words, a random-letter tail, `cat`/`dog` about
once per 2,000 words, an address-like token every 500 words. Timings are single-process probes on a
shared machine, good to a factor, not to the noise floor.

## 1. What is covered today

| Screen | Built | Consulted | Conditions |
|---|---|---|---|
| `LeadingStrings` (`SearchValues<string>`, ordinal) | `PatternObject.FindLeadingStrings` `src/FuzzyRegex/Engine/PatternObject.cs:1378-1462`, called `:1327` | `Matcher.BasicMatch` `src/FuzzyRegex/Engine/Matcher.cs:12797-12799`, sweep `:12974-12982` | Not fuzzy, not reversed (`:1380`); search only, no partial match, no `(*SKIP)` (`Matcher.cs:12797`). Every path from the start node, looking through capture groups, nested branches and the zero-width first-set check (`SetUnion` etc. with `Step == 0`, `:1436-1446`), must reach a case-sensitive forward `String`/`Character` with no lone surrogate (`:1447-1456`). At least one branch; at most 1,000 branch nodes (`:1466`) |
| `LeadingUnits` (`SearchValues<char>`) | same walk, `:1414-1417` | `Matcher.cs:12984-12994`, `NextLeadingUnit` `:12650` | As above, but some head is `StringIgn`/`StringFld`/`CharacterIgn` (not LOCALE); first UTF-16 unit of every character that can start a head, fold-closed |
| Required string (`ReqString`, one literal) | upstream `get_required_string` | `LocateRequiredString` `Matcher.cs:11881`; fold screen `RequiredStringScreen` | One literal at a fixed offset (jump) or anywhere ahead (`ReqOffset == -1`, refuse-only) |
| Upstream first-set + `search_start` | `ParseFunctions.CheckFirstset` `src/FuzzyRegex/Parsing/ParseFunctions.cs:3144` | `Matcher.SearchStart` `:12249` | A one-character class scan in front of a branch; none under IGNORECASE+FULLCASE (`upstream/regex/_regex_core.py:380-405`) |
| `BranchChain` | `src/FuzzyRegex/Engine/BranchChain.cs` | BRANCH arm | Not a screen: once an attempt enters a run of 8+ keyed alternatives it jumps to those whose first units fit (SYNC-DIVERGENCE row 69) |
| `FuzzyLiteralFilter` | `src/FuzzyRegex/Engine/FuzzyLiteralFilter.cs` | `Matcher.cs:12934` | One fuzzy ASCII literal only |

Measured coverage (`run1.txt`, column `strings=`): `cat|dog`, `(cat|dog)`, `(?:cats?|dogs?)`,
`(?:cat|dog|bird)s`, `\L<ten>`, `\L<pref>` (prefix-factored list) all get Teddy. So do
`(?:cat|dog)+` and `(?:cat|dog){2}`: the compiler copies a repeat's first iteration in front, so the
walk sees a branch. `cat|car` is factored to `ca[tr]` and gets the required string `ca` instead,
which is as good.

## 2. What is left

Shapes that reach the search loop with a known literal set but no string screen (`run1.txt`), with
probe cost against the covered `(?:cat|dog)` at **79 us** per MB (`run-c.txt`):

| # | Shape | Today | Probe cost | How common | Upstream screen |
|---|---|---|---|---|---|
| L1 | Zero-width node in front: `\b(?:cat|dog)\b`, `\b\L<list>\b`, `(?<![a-z])(?:..)`, `^(?:..)` multiline | first-set char scan only | `\b(?:cat|dog)` 7.1 ms (90x); `\b\L<ten>\b` 28.0 ms against `\L<ten>` 0.17 ms (165x) | Very common: whole-word keyword search is the usual way to write a keyword list | First-set char scan (`search_start` on the set), same as ours |
| L2 | Case-insensitive alternation: `(?i)cat|dog`, `(?i)\L<list>` | **none at all** (see note below) | `(?:cat|dog)` IgnoreCase 8.2 ms (104x) | Common: keyword lists are often case-blind | None: `_add_precheck` gives up on any ignore-case literal |
| L3 | Optional or one-of prefix: `(?:the )?(?:cat|dog)`, `[ab](?:cat|dog)` | first-set only | not timed; same mechanism as L1 | Moderate | First-set only |
| L4 | Variable-width run, then a required literal set: `\w+@(?:gmail|yahoo)\.com`, `\d+ ?(?:kg|lb)` | one-character required string (`@`, ` `) at offset -1 | `\w+@(?:gmail|yahoo)\.com`, no match, 160 ms | Common in extraction and validation patterns | One-character required string, same as ours |
| L5 | A branch with an empty or non-literal arm: `cat|dog|`, `(?:cat|dog)x|[0-9]` | none | not timed | Rare; no literal set exists | none |
| L6 | Very large list (over ~1,000 members) | refused by `_maxLeadingBranches`; `\L<big>` (2,000) happened to share prefix `w00` | n/a | Rare but real (dictionaries) | none |
| L7 | Fuzzy, reversed, partial, `(*SKIP)` | refused by design | n/a | Fuzzy alternation is this library's main use (FuzzyPhrase rows) | none |

**Note on L2.** `LeadingUnits` was meant to cover case-insensitive alternations (2026-10-11), and
`LeadingUnitsTests` pins it for patterns with inline flags such as `(?fi)straße|café`. Yet
`cat|dog` under `FuzzyRegexOptions.IgnoreCase` gets neither screen (`run1.txt` lines 16-17, 22),
and the node dump (`run-d.txt`) shows why. The compiler puts a zero-width first-set check in front
of the branch, and for these literals it is `SetUnionIgn` (step 0), not `SetUnion`. The walk's
look-through list (`PatternObject.cs:1436-1446`) names only the case-sensitive set opcodes, so it
refuses at the first node. `straße|café` gets `LeadingUnits` only because `ß` and `é` stop the
compiler building a first-set check at all. This is the same for `(?i)` inline and for
`IgnoreCase` with or without `FullCase`. A coverage gap in an existing screen, cheaper to close than
any new option, and the probe numbers above are its cost.

L7 stays refused: an error can delete or change the literal, so no exact literal screen is sound.
`FuzzyLiteralFilter` (Navarro partitioning, C3 Myers stage) is the fuzzy route; extending it to
alternations is a separate stage, not C6.

## 3. Options

All options are reject-only: the engine still starts each attempt and picks the branch. All inherit
item 14's guards: not fuzzy, not reversed, no partial match, no `(*SKIP)` for a jump. A screen hit
is a candidate; a candidate the engine rejects costs one call plus one attempt.

**Per-call cost, measured.** One `IndexOfAny(SearchValues<string>)` per hit on dense hits: `th|he|an`,
44,870 hits in 0.85 ms, about 19 ns a hit; `the|and` 12,450 hits, 0.34 ms, 28 ns. Sparse `cat|dog`:
0.07 ms per MB, about 15 characters per ns. The worst subject, `"th"` repeated with screen `th|ht`, one hit at every
position: 1,048,575 calls in 11.95 ms, about 11 ns a call. So the most a string screen can add is
about 11-28 ns per attempt, against an engine attempt that costs more (next paragraph).

**Option A. Close the L2 gap.** Add the zero-width ignore-case set opcodes (`SetUnionIgn`,
`SetDiffIgn`, `SetInterIgn`, `SetSymDiffIgn`, `RangeIgn`, `CharacterIgn`, each with `Step == 0`) to
the walk's look-through list, so `LeadingUnits` fires. The check reads nothing, so skipping it is as
sound as skipping `SetUnion`. Worst case: what `LeadingUnits` already has, one sweep per run of
hits (`NextLeadingUnit`), measured adversarial cost in SYNC-DIVERGENCE row 68. Refuses: LOCALE,
reverse, fuzzy.

**Option B. Case-insensitive strings via `StringComparison.OrdinalIgnoreCase`.** Stronger than units
(`cat|dog` raw: strings-OIC 0.085 ms against units 0.64 ms). But .NET's ignore-case rule is not
mrab's: measured, OIC misses `k` against U+212A KELVIN SIGN and `s` against U+017F LONG S, both of
which mrab's IGNORECASE matches, and misses `ss`/`ß` under FULLCASE (`run1.txt`, B lines). A screen
that misses a real match changes the answer, so B may only be built when every head character's
mrab case closure equals its OIC closure: ASCII letters other than `k` and `s`, digits and
punctuation, no FULLCASE expansion (no `ss`, `st`, `fi` and the like). Else fall back to A. On
frequent short heads OIC Teddy is slower than units (`th|he|an` 4.66 ms against 1.24 ms), so B also
needs a member-length floor (heads of 3+ characters) or a measured choice.

**Option C. Look through zero-width nodes (L1).** Add to the walk's look-through list the nodes that
read no text and cannot move the start: word boundaries (`\b`, `\B`, start/end of word, default
boundary), line and string anchors, and lookarounds (skip the assertion, follow its continuation).
A match starting at `p` still opens with a head literal at `p`, so the screen stays sound. Must
refuse: `\K` is fine (it moves the reported start, not the attempt start) but `\G`, conditionals and
group calls take the default (refuse). Worst case as item 14: one sweep per attempt, 19-28 ns a hit.
Expected gain 90-165x on L1 probes; the engine then still checks `\b` at each candidate.

**Option D. Prefix-set expansion (L3).** Generalise the walk from "first literal on each path" to a
small cross-product: an optional group yields its body's heads plus the continuation's heads, a
small class `[ab]` times the next literal yields `acat`, `adog`, ... Capped (Rust `regex-syntax`
`Seq` caps at 64 members and shrinks to prefixes when over). Worst case: compile time
proportional to the cap; screen cost as item 14. Gain only on L3, which is less common.

**Option E. Required literal set, refuse-only (L4).** When the required element at a variable offset
is an alternation of literals, keep its members as a `SearchValues<string>` beside `ReqString`. Before
an attempt at `p`, if the cached next member hit `h` is below `p`, search again from `p`; no hit
means no attempt from here can succeed, so fail the scan. Total cost one pass over the text plus one
call per hit, and attempts are never moved, only refused, so it composes with `(*SKIP)` the way the
existing offset -1 arm does. Must also refuse when the set sits inside a lookaround, a conditional
or a repeat with minimum 0, which `get_required_string` already excludes. Gain: L4 no-match scans
(160 ms probe) fall to one Teddy pass, about 0.1 ms; matching subjects gain only on their tail.
Choose between the single-character `ReqString` and the set by rarity (S60b item 8's table): longer
members win.

**Option F. Self-disable (aho-corasick `MIN_SKIPS = 40`, `MIN_AVG_FACTOR = 2`).** Count screen calls
and bytes skipped; after 40 calls averaging under 2 x the minimum member length skipped, switch the
screen off for the rest of the scan. Not needed by the measurements so far: with frequent members
the screen still beat no screen 5-10x (`(?:th|he|an)q` 1.55 ms against 8.76 ms; `(?:the|and)q`
1.02 ms against 10.46 ms), because each rejected attempt costs the engine more than the 11-28 ns
call. Caveat: in those rows the trailing `q` is also a fixed-offset required string, so both variants
already jump between `q`s and the ratio understates neither side cleanly; the build session repeats
them with a tail that is not a literal. Kept as the fallback if the dense adversary shows a loss.

## 4. Unknowns and the probes that settle them

| Question | Probe | Result |
|---|---|---|
| Which shapes have no screen | `run1.txt` part A | Section 2 |
| Does OIC match mrab IGNORECASE | part B, ten pairs against `FuzzyRegex.IsMatch` | No for Kelvin, long s, `ß`; yes for `σ/ς`, `É`, ASCII |
| Which algorithm .NET picks | `GetType().Name` | 2-3 ASCII strings of 2-3 chars: Teddy non-bucketised; 10 strings: Teddy bucketised; 2,000 strings, 1-char members or non-ASCII members: Aho-Corasick; same picks for OIC |
| Why IgnoreCase misses `LeadingUnits` | part D node dump | Zero-width `SetUnionIgn` first-set check not in the look-through list |
| Dense-hit adversary | part C, `"th"` x 512K | Raw: 11 ns a call. Engine rows were void: the trailing `q` is a required string absent from the subject, so all three variants failed at once (30 us). Rerun with a tail that is not a required string |
| Is the screen worth it with frequent members | part C | Yes, 5-10x (Option F note) |

Still open for the build session, each a one-line probe: the engine-level dense adversary (above); whether lookaround look-through (Option C)
interacts with `HasSkipVerb`-free verbs (`(*PRUNE)` inside a lookahead); the exact OIC closure table
for Option B, checked against `RequiredStringScreen.FirstCodepoints` over the BMP (the
`LeadingUnitsTests` reference loop already does this).

## 5. Recommendation

Build in this order, one commit each, stopping when a step does not pay:

1. **Option A** (close the L2 gap). Smallest change, fixes a screen that is believed to work and does
   not. Expected: case-insensitive alternation over a megabyte from 8.2 ms to roughly the units cost
   (under 1 ms).
2. **Option C** (zero-width look-through, word boundaries and anchors first, lookarounds second).
   Largest common-case win: 90-165x on whole-word keyword and list search.
3. **Option E** (required literal set) for L4 no-match scans.
4. Option B only if A leaves case-insensitive rows measurably behind the case-sensitive ones, with
   the closure guard. Options D and F only on evidence.

**Bench rows.** No current row exercises L1, L2 or L4: `CaseFoldedScan` is one literal,
`CaseFoldAccented` (`straße|café`, full folding) is already screened, `ValidateEmails` is anchored
and per-line. Propose, in `WorkloadBenchmarks` over `Corpus.Long` and in `ManyInputsBenchmarks` over
the usage lines, each with a Python pair for the gap table:
- `WholeWordList`: `\b\L<words>\b` with ten words, `Count` (L1).
- `CaseBlindAlternation`: `(?i)(?:amber|copper|violet)`, `Count` (L2, and L1 if written with `\b`).
- `RequiredDomainNoMatch`: `\w+@(?:gmail|yahoo)\.com` over a subject with `@` but no member,
  `IsMatch` (L4).

**Gates** (per the optimise skill and S60b's verification): differential against main over the
oracle and a shape sweep (the screen can only skip; a changed answer is a bug); the three permanent
files (`BacktrackingVerbTests`, `PartialMatchingTests`, `ReverseMatchingTests`) green and unchanged;
pinned before/after on all 37 gap rows with no row outside the noise floor (1.10); the dense
adversary no slower than no screen; a SYNC-DIVERGENCE row extending row 68; `LeadingUnitsTests`'
reference loop extended to the new shapes; AOT green.
