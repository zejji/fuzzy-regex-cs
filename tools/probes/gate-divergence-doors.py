r"""Run every judged family's own control over every diverging row of an oracle gate run.

`gate-divergence-triage.py` says WHAT diverged; this says what each row answers when the questions
the existing entries in `tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` are judged on are put
to it. It renders no verdict. It is the batch form of the per-family probes
(`upstream-skip-carried-slice-doors.py`, `upstream-search-start-whole-region-partial.py`,
`upstream-partial-retry-slice-restore.py`), written because the 6000-row gate produces divergences
by the dozen and asking each one by hand is how a sitting runs out before the rows do::

    pwsh -File tools/run-oracle.ps1 -Count 6000       # writes report-<seed>.txt and wave-<seed>.jsonl
    python tools/probes/gate-divergence-doors.py      # reads both, asks every door

NOTHING IS TRANSCRIBED. The row comes out of `wave-<seed>.jsonl` and this port's answer out of
`report-<seed>.txt`, so a row cannot be mis-copied and the probe cannot drift from the run. The wave
carries a HEADER line, so wave line number is row number PLUS ONE - the one off-by-one here that
would silently ask every question about the wrong row.

THE DOORS, and which family each belongs to:

  as drawn / (*SKIP)->(*PRUNE) / verb deleted
      `(*PRUNE)` prunes exactly the backtracking `(*SKIP)` prunes; the only extra thing `(*SKIP)`
      does is move a slice bound (`upstream/src/_regex.c:14553` reversed, `:14555` forwards). A
      difference between those two lines is about the bound, not about what the pattern means. Not a
      judgement on its own - on a SCAN the two are documented to differ.

  no partial
      Family 1's whole control (`partial-retry-carried-slice-forward`): a `partial=True` call
      answering a match with the partial flag CLEAR, where the same call without the flag answers
      None, is upstream contradicting itself. `partial=True` is documented to ALSO allow a partial.

  match over upstream's own span
      `search-start-partial`'s control (S37, S43, S52): when upstream's partial covers the whole
      searched region, ask its own anchored `match` over that very span. None there means the
      prefilter answered, not the engine.

  anchor sweep
      The rest of that control: the leftmost `pos` (forward) or `endpos` (reversed) at which
      upstream's own anchored partial answers at all. A reversed match anchors at the END, so the
      bound that moves is `endpos`.

  stepwise
      `overlapped-skip-*` and `skip-carried-slice-on-a-scan-with-no-walk` (S34, S52): upstream's own
      scan taken one match at a time, each step a fresh attempt, so no bound a previous match's
      `(*SKIP)` moved is still moved. SOUND ONLY WHERE THE RECORDER WOULD RECORD IT - it is printed
      with its caveats, not as a verdict, and it is NOT a control for a row whose pattern reads the
      end of the subject, because an explicit `endpos` sets `slice_end` legitimately.

  without (?b) / without (?e) / without either
      `bestmatch-loses-a-candidate`'s door, and the one its own recorded discriminator cannot open.
      That entry keys on `bestmatchFreeOutcome`, upstream's answer with `(?b)` deleted - which is
      the same whether `(?b)` alone or the pair `(?b)(?e)` destroyed the match. Deleting them one at
      a time says which, and on the two rows S52 sitting 8 judged into that entry it is `(?b)`:
      the `(?e)`-deleted line is still None.

  $ positions
      `end-of-line-reads-a-skip-moved-slice` (S52): `try_match_END_OF_LINE` (`:7110`) is the one
      edge predicate of eight that reads `slice_end` rather than a text bound. A span ending where
      upstream's own `$` is false is that entry's whole signature.

Written by S52 sitting 8 against regex 2026.9.10. Pass seeds as arguments to read an older run.
"""

import datetime
import json
import re
import sys
from pathlib import Path

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

REPO_ROOT = Path(__file__).resolve().parents[2]
REPORTS = REPO_ROOT / "TestResults" / "oracle"
HEAD = re.compile(r"^DIVERGE row (\d+) \((\S+)\) (\S+) flags=(\S+)")

# `verbs` and `partial-sliced` are recorded with upstream's required-string prefilter neutralised
# (PREFILTER_FREE_GENERATORS, tools/record-oracle.py:325), so a probe has to ask the same way or it
# is not reproducing the row at all.
PREFILTER_FREE = ("verbs", "partial-sliced")
_REQ_OFFSET_ARG, _REQ_CHARS_ARG = 7, 8
_inner = regex._regex.compile
REVERSE = 0x400

# Upstream's POSIX flag bit (`regex.P`), read off the COMPILED pattern because an inline `(?p)`
# never reaches the row's own flags - `tools/probes/upstream-posix-flag-is-visible-on-compiled.py`.
# Spelled out here for the same reason `record-oracle.py:342` spells it out.
POSIX_FLAG = 0x10000

# Every upstream call here carries a timeout, because AN ABLATION IS NOT THE DRAWN ROW. Deleting a
# `(*SKIP)` deletes the pruning that made the drawn pattern cheap, and on a `(?b)` best-match row the
# verb-free spelling can run for minutes where the row itself answers instantly (seed 7 row 76160 is
# one). A hung ablation reports nothing about any of the other rows; `TIMED OUT` reports about one.
CALL_TIMEOUT = 5.0


def default_seeds() -> list[int]:
    """The seeds `tools/run-oracle.ps1` runs by default; the third is the RUN DATE (`:243`)."""
    return [7, 4242, int(datetime.date.today().strftime("%Y%m%d"))]


def _without_required_string(*args):
    args = list(args)
    args[_REQ_OFFSET_ARG] = -1
    args[_REQ_CHARS_ARG] = None
    return _inner(*args)


def compile_row(row: dict, pattern: str | None = None):
    pattern = row["pattern"] if pattern is None else pattern
    lists = row.get("namedLists") or {}
    if row.get("generator", "rows") not in PREFILTER_FREE:
        return regex.compile(pattern, row.get("flags", 0), cache_pattern=False, **lists)
    regex._regex.compile = _without_required_string
    try:
        return regex.compile(pattern, row.get("flags", 0), cache_pattern=False, **lists)
    finally:
        regex._regex.compile = _inner


def describe(m) -> str:
    if m is None:
        return "None"
    bits = [f"{m.span()}"]
    for n in range(1, m.re.groups + 1):
        bits.append(f"g{n}={m.span(n)}" if m.span(n) != (-1, -1) else f"g{n}=unset")
    bits.append("PARTIAL" if m.partial else "not-partial")
    if any(m.fuzzy_counts):
        # NEVER `m.fuzzy_changes` ON A POSIX MATCH THAT SPENT AN ERROR. It is an access violation
        # that kills this process (0xC0000005 on Windows, SIGSEGV under Git Bash) rather than an
        # exception, so `answer`'s `except` cannot see it and ONE such row takes the whole run with
        # it - which is what it did to S52's 37-row triage, dying inside row 32 of 37 after
        # completing 31, so rows 32 to 37 were never asked.
        # Ledger entry 9; the same guard and the same reason as `record-oracle.py:1019`, keyed off
        # POSIX rather than off the spent error because nothing in the pattern or the subject
        # predicts one. The counts on the same match answer correctly and are kept.
        changes = ("unavailable upstream (POSIX)" if m.re.flags & POSIX_FLAG
                   else str(m.fuzzy_changes))
        bits.append(f"counts={m.fuzzy_counts} changes={changes}")
    return " ".join(bits)


def slice_of(row: dict) -> tuple[int, int]:
    """The row's own `pos`/`endpos` IN CODEPOINTS, which is the unit upstream counts in.

    `partial-sliced` draws a slice and the recorder writes it twice - `pos`/`endpos` in UTF-16 for
    this port and `codepointSlice` for upstream. Asking the whole subject instead is asking a
    DIFFERENT QUESTION: on seed 20260915 row 105880 the sliceless call answers (9, 10) where the row
    itself is (7, 7). The first draft of this probe made exactly that mistake on all four
    `partial-sliced` rows of the gate.
    """
    sliced = row.get("codepointSlice")
    if sliced:
        return tuple(sliced)
    # A HAND-WRITTEN `--rows` row has no `codepointSlice`; its slice is `pos`/`endpos`, and those are
    # in CODEPOINTS, which is the unit `record-oracle.py` reads them in (`:599`). Falling through to
    # the whole subject instead asks a different question - the same mis-ask the recorded rows had.
    pos, endpos = row.get("pos"), row.get("endpos")
    if pos is None and endpos is None:
        return (0, len(row["subject"]))
    return (0 if pos is None else pos, len(row["subject"]) if endpos is None else endpos)


def answer(row: dict, pattern: str | None = None) -> str:
    """Upstream's answer to the row's own operation, optionally on an ablated pattern."""
    try:
        compiled = compile_row(row, pattern)
        subject, operation = row["subject"], row["operation"]
        partial = bool(row.get("partial"))
        if operation in ("match", "search", "fullmatch"):
            call = getattr(compiled, operation)
            lo, hi = slice_of(row)
            return describe(call(subject, lo, hi, partial=True, timeout=CALL_TIMEOUT) if partial
                            else call(subject, lo, hi, timeout=CALL_TIMEOUT))
        if operation in ("finditer", "finditer-overlapped"):
            found = list(compiled.finditer(subject, overlapped=operation.endswith("overlapped"),
                                           timeout=CALL_TIMEOUT))
            return f"{len(found)} | " + " || ".join(describe(m) for m in found)
        if operation == "split":
            parts = compiled.split(subject, row.get("count", 0) or 0, timeout=CALL_TIMEOUT)
            return f"{len(parts)} | " + " ".join(repr(p) for p in parts)
        if operation == "sub":
            return repr(compiled.subn(row["template"], subject, count=row.get("count", 0) or 0,
                                      timeout=CALL_TIMEOUT))
        if operation == "subf":
            return repr(compiled.subfn(row["template"], subject, count=row.get("count", 0) or 0,
                                       timeout=CALL_TIMEOUT))
        return f"unhandled operation {operation}"
    except Exception as e:  # noqa: BLE001 - an exception IS upstream's answer on some rows
        return f"{type(e).__name__}: {e}"


def sweep(row: dict) -> str:
    """The leftmost anchored partial upstream answers, on the bound a reversed match actually moves.

    Forward: vary `pos`, since `match` anchors at the start. Reversed: vary `endpos`, since it
    anchors at the END and `pos` cannot move the anchor at all.
    """
    compiled = compile_row(row)
    subject = row["subject"]
    reverse = "(?r" in row["pattern"] or bool(row.get("flags", 0) & REVERSE)
    lo, hi = slice_of(row)
    hits = []
    for p in range(lo, hi + 1):
        try:
            m = (compiled.match(subject, lo, p, partial=True, timeout=CALL_TIMEOUT) if reverse
                 else compiled.match(subject, p, hi, partial=True, timeout=CALL_TIMEOUT))
        except Exception as e:  # noqa: BLE001
            return f"{type(e).__name__}: {e}"
        if m is not None:
            hits.append(f"{'endpos' if reverse else 'pos'}={p} {describe(m)}")
        if len(hits) >= 3:
            break
    return " || ".join(hits) or "never answers at any anchor"


def stepwise(row: dict) -> str:
    """Upstream's own scan taken one match at a time, each step a fresh attempt."""
    compiled = compile_row(row)
    subject = row["subject"]
    reverse = "(?r" in row["pattern"] or bool(row.get("flags", 0) & REVERSE)
    overlapped = row["operation"].endswith("overlapped")
    spans, zero_width = [], False
    pos, endpos = 0, len(subject)
    while len(spans) < 12:
        if reverse and endpos < 0:
            break
        if not reverse and pos > len(subject):
            break
        try:
            m = (compiled.search(subject, pos, endpos, timeout=CALL_TIMEOUT) if reverse
                 else compiled.search(subject, pos, timeout=CALL_TIMEOUT))
        except Exception as e:  # noqa: BLE001
            return f"{type(e).__name__}: {e}"
        if m is None:
            break
        if m.end() == m.start() and not overlapped:
            zero_width = True
            break
        spans.append(m.span())
        if reverse:
            endpos = m.end() - 1 if overlapped else m.start()
        elif overlapped:
            pos = m.start() + 1
        else:
            pos = m.end()
    caveat = "  <- ZERO-WIDTH match reached: must_advance (:20932) makes this NOT the scanner's step" if zero_width else ""
    return f"{len(spans)} | " + " || ".join(str(s) for s in spans) + caveat


def dollar_positions(subject: str, flags: int, unicode_lines: bool = False) -> list[int]:
    end = regex.compile("(?w)$" if unicode_lines else "$", flags, cache_pattern=False)
    return [p for p in range(len(subject) + 1) if end.match(subject, p) is not None]


def read_report(seed: int) -> list[tuple[int, str, str]]:
    """Every DIVERGE row of one seed's report as (row number, upstream line, port line)."""
    report = REPORTS / f"report-{seed}.txt"
    if not report.exists():
        return []
    lines = report.read_text(encoding="utf-8", errors="replace").splitlines()
    starts = [i for i, line in enumerate(lines) if HEAD.match(line)]
    out = []
    for n, start in enumerate(starts):
        block = lines[start + 1:starts[n + 1] if n + 1 < len(starts) else len(lines)]

        def first(prefix: str) -> str:
            return next((x.strip()[len(prefix):].strip() for x in block if x.strip().startswith(prefix)), "")

        out.append((int(HEAD.match(lines[start]).group(1)), first("upstream "), first("port ")))
    return out


def read_wave_rows(seed: int, numbers: list[int]) -> dict[int, dict]:
    """The named rows of `wave-<seed>.jsonl`, in ONE pass over the file.

    THE HEADER LINE MAKES THE FILE LINE number + 1. One pass rather than one per row because a
    6000-row gate wave is about 40 MB and the gate leaves a dozen divergences a seed; re-scanning it
    per row is the difference between three seconds and four minutes.
    """
    wave = REPORTS / f"wave-{seed}.jsonl"
    if not wave.exists():
        return {}
    wanted = set(numbers)
    found: dict[int, dict] = {}
    with wave.open(encoding="utf-8") as handle:
        for i, line in enumerate(handle):
            if i in wanted:
                found[i] = json.loads(line)
                if len(found) == len(wanted):
                    break
    return found


def doors(row: dict, label: str, upstream_line: str, port_line: str) -> None:
    """Put every judged family's control to one row and print what each answers.

    Renders no verdict. `upstream_line` and `port_line` are what the RUN recorded, quoted back so a
    reader can see the probe reproducing them rather than taking the probe's word for it.
    """
    pattern, subject = row["pattern"], row["subject"]
    partial = bool(row.get("partial"))
    reverse = "(?r" in pattern or bool(row.get("flags", 0) & REVERSE)

    print(f"\n--- {label}  {row.get('generator', 'rows')}  {row['operation']}"
          f"{' partial' if partial else ''}  flags={row.get('flags', 0):#x}"
          f"{'  REVERSED' if reverse else ''}")
    print("    pattern            " + ascii(pattern))
    print("    subject            " + ascii(subject))
    if row.get("codepointSlice") or row.get("pos") is not None or row.get("endpos") is not None:
        print(f"    slice (codepoints) {slice_of(row)}   of {len(subject)}")
    if row.get("searchOnlyPartial") is not None:
        print(f"    searchOnlyPartial  {row['searchOnlyPartial']}"
              "   <- search-start-partial's own recorded discriminator")
    if row.get("namedLists"):
        print("    lists              " + ascii(str(row["namedLists"])))
    if row.get("template") is not None:
        print("    template           " + ascii(row["template"]) + f"  count={row.get('count', 0)}")
    print("    UPSTREAM (recorded)" + upstream_line)
    print("    PORT     (recorded)" + port_line)
    print("    as drawn           " + ascii(answer(row)))

    if "(*SKIP)" in pattern:
        print("    (*SKIP)->(*PRUNE)  " + ascii(answer(row, pattern.replace("(*SKIP)", "(*PRUNE)"))))
        print("    verb deleted       " + ascii(answer(row, pattern.replace("(*SKIP)", ""))))

    if partial:
        # The same call with the flag dropped, which is `partial-retry-carried-slice-forward`'s
        # whole control: a COMPLETE match the flagless call denies is upstream contradicting itself.
        compiled = compile_row(row)
        call = getattr(compiled, row["operation"])
        slo, shi = slice_of(row)
        print("    no partial         " + ascii(describe(call(subject, slo, shi, timeout=CALL_TIMEOUT))))

        # `search-start-partial`: upstream's own anchored match over the span it reported. The
        # recorder writes that span in CODEPOINTS as well as UTF-16, and upstream counts codepoints,
        # so `codepointSpan` is the only one of the two that can be handed back to it.
        span = row.get("codepointSpan")
        if span:
            lo, hi = span
            print(f"    match over ({lo},{hi})  "
                  + ascii(describe(compiled.match(subject, lo, hi, partial=True, timeout=CALL_TIMEOUT)))
                  + "   [non-partial: "
                  + ascii(describe(compiled.match(subject, lo, hi, timeout=CALL_TIMEOUT))) + "]")
        print("    anchor sweep       " + ascii(sweep(row)))

    # `bestmatch-loses-a-candidate`'s door, and the one its own recorded discriminator cannot open.
    # That entry keys on `bestmatchFreeOutcome`, upstream's answer with `(?b)` deleted - which reads
    # the same whether `(?b)` alone or the pair `(?b)(?e)` destroyed the match. Deleting them one at
    # a time says which.
    for flag in ("(?b)", "(?e)"):
        if flag in pattern:
            print(f"    without {flag}       " + ascii(answer(row, pattern.replace(flag, "", 1))))
    if "(?b)" in pattern and "(?e)" in pattern:
        print("    without either     "
              + ascii(answer(row, pattern.replace("(?b)", "", 1).replace("(?e)", "", 1))))

    if row["operation"] in ("finditer", "finditer-overlapped", "split", "sub", "subf"):
        print("    stepwise           " + ascii(stepwise(row)))

    if "$" in pattern:
        flags = row.get("flags", 0)
        print(f"    upstream's own `$` true at  {dollar_positions(subject, flags)}"
              f"   (?w)$ at {dollar_positions(subject, flags, True)}")


def from_rows_file(path: Path) -> int:
    """Put the doors to the rows of a `--rows`-shaped `.jsonl` instead of to a seed's report.

    ONCE A ROW IS JUDGED IT LEAVES THE REPORT, so pointing this probe at a seed stops reproducing
    the doors for a row an entry now classifies - and an entry citing this probe would then cite
    something nobody can run. Handing the rows in directly keeps the citation good for the life of
    the entry. There is no report to read this port's answer out of that way, so those two lines say
    so; every door still opens, because every door is upstream-side.
    """
    rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    print(f"\n=== {path}  ({len(rows)} rows)")
    for number, row in enumerate(rows, start=1):
        doors(row, f"{path.name} row {number}", " (no report)", " (no report)")
    return 0


def main(argv=None) -> int:
    argv = list(argv if argv is not None else sys.argv[1:])
    print("regex", regex.__version__)

    if argv and argv[0] == "--rows":
        return from_rows_file(Path(argv[1]))

    for seed in [int(a) for a in argv] or default_seeds():
        rows = read_report(seed)
        if not rows:
            print(f"\n=== seed {seed}: no report - the seed was GREEN, or the gate has not run")
            continue
        print(f"\n=== seed {seed}  ({len(rows)} diverging rows)")
        drawn = read_wave_rows(seed, [number for number, _, _ in rows])
        for number, upstream_line, port_line in rows:
            row = drawn.get(number)
            if row is None:
                print(f"\n--- row {number}: no wave-{seed}.jsonl - re-run the gate")
                continue
            doors(row, f"seed {seed} row {number}", "  " + upstream_line, "  " + port_line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
