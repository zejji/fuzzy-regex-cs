# Deliberate differences from upstream mrab-regex

The running list of every place this port intentionally behaves differently from the pinned
upstream release, kept so that Phase 8's user documentation is written from a record rather than
from memory. **A slice that introduces or removes a deliberate difference appends to this file in
the same commit** (port-slice skill rule, 2026-09-14). Accidental differences are not listed here:
they are oracle divergences, and they are either fixed or pinned in
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` with a ledger entry in
`docs/plan/upstream-reports/LEDGER.md`.

Each entry: what differs, why, where decided, and what a user does to get upstream's behaviour if
they can. "Owner" means the project owner decided it; the DECISIONS.md date is the trail.

## Defaults

| Difference | Why | Decided | Upstream behaviour available? |
|---|---|---|---|
| **Version 1 is the default** (nested sets and set operations; full case-folding under `IgnoreCase`). Upstream defaults to `VERSION0` for `re` compatibility. | No `re` users to protect; both V1 behaviours are the library's reason to exist. Measured 2026-09-14: zero-width handling and inline-flag scoping are already identical in V0 and V1 on 2026.9.10. | Owner, spec amendment 24, slice S50b | Yes: `FuzzyRegexOptions.Version0` or `(?V0)`. The V1 compile error for an unescaped `[` inside a set names it. |
| **No `concurrent` parameter.** Upstream's `concurrent=True` releases the GIL during a match. | The port always matches without a global lock; compiled patterns are immutable and shareable (slice S52b proves it). | Spec "runtime discipline"; amendment 23 | Not applicable. |
| **Per-call timeouts and `CancellationToken`** on every input-dependent method, in the .NET idiom (`MatchTimeout`-style and a token). Upstream has a per-call `timeout=` keyword only. | .NET callers expect both; precedent from `System.Text.RegularExpressions` and first-party libraries researched in the slice. | Owner, spec amendment 22, slice S51 | Upstream's `timeout=` maps onto the per-call value. |

## API shape

| Difference | Why | Decided | Upstream behaviour available? |
|---|---|---|---|
| **`Regex`-shaped surface**: `FuzzyRegex`, `Match`, `Group`, `Capture` with `(Index, Length)`; `Matches`/`Split`/`Replace` naming. | Idiomatic .NET; the design spec's public-API sketch. | Spec section "Public API sketch", S01 | Python names are documented as a mapping in the API docs (Phase 8). |
| **Indices are UTF-16 code units**, not codepoints. | .NET strings are UTF-16; the ported tests translate every non-BMP expectation. | Spec; DECISIONS 2026-08-30 (`Source` reads codepoints, counts `char`s) | No. Documented with an example for astral characters. |
| **`Split` spells "no limit" as `maxSplits = -1`**; upstream spells it `maxsplit=0` and reads a negative as "no splits". | .NET convention; the inversion is applied at the boundary only. | S01; `Iteration.cs` | The mapping is documented. |
| **Replacement templates speak upstream's language** (`\1`, `\g<name>`, `\n`, `\x41`, `\N{...}`), not `Regex`'s `$1`. | Fidelity to upstream's `sub` semantics; `$` is ordinary text. | DECISIONS 2026-08-29 | Not applicable; documented prominently because .NET users will expect `$1`. |
| **Exception mapping**: upstream's `IndexError("unknown group")` becomes `ArgumentException`; the C compiler's `RuntimeError("invalid RE code")` becomes `NotSupportedException`; pattern syntax errors are `FuzzyRegexParseException`. | .NET exception idiom. | DECISIONS 2026-08-31 | Not applicable; documented per method. |
| **`Match.allcaptures`, `allspans`, `groupdict`, `capturesdict`** are not on the public surface as of Phase 2. | Eleven upstream assertions use them; the port covers them through `Captures`. Re-check in Phase 8 whether any is owed. | DECISIONS 2026-08-30 | Equivalent data through `Group.Captures`. |

## Behaviour where the port answers differently on purpose

| Difference | Why | Decided | Upstream behaviour available? |
|---|---|---|---|
| **`(?e)` and `(?b)` rank candidates by fuzzy COST** (cheapest, then fewer errors, then earliest); upstream ranks by error count. | Upstream issue 470 reproduced on 2026.9.10; pre-2015.09.28 upstream used `max_cost`; TRE/agrep rank by lowest cost; the user's own cost weights are otherwise ignored by the ranking. | Owner, S41/S42, DECISIONS 2026-09-13 | No. One ported test expectation changed (test_fuzzy row 44), justified in DECISIONS. |
| **Inherited upstream bugs are fixed here**, each with a ledger entry and, from S47c, the mechanism traced in upstream's C: ledger 7 (Turkic-only `T` fold rows merged into the default tables), 9's port half (a segfault-adjacent read), 11 (fuzzy counts and change lists desynchronised, four mechanisms), 12 (BESTMATCH guard counting errors twice), 13 (BESTMATCH partial never attempted after `(*SKIP)`), 14 (runaway recursion filling memory), 5 (verb/partial doors, S48). | Owner rule 2026-09-12: no known bug is left in the port, inherited or not; spec amendment 16's outcomes (b) and (c). | DECISIONS per entry; `LEDGER.md` | No. Each is a permanent pinned test; the ledger drafts the upstream report, filed in Phase 8 only with owner approval. |
| **Not ported at all**: the functions in `docs/PORTMAP.md`'s deliberately-not-ported table (CPython object plumbing, the `concurrent` GIL dance, pickling, the pattern cache). | No .NET meaning, or covered by the runtime. | PORTMAP | Not applicable. |

## Kept identical although upstream looks odd

Listed so Phase 8 does not "fix" them: `same_match` override in ENHANCEMATCH (deliberate upstream
rework, 2015.11.5; honouring the dead check loses matches); the `better` termination test in
ENHANCEMATCH; `Split` takes no `pos`/`endpos` because upstream's does not. See DECISIONS 2026-09-13.
