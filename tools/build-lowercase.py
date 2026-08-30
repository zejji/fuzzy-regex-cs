#!/usr/bin/env python3
r"""Build the lowercase-mapping table that ``Sequence._fix_full_casefold`` needs.

``_regex_core.py`` line 3643 calls Python's ``str.lower()`` on already-case-folded text, and the
result decides which chunks of a literal get the full-case-folding opcode and which get the
cheaper simple-folding one. Folding does *not* subsume lowercasing: ``fold_case`` leaves ``I``,
``İ`` and the 86 upper-case Cherokee letters alone, so ``(?fiu)fI`` compiles to ``STRING_FLD``
only because ``.lower()`` turns ``fI`` into ``fi``, which is the folded form of the ligature
``ﬁ``. Measured 2026-08-30 against the local oracle.

Upstream's own generated tables carry no lowercase mapping, and .NET's is a different Unicode
version - 67 codepoints disagree with CPython, measured the same day - so this is the second
place the port needs a UCD data file, alongside ``tools/build-character-names.py``
(``docs/plan/2026-08-30-phase2-decisions.md``, decision B).

Reads ``UnicodeData.txt`` field 13 (Simple_Lowercase_Mapping) and the *unconditional* rows of
``SpecialCasing.txt`` for **Unicode 17.0.0**, the version ``upstream/src/_regex_unicode.h``
declares. Writes ``src/FuzzyRegex/Unicode/UnicodeLowercase.g.cs``.

**Verification happens here, in Python, against the host's own ``str.lower()``**, which is an
implementation independent of these data files: over every codepoint the host's Unicode version
knows, the table must reproduce it exactly. The C# test then only has to prove the table survived
the trip into C#, which is what the fixture digest is for.

The 16 *conditional* SpecialCasing rows are deliberately skipped. Fifteen are Lithuanian or
Turkish, which are locale-conditional and which CPython's ``str.lower()`` does not apply either;
the sixteenth is final sigma, which is context-conditional and which CPython does apply - but no
codepoint folds to U+03A3, measured over all 1,114,112 codepoints on 2026-08-30, so folded text
can never contain one and the rule is unreachable here.

Usage:
    python tools/build-lowercase.py            # download (cached), verify, write
    python tools/build-lowercase.py --check    # fail if the committed files differ
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import unicodedata
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
CACHE = REPO_ROOT / '.scratch/ucd'
OUT_PATH = REPO_ROOT / 'src/FuzzyRegex/Unicode/UnicodeLowercase.g.cs'
FIXTURE_PATH = REPO_ROOT / 'tests/FuzzyRegex.Tests/Gaps/Unicode/lowercase.json'

UNICODE_VERSION = '17.0.0'
BASE_URL = f'https://www.unicode.org/Public/{UNICODE_VERSION}/ucd/'

NAMESPACE = 'Fuzzy.Text.RegularExpressions.Unicode'


class Fail(Exception):
    pass


def fetch(name: str) -> str:
    """Downloads a UCD file once and caches it under .scratch/ (gitignored)."""
    CACHE.mkdir(parents=True, exist_ok=True)
    path = CACHE / f'{UNICODE_VERSION}-{name}'
    if not path.exists():
        print(f'downloading {BASE_URL + name}', file=sys.stderr, flush=True)
        with urllib.request.urlopen(BASE_URL + name, timeout=120) as response:
            path.write_bytes(response.read())
    return path.read_text(encoding='utf-8')


def parse_simple(text: str) -> dict[int, list[int]]:
    """UnicodeData.txt field 13, the Simple_Lowercase_Mapping."""
    mapping: dict[int, list[int]] = {}
    for line in text.splitlines():
        fields = line.split(';')
        if len(fields) < 14 or not fields[13]:
            continue
        mapping[int(fields[0], 16)] = [int(fields[13], 16)]
    return mapping


def parse_special(text: str) -> tuple[dict[int, list[int]], int]:
    """The unconditional lowercase rows of SpecialCasing.txt, and how many were conditional."""
    mapping: dict[int, list[int]] = {}
    conditional = 0
    for line in text.splitlines():
        line = line.split('#', 1)[0].strip()
        if not line:
            continue
        fields = [f.strip() for f in line.split(';')]
        if len(fields) < 4:
            raise Fail(f'SpecialCasing row has too few fields: {line!r}')
        # A fifth non-empty field is the condition list.
        if len(fields) > 4 and fields[4]:
            conditional += 1
            continue
        codepoint = int(fields[0], 16)
        mapping[codepoint] = [int(x, 16) for x in fields[1].split()] if fields[1] else []
    return mapping, conditional


def parse_age(text: str, version: str) -> list[tuple[int, int]]:
    """The codepoint ranges DerivedAge.txt assigns to one Unicode version."""
    ranges: list[tuple[int, int]] = []
    for line in text.splitlines():
        line = line.split('#', 1)[0].strip()
        if not line:
            continue
        codes, _, age = line.partition(';')
        if age.strip() != version:
            continue
        codes = codes.strip()
        first, _, last = codes.partition('..')
        ranges.append((int(first, 16), int(last or first, 16)))
    return ranges


def build_table() -> tuple[dict[int, list[int]], int]:
    """The full lowercase mapping, keyed by codepoint, excluding the identity rows."""
    table = parse_simple(fetch('UnicodeData.txt'))
    special, conditional = parse_special(fetch('SpecialCasing.txt'))

    for codepoint, lower in special.items():
        if lower != [codepoint]:
            table[codepoint] = lower

    return {cp: low for cp, low in table.items() if low != [cp]}, conditional


def verify(table: dict[int, list[int]]) -> tuple[int, int]:
    """Checks the table against the host CPython, which is an independent implementation.

    Codepoints assigned in Unicode 17.0 are skipped: the host's ``str.lower`` cannot know them.
    Returns how many were checked and how many were skipped.
    """
    new_in_17 = {cp
                 for first, last in parse_age(fetch('DerivedAge.txt'), '17.0')
                 for cp in range(first, last + 1)}

    checked = 0
    for codepoint in range(0x110000):
        if codepoint in new_in_17:
            continue
        expected = [ord(c) for c in chr(codepoint).lower()]
        got = table.get(codepoint, [codepoint])
        if got != expected:
            raise Fail(f'U+{codepoint:04X} lowercases to {got} here and to {expected} in '
                       f'CPython {unicodedata.unidata_version}')
        checked += 1

    return checked, len(new_in_17)


def render_cs(rows: list[tuple[int, list[int]]]) -> str:
    single = [(cp, low[0]) for cp, low in rows if len(low) == 1]
    expanding = [(cp, low) for cp, low in rows if len(low) != 1]

    lines = [
        '// <auto-generated/>',
        f'// Built from the Unicode {UNICODE_VERSION} UCD by tools/build-lowercase.py.',
        '// Do not edit by hand: re-run the script instead.',
        '#nullable enable',
        '',
        f'namespace {NAMESPACE};',
        '',
        '/// <summary>',
        "/// Python's <c>str.lower()</c>, one codepoint at a time: UnicodeData.txt's",
        '/// Simple_Lowercase_Mapping with the unconditional rows of SpecialCasing.txt over it.',
        '/// </summary>',
        'internal static class UnicodeLowercase',
        '{',
        '    /// <summary>The Unicode version this mapping comes from.</summary>',
        f'    internal const string Version = "{UNICODE_VERSION}";',
        '',
        '    /// <summary>',
        '    /// The codepoints that change under lowercasing, ascending, so a lookup is a binary',
        '    /// search. Every codepoint not here lowercases to itself.',
        '    /// </summary>',
        '    internal static readonly int[] From = new int[]',
        '    {',
    ]
    for i in range(0, len(single), 12):
        lines.append('        ' + ', '.join(str(cp) for cp, _ in single[i:i + 12]) + ',')
    lines.extend([
        '    };',
        '',
        '    /// <summary>What each entry of <see cref="From"/> lowercases to.</summary>',
        '    internal static readonly int[] To = new int[]',
        '    {',
    ])
    for i in range(0, len(single), 12):
        lines.append('        ' + ', '.join(str(low) for _, low in single[i:i + 12]) + ',')
    lines.extend([
        '    };',
        '',
        '    /// <summary>',
        '    /// The codepoints whose lowercase is not one codepoint. SpecialCasing.txt gives',
        '    /// exactly one in Unicode 17.0.0 - U+0130 - but the table is built from the data, so',
        '    /// a later Unicode adding another needs no code change.',
        '    /// </summary>',
        '    internal static readonly (int From, int[] To)[] Expanding =',
        '    [',
    ])
    for codepoint, lower in expanding:
        lines.append(f'        ({codepoint}, [{", ".join(str(x) for x in lower)}]),')
    lines.extend([
        '    ];',
        '}',
        '',
    ])
    return '\n'.join(lines)


def render_fixture(rows: list[tuple[int, list[int]]], checked: int, skipped: int,
                   conditional: int) -> str:
    digest = hashlib.sha256()
    for codepoint, lower in rows:
        digest.update(f'{codepoint}:{",".join(str(x) for x in lower)}\n'.encode('utf-8'))

    return json.dumps({
        'comment': ('GENERATED by tools/build-lowercase.py. Do not edit by hand. The script '
                    'verifies the table against the host CPython str.lower(); this fixture only '
                    'proves the table reached C# intact.'),
        'unicodeVersion': UNICODE_VERSION,
        'hostUnicodeVersion': unicodedata.unidata_version,
        'mappingCount': len(rows),
        'expandingCount': sum(1 for _, low in rows if len(low) != 1),
        'mappings': digest.hexdigest(),
        'codepointsCheckedAgainstHost': checked,
        'codepointsSkippedAsNewIn17': skipped,
        'conditionalSpecialCasingRowsSkipped': conditional,
    }, indent=2) + '\n'


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true',
                        help='fail if the committed files differ from what would be written')
    args = parser.parse_args()

    table, conditional = build_table()
    checked, skipped = verify(table)
    rows = sorted(table.items())
    print(f'  {len(rows)} mappings, {checked} codepoints checked against CPython '
          f'{unicodedata.unidata_version}, {skipped} skipped as new in 17.0', file=sys.stderr)

    outputs = [
        (OUT_PATH, render_cs(rows)),
        (FIXTURE_PATH, render_fixture(rows, checked, skipped, conditional)),
    ]

    if args.check:
        stale = [path for path, text in outputs
                 if not path.exists() or path.read_text(encoding='utf-8') != text]
        if stale:
            print('build-lowercase: OUT OF DATE - '
                  + ', '.join(str(p.relative_to(REPO_ROOT)) for p in stale), file=sys.stderr)
            return 1
        print('build-lowercase: up to date')
        return 0

    for path, text in outputs:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8', newline='\n')

    print(f'wrote {OUT_PATH.relative_to(REPO_ROOT)} and '
          f'{FIXTURE_PATH.relative_to(REPO_ROOT)}', file=sys.stderr)
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except Fail as failure:
        print(f'build-lowercase: {failure}', file=sys.stderr)
        sys.exit(1)
