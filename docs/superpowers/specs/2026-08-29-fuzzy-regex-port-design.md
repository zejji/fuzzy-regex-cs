# FuzzyRegex: porting mrab-regex to .NET - design spec

Date: 2026-08-29
Status: agreed (design approved in planning session). Amended 2026-08-29 during Phase 0 and
slice S01, and 2026-08-30 at the Phase 1 checkpoint - see "Amendments" at the end; the amended
text is inline, so this file remains the single source.
Working name: **FuzzyRegex** (NuGet id, assembly and entry type; the namespace is
`Fuzzy.Text.RegularExpressions` - see amendment 8. Rename is cheap any time before first
publish)

## 1. Summary

Port [mrab-regex](https://github.com/mrabarnett/mrab-regex) (the Python `regex` PyPI package) to a
.NET 10+ class library. mrab-regex is a complete alternative regex engine whose headline feature,
fuzzy (approximate) matching with per-error-type budgets (`{e<=2}`, `{2i+2d+1s<=4}`,
`{s<=2:[a-z]}`, `BESTMATCH`, `ENHANCEMATCH`), has **no .NET equivalent** (verified 2026-08-29:
nothing on NuGet or GitHub offers pattern-level approximate matching; existing packages such as
FuzzySharp are whole-string similarity scorers only, and no TRE binding for .NET exists).

This is a full-feature port: recursion, variable-length lookbehind, POSIX leftmost-longest,
nested sets, branch reset, named lists, partial matches, capture lists, Unicode 17.0.0
properties/scripts/blocks - everything upstream does - plus fuzzy matching.

## 2. Decisions record

Agreed with the project owner on 2026-08-29:

| Decision | Choice |
|---|---|
| Scope | Full feature port (not fuzzy-only subset) |
| Engine strategy | Faithful port of upstream architecture first; optimize hot paths later behind green tests |
| Public API | Mirror System.Text.RegularExpressions shapes, plus extensions for mrab-only features |
| Parity | Semantic parity (same matches for same pattern+text, upstream Unicode tables) with .NET conventions (UTF-16 code-unit indices) |
| Distribution | Private GitHub repo now, built to full OSS discipline; flip public later. NuGet publish at 1.0 |
| v1.0 definition | All applicable upstream tests ported and passing + our gap tests + benchmark gate (>= Python regex on medians, no pathological regressions) + API docs |
| Execution process | Approach A: test-first parity ratchet, session-sized slices (details in section 8) |
| Test stack | TUnit + AwesomeAssertions (+ NSubstitute where seams exist), Microsoft.Testing.Platform runner |
| Upstream reference | Git submodule `upstream/` pinned at release SHA |

## 3. Upstream facts (evidence, gathered 2026-08-29)

Pinned submodule commit: `1760a20647f1c2ddcc025128407fe6f7edb905a1` (2026-08-12, "Support Python 3.15").
Default branch is `hg`. License: `Apache-2.0 AND CNRI-Python` (additions Apache-2.0; core derived
from CPython's `re`, CNRI Python 1.6 license). Our LICENSE/NOTICE must carry both attributions.

| Upstream file | Lines | Role |
|---|---|---|
| `src/_regex.c` | 26,655 | matching engine: bytecode VM, backtracker, fuzzy error-budget states |
| `src/_regex_unicode.c` | 31,586 | **generated** Unicode 17.0.0 tables (generator: `tools/build_regex_unicode.py`, 1,785 lines) |
| `regex/_regex_core.py` | 4,676 | pattern parser -> node tree -> optimizer -> bytecode emitter |
| `regex/_main.py` | 759 | public API layer |
| `regex/tests/test_regex.py` | 4,540 | 102 test methods, ~1,544 assertions |

Hand-port surface is therefore ~32k lines plus test translation; the 31.6k-line Unicode file is
regenerated, not hand-ported.

Release cadence (from changelog): multiple releases per month; engine/semantic bug fixes dominate
over pure Unicode data bumps (recent examples: fuzzy-matching segfault and out-of-bounds-read
fixes in 2026.7.19, fuzzy `{e<=0}` 210x slowdown fix in 2026.1.15). Upstream sync must therefore
handle logic diffs routinely - a key reason for the faithful-structure decision.

Fuzzy algorithm: backtracking matcher with error-counting states (not automaton-based), per-type
budgets checked during backtracking; `BESTMATCH` searches for the least-cost match, `ENHANCEMATCH`
improves an already-found match. Prior art in other ecosystems uses tagged-NFA edit-distance
automata (TRE, RE-flex) - relevant only as background; we port upstream's approach for parity.

## 4. Architecture

Four areas mirror upstream one-to-one so that upstream diffs map mechanically onto our files:

| Ours | Upstream source | Notes |
|---|---|---|
| `Parsing/` | `_regex_core.py` | parser, node tree, optimizer, bytecode emitter |
| `Engine/` | `_regex.c` | bytecode VM, backtracking matcher, fuzzy states |
| `Unicode/` | `_regex_unicode.c` | generated C#; `ReadOnlySpan<byte>` static-data pattern |
| root namespace `Fuzzy.Text.RegularExpressions` | `_main.py` | public API |

`docs/PORTMAP.md` records upstream symbol -> C# type/member, appended as each slice lands. This
map is what makes future upstream syncs mechanical.

**String semantics (highest-risk design point).** Upstream matches per Unicode codepoint; .NET
strings are UTF-16 code units. The engine iterates codepoints (decoding surrogate pairs inline);
all public indices and lengths are UTF-16 code units, matching built-in `Regex` conventions.
Ported tests translate expected indices wherever non-BMP characters appear. This area gets
dedicated gap tests beyond the ported suite.

**Runtime discipline.** Compiled pattern objects are immutable and thread-safe (both upstream and
.NET `Regex` promise this). Matching state lives in structs; backtracking stacks rent from
`ArrayPool`; input flows as `ReadOnlySpan<char>`. No `unsafe` before benchmarks prove the need.

**Public API sketch** (final shapes settled in the implementation plan for Phase 2):
`FuzzyRegex` class shaped like `System.Text.RegularExpressions.Regex` (constructors with options,
`IsMatch`/`Match`/`Matches`/`Replace`/`Split`, static conveniences, `MatchTimeout`), `Match`/`Group`
shapes likewise, plus mrab-only members: `Group.Captures` as full capture list, `Match.FuzzyCounts`
(substitutions, insertions, deletions), partial-match support, options flags for `BestMatch`,
`EnhanceMatch`, `Posix`, `Version0/Version1` semantics.

## 5. Testing regimen

Correctness strategy has three legs; all three exist before performance work starts.

1. **Ported suite first.** Phase 1 translates all of `test_regex.py` to TUnit before any engine
   code is written. Every ported test carries its upstream test name (and assertion index where a
   Python method fans out to many TUnit tests). Tests for unimplemented features start as
   `[Skip("needs:<capability>")]` - for example `[Skip("needs:lookbehind - variable-length
   lookbehind is not implemented")]` - naming the **capability** that will enable them.
   (Amendment 1, 2026-08-29: this originally said `[Skip("S<nn>")]`, naming the slice. That
   cannot work: Phase 1 writes ~1,544 skipped tests, but section 8 authors slices one phase
   ahead only, so the slice ids for Phases 3-5 do not exist when the tests are written. A
   capability is something Phase 1 genuinely knows, and "tests waiting on `lookbehind`: 47" is
   what a slice author actually needs from the status board. Each slice file declares the
   capability tags it delivers.)
2. **Parity ratchet.** A committed baseline (`tests/parity-baseline.json`) records the sorted
   **set of passing test ids**, not a per-area count (Amendment 5: a count lets a swap - one test
   enabled, another quietly broken - pass unnoticed, and the id set makes a slice's diff show
   exactly which tests it enabled). CI (Windows, Linux, macOS) fails if any previously-passing
   test fails, disappears from the run, or any test fails at all. A "fix"
   that breaks something cannot merge. This is the primary defence against review-loop churn and
   agent regressions.
3. **Differential oracle, from the first executing slice.** A property-based harness generates
   patterns and inputs, runs both this library and Python `regex`, and diffs results. In CI the
   oracle is **built from the pinned `upstream/` submodule** rather than installed from PyPI, so
   the ground truth is exactly the commit being ported, and the job asserts the two versions
   match (Amendment 7). Every divergence is minimized into a permanent ordinary test. The
   scheduled CI job stays scheduled - it needs Python and its runtime varies, so it is not a
   merge gate - but the harness is **stood up at the start of Phase 3 and run locally in every
   slice that touches the engine** (Amendment 10).

   Legs 1 and 2 cannot substitute for this, and that is measured rather than assumed. In the
   closest published analogue - verifying LLM-transpiled C into Rust - only **72% of transpiled
   functions were semantically equivalent to the original despite compiling and passing the
   existing tests**, ranging from 56.3% to 92.1% by codebase; differential testing against the
   reference caught the rest at 85.7-88.2% precision and 100% recall (RustAssure,
   arXiv:2510.07604). Passing upstream's own suite is evidence of parity, not proof of it.

Gap tests we add beyond the ported suite: surrogate/UTF-16 edge cases, `MatchTimeout`,
Span-based API contracts, thread-safety smoke tests, and any behaviour the oracle finds.

Per-slice workflow is TDD: un-skip the slice's tests, watch them fail, implement to green.
Status reporting is generated, never hand-maintained: a script reads skip attributes plus test
results and writes `docs/STATUS.md` (parity percentage per feature area).

## 6. Unicode pipeline

The tables are **transliterated from upstream's generated `src/_regex_unicode.c`** by a script
(`tools/transliterate-unicode.py`), not regenerated from UCD data files (Amendment 11, 2026-08-30;
this originally said "port `tools/build_regex_unicode.py` to a C# console tool"). Upstream commits
the generated C, so copying its numbers gives table parity by construction, where a second
generator would be a second 1,800-line program whose only check is comparison with the first. An
upstream Unicode bump becomes: bump the submodule, re-run the script, run the oracle. The one
piece of data upstream does not ship is the character-name table behind `\N{...}` (upstream leans
on CPython's `unicodedata`); that is generated once from the UCD 17.0.0 `UnicodeData.txt` and
`NameAliases.txt` by a script in `tools/`. Correctness is proven by oracle tests (`\p{...}`
behaviour compared against Python for every property, script and block, sampled across all
planes), not by reviewing generated output. The tables land in Phase 2 (slice S09), because the
parser consults them; see `docs/plan/2026-08-30-phase2-decisions.md`.

## 7. Repo layout and conventions

```
global.json                        # SDK pin + Microsoft.Testing.Platform test runner
Directory.Build.props              # net10.0, LangVersion latest, nullable, TreatWarningsAsErrors,
                                   # analyzer set (adapted from an earlier internal monorepo)
Directory.Packages.props           # central package management, transitive pinning
.editorconfig
src/FuzzyRegex/
src/FuzzyRegex.UnicodeGenerator/
tests/FuzzyRegex.Tests/            # ported upstream tests + gap tests
tests/FuzzyRegex.OracleTests/      # differential harness vs Python regex (dev + scheduled CI)
bench/FuzzyRegex.Benchmarks/       # BenchmarkDotNet
upstream/                          # git submodule -> mrabarnett/mrab-regex @ pinned SHA
docs/                              # this spec, ROADMAP, slice files, PORTMAP, generated STATUS
tools/                             # run-slices driver, status generator scripts
.claude/skills/                    # port-slice, port-tests, sync-upstream, benchmark
.github/workflows/                 # ci.yml (3-OS build+test+ratchet), oracle.yml (scheduled)
```

Copied from the monorepo: central package management, TUnit/AwesomeAssertions versions, analyzer
packages (build-time only, zero runtime cost), warnings-as-errors, `global.json` with the
Microsoft.Testing.Platform runner. Not copied: Husky hooks, Bitbucket CI, application-stack
packages. OSS discipline from day one: LICENSE (Apache-2.0) + NOTICE (upstream Apache-2.0 and
CNRI-Python attribution), README, CHANGELOG, XML docs on public API, semver.

## 8. Work tracking and context management

Principle (best-evidenced practice, and the pattern Anthropic's own migration kit uses):
**derive status from the repo; never maintain it as prose an agent must remember to update.**
LLM performance degrades with context length ("context rot"), and hand-synced TODO lists are the
documented failure mode for long agent projects.

- **Slice files as the work queue.** `docs/plan/slices/S07-lookaround.md` holds scope, upstream
  line references and done-criteria for one session-sized slice. Completing a slice = moving the
  file to `docs/plan/slices/done/` with closing notes appended. Listing the directory *is* the
  todo list: atomic, visible in git, impossible to half-update.
- **No duplicated status.** `docs/plan/ROADMAP.md` lists phases and points at slice files; it
  never restates their status. Parity status lives only in generated `docs/STATUS.md`.
- **Tiny hand-maintained state.** `docs/plan/STATE.md` (current slice, blockers, next action;
  30 lines max; rewritten, never appended, at each session end) and `docs/plan/DECISIONS.md`
  (append-only dated one-liners).
- **Slices are authored just-in-time**, one phase ahead only; detailed plans written months early
  rot like TODO lists do.
- **Fresh session per slice.** No long-lived orchestrator context. Each slice runs in a fresh
  Claude Code process following the `port-slice` skill: read STATE.md + roadmap + slice file +
  generated status (small, fixed context); work test-first; finish with ratchet green, commit,
  STATE.md rewritten, slice file moved.
- **Model mix.** Opus is the main agent for slice sessions; Sonnet subagents handle mechanical
  batches (test translation, table checks, searches); Fable is reserved for architecture changes,
  upstream-sync analysis and escalation when a slice has failed twice. Rationale: the ratchet, not
  the orchestrator's memory, protects quality; Fable costs ~2x Opus per token with always-on
  thinking.
- **Review discipline.** One blind reviewer pass per *unreviewed change*, with a hard evidence
  gate. The reviewer must name a concrete defect and hand over the reproduction - the command and
  its output, or a failing test. Style nits, speculative rewrites and prose rationale are out of
  scope and are rejected unread. Fix, re-run the ratchet, done.

  Three things this does **not** mean (Amendment 9):

  - **No critique loops.** Reviewer opines, code changes, reviewer opines again is the pattern
    that breaks working code, and it is banned. Automated code review is genuinely this
    imprecise: four of the techniques benchmarked in arXiv:2509.01494 scored **under 10%
    precision** (2.79-9.22%), and LLM reviewers systematically over-flag *correct* code as
    defective (arXiv:2603.00539).
  - **Repairing against execution feedback is not a critique loop**, and is not capped at one
    round. A red ratchet, a failing test or an oracle divergence is ground truth, not an opinion.
    Measured: two repair rounds against error tracebacks capture **76-95% of the total achievable
    improvement** across seven models, with no model regressing (arXiv:2604.10508). Two rounds,
    then stop and think rather than iterate.
  - **A pass covers the diff it read.** If fixing the findings adds public API, changes tooling,
    or touches anything the reviewer did not see, that delta is *unreviewed* and gets its own
    single pass. Reviewing changed-since-review code is coverage, not a second opinion.

  The operational form of all this, with a paste-ready reviewer brief, is `docs/VERIFICATION.md`.
  This section is the reasoning; that file is what an agent actually reads.

  The evidence gate is the load-bearing part, and it is what makes one pass enough. An adversarial
  refutation gate of the same shape killed **~79% of 171 candidate findings** before they were
  acted on (arXiv:2604.19049), and in agentic repair, removing execution-grounded validation
  increased unnecessary repairs by **131.7%** (arXiv:2604.10800).
- Issue-tracker tooling for agents (e.g. beads) evaluated and rejected: overkill for a solo
  project; the file-queue plus derived status covers the same need.

## 9. Autonomous operation and allowance budget

Requirement: once the plan is agreed, the port should proceed effectively autonomously, consuming
only a defined sub-portion of the owner's Claude allowance so other work continues in parallel.
Owner-facing start/pause/resume instructions and the full safeguard list live in
`docs/plan/OPERATIONS.md`.

Mechanism (exact details validated as a Phase 0 task, not assumed):

- **Driver script** `tools/run-slices.ps1`: a loop that (1) checks the budget gate, (2) launches a
  fresh `claude -p` (print-mode) process running the `port-slice` skill, (3) verifies the session
  ended with ratchet green and a commit, (4) repeats until a stop condition: budget exhausted,
  configured slice count reached, phase boundary reached, or two consecutive failures (which
  parks the slice and stops for human attention).
- **Budget gate, primary (deterministic):** `docs/plan/budget.json` configures max slice sessions
  per week and per day. Session counting is local and exact, so the project's share of allowance
  is predictable from measured tokens-per-slice (measured during Phase 0/1 and recorded).
- **Budget gate, secondary (validated 2026-08-29 - see Amendment 3):** rolling 24-hour and
  7-day token windows summed from `~/.claude/projects/**/*.jsonl`, counted globally across every
  session on the machine, plus a back-off while a recorded rate-limit reset is still in the
  future. There is no live plan-utilisation check, because no local source reports one.
- **Human checkpoints stay at phase boundaries:** the driver never crosses a phase boundary
  autonomously. The owner reviews progress, adjusts slice plans for the next phase, and restarts
  the driver.

## 10. Upstream sync (post-1.0 maintenance)

- `upstream/` submodule pins the exact upstream commit our release tracks; our release notes state
  the upstream version.
- `sync-upstream` skill: bump the submodule; read the changelog delta; `git diff old..new -- src/
  regex/`; map hunks to our files via PORTMAP.md; port one changelog entry at a time, test-first
  (port the upstream test change first where one exists). Expected steady-state cost: roughly one
  session per month.

## 11. Performance plan

Correctness gates first; optimization is Phase 7 and benchmark-driven throughout.

- BenchmarkDotNet suite: literal-heavy, class-heavy, backtracking-heavy, fuzzy (short and long
  inputs, varying error budgets), BESTMATCH workloads.
- Baselines committed as JSON: Python `regex` measured via pyperf on the same machine; built-in
  `System.Text.RegularExpressions` where features overlap.
- v1.0 gate (mechanical, checked by the benchmark suite on the same machine): for every workload
  in the suite, our median time (BenchmarkDotNet) must be <= the Python `regex` median (pyperf)
  for the equivalent operation, with one tolerance: no more than 10% of workloads may be slower,
  and none by more than 1.25x. "Workload" means each named benchmark case (pattern + input
  corpus + operation), not an average across cases - an overall-mean win cannot hide a badly
  regressed case. Workloads Python cannot express (Span APIs, etc.) are excluded from the gate.
- Optimization levers in order: allocation elimination (Span, stackalloc, ArrayPool),
  `SearchValues<char>` literal prefilters, struct layout and devirtualization of the VM dispatch,
  and only then `unsafe`. Source-generated compiled patterns (like .NET's regex source generator)
  are explicitly post-1.0: the bytecode design does not preclude them, and building them now is
  speculative.

## 12. Phasing and estimates

| Phase | Content | Sessions (est.) | Main model |
|---|---|---|---|
| 0 | Scaffolding: solution, props, CI, skills, driver script, budget-gate validation, AGENTS.md, slice files for Phase 1 | 1-2 | Opus |
| 1 | Public API surface stub, then port the full upstream test suite (all skipped initially) | 3-6 | Sonnet under Opus |
| 2 | Compile-parity corpus, then parser/compiler (`_regex_core.py`), Unicode tables, pattern-level API | 8 | Opus |
| 3 | **Differential oracle harness first**, then VM core: literals, classes, quantifiers, groups, backrefs, anchors + the Match object, substitution and the iteration API (`Matches`/`Split`/`Replace`), which no later phase claims and without which no group test can run | 11-16 | Opus |
| 4 | Advanced: lookaround, atomic/possessive, recursion, branch reset, named lists, POSIX, partial | 8-12 | Opus |
| 5 | Fuzzy matching + BESTMATCH/ENHANCEMATCH | 5-8 | Opus |
| 6 | Oracle *hardening* (broader generators, all Unicode planes) + gap tests (Unicode tables moved to Phase 2, Amendment 11) + native-AOT compatibility gate, against a named exit gate (Amendment 12) + the upstream open-issue sweep (Amendment 13); its mutation-testing gate item widens from the API layer to the engine and runs overnight (Amendment 14) | 7-12 | Opus/Sonnet |
| 7 | Benchmarks + optimization, AOT-compatible throughout (Amendment 12) | 5-10 | Opus |
| 8 | Docs, packaging, NuGet, 1.0 | 2-3 | Sonnet/Opus |
| 9 | Browser demo: Vue 3 page, engine in a Web Worker, GitHub Pages; after 1.0 (Amendment 12) | 2-3 | Opus |

(Amendment 2, 2026-08-29: Phase 1's first slice writes the public API surface as signatures only,
every member throwing. Without it the ported tests cannot compile, so "port the whole suite in
Phase 1" and "API skeleton in Phase 2" were mutually exclusive as written. Phase 2 keeps the same
scope, implementing the parser and compiler behind that surface.)

Total roughly 51-78 slice sessions; 2-4 calendar months at Premium-plan cadence. Estimates carry
+/-50% uncertainty; the generated status board makes the true rate visible within the first two
phases. Fuzzy matching is usable at the end of Phase 5, about two-thirds through.

## 13. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Codepoint vs UTF-16 semantics produce subtle divergences | Dedicated gap tests; oracle harness; indices translated systematically during test port (Phase 1 conventions in `port-tests` skill) |
| A 26.7k-line C interpreter hides coupling that resists slicing | Faithful structure keeps upstream as the reference at every step; slices ordered by upstream's own feature dependencies; escalate to Fable after two failed attempts |
| Review loops break working code | Parity ratchet in CI; hard evidence gate (reviewer hands over a reproduction, not an opinion); one pass per unreviewed change; critique loops banned, execution-feedback repair capped at two rounds (section 8) |
| Estimates wrong, allowance strain | Deterministic session budget; measured tokens-per-slice after Phase 1; phase-boundary human checkpoints allow re-planning |
| Upstream moves while we port | Submodule pin; we port a fixed SHA and sync forward post-1.0 via the sync-upstream skill |
| Generated Unicode tables wrong | Oracle property tests across all planes, not manual review |
| Python oracle unavailable in CI | Oracle job is scheduled/dev-only; ported suite + ratchet are the merge gate |
| Port passes upstream's suite but is still semantically wrong | The failure mode legs 1 and 2 cannot catch (72% semantic equivalence despite passing tests, arXiv:2510.07604). Oracle harness stood up before the first VM slice and run locally in every engine slice, not left to Phase 6 |
| Oracle is a different upstream version than the port targets, so divergences are version drift | CI builds the oracle from the pinned submodule and asserts the version; before trusting a PyPI release locally, diff the pin against it over `src/` and `regex/` and record the result (`sync-upstream`) |

## 14. References (all fetched 2026-08-29)

- Upstream: https://github.com/mrabarnett/mrab-regex (branch `hg`), https://pypi.org/project/regex/
- License facts: PyPI JSON `license_expression: "Apache-2.0 AND CNRI-Python"`; upstream LICENSE.txt
- No .NET equivalent: NuGet/GitHub survey (FuzzySharp, FuzzyString, BlueSimilarity are whole-string
  similarity only; no TRE binding for .NET found)
- Migration methodology: https://claude.com/blog/ai-code-migration and
  https://github.com/anthropics/code-migration-kit-with-claude-code ("progress is implicit in file
  system state"); Bun Zig->Rust port precedent
- Approximate-matching algorithms background: TRE tagged-NFA (Laurikari, SPIRE 2000,
  http://laurikari.net/ville/spire2000-tnfa.pdf); RE-flex FuzzyMatcher
- Context degradation: Chroma "context rot" study coverage (https://redis.io/blog/context-rot/);
  subagent-isolation guidance in Claude Code best-practice writeups


## 15. Amendments

Made during Phase 0 (2026-08-29), each because the spec as written could not be executed. The
amended text is inline above; this list is the record of what changed and why.

1. **Skip markers name a capability, not a slice** (section 5). Section 5 required
   `[Skip("S<nn>")]` while section 8 authors slices one phase ahead only, so Phase 1 would have
   had to invent slice ids for work months away. Skip reasons are now `needs:<capability>` and
   slice files declare the tags they deliver. Decided by the project owner.

2. **Phase 1 opens with a public API surface stub** (section 12). Ported tests cannot compile
   against an API that does not exist, so "port the whole suite in Phase 1" and "API skeleton in
   Phase 2" contradicted each other. Phase 1 slice S01 writes signatures only. Decided by the
   project owner.

3. **No live plan-utilisation gate exists** (section 9). Phase 0 validated all three candidate
   mechanisms and none is usable unattended:
   - `~/.claude/stats-cache.json` lagged 45 days on the day it was checked and carries no plan
     denominator;
   - a `rateLimit` record appears in the session logs only *after* a request has been rejected
     with HTTP 429 - useful as a back-off signal, useless as a headroom signal;
   - the claude.ai usage page needs an interactive browser, which an unattended `claude -p`
     driver has not got.

   Section 9 anticipated this ("if neither is, the deterministic cap alone stands"). The
   deterministic slice caps are therefore the gate, strengthened with rolling token windows
   computed from the session logs, which are exact and current.

4. **Per-slice cost is measured from `claude -p --output-format json`**, which returns `usage`,
   `total_cost_usd`, `is_error` and `permission_denials`. More reliable than scraping logs, and
   it also lets the driver notice a slice that stalled on the tool allowlist.

5. **The ratchet baseline is a set of test ids, not a count** (section 5). Strictly stronger, and
   it makes the diff of a slice self-documenting.

6. **The driver runs with a scoped tool allowlist**, not a permission bypass (section 9 left the
   mechanism open). Read/Write/Edit/Glob/Grep/TodoWrite/Skill/Task/Agent, and Bash restricted to
   `dotnet`, `git`, `pwsh` and `python`. An unattended session must not be able to run arbitrary
   commands on the machine. Decided by the project owner.

7. **The CI oracle is built from the pinned submodule, not installed from PyPI** (sections 5 and
   6). Upstream tags releases it never publishes - the original pin, 2026.8.12, is one of them -
   so PyPI is not always even an option, and a version-mismatched oracle reports drift as port
   defects. The Linux runners already have a C compiler, so this costs nothing there;
   `oracle.yml` asserts the built version equals `upstream/pyproject.toml`.

   No C compiler is installed on the Windows dev box, deliberately. Checked 2026-08-29: the diff
   from the newest published release (2026.7.19) to the pin touches only `pyproject.toml`,
   `changelog.txt`, `.github/` and the `__version__` string, leaving `src/_regex.c`,
   `src/_regex_unicode.c`, `regex/_regex_core.py` and the test suite byte-identical. The local
   PyPI oracle is therefore behaviour-identical to the pin, and 3-7 GB of MSVC Build Tools would
   buy a different version string and nothing else. `sync-upstream` says how to measure the gap
   at each sync and when installing the toolchain becomes necessary.

8. **The root namespace is `Fuzzy.Text.RegularExpressions`, not `FuzzyRegex`** (section 4). Made
   during S01, 2026-08-29. A type cannot be named after the namespace that contains it and stay
   reachable: with namespace `FuzzyRegex` and entry type `FuzzyRegex`, a consumer who writes
   `using FuzzyRegex;` then gets `error CS0118: 'FuzzyRegex' is a namespace but is used like a
   type` on `new FuzzyRegex(...)`, and `error CS0234` on `FuzzyRegex.IsMatch(...)`. Measured with
   a throwaway project referencing the built library, not reasoned about. The namespace moved
   rather than the type, mirroring `System.Text.RegularExpressions`; the assembly, the NuGet
   package and every directory keep the name `FuzzyRegex`. Test and benchmark namespaces moved
   with it, because `FuzzyRegex.Tests.*` declared the colliding namespace too. Decided by the
   project owner.

   Verified after the move: `using Fuzzy.Text.RegularExpressions;` alone gives unqualified access
   to `FuzzyRegex`, `Match`, `Group` and `FuzzyCounts`. Importing
   `System.Text.RegularExpressions` as well makes **seven** names ambiguous (CS0104), measured:
   `Match`, `Group`, `Capture`, `MatchCollection`, `GroupCollection`, `CaptureCollection` and
   `MatchEvaluator`. A single type alias therefore does not clear it; the workaround is a
   namespace alias (`using FR = Fuzzy.Text.RegularExpressions;`, then `FR.Match`) or per-type
   aliases for each name actually used. Still an explicit, diagnosable clash rather than the hard
   block it replaced, but it is not a one-liner - and it is the price of mirroring `Regex`'s type
   names, which the owner chose deliberately.

9. **Review discipline reworded: the evidence gate is what matters, not the pass count**
   (section 8). Made 2026-08-29 after the S01 review, on published evidence rather than taste.
   The original wording, "one pass, no loops", conflated two different loops. Banning the critique
   loop is right and the evidence is strong. Capping *execution-feedback repair* at one round was
   not: two rounds against error tracebacks capture 76-95% of achievable improvement across seven
   models with no model regressing (arXiv:2604.10508). The wording also let a real gap through -
   fixes made in response to a review shipped unreviewed, because "one pass" was read as one pass
   per *slice* rather than one pass per *unreviewed change*. That is how S01 committed roughly 200
   lines of new public API that no reviewer had seen.

   Two supporting rules added: the reviewer hands over a reproduction rather than prose (an
   adversarial gate of this shape killed ~79% of 171 candidate findings, arXiv:2604.19049), and
   the reviewer prompt must not ask for explanations or proposed corrections, which measurably
   *raise* misjudgement rates (arXiv:2603.00539).

   Evidence limits, recorded so nobody over-reads this: arXiv:2604.10508 tested Llama, Qwen and
   Gemini models on HumanEval-style benchmarks, not a frontier model on a 30k-line port;
   arXiv:2310.01798's self-correction result is about *reasoning* tasks, not code specifically;
   and no study found compares LLM review against symbolic or differential verification head to
   head. The direction is well supported, the magnitudes are not ours.

10. **The differential oracle moves from Phase 6 to the start of Phase 3** (sections 5 and 12).
   Made 2026-08-29. Section 5 called the oracle one of three correctness legs but scheduled it
   *after* the entire engine (Phases 2-5), leaving legs 1 and 2 to carry every engine slice alone.
   Those two legs cannot carry it: in the closest published analogue, 72% of LLM-transpiled
   functions were semantically equivalent despite compiling and passing the existing tests, and
   differential testing against the reference is what found the rest (arXiv:2510.07604). Finding a
   divergence in the slice that introduced it costs minutes; finding it in Phase 6 means
   archaeology across the whole engine.

   Cost is low: `tests/FuzzyRegex.OracleTests/` and `.github/workflows/oracle.yml` already exist
   (Phase 0), and Python `regex` is already installed locally, so the change is when the harness
   gets written, not whether the infrastructure exists. The scheduled CI job stays scheduled and
   stays off the merge path. Phase 6 keeps oracle *hardening* - broader generators, all Unicode
   planes - which is a different job from having an oracle at all.

   One caveat specific to regex engines: cross-engine differential testing is normally undermined
   by dialect divergence, since POSIX and PCRE disagree on semantics (arXiv:2603.00311). It does
   not apply here. We are not comparing dialects; we are comparing one implementation against the
   exact upstream commit it is a port of, which is the ideal case for the technique. Where the
   oracle genuinely cannot reach, that paper's metamorphic relations from Kleene algebra are the
   complement to reach for in Phase 6.

11. **Unicode tables are transliterated from upstream's generated C, land in Phase 2, and a
   character-name table is generated for `\N{...}`** (sections 6 and 12). Made 2026-08-30 at the
   Phase 1 checkpoint. Two findings forced it. First, the parser consults the Unicode data at
   fourteen call sites (`fold_case`, `get_all_cases`, `get_expand_on_folding`,
   `has_property_value`, `get_properties`): `\p{...}`, POSIX classes and every case-insensitive
   pattern change the bytecode according to those tables, so a Phase 6 delivery would have left
   most of Phase 2 unverifiable. Second, `_regex_unicode.c` is entirely generated (about 250
   data arrays, three struct arrays, 104 lookup functions of roughly twenty statement shapes),
   so reading it and writing C# gives parity by construction, whereas porting the 1,785-line
   generator gives a second program to get right and nothing a pinned port can use. `\N{name}` is
   the exception: upstream uses CPython's `unicodedata.lookup`, .NET has no character-name API,
   and 41 ported tests need it, so a name table is generated from UCD 17.0.0 (34,137 stored
   names after the algorithmic ranges are computed; roughly 200-300 KB). Options, measurements
   and the divergence from CPython 3.14's 16.0.0 `unicodedata` are in
   `docs/plan/2026-08-30-phase2-decisions.md`. Phase 2 also opens with a compile-parity corpus
   (slice S06) for the same reason Phase 3 opens with the oracle. Decided by the project owner.

12. **Native AOT is a requirement, Phase 6 gains an exit gate, and a Phase 9 browser demo is added
   after 1.0** (sections 11 and 12). Made 2026-08-31, decided by the project owner. Three changes,
   recorded as one because they were decided together and because the first is the reason the third
   is worth building.

   *Native AOT.* The library must stay AOT-compatible, enforced in two places because static
   analysis and a published binary answer different questions. Statically,
   `<IsAotCompatible>true</IsAotCompatible>` goes on `src/FuzzyRegex` now rather than at packaging
   time: on net8 and later it enables the trim, AOT and single-file analyzers, and
   `Directory.Build.props` already sets `TreatWarningsAsErrors`, so the slice that introduces a
   hazard fails its own build. It costs nothing today - measured 2026-08-31, `src/` contains no
   reflection at all (every `System.Reflection` hit was generated `obj/` assembly-info or a compiled
   binary; the only `System.Type` use is `typeof(...)` and `GetType()` inside `Equals` and
   `GetHashCode`, which is statically known and trim-safe). Dynamically, Phase 6 publishes a small
   consumer app with `PublishAot=true` in CI and asserts real matches, because static analysis
   cannot see a runtime-only failure. **This is why it could not wait for Phase 8.** The classic
   .NET regex optimization is `RegexOptions.Compiled`, which emits IL at run time through
   `Reflection.Emit` and `DynamicMethod` and has no JIT to emit into under native AOT. Phase 7 has
   to plan around that constraint rather than discover it after building the fast path, and a
   packaging phase is the worst moment to find a design problem.

   *Phase 6 exit gate.* "Sweep for coverage gaps" with no criteria produces a number nobody acts on,
   so the phase now closes against four, in descending order of value: skips remaining in the ported
   suite, which is the real coverage metric for a port and already exists (1,880 of 5,492 as of
   2026-08-31, each carrying a machine-readable `needs:` reason that `tools/PortTools.psm1`
   aggregates into `docs/STATUS.md`, giving an enumerated per-capability list of unported upstream
   behaviour); oracle waves across every generator with zero divergences, the only real ground truth
   and the only instrument that finds behaviour never tested at all; mutation testing scoped to the
   public API layer and the parse-error paths, the two places the oracle cannot reach, and the only
   instrument that answers "would a regression actually fail a test?"; and line coverage last, as a
   backstop to find a file or branch with no test at all, never as a percentage target. Phase 6 also
   pins what Phase 7 regresses against, since optimization is where silent behaviour change is
   likeliest: the benchmark baselines, and the edge cases an optimizer is tempted to special-case -
   zero-width and empty matches, anchors, `MatchTimeout`, large inputs, pathological backtracking.

   *Phase 9 browser demo.* A Vue 3 page, vendored as an ESM build so there is no build step and no
   CDN dependency, driving a .NET WebAssembly runtime hosted in a Web Worker that exposes one
   `[JSExport]` string-in, JSON-out match method; deployed to GitHub Pages. The worker is the whole
   safety design, and that reasoning is why this belongs in the spec rather than in a README task. A
   public demo invites strangers to type pathological patterns; regex matching is unbounded in the
   worst case, and this is a fuzzy engine, so approximate matching is combinatorially worse than the
   exact case. `MatchTimeout` only bounds a freeze if the engine polls the deadline in its inner
   loop, so a single missed check point turns a bounded stutter into a frozen tab - which makes the
   guarantee a property of engine correctness, exactly what a demo of an unfinished port cannot
   assume. Running in a worker makes "the page never freezes" a property of the browser's scheduler
   instead: a runaway is killed with `worker.terminate()`, with a warm spare pre-spawned so respawn
   latency is hidden, and `MatchTimeout` stays as the fast common-case exit and defence in depth,
   never as the sole net. No cross-origin isolation is needed - the worker boots an independent
   runtime and talks over `postMessage`, and COOP/COEP are required only by `WasmEnableThreads`,
   which this does not use - and that distinction is recorded because GitHub Pages cannot set custom
   response headers at all, so without it someone will later read the design as impossible on Pages.
   Blazor was the original recommendation and was dropped once the UI no longer had to be .NET. It
   is placed after 1.0 because it depends on a frozen public API and on the Phase 6 trim and AOT
   gate, and because demo polish must not delay the release; if the README has to carry a working
   "try it in your browser" link *at* 1.0, it moves into Phase 8 and Phase 8's estimate rises. The
   2-3 sessions in the table are an estimate with no measured basis. It also earns its keep as a
   test: publishing into a live WebAssembly host is a real trimming and AOT proof under a different
   runtime from the CI gate. Known risks, at the precision the evidence supports: the `wasmbrowser`
   and `wasmconsole` project templates are documented as experimental ("the developer workflow for
   the templates is evolving"), while the `[JSImport]`/`[JSExport]` interop the worker actually
   depends on has been supported since .NET 8, so expect template ergonomics to shift and the API
   surface to hold; respawn latency after `terminate()` has no published figure and none has been
   measured here, the warm spare being the documented mitigation and the compiled module being
   browser-cached so a respawn hits cache rather than re-downloading; and `[JSExport]` requires
   `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, which fails at build time, not at run time.

13. **Phase 6 sweeps upstream's open issues: triage, reproduce, fix here, report there** (sections
   5 and 12). Made 2026-08-31, decided by the project owner. Sized at 3-5 sessions on top of Phase
   6's existing 3-5, taking the phase to 6-10.

   *Why it is a separate instrument.* **The differential oracle is blind to inherited bugs by
   construction.** It compares this port against upstream, so a bug faithfully reproduced from
   upstream produces *agreement*, and agreement is what the oracle is built to report as success. A
   divergence appears only in the cases where we accidentally failed to reproduce one. No number of
   extra oracle waves changes this - the blindness is in the definition of the comparison, not in
   its coverage. The issue sweep is the only instrument that can see these, which is also why it
   sits before Phase 7: optimizing on top of behaviour already known to be wrong means measuring,
   tuning and locking in the wrong answer.

   *Sizing, measured rather than guessed.* All 79 open issues on `mrabarnett/mrab-regex` were
   triaged with `gh` on 2026-08-31: 40 are not bugs (feature requests, questions, doc and refactor
   suggestions), 24 are Python-specific and cannot exist in a C# port (packaging, wheels, type
   stubs, build toolchains), 12 are real engine bugs this port would inherit, and 3 cannot be
   decided without attempting a reproduction. The 12 fall in four groups: four 2026 memory-safety
   bugs from a fuzzing campaign (611-614), four fuzzy and BESTMATCH bugs (470, 563, 564, 596), two
   resource blowups (551, an infinite loop on a `V1` search; 554, a `MemoryError` from `fullmatch`
   where CPython's own `re` succeeds), and two singletons (367, a `partial=True` true positive
   where the lookaheads are jointly unsatisfiable; 425, branch reset with mixed named and numbered
   groups returning the wrong capture). The classification is the record; what a reproduce-fix-
   report cycle actually costs per issue is not measured, so 3-5 is an estimate whose *basis* is
   the triage count and nothing more. Questions, feature requests and Python-specific issues are
   out of scope by definition.

   *What a memory-safety bug becomes here.* Issues 611-614 are heap-buffer-overflow reads and
   writes in C, and those cannot manifest as memory corruption in a memory-safe language: the same
   defect surfaces as an `IndexOutOfRangeException` or, worse, as a quietly wrong answer. The
   *underlying logic errors* are inherited all the same - boolean precedence desyncing the group
   count from the emitted groups, a stale required-string cache position, a stale backtrack limit
   left by `(*SKIP)` inside an atomic group, and `build_GROUP()` dropping match direction for a
   group called from a lookbehind. Several are compiler-side, in code S15 and S16 have already
   ported, so they may surface before Phase 6 reaches them. That is a possibility to recognise when
   it happens, not a plan to rely on.

   *Fixing a bug upstream still has creates a permanent oracle divergence*, so the sweep needs a
   place to put one. That place is an intentional-divergence allowlist: a version-controlled,
   machine-readable manifest the oracle consults, which **reclassifies** a listed divergence as
   expected rather than skipping it silently. Each entry carries the row identity, our expected
   result, upstream's result, a reason with a durable link (the upstream issue number and our
   DECISIONS entry), and the upstream version or commit it was recorded against. This is the common
   pattern rather than an invention: web-platform-tests uses `meta/*.ini` with `expected: FAIL`,
   test262 uses per-test frontmatter, pytest uses `xfail`. PyPy's `cpython_differences` page is the
   weak variant of the same idea, prose-only and enforced by nothing but the rule that an
   undocumented difference is a bug.

   **The anti-rot ingredient is strictness.** If a listed divergence stops diverging - upstream
   fixed it, or our fix regressed - the run must fail, exactly as pytest's `xfail_strict` turns an
   unexpected pass into a failure. Chromium's TestExpectations lacks this and is documented as
   accumulating stale entries. This repository already has the shape: `tests/parity-baseline.json`
   plus `tools/check-ratchet.ps1` is a generated manifest, pinned to an upstream commit, that fails
   both on a regression and on a baselined test silently disappearing. The allowlist follows that
   convention rather than inventing a second one.

   *Reporting upstream.* Verified 2026-08-31: the maintainer merges external pull requests (7 of
   the last 20 closed PRs, February to May 2026), there is no CLA and no CONTRIBUTING.md, and crisp
   technical reports get acted on within hours - #607 and #608, both SIGSEGVs, were fixed the same
   evening - while arguments about design trade-offs stall. The reports he acted on fastest shared
   four traits, which are the template: a minimal, directly runnable reproduction with the exact
   version or commit pinned; naming the faulting function or mechanism rather than only the
   symptom; explicitly distinguishing the bug from a similar one already fixed; and proposing a
   concrete fix. **Nothing is filed upstream without the owner approving the drafted text first**,
   and `gh` stays out of the driver's allowlist in `tools/run-slices.ps1` so an unattended slice
   session cannot post on its own - a tool outside that list stalls the slice, which is the correct
   outcome for this one.

   *Licence: checked 2026-08-31, no action needed.* `NOTICE` already declares the derivative work,
   attributes mrab-regex at the pinned commit, carries upstream's own CNRI/Secret Labs statement
   and credits the Unicode Character Database, which satisfies Apache 2.0 section 4(b) and 4(c).
   Contributing a fix back changes nothing about that.

14. **The Phase 6 mutation-testing pass widens from the API layer to the engine, because overnight
   capacity was granted** (section 12). Made 2026-08-31, decided by the project owner. Sized at 1-2
   sessions on top of Phase 6's 6-10, taking the phase to 7-12.

   *This is a change of scope, not a new instrument.* Amendment 12 already put mutation testing in
   the Phase 6 exit gate as item 3, and the roadmap already parked Stryker.NET for Phase 6. Both
   deliberately confined it to **the public API layer and the parse-error paths** - "the two places
   the oracle cannot reach" - and both gave the same reason for confining it: "Stryker reruns the
   suite per mutant, so a 30k-line engine would take hours to days per run - never a merge gate."
   That reason was about machine time in a working day.

   *What changed is the constraint, not the technique.* The owner has allowed the mutation pass and
   the Phase 7 optimization slices to run overnight, with no contention for the machine. A cost that
   is unacceptable interactively is ordinary unattended, so the runtime argument no longer scopes
   the engine out. The engine is where Phase 7 will do its rewriting, so it is the part whose test
   coverage most needs measuring before that starts.

   *Why before Phase 7, restated.* Phase 7 is optimization: behaviour-preserving rewrites whose only
   safety net is the suite. Measuring holes in that net after the rewriting has begun measures the
   wrong thing. This is Amendment 13's argument for the upstream-issue sweep, applied to the tests
   rather than to the behaviour.

   *What a mutation score is worth, with its published limits.* Across 357 real faults, 230,000
   mutants and 321 KLOC in five Java programs, mutant detection correlated significantly with
   real-fault detection, and more strongly than statement coverage did. The same study bounds the
   claim: the coupling effect held for **73%** of those faults, **10%** would have needed a new or
   stronger mutation operator, and **17%** were not coupled to any mutant (Just et al., FSE 2014).
   So it is a sharper proxy than coverage, not a proof, and it does not replace either of the other
   two instruments: the oracle compares against upstream, and the per-slice negative controls test
   whether the oracle's *generators* have teeth, which is a question mutation testing never asks.

   *Tooling, checked 2026-08-31.* Stryker.NET 4.16.0 (released 2026-07-03). It can reach this suite
   only through its Microsoft Testing Platform runner, added in 4.13 and still marked preview,
   because MTP is the only runner supporting TUnit. That preview status is the delivery risk, and is
   why this stays a one-off measurement with a written verdict rather than a CI gate: `break-at`
   exists, but a preview runner is not something to block a build on.

   *Scoping still applies, overnight or not.* The engine is ~7,900 lines with ~3,100 in
   `Matcher.cs`, and the suite takes 43 seconds as of S19. Use `mutate` to bound the files,
   `mutation-level` to bound operator aggressiveness, `ignore-methods` for the seam throwers,
   `concurrency` for a machine with nothing else on it, and `since` with `with-baseline` if it is
   ever repeated. Expect equivalent mutants in quantity - a backtracking matcher is full of
   defensive guards unobservable from outside, and S19 already met one by hand when relaxing a
   `count > maxCount` check produced zero divergences because that branch is dead in practice.
   Triaging those is most of the work, which is why the deliverable is a verdict and the tests it
   justifies, not a score to chase. Google's published practice is the calibration: they abandoned
   exhaustive mutation for one mutant per covered line, scoped to the diff under review, with
   unproductive lines suppressed (Petrovic and Ivankovic, ICSE-SEIP 2018; Petrovic et al., TSE
   2021).

15. **Phase 4's content is corrected to what is actually unported, and the four "unclaimed"
    capability families are absorbed into it** (section 12; ROADMAP 2026-09-11 note; DECISIONS
    2026-09-11). At the Phase 4 checkpoint every remaining `needs:` tag was probed by removing its
    skip attributes and running the suite. Atomic, possessive, branch reset and named lists - four
    of the seven families the Phase 4 row listed - already matched: upstream has no possessive or
    `STRING_SET` opcode, both lower to constructs Phases 2-3 ported, and the skip prose was Phase
    1's description of a parser that has existed since S13. The same probe showed inline flags,
    version flags, comments and `(*FAIL)` done, leaving `(*PRUNE)`/`(*SKIP)` as the only unclaimed
    work, which Phase 4 takes. Phase 4 is therefore lookaround, conditional-with-lookaround, the
    two verbs, recursion, partial matching and POSIX, in seven slices (S27-S33) against the 8-12
    estimated - fewer because the estimate counted finished work, not because the remaining work
    got cheaper. Ninety-two tests were un-skipped in commit `8ae8607`. Every phase close from now
    on probes the board this way before writing its handover. Decided by evidence; owner to
    confirm at the Phase 4 review.

16. **No known bug ships in this port, inherited or not** (sections 5 and 12; owner decision,
    2026-09-12, wording tightened 2026-09-13 at the owner's request). Every bug conclusively
    identified is fixed before 1.0, whoever introduced it. A divergence between this port and
    upstream is judged, never merely recorded, and the standard of evidence is the one Phase 4 and
    Phase 5 actually used: the documented definition of the feature (upstream's docs and, where they
    are silent, the definitions other engines publish); a real run of a second engine where the
    construct exists there (PCRE2 and Perl for verbs, partial matching and lookaround; TRE and agrep
    for weighted fuzzy costs); a survey of comparable libraries where upstream defines nothing;
    upstream's own release history where a behaviour changed; a blind review that reproduces rather
    than reasons; and an independent verifier that did not see the first verdict. Four outcomes,
    and every judged divergence lands in exactly one:
    (a) **the port is wrong** - fixed test-first in the slice that found it, or the next one;
    (b) **upstream is wrong and the port is right** - the port's answer pinned as a permanent test,
    which Phase 7's port of upstream's start optimisations may not change, plus a ledger entry;
    (c) **both are wrong** because the port faithfully reproduces upstream's bug - fixed here, on
    Phase 6's fix list at the latest, plus a ledger entry; "draft-if-theirs" never means "leave it";
    (d) **strong but not conclusive** - a ledger entry stating what is known and what would settle
    it, and the decision is the owner's; a slice neither fixes it silently nor drops it.
    A bug upstream has that this port does not needs only (b)'s ledger entry. Nothing is filed
    upstream until everything else in the plan is done (amendment 17). Phase 4 gained S33 to apply
    this to the divergences S29, S31 and S32 had parked; the evidence is
    `docs/plan/2026-09-12-divergence-research.md`. Decided by the project owner.

17. **Phase 6 opens with an upstream sync and a bug sweep, and Phase 7 is gated on them** (sections
    10 and 12; owner decision, 2026-09-12). Before any optimisation work, the port is brought level
    with upstream's newest *release* (never its head: the oracle must stay a PyPI wheel, amendment
    7), every changelog entry since the pin is ported or recorded as not applicable, the issue
    tracker is re-triaged live, and every bug we have identified or reproduced from an issue is
    fixed here and drafted upstream for approval. Unreleased fixes found on head are ported with
    their own issue as ground truth and recorded as such. Measured at the decision: 21 commits and
    five releases behind, all substantive fixes already on the inherited-bug list. Estimate 7-12
    becomes 9-14. Decided by the project owner.

18. **A `needs:` tag names the capability a test waits on, not the feature the test is about, and
    Phase 5's fuzzy tags are re-attributed accordingly** (sections 7 and 8; S40, 2026-09-13). Phase
    5 was authored on the assumption that the nine `fuzzy-*` tags split cleanly along the same line
    as its slices, so S40 - the last of the three plain-matching slices - was written to deliver
    seven tags outright and to leave only `fuzzy-bestmatch` and `fuzzy-enhancematch` behind. That
    assumption was wrong, and the evidence is mechanical rather than a matter of judgement. With the
    `{...:test}` constraint ported, 31 test cases carrying `fuzzy-matching`, `fuzzy-budget`,
    `fuzzy-counts` and `fuzzy-changes` still failed, and **every one of the 31 failed at the engine's
    own `ENHANCEMATCH` or `BESTMATCH` seam, not on an assertion**: their patterns carry `(?e)`,
    `(?b)`, `(?be)` or `FuzzyRegexOptions.BestMatch`. A test whose pattern asks for BESTMATCH waits
    on BESTMATCH whatever upstream's test method is named after, so its tag is `fuzzy-bestmatch`.

    S40 therefore delivers `fuzzy-insertion`, `fuzzy-deletion` and `fuzzy-substitution` (S39's, and
    already skip-free), the constraint itself, and every plain row of the other four tags; the 31
    ranking-mode cases move to the two tags that describe them. Eight upstream methods that mixed
    plain and ranking rows in one `[Arguments]` fan-out were split, per the fan-out convention in
    the `port-tests` skill, so a plain row is not skipped to keep a `(?e)` sibling company. The
    board's remaining 55 skips are then exactly S41's and S42's scope, which is the number those
    slices need.

    **The general rule this settles:** when a red test is analysed to a seam that names a different
    capability, correcting the tag is the faithful record, not a dodge - the slice rule against
    retagging is against retagging *instead of* analysing. Analysing first is what makes the
    difference, and the analysis is the seam's own exception message. Phase 5's slice count is
    unchanged: nothing moved between slices, only between tags.

19. **Phase 5 gains S40a, because the 6000-row default wave found three defects and cannot finish**
    (sections 8 and 12; S40, 2026-09-13). S40's slice file asked for the default oracle wave green
    at 6000 rows a generator - 126,000 rows a seed, ten times anything run before, since S37's 6000
    was `interactions` alone and S39's default wave was 600 a generator. It does not finish.
    **Upstream never returns from `regex.search('.?x(?>a(*SKIP)z)', 'xzxa')`** - an optional leading
    item, an atomic group and `(*SKIP)` inside it, all three needed - so the recorder, which has no
    per-row timeout, stops dead. At seed 7, where the wave does complete, four rows diverge in
    `partial`, `partial-sliced` and `verbs`; **all four were proven to be HEAD's** by consuming the
    identical saved wave with HEAD's engine in a worktree. A fourth defect came from S40's blind
    review: a fuzzy section inside a lookbehind reports `FuzzyChanges` that contradict its own
    `FuzzyCounts`, and plain `(?r)` does not, so it is not the reversal itself.

    None is S40's doing and S40 fixed none of them, which is the S33/S34/S35 pattern named in
    amendment 15's discussion and predicted by the ROADMAP's Phase 5 note: verification getting
    stricter adds slices, and each addition finds a real defect. Amendment 16 is why they cannot
    just be logged. S40a takes all four plus the recorder timeout that `regex`'s own `timeout=`
    keyword makes small, and its exit gate is the 6000-row wave S40 could not run. Phase 5 becomes
    8 slices, S37-S43 plus S40a.

20. **Phase 5 gains S40b, S40c and S40d, because S40a's exit gate at three seeds found fifteen
    rows rather than four** (sections 8 and 12; S40a, 2026-09-13; written by the orchestrator
    because S40a's second session believed the spec lived outside this repository - it is this
    file). Run for the first time at three seeds, the 6000-row default wave gave 3 + 5 + 7
    diverging rows; S40 had seen four because only seed 7 ever completed. S40a's two sittings fixed
    the recorder's per-row timeout and one engine defect (a scanner carrying a slice a `(*SKIP)` had
    moved into the next match, ledger entry 5's own proposed fix), found that two of its four items
    rested on premises measurement overturned (the atomic-group hang is upstream's and already fixed
    there; the change/count contradiction is upstream's and inherited), and triaged the fifteen into
    three mechanisms: S40b, the partial pass inheriting a moved `slice_start` (a port defect, four
    rows, and S37's pinned answer re-judged with it); S40c, a partial leaking through a group call
    inside an opposite-direction lookaround (seven rows, which turned out to be the port's width
    early-out counting UTF-16 code units); S40d, the reversed carried slice in shapes no tell reached
    (two rows, and the gate itself). All three landed the same day. Phase 5 is eleven slices,
    S37-S43 plus S40a-S40d, every addition verification rather than feature, as the ROADMAP's
    Phase 5 note predicted.

21. **Phase 5 closes at eleven slices against an estimate of 5-8, and the overrun is entirely the
    oracle** (sections 8 and 12; S43, 2026-09-13). The seven authored slices delivered the whole of
    the fuzzy feature set and landed as authored; the four additions - S40a, S40b, S40c, S40d - were
    all a widened wave finding defects, and each found a real one. The ROADMAP's own Phase 5 budget
    note called this before the phase began ("its 5-8 counts fuzzy features, and the oracle will add
    slices to it"), so the estimate was wrong about scope rather than about the work: **an estimate
    that counts features cannot bound a phase whose gate is a generator.** Phase 6 opens with the
    upstream sync and the bug sweep and its own gate is oracle hardening, so read its 9-14 the same
    way and expect the gate, not the features, to set the number.

    The close itself made the point a fifth time. S43's exit gate asks for the default wave at three
    seeds AND `fuzzy`/`interactions` at a fourth seed the phase had never used; the first three went
    green after the seven judged rows were classified, and the fourth immediately produced three more
    unjudged divergences - one of them the cleanest reproduction ledger entry 8 has ever had, which
    overturned that entry's own written claim that no minimal form existed. **A seed the phase has
    not used is worth more than more rows at a seed it has**, which is VERIFICATION rule 7a one level
    up, and Phase 6's hardening slices should be scoped on that basis.
