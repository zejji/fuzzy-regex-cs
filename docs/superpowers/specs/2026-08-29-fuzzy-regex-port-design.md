# FuzzyRegex: porting mrab-regex to .NET - design spec

Date: 2026-08-29
Status: agreed (design approved in planning session). Amended 2026-08-29 during Phase 0 - see
"Amendments" at the end; the amended text is inline, so this file remains the single source.
Working name: **FuzzyRegex** (NuGet id and namespace; rename is cheap any time before first publish)

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
| root namespace | `_main.py` | public API |

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
3. **Differential oracle.** A property-based harness generates patterns and inputs, runs both this
   library and Python `regex`, and diffs results. In CI the oracle is **built from the pinned
   `upstream/` submodule** rather than installed from PyPI, so the ground truth is exactly the
   commit being ported, and the job asserts the two versions match (Amendment 7). Every divergence is minimized
   into a permanent ordinary test. Runs on demand and on a scheduled CI job, not per-commit.

Gap tests we add beyond the ported suite: surrogate/UTF-16 edge cases, `MatchTimeout`,
Span-based API contracts, thread-safety smoke tests, and any behaviour the oracle finds.

Per-slice workflow is TDD: un-skip the slice's tests, watch them fail, implement to green.
Status reporting is generated, never hand-maintained: a script reads skip attributes plus test
results and writes `docs/STATUS.md` (parity percentage per feature area).

## 6. Unicode pipeline

Port `tools/build_regex_unicode.py` to a C# console tool (`src/FuzzyRegex.UnicodeGenerator`) that
reads UCD 17.0.0 data files and emits the table sources. Correctness is proven by oracle tests
(`\p{...}` behaviour compared against Python for every property, script and block, sampled across
all planes), not by reviewing generated output. An upstream Unicode bump becomes: drop in new UCD
files, regenerate, run the oracle.

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
- **Review discipline.** One blind reviewer pass per slice. The reviewer must name a concrete
  defect with a failing test or reproduction; style nits and speculative rewrites are out of
  scope. Fix, re-run ratchet, done. No multi-pass review loops - the ratchet and oracle replace
  them.
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
| 2 | Parser/compiler (`_regex_core.py`) + API skeleton | 5-8 | Opus |
| 3 | VM core: literals, classes, quantifiers, groups, backrefs, anchors | 10-15 | Opus |
| 4 | Advanced: lookaround, atomic/possessive, recursion, branch reset, named lists, POSIX, partial | 8-12 | Opus |
| 5 | Fuzzy matching + BESTMATCH/ENHANCEMATCH | 5-8 | Opus |
| 6 | Oracle hardening + gap tests | 3-5 | Opus/Sonnet |
| 7 | Benchmarks + optimization | 5-10 | Opus |
| 8 | Docs, packaging, NuGet, 1.0 | 2-3 | Sonnet/Opus |

(Amendment 2, 2026-08-29: Phase 1's first slice writes the public API surface as signatures only,
every member throwing. Without it the ported tests cannot compile, so "port the whole suite in
Phase 1" and "API skeleton in Phase 2" were mutually exclusive as written. Phase 2 keeps the same
scope, implementing the parser and compiler behind that surface.)

Total roughly 42-69 slice sessions; 2-4 calendar months at Premium-plan cadence. Estimates carry
+/-50% uncertainty; the generated status board makes the true rate visible within the first two
phases. Fuzzy matching is usable at the end of Phase 5, about two-thirds through.

## 13. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Codepoint vs UTF-16 semantics produce subtle divergences | Dedicated gap tests; oracle harness; indices translated systematically during test port (Phase 1 conventions in `port-tests` skill) |
| A 26.7k-line C interpreter hides coupling that resists slicing | Faithful structure keeps upstream as the reference at every step; slices ordered by upstream's own feature dependencies; escalate to Fable after two failed attempts |
| Review loops break working code | Parity ratchet in CI; reviewer must prove findings with failing tests; single review pass per slice |
| Estimates wrong, allowance strain | Deterministic session budget; measured tokens-per-slice after Phase 1; phase-boundary human checkpoints allow re-planning |
| Upstream moves while we port | Submodule pin; we port a fixed SHA and sync forward post-1.0 via the sync-upstream skill |
| Generated Unicode tables wrong | Oracle property tests across all planes, not manual review |
| Python oracle unavailable in CI | Oracle job is scheduled/dev-only; ported suite + ratchet are the merge gate |
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
