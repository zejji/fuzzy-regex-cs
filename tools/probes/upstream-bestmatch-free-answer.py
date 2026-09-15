"""Every row S46's entry-12 fix moved, against upstream with and without `(?b)`.

The claim `bestmatch-loses-a-candidate` rests on, and the thing that makes the family
judgeable at all: **on every row, this port's answer under the flag is upstream's OWN
answer with the flag deleted** - groups, counts and change positions included.

Written by S46 on 2026-09-14 with nine rows, extended to fourteen by S52 sitting 16 on
2026-09-15.
The rows are carried INLINE rather than read out of
`TestResults/oracle/wave-<seed>.jsonl`, because those waves are not committed and a probe
that needs one is a probe nobody can re-run - which is why S18's controls are
permanently lost. Each is quoted as the wave drew it, with the seed and row number beside
it.

Run it:

    python tools/probes/upstream-bestmatch-free-answer.py

Expected on 2026.9.10: for every row the `without (?b)` and `as (?e)` lines agree with each
other, and the `with (?b)` line differs from both. The engine-side half - that this port
answers the `without (?b)` line - is asserted by the oracle entry itself on every wave,
not here.
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

# (seed, row, pattern, flags, subject, operation, partial). Every pattern carries `(?b)`
# as the wave drew it, and THE FLAGS AND THE `partial` FLAG ARE PART OF THE QUESTION - a
# first draft of this probe dropped both and mis-stated row 123683, whose upstream answer
# is a PARTIAL rather than no match, and row 76927, which carries 0x400A.
#
# Rows 1-4 are upstream losing the match outright; 5 and 6 are both engines matching the
# same span at the same error COUNT with a different mix; 7 is upstream reporting a partial
# where its own flagless engine completes the match; 8 and 9 are the two aggregate
# operations, whose outcome is not a match object.
ROWS = [
    (4242, 121859, r"(?b)(?:abx\sx){i<=2}", 0, "abx bxx", "fullmatch", False),
    (4242, 123179, r"(?b)(?:[^a-f]a0\Bx\p{L}){e<=3:[abx]}", 0, "zaQa", "fullmatch", False),
    (4242, 124251, r"(?b)(?:[a-f][a-f][^a-f]){1<=e<=2}", 0, "badyf", "fullmatch", False),
    (4242, 125275, r"(?b)(?:\s[ab]+){i<=2}", 0, "a bax", "fullmatch", False),
    (4242, 120771, r"(?b)(?e)(?:x0bb+?[^a-f]b+?){e<=2}", 0, "x0fbbbxba", "fullmatch", False),
    (
        20260914,
        76927,
        r"(?b)^(?:(\s*?)(\d)(?:\p{Lu}){e<=2,s<=1}){e<=2:\w}(?:\p{L}([^a-f])\U00010400){1<=e<=2}$",
        16394,
        "AA\U00010400a\U0001F600aa",
        "finditer-overlapped",
        False,
    ),
    (4242, 123683, r"(?b)(?:[ab]*?[^a-f]){e<=2}", 0, "0aya", "fullmatch", True),
    (7, 121774, r"(?b)(?:\W(?:b\B){d<=1:\d}){e}", 0, ".ba", "finditer", False),
    (4242, 125716, r"(?b)(?i)(?:x\A){e<=3}", 0, "aX", "sub", False),
    # S52 sitting 16's four, from the eight-seed seed sweep (`tools/probes/
    # sweep-divergence-rows.jsonl` rows 3, 17, 23 and 37). Each was ATTRIBUTED to this
    # family by a port-side control rather than by its shape: restoring upstream's doubled
    # `END_FUZZY` term in `Matcher.cs` makes this port refuse these four and no others of
    # the eight - see the slice notes. ALL FOUR are the counter-example to entry 12's old
    # headline: the flagless fit needs ONE insertion on every one of them, spent beside a
    # deletion on the first and third and a substitution on the second and fourth.
    (31256406, 40339, r"(?b)(?:\s(?:[^a-f]ab){e<=2:\s}){1<=e<=2}", 0, " abb", "fullmatch", True),
    (523701539, 41539, r"(?b)(a0)(?:(?:\1)){e<=3}", 0, "a0\U0001d5180\U0001f600", "fullmatch", False),
    (655924813, 41563, r"(?b)(?i)(?:(?:aba){1<=e<=2:[0-9]}){e<=3}", 0, "AB\U0001d518", "fullmatch", True),
    (793244924, 40595, r"(?b)(?i)(a0)(?:(?:\p{L}(?:\1)){s<=1}){1<=e<=2:\d}", 0, "a0xax0", "fullmatch", False),
    # And the fifth, found BY that control rather than by the signature: sweep row 20 does
    # not look like this family at all - both engines match, at the same span and the same
    # error count - and the control moves it with the other four. What differs is WHERE the
    # errors fall. IN THE CODEPOINTS THIS PROBE PRINTS, upstream substitutes at 0 and 4 and
    # inserts at 3, and its own flagless answer substitutes at 0 and 3 and inserts at 4,
    # which is this port's answer; the subject is astral, so in the UTF-16 indices the
    # recorded row and `ExpectedDivergences` carry, those read 0 and 7 against 0 and 6,
    # with the insertion at 6 against 7.
    (
        655924813,
        25002,
        "(?b)(?e)^(?:[[:digit:]][\\w\\s](?:[^\\p{L}]){e<=1}){s<=1,i<=1,d<=1}"
        "(?:\U0001d518\\d){s<=1,i<=1,d<=1}(?!(?:(\\p{ASCII})\\p{Lu}{0,}){e<=2,s<=1})$",
        8,
        "\U00010428\U00010428\U0001d518aa",
        "match",
        False,
    ),
]


def describe(m) -> str:
    if m is None:
        return "None"
    bits = [f"span={m.span()}"]
    if m.partial:
        bits.append("PARTIAL")
    bits.append(f"counts={m.fuzzy_counts}")
    bits.append(f"changes={m.fuzzy_changes}")
    return " ".join(bits)


def run(pattern: str, flags: int, subject: str, operation: str, partial: bool) -> str:
    compiled = regex.compile(pattern, flags)
    if operation in ("match", "search", "fullmatch"):
        return describe(getattr(compiled, operation)(subject, partial=partial))
    if operation in ("finditer", "finditer-overlapped"):
        found = list(compiled.finditer(subject, overlapped=operation.endswith("overlapped")))
        return f"{len(found)} | " + " || ".join(describe(m) for m in found)
    if operation == "sub":
        return repr(compiled.sub("<>", subject, count=0))
    raise SystemExit(f"unhandled operation {operation}")


if __name__ == "__main__":
    print("regex", regex.__version__)

    for seed, number, pattern, flags, subject, operation, partial in ROWS:
        flagless = pattern.replace("(?b)", "", 1)

        print()
        print(f"--- seed {seed} row {number}  {operation}  flags={flags:#x} partial={partial}")
        print(f"    pattern {pattern!r}")
        print(f"    subject {subject!r}")
        print(f"    with    (?b): {run(pattern, flags, subject, operation, partial)}")
        print(f"    without (?b): {run(flagless, flags, subject, operation, partial)}")
        print(f"    as      (?e): {run('(?e)' + flagless, flags, subject, operation, partial)}")
