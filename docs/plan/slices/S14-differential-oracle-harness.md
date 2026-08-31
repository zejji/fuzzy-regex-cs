---
slice: S14
phase: 3
title: Differential oracle harness
delivers: []
---

# S14 - Differential oracle harness

Phase 3 opens with the oracle, not the VM (ROADMAP; design spec amendment 10). From this slice
on, VERIFICATION.md rule 7 applies: no slice that touches the engine commits without a local
oracle run. This slice builds that harness and proves it can discriminate, before there is any
engine for it to test.

## What the harness is

A generative differential tester for *matching*, the analogue of what the S06 corpus is for
*compiling*: run the same (pattern, flags, subject, operation) rows through Python `regex` and
through this port, and diff the public results. The corpus pattern to copy is
`tools/record-compile-corpus.py` + `tests/FuzzyRegex.Tests/Gaps/CompileParity/` - recorder in
Python, consumer in C# - except the oracle's rows are generated per wave from a seed rather than
committed, and only minimised divergences become permanent tests.

## Scope

- **`tools/record-oracle.py`**: given a wave spec (generator name + seed + count, or an explicit
  row file), runs the oracle over each row and writes JSONL: pattern, flags, kwargs (named lists
  **sorted** - DECISIONS 2026-08-31, the `_canonical_kwargs` lesson: 58 false divergences), the
  subject, the operation (`search`, `match`, `fullmatch`), and the outcome - no match, or the
  match's span plus every group's span and captures, or the exception type and message.
- **Index translation happens in the recorder.** Python reports codepoint `(start, end)`; the
  recorder converts each to UTF-16 `(Index, Length)` against the subject it has in hand, so the
  file holds the values our public API must return and the C# side compares verbatim. This is one
  of the exactly two places the span convention is enforced (DECISIONS 2026-08-31); the other is
  `Match`/`Group`'s accessors (S16). A slip at either shows as a divergence, not an agreement.
- **The C# consumer** in `tests/FuzzyRegex.OracleTests/` (the empty project from Phase 0): read
  the JSONL, run `FuzzyRegex`, report per row `agree`, `diverge` (with both sides' values), or
  `unsupported` (the engine threw a `needs:` seam exception or `NotImplementedException`).
  Unsupported is informational; any `diverge` fails the run. Write divergences to
  `TestResults/` so oracle.yml's existing upload step finds them.
- **`tools/run-oracle.ps1`**: one command for record + consume + verdict, exported through
  `PortTools.psm1` conventions if that is where the other check scripts live. This is what every
  later engine slice runs before committing.
- **Oracle version policy**: the recorder asserts `regex.__version__` is either the pinned
  `2026.8.12` (CI builds it from `upstream/`) or the PyPI `2026.7.19` a dev machine gets from
  `pip install regex`, and records which in the output header. Upstream never published 2026.8.12
  to PyPI, and the changelog delta between the two is the single line "Support Python 3.15."
  (`upstream/changelog.txt`, verified 2026-08-31) - behaviourally empty. Anything else fails the
  run: a silently mismatched oracle produces divergences that are version drift, not defects.
- **oracle.yml**: replace the placeholder "Run the oracle" step and its stale comment ("the
  harness itself arrives in phase 6") with the real harness; drop `--ignore-exit-code 8` once a
  test actually runs. The scheduled job stays off the merge path (design spec amendment 7).
- **First generators**, deliberately simple - S16 is their first real customer: literal patterns
  (escaped random strings) and literal-plus-dot patterns over short random subjects, ASCII and
  non-BMP mixed, so the surrogate translation is exercised from day one.
- **Minimisation workflow**, documented in the harness's README section or script header: shrink
  a diverging row by hand or by bisection to the smallest pattern/subject pair, then pin it as an
  ordinary test in `tests/FuzzyRegex.Tests/Gaps/` with the oracle output quoted. Rule 7 requires
  every divergence to end as a permanent test.

## Verification

Nothing can match yet, so the harness is verified against itself:

- **Oracle vs oracle**: run the recorder twice (different `PYTHONHASHSEED`) and compare - the
  record must be deterministic, like the corpus recorder's `--verify-determinism`.
- **Stub engine**: run the consumer against the current engine (every match method throws). Every
  row must report `unsupported`; zero `diverge`; the run must not crash.
- **Negative control**: corrupt three recorded rows (a span off by one, a no-match flipped, a
  group span dropped) and the consumer must report exactly three divergences, naming them.
- **Non-BMP round trip**: a subject containing an astral character must record UTF-16 indices
  that differ from Python's codepoint indices, and the row must say so - quote one in the closing
  notes as proof the translation layer is live.

## Done when

- [ ] `tools/run-oracle.ps1` runs record + consume + verdict in one command and exits nonzero on
      any divergence.
- [ ] The three verifications above pass, with output quoted in the closing notes.
- [ ] The version policy is enforced and recorded in the output header; oracle.yml runs the real
      harness with the stale phase-6 comment gone.
- [ ] `docs/PORTMAP.md` gains a short harness section (what it covers, where the seam is).
- [ ] Ratchet GREEN, baseline updated, blind review (hunt: a codepoint-to-UTF-16 translation that
      indexes by `char` instead of by rune, a named list handed to the oracle unsorted, a
      divergence that a crash in the C# consumer would silently swallow), commit.
