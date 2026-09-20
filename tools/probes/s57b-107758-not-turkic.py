"""Is gate row 107758 a Turkic-folding divergence or a slice-start one?

THE ROW. Seed 20260920, generator `partial-sliced`, flags 0x10a. The oracle pinned it as
`turkic-default-folding` until 2026-09-20, because its subject and pattern are full of dotless i
and the divergence's spans cover one. That entry's own Reason warns against exactly this: "Requiring
only IGNORECASE and one of the four somewhere in the subject would classify any unrelated defect
that landed on a row holding an `I`", and it asks for an isolating control before a row is claimed.

THE CONTROL. Swap EVERY U+0131 in both pattern and subject for a letter with no `T` row in
CaseFolding.txt - `h`, then U+00E5 - and see whether the divergence survives. If it does, folding is
not what upstream and this port disagree about.

WHAT IT PRINTS, and what it printed on regex 2026.9.10 on 2026-09-20: upstream's answer over the
slice and upstream's answer over the same text cut out as a subject of its own, shifted back. Both
stayed at `0:(3, 3)` and `0:(3, 4)` in all three forms, so the row belongs to the slice-start
family and is pinned as `reversed-partial-answers-the-cut-subject`.

    python tools/probes/s57b-107758-not-turkic.py
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

PATTERN = "(?r)ı[\\p{L}||\\p{N}](?:[\\p{ASCII}--\\p{L}]+?(*PRUNE)[\\w\\s]|\\S)"
SUBJECT = "ıßİı\r\n"
FLAGS = 0x10A


def describe(m):
    if m is None:
        return "None"
    return f"0:{m.span(0)} partial={m.partial}"


def ask(pattern, subject, lo, hi):
    compiled = regex.compile(pattern, FLAGS)
    over_slice = compiled.search(subject, lo, hi, partial=True)
    cut = compiled.search(subject[lo:hi], 0, hi - lo, partial=True)
    shifted = "None" if cut is None else f"0:({cut.span(0)[0] + lo}, {cut.span(0)[1] + lo}) partial={cut.partial}"
    print(f"  pattern {pattern!r}")
    print(f"  subject {subject!r}  slice ({lo}, {hi}) = {subject[lo:hi]!r}")
    print(f"  over the slice  {describe(over_slice)}")
    print(f"  cut, shifted    {shifted}")


print("=== the row as recorded (codepoint slice 3, 4)")
ask(PATTERN, SUBJECT, 3, 4)

# U+0131 has a `T` row; `h` and U+00E5 do not. The pattern's leading U+0131 and the sliced
# character are both swapped, because either one alone leaves a Turkic letter in the comparison.
for letter in ("h", "å"):
    swapped_pattern = PATTERN.replace("ı", letter)
    swapped_subject = SUBJECT.replace("ı", letter)
    print(f"\n=== every U+0131 swapped for {letter!r}")
    ask(swapped_pattern, swapped_subject, 3, 4)

# And the other half: keep the Turkic letters, drop the slice.
print("\n=== the row unsliced, so the slice-start rule cannot apply")
compiled = regex.compile(PATTERN, FLAGS)
print(f"  whole subject   {describe(compiled.search(SUBJECT, partial=True))}")
