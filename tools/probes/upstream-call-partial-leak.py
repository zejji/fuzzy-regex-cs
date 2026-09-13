# S40a (2026-09-13): where upstream's extra PARTIAL comes from, on the rows where upstream answers
# a partial match and this port answers a complete one.
#
# Seven rows of a 6000-row three-seed wave are this family (seed 7: 98956, 103926; seed 4242:
# 100842, 104237, 106041; seed 20260913: 98191, and 74396 as a substitution). Every one has a group
# CALL inside a lookaround that runs the other way round from the pattern and an optional tail.
# FIVE of the seven are the shape below - an astral subject, upstream partial, this port a complete
# match of the same span. The other two are not: row 98191's subject is ' \r', no astral character
# anywhere, and this port answers NO MATCH at all where upstream answers a partial; row 74396 is a
# SUBSTITUTION, where no partial is reported by either engine and the divergence is the replacement
# count (upstream 1, this port 2). So the astral subject is what the generators happen to draw
# rather than the precondition, and a slice that fixes only the shape below has fixed five of seven.
#
# S40a session 1 read the family as "this port is inconsistent across astrality"; the matrix below
# says something narrower and more useful, which is why it exists.
#
#   python tools/probes/upstream-call-partial-leak.py            # the pinned 2026.7.19
#   python tools/probes/upstream-call-partial-leak.py --newer    # 2026.9.10 from .venvs/
#
# --newer inserts the venv's site-packages on sys.path rather than running its interpreter, because
# the unattended sandbox allows `python` and not `.venvs/*/Scripts/python`. Same CPython, so the
# extension loads.
#
# WHAT IT MEASURES, on 2026.7.19 and on 2026.9.10 - IDENTICAL on both, so this is NOT issue 614:
#
#   * upstream reports partial=True for the CALL form and partial=False for the same lookaround
#     written out as its own body, on ASCII and astral subjects alike. Upstream contradicts itself
#     between a call and its inlined body, which is ledger entry 8's signature.
#   * this port answers False for every optional tail EXCEPT the two ASCII CALL forms - the forward
#     one and the reversed one - where it answers True. So the port is not "wrong on astral": it
#     reproduces upstream's leak at one width and not at the other, and its astral answers are the
#     ones consistent with all its other answers.
#
# That reverses which side needs explaining. The open question for S40c is not "why does this port
# lose the partial on an astral subject" but "why does it leak one on an ASCII subject", and the
# answer decides whether the wave rows are `port right, upstream leaks` - in which case the ASCII
# rows start diverging once the leak goes - or something else entirely.
import sys
from pathlib import Path

if '--newer' in sys.argv:
    venv = Path(__file__).resolve().parent.parent.parent / '.venvs' / 'regex-2026.9.10' / 'Lib' / 'site-packages'
    sys.path.insert(0, str(venv))

import regex

sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

A = '\U00010400'   # one codepoint, two UTF-16 code units

# label, pattern, subject, whether to ask fullmatch rather than search
CASES = [
    ('call, astral', rf'(?P<g1>{A})(?:(?<=(?P>g1))\w)?', A, False),
    ('call, ascii', r'(?P<g1>A)(?:(?<=(?P>g1))\w)?', 'A', False),
    ('inline, astral', rf'(?P<g1>{A})(?:(?<={A})\w)?', A, False),
    ('inline, ascii', r'(?P<g1>A)(?:(?<=A)\w)?', 'A', False),
    ('call, tail not optional, astral', rf'(?P<g1>{A})(?:(?<=(?P>g1))\w)', A, False),
    ('bare optional tail, astral', rf'(?P<g1>{A})\w?', A, False),
    ('bare required tail, astral', rf'(?P<g1>{A})\w', A, False),
    ('bare required tail, ascii', r'(?P<g1>A)\w', 'A', False),
    ('reversed call, astral', r'(?r)(?P<g1>\w+)(?:(?!(?P>g1))\s)?', A, True),
    ('reversed call, ascii', r'(?r)(?P<g1>\w+)(?:(?!(?P>g1))\s)?', 'a', True),
    ('reversed inline, astral', r'(?r)(?P<g1>\w+)(?:(?!\w+)\s)?', A, True),
    ('call outside any lookaround, astral', rf'(?P<g1>{A})(?P>g1)?', A, False),
]

# This port's answer to each, measured on 2026-09-13 with .scratch/try-astral-partial.ps1 against
# src/FuzzyRegex/bin/Debug. Printed beside upstream's so the matrix is readable in one run; it is a
# recorded figure, not something this script asks the port for.
OURS = {
    'call, astral': False,
    'call, ascii': True,
    'inline, astral': False,
    'inline, ascii': False,
    'call, tail not optional, astral': True,
    'bare optional tail, astral': False,
    'bare required tail, astral': True,
    'bare required tail, ascii': True,
    'reversed call, astral': False,
    'reversed call, ascii': True,
    'reversed inline, astral': False,
    'call outside any lookaround, astral': False,
}

print(f'regex {regex.__version__}')
print(f'{"case":<38} {"upstream":<10} {"port":<8} agree')
for label, pattern, subject, full in CASES:
    compiled = regex.compile(pattern)
    match = (compiled.fullmatch if full else compiled.search)(subject, partial=True)
    theirs = bool(match and match.partial)
    mine = OURS[label]
    print(f'{label:<38} {theirs!s:<10} {mine!s:<8} {"yes" if theirs == mine else "NO"}')
