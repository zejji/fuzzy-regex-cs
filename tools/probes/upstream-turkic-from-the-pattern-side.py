"""The `turkic-default-folding` rows where the Turkic letter is in the PATTERN, not the subject.

S52's second sitting, 2026-09-15. The three-seed 2000-row wave of commit 407c0cb was red on two
rows that plainly belong to the family the entry `turkic-default-folding` covers and that its
predicate cannot see: that predicate asks which Turkic letters the DIVERGENCE'S SPANS cover in the
SUBJECT, and on these two the U+0130 is in a `\\L<name>` list and in the pattern text.

The control both rows need is the same one, and it is stronger than "take the U+0130 away": a
swap for a letter whose DEFAULT full fold is ALSO longer than one character. U+00DF folds to `ss`,
U+FB00 to `ff` and U+01F0 to `j` plus U+030C, and upstream and this port agree on all three. Only
U+0130 differs, because `CaseFolding.txt`'s `0130; T; 0069` - a row the file says to exclude by
default - makes upstream fold it to a SINGLE `i` where the default data gives `i` plus U+0307. So
the control holds the LENGTH of the fold fixed and varies only whether a `T` row is consulted,
which "swap it for `h`" does not.

Run::

    python tools/probes/upstream-turkic-from-the-pattern-side.py

The port half is tools/probes/port-turkic-from-the-pattern-side.ps1, run after a Debug build.
"""

import regex

I, F, M, V1 = regex.I, regex.F, regex.M, regex.V1


def show(title, fn):
    try:
        print(f"{title:56}: {ascii(fn())}")
    except Exception as e:  # noqa: BLE001
        print(f"{title:56}: {type(e).__name__}: {ascii(str(e))}")


print(f"regex {regex.__version__}")

# ---------------------------------------------------------------- row 25482
# seed 20260915 row 25482 (interactions, match with partial=True). Upstream answers the zero-width
# match at codepoint 3 having spent TWO substitutions; this port answers it at 2 having spent one.
print()
print("=== seed 20260915 row 25482: the Turkic letter is a member of a `\\L<name>` list")
PAT25 = r"(?(?=\D)[\p{L}||\p{N}])\L<w1>{e<=2}\K"
SUB25 = "ﬀ\r ﬀ"


def row25(first: str):
    """The row with the list's first word's leading U+0130 swapped for `first`."""
    compiled = regex.compile(PAT25, I | F | V1, w1=[first + "ı", "ﬀ"])
    m = compiled.match(SUB25, partial=True)
    return None if m is None else (m.span(), m.fuzzy_counts, m.fuzzy_changes)


show("  as drawn, w1=[U+0130 U+0131, U+FB00]", lambda: row25("İ"))
print("  CONTROL: a first letter whose DEFAULT fold is also longer than one character")
for ch, name in (("ß", "U+00DF -> ss"), ("ﬀ", "U+FB00 -> ff"), ("ǰ", "U+01F0 -> j U+030C")):
    show(f"    w1=[{name} + U+0131, U+FB00]", lambda c=ch: row25(c))
print("  CONTROL: a first letter that folds to ONE character, which is what U+0130 does upstream")
for ch, name in (("h", "h"), ("i", "i")):
    show(f"    w1=[{name} + U+0131, U+FB00]", lambda c=ch: row25(c))
print("  and the folds themselves, as upstream and the default data see them")
for ch in ("İ", "ß", "ﬀ", "ǰ", "h"):
    show(f"    upstream (?i)(?f) U+{ord(ch):04X} against a single `i`",
         lambda c=ch: regex.compile(c, I | F).fullmatch("i"))
    show(f"    upstream (?i)(?f) U+{ord(ch):04X} against `i` + U+0307",
         lambda c=ch: regex.compile(c, I | F).fullmatch("i̇"))

# ---------------------------------------------------------------- row 34508
# seed 20260915 row 34508 (partial-sliced, search with partial=True over an EMPTY slice [5, 5)).
# Upstream answers a zero-width partial; this port answers None - and so does upstream for every
# other leading character whose full fold is longer than one character.
print()
print("=== seed 20260915 row 34508: the Turkic letter is the pattern's leading literal")
SUB34 = "sﬁﬀıİ"


def row34(pat: str):
    m = regex.compile(pat, I | F | M | V1).match(SUB34, 5, 5, partial=True)
    return None if m is None else (m.span(), m.partial)


print("  the row is `match`, not `search`: upstream's `search_start` prefilter is called only when")
print("  searching (upstream/src/_regex.c:11816), so this is its SLOW path answering")
show("  as drawn, ^U+0130\\K\\b", lambda: row34("^İ\\K\\b"))
print("  CONTROL: a leading literal whose DEFAULT fold is also longer than one character")
for ch, name in (("ß", "U+00DF -> ss"), ("ﬀ", "U+FB00 -> ff"), ("ǰ", "U+01F0 -> j U+030C")):
    show(f"    ^{name}\\K\\b", lambda c=ch: row34("^" + c + "\\K\\b"))
print("  CONTROL: a leading literal that folds to ONE character")
for ch in ("h", "i", "ı"):
    show(f"    ^U+{ord(ch):04X}\\K\\b", lambda c=ch: row34("^" + c + "\\K\\b"))
print("  CONTROL: the same letters with no `^`, where upstream reports the partial for every one")
for ch in ("İ", "ß", "ﬀ", "h"):
    show(f"    U+{ord(ch):04X}\\K\\b", lambda c=ch: row34(c + "\\K\\b"))
