r"""Every S57b gate row, with the control its candidate family is judged on, in one pass.

`gate-divergence-doors.py` asks every judged family's door of every row and renders no verdict.
This asks the four questions the doors do NOT ask, over the twenty rows of the 6000-row gate that
S57 sitting 3 found red::

    python tools/probes/s57b-gate-row-controls.py

THE FOUR QUESTIONS, and which family each belongs to:

  counts vs changes
      `fuzzy-changes-of-the-wrong-kind-for-their-own-counts`. `fuzzy_counts` is
      (substitutions, insertions, deletions) and `fuzzy_changes` is the three lists of positions
      for the same three kinds, so the length of each list must equal its own count. Where it does
      not, upstream has recorded a change of the wrong KIND for its own counts, and a port that
      keeps the two consistent must differ.

  total error cost
      `enhancematch-ranks-by-cost` and `bestmatch-ranks-by-cost`. Under `(?e)` and `(?b)` the
      answer is the one with the FEWEST errors, so the two engines' totals are the question, not
      their change lists. A side with a strictly smaller total has found a better match than the
      other engine's own flag asked for.

  anchor reachability (uncapped)
      `search-start-partial` against `partial-retry-*`, the split
      `upstream-partial-anchor-reachability.py` established for S52 sitting 15. Is upstream's own
      search answer a span ANY anchored call in the searched region produces? Unreachable means
      the prefilter answered rather than the engine; reachable-but-not-at-the-first-anchor means a
      bound skipped the anchors in between. The sweep is uncapped, because a reversed row is
      anchored by its END and the anchor its search owes is the HIGHEST `endpos` that answers.

  Turkic cells
      `turkic-default-folding`. `CaseFolding.txt` marks `0049; T; 0131` and `0130; T; 0069`, and
      says the `T` rows are excluded by default; upstream merges them into both default tables, so
      `(?i)I` matches U+0131 upstream and not here. The control is two cells - the row's own
      uppercase class against U+0131 - plus the swap the pattern-side entry uses: replace the
      Turkic letter with U+00DF or U+FB00, which fold long but carry no `T` row, and the only
      thing varied is whether a `T` row is consulted.

The helpers are imported from `upstream-partial-anchor-reachability.py` rather than copied, so the
required-string handling for `PREFILTER_FREE` generators and the codepoint slice cannot drift
between the two. The rows come out of `s57b-gate-rows.jsonl`, which `.scratch/extract-gate-rows.py`
cut from `wave-<seed>.jsonl` - nothing here is transcribed.

Written by S57b against regex 2026.9.10.
"""

import importlib.util
import json
import sys
from pathlib import Path

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

REPO_ROOT = Path(__file__).resolve().parents[2]
ROWS_FILE = REPO_ROOT / "tools" / "probes" / "s57b-gate-rows.jsonl"

_REACH = REPO_ROOT / "tools" / "probes" / "upstream-partial-anchor-reachability.py"
_spec = importlib.util.spec_from_file_location("anchor_reachability", _REACH)
reach = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(reach)

IGNORECASE = 0x2
FULLCASE = 0x4000

# Letters that fold to more than one character and carry NO `T` row in `CaseFolding.txt`, so
# swapping one in varies whether a Turkic row is consulted and nothing else. The same three the
# `turkic-default-folding-from-the-pattern-side` entry uses.
NON_TURKIC_LONG_FOLDS = ("ß", "ﬀ", "ǰ")
TURKIC_LETTERS = "İı"


def counts_vs_changes(m) -> str:
    """Whether each `fuzzy_changes` list is as long as its own `fuzzy_counts` entry."""
    if m is None or not any(m.fuzzy_counts):
        return "no fuzzy answer"
    if m.re.flags & 0x10000:  # POSIX: reading `fuzzy_changes` on a spent error is ledger entry 9
        return f"counts={m.fuzzy_counts} changes unavailable upstream (POSIX)"
    lengths = tuple(len(part) for part in m.fuzzy_changes)
    verdict = "CONSISTENT" if lengths == tuple(m.fuzzy_counts) else "WRONG KIND"
    return f"counts={tuple(m.fuzzy_counts)} change-list lengths={lengths}  {verdict}"


# `fuzzy=(0,1,1)[s:][i:4][d:4]` as the report renders this port's answer. THE WAVE'S `outcome` IS
# UPSTREAM'S, not this port's - the first draft of this probe read the counts out of there and
# printed "equal" on every fuzzy row, which is upstream compared with itself.
_PORT_COUNTS = regex.compile(r"fuzzy=\((\d+),(\d+),(\d+)\)")


def port_counts(port_line: str) -> tuple[int, int, int] | None:
    """This port's fuzzy counts, read off the run's own report line."""
    found = _PORT_COUNTS.search(port_line or "")
    return tuple(int(g) for g in found.groups()) if found else None


def turkic_cells(row: dict) -> list[str]:
    """The row's own uppercase classes against U+0131, and the non-Turkic swap."""
    flags = row.get("flags", 0)
    out = [f"flags carry IGNORECASE={bool(flags & IGNORECASE)} FULLCASE={bool(flags & FULLCASE)}"]
    for klass in ("[A-Z]", r"\p{Lu}", "I", "i"):
        try:
            compiled = regex.compile(klass, flags, cache_pattern=False)
        except Exception as e:  # noqa: BLE001
            out.append(f"{klass:<10} did not compile: {type(e).__name__}: {e}")
            continue
        cells = []
        for letter in ("ı", "İ"):
            m = compiled.fullmatch(letter, timeout=reach.CALL_TIMEOUT)
            cells.append(f"U+{ord(letter):04X} {'MATCH' if m else 'no match'}")
        out.append(f"{klass:<10} " + "   ".join(cells))

    pattern, subject = row["pattern"], row["subject"]
    for replacement in NON_TURKIC_LONG_FOLDS:
        swapped_pattern, swapped_subject = pattern, subject
        for letter in TURKIC_LETTERS:
            swapped_pattern = swapped_pattern.replace(letter, replacement)
            swapped_subject = swapped_subject.replace(letter, replacement)
        swapped = dict(row, pattern=swapped_pattern, subject=swapped_subject)
        # A replacement THE ROW ALREADY CONTAINS is not a clean cell: it does not only vary whether
        # a `T` row is consulted, it also adds a repeat of a letter the pattern or the subject was
        # already matching on. Row 2 carries U+00DF in its pattern and U+FB00 in its subject, and
        # its three cells disagree for exactly that reason - only the U+01F0 one is clean.
        contaminated = replacement in pattern or replacement in subject
        out.append(f"swap {TURKIC_LETTERS!a} -> {replacement!a}   upstream "
                   + ascii(answer_for(swapped))
                   + ("   <- CONTAMINATED: the row already holds this letter" if contaminated
                      else ""))
    return out


def answer_for(row: dict, pattern: str | None = None) -> str:
    """Upstream's answer to the row's own operation - the doors probe's `answer`, reused."""
    return doors.answer(row, pattern)


_DOORS = REPO_ROOT / "tools" / "probes" / "gate-divergence-doors.py"
_dspec = importlib.util.spec_from_file_location("gate_divergence_doors", _DOORS)
doors = importlib.util.module_from_spec(_dspec)
_dspec.loader.exec_module(doors)


def partial_questions(row: dict) -> list[str]:
    """The uncapped sweep and the reachability grid, for a partial row.

    THE ROW'S OWN OPERATION, not `search`. Thirteen of these twenty are partial: nine `search`,
    three `fullmatch` and one `match`. Asking `search` of a `fullmatch` row reports a span the row
    never produced (row 19 answers (0, 7) and the sliceless search answers (0, 5)), which is a
    different question. Only the nine `search` rows reach this function; see its caller.
    """
    out = []
    try:
        call = getattr(reach.compile_row(row), row["operation"])
        drawn = call(row["subject"], *reach.slice_of(row), partial=True,
                     timeout=reach.CALL_TIMEOUT)
    except Exception as e:  # noqa: BLE001 - upstream refuses to compile two of the twenty
        return [f"upstream cannot compile: {type(e).__name__}: {e}"]
    hits, reverse = reach.anchor_sweep(row)
    bound = "endpos" if reverse else "pos"
    first = hits[-1] if reverse and hits else (hits[0] if hits else None)
    out.append(f"anchors answering ({bound}) {[p for p, _ in hits]}"
               + ("  (reversed: the search owes the HIGHEST)" if reverse else
                  "  (forward: the search owes the LOWEST)"))
    out.append("first anchor owed   " + (f"{bound}={first[0]}  {first[1]}" if first
                                         else "none answers anywhere"))
    spans = reach.reachable_spans(row)
    span = drawn.span() if drawn else None
    out.append(f"upstream's span     {span}   reachable by SOME anchored call: "
               + ("YES" if span in spans else "NO")
               + f"   (grid holds {len(spans)} spans)")
    return out


def main() -> int:
    print("regex", regex.__version__)
    rows = [json.loads(line) for line in
            ROWS_FILE.read_text(encoding="utf-8").splitlines() if line.strip()]

    # This port's side comes out of the RUN'S OWN report, never transcribed, the way
    # `gate-divergence-doors.py` reads it. Keyed by (seed, row number).
    ports: dict[tuple[int, int], str] = {}
    for seed in sorted({row["s57bSeed"] for row in rows}):
        for number, _, port_line in doors.read_report(seed):
            ports[(seed, number)] = port_line

    for number, row in enumerate(rows, start=1):
        port_line = ports.get((row["s57bSeed"], row["s57bRow"]), "")
        pattern, subject = row["pattern"], row["subject"]
        flags = row.get("flags", 0)
        partial = bool(row.get("partial"))
        print(f"\n=== row {number}  seed {row['s57bSeed']} row {row['s57bRow']}"
              f"  {row.get('generator')}  {row['operation']}{' partial' if partial else ''}"
              f"  flags={flags:#x}")
        print("    pattern             " + ascii(pattern))
        print("    subject             " + ascii(subject))
        print("    upstream (recorded) " + ascii(json.dumps(row.get("outcome"))[:220]))
        print("    port     (recorded) " + ascii(port_line))

        try:
            compiled = reach.compile_row(row)
        except Exception as e:  # noqa: BLE001
            print(f"    UPSTREAM CANNOT COMPILE THE ROW: {type(e).__name__}: {e}")
            continue

        # ONLY FOR A `search` ROW. The grid is built from anchored `match` calls, and it asks
        # whether upstream's SEARCH answered something its own matcher cannot make. A `match` or
        # `fullmatch` row is already anchored, so its answer is trivially its own matcher's and the
        # question does not arise - printing the grid there reports a `search`'s span beside a
        # `fullmatch`'s and invites the reader to compare two different calls.
        if partial and row["operation"] == "search":
            for line in partial_questions(row):
                print("    " + line)

        # Fuzzy: the two questions the doors never ask.
        lo, hi = reach.slice_of(row)
        if row["operation"] in ("match", "search", "fullmatch"):
            call = getattr(compiled, row["operation"])
            m = (call(subject, lo, hi, partial=True, timeout=reach.CALL_TIMEOUT) if partial
                 else call(subject, lo, hi, timeout=reach.CALL_TIMEOUT))
            if m is not None and any(m.fuzzy_counts):
                print("    counts vs changes   " + counts_vs_changes(m))
                mine = port_counts(port_line)
                if mine is not None:
                    theirs = tuple(m.fuzzy_counts)
                    # A CHEAPER/DEARER verdict only means something under a RANKING flag. Without
                    # `(?b)` or `(?e)` upstream returns the first match it finds, not the fewest-
                    # error one, so a smaller total is not a better answer by its own rules and
                    # calling it cheaper reads as a finding where there is none. Read the flags off
                    # the COMPILED pattern, because every one of these rows carries its ranking flag
                    # inline in the pattern and the row's own `flags` field is 0 for three of them.
                    ranked = m.re.flags & (regex.BESTMATCH | regex.ENHANCEMATCH)
                    verdict = ("   <- this port's is CHEAPER" if sum(mine) < sum(theirs)
                               else "   <- upstream's is CHEAPER" if sum(theirs) < sum(mine)
                               else "   <- equal") if ranked else \
                              "   (no (?b) or (?e): totals are not ranked, so neither is 'better')"
                    print(f"    total error cost    upstream {sum(theirs)} {theirs}"
                          f"   this port {sum(mine)} {mine}" + verdict)

        if any(letter in pattern + subject for letter in TURKIC_LETTERS) and flags & IGNORECASE:
            for line in turkic_cells(row):
                print("    " + line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
