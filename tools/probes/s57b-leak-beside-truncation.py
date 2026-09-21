r"""Upstream's half of `leaked-changes-beside-a-truncated-partial`, the two gate rows it judges.

Seed 7 row 75921 and seed 20260920 row 72433 of the 6000-row gate are both PARTIAL matches on which
the two engines agree about the span, the groups, `lastindex`, `lastgroup` and the partial flag, and
differ only over the fuzzy half. Neither of ledger entry 11's two sibling pins can take them:

  * `fuzzy-changes-leaked-from-an-abandoned-attempt` (mechanism A) demands the two engines agree on
    the COUNTS, and here upstream counts fewer errors than this port does.
  * `fuzzy-counts-of-a-partial-are-the-innermost-sections` (mechanism B) demands upstream's DRAWN
    positions be a prefix of this port's, and here they are not - they are a stale attempt's.

This probe shows why, from upstream alone. On both rows, asking upstream the same question ANCHORED
at the span it reported - which makes the winning attempt its first attempt, so no earlier attempt
can have left anything on the change stack (`_leak_free_fuzzy` in tools/record-oracle.py) - moves
its answer, and what it moves to IS a prefix of this port's script. So the leak and the truncation
are both present, one on top of the other.

    python tools/probes/s57b-leak-beside-truncation.py

POSITIONS ARE PYTHON CODEPOINTS HERE. The recorder converts spans and change positions to UTF-16 at
the boundary, so the numbers in `tools/probes/s57b-gate-rows.jsonl` and in the oracle report are
UTF-16 offsets. Row 75921's subject is all BMP and the two agree; row 72433's subject is two astral
characters followed by two joiners, so its UTF-16 numbers are larger. Each row below prints both.

The port half is tools/probes/s57b-port-leak-beside-truncation.cs. Measured 2026-09-21 against
regex 2026.9.10.
"""

import regex

ROWS = [
    {
        "seed": 7,
        "row": 75921,
        "pattern": "(?e)(?>(?:([\\w\\s])(?:\\w){1<=e<=2}){e<=1:\\d})\\m$",
        "subject": "bB.a.",
        "flags": 0x80,
        "partial": True,
        # The row also records an `atomicFreeOutcome`. It is NOT a door onto this row, and the
        # ablation below is here to show that rather than to be believed: spelling `(?>` as `(?:`
        # moves the SPAN, so the cut-free answer is a different match and says nothing about this one.
        "ablations": [("atomic cut removed", "(?e)(?:(?:([\\w\\s])(?:\\w){1<=e<=2}){e<=1:\\d})\\m$")],
    },
    {
        "seed": 20260920,
        "row": 72433,
        "pattern": "(?:(\\p{L})(?:([\\w\\s]*)){e<=2,i<=1}){2i+1d+1s<=2}\\B(?:(?:([[:alpha:]])‍([[:alpha:]]*?)){s<=1,i<=1,d<=1}(*PRUNE)\\p{ASCII}|[^a-f])$",
        "subject": "\U0001f3fb\U0001f3fb‍‍",
        "flags": 0x400A,
        "partial": True,
        # `(*PRUNE)` abandons an attempt without unwinding, which is mechanism A's own description.
        "ablations": [
            ("(*PRUNE) -> (*SKIP)", None),
            ("verb deleted", None),
        ],
    },
]

KINDS = ("substitutions", "insertions", "deletions")


def fuzzy(m) -> str:
    """Upstream's own two views of one edit script, side by side."""
    if m is None:
        return "no match"
    counts = m.fuzzy_counts
    changes = m.fuzzy_changes
    listed = ", ".join(f"{k}={list(p)}" for k, p in zip(KINDS, changes))
    consistent = all(counts[i] == len(changes[i]) for i in range(3))
    return (
        f"span={m.span(0)} counts={counts} {listed}"
        + ("" if consistent else "   <- the counts and the changes disagree IN KIND")
    )


def utf16(subject: str, position: int) -> int:
    """The same position counted the way the recorder writes it."""
    return len(subject[:position].encode("utf-16-le")) // 2


def main() -> int:
    print("regex", regex.__version__)

    for row in ROWS:
        subject, flags = row["subject"], row["flags"]
        compiled = regex.compile(row["pattern"], flags)
        drawn = compiled.search(subject, partial=row["partial"])

        print(f"\n=== seed {row['seed']} row {row['row']}   subject {ascii(subject)}")
        print(f"  pattern            {ascii(row['pattern'])}")
        print(f"  AS DRAWN           {fuzzy(drawn)}")

        if drawn is None:
            continue

        start, end = drawn.span(0)
        print(f"  the span in UTF-16 ({utf16(subject, start)}, {utf16(subject, end)})")

        # The leak-free question, exactly as the recorder asks it.
        again = compiled.match(subject, start, end, partial=row["partial"])
        moved = again is not None and again.span(0) == (start, end)
        print(f"  ANCHORED THERE     {fuzzy(again) if moved else 'a different match - unanswerable'}")

        for label, pattern in row["ablations"]:
            if pattern is None:
                pattern = (
                    row["pattern"].replace("(*PRUNE)", "(*SKIP)")
                    if "SKIP" in label
                    else row["pattern"].replace("(*PRUNE)", "")
                )
            print(f"  {label:<18} {fuzzy(regex.compile(pattern, flags).search(subject, partial=row['partial']))}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
