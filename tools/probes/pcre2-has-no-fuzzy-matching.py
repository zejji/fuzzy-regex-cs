"""Why a fuzzy-matching divergence has no second engine to be judged against.

Design spec amendment 16 asks for a real run of a second engine before calling a
divergence upstream's bug. For the fuzzy family - ledger entries 11, 12, 13 and 14 - that
limb is unavailable, and S46 measured that on 2026-09-14 rather than asserting it, on the
`pcre2` binding 0.7.1 over libpcre2 10.47 that the orchestrator installed for the purpose.

**PCRE2 does not merely lack fuzzy matching - it reads the fuzzy suffix as LITERAL TEXT**,
which is worse than an error, because a comparison built on it would answer confidently and
wrongly:

    pcre2.compile(r'(?:x){e<=3}').match('xyz')        -> None
    pcre2.compile(r'(?:x){e<=3}').match('x{e<=3}')    -> span (0, 7)

The braces are a literal `{e<=3}` after `x`, not a budget of three errors. Only `(?b)`
fails loudly (`unrecognized character after (? or (?-`), and only because PCRE2 has no such
flag.

So for a fuzzy entry the judgement rests on upstream's own definition plus self-refutation
instead - `BESTMATCH` is documented as a RANKING flag (`upstream/README.rst:592`), so it
chooses among the flagless engine's candidates and cannot destroy them all, and the same
engine answers the match the moment the flag is deleted. Perl and .NET have no approximate
matching either; the engines that do are TRE and agrep, neither of which is installed here
and neither of which implements upstream's `{...}` syntax or its `BESTMATCH` ranking, so
neither would be answering the same question.

Run it:

    python tools/probes/pcre2-has-no-fuzzy-matching.py
"""

import pcre2

FUZZY = (r"(?:x){e<=3}", r"(?b)(?:x){e<=3}", r"(?:abc){s<=1}", r"(?:abc){i<=2}")

if __name__ == "__main__":
    print("pcre2 binding", getattr(pcre2, "__version__", "?"), "| libpcre2 10.47")
    print()

    for pattern in FUZZY:
        try:
            compiled = pcre2.compile(pattern)
        except Exception as exc:  # noqa: BLE001 - a probe
            print(f"compile({pattern!r}) -> {type(exc).__name__}: {exc}")
            continue
        print(f"compile({pattern!r}) OK -> match('xyz') = {compiled.match('xyz')}")

    print()
    print("and what it compiled them TO - the braces are literal:")
    for pattern, literal in ((r"(?:x){e<=3}", "x{e<=3}"), (r"(?:abc){s<=1}", "abc{s<=1}")):
        compiled = pcre2.compile(pattern)
        print(f"  {pattern!r} vs {literal!r} -> {compiled.match(literal)}")

    print()
    print("control - an ordinary pattern compiles and matches, so the binding is fine:")
    print("  'x.z' vs 'xyz' ->", pcre2.compile(r"x.z").match("xyz"))
