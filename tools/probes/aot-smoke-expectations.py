"""Asks upstream `regex` for the answer to every case the Native AOT smoke app asserts.

S53. The smoke app (`samples/FuzzyRegex.AotSmoke`) is the dynamic half of the AOT gate: a
published consumer binary that asserts real answers, so a table the ILCompiler dropped or a
reflection-free path that only works under the JIT shows up as a wrong answer rather than as
silence. Every expected value it hardcodes therefore needs the same provenance a gap test's does
(port-slice rule, owner 2026-09-15): it comes from a real upstream run, not from this port's own
output, because the port is what the smoke app is testing.

Run it, and paste the block it prints into the smoke app's comment header:

    .venvs/regex-2026.9.10/Scripts/python.exe tools/probes/aot-smoke-expectations.py

INDICES. Python counts codepoints and .NET counts UTF-16 code units, so every span is printed
twice where they differ; the smoke app asserts the UTF-16 column. Subjects are BMP-only apart
from the one non-BMP case, which is there precisely to exercise the difference.

NOT ASKED HERE, and why. `MatchTimeout` has no upstream counterpart - upstream's own timeout is
a `regex.Regex` compile-time argument absent from its match API - so the smoke app's timeout case
is a .NET API contract, not a parity claim, and it asserts only that the exception type fires.
"""

import sys

import regex

CASES = [
    # (area, description, callable returning the answer)
    ("literals", r'search("world", "hello world")', lambda: regex.search(r"world", "hello world")),
    ("classes", r'search("[aeiou]+", "queueing")', lambda: regex.search(r"[aeiou]+", "queueing")),
    (
        "unicode-properties",
        r'search(r"\p{Greek}+", "abcαβγdef")',
        lambda: regex.search(r"\p{Greek}+", "abcαβγdef"),
    ),
    (
        "unicode-properties-non-bmp",
        r'search(r"\p{Deseret}+", "ab\U00010400\U00010401cd")',
        lambda: regex.search(r"\p{Deseret}+", "ab\U00010400\U00010401cd"),
    ),
    (
        "character-names",
        r'search(r"\N{GREEK SMALL LETTER ALPHA}", "xαy")',
        lambda: regex.search(r"\N{GREEK SMALL LETTER ALPHA}", "xαy"),
    ),
    (
        "full-case-folding",
        r'search(r"(?fi)fi", "aﬁb")  # U+FB01 LATIN SMALL LIGATURE FI',
        lambda: regex.search(r"(?fi)fi", "aﬁb"),
    ),
    (
        "named-lists",
        r'compile(r"\L<animals>", animals=["cat", "dog"]).search("a dog here")',
        lambda: regex.compile(r"\L<animals>", animals=["cat", "dog"]).search("a dog here"),
    ),
    (
        "lookaround",
        r'sub(r"(?<=\d)(?=(?:\d{3})+$)", ",", "1234567")',
        lambda: regex.sub(r"(?<=\d)(?=(?:\d{3})+$)", ",", "1234567"),
    ),
    (
        "recursion",
        r'search(r"\((?:[^()]++|(?R))*+\)", "x(a(b)c)y")',
        lambda: regex.search(r"\((?:[^()]++|(?R))*+\)", "x(a(b)c)y"),
    ),
    (
        "conditionals",
        r'search(r"(a)?(?(1)b|c)", "xxab")',
        lambda: regex.search(r"(a)?(?(1)b|c)", "xxab"),
    ),
    (
        "conditionals-else",
        r'search(r"(a)?(?(1)b|c)", "xxc")',
        lambda: regex.search(r"(a)?(?(1)b|c)", "xxc"),
    ),
    (
        "verbs",
        r'search(r"a(*SKIP)(*FAIL)|b", "ab")',
        lambda: regex.search(r"a(*SKIP)(*FAIL)|b", "ab"),
    ),
    ("partial", r'match(r"abcdef", "abc", partial=True)', lambda: regex.match(r"abcdef", "abc", partial=True)),
    ("posix", r'search(r"(?p)a|ab", "ab")', lambda: regex.search(r"(?p)a|ab", "ab")),
    (
        "fuzzy-counts-and-changes",
        r'search(r"(?:foobar){e<=2}", "xxfoxbarxx")',
        lambda: regex.search(r"(?:foobar){e<=2}", "xxfoxbarxx"),
    ),
    (
        "enhancematch",
        r'search(r"(?e)(?:foobar){e<=2}", "xxfoxbarxx")',
        lambda: regex.search(r"(?e)(?:foobar){e<=2}", "xxfoxbarxx"),
    ),
    (
        "bestmatch",
        r'search(r"(?b)(?:foobar){e<=3}", "xxfoobrxx")',
        lambda: regex.search(r"(?b)(?:foobar){e<=3}", "xxfoobrxx"),
    ),
    (
        "substitution-template",
        r'sub(r"(\w+) (\w+)", r"\2 \1", "hello world")',
        lambda: regex.sub(r"(\w+) (\w+)", r"\2 \1", "hello world"),
    ),
    (
        "substitution-callback",
        r'sub(r"\d+", lambda m: str(int(m.group()) * 2), "a1b22c333")',
        lambda: regex.sub(r"\d+", lambda m: str(int(m.group()) * 2), "a1b22c333"),
    ),
    (
        "substitution-format",
        r'subf(r"(\w+) (\w+)", "{2} {1}", "hello world")',
        lambda: regex.subf(r"(\w+) (\w+)", "{2} {1}", "hello world"),
    ),
    ("split", r'split(r"[,;]", "a,b;c")', lambda: regex.split(r"[,;]", "a,b;c")),
    ("split-captures", r'split(r"([,;])", "a,b;c")', lambda: regex.split(r"([,;])", "a,b;c")),
    ("matches", r'findall(r"\d+", "a1b22c333")', lambda: regex.findall(r"\d+", "a1b22c333")),
    ("overlapped", r'findall(r"\d\d", "1234", overlapped=True)', lambda: regex.findall(r"\d\d", "1234", overlapped=True)),
    ("branch-reset", r'search(r"(?|(a)|(b))", "b").group(1)', lambda: regex.search(r"(?|(a)|(b))", "b")),
    ("reverse", r'search(r"(?r)\d+", "a1b22c")', lambda: regex.search(r"(?r)\d+", "a1b22c")),
    ("escape", r'escape("a.b*c", special_only=True)', lambda: regex.escape("a.b*c", special_only=True)),
    ("named-groups", r'search(r"(?<word>\w+)", "hi there").group("word")', lambda: regex.search(r"(?<word>\w+)", "hi there")),
]


def utf16(text: str, index: int) -> int:
    """The UTF-16 code-unit offset of a codepoint offset into `text`."""
    return len(text[:index].encode("utf-16-le")) // 2


def render(answer: object) -> str:
    """One line describing whatever the case returned."""
    if answer is None:
        return "None"
    if isinstance(answer, (str, list)):
        return repr(answer)

    match = answer
    subject = match.string
    start, end = match.span()
    bits = [
        f"span={start},{end}",
        f"utf16span={utf16(subject, start)},{utf16(subject, end)}",
        f"value={match.group()!r}",
        f"groups={match.groups()!r}",
    ]
    if match.partial:
        bits.append("partial=True")
    if match.fuzzy_counts != (0, 0, 0):
        # Upstream's tuple order is (substitutions, insertions, deletions).
        bits.append(f"fuzzy_counts={match.fuzzy_counts!r}")
        bits.append(f"fuzzy_changes={match.fuzzy_changes!r}")
    if match.groupdict():
        bits.append(f"groupdict={match.groupdict()!r}")
    return "  ".join(bits)


def main() -> int:
    # Windows consoles default to cp1252, which cannot encode the Greek and Deseret subjects.
    sys.stdout.reconfigure(encoding="utf-8")
    print(f"# regex {regex.__version__} on Python {sys.version.split()[0]}")
    width = max(len(area) for area, _, _ in CASES)
    for area, description, call in CASES:
        try:
            answer = call()
        except Exception as error:  # noqa: BLE001 - a raising case is itself an answer
            rendered = f"{type(error).__name__}: {error}"
        else:
            rendered = render(answer)
        print(f"{area.ljust(width)}  {description}")
        print(f"{' ' * width}  -> {rendered}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
