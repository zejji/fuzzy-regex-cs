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
print("=== seed 7 row 118133 (verbs, subf), added by S52's ninth sitting 2026-09-15")
print("  Upstream raises IndexError from the TEMPLATE, which is downstream of the disagreement -")
print("  the question is whether `[A-Z]{1,3}` under IGNORECASE reaches the dotless i at 3.")
pat118 = (
    r"[A-Z]{1,3}(?<![a\d](*SKIP))[\w\s]\d*+(?=(*PRUNE))[^a][^a]*(?!(*SKIP)ı)\S"
)
sub118 = "Aİﬀı\r\nS"
show("  upstream subfn (the recorded answer)",
     lambda: regex.compile(pat118, I | M).subfn("{0}{0[-2]}", sub118))
show("  the match behind it",
     lambda: [(m.span(), m.group()) for m in regex.compile(pat118, I | M).finditer(sub118)])
print("  CONTROL: only a range SPANNING `I` reaches the dotless i - 0049; T; 0131. A range that")
print("  stops short of `I`, or starts past it, finds NOTHING AT ALL, which is this port's answer.")
for cls in ("[A-Z]", "[A-Y]", "[A-H]", "[J-Z]"):
    show(f"    {cls}{{1,3}} over the row",
         lambda c=cls: regex.compile(pat118.replace("[A-Z]{1,3}", c + "{1,3}"), I | M).subfn(
             "{0}{0[-2]}", sub118))
print("  CONTROL: replace the U+0131 in the SUBJECT with a non-ASCII letter carrying no `T` row")
print("  and the match goes with it (U+00DF is outside [A-Z] under every folding).")
show("    U+0131 -> U+00DF in the subject",
     lambda: regex.compile(pat118, I | M).subfn("{0}{0[-2]}", sub118.replace("ı", "ß")))

print()
print("=== seed 20260915 row 75528 (interactions, split), added by S52's ninth sitting 2026-09-15")
pat755, sub755 = r"(?p)(?P<g1>\d)??\L<w1>{e<=2:\s}", "İﬁ\r\nı"
lists755 = {"w1": ["a", "ﬁ"]}
show("  upstream split (the recorded answer)",
     lambda: regex.compile(pat755, I | M | regex.V1, **lists755).split(sub755))
show("  the match behind it",
     lambda: [m.span() for m in regex.compile(pat755, I | M | regex.V1, **lists755).finditer(sub755)])
print("  CONTROL: upstream EATS the U+0130 - its first match is (0, 1) and the part is '' - and")
print("  every swap for a letter with no `T` row gives a ZERO-WIDTH first match and puts the")
print("  letter back in the parts, which is this port's answer. Fold LENGTH is not the variable:")
print("  U+00DF, U+FB00 and U+01F0 all fold to two characters and `h` to one, and all four agree.")
for ch, label in (
    ("İ", "as drawn, U+0130"),
    ("ß", "U+0130 -> U+00DF"),
    ("ﬀ", "U+0130 -> U+FB00"),
    ("ǰ", "U+0130 -> U+01F0"),
    ("h", "U+0130 -> h"),
):
    show(f"    {label}",
         lambda c=ch: (lambda rx, s: (rx.split(s), [m.span() for m in rx.finditer(s)]))(
             regex.compile(pat755, I | M | regex.V1, **lists755), sub755.replace("İ", c)))

print()
print("=== seed 20260915 row 88716 (conditionals, sub), added by S52's ninth sitting 2026-09-15")
print("  The Turkic letter is read by a LOOKBEHIND, so NO MATCH SPAN COVERS IT - which is why this")
print("  row has an entry of its own rather than joining the three above.")
pat887 = r"(?r)(?(?<!(?:a|\p{ASCII})+)\d{3}|)(?:(?(?<=ı[\w\s])a\w|(?P<g1>ı))ı|a)"
sub887 = "ııııaa"
show("  upstream subn (the recorded answer)",
     lambda: regex.compile(pat887, I | F).subn(r"\1\1", sub887))
show("  the matches behind it",
     lambda: [m.span() for m in regex.compile(pat887, I | F).finditer(sub887)])
print("  CONTROL: EVERY SWAP MUST STAY NON-ASCII. The pattern's own `(?<!(?:a|\\p{ASCII})+)` reads")
print("  ASCII-ness, so swapping the dotless i for `h` or `i` changes the question instead of")
print("  isolating the `T` row - and both of those DO reproduce upstream's count, for that reason.")
print("  These four are non-ASCII, fold to one character and carry no `T` row; on all four the two")
print("  engines agree, and only the dotless i diverges.")
for ch, label in (
    ("ı", "as drawn, U+0131"),
    ("ñ", "U+0131 -> U+00F1"),
    ("ǧ", "U+0131 -> U+01E7"),
    ("ĥ", "U+0131 -> U+0125"),
    ("å", "U+0131 -> U+00E5"),
):
    show(f"    {label}",
         lambda c=ch: regex.compile(pat887.replace("ı", c), I | F).subn(
             r"\1\1", sub887.replace("ı", c)))
print("  CONTROL: drop IGNORECASE and the extra match goes, so it is the case data that supplies it")
show("    IGNORECASE dropped", lambda: regex.compile(pat887, F).subn(r"\1\1", sub887))

print()
print("=== the minimal forms the entry's rows are pinned as, one per span-less shape")
show("  subn('(?i)I','X','\\u0131')", lambda: regex.compile("(?i)I").subn("X", "ı"))
show("  split('(?i)(I)','a\\u0131b')", lambda: regex.compile("(?i)(I)").split("aıb"))
show("  subfn('(?i)I','{1}','\\u0131')", lambda: regex.compile("(?i)I").subfn("{1}", "ı"))
print("  and the refusal twin, where U+0131 pairs with ITSELF and no `T` row is consulted")
show("  subn('(?i)\\u0131','X','\\u0131')", lambda: regex.compile("(?i)ı").subn("X", "ı"))

print()
print("=== the MECHANISM of the lookaround row, isolated to one line: the SET")
print("  A MULTI-MEMBER set holding `\\p{ASCII}` reaches characters whose CASE PARTNER is ASCII.")
print("  A one-member set does not, which is the cell that says what the rule is: `[\\p{ASCII}]`")
print("  reaches nothing and `[\\p{ASCII}\\p{ASCII}]` - the same member twice, so exactly the same")
print("  characters - reaches them all. `[ab]` reaching nothing says it is the PROPERTY's members")
print("  expanding rather than sets in general.")
print("  That expansion is NOT the defect: it reaches the ordinary `C` rows too and this port")
print("  agrees on those. The `T` rows are the whole of the difference.")
print("  (Two drafts of the oracle entry were killed by measurement, both by the blind review: the")
print("  first blamed the INNER lookbehind `(?<=U+0131[\\w\\s])`, and neutering that leaves the")
print("  engines disagreeing; the second said a set expands its members, and `[\\p{ASCII}]` below")
print("  refutes it.)")
for spelling in (
    r"[a\p{ASCII}]",
    r"[\p{ASCII}\p{ASCII}]",
    r"[\p{ASCII}z]",
    r"[\p{ASCII}]",
    r"\p{ASCII}",
    r"a",
    r"[a]",
    r"[ab]",
):
    cells = []
    for ch in ("ı", "İ", "K", "ſ", "Å", "ñ"):
        hit = regex.compile(spelling, I | F, cache_pattern=False).match(ch)
        cells.append(f"U+{ord(ch):04X}={'M' if hit else '-'}")
    print(f"    {spelling:16} {' '.join(cells)}")
print("    U+0131 0049;T;0131   U+0130 0130;T;0069   <- the two Turkic rows, port REFUSES")
print("    U+212A 212A;C;006B   U+017F 017F;C;0073   <- ordinary rows, port AGREES")
print("    U+00C5 and U+00F1 have non-ASCII partners, so neither engine reaches them")
print("  Port half, measured 2026-09-15 with `pwsh -File tools/run-oracle.ps1 -Rows` over the")
print("  42-cell grid this prints: 36 cells AGREE, and the only 6 that diverge are U+0131 and")
print("  U+0130 against the three multi-member spellings.")

print()
print("=== a lookbehind is not itself a way round the refusal - the plain `I` is what pairs")
for subject in ("Iı", "ıı", "iı", "İı"):
    show(f"  search('(?i)(?<=\\u0131)\\u0131', {ascii(subject)})",
         lambda s=subject: regex.compile("(?i)(?<=ı)ı").search(s))
