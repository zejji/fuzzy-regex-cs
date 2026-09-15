"""The rows classified against upstream with one construct taken away.

The claim each of the two new `ExpectedDivergences` entries rests on, and the thing that makes
either family judgeable at all: **on every row this port's answer is upstream's OWN answer with
one construct deleted** - POSIX for `posix-fuzzy-contradicts-its-own-flagless-answer`, the atomic group's
backtracking cut for `atomic-group-leaks-a-change-position`.

Written by S48b's second sitting on 2026-09-14. The rows are carried INLINE rather than read out
of `TestResults/oracle/wave-<seed>.jsonl`, because those waves are not committed and a probe that
needs one is a probe nobody can re-run - which is why S18's controls are permanently lost. Each is
quoted as the wave drew it, with the seed and row number beside it.

Run it::

    python tools/probes/upstream-posix-and-atomic-free-answers.py

Expected on 2026.9.10, and this is what the entries claim:

* row 76983 - the `control` line is this port's answer and the `as drawn` line is not. Upstream
  under POSIX spends an error its own POSIX-free engine does not need for the same span.
* row 76101 - the `control` line is again this port's answer, but the COST does not move at all:
  both cost (1, 0, 1) and the SPAN changes, (0, 7) under POSIX against (0, 8) without it. POSIX
  picked the SHORTER of two equal-cost matches, which is leftmost-longest inverted - ledger entry
  16's mechanism, not entry 9's. A first draft of this probe lumped it with 76983 and the blind
  review reproduced the row that refutes that.
* row 73895 - the whole SCAN moves when POSIX goes, so the control is the ANCHORED question:
  upstream's own POSIX-free `fullmatch` over the span it reported under POSIX. That answers with
  NO errors where the POSIX scan charged one, so upstream contradicts itself at identical flags.
* row 74033 - the `control` line moves the deletion from codepoint 2 to 3, which is UTF-16 4 on
  this astral subject and is this port's answer. `leakFreeFuzzy` cannot see it, because that
  question re-asks upstream ANCHORED and an atomic group's leak is inside ONE attempt.

S52 added the last two POSIX rows on 2026-09-15, from the three-seed 2000-row wave of commit
407c0cb:

* seed 7 row 24430 - row 76983's mechanism again, now through a `\\L<name>` list and with POSIX set
  as a FLAG rather than written `(?p)`. The two matches agree on both spans and on the second
  match's cost; upstream under POSIX charges the FIRST match's span one deletion where its own
  POSIX-free engine spends none on the identical span, and none is this port's answer.
* seed 7 row 24916 - a new symptom in the same family: POSIX does not move a cost or a span, it
  invents a MATCH. `subn` with `count=2` replaces twice under POSIX and once without it, on a
  template that expands to nothing either way, so the text is identical and only the count says so.
  A choosing flag cannot produce a match the flagless engine cannot make.

S52 sitting 9 added the second ATOMIC row on 2026-09-15, from the seed-7 6000-row gate:

* seed 7 row 74510 - the stronger of the two, because it needs no comparison with this port to be
  wrong. Upstream counts `(1, 2, 0)` and then lists TWO substitutions and ONE insertion, and
  `fuzzy_changes` is documented as the positions of the changes `fuzzy_counts` counts. The
  `control` line keeps the span, all five groups and the counts and re-kinds the list to one
  substitution at 5 and two insertions at 3 and 4, which is this port's answer.

**Reading `fuzzy_changes` on a POSIX fuzzy match SEGFAULTS the interpreter** (ledger entry 9,
`tools/probes/upstream-posix-fuzzy-crash.py`), so it is GUARDED here rather than caught - a
segfault is not an exception, and a probe that tries to catch it kills the run instead of
reporting it.

The engine-side half - that this port answers the `control` line - is asserted by the oracle
entries themselves on every wave, and by the gap tests they name, not here.
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

# Upstream's POSIX flag bit, which is `regex.P`. Read off the COMPILED pattern rather than off the
# row's flags, because an inline `(?p)` never reaches the row's flags.
_POSIX = 0x10000

# (seed, row, pattern, flags, subject, operation, template, count, named lists). THE FLAGS ARE
# PART OF THE QUESTION - none of these reproduces without them.
POSIX_ROWS = [
    (
        7,
        73895,
        r"(?b)(?e)(?r)(?p)(?:(?P<g1>\D+?)([\p{L}||\p{N}]*)\w){e<=2,s<=1}(?P<g3>[A])(?:(?(3)(?!(?P>g3))[\w--[0-9]]))*?",
        0x10A,
        "\nAA\U0001F600\U0001F600aa ",
        "finditer-overlapped",
        None,
        0,
        {},
    ),
    (
        7,
        76983,
        r"(?e)(?r)(?p)(?:[^\d][a\d]\p{L}){s<=1,i<=1,d<=1}(\p{Lu})+\b",
        0x8,
        " ﬀS",
        "finditer-overlapped",
        None,
        0,
        {},
    ),
    (
        20260914,
        76101,
        r"(?b)(?e)(?r)(?:\p{Ll}+.([a]+)){s<=1:\W}(?:[a](?P<g2>[\w\s]*)){e<=1}$",
        0x10000,
        "ıı\rAAﬁ\r\n",
        "subf",
        "{g2}{g2}{1}",
        0,
        {},
    ),
    (
        7,
        24430,
        r"(?e)(?r)\b(?P<g1>[[:alpha:]]+?)\L<w1>{s<=1,i<=1,d<=1:\d}"
        r"(?<!(?:[a\d](?P<g2>[\p{L}\p{N}]+?)\U0001F600){e<=2,i<=1})",
        0x10008,
        "a\U0001F600\U00010428\U0001F600\U0001D518\r\nAa",
        "finditer",
        None,
        0,
        {"w1": ["\U0001F600A", "\U0001F600\U0001D518", "\U0001F600\U0001D518A"]},
    ),
    (
        7,
        24916,
        r"(?b)(?:([a]*)[a]*){s<=1}\g<1>\K$",
        0x1400A,
        "\rA\n",
        "sub",
        r"\1",
        2,
        {},
    ),
]

ATOMIC_ROWS = [
    (
        20260914,
        74033,
        r"^(?:\p{Ll}\w??[a-f]){1i+2d+1s<=3}(?>(?:\p{Ll}(?:\p{L}){s<=1,i<=1,d<=1}){d<=1})$",
        0x0,
        "AA\U0001D518\U0001D518",
        "search",
        None,
        0,
        {},
    ),
    # Added by S52 sitting 9 (2026-09-15), from the seed-7 6000-row gate. The STRONGER of the two:
    # here upstream's drawn change list is of the wrong KIND for upstream's OWN counts - it counts
    # one substitution and two insertions and then lists TWO substitutions and ONE insertion - so
    # the row is a contradiction before this port is consulted at all. The `(?:` control keeps the
    # span, all five groups and the counts and re-kinds the list to this port's answer.
    (
        7,
        74510,
        r"^(?:(\p{Lu})([\w\s])\W){1i+2d+1s<=3}"
        r"(?>(?:(\s?)(?:([^\d])){s<=1,i<=1,d<=1:\w}){2i+1d+1s<=2})([a\d]{0,})$",
        0x400A,
        "ﬃﬃ ﬃﬃßß",
        "finditer",
        None,
        0,
        {},
    ),
]


def describe(m) -> str:
    if m is None:
        return "None"
    bits = [f"span={m.span()}"]
    for n in range(1, m.re.groups + 1):
        span = m.span(n)
        bits.append(f"g{n}={span}" if span != (-1, -1) else f"g{n}=unset")
    if m.partial:
        bits.append("PARTIAL")
    bits.append(f"counts={m.fuzzy_counts}")
    # GUARDED, not caught - see the module docstring.
    bits.append("changes=<segfaults under POSIX>" if m.re.flags & _POSIX else f"changes={m.fuzzy_changes}")
    return " ".join(bits)


def run(pattern: str, flags: int, subject: str, operation: str, template, count: int, lists=None) -> str:
    compiled = regex.compile(pattern, flags, **(lists or {}))
    if operation in ("match", "search", "fullmatch"):
        return describe(getattr(compiled, operation)(subject))
    if operation in ("finditer", "finditer-overlapped"):
        found = list(compiled.finditer(subject, overlapped=operation.endswith("overlapped")))
        return f"{len(found)} | " + " || ".join(describe(m) for m in found)
    if operation in ("sub", "subf"):
        method = compiled.subn if operation == "sub" else compiled.subfn
        return repr(method(template, subject, count=count))
    raise SystemExit(f"unhandled operation {operation}")


if __name__ == "__main__":
    print("regex", regex.__version__)

    for seed, number, pattern, flags, subject, operation, template, count, lists in POSIX_ROWS:
        # The prefix-only edit `tools/record-oracle.py` makes, so the probe and the recorder ask
        # the same question.
        free = pattern.replace("(?p)", "", 1)
        free_flags = flags & ~_POSIX

        print()
        print(f"--- seed {seed} row {number}  {operation}  flags={flags:#x}  POSIX")
        print(f"    pattern  {pattern!r}")
        print(f"    subject  {subject!r}")
        if lists:
            print(f"    lists    {lists!r}")
        print(f"    as drawn {run(pattern, flags, subject, operation, template, count, lists)}")
        print(f"    control  {run(free, free_flags, subject, operation, template, count, lists)}")

        # Row 73895's control is ANCHORED, because the POSIX-free SCAN answers a different span.
        # The question upstream contradicts itself on is what the span it DID report costs.
        if number == 73895:
            span = regex.compile(pattern, flags).search(subject).span()
            anchored = regex.compile(free, free_flags).fullmatch(subject, *span)
            print(f"    anchored control over upstream's own POSIX span {span}: {describe(anchored)}")

        # Row 24430's scan agrees on both spans, so the contradiction is per MATCH: what each span
        # costs under POSIX against what upstream's own POSIX-free engine spends on the identical
        # span. Printed match by match, because only the first one moves.
        if number == 24430:
            drawn = list(regex.compile(pattern, flags, **lists).finditer(subject))
            without = regex.compile(free, free_flags, **lists)
            for m in drawn:
                same = without.fullmatch(subject, *m.span())
                print(
                    f"    span {m.span()}: POSIX counts={m.fuzzy_counts}   "
                    f"POSIX-free over that very span {describe(same)}"
                )

    for seed, number, pattern, flags, subject, operation, template, count, lists in ATOMIC_ROWS:
        free = pattern.replace("(?>", "(?:")

        print()
        print(f"--- seed {seed} row {number}  {operation}  flags={flags:#x}  ATOMIC")
        print(f"    pattern  {pattern!r}")
        print(f"    subject  {subject!r}")
        print(f"    as drawn {run(pattern, flags, subject, operation, template, count, lists)}")
        print(f"    control  {run(free, flags, subject, operation, template, count, lists)}")
