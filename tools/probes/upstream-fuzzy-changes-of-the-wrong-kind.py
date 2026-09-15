r"""Upstream reports fuzzy change positions of the wrong KIND for its own fuzzy counts.

The evidence `ExpectedDivergences`'s `fuzzy-changes-of-the-wrong-kind-for-their-own-counts` rests
on, and ledger entry 11's mechanism through a fourth door. Seed 7 row 74345 of the 6000-row gate of
2026-09-15, carried INLINE because the gate's waves are not committed and a probe that needs one is
a probe nobody can re-run.

Upstream answers the drawn row with `fuzzy_counts` ``(0, 2, 0)`` - no substitutions, two insertions,
no deletions - and `fuzzy_changes` holding one SUBSTITUTION and one DELETION and no insertion.
`fuzzy_changes` is documented as the positions of the changes `fuzzy_counts` counts, so that one
answer contradicts itself; this port answers the same counts with two insertion positions.

WHAT THE ABLATIONS SHOW, and it is a negative result worth keeping: every construct-level ablation
MOVES THE CANDIDATE, so none of them can arbitrate the drawn answer's positions. The pattern carries
a fuzzy section inside a NEGATIVE LOOKAHEAD - a construct that succeeds precisely when its body's
sub-attempts are abandoned, which is the defect class's own description - but that attribution is
NOT established here and the entry does not claim it. Anchoring the drawn span holds the candidate
and reproduces the contradiction unchanged, which is the row's own recorded `leakFreeFuzzy` said a
second way.

Run it::

    python tools/probes/upstream-fuzzy-changes-of-the-wrong-kind.py

Exits non-zero if upstream stops contradicting itself on this row, so a sync notices.

Written by S52 sitting 9 against regex 2026.9.10, 2026-09-15.
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

SUBJECT = "ßß\U00010400\n"
FLAGS = 0x108
DRAWN = r"(?r)(?!(?:(?P<g1>\p{Nd}{0,2})ß){s<=1,i<=1,d<=1:\s})(?:\U00010400\U00010400){e<=2}\b"

# The drawn span, in CODEPOINTS, as the wave recorded it. Anchoring here is the one question that
# holds the candidate still.
DRAWN_SPAN = (0, 3)

ABLATIONS = [
    (
        "lookahead deleted",
        r"(?r)(?:\U00010400\U00010400){e<=2}\b",
    ),
    (
        "lookahead body not fuzzy",
        r"(?r)(?!(?:(?P<g1>\p{Nd}{0,2})ß))(?:\U00010400\U00010400){e<=2}\b",
    ),
    (
        "lookahead body fuzzy, zero budget",
        r"(?r)(?!(?:(?P<g1>\p{Nd}{0,2})ß){e<=0})(?:\U00010400\U00010400){e<=2}\b",
    ),
    (
        "lookahead spelled POSITIVE",
        r"(?r)(?=(?:(?P<g1>\p{Nd}{0,2})ß){s<=1,i<=1,d<=1:\s})(?:\U00010400\U00010400){e<=2}\b",
    ),
]


def describe(m) -> str:
    if m is None:
        return "None"
    bits = [f"span={m.span()}"]
    for n in range(1, m.re.groups + 1):
        span = m.span(n)
        bits.append(f"g{n}={span}" if span != (-1, -1) else f"g{n}=unset")
    bits.append("PARTIAL" if m.partial else "complete")
    bits.append(f"counts={m.fuzzy_counts} changes={m.fuzzy_changes}")
    return " ".join(bits)


def kinds(counts, changes) -> str:
    """Which kinds each half of upstream's answer names, so the contradiction reads at a glance."""
    named = [
        f"{n} {kind}" for n, kind in zip(counts, ("sub", "ins", "del"), strict=True) if n
    ]
    listed = [
        f"{len(positions)} {kind}"
        for positions, kind in zip(changes, ("sub", "ins", "del"), strict=True)
        if positions
    ]
    return f"counts say {', '.join(named) or 'nothing'}; list names {', '.join(listed) or 'nothing'}"


if __name__ == "__main__":
    print("regex", regex.__version__)
    print(f"pattern {DRAWN!r}")
    print(f"subject {SUBJECT!r}  flags {FLAGS:#x}  search partial=True")
    print()

    drawn = regex.compile(DRAWN, FLAGS, cache_pattern=False).search(SUBJECT, partial=True)
    print(f"  as drawn                          {describe(drawn)}")
    print(f"    -> {kinds(drawn.fuzzy_counts, drawn.fuzzy_changes)}")

    anchored = regex.compile(DRAWN, FLAGS, cache_pattern=False).match(
        SUBJECT, *DRAWN_SPAN, partial=True
    )
    print(f"  anchored over its own span {DRAWN_SPAN}  {describe(anchored)}")

    print()
    print("  ablations - every one moves the candidate, so none of them arbitrates the row:")
    for label, pattern in ABLATIONS:
        got = regex.compile(pattern, FLAGS, cache_pattern=False).search(SUBJECT, partial=True)
        print(f"    {label:36} {describe(got)}")

    # The claim, asserted rather than eyeballed: the counts say insertions only, and the list names
    # no insertion at all.
    failures = []
    for label, m in (("drawn", drawn), ("anchored", anchored)):
        if m is None:
            failures.append(f"{label}: upstream now answers None")
            continue
        subs, ins, dels = m.fuzzy_counts
        sub_at, ins_at, del_at = m.fuzzy_changes
        if (subs, ins, dels) != (0, 2, 0):
            failures.append(f"{label}: counts moved to {m.fuzzy_counts}, were (0, 2, 0)")
        if list(ins_at):
            failures.append(f"{label}: upstream now names insertions at {list(ins_at)}")
        if not (list(sub_at) and list(del_at)):
            failures.append(f"{label}: upstream no longer names a substitution AND a deletion")

    print()
    if failures:
        print("UPSTREAM HAS CHANGED - the entry needs re-judging:")
        for line in failures:
            print(f"  {line}")
        raise SystemExit(1)

    print("CONFIRMED: both the drawn answer and the anchored one count two insertions and list none.")
