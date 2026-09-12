# S37 (2026-09-12): a group reached by a call from a lookaround running the other way round from
# the pattern makes upstream LOSE a match, and upstream says so itself - deleting the piece that
# holds the call, which in every one of these rows can match zero-width, gives upstream the match it
# had just refused. A zero-width-capable piece cannot remove a match, so the port is right.
#
# Run with the pinned interpreter for upstream's answer, and with .venvs/regex-2026.9.10 on the path
# to check the sync: all five rows answered identically on 2026-09-12, so issue 614's fix does not
# cover this.
import sys

import regex

sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

# label, whole pattern, the same pattern with the call-holding piece deleted, subject, flags,
# overlapped, and the pattern with the call written out - which says whether the piece can match
# zero-width, because a match no wider than the shorter pattern's is one the piece matched empty in.
CASES = [
    (
        'seed 7 row 4182 - the piece is a "??" optional, so zero iterations is always available',
        r'(?P<g1>\S)(?:(?(1)(?<!(?P>g1))[[:alpha:]]))??([a]{0,0})?\2\b',
        r'(?P<g1>\S)([a]{0,0})?\2\b',
        None,
        'aa\U00010428\U00010428A',
        0x0,
        False,
    ),
    (
        'seed 20260912 row 4407 - the piece is a "*" repeat, so zero iterations is always available',
        r'\b(?(?![\w\s])[[:digit:]])(\w)(?P<g2>[^\d]{3})(?:(?(2)(?<!(?&g2))[a-f]|[^a]))*',
        r'\b(?(?![\w\s])[[:digit:]])(\w)(?P<g2>[^\d]{3})',
        None,
        'İİ\nİİﬁﬁ ',
        0x410A,
        True,
    ),
    (
        'seed 4242 row 5087 - "{3}" of a conditional whose group is unset, so each iteration is empty',
        r'(?r)\b(?P<g1>[A])(?:(?(1)(?=(?&g1))\S)){3}(\p{Nd}+?)?',
        r'(?r)\b(?P<g1>[A])(\p{Nd}+?)?',
        r'(?r)\b(?P<g1>[A])(?:(?(1)(?=[A])\S)){3}(\p{Nd}+?)?',
        'AA..0',
        0xA,
        True,
    ),
    (
        'seed 4242 row 5773 - "+?" of the same, so one empty iteration satisfies it',
        r'(?r)^([[:alpha:]]+)(?P<g2>[\U0001F600])(?:(?(2)(?=(?P>g2))[^\p{L}]))+?',
        r'(?r)^([[:alpha:]]+)(?P<g2>[\U0001F600])',
        r'(?r)^([[:alpha:]]+)(?P<g2>[\U0001F600])(?:(?(2)(?=[\U0001F600])[^\p{L}]))+?',
        'a\U0001F600\r',
        0xA,
        False,
    ),
    (
        'seed 99991 row 1624 - the piece is a "?" optional, so zero is always available (a split row)',
        r'(?P<g1>[\U00010400A]{2,2})(?:(?(1)(?<!(?P>g1))[^\d]|[a-f]))?([abz]{0,2})$',
        r'(?P<g1>[\U00010400A]{2,2})([abz]{0,2})$',
        None,
        '\U00010400\U00010400A',
        0x10000,
        False,
    ),
]


def spans(pat, subject, flags, overlapped):
    return [m.span() for m in regex.compile(pat, flags, cache_pattern=False).finditer(subject, overlapped=overlapped)]


print(f'regex {regex.__version__}')
for label, whole, shorter, inlined, subject, flags, overlapped in CASES:
    print(f'\n== {label}')
    print(f'   subject {subject!r} flags 0x{flags:x} overlapped={overlapped}')
    whole_spans = spans(whole, subject, flags, overlapped)
    short_spans = spans(shorter, subject, flags, overlapped)
    print(f'   whole    {whole!r}\n            {whole_spans}')
    print(f'   shorter  {shorter!r}\n            {short_spans}')
    lost = sorted({s for s, _ in short_spans} - {s for s, _ in whole_spans})
    print(f'   start positions the SHORTER pattern reaches and the whole one refuses: {lost}')
    if inlined:
        # The width of this answer is what proves the piece matched empty: were the conditional
        # taking its yes-branch, the repeat would have to consume.
        print(f'   inlined  {inlined!r}\n            {spans(inlined, subject, flags, overlapped)}')
