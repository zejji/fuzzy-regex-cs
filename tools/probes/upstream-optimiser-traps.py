"""The upstream answers every S54 optimiser-trap pin asserts.

S54 pins the edge cases an optimiser is tempted to special-case. Every expected value in
`tests/FuzzyRegex.Tests/Gaps/Engine/OptimiserTrapsTests.cs` that upstream can express comes from
this script, per the port-slice provenance rule: an expectation copied from this port's own output
is not evidence, because the port is what is under test.

Run:  python tools/probes/upstream-optimiser-traps.py

The 1 MB subjects are built to the same recipe as `bench/FuzzyRegex.Benchmarks/Corpus.cs` and the
test file, so the pins pin exactly what the benchmarks measure.
"""

import regex

V = regex.VERSION1

SENTENCE = "the quick brown fox jumps over the lazy dog "
MEGABYTE = 1024 * 1024


def pad(tail, size=MEGABYTE):
    """Build a subject of at least `size` filler characters plus `tail`, as Corpus.Pad does."""
    out = []
    length = 0
    while length < size:
        out.append(SENTENCE)
        length += len(SENTENCE)
    out.append(tail)
    return "".join(out)


LONG = pad("a needle in a haystack.")
LONG_PARTIAL = pad("a needle in a hay")
SHORT = SENTENCE + "and finds a needle."
DENSE = pad("a needle in a haystack.", 100 * 1024)


def spans(pattern, subject, flags=0):
    return [m.span() for m in regex.finditer(pattern, subject, flags | V)]


def show(label, value):
    print(f"{label}: {value!r}")


print(f"# regex {regex.__version__}, VERSION1 explicit")
print(f"# len(LONG)={len(LONG)} len(LONG_PARTIAL)={len(LONG_PARTIAL)} len(SHORT)={len(SHORT)}")

print("\n## A. Zero-width and empty matches, every position including the end")
show("'' on 'abc'", spans("", "abc"))
show("'' on ''", spans("", ""))
show("a* on 'abc'", spans("a*", "abc"))
show("a* on 'baaac'", spans("a*", "baaac"))
show(r"\b on 'ab cd'", spans(r"\b", "ab cd"))
show("(?=b) on 'abcb'", spans("(?=b)", "abcb"))
show("x* on 'abc' (never consumes)", spans("x*", "abc"))
show("sub '' -> '-' on 'abc'", regex.sub("", "-", "abc", flags=V))
show("split '' on 'abc'", regex.split("", "abc", flags=V))
show("split 'a*' on 'baaac'", regex.split("a*", "baaac", flags=V))

print("\n## B. Anchors under flag combinations, subject 'a\\nb\\na'")
subject = "a\nb\na"
for name, flags in (
    ("none", 0),
    ("MULTILINE", regex.M),
    ("DOTALL", regex.S),
    ("MULTILINE|DOTALL", regex.M | regex.S),
):
    show(f"^a  [{name}]", spans("^a", subject, flags))
    show(f"a$  [{name}]", spans("a$", subject, flags))
    show(f"\\Aa [{name}]", spans(r"\Aa", subject, flags))
    show(f"a\\Z [{name}]", spans(r"a\Z", subject, flags))
show("a$ on 'a\\n' [none]", spans("a$", "a\n"))
show("a$ on 'a\\n' [MULTILINE]", spans("a$", "a\n", regex.M))
show(r"a\Z on 'a\n' [none]", spans(r"a\Z", "a\n"))
show("^$ on '' [none]", spans("^$", ""))
show("^$ on '\\n' [MULTILINE]", spans("^$", "\n", regex.M))

print("\n## C. Pathological backtracking: both classic shapes over a run of 'a'")
for pattern in (r"(a+)+b", r"(a|a)*b"):
    for n in (16, 22, 24):
        show(f"{pattern} on 'a'*{n}", regex.search(pattern, "a" * n, flags=V))
    show(
        f"{pattern} on 'a'*24 + 'b'",
        regex.search(pattern, "a" * 24 + "b", flags=V).span(),
    )

print("\n## D. 1 MB subjects, the benchmark workloads")
show("search 'needle' in LONG", regex.search("needle", LONG, flags=V).span())
show("count '[a-z]{3}[^a-z]' in LONG", len(spans("[a-z]{3}[^a-z]", LONG)))
show(
    "count 'QUICK' IGNORECASE|FULLCASE in LONG",
    len(spans("QUICK", LONG, regex.I | regex.F)),
)
show("search '(?r)zebra' in LONG", regex.search("(?r)zebra", LONG, flags=V))
words = spans(r"\w+", LONG)
show(r"count '\w+' in LONG", len(words))
show(r"first two '\w+' spans in LONG", words[:2])
show(r"last '\w+' span in LONG", words[-1])
show(
    "len(sub '(\\w+) (\\w+)' -> '\\\\2 \\\\1') on LONG",
    len(regex.sub(r"(\w+) (\w+)", r"\2 \1", LONG, flags=V)),
)
pieces = regex.split(r"\w+", LONG, flags=V)
show(r"len(split '\w+') on LONG", len(pieces))
show(r"first three split pieces", pieces[:3])
show(r"last split piece", pieces[-1])
partial = regex.compile(r"a needle in a haystack\.", flags=V).search(
    LONG_PARTIAL, partial=True
)
show("partial search in LONG_PARTIAL", (partial.span(), partial.partial))
show("search 'needle' in SHORT", regex.search("needle", SHORT, flags=V).span())

print("\n## E. The 100 KB subject, where the full lazy walk is affordable")
dense_words = spans(r"\w+", DENSE)
show("len(DENSE)", len(DENSE))
show(r"count '\w+' in DENSE", len(dense_words))
show(r"first two '\w+' spans in DENSE", dense_words[:2])
show(r"last '\w+' span in DENSE", dense_words[-1])
