# What the Rust fuzzy-regex library does that Phase 7 can borrow (2026-09-18)

Owner question (2026-09-18 evening): are there optimisation techniques in `fuzzy-regex-rs`
(https://kakserpom.github.io/fuzzy-regex-rs/intro.html, source github.com/kakserpom/fuzzy-regex-rs)
that this port should add to its optimisation plan? Reviewed by an Opus subagent against
`OPTIMISATION-NOTES.md`, the ROADMAP's Phase 7 section and slices S58-S63, with the two actionable
claims re-checked by the orchestrator against the source pages the same evening.

**Answer in one line:** two additions, both folded into S60 (prefilter family); nothing warrants a
new slice; most of the library's speed comes from a non-backtracking automaton design and a
different fuzzy algebra that this port must not copy, because it returns mrab-regex's answers exactly.

## Sources read

Book pages `impl_overview`, `impl_dfa`, `impl_bitap`, `impl_bridge`, `perf_tips`, `advanced_wordlists`,
`installation`, `impl_streaming`; repo docs `docs/RE_SHARP_OPTIMIZATIONS.md` and
`docs/OPT_FUZZ_FINDINGS.md` (raw.githubusercontent.com, master). Quotes below are verbatim from
those pages on 2026-09-18.

## Techniques and verdicts

| # | Technique (where it acts) | Their evidence | Verdict for this port |
|---|---|---|---|
| 1 | Literal prefilter with SIMD variants: `None / SingleByte / TwoBytes / ThreeBytes / Teddy` (prefilter) | RE_SHARP §2: "`SingleByte` ... `memrchr` for single byte", "`Teddy` ... SIMD range matching" | Already in plan: S60 items 1-4 (`SearchValues<char>`, vectorised `IndexOf`) |
| 2 | Literal extraction from the pattern to drive the prefilter (compile) | impl_bridge: "Use extracted literals to quickly skip non-matching regions" | Already in plan: S60 item 1, required-string case flags in `PatternObject.cs` |
| 3 | **Byte-frequency rarity gate**: pick the rarest pattern byte to skip on, only when profitable (prefilter) | OPT_FUZZ_FINDINGS §5 roadmap (borrowed from resharp): "byte-frequency skip with a profitability gate (highest ROI)"; their slowest path is "full-scan no-match" | **New, applicable. Added to S60** as item 8. Pure skip-choice: cannot change an answer. Risk is a regression on short subjects, which S58's noise floor is there to catch |
| 4 | End-anchor windowing: search only a window near the end for end-anchored patterns (search loop) | impl_bridge: "the result set is identical - only the work is bounded"; disabled under multi-line `$` | Already in plan: upstream's `search_start_END_OF_LINE_rev` twin, S60 item 2 (`Matcher.cs:1897`, `:4778`) |
| 5 | Reverse search for `.*SUFFIX` (compile/search) | impl_overview §3: "`.*test` uses reverse search (O(n) vs O(n^2))" | Not applicable (settled 2026-09-18 evening, `2026-09-18-optimisation-research.md` §1): regex-automata's own `strategy.rs` documents the hazard, `/[a-z]+ing/` on `tingling` reports `ting` "But 'tingling' is the correct match because of greediness"; it needs a reverse DFA and a "no earlier match" proof that `(*SKIP)`/`(*PRUNE)` make undecidable here |
| 6 | Pure greedy dot-star returns instantly (compile) | impl_overview §3 | Not applicable: `.*` here still sets group 0, fuzzy counts and `(*SKIP)` state |
| 7 | **Aho-Corasick fast path for large word lists behind a threshold** (compile/search) | advanced_wordlists: "Results are identical to the alternation; only the speed differs", "DEFAULT_WORD_LIST_AC_THRESHOLD == 64" | **New, applicable. Added to S60** as item 9: named lists (`\L<name>`, `PatternObject.cs:103`, `:109`) are matched per character via `Matcher.InSetUnion` (`Matcher.cs:536`); a per-list trie or `SearchValues<string>` above a threshold is answer-identical by construction but needs its own oracle wave |
| 8 | Compiled-automaton cache and byte serialization (memory) | OPT_FUZZ_FINDINGS §5: "47-53x on repeated pattern use" | Cache: already S59. Serialization: not applicable (no mrab API; AOT and versioning surface) |
| 9 | Bounded-repeat explicit count states (compile/memory) | OPT_FUZZ_FINDINGS §5: "linear, not exponential" | Already known and deferred post-1.0: the `NodeCompiler.cs` unrolling row in OPTIMISATION-NOTES. Their note corroborates the lift |
| 10 | Case folding compiled into transitions (compile) | OPT_FUZZ_FINDINGS §5, "Medium ROI" | Already in plan: S60 item 6 (`Unicode/Encodings.cs:166`, `:177`) |
| 11 | Two-pass reverse prefilter then verify for all-matches (search loop) | RE_SHARP §2, their own number "two_pass: 1.10s <- Still O(n^2)" | Not applicable: no win by their measurement, and pass 2 dedups leftmost-longest |
| 12 | "Hardened" O(n) all-matches tracking every active DFA state (search loop) | RE_SHARP §3: "~150x faster for 10KB text" | Not applicable: needs a DFA; picks matches by DFA leftmost semantics, this port backtracks |
| 13 | Bitap / Damerau-Levenshtein automata for fuzzy matching (fuzzy costing) | impl_bitap: "O(n x k) time complexity" | Not applicable: a different fuzzy algebra (transpositions, similarity scores); mrab costing (`Matcher.cs:3141`, `:3151`) is pinned |
| 14 | Minterms and partition refinement; `mimalloc`; streaming O(window) memory | OPT_FUZZ_FINDINGS §5 "low feasibility without a rewrite"; installation; impl_streaming | Not applicable: symbolic rewrite; .NET has no pluggable allocator (we pool, `ByteStack.cs`); streaming is outside mrab's API |

## Semantics that differ from mrab-regex (never copy)

- Non-backtracking automaton match selection ("the leftmost `start_pos` among all accepting states").
- Damerau-Levenshtein transposition counts as one edit under `{e<=1}`; mrab's `e` is insert, delete, substitute only.
- Similarity scores "in the 0.0-1.0 range" gating matches; no mrab equivalent.
- Per-operation limits: their own findings doc records that unspecified ops were treated as unlimited and that explicit `s<=0` leaked; the mrab rule this port implements is the one they were fixing towards.
- Zero-width whole-pattern deletion: "mrab emits empty match, fuzzy-regex declines".
- Multi-piece fuzzy groups each honouring the full budget (an over-match they acknowledge).
- `.*` returning "instantly"; streaming results bounded by a chunk window.

## Where this landed

S60 items 8 and 9 (this file's #3 and #7); a section in `OPTIMISATION-NOTES.md`; a pointer
paragraph in the ROADMAP's Phase 7 section. No spec amendment: Phase 7's scope and answer-identity
rule are unchanged.
