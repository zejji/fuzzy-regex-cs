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

- [x] `tools/run-oracle.ps1` runs record + consume + verdict in one command and exits nonzero on
      any divergence.
- [x] The three verifications above pass, with output quoted in the closing notes.
- [x] The version policy is enforced and recorded in the output header; oracle.yml runs the real
      harness with the stale phase-6 comment gone.
- [x] `docs/PORTMAP.md` gains a short harness section (what it covers, where the seam is).
- [x] Ratchet GREEN, baseline updated, blind review (hunt: a codepoint-to-UTF-16 translation that
      indexes by `char` instead of by rune, a named list handed to the oracle unsorted, a
      divergence that a crash in the C# consumer would silently swallow), commit.

## Closing notes

Landed: `tools/record-oracle.py`, `tools/run-oracle.ps1`, three files in
`tests/FuzzyRegex.OracleTests/`, the real oracle.yml steps, and a PORTMAP section. Five tests in
the consumer project; the ported suite is untouched, so the ratchet reads exactly as S13 left it
(3926 total, 2046 passing, nothing failing).

**The four verifications.** Quoted from the runs:

- Oracle vs oracle: `deterministic: two runs of seed 20260831 produced byte-identical waves`.
- Stub engine, 1000 rows: `agree 0  unsupported 1000  diverge 0  of 1000 rows`, no crash.
- Negative control: `Corrupting_a_recorded_row_is_reported_as_a_divergence` corrupts three rows in
  the three shapes a real defect takes (a span shifted by one, a group dropped, a no-match reported
  as a match) and requires exactly three divergences, each naming its row - with an honest-engine
  half first, so the test cannot pass on a comparator that reports everything.
- Non-BMP round trip, from a real wave: pattern `b<grin>` over subject `<grin><grin>bcb<grin><frak-U>`
  records `"codepointSpan": [4, 6]` and `"index": 6, "length": 3`. Both numbers move, so the
  translation is live rather than a pass-through.

**One correction to the slice as written.** It asks for "three verifications" and lists four; all
four were done and the box is ticked against the four.

**What the harness can already find, with no matcher.** A pattern upstream rejects and this port
compiles is a divergence decided entirely at compile time, so it is reported as one. That is S13's
five deferred patterns - `(?:abc){e<=1:\b}` records `RuntimeError: invalid RE code` - and it means
**any generator emitting fuzzy patterns before S15 lands `re_compile` will legitimately turn the
wave red.** Today's two generators emit literals only, so the wave is clean.

**Analyzers: five findings, all obeyed, none disapplied.** S3928/MA0015 wanted the `paramName`
passed to a stand-in `ArgumentOutOfRangeException` to name a real parameter, and CA2208 wanted its
message not to repeat that name; both are right, and the fix was to build those exceptions in small
helpers that do take the parameter (`VersionRejection(int version)`, `MatcherCrash(int index)`), so
the test asserts against the message .NET really produces rather than an imitation. CA2201 refused a
constructed `IndexOutOfRangeException`; also right, and the file already had the answer - build the
`ErrorOutcome` directly, as the pre-existing assertion on that type does. S3981 and CS-level dead
code caught two attempts to *neuter* a check while proving it load-bearing, which is the analyzers
working; the revert-and-observe was done by deleting the guard instead.

**Review.** Three blind passes, because the second pass found a defect in the first pass's fix and
the third was a first pass over code no reviewer had seen. Raised 3 + 2 + 2 = 7 findings; **all 7
reproduced and all 7 fixed** - unusually high, and worth reading as a property of the subject: a
harness has no ported behaviour to check it against, so a reviewer reading it line by line is the
only ground truth there is.

- Pass 1 (whole diff): the `--self-check` per-generator-seed guard could not observe the regression
  it named, because it recorded one generator on both sides; the index translation had no automated
  check at all (deleting `offsets[len(subject)] = units` left every guard green); and `Compare`
  scored any allow-listed exception as agreement for a non-`regex.error` rejection, so an
  `ArgumentOutOfRangeException` crash was parity. The third was confirmed against a real build
  before fixing, and again after: `to be OracleVerdict.Diverge ... but found OracleVerdict.Agree`.
- Pass 2 (the fixes only): comparing the *message* to fix that, as `CompileParityTests` does,
  produces false divergences - `regex.compile('a', V0|V1)` raises `KeyError: regex.V0|V1` while
  this port raises `ArgumentOutOfRangeException` whose Message .NET decorates with the parameter
  name and value, so the texts can never match. Both sides measured before the rule was replaced
  by the phase rule. It also showed the test never exercised the allow-list, which the added
  compile-phase `IndexOutOfRangeException` case now does.
- Pass 3 (the phase mechanism only): a seam hit *after* a successful compile was reported
  `unsupported`, which hid every missing rejection behind the unported matcher for the whole of
  phase 3 - the `CompiledButUnmatched` verdict above. Its second finding is a real coverage gap
  left open on purpose and marked at the line: the `whileMatching: true` attribution is unreachable
  until a matcher exists, so only `Compare`'s side of that flag is tested. **S16 must pin it.**

Each fix was proven load-bearing by deleting the guard and observing the specific assertion fail,
not by re-running the reviewer. The two new recorder guards were proven the same way, by mutating a
copy of the recorder (`.scratch/s14_guard_check.py`): `caught 'drop the exclusive-end offset'` and
`caught 'share one RNG stream across generators'`.

**For the next slice.** `tools/run-oracle.ps1` is now rule 7's command; run it before committing
anything that touches the engine. Add a generator per slice rather than reaching for a general one:
the wave is only as good as the rows, and a generator emitting constructs no slice has ported yet
produces 100% `unsupported` and tells nobody anything.
