#!/usr/bin/env python3
r"""Build the character-name table that ``\N{...}`` needs.

Upstream calls Python's ``unicodedata.lookup``. .NET has no character-name API at all, so this is
the one place the port genuinely needs a UCD data file: everything else is transliterated from
tables upstream has already generated (``docs/plan/2026-08-30-phase2-decisions.md``, decision B).

Reads ``UnicodeData.txt`` and ``NameAliases.txt`` for **Unicode 17.0.0**, the version
``upstream/src/_regex_unicode.h`` declares - not whichever version the host CPython's
``unicodedata`` happens to ship. Writes ``src/FuzzyRegex/Unicode/UnicodeCharacterNames.g.cs``.

The four algorithmic ranges are computed, not stored: Hangul syllables (11,172 names), CJK unified
ideographs (~100,000) and the two Tangut ranges (~7,000) would otherwise be 90% of the table.
Everything else - 40,470 names plus 481 aliases - is stored.

**Verification happens here, in Python, against the host's own ``unicodedata``**, because that is
an implementation independent of these data files. For every codepoint the host can name, the
table (or the algorithm) must map that name back to that codepoint. Names are immutable under
Unicode's stability policy, so a 16.0.0 name is still a 17.0.0 name; only the additions cannot be
checked this way, and they are counted instead. The C# test then only has to prove the table
survived the trip into C#, which is what the fixture digest is for.

Usage:
    python tools/build-character-names.py            # download (cached), verify, write
    python tools/build-character-names.py --check    # fail if the committed file differs
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import unicodedata
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
CACHE = REPO_ROOT / '.scratch/ucd'
OUT_PATH = REPO_ROOT / 'src/FuzzyRegex/Unicode/UnicodeCharacterNames.g.cs'
FIXTURE_PATH = REPO_ROOT / 'tests/FuzzyRegex.Tests/Gaps/Unicode/character-names.json'

UNICODE_VERSION = '17.0.0'
BASE_URL = f'https://www.unicode.org/Public/{UNICODE_VERSION}/ucd/'
FILES = ('UnicodeData.txt', 'NameAliases.txt', 'DerivedAge.txt')

NAMESPACE = 'Fuzzy.Text.RegularExpressions.Unicode'

# Hangul jamo short names (UAX #15, and the same three tables CPython's makeunicodedata.py uses).
JAMO_L = ['G', 'GG', 'N', 'D', 'DD', 'R', 'M', 'B', 'BB', 'S', 'SS', '', 'J', 'JJ', 'C', 'K',
          'T', 'P', 'H']
JAMO_V = ['A', 'AE', 'YA', 'YAE', 'EO', 'E', 'YEO', 'YE', 'O', 'WA', 'WAE', 'OE', 'YO', 'U',
          'WEO', 'WE', 'WI', 'YU', 'EU', 'YI', 'I']
JAMO_T = ['', 'G', 'GG', 'GS', 'N', 'NJ', 'NH', 'D', 'L', 'LG', 'LM', 'LB', 'LS', 'LT', 'LP',
          'LH', 'M', 'B', 'BS', 'S', 'SS', 'NG', 'J', 'C', 'K', 'T', 'P', 'H']


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


# --------------------------------------------------------------------------------------
# The algorithmic ranges
# --------------------------------------------------------------------------------------

def classify_range(label: str) -> str | None:
    """The name pattern a ``<..., First>`` label implies, or None where there is no name."""
    if 'Hangul Syllable' in label:
        return 'HANGUL SYLLABLE '
    if 'CJK Ideograph' in label:
        return 'CJK UNIFIED IDEOGRAPH-'
    if 'Tangut Ideograph' in label:
        return 'TANGUT IDEOGRAPH-'
    if 'Surrogate' in label or 'Private Use' in label:
        return None
    raise Fail(f'unknown algorithmic range {label!r}: decide what it is named before shipping')


def hangul_name(codepoint: int) -> str:
    index = codepoint - 0xAC00
    return ('HANGUL SYLLABLE '
            + JAMO_L[index // 588] + JAMO_V[(index % 588) // 28] + JAMO_T[index % 28])


# --------------------------------------------------------------------------------------
# Parsing
# --------------------------------------------------------------------------------------

def parse_unicode_data(text: str):
    """Returns (explicit name -> codepoint pairs, algorithmic ranges)."""
    pairs: list[tuple[str, int]] = []
    ranges: list[tuple[str, int, int]] = []
    pending: tuple[str, int] | None = None

    for line in text.splitlines():
        if not line:
            continue
        fields = line.split(';')
        codepoint = int(fields[0], 16)
        name = fields[1]

        if name.startswith('<'):
            if name.endswith(', First>'):
                pending = (name, codepoint)
                continue
            if name.endswith(', Last>'):
                if pending is None:
                    raise Fail(f'{name} at {codepoint:04X} has no matching First')
                label, first = pending
                pending = None
                prefix = classify_range(label)
                if prefix is not None:
                    ranges.append((prefix, first, codepoint))
                continue
            # <control> and friends: no name at all.
            continue

        pairs.append((name, codepoint))

    if pending is not None:
        raise Fail(f'{pending[0]} has no matching Last')
    return pairs, ranges


def parse_aliases(text: str) -> list[tuple[str, int, str]]:
    aliases = []
    for line in text.splitlines():
        line = line.split('#')[0].strip()
        if not line:
            continue
        code, alias, kind = line.split(';')
        aliases.append((alias, int(code, 16), kind))
    return aliases


def parse_age(text: str, version: str) -> list[tuple[int, int]]:
    """The codepoint ranges DerivedAge.txt assigns to one version."""
    ranges = []
    for line in text.splitlines():
        line = line.split('#')[0].strip()
        if not line:
            continue
        codes, age = (part.strip() for part in line.split(';'))
        if age != version:
            continue
        if '..' in codes:
            first, last = (int(c, 16) for c in codes.split('..'))
        else:
            first = last = int(codes, 16)
        ranges.append((first, last))
    return ranges


# --------------------------------------------------------------------------------------
# Verification, against the host's own unicodedata
# --------------------------------------------------------------------------------------

def algorithmic_lookup(name: str, ranges: list[tuple[str, int, int]]) -> int | None:
    for prefix, first, last in ranges:
        if not name.startswith(prefix):
            continue
        suffix = name[len(prefix):]
        if prefix == 'HANGUL SYLLABLE ':
            for codepoint in range(first, last + 1):
                if hangul_name(codepoint) == name:
                    return codepoint
            continue
        if re.fullmatch(r'[0-9A-F]{4,6}', suffix):
            codepoint = int(suffix, 16)
            if first <= codepoint <= last:
                return codepoint
    return None


def verify(table: dict[str, int], ranges: list[tuple[str, int, int]]) -> int:
    """Every name the host's unicodedata knows must resolve to the same codepoint here."""
    checked = 0
    mismatches = []
    for codepoint in range(0x110000):
        name = unicodedata.name(chr(codepoint), '')
        if not name:
            continue
        checked += 1
        found = table.get(name)
        if found is None:
            found = algorithmic_lookup(name, ranges)
        if found != codepoint:
            mismatches.append((name, codepoint, found))
            if len(mismatches) > 20:
                break

    if mismatches:
        for name, expected, found in mismatches:
            print(f'  {name!r}: unicodedata says U+{expected:04X}, the table says {found}',
                  file=sys.stderr)
        raise Fail(f'{len(mismatches)}+ names disagree with unicodedata '
                   f'{unicodedata.unidata_version}')
    return checked


def verify_algorithmic(ranges: list[tuple[str, int, int]],
                       new_in_17: set[int]) -> list[tuple[str, int, int]]:
    """The names we *compute* have to be the names the host resolves.

    The loop above only sees codepoints ``unicodedata.name`` will name, and it will not name a
    Tangut ideograph even though ``unicodedata.lookup`` resolves one (measured 2026-08-30,
    CPython 3.14.6). Going the other way - compute the name, ask the host to look it up - covers
    every algorithmic range instead of most of them. A codepoint new in 17.0 has no 16.0.0 name
    to check against and is counted separately rather than skipped silently.
    """
    report = []
    for prefix, first, last in ranges:
        verified = 0
        unknown = 0
        for codepoint in range(first, last + 1):
            name = (hangul_name(codepoint) if prefix == 'HANGUL SYLLABLE '
                    else f'{prefix}{codepoint:04X}')
            try:
                resolved = unicodedata.lookup(name)
            except KeyError:
                if codepoint in new_in_17:
                    unknown += 1
                    continue
                raise Fail(f'{name!r} (U+{codepoint:04X}) is not new in 17.0 but unicodedata '
                           f'{unicodedata.unidata_version} does not know it: the range prefix '
                           f'is wrong')
            if len(resolved) != 1 or ord(resolved) != codepoint:
                raise Fail(f'{name!r} resolves to {resolved!r}, not U+{codepoint:04X}')
            verified += 1
        report.append((f'{prefix}{first:04X}..{last:04X}', verified, unknown))
    return report


# --------------------------------------------------------------------------------------
# Emitting
# --------------------------------------------------------------------------------------

def render_cs(entries: list[tuple[str, int]], ranges: list[tuple[str, int, int]]) -> str:
    lines = [
        '// <auto-generated/>',
        f'// Built from the Unicode {UNICODE_VERSION} UCD by tools/build-character-names.py.',
        '// Do not edit by hand: re-run the script instead.',
        '#nullable enable',
        '',
        f'namespace {NAMESPACE};',
        '',
        'internal static partial class UnicodeCharacterNames',
        '{',
        f'    /// <summary>The Unicode version these names come from.</summary>',
        f'    internal const string Version = "{UNICODE_VERSION}";',
        '',
        '    /// <summary>',
        '    /// Every stored character name, upper case and sorted ordinally, so a lookup is a',
        '    /// binary search. The algorithmic ranges below are computed instead of stored.',
        '    /// </summary>',
        '    internal static readonly string[] Names = new string[]',
        '    {',
    ]
    lines.extend(f'        "{name}",' for name, _ in entries)
    lines.extend([
        '    };',
        '',
        '    /// <summary>The codepoint each entry of <see cref="Names"/> names.</summary>',
        '    internal static readonly int[] Codepoints = new int[]',
        '    {',
    ])
    for i in range(0, len(entries), 12):
        lines.append('        ' + ', '.join(str(cp) for _, cp in entries[i:i + 12]) + ',')
    lines.extend([
        '    };',
        '',
        '    /// <summary>',
        '    /// The ranges whose names UnicodeData.txt gives as a pattern rather than one row per',
        '    /// codepoint. Every prefix except Hangul is followed by the codepoint in upper-case',
        '    /// hexadecimal.',
        '    /// </summary>',
        '    internal static readonly (string Prefix, int First, int Last)[] AlgorithmicRanges =',
        '    [',
    ])
    for prefix, first, last in ranges:
        lines.append(f'        ("{prefix}", 0x{first:04X}, 0x{last:04X}),')
    lines.append('    ];')

    # Emitted rather than hand-copied into the lookup: these are the tables this script builds
    # every Hangul syllable name from, and the lookup takes them apart again. One typo in a
    # second copy would lose a syllable silently.
    for member, table, summary in (
        ('JamoLeading', JAMO_L, 'Leading jamo. Index 11 is deliberately empty: HANGUL SYLLABLE A '
                                'is a real name.'),
        ('JamoVowel', JAMO_V, 'Vowel jamo.'),
        ('JamoTrailing', JAMO_T, 'Trailing jamo. Index 0 is empty: a syllable need not have one.'),
    ):
        lines.extend([
            '',
            f'    /// <summary>{summary}</summary>',
            f'    internal static readonly string[] {member} =',
            '    [',
            '        ' + ', '.join(f'"{v}"' for v in table) + ',',
            '    ];',
        ])

    lines.extend([
        '',
        '    /// <summary>The first Hangul syllable, U+AC00.</summary>',
        '    internal const int HangulFirst = 0xAC00;',
        '',
        '    /// <summary>The prefix every Hangul syllable name carries.</summary>',
        '    internal const string HangulPrefix = "HANGUL SYLLABLE ";',
        '}',
        '',
    ])
    return '\n'.join(lines)


def render_fixture(entries: list[tuple[str, int]], ranges, checked: int, additions: int,
                   addition_ranges: list[tuple[int, int]]) -> str:
    digest = hashlib.sha256()
    for name, codepoint in entries:
        digest.update(f'{name}:{codepoint}\n'.encode('utf-8'))

    return json.dumps({
        'comment': ('GENERATED by tools/build-character-names.py. Do not edit by hand. '
                    'The script verifies the table against the host unicodedata; this fixture '
                    'only proves the table reached C# intact.'),
        'unicodeVersion': UNICODE_VERSION,
        'hostUnicodeVersion': unicodedata.unidata_version,
        'storedNameCount': len(entries),
        'storedNames': digest.hexdigest(),
        'algorithmicRangeCount': len(ranges),
        'namesCheckedAgainstHost': checked,
        'codepointsAddedIn17': additions,
        'codepointRangesAddedIn17': [[first, last] for first, last in addition_ranges],
    }, indent=2) + '\n'


# --------------------------------------------------------------------------------------

def build():
    pairs, ranges = parse_unicode_data(fetch('UnicodeData.txt'))
    aliases = parse_aliases(fetch('NameAliases.txt'))

    table: dict[str, int] = {}
    for name, codepoint in pairs:
        if name in table:
            raise Fail(f'duplicate name {name!r}')
        table[name] = codepoint

    # unicodedata.lookup accepts aliases, but a real name wins over one.
    for alias, codepoint, kind in aliases:
        if alias in table and table[alias] != codepoint:
            continue
        table[alias] = codepoint

    for name in table:
        if not re.fullmatch(r'[A-Z0-9 \-]+', name):
            raise Fail(f'{name!r} is not upper-case ASCII: the lookup upper-cases its argument, '
                       f'so a name outside that set would never be found')

    checked = verify(table, ranges)

    age_ranges = parse_age(fetch('DerivedAge.txt'), '17.0')
    additions = sum(last - first + 1 for first, last in age_ranges)
    new_in_17 = {cp for first, last in age_ranges for cp in range(first, last + 1)}

    for label, verified, unknown in verify_algorithmic(ranges, new_in_17):
        print(f'  {label}: {verified} verified, {unknown} new in 17.0', file=sys.stderr)

    entries = sorted(table.items())
    return entries, ranges, checked, additions, age_ranges


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true',
                        help='fail if the committed files differ from what would be written')
    args = parser.parse_args()

    entries, ranges, checked, additions, age_ranges = build()
    cs = render_cs(entries, ranges)
    fixture = render_fixture(entries, ranges, checked, additions, age_ranges)

    outputs = [(OUT_PATH, cs), (FIXTURE_PATH, fixture)]

    if args.check:
        stale = [path for path, text in outputs
                 if not path.exists() or path.read_text(encoding='utf-8') != text]
        if stale:
            print('build-character-names: OUT OF DATE - '
                  + ', '.join(str(p.relative_to(REPO_ROOT)) for p in stale), file=sys.stderr)
            return 1
        print('build-character-names: up to date')
        return 0

    for path, text in outputs:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8', newline='\n')

    print(f'stored names:        {len(entries)}')
    print(f'name characters:     {sum(len(n) for n, _ in entries)}')
    print(f'algorithmic ranges:  {len(ranges)}')
    print(f'checked against unicodedata {unicodedata.unidata_version}: {checked} names')
    print(f'codepoints new in 17.0: {additions}')
    print(f'wrote {OUT_PATH.relative_to(REPO_ROOT)} '
          f'({OUT_PATH.stat().st_size} bytes) and '
          f'{FIXTURE_PATH.relative_to(REPO_ROOT)}')
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except Fail as exc:
        print(f'build-character-names: {exc}', file=sys.stderr)
        sys.exit(2)
