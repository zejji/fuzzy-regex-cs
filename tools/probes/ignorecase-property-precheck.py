#!/usr/bin/env python
"""Oracle row 20260927:3732: a case-insensitive \\p{Ll} refuses a partner-less capital when the
compiler hoists it into a first-set precheck.

Run: python tools/probes/ignorecase-property-precheck.py

Part 1 isolates it on upstream (`import regex`). The row is `(?r)(?P<g1>\\p{ASCII}{2})(\\p{Ll}+?)??`
with flags 10 (IGNORECASE | MULTILINE) over '\\n\\r' U+1D518 U+1F600. Upstream's bare `(?i)\\p{Ll}`
accepts U+1D518 (matches_PROPERTY_IGN, upstream/src/_regex.c:2958-2966, "any cased letter"), but the
pattern's first item is optional, so _check_firstset (_regex_core.py) compiles a
SET_UNION_IGN precheck of every possible first item, and a PROPERTY member of a case-insensitive
set is answered by matches_member_ign (_regex.c:3085-3107), which asks the plain property of each
case variant. U+1D518 has no case variant, so the precheck refuses the one position the match
needs. Ledger entry 35 C is that set rule; this is its door through a set the pattern never wrote.

Part 2 surveys the same questions in PCRE2 (pip pcre2), Perl, node and Python's re. .NET is
tools/probes/ignorecase-property-precheck.cs.
"""

import json
import re
import subprocess

import pcre2
import regex

LETTERS = {"U+2102": chr(0x2102), "U+1D518": chr(0x1D518), "A": "A", "a": "a"}


def span(m):
    return m.span() if m else None


def part1():
    print("== upstream regex", regex.__version__)
    # The row itself, as the oracle asked it.
    row = r"(?r)(?P<g1>\p{ASCII}{2})(\p{Ll}+?)??"
    subject = "\n\r\U0001d518\U0001f600"
    print("row sub:", ascii(regex.sub(row, "\U0001f600\\1]", subject, flags=regex.I | regex.M)))
    print("row sub without IGNORECASE:", ascii(regex.sub(row, "\U0001f600\\1]", subject, flags=regex.M)))
    # The ladder, one change a line. '?' makes the first item optional, so a precheck is built.
    rows = [
        (r"(?i)\p{Ll}", "{}"),  # bare: any cased letter
        (r"(?i)[\p{Ll}x]", "{}"),  # an explicit set: case closure
        (r"(?i)\p{Ll}?a{2}", "{}aa"),  # forward, precheck [\p{Ll}a]
        (r"(?i)\p{Ll}?aa", "{}aa"),  # a literal tail: the precheck is built too
        (r"(?ri)a{2}\p{Ll}?", "aa{}"),  # reverse, the row's shape
        (r"(?ri)a{2}\p{Ll}", "aa{}"),  # not optional: no precheck
        (r"(?i)\p{Lu}?a{2}", "{}aa"),  # U+1D518 is Lu, so the plain property holds
    ]
    for pattern, shape in rows:
        cells = [f"{name}:{span(regex.search(pattern, shape.format(ch)))}" for name, ch in LETTERS.items()]
        print(f"{pattern:22} {'  '.join(cells)}")
    # The precheck is in the compiled code: SET_UNION_IGN_REV (67) over CHARACTER 'a' and PROPERTY Ll.
    import regex._regex as native

    seen = []
    original = native.compile

    def spy(pattern, flags, code, *rest, **kw):
        seen.append(code[:9])
        return original(pattern, flags, code, *rest, **kw)

    native.compile = spy
    regex.purge()  # the ladder above compiled it already, and a cached pattern is not recompiled
    regex.compile(r"(?ri)a{2}\p{Ll}?")
    native.compile = original
    print("compiled head of (?ri)a{2}\\p{Ll}?:", seen)

    # The ablation: the same upstream with no first-set precheck compiled, and nothing else changed.
    import regex._main as main

    compile_firstset = main._compile_firstset
    main._compile_firstset = lambda info, fs: []
    regex.purge()
    print("-- upstream with _compile_firstset returning no precheck")
    print("row sub:", ascii(regex.sub(row, "\U0001f600\\1]", subject, flags=regex.I | regex.M)))
    for pattern, shape in rows:
        cells = [f"{name}:{span(regex.search(pattern, shape.format(ch)))}" for name, ch in LETTERS.items()]
        print(f"{pattern:22} {'  '.join(cells)}")
    main._compile_firstset = compile_firstset
    regex.purge()


def perl(pattern, ch):
    code = "binmode STDIN,':utf8'; my $s = <STDIN>; $s =~ s/\\s+\\z//; print(($s =~ /%s/i) ? 1 : 0)" % pattern
    out = subprocess.run(["perl", "-CS", "-e", code], input=ch + "\n", capture_output=True, text=True, encoding="utf-8")
    return out.stdout.strip() or out.stderr.strip()[:60]


def node(pattern, ch):
    code = "const s=require('fs').readFileSync(0,'utf8').trimEnd();process.stdout.write(%s.test(s)?'1':'0')" % pattern
    out = subprocess.run(["node", "-e", code], input=ch, capture_output=True, text=True, encoding="utf-8")
    return out.stdout.strip() or out.stderr.strip()[:60]


def part2():
    print("== survey: does a case-insensitive \\p{Ll} (bare, then in a set) accept each letter?")
    table = {}
    for name, ch in LETTERS.items():
        table[name] = {
            "upstream bare": int(bool(regex.fullmatch(r"(?i)\p{Ll}", ch))),
            "upstream set": int(bool(regex.fullmatch(r"(?i)[\p{Ll}x]", ch))),
            "pcre2 bare": int(bool(pcre2.compile(r"^\p{Ll}$", pcre2.I).search(ch))),
            "pcre2 set": int(bool(pcre2.compile(r"^[\p{Ll}x]$", pcre2.I).search(ch))),
            "perl bare": perl(r"^\p{Ll}$", ch),
            "perl set": perl(r"^[\p{Ll}x]$", ch),
            "node bare": node(r"/^\p{Ll}$/iu", ch),
            "node set": node(r"/^[\p{Ll}x]$/iu", ch),
        }
    print(json.dumps(table, indent=1))
    try:
        re.compile(r"\p{Ll}", re.I)
    except re.error as e:
        print("python re:", e)


if __name__ == "__main__":
    part1()
    part2()
