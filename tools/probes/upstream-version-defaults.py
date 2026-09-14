"""What actually differs between upstream's VERSION0 and VERSION1, measured.

S50b flips this port's compile-time default from VERSION0 to VERSION1, so the first thing the
slice needs is a re-established list of what that changes. Upstream's README lists four
differences; two of them stopped existing when VERSION0 was brought in line with Python `re` 3.7+,
and the README does not list the fifth this probe finds.

Run:  python tools/probes/upstream-version-defaults.py
"""

import sys

import regex


def show(label, fn):
    try:
        print(f"  {label}: {fn()!r}")
    except Exception as exc:  # noqa: BLE001 - an error IS the observation here
        print(f"  {label}: {type(exc).__name__}: {exc}")


def main():
    print(f"regex {regex.__version__}, CPython {sys.version.split()[0]}")
    print(f"regex.DEFAULT_VERSION = {regex.DEFAULT_VERSION!r} "
          f"(VERSION0={regex.VERSION0!r}, VERSION1={regex.VERSION1!r})")
    print(f"resolves to: {'VERSION0' if regex.DEFAULT_VERSION == regex.VERSION0 else 'VERSION1'}")

    print("\n1. zero-width split / sub (README: differs; measured: identical)")
    show("V0 split('', 'abc')", lambda: regex.split(r"(?V0)", "abc"))
    show("V1 split('', 'abc')", lambda: regex.split(r"(?V1)", "abc"))
    show("V0 sub('', '-', 'abc')", lambda: regex.sub(r"(?V0)", "-", "abc"))
    show("V1 sub('', '-', 'abc')", lambda: regex.sub(r"(?V1)", "-", "abc"))
    show("V0 sub('x*', '-', 'abxd')", lambda: regex.sub(r"(?V0)x*", "-", "abxd"))
    show("V1 sub('x*', '-', 'abxd')", lambda: regex.sub(r"(?V1)x*", "-", "abxd"))

    print("\n2. inline flag scoping and turn-off (README: differs; measured: identical)")
    show("V0 '(?i)a(?-i)a' vs 'AA'", lambda: bool(regex.match(r"(?V0)(?i)a(?-i)a", "AA")))
    show("V1 '(?i)a(?-i)a' vs 'AA'", lambda: bool(regex.match(r"(?V1)(?i)a(?-i)a", "AA")))
    show("V0 '(?:(?i)a)a' vs 'Aa'", lambda: bool(regex.match(r"(?V0)(?:(?i)a)a", "Aa")))
    show("V1 '(?:(?i)a)a' vs 'Aa'", lambda: bool(regex.match(r"(?V1)(?:(?i)a)a", "Aa")))
    show("V0 '(?:(?i)a)a' vs 'AA'", lambda: bool(regex.match(r"(?V0)(?:(?i)a)a", "AA")))
    show("V1 '(?:(?i)a)a' vs 'AA'", lambda: bool(regex.match(r"(?V1)(?:(?i)a)a", "AA")))

    print("\n3. nested sets and set operations (README: differs; measured: differs)")
    show("V0 '[[a-z]--[aeiou]]' vs 'b'", lambda: regex.findall(r"(?V0)[[a-z]--[aeiou]]", "b-]x"))
    show("V1 '[[a-z]--[aeiou]]' vs 'b'", lambda: regex.findall(r"(?V1)[[a-z]--[aeiou]]", "b-]x"))
    show("V0 '[[]' vs '['", lambda: regex.findall(r"(?V0)[[]", "[a"))
    show("V1 '[[]' vs '['", lambda: regex.findall(r"(?V1)[[]", "[a"))
    show("V0 '[a[b]' vs 'ab['", lambda: regex.findall(r"(?V0)[a[b]", "ab["))
    show("V1 '[a[b]' vs 'ab['", lambda: regex.findall(r"(?V1)[a[b]", "ab["))

    print("\n4. case-insensitive folding (README: differs; measured: differs)")
    show("V0 (?i) 'ss' vs '\\N{LATIN SMALL LETTER SHARP S}'", lambda: bool(regex.match(r"(?V0)(?i)ss", "ß")))
    show("V1 (?i) 'ss' vs '\\N{LATIN SMALL LETTER SHARP S}'", lambda: bool(regex.match(r"(?V1)(?i)ss", "ß")))
    show("V0 (?i) 'fi' vs '\\N{LATIN SMALL LIGATURE FI}'", lambda: bool(regex.match(r"(?V0)(?i)fi", "ﬁ")))
    show("V1 (?i) 'fi' vs '\\N{LATIN SMALL LIGATURE FI}'", lambda: bool(regex.match(r"(?V1)(?i)fi", "ﬁ")))
    show("V0 (?i) 'k' vs KELVIN SIGN", lambda: bool(regex.match(r"(?V0)(?i)k", "K")))
    show("V1 (?i) 'k' vs KELVIN SIGN", lambda: bool(regex.match(r"(?V1)(?i)k", "K")))
    show("V1 (?i)(?-f) 'ss' vs SHARP S", lambda: bool(regex.match(r"(?V1)(?i)(?-f)ss", "ß")))
    show("V0 (?i)(?f) 'ss' vs SHARP S", lambda: bool(regex.match(r"(?V0)(?i)(?f)ss", "ß")))

    print("\n5. a backreference to an OPEN group (NOT in the README; measured: differs)")
    show("V0 '(a\\1)'", lambda: bool(regex.match(r"(?V0)(a\1)", "a")))
    show("V1 '(a\\1)'", lambda: bool(regex.match(r"(?V1)(a\1)", "a")))
    show("V0 '(?P<x>a(?P=x))'", lambda: bool(regex.match(r"(?V0)(?P<x>a(?P=x))", "a")))
    show("V1 '(?P<x>a(?P=x))'", lambda: bool(regex.match(r"(?V1)(?P<x>a(?P=x))", "a")))

    print("\n6. what the version bit does to Pattern.flags")
    show("V0 flags & FULLCASE", lambda: bool(regex.compile(r"(?V0)a").flags & regex.FULLCASE))
    show("V1 flags & FULLCASE", lambda: bool(regex.compile(r"(?V1)a").flags & regex.FULLCASE))
    show("default flags & VERSION1", lambda: bool(regex.compile(r"a").flags & regex.VERSION1))
    show("default flags & VERSION0", lambda: bool(regex.compile(r"a").flags & regex.VERSION0))


if __name__ == "__main__":
    main()
