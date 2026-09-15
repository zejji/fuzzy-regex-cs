"""The `turkic-default-folding` rows whose answers carry no spans, and the controls that judge them.

S52, 2026-09-15. The three-seed 2000-row wave of commit 58977bb was RED at all three seeds; two of
the three rows were this family in a `subf` and a `split`, where upstream's answer is an exception
and a list of parts and so holds no match position for `ExpectedDivergences` to read. S52's second
sitting added a third, seed 4242 row 24256 of the 2000-row wave of commit 407c0cb.

What this probe shows is that each row IS the `T` rows of `CaseFolding.txt` and not something else,
by taking the Turkic pairing away and watching the divergence go with it. Run:

    python tools/probes/upstream-turkic-without-spans.py

The port half is tools/probes/port-turkic-without-spans.ps1, run after a Debug build.
"""

import regex

I, F, M = regex.I, regex.F, regex.M


def show(title, fn):
    try:
        print(f"{title:52}: {ascii(fn())}")
    except Exception as e:  # noqa: BLE001 - the exception IS the recorded answer on one of these rows
        print(f"{title:52}: {type(e).__name__}: {ascii(str(e))}")


print(f"regex {regex.__version__}")

print()
print("=== seed 7 row 29165 (conditionals, subf): upstream raises WHILE MATCHING, so no span")
pat29, sub29 = r"(?r)(?(?!s)[A-Z]{2}|(S))$", "s\rSı"
show("  upstream subfn (the recorded answer)",
     lambda: regex.compile(pat29, I | F | M).subfn("{0[2]}", sub29, count=3))
show("  the match behind it",
     lambda: [(m.span(), m.group()) for m in regex.compile(pat29, I | F | M).finditer(sub29)])
print("  CONTROL: only a range SPANNING `I` reaches the dotless i - 0049; T; 0131")
for cls in ("[A-Z]", "[A-Y]", "[A-H]", "[J-Z]"):
    show(f"    {cls} vs U+0131", lambda c=cls: regex.compile(c, I | F).match("ı"))
print("  CONTROL: take the U+0131 away and the match goes too")
show("    same row with U+0131 -> '.'",
     lambda: [(m.span(), m.group()) for m in regex.compile(pat29, I | F | M).finditer("s\rS.")])

print()
print("=== seed 4242 row 24416 (interactions, split): upstream answers a list of parts, so no span")
pat24, sub24 = r"^(?:ﬁİ){e<=2:\S}([^a-f]+)$", "İİﬁ ﬁﬀ"
show("  upstream split (the recorded answer)",
     lambda: regex.compile(pat24, I | F).split(sub24, maxsplit=3))
show("  the match behind it",
     lambda: (lambda m: (m.span(), m.span(1), m.fuzzy_counts))(regex.compile(pat24, I | F).search(sub24)))
print("  CONTROL: swap U+0130 for a letter with no `T` row and upstream lands on this port's span")
for pat, sub, label in [
    (r"(?:ﬁİ){e<=2:\S}", "İİﬁ", "as drawn, U+0130"),
    (r"(?:ﬁh){e<=2:\S}", "hhﬁ", "U+0130 -> h"),
    (r"(?:ﬁÅ){e<=2:\S}", "ÅÅﬁ", "U+0130 -> U+00C5"),
]:
    show(f"    {label}", lambda p=pat, s=sub: (lambda m: (m.span(), m.group(), m.fuzzy_counts))(
        regex.compile(p, I | F).match(s)))

print()
print("=== seed 4242 row 24256 (interactions, split), added by S52's second sitting 2026-09-15")
pat42, sub42 = r"\b(?P<g1>[^a-f])*?(?P<g2>[A-Z]{1}?)", "ßßıı"
show("  upstream split (the recorded answer)",
     lambda: regex.compile(pat42, I | F).split(sub42))
show("  the match behind it",
     lambda: [(m.span(), m.span(2)) for m in regex.compile(pat42, I | F).finditer(sub42)])
print("  CONTROL: swap the two U+0131 for a letter whose DEFAULT fold reaches A-Z and the")
print("  divergence goes with it - upstream splits four ways and so does this port, because the")
print("  `[A-Z]` is now matching something every engine agrees it matches")
show("    U+0131 -> z", lambda: regex.compile(pat42, I | F).split("ßßzz"))
print("  CONTROL: again only a range SPANNING `I` reaches the dotless i - 0049; T; 0131")
for cls in ("[A-Z]", "[A-Y]", "[A-H]", "[J-Z]"):
    show(f"    {cls} vs U+0131", lambda c=cls: regex.compile(c, I | F).match("ı"))

print()
print("=== the minimal forms the entry's rows are pinned as, one per span-less shape")
show("  subn('(?i)I','X','\\u0131')", lambda: regex.compile("(?i)I").subn("X", "ı"))
show("  split('(?i)(I)','a\\u0131b')", lambda: regex.compile("(?i)(I)").split("aıb"))
show("  subfn('(?i)I','{1}','\\u0131')", lambda: regex.compile("(?i)I").subfn("{1}", "ı"))
print("  and the refusal twin, where U+0131 pairs with ITSELF and no `T` row is consulted")
show("  subn('(?i)\\u0131','X','\\u0131')", lambda: regex.compile("(?i)ı").subn("X", "ı"))
