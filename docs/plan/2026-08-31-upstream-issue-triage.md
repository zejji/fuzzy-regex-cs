# mrab-regex open issue triage (79 open, snapshot 2026-08-31)

Produced 2026-08-31 to size the Phase 6 upstream open-issue sweep (spec amendment 13). Every open
issue on `mrabarnett/mrab-regex` was listed with `gh issue list` and classified by hand into the
four buckets below. This is a point-in-time snapshot, not a live view: issues opened, closed or
reclassified upstream after 2026-08-31 are not reflected here.

Bucket key: A=not a bug, B=Python-specific (can't exist in C# port), C=real engine bug (open, we'd inherit), D=needs repro/human judgement.

| # | Bucket | Reason |
|---|---|---|
| 18 | A | Feature request: regex in property values |
| 99 | A | Perf/refactor suggestion (switch out of loop), not a bug |
| 185 | B | pip install failure on AWS EC2 AMI (packaging/env) |
| 215 | A | Perf question; maintainer explained short-circuit behaviour, not a bug |
| 218 | A | Request for a changelog |
| 258 | A | "GitHub?" - support/meta question |
| 279 | A | Doc request for `_regex` internals |
| 282 | A | Doc/feature request: perf comparison with `re` |
| 321 | B | Timeout granularity difference macOS vs Windows; CPU-time vs wall-time measurement, CPython-specific |
| 332 | B | Jupyter/pip upgrade error (env/packaging) |
| 334 | D | "Kernel crash" on fuzzy search; reduced repro given but unreliable across machines, last comment suggests already fixed post-#525 - needs repro attempt |
| 356 | B | Compilation fails under macOS (build/toolchain) |
| 358 | A | fuzzy_changes index overlap explained by maintainer as substitution-at-cap behaviour, not confirmed bug |
| 366 | A | Meta: tests use internal module instead of unittest.main |
| 367 | C | partial=True match returns true positive when lookahead constraints are jointly unsatisfiable ("second even prime") |
| 375 | A | Feature request: retrieve multiple captures for duplicate group names |
| 379 | B | Can't install on Debian via pip (packaging) |
| 381 | A | User error: repl / string-vs-bytes type mismatch |
| 382 | B | Install error on OSX Catalina + Xcode12 (build/env) |
| 388 | A | Request to add .gitignore |
| 389 | B | Test suite invocation failure tied to source distribution / build (2020.10.28) |
| 390 | B | Install error on Python 3.5 (packaging/version support) |
| 392 | B | Missing wheel for a release/CPython version |
| 395 | A | Internal refactor suggestion (eliminate switch), not a bug |
| 397 | D | DEFINE + recursion reported infinite loop/MemoryError; maintainer disputed the repro (invalid syntax, inconsistent patterns) - unresolved |
| 398 | A | Design discussion on Unicode property evaluation order; maintainer confirms "works as documented" |
| 400 | A | Feature request: Damerau-Levenshtein fuzzy distance |
| 406 | A | Request for changelog/similar |
| 409 | A | Doc request: mention recursive/repeated patterns in docstring |
| 410 | B | Doesn't work on docker python3.9 alpine (build/env, musl libc) |
| 413 | B | ModuleNotFoundError for `regex._regex` (build/install of C extension) |
| 416 | A | pytest misconfiguration/user error (passes unsupported args to unittest) |
| 417 | A | Feature request: weighted errors per character |
| 423 | B | Type-checking (mypy/pyright) stub gap - Python typing ecosystem issue |
| 425 | C | Branch-reset with mixed named/numbered groups yields wrong captured group value |
| 448 | A | Same pytest/unittest invocation user error as #416 |
| 450 | B | Wheel/arch mismatch on Apple M1 (packaging) |
| 454 | A | Question about flags / PCRE-vs-regex semantics when porting patterns |
| 457 | B | `repl` callback returning None: Python API-shape difference from `re` (typing/behaviour of callables) |
| 460 | A | Third-party tool (PyLint) not recognizing flags; not an engine issue |
| 461 | A | User error: invalid pattern built via string formatting |
| 462 | A | Support/output-noise question about pip install messages |
| 464 | A | Doc request: clarify Best Match (?b) flag |
| 470 | C | BESTMATCH ignores user-specified per-operation costs when choosing optimal fuzzy match |
| 471 | B | Thread-safety gap in CPython module-level `_cache`/globals (GIL-adjacent, Python module state) |
| 475 | B | Release process problem (2022.7.24 improperly released) |
| 482 | A | Duplicate of #416/#448 pytest invocation user error |
| 487 | A | Fuzzy charset test applies to both insertion+substitution by design (maintainer confirms intentional from #338) |
| 488 | B | Missing `unicodedata` module (install/env) |
| 501 | A | UX/docs suggestion for error message wording |
| 503 | A | Question: can \K \R \G (*SKIP)(*FAIL)(*PRUNE) be used |
| 505 | B | EncodingWarning triggered by Python interpreter flag (Python-version-specific) |
| 506 | A | Doc question: cross-compiling the package |
| 510 | A | Feature/process request: GitHub releases |
| 513 | A | Feature request: query min/max match length |
| 517 | A | Stale/resolved - reporter's own test case now behaves as expected on current version |
| 519 | A | Feature/process request: releases workflow |
| 544 | A | Feature request: expose AST from `_regex_core` |
| 547 | B | Incomplete install via `uv` (packaging) |
| 551 | C | Infinite loop on `V1` search for a specific pattern+flags+text; maintainer confirmed "yes, it's a bug" |
| 554 | C | `fullmatch` on a long string with a repeating capture group raises MemoryError where `re` succeeds (excessive memory use) |
| 557 | A | Feature request: add a trace mode |
| 563 | C | `\m` anchor combined with a fuzzy quantifier fails to match at the very start of the string; maintainer: "looks like a bug" |
| 564 | C | Loosening a fuzzy edit-distance limit (`<=1` to `<=2`) reduces the matches returned; maintainer: "looks like a bug" |
| 566 | B | Type stub return-type annotation issue (`tuple[list[AnyStr]]`) - Python typing ecosystem |
| 569 | B | Install failure on win-arm64 (packaging/wheels) |
| 571 | A | `\w` vs `re` on category `No` characters (e.g. superscript 2); resolved as regex following Unicode TR18 spec, not a bug |
| 578 | A | Feature request: pre-initialise backreferences for partial matching |
| 587 | B | CI/PyPI trusted-publishing migration (release infra) |
| 588 | A | Meta/support question about the module's audience |
| 589 | D | `fullmatch(partial=True)` with negative lookahead: reporter and maintainer/commenters disagree on expected semantics; unresolved whether it's a bug or correct boundary behaviour |
| 590 | B | Source distribution missing `setup.py` (packaging) |
| 596 | C | `{e<=0}` (a no-op fuzzy constraint) is not elided and causes ~210x slowdown vs the equivalent non-fuzzy pattern |
| 609 | B | Python 3.15 support / cibuildwheel compatibility |
| 610 | A | Doc request: add install instructions to README |
| 611 | C | Boolean-precedence bug in `LookAroundConditional.is_empty()` desyncs group count from emitted groups, causing a heap-buffer-overflow WRITE at compile time (also MemoryError at search time) |
| 612 | C | Stale required-string cache position combined with BESTMATCH+POSIX narrowing causes `count_one()` size underflow -> heap-buffer-overflow READ |
| 613 | C | `(*SKIP)` inside an atomic group leaves a stale backtrack limit; a specialised IGNORECASE scan loop stops only on `pos == limit` and walks off the buffer -> heap-buffer-overflow READ |
| 614 | C | `build_GROUP()` fails to propagate match direction into a group called from a lookbehind, producing a forward-stepping reverse body that reads past the buffer -> heap-buffer-overflow READ |

Totals: A=40, B=24, C=12, D=3 (sum 79).
