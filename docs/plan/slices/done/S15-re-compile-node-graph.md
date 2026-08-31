---
slice: S15
phase: 3
title: re_compile - the code list becomes the node graph
delivers: []
---

# S15 - re_compile: the code list becomes the node graph

The port of `_regex.c`'s compiler: everything between receiving the code list (our
`CompiledPattern`, `src/FuzzyRegex/Parsing/CompiledPattern.cs`) and having a runnable node
graph. New directory `src/FuzzyRegex/Engine/`, mirroring `upstream/src/_regex.c` name for name,
same house rules as `Parsing/` (AGENTS.md: keep upstream names recognisable, quote the upstream
line above anything non-obvious).

## Scope

All line references are `upstream/src/_regex.c`.

- **`RE_Node`** (`_regex.h`; the C struct with `op`, `values`, `next_1`/`next_2`, `status`,
  `string`/`nonstring` union) and the node-level tables it needs. This is very likely the port's
  first field-exposing struct: the `.editorconfig` comment at the `CA1051` block records that
  Sonar's S1104 and MA0008 fire on that shape and fail the build under `TreatWarningsAsErrors`.
  Re-measure and handle both in this slice, per that comment - in the engine scope, on the merits,
  not preemptively. If this slice ends up with no such struct, the duty moves to S16, which
  definitely lands one.
- **Node construction and bookkeeping**: `create_node` (`:23864`), `add_node` (`:23918`),
  `ensure_group` (`:23926`), `record_ref_group` (`:23963`), `record_group` (`:23973`),
  `record_group_end` (`:23990`), `ensure_call_ref` (`:23996`), `record_call_ref_defined`
  (`:24033`), `record_call_ref_used` (`:24045`), `sequence_matches_one` (`:24056`),
  `record_repeat` (`:24067`), `get_step` (`:24104`).
- **The builders, all of them**: `build_ANY` (`:24157`), `build_FUZZY` (`:24190`), `build_ATOMIC`
  (`:24291`), `build_BOUNDARY` (`:24349`), `build_BRANCH` (`:24376`), `build_CALL_REF`
  (`:24450`), `build_CHARACTER_or_PROPERTY` (`:24509`), `build_CONDITIONAL` (`:24547`),
  `build_GROUP` (`:24680`), `build_GROUP_CALL` (`:24757`), `build_GROUP_EXISTS` (`:24792`),
  `build_LOOKAROUND` (`:24900`), `build_RANGE` (`:24976`), `build_REF_GROUP` (`:25015`),
  `build_REPEAT` (`:25046`), `build_STRING` (`:25235`), `build_SET` (`:25282`), `build_SUCCESS`
  (`:25374`), `build_zerowidth` (`:25393`), `build_charset_equiv` (`:25418`), `build_sequence`
  (`:25490`), `compile_to_nodes` (`:25701`). Build every opcode now, including the Phase 4 and 5
  ones - the graph must hold whatever the parser emits, and only *matching* them is later work.
- **The optimiser**: `optimise_pattern` (`:23821`) and its parts - `skip_one_way_branches`
  (`:23136`), `add_repeat_guards` (`:23237`) with `CheckStack` (`:23188-23236`) and `add_index`
  (`:23482`), `record_subpattern_repeats_and_fuzzy_sections` (`:23517`), `use_nodes` /
  `discard_unused_nodes` (`:23617`, `:23641`) with `NodeStack` (`:23573-23616`),
  `mark_named_groups` (`:23672`), and `set_test_nodes` (`:23805`) with `can_test_past` (`:23697`)
  and `set_test_node` (`:23716`). Port the test-node marking even though the match loop's
  fast path that consults it is deferred to Phase 7 (DECISIONS 2026-08-31) - the graph shape
  should match upstream's so Phase 7 is a switch-flip, not a re-plumb.
- **`re_compile` itself** (`:25863-26121`): the tail after argument unpacking - our input is the
  `CompiledPattern` record, so the `PyArg_ParseTuple` half drops. `get_required_chars` (`:25756`)
  and `make_STRING_node` (`:25802`) for the required-string fields, which are stored on the
  compiled pattern now and consulted when Phase 7 ports the prefilters.
- **The rejections.** `compile_to_nodes` failing is upstream's `RuntimeError: invalid RE code`.
  Five patterns reach it that this port currently compiles: `{e<=1:\X}`, `{e<=1:\b}`,
  `{e<=1:\A}`, `{e<=1:\Z}`, `{e<=1:\L<a>}` (each attached to a preceding item, e.g.
  `a{e<=1:\b}`) - a fuzzy test must match exactly one character, upstream's parser checks only
  the syntactic shape, and its C compiler rejects the opcode (S13 closing notes; DECISIONS
  2026-08-31; re-verified 2026-08-31 against regex 2026.7.19: all five raise
  `RuntimeError: invalid RE code`). Decide the exception type this maps to and record it in
  DECISIONS: the precedents are `FuzzyRegexParseException` for upstream's deliberate `error`
  rejections and `NotSupportedException` for upstream internal errors escaping
  (`UpstreamInternalErrorTests`); this one is a deliberate rejection with a non-`error` type, so
  neither precedent settles it alone.
- **Deliberately not ported**, recorded in PORTMAP with reasons: `pack_code_list` /
  `unpack_code_list` (`:22780`, `:22829` - pickling), `scan_locale_chars` (`:25824` - locale
  encoding, PORTMAP already covers it), `pattern_sizeof` (`:22515`), the deallocs.

## Verification

The node graph itself is observable only through matching, which does not exist yet. What is
observable is accept/reject: for any code list, does `re_compile` build or raise. Pin that.

- **Every corpus row builds**: all 1547 compile rows' code lists (from
  `tests/FuzzyRegex.Tests/Gaps/CompileParity/corpus.json`) must produce a node graph without
  error - upstream compiled every one of them.
- **The five reject**, pinned as permanent tests quoting the oracle output.
- **A differential accept/reject wave**: recreate the S13 recorder from its closing notes
  (`.scratch/` was cleared; the recipe is `.scratch/s13_record.py` + wave + compare + classify,
  and named lists must be sorted before upstream sees them). Generate fuzzy-test-shaped patterns
  (`{e<=1:...}` around one-char items, zero-width items, multi-char items, named lists, grapheme)
  plus a spread of ordinary patterns, and compare accept/reject against upstream. Zero
  unexplained divergences; run a negative control.
- **Structural spot checks** as unit tests where upstream's behaviour is pinnable without
  matching: group count and named-group marking, repeat records, and that `skip_one_way_branches`
  leaves a smaller graph for a pattern with a one-way branch.

## Done when

- [x] All 1547 corpus code lists build; the five patterns reject with the decided exception; both
      pinned by tests.
- [x] Accept/reject wave ran with zero unexplained divergences and a working negative control;
      counts quoted in the closing notes.
- [x] Local oracle run (`tools/run-oracle.ps1`) is green - trivially, everything `unsupported` -
      proving the harness stays wired as the engine grows.
- [x] S1104/MA0008/CA1051 measured and handled per the `.editorconfig` comment, or the duty
      explicitly handed to S16 in the closing notes because no field-exposing struct landed.
- [x] `docs/PORTMAP.md` lists every `_regex.c` symbol this slice ported or deliberately skipped,
      with line numbers; exception mapping recorded in DECISIONS.
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a `values[]` offset off by one against
      the code-list layout, a builder that reads past `end_code` on a truncated body, an int
      width that truncates `UNLIMITED` or a `Py_ssize_t`-sized value), commit.

---

## Closing notes (2026-08-31)

**What landed.** `src/FuzzyRegex/Engine/` exists: `Node.cs` (`RE_Node`, `RE_NextNode`, the
`RE_STATUS_*`/`RE_FUZZY_VAL_*`/node-flag tables, `node_matches_one_character`,
`locate_test_start`), `PatternObject.cs` (the compiler's half of `PatternObject`, its three info
records, `re_compile` and `get_required_chars`), `NodeCompiler.cs` (`RE_CompileArgs`, the twelve
bookkeeping functions, all twenty builders, `build_sequence`, `compile_to_nodes`,
`make_STRING_node`) and `Optimiser.cs` (`optimise_pattern` and its eleven parts). The seventeen
engine-only operators from `_regex.h` were added to `Parsing.Opcode` rather than to a second enum.
`FuzzyRegex`'s constructor now runs the node compiler, which is where upstream puts the last
rejection a pattern can meet. Ratchet GREEN: 5492 tests, 3612 passing (baseline was 2040 ids,
now 3606 - the six-id gap is the duplicate corpus rows DECISIONS records), 0 failing.

**Verification.**

- **Every one of the 1547 corpus compile rows builds a graph** (`Gaps/Engine/NodeGraphTests`), and
  the same test asserts the one structural property that holds for every pattern: no node with a
  single-exit `BRANCH` survives `skip_one_way_branches` plus `discard_unused_nodes`.
- **Accept/reject wave: 1,684 rows, agree 1,684, diverge 0** (`.scratch/s15_generate.py`,
  `.scratch/wave/`, `.scratch/s15_compare.py`). Fuzzy tests wrapped around fourteen one-character
  items, seventeen zero-width items, twelve multi-character items and a named list, in six
  constraint forms and under five flag sets; seventy-eight ordinary patterns across eight flag
  sets, covering every builder; 600 random-grammar rows. Upstream refuses 198 of them with
  `RuntimeError: invalid RE code` and this port refuses **exactly those 198**. Negative control:
  `--control 12` flips twelve of our verdicts and the comparer reports twelve divergences,
  bucketed and with examples.
- **Local oracle GREEN** - `agree 0  unsupported 600  diverge 0`, which is the expected shape with
  no matcher. No generator was added: STATE's rule is that a generator ahead of the engine
  produces an all-`unsupported` wave and proves nothing, and S15 lands no matching.
- **Both mutation checks ran.** Changing `Opcode.Branch` to `Opcode.Atomic` in
  `SkipOneWayBranches`'s first destination: 738 of 1566 tests fail. Making `CompileToNodes` return
  `true` on a failed build: all 5 rejection tests fail.

**Surprises.**

- **The oracle caught the behaviour change itself.** S14's
  `A_pattern_upstream_rejected_and_this_port_compiled_is_a_divergence` went red the moment the
  rejection landed, because it pinned the *gap*. It is rewritten as
  `A_pattern_upstreams_compiler_rejects_is_rejected_here_too`, now asserting agreement - and it
  still pins the `CompiledButUnmatched` + recorded-error rule with a stand-in, because that rule
  is what keeps any *other* missing rejection from hiding behind the unported matcher for the rest
  of Phase 3. This is the harness doing its job, not a test being weakened.
- **`record_subpattern_repeats_and_fuzzy_sections` is vestigial upstream.** Both call sites
  (`:23837`, `:23845`) pass a null parent, so `add_index` returns immediately every time and the
  walk's only lasting effect is a `VISITED_REP` mark nothing reads. Ported with the parameter
  intact so a release that restores the atomic and lookaround call sites still diffs onto our file.
- **Two upstream shapes are reproduced rather than corrected**, named in `NodeCompiler`'s remarks:
  several builders check `code + n > end_code` and then read `code[n]`, one word too far; and
  `build_sequence`'s `STRING` case is `if (!build_STRING(...)) return FALSE;` (`:25679`) where
  every sibling compares against `RE_ERROR_SUCCESS`, so the guard is dead and a truncated `STRING`
  would spin rather than be rejected. Neither is reachable from `PatternCompiler`, the only
  caller, whose code lists are always well-formed.
- **`build_REPEAT`'s "all done" branch is dead** (`:25129`): the extraction loop above it always
  leaves `min_count` at zero, so `a{3}` builds a repeat node with min and max both zero. Ported as
  written; the corpus rows depend on it.
- **`re_compile` does not write its masked `req_flags` back.** `:25997` stores the parser's value
  on the pattern and `:26052` masks `FULLCASE` off a *local* used only to choose the required
  string's opcode. `PatternObject.ReqFlags` therefore keeps the unmasked value.

**Analyzer findings.** Five rules fired on new code, each decided on its merits; **nothing was
suppressed to reach green**. S8969 (redundant `!`) and S1264 (`for(;;)` should be `while`) in
`LocateTestStart` were both right and the code changed. IDE0052 (`_program` never read) was right -
the field was removed and the graph discarded until S16, recorded in DECISIONS. S2178 (`|` should
be `||`) fired on `build_GROUP`'s `has_captures |= has_captures | visible_captures`; neither
operand has an effect, so `||` is the same expression and the rule is satisfied without changing
behaviour. IDE1006 wanted `_` on the private status constants, which is the convention
`FuzzyRegex._metachars` already sets, so they were renamed.
**One `.editorconfig` change, and it is not a suppression:** `csharp_indent_case_contents_when_block
= false` makes the .NET formatter agree with CSharpier about a braced `case` block, which the port
needs wherever upstream's C case declares a local. IDE0055 still fires on every other difference.

**The CA1051/S1104/MA0008 duty moves to S16, and the reason is measured, not assumed.** Two new
types could have triggered it. `Node` is a class with `internal` fields, and all three rules are
about *visible* fields, so none fires. `CompileArgs` is a struct with `internal` fields; a clean
`dotnet build src/FuzzyRegex/FuzzyRegex.csproj` under `TreatWarningsAsErrors` shows MA0008 does not
fire on it either - it has reference-typed fields, so no `StructLayoutAttribute` is wanted.
`.editorconfig`'s note therefore still stands untested against a public-field struct, and S16, which
lands the matcher's state, is where it will be.

**Review.** One blind pass, dispatched with `docs/VERIFICATION.md`'s brief and asked for
reproductions rather than prose. It reported **no defects**, having checked every builder's
`args->code` advance and read offsets, all twelve `build_FUZZY` constraint slots against the
`RE_FUZZY_VAL_*` mapping, every `add_node` call order, the per-builder `CompileArgs` copy-back
subsets, `UNLIMITED` as `uint` throughout, the status bits and shift, and the `List<T>` growth
against upstream's capacity/count pairs. It ran three further differential waves of its own -
10,760 rows against `regex` 2026.7.19, **0 verdict differences** - and confirmed no
`IndexOutOfRangeException` or `NullReferenceException` escaped the new engine code on any of them.
**No second pass:** nothing changed after the review, so there is no unreviewed delta for one to
cover.
