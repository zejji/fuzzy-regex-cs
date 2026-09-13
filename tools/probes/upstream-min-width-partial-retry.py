# S40c (2026-09-13): what actually decides upstream's extra PARTIAL on the "group call inside an
# opposite-direction lookaround" family - and it is not the call.
#
# THE MECHANISM, traced in this port and predicted here for upstream.
#
# `do_match` (upstream/src/_regex.c:18121) answers a partial request in two passes: a NON-PARTIAL
# pass first, and only if that FAILS a second pass with `partial_side` set. `do_exact_match`
# (:18064) opens with a width early-out - if fewer characters are available than `min_width`, fail
# without matching at all - and that early-out is guarded by `partial_side == RE_PARTIAL_NONE`, so
# it fires on the FIRST pass and not on the second.
#
# `min_width` counts a group CALL at the width of the group it calls, even inside a LOOKAROUND,
# which is zero-width. So `(?P<g1>A)(?:(?<=(?P>g1))\w)?` has min_width 2 where the same lookaround
# written out has min_width 1. On a one-character subject that makes the non-partial pass fail on
# arithmetic alone; the partial pass then runs, the lookbehind succeeds, the tail asks for a
# character past the end, and the answer is a PARTIAL of the span the first pass would have
# reported as complete.
#
# THE PREDICTION THIS SCRIPT TESTS. If that is the mechanism then upstream's partial depends on how
# many characters are AVAILABLE, not on the call being present:
#
#   * give the same pattern MORE text than the inflated min_width and the partial goes away;
#   * widen the called group and the threshold moves with it, by exactly the callee's width.
#
# Both are checked below and both hold (regex 2026.7.19 and 2026.9.10, measured 2026-09-13).
#
# WHY THE PORT DIVERGED ON AN ASTRAL SUBJECT. This port indexes the subject by UTF-16 code unit, and
# `do_exact_match`'s `available` was a code-unit subtraction. One astral codepoint is two units, so
# `available` read 2 where upstream reads 1, the early-out did not fire, the non-partial pass ran
# and SUCCEEDED, and the partial retry upstream performs never happened. Fixed in S40c by counting
# characters; this file is the evidence that the ASCII answer was right all along.
#
#   python tools/probes/upstream-min-width-partial-retry.py
#   python tools/probes/upstream-min-width-partial-retry.py --newer    # 2026.9.10 from .venvs/
import sys
from pathlib import Path

if '--newer' in sys.argv:
    venv = Path(__file__).resolve().parent.parent.parent / '.venvs' / 'regex-2026.9.10' / 'Lib' / 'site-packages'
    sys.path.insert(0, str(venv))

import regex

sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

A = '\U00010400'   # one codepoint, two UTF-16 code units

# HOW THE CASES ARE BUILT, because a careless row proves nothing. A partial can only arise where
# the optional tail asks for a character PAST THE END, so in every row below the match ends at the
# end of the subject and the tail is refused there. A `\w*` prefix soaks up any extra characters,
# which is what lets the AVAILABLE WIDTH vary while that stays true. Then the only variable left
# between a True row and a False row is how many characters the early-out has to count.
#
# (The first draft of this file asked `(?P<g1>ABC)(?:(?<=(?P>g1))\w)?` of 'ABCDE' and predicted a
# partial. Upstream said no, rightly: there the tail MATCHES the 'D', so the partial pass finds a
# complete match and the early-out's effect is invisible. Kept as a note because the row looked
# like a refutation and was a badly chosen experiment.)
#
# label, pattern, subject, expected partial - the prediction, written down before it is run.
CASES = [
    # The callee is one character, so min_width is 2 where the inlined body would give 1.
    # One character available: the early-out fires, the partial pass runs, the tail is past the end.
    ('w1 call, 1 char available', r'\w*(?P<g1>A)(?:(?<=(?P>g1))\w)?', 'A', True),
    # Two available: it does not fire, the non-partial pass succeeds, and the tail is past the end
    # in this row too - so the width is the only thing that changed.
    ('w1 call, 2 chars available', r'\w*(?P<g1>A)(?:(?<=(?P>g1))\w)?', 'BA', False),

    # The callee is three characters, so min_width is 6 and the threshold moves by exactly that.
    ('w3 call, 3 chars available', r'\w*(?P<g1>ABC)(?:(?<=(?P>g1))\w)?', 'ABC', True),
    ('w3 call, 5 chars available', r'\w*(?P<g1>ABC)(?:(?<=(?P>g1))\w)?', 'XXABC', True),
    ('w3 call, 6 chars available', r'\w*(?P<g1>ABC)(?:(?<=(?P>g1))\w)?', 'XXXABC', False),

    # No call: min_width is the body's own width, so the early-out never fires on these and the
    # answer does not move with the subject at all.
    ('w1 inline, 1 char available', r'\w*(?P<g1>A)(?:(?<=A)\w)?', 'A', False),
    ('w3 inline, 3 chars available', r'\w*(?P<g1>ABC)(?:(?<=ABC)\w)?', 'ABC', False),

    # The astral subject is ONE character, so it behaves exactly like the ASCII one-character row.
    # This is the pair the port got wrong: counting UTF-16 code units made the first of these read
    # as two characters, so the early-out did not fire and the partial never happened.
    ('astral call, 1 char available', rf'\w*(?P<g1>{A})(?:(?<=(?P>g1))\w)?', A, True),
    ('astral call, 2 chars available', rf'\w*(?P<g1>{A})(?:(?<=(?P>g1))\w)?', 'B' + A, False),
]

print(f'regex {regex.__version__}')
print(f'{"case":<34} {"predicted":<10} {"upstream":<10} agree')
ok = True
for label, pattern, subject, predicted in CASES:
    match = regex.compile(pattern).search(subject, partial=True)
    actual = bool(match and match.partial)
    agree = actual == predicted
    ok = ok and agree
    print(f'{label:<34} {predicted!s:<10} {actual!s:<10} {"yes" if agree else "NO"}')

print('PREDICTION HOLDS' if ok else 'PREDICTION FAILS')
