# Optimisation research sweep (2026-09-18 evening)

Owner request: to undertake the additional research needed to fully mine optimisation opportunities, because they really want this library to be fast and to scour the literature (including latest research) for additional algorithmic and hardware optimizations. Three Opus subagents ran read-only
sweeps the same evening; the orchestrator spot-checked each report's decisive quotes against the
cited source before anything below was written. Companion: `2026-09-18-fuzzy-regex-rs-techniques.md`
(the Rust fuzzy-regex library, reviewed earlier the same evening).

The rule every verdict is judged by: this port returns mrab-regex's answers exactly. A pass that
can only **reject** ("no match can start in this region") or **bound** the search is admissible by
construction. Anything that **selects** a match needs either a proof or an oracle wave before it may
land, and anything that changes which match is returned is out.

## 1. Prefilters and fuzzy fast paths (Rust regex-automata, RE2, Myers, Wu-Manber, Navarro, Hyperscan)

### The one new algorithm: a reject-only prefilter that survives a fuzzy section

Today the port has no prefilter at all, and under a fuzzy section even S60's required-string
locator is unusable, because an error can delete the required character. Navarro's pattern
partitioning restores one. Navarro, *A Guided Tour to Approximate String Matching* (ACM Computing
Surveys 33(1), 2001; text identical to TR/DCC-1999-005, https://www.dcc.uchile.cl/TR/1999/TR_DCC-1999-005.pdf), §8.1:

> "if neither 'sur' nor 'vey' appear in a text area, then 'survey' cannot be found there with one
> error under the edit distance. This is because a single edit operation cannot alter both halves
> of the pattern."

and the admissibility statement itself:

> "a filtering algorithm is normally unable to discover the matching text positions by itself.
> Rather, it is used to discard (hopefully large) areas of the text which cannot contain a match ...
> Any filtering algorithm must be coupled with a process that verifies all those text positions that
> could not be discarded by the filter."

For a fuzzy section with total error budget `k`, split its literal part into `k+1` pieces; a window
where none of the pieces occurs (vectorised `IndexOf`) cannot contain a match and is skipped; the
backtracker verifies everything else, unchanged. It only ever skips, never chooses. Caveat from the
same section: "the performance of filtering algorithms is very sensitive to the error level", so it
is gated on `k` small relative to the literal length, by measurement.

Optional second stage, only if stage one leaves too many candidates: Myers' bit-vector algorithm
(JACM 46(3), 1999, "requires only O(nm/w) time") computes the minimum Levenshtein distance over a
candidate window; reject the window if that minimum exceeds the section's total budget. Plain
Levenshtein with `k = e_max` is a relaxation of mrab's `{i<=a,d<=b,s<=c,e<=k}` (it can only
under-reject), so it stays reject-only and safe.

**Landed in the plan as S60 item 10.** Build beside the required-string analysis in
`PatternObject.cs` (around lines 253-276 and `GetRequiredChars` at 393); consult at the search-start
sites `Matcher.cs:4725` and `:4793`; honour the slice-narrowing site at `Matcher.cs:10049` so a
`(*SKIP)`-moved position is never re-searched (S60 item 5's rule). Not upstream code, so it carries a
`sync-divergence:` marker and a SYNC-DIVERGENCE.md row. Expected win: the full-scan no-match
workload on fuzzy patterns, where the port pays O(n x attempt) today.

### Confirmed already in plan

- First-byte `memchr` and literal-prefix candidates (RE2 regexp3; Rust `regex` internals): S60
  items 1 and 4. The answer-identity warrant for swapping scalar scans to `IndexOf` is memchr's own:
  "For all non-empty needles, these routines will report exactly the same values as the
  corresponding routines in the standard library" (https://docs.rs/memchr/latest/memchr/memmem/).
- Reverse-anchored search when the pattern is anchored at the end and not the start: S60 item 2
  (`search_start_END_OF_LINE_rev`, `Matcher.cs:1897`, `:4778`). regex-automata's `strategy.rs`
  precondition list ("not also anchored at the start"; anchored searches fall back to the core
  engine) is a ready-made test matrix for that item.
- Teddy and Aho-Corasick for many literals: S60 item 9 (`SearchValues<string>` for `\L<name>`).
- Rarity heuristics for the skip byte: S60 item 8.

### Rejected, with the citation that settles it

- **Reverse-suffix and reverse-inner search** (regex-automata `meta/reverse_inner.rs`,
  `meta/strategy.rs`). Both select a match, need a reverse DFA, and carry a documented
  answer-changing hazard: for `/[a-z]+ing/` against `tingling`, stopping at the first suffix hit
  reports `ting`, "But 'tingling' is the correct match because of greediness." Worse here:
  `(*SKIP)`/`(*PRUNE)` move the search slice mid-attempt, so the "no earlier match" obligation is
  not decidable from the pattern. This closes the "unclear" left on fuzzy-regex-rs #5.
- **DFA-first then NFA on the matched region** (RE2), **lazy DFA**, **bit-parallel NFA as the
  engine**: need an automaton engine, phase-sized, and import automaton match selection.

### The fuzzy-literal fast path: what would have to be true

A fast path answering a pure literal under `{e<=k}` with a DP or bit-vector scan is answer-identical
only if all four hold, and the fourth is already known to fail:

1. mrab returns the leftmost start at which any match exists, ignoring distance.
2. Given the start, the end and the (i, d, s) mix are the first the backtracker reaches, not the
   minimum: that is exactly what `BestMatch` changes (`FuzzyRegexOptions.cs:113`, "Find the best
   fuzzy match rather than the first one"). An `argmin` scan is therefore the wrong rule.
3. `FuzzyCounts` and `FuzzyChanges` must agree, not only the span; they are public.
4. `BESTMATCH`/`ENHANCEMATCH`, per-operation limits, `(*SKIP)`-reachable slices, partial matching
   and reverse search must be excluded.

Oracle experiment that decides it (not run; record here for a later slice): literals of length 3-8
over a 3-symbol alphabet, `k` in 1..3, every subject up to length 12 plus a random sample at 16-24;
record `(start, end, fuzzy_counts)` from mrab-regex and compare with (a) argmin-distance-then-leftmost,
(b) leftmost-then-argmin, (c) leftmost-then-first-in-backtracking-order. Expected outcome on current
evidence: only (c) has zero disagreements, which means a DP scan is admissible **only as a reject
filter** (the S60 item 10 second stage), never as an answer producer.

## 2. Production backtracking engines: .NET Regex (.NET 7 onwards) and PCRE2

Sources: Stephen Toub, "Regular Expression Improvements in .NET 7"
(https://devblogs.microsoft.com/dotnet/regular-expression-improvements-in-dotnet-7/); the
dotnet/runtime backtracking engine source `RegexFindOptimizations.cs` and `RegexNode.cs` (main,
fetched 2026-09-18; the blog post truncated before its later sections, so the source is quoted where
the post was unreachable); PCRE2 `pcre2_study.c`, `pcre2_auto_possess.c` (master), and the
`pcre2perform` and `pcre2api` man pages. The orchestrator re-fetched `RegexFindOptimizations.cs` and
`pcre2_auto_possess.c` and confirmed the quotes used below.

These two engines matter most because they are backtrackers like this port: every technique here
keeps backtracking semantics and is either a start-position filter, a compile-time rewrite with a
documented guard list, or a bound.

### New, applicable, folded into S60 (items 11-16)

| Technique | Evidence | Where it lands, win, risk |
|---|---|---|
| **Fixed-distance sets at non-zero offsets, quality-ranked, capped** | RFO ctor: "Build up a list of all of the sets that are a fixed distance from the start of the expression."; "whatever set is first is the one deemed most efficient to use."; `const int MaxSetsToUse = 3; // arbitrary tuned limit` | S60 item 11. mrab has one fixed-offset required string (`PatternObject.cs:179` `ReqOffset`); the set-at-offset list is built from `Parsing/Nodes.cs:154-177` (`GetFirstset`/`GetRequiredString`) and consumed at `Matcher.cs:4718`/`:10046`. Big on class-heavy no-match scans. Reject-only if used to discard start positions; must take the slice from the same state as the slow path (item 5) |
| **Per-position minimum-length pruning and end-anchor fixed-length jump** | RFO: "Return early if we know there's not enough input left to match."; TrailingAnchor: "If there is one, and we can also compute a fixed length for the whole expression, we can use that to quickly jump" | S60 item 12. `MinWidth` exists (`NodeCompiler.cs:156`, `MatchState.cs:580`) and is checked once per attempt (`Matcher.cs:9238`, `:9395`, `:9739`) but never per candidate start; no end-anchor jump. Turns the tail of every failing scan into O(1). Keep the existing `MaxErrors == 0` condition: `MinWidth` is an exact-match bound and deletions shorten a fuzzy match |
| **Literal after loop** | T7: "even though the loop wasn't written as an atomic loop, it can be processed as one."; RFO `LiteralAfterLoop_LeftToRight`: "The loop doesn't overlap with the literal, so we can start from after the last place the literal matched." | S60 item 13, non-fuzzy only. Lands at `Matcher.cs:4718`/`:4772` with loop-node data from `NodeCompiler.cs`. The largest single .NET win on `[\w.+-]+@...` shapes. Top risk: under fuzzy costing the loop's set can consume the literal by substitution, so the non-overlap premise fails; guard on `MaxErrors == 0`, greedy loop, no verbs |
| **Multi-string leading search** | RFO ctor: "If there are multiple case-insensitive leading strings, we can search for any of them."; "prefer multi-string search via SearchValues if available." | S60 item 14: generalises item 9 from `\L<name>` lists to literal top-level alternations (`Parsing/Nodes.cs:1099-1116`). Nearly free once item 9 exists. Risk: the multi-string search must return the earliest position and let the engine choose the branch |
| **First-unit versus required-unit clearing** (a correctness trap, not a speed item) | pcre2_study.c: "If it is the same as a required later code unit, then clear the required later code unit."; "Patterns such as /a*a/ don't work if both the start unit and required unit are the same." | S60 item 15: write the `a*a` test red before the locator ships (`PatternObject.cs:179` plus the locator) |
| **Start-code bitmap with caseless-pair collapse** | pcre2_study.c `set_start_bits`: "attempts to build a bitmap of the set of possible starting code units whose values are less than 256."; "In 16-bit and 32-bit mode, values above 255 all cause the 255 bit to be set."; "plausibly worth doing for patterns such as [Ww]ord or (word|WORD)." | S60 item 16, an implementation choice inside item 4: a 256-bit bitmap plus an escape bit may be cheaper to build per pattern than `SearchValues<char>` for wide sets; measure both. Also .NET's own thresholds: "we'll use that span-based overload for sets with four or five characters", above which `SearchValues` |
| **Leading `.*` auto-anchoring** | pcre2perform: "If the pattern has multiple top-level branches, they must all be anchorable."; "automatically disabled if the pattern contains" `(*PRUNE)`/`(*SKIP)`; "That saves PCRE2 from having to scan along the subject looking for a newline to restart at." | S60 item 17, small. Lands `Optimiser.cs:21`. Guard exactly as PCRE2 does plus `MaxErrors == 0` |

Corroborations for items already planned: `HasHighFrequencyChars` ("When the characters are rare,
IndexOfAny is an excellent filter and is preferred.") is first-party backing for item 8; PCRE2's
following-literal check ("PCRE2 checks that there is a 'b' later in the subject string") is
independent confirmation that `locate_required_string` (item 1) is the shared idea, not an mrab quirk.

### The biggest bet, proposed as a NEW slice (owner decision needed)

**Automatic atomicity (.NET) and auto-possessification (PCRE2).** Both engines rewrite the compiled
pattern so that loops nothing can backtrack into become atomic: RegexNode.cs `EliminateEndingBacktracking`:
"If we find backtracking construct at the end of the regex, we can instead make it non-backtracking
... since nothing would ever backtrack into it anyway."; "The correctness of this optimization
depends on nothing being able to backtrack into"; pcre2api: "it turns a+b into a++b in order to avoid
backtracks into a+ that can never be successful." This is the fix for the `(a|a)*b` class that took
10.6 s at n=24 (S19). It is a compile-time graph rewrite in `Optimiser.cs:21` (`OptimisePattern`,
beside `SkipOneWayBranches:55` and `SetTestNodes:613`), so it is a `sync-divergence:` item with its
own soundness argument, exactly like the counted-repeat lift. Answer-preserving only if nothing can
backtrack in, and in this engine fuzzy errors, `(*SKIP)`/`(*PRUNE)`, group calls and recursion,
conditionals, lookbehind and `lastindex` reporting all break the premise. PCRE2's guard list is the
precondition set to port verbatim: "A non-greedy iterator must never be possessified."; "If the
bracket is capturing it might be referenced by an OP_RECURSE so its last iterator can never be
possessified if the pattern contains recursions."; "Fixed-length lookbehinds can be treated the
same way, but variable length lookbehinds must not auto-possessify their last iterator."; and the
bounded-analysis precedent: "the check just stops, leaving the remainder of the pattern
unpossessified." Needs its own oracle wave and a SYNC-DIVERGENCE.md row. Not an S58-S63 item;
proposed as **S62b** after S62 (inner loop) so the inner loop is measured first and the rewrite's
win is attributed honestly. Recorded here pending the owner's decision; no slice file yet.

### Not applicable, with the reason

- Source generator / `RegexOptions.Compiled` and PCRE2's JIT: no IL emit under Native AOT, and a
  generated matcher abandons mrab's graph shape that `sync-upstream` depends on.
- `RegexOptions.NonBacktracking`: the post makes no identity claim ("also has a subtle difference
  with regards to execution"); negative evidence that supports keeping the backtracker.
- PCRE2 match/depth limits: `timeout` already bounds work; new public surface is frozen since S53b.
- Case-insensitivity resolved at construction (`(?i)abcd` to `[Aa][Bb][Cc][Dd]`): unclear. mrab
  resolves case through `CaseEncoding` at match time (`Matcher.cs:325`, `Encodings.cs:166`);
  pre-expansion is answer-preserving only if full case folding, Turkic and `FULLCASE` all agree,
  which needs both engines run. S60 item 6 is the home if anyone proves it.
- Alternation lowering (single-letter branches to sets, common prefix and suffix extraction):
  already mrab's own, in the port at `Parsing/Nodes.cs:1099`, `:1111`, `:1116`, `:1301`.

### Evidence worth citing in S60's constraint section

pcre2api: "Disabling start-up optimizations may change the outcome of a matching operation."; with
`(*COMMIT)ABC` on `DEFABC` the unoptimised run gives "no match"; the `(*MARK)` case shows start
optimisations "do affect the auxiliary information that is returned". First-party, non-mrab
evidence that a start optimisation legitimately changes `(*COMMIT)`-class answers and the last-mark
value, which is precisely the divergence the three permanent verb test files pin.

## 3. Recent literature and hardware (2018-2026)

Sources: Davis, Servant, Lee, "Using Selective Memoization to Defeat Regular Expression Denial of
Service" (IEEE S&P 2021, https://davisjam.github.io/files/publications/DavisServantLee-SelectiveMemo-IEEE-SP21.pdf);
Fujinami and Hasuo (ESOP 2024, https://arxiv.org/abs/2401.12639); Berglund, van der Merwe, le Roux
(NCMA 2026, https://arxiv.org/abs/2606.26678); Moseley et al. (PLDI 2023, .NET NonBacktracking);
Turoňová et al. (USENIX Security 2022, FoSSaCS 2023, https://arxiv.org/abs/2301.12851); Wang et al.
(NSDI 2019, Sheng); Parabix/icgrep; dotnet/runtime PR 88394 (`SearchValues<string>` internals);
the Native AOT optimising guide; Toub's .NET 10 performance post. One quote (Rohou et al., CGO 2015,
on indirect-branch prediction) could not be fetched in full and is marked unverified.

### The single biggest bet: selective memoisation of failed positions

Davis et al. make a backtracking engine linear-time **without changing its answers** by remembering
the simulation positions (state, text offset) that have already failed: "Full memoization is sound
and compatible, but its space costs are too high." and "selective memoization lowers the space cost
of memoization by an order of magnitude for the median regex, and that run-length encoding lowers
the space cost to constant for 90% of regexes." Their Table I gives O(|Q|^2 x |w|) time and
O(|Q| x |w|) space for the memoised Spencer-style engine. They also measured the representation:
"the RLE representation (green) achieves constant space costs for most regexes", and against a hash
table "30-50% of the possible simulation positions are explored, and the overheads of the hash
table outweigh the savings in unfilled entries." And they name the exact shape this port measured as
catastrophic (S54: `(a|a)*b` from 161 ms at n=18 to 10.6 s at n=24): Perl's partial scheme "is not
sound - e.g., it protects (a*)* (otherwise exponential), but not (a|a)* (exponential) nor a*a*
(quadratic)." Fujinami and Hasuo extend the result to "look-around atomic grouping" with
"linear-time backtracking matching algorithms" whose "efficiency relies on memoization, much like
one Davis et al."; Berglund et al. (2026) shrink the memoised set further ("MFN provides correctness
guarantees equivalent to CN while often using fewer (memoized) states") and list "counters,
backreferences, and lookaheads" as still open.

Why it is a bet and not an item: this port's simulation position is not (state, offset). It is
(node, offset, **fuzzy error counters**, group and repeat stack), and a position reached with fewer
errors can succeed where a costlier one failed, so memoising on (node, offset) alone is unsound under
`{e<=k}`; the counters must be in the key. Davis §IX.C: "Because the contents of a capture group
depend on the path taken through the automaton, REWBR disrupts our path-independent memoization
scheme." (backreferences: disable, or key on the capture vector). §IX.D covers side effects, which is
`(*SKIP)`/`(*PRUNE)`/`(*COMMIT)` here: disable when a verb is present; `BacktrackingVerbTests` pins
those answers. Landing: a memo probe beside the iteration and cancel check at `Matcher.cs:4857`, in
front of the main `switch (node.Op)` at `:4864`, inside `BasicMatch` (`:4661`); RLE or bitmap
representation, not a hash table. Cost: a phase, not a slice, with a soundness argument and an oracle
wave, the bar `OPTIMISATION-NOTES.md` already sets for the counted-repeat rewrite. **Recorded as a
post-Phase-7 candidate for the owner's decision.** It and the auto-atomicity proposal (§2) attack the
same catastrophic class from two sides; memoisation is the general one, atomicity the cheap one.

### Applicable now, small

- **Native AOT vector width is fixed at publish.** "By default, the compiler targets the minimum
  instruction set supported by the target OS and architecture"; `IlcInstructionSet` and
  `IlcMaxVectorTBitWidth` control it (dotnet/runtime nativeaot/docs/optimizing.md). So
  `Vector256.IsHardwareAccelerated` is a publish-time constant under ILC, and a vector win measured
  under the JIT may not exist in the shipped AOT binary. **Added to S62 as a verification item**:
  state the published instruction set before trusting any vector or inlining number.
- **`SearchValues<string>` already chooses Teddy, Aho-Corasick or Rabin-Karp** (dotnet/runtime PR
  88394: "Teddy-based approach for searching for length=2 or 3 prefixes... Aho-Corasick-based
  approach that we use when dealing with many values... Rabin-Karp... fallback"; Arm64 Teddy in
  PR 118110). S60 items 9 and 14 must use it and not hand-roll Teddy.
- **.NET 10 JIT stack-allocates non-escaping objects after inlining** (Toub, .NET 10 post): a
  `ref struct` enumerator's win under the JIT may be smaller than expected and larger under AOT.
  S61 already demands both measurements; this is the reason to keep doing so.
- **Indirect-branch prediction may no longer be the interpreter bottleneck** (Rohou, Swamy, Seznec,
  CGO 2015, abstract only: "the accuracy of indirect branch prediction is no longer critical for
  interpreters"; unverified). Supports keeping S62 item 5 (replace `switch (node.Op)` dispatch) as
  "propose, do not implement". Verify the quote before citing it anywhere binding.

### Post-1.0, principled fix for a known ceiling

Counting-set automata (Turoňová et al.): "matching linear in the length of the text and independent
of the repetition bounds" for "synchronizing" counted regexes, which "covers nearly all counting
used in usual applications". Determinised automata only, so not liftable wholesale; the transferable
idea is a counter register per repeat instead of copied nodes, which is the principled version of the
`NodeCompiler.cs:1430` unrolling row (`(a{1000}){1000}` = 238 MB; S56b only bounds it). Same
soundness debt that row already names.

### Not applicable

- Sheng shuffle-based DFA (NSDI 2019; PSHUFB's 16 lanes, AVX-512 VBMI for more): a DFA technique,
  and Native AOT freezes the instruction set. Prefilter use only, and `SearchValues` already covers it.
- Parabix/icgrep bit-stream transposition ("roughly about 1 CPU cycle per byte" just to transpose):
  a whole-buffer scan, incompatible with per-position backtracking.
- Wavefront alignment and Edlib: gap-affine alignment cost, a different objective from mrab's
  fuzzy cost model; adopting it changes answers.
- .NET NonBacktracking / derivatives (PLDI 2023): "supports anchors and counting, preserves
  backtracking semantics" but no backreferences, atomic groups or fuzzy; the precedent that
  identity can be **proved** is the only thing to take from it.
- `RegexOptions.Compiled` under AOT "ignores the Compiled option and instead falls back to
  interpreting"; source generators need a compile-time-constant pattern. Already in the notes.

## Summary: what changed in the plan tonight

| Where | Change |
|---|---|
| S58 | fuzzy no-match large-subject workload added |
| S60 | items 8-17: rarity gate, `\L<name>` fast path, fuzzy reject filter (Navarro), fixed-distance sets, per-position min-length and end-anchor jump, literal-after-loop (non-fuzzy), multi-string leading search, `a*a` trap test, start-code bitmap, leading `.*` anchoring |
| S62 | verification item: published `IlcInstructionSet` stated before any vector number is trusted |
| Proposed, owner decision | S62b auto-atomicity / auto-possessification (§2); post-Phase-7 selective memoisation (§3) |
| Not adopted, with citations | reverse-suffix/inner, DFA engines, Bitap-as-engine, dot-star shortcuts, NonBacktracking, JIT/IL emit, Sheng, Parabix, WFA |

S60 is now seventeen items; when Phase 7 starts, split it into a locator slice (items 1-7, 15) and a
start-position filter slice (items 8-14, 16, 17) so each sitting has one measurable target.
