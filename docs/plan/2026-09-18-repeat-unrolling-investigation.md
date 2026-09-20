# Why compiling `((a{1000}){1000}){1000}` exhausts memory, and what to do about it

Investigation of 2026-09-18 (Fable subagent, read-only, all probes under `DOTNET_GCHeapHardLimit`
= 1 GB and `timeout 120`), prompted by a probe that grew past 15 GB and crashed the owner's
machine. Paths are relative to the repo root; NC = `src/FuzzyRegex/Engine/NodeCompiler.cs`,
M = `src/FuzzyRegex/Engine/Matcher.cs`.

## 1. What the unrolling buys

`BuildRepeat` (NC:1402-1449) builds the repeat body `minCount` times whenever `minCount > 0` and the
repeat is not `{1,1}`, appending each copy to the node graph, then subtracts the copies from
`minCount` (NC:1436) so the repeat node it finally emits (NC:1463-1473) always has a minimum of 0.
Upstream `build_REPEAT` (`upstream/src/_regex.c:25166-25197`) is line for line the same. The
comment says "if it contains a repeat" but nothing conditions on that: even `a{1000}` is unrolled
(1005 nodes measured).

Upstream's reason: changelog 2018.11.22, "Hg issue 304: ... moves the minimum number of repeats
out of a repeat if it contains a repeat. This allows a repeat guard to be put back, which reduces
the chance of catastrophic backtracking." The repeat guard is keyed on (repeat index, text
position) only (M:8649, M:9104, consulted at M:5434 and M:6157). That memo is sound only if "the
tail fails from position P" does not depend on the iteration count, which holds only when the
minimum is 0. Unrolling makes it 0 for every repeat node.

Semantically the unrolling is not required: the matcher still carries full minimum-count handling
(M:6028, M:6151, M:6180, M:8646, M:8762, M:8784), dead since 2018 because the minimum is always 0.
It is an optimisation that the guard's correctness now depends on.

## 2. Growth curve (the port)

Each pattern in its own process; managed bytes retained after compile and node count:

| pattern | ms | retained | nodes | bytes per body copy |
|---|---|---|---|---|
| `a{1000}` | 43 | 0.3 MB | 1,005 | 262 |
| `(a{100}){100}` | 71 | 2.6 MB | 10,509 | 271 |
| `(a{1000}){100}` | 232 | 24.5 MB | 101,409 | 256 |
| `(a{1000}){1000}` | 2,299 | 238 MB | 1,005,009 | 248 |
| `((a{50}){50}){50}` | 259 | 35 MB | 140,663 | 296 |
| `((a{100}){100}){100}` | 2,783 | 267 MB | 1,061,313 | 264 |
| `((a{150}){150}){150}` | 9,028 | OutOfMemory at the 1 GB cap | | |

Linear in the product of the counts, about 250 bytes per `Node` (`Engine/Node.cs:59`), 1.0 to 1.7
nodes per body copy, working set about twice the managed size, about 2.5 microseconds per copy.
Upstream Python `regex` 2026.9.10 on the same machine: `(a{1000}){1000}` 247 MB / 5.9 s,
`((a{100}){100}){100}` 275 MB / 7.6 s, `((a{150}){150}){150}` 877 MB / 22 s. Inherited behaviour,
not a porting bug. Python `re` and .NET `System.Text.RegularExpressions` compile the cubed pattern
in milliseconds with loop opcodes; .NET's `NonBacktracking` refuses it with a `NotSupportedException`
naming an estimated 1,000,000,001 nodes against a 10,000 limit.

## 3. Options

1. **A node budget in the compiler (chosen, S56b).** Count at `CreateNode` (NC:205-218, the single
   point where `pattern.NodeList.Add` happens); throw `FuzzyRegexParseException` when the budget is
   exceeded; default about 1,000,000 nodes; configurable. Exact parity for every pattern upstream
   compiles within about 250 MB; the corpus's largest count is `{65535}`. One DIVERGENCES row.
2. **Emit counted repeats instead of unrolling.** Flat memory and identical results in principle,
   but the position-keyed tail guard is unsound when the minimum is above 0, which is the very bug
   the 2018 change fixed. Needs a guard redesign (key by count, or disable for counted repeats),
   revalidation of the dead matcher branches, and changes backtracking behaviour that the oracle
   cannot see because it compares results, not time. Structural divergence in the hottest path,
   reconciled at every upstream sync. Parked as a post-1.0 candidate, to be measured under Phase 7
   benchmarks.
3. **Go-style parse-time rule** (reject counts over 1000, nested product over 1000). Cheapest, but
   rejects patterns upstream and the corpus accept. Not adopted.

Other engines, for reference: RE2 rejects counts over 1000 and has an 8 MiB program budget; PCRE2
caps counts at 65535 and compiled size at about 64 K code units; Rust `regex` has a 10 MiB size
limit; Go rejects counts over 1000 with a nested-product rule. Security guidance (OWASP) covers
untrusted subjects, not untrusted patterns; the engine constants above are the only industry
numbers.

## 4. Other input-proportional compile allocations

All linear in pattern length or in caller-supplied data: sets (one node per member or range,
NC:1612), strings (one node with n values, NC:185), groups (`GroupInfoList`, `PatternObject.cs:121`),
named lists `\L<name>` (caller-supplied dictionary, `PatternObject.cs:103`), fuzzy constraint tables
(`RepeatInfoList`, `PatternObject.cs:127`). Width arithmetic saturates (`Widths.Multiply`). The
counted-repeat unrolling is the only super-linear allocation, so the budget plus the existing match
timeout is what server-side use on untrusted patterns needs from this library.

## Browser note (for S71)

A .NET WebAssembly worker's heap ceiling is `EmccMaximumHeapSize` (default 2 GiB; Microsoft's own
guidance lowers it for mobile). Set it explicitly (256 to 512 MiB) so a runaway compile fails as a
contained error inside the worker rather than a tab crash, and keep the terminate watchdog for the
runaway-match case. The compile budget makes both rare.
