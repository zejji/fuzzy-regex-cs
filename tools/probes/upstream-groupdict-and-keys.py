"""Upstream's ``groupdict`` and ``capturesdict`` for the patterns S53b's dictionary view is pinned
against.

S53b gives ``GroupCollection`` an ``IReadOnlyDictionary<string, Group>`` face. Its shape is .NET's
(see ``tools/probes/dotnet-groupcollection-dictionary.ps1``), but the VALUES it reports have to be
upstream's, so the expected value of every new gap-test assertion comes from here rather than from
this port's own output.

Run:  python tools/probes/upstream-groupdict-and-keys.py
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

CASES = (
    # (pattern, subject, how) - 'match' is regex.match, 'search' is regex.search.
    ("(?P<first>first) (?P<second>second)", "first second", "match"),
    (r"(?&routine)(?(DEFINE)(?<routine>.))", "a", "search"),
    (r"(?(DEFINE)(?<func>.))(?&func)", "abc", "search"),
    (r"(?<a>a)(b)(?<c>c)?", "ab", "match"),
    (r"(?<x>a)|(?<y>b)", "b", "search"),
    (r"(?<r>[ab])+", "aba", "match"),
)

print("regex", regex.__version__)
for pattern, subject, how in CASES:
    m = (regex.match if how == "match" else regex.search)(pattern, subject)
    print()
    print(f"{how}({pattern!r}, {subject!r})")
    print("  groupindex   =", dict(m.re.groupindex))
    print("  groupdict    =", m.groupdict())
    print("  capturesdict =", m.capturesdict())
    print("  group count  =", m.re.groups)
