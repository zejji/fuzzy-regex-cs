"""Asks upstream `regex` for the answer to every matching case the demo's JSON contract pins.

S70. `demo/FuzzyRegex.Demo.Wasm/DemoEngine.cs` turns a pattern, a flag list and a subject into one
JSON string for the browser demo's Web Worker. The spans and fuzzy counts inside that JSON are
matching answers, so every expected value the contract tests hardcode needs the provenance the
port-slice rule asks for (owner, 2026-09-15): it comes from a real upstream run, not from this
port's own output, because the port is what the tests are testing.

Run it, and paste the block it prints into the comment header of
`tests/FuzzyRegex.Tests/Gaps/Demo/DemoEngineContractTests.cs`:

    C:/Users/<user>/source/repos/fuzzy-regex-cs/.venvs/regex-2026.9.10/Scripts/python.exe \
        tools/probes/demo-json-contract-expectations.py

The venv lives in the MAIN checkout, not in a worktree - `.venvs/` is gitignored, so a worktree
does not have one. That absolute path is the whole reason this line is here.

INDICES. Python counts codepoints and .NET counts UTF-16 code units, so every span is printed
twice where they differ. The demo's JSON reports UTF-16, because the page slices a JavaScript
string with it and JavaScript strings are UTF-16 too; the astral case below exists to pin exactly
that, and it is the one case where the two columns differ.

NOT ASKED HERE, and why. The timeout, the subject-length cap and the unknown-flag rejection have
no upstream counterpart at all - they are this demo's own trust-boundary contract, not a parity
claim - so the tests assert only the shape of the error JSON for those three.
"""

import sys

import regex


def utf16_span(subject: str, start: int, end: int) -> tuple[int, int]:
    """The (index, length) .NET reports for a codepoint span of a Python string."""
    index = len(subject[:start].encode("utf-16-le")) // 2
    length = len(subject[start:end].encode("utf-16-le")) // 2
    return index, length


def show(subject: str, start: int, end: int) -> str:
    if start < 0:
        return "no match"
    index, length = utf16_span(subject, start, end)
    codepoints = f"codepoints index={start} length={end - start}"
    return f"utf16 index={index} length={length}   ({codepoints})"


CASES = [
    (
        "groups-and-captures",
        r'compile(r"(?<word>\w+)\s+(\w+)").search("hello world")',
        "hello world",
        lambda: regex.compile(r"(?<word>\w+)\s+(\w+)").search("hello world"),
    ),
    (
        "repeated-captures",
        r'compile(r"(\w)+").search("abc")',
        "abc",
        lambda: regex.compile(r"(\w)+").search("abc"),
    ),
    (
        "optional-group-that-did-not-take-part",
        r'compile(r"(a)|(b)").search("b")',
        "b",
        lambda: regex.compile(r"(a)|(b)").search("b"),
    ),
    (
        "fuzzy-with-counts",
        r'compile(r"(?:kitten){e<=3}").search("sitting")',
        "sitting",
        lambda: regex.compile(r"(?:kitten){e<=3}").search("sitting"),
    ),
    (
        "fuzzy-per-error-type",
        r'compile(r"(?:foobar){i<=1,d<=1,s<=1}").search("xfoobat")',
        "xfoobat",
        lambda: regex.compile(r"(?:foobar){i<=1,d<=1,s<=1}").search("xfoobat"),
    ),
    (
        "fuzzy-two-deletions-at-one-place",
        r'compile(r"(?:abcdef){d<=2}").search("abef")',
        "abef",
        lambda: regex.compile(r"(?:abcdef){d<=2}").search("abef"),
    ),
    (
        "astral-subject",
        r'compile(r"\p{Deseret}+").search("ab\U00010400\U00010401cd")',
        "ab\U00010400\U00010401cd",
        lambda: regex.compile(r"\p{Deseret}+").search("ab\U00010400\U00010401cd"),
    ),
]

ALL_MATCHES = [
    (
        "every-match",
        r'compile(r"\d+").finditer("a1 b22 c333")',
        "a1 b22 c333",
        lambda: regex.compile(r"\d+").finditer("a1 b22 c333"),
    ),
    (
        "ignorecase-flag",
        r'compile(r"ab", regex.IGNORECASE).finditer("AB ab Ab")',
        "AB ab Ab",
        lambda: regex.compile(r"ab", regex.IGNORECASE).finditer("AB ab Ab"),
    ),
]

PARSE_ERRORS = [
    ("unbalanced-parenthesis", r"(", lambda: regex.compile(r"(")),
    ("nothing-to-repeat", r"*", lambda: regex.compile(r"*")),
]


def main() -> int:
    print(f"regex {regex.__version__}, Python {sys.version.split()[0]}")
    print()

    for name, call, subject, run in CASES:
        m = run()
        print(f"{name}: {call}")
        if m is None:
            print("    no match")
            continue
        print(f"    match       {show(subject, m.start(), m.end())}")
        print(f"    fuzzy_counts (sub, ins, del) = {m.fuzzy_counts}")
        print(f"    fuzzy_changes (sub, ins, del) = {m.fuzzy_changes}")
        # No subject positions printed here. Upstream reports a deletion where the missing character
        # would sit in a string that had every deletion put back, so the i-th is shifted by i
        # (match_fuzzy_changes, _regex.c:20555-20558), and the demo un-shifts them to draw its
        # caret in the subject on screen. Un-shifting them here too would make this probe
        # re-implement `DemoEngine.Edits`
        # and then check the port against its own algorithm, which is not evidence of anything: the
        # subject position belongs in the test that asserts it, derived from the subject by hand.
        for number in range(0, (m.re.groups or 0) + 1):
            name_of = {v: k for k, v in m.re.groupindex.items()}.get(number)
            label = f"{number}" if name_of is None else f"{number} ({name_of})"
            span = m.span(number)
            print(f"    group {label:<10} {show(subject, span[0], span[1])}")
            captures = m.captures(number)
            starts = m.starts(number)
            ends = m.ends(number)
            if len(captures) != 1:
                for start, end in zip(starts, ends, strict=True):
                    print(f"        capture {show(subject, start, end)}")
        print()

    for name, call, subject, run in ALL_MATCHES:
        print(f"{name}: {call}")
        for m in run():
            print(f"    match       {show(subject, m.start(), m.end())}")
        print()

    for name, pattern, run in PARSE_ERRORS:
        print(f"{name}: compile({pattern!r})")
        try:
            run()
        except Exception as exc:  # noqa: BLE001 - the message IS the answer being recorded
            print(f"    {type(exc).__name__}: {exc}")
        else:
            print("    no error")
        print()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
