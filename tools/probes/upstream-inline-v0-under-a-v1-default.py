"""Upstream disagrees with itself about VERSION0 when DEFAULT_VERSION is VERSION1.

`regex.compile('a', regex.V0)` folds simply, as version 0 is defined to; `regex.compile('(?V0)a')`
folds fully. The inline spelling takes the `_UnscopedFlagSet` retry, and the first attempt has
already OR-ed version 1's implied FULLCASE into `info.global_flags`, which is what seeds the
second one.

It cannot fire in upstream's shipped configuration, where `DEFAULT_VERSION = VERSION0` contributes
no default flags to carry - but changing the global is supported ("Hg issue 69: Changing
DEFAULT_VERSION does not actually work ... should now work as expected"), and S50b made VERSION1
this port's default, so the port fixes it. See ledger entry 22 and
`tests/FuzzyRegex.Tests/Gaps/Parsing/DefaultVersionTests.cs`.

Run:  python tools/probes/upstream-inline-v0-under-a-v1-default.py
"""

import sys

import regex
import regex._main as _main
import regex._regex_core as _core

_NAMES = {
    regex.IGNORECASE: "I",
    regex.FULLCASE: "F",
    regex.UNICODE: "U",
    regex.VERSION0: "V0",
    regex.VERSION1: "V1",
    regex.DOTALL: "S",
    regex.REVERSE: "R",
}

SHARP_S = "ß"


def spell(flags):
    return "|".join(name for bit, name in _NAMES.items() if flags & bit) or "0"


def set_default(version):
    # The module keeps the same global in three places; _main._compile reads its own and copies it
    # into _regex_core, and the pattern cache is keyed on it.
    regex.DEFAULT_VERSION = version
    _main.DEFAULT_VERSION = version
    _core.DEFAULT_VERSION = version
    _main._cache.clear()


def main():
    print(f"regex {regex.__version__}, CPython {sys.version.split()[0]}")

    for version in (regex.VERSION0, regex.VERSION1):
        set_default(version)
        print(f"\nDEFAULT_VERSION = {spell(version)}")
        print(f"  compile('a', regex.V0)            flags = {spell(regex.compile('a', regex.V0).flags)}")
        print(f"  compile('(?V0)a')                 flags = {spell(regex.compile('(?V0)a').flags)}")
        print(f"  compile('a(?V0)')                 flags = {spell(regex.compile('a(?V0)').flags)}")
        print(
            "  compile('ss', regex.V0|regex.I) vs SHARP S: "
            f"{bool(regex.compile('ss', regex.V0 | regex.I).match(SHARP_S))}"
        )
        print(
            "  compile('(?V0)(?i)ss')          vs SHARP S: "
            f"{bool(regex.compile('(?V0)(?i)ss').match(SHARP_S))}"
        )

    set_default(regex.VERSION0)


if __name__ == "__main__":
    main()
