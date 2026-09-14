"""What a mature engine does with a self-recursive call that need not consume anything.

Ledger entry 14. Upstream `regex` allocates until `MemoryError`; this port inherits the
non-termination and bounds it, raising `InvalidOperationException: the regular expression engine's
backtracking stack exceeded its 1GB limit`. The question the S47 slice has to answer is whether
"a resource bound surfaced as a clear exception" is the right shape of answer or a cop-out, and
amendment 16 asks for a real run of a second engine rather than an argument.

PCRE2 HAS NO FUZZY MATCHING (measured for ledger entry 12,
`tools/probes/pcre2-has-no-fuzzy-matching.py`: it reads `{e<=3}` as literal text), so it cannot be
shown the exact pattern. What it CAN be shown is the same defect without the fuzzy section - a
recursion whose body can match empty - which is the mechanism entry 14 is about.

Run: `python tools/probes/pcre2-bounds-an-unbounded-recursion.py`
"""

import re as dotnet_style_unused  # noqa: F401 - keeps the import list honest about what is NOT used

import pcre2

# Each is a group that calls itself with a body that need not consume anything, which is entry 14's
# mechanism with the fuzzy section replaced by an ordinary optional atom.
PATTERNS = [
    (r"(?P<g1>(?:a?)(?&g1)?)", "aaaa"),
    (r"(?P<g1>(?:a*)(?&g1)?)", "aaaa"),
    (r"(?P<g1>(?:ab)?(?&g1)?)", "abab"),
    (r"(?:(?R))", "ab"),
    (r"(?:a(?R)?b)", "aabb"),
]

print("pcre2 binding", getattr(pcre2, "__version__", "?"), "| libpcre2 10.47")
for pattern, subject in PATTERNS:
    try:
        compiled = pcre2.compile(pattern)
    except Exception as error:  # noqa: BLE001 - the answer may be a compile-time refusal
        print(f"{pattern!r:32} compile -> {type(error).__name__}: {error}")
        continue

    try:
        match = compiled.match(subject)
    except Exception as error:  # noqa: BLE001 - or a match-time limit
        print(f"{pattern!r:32} match   -> {type(error).__name__}: {error}")
        continue

    print(f"{pattern!r:32} match   -> {match.span() if match else None}")
