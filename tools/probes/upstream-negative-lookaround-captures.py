# S37 (2026-09-12): a NEGATIVE lookaround that succeeds leaves no capture behind, because it only
# succeeds when its body fails. That is what lets `CarriesACaptureOutsideItself` in
# tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs exclude only the POSITIVE forms and '\K', and
# so classify seed 7's row 5543 - a reversed overlapped '(*SKIP)' scan whose extra match carries a
# capture outside itself, in a pattern that happens to hold a '(?<!'.
#
# This port agrees on every row below; the same four questions went through the oracle consumer as
# explicit rows and were recorded as agreements.
import sys

import regex

sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

CASES = [
    (r'(?!(a))b', 'b'),
    (r'(?<!(a))b', 'b'),
    (r'(a)(?!(?:(b))x)b', 'ab'),
    (r'(?!(a)x)ab', 'ab'),
    # The positive forms, which CAN put a capture outside the match and are still excluded.
    (r'(?=(ab))a', 'ab'),
    (r'(?<=(a))b', 'ab'),
]

print(f'regex {regex.__version__}')
for pat, subject in CASES:
    c = regex.compile(pat, cache_pattern=False)
    m = c.search(subject)
    groups = None if m is None else [m.spans(n) for n in range(1, c.groups + 1)]
    print(f'{pat!r} on {subject!r} -> {m.span() if m else None}  group spans {groups}')

# The row itself, and the three questions that place its extra match in upstream rather than here.
PAT = r'(?r)(?:\D{1,1}(*SKIP)[\p{ASCII}&&\p{L}]|[[a-f]~~[d-k]])(?P<g1>.*)??(?P<g2>[A])(?:(?(2)(?<!(?&g2))\p{Nd}))\b'
SUB = 'A\r\nAAA'
FLAGS = 0x108

print(f'\nrow 5543  {PAT!r} on {SUB!r}')
compiled = regex.compile(PAT, FLAGS, cache_pattern=False)
print('  as recorded      ', [(m.span(), m.spans('g2')) for m in compiled.finditer(SUB, overlapped=True)])
for replacement, name in (('', 'verb deleted'), ('(*PRUNE)', 'verb -> (*PRUNE)')):
    other = regex.compile(PAT.replace('(*SKIP)', replacement), FLAGS, cache_pattern=False)
    print(f'  {name:17}', [(m.span(), m.spans('g2')) for m in other.finditer(SUB, overlapped=True)])
written_out = regex.compile(PAT.replace('(?&g2)', '[A]'), FLAGS, cache_pattern=False)
print('  call written out ', [(m.span(), m.spans('g2')) for m in written_out.finditer(SUB, overlapped=True)])
for pos in range(len(SUB) + 1):
    found, anchored = compiled.search(SUB, pos), compiled.match(SUB, pos)
    print(f'  pos {pos}: search {found.span() if found else None}  match {anchored.span() if anchored else None}')
