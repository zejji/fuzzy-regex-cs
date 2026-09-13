# S40a (2026-09-13): the three reversed `(*SKIP)` rows that the per-match slice reset exposed,
# plus a FOURTH that S40d found at a seed no slice had used, where the same carried slice makes
# upstream's scanner LOSE a match rather than invent one.
#
# A `(*SKIP)` moves `slice_start`/`slice_end` mid-attempt and upstream restores it nowhere (ledger
# entry 5), so one scanner state carries the moved slice into its next match. S40a made this port
# put the slice back at the start of every match, which is the fix that entry proposes - and the
# immediate effect is that rows where this port used to AGREE by reproducing the bug now diverge.
# Three of them, in three shapes, and only the first is classified by
# `overlapped-skip-extra-match-reversed`; the other two red the wave on purpose.
#
# `verbs` rows are recorded prefilter-free, so every call here switches `locate_required_string`
# off exactly as tools/record-oracle.py does. Without that these are not the wave's answers.
import sys

import regex

sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

_REQ_OFFSET_ARG = 7
_REQ_CHARS_ARG = 8


def compile_free(pattern, flags):
    """upstream's compiler with the required-string prefilter switched off."""
    inner = regex._regex.compile

    def without_required_string(*args):
        args = list(args)
        args[_REQ_OFFSET_ARG] = -1
        args[_REQ_CHARS_ARG] = None
        return inner(*args)

    regex._regex.compile = without_required_string
    try:
        return regex.compile(pattern, flags, cache_pattern=False)
    finally:
        regex._regex.compile = inner


def scan(label, pattern, subject, flags, overlapped=False):
    spans = [m.span() for m in compile_free(pattern, flags).finditer(subject, overlapped=overlapped)]
    print(f'   {label:<44} {spans}')


def replaced(label, pattern, template, subject, flags):
    print(f'   {label:<44} {compile_free(pattern, flags).subn(template, subject, count=0)!r}')


print(f'regex {regex.__version__}')

# ---------------------------------------------------------------------------------------------
# CLASSIFIED. Seed 20260913 row 116766, a PLAIN reversed finditer - the shape that made S40a widen
# `overlapped-skip-extra-match-reversed` past `finditer-overlapped`. The `$` tell settles it:
# upstream's second and third matches need `$` where the subject has an 'a'.
# ---------------------------------------------------------------------------------------------
P1, S1, F1 = r'(?r)[[:digit:]]*(*SKIP)a$', '\r\nAAa\r\naaa', 0x400A
print(f'\n== row 116766  plain reversed finditer  subject {S1!r}')
scan('as recorded (upstream)', P1, S1, F1)
scan('the verb made (*PRUNE)', P1.replace('(*SKIP)', '(*PRUNE)'), S1, F1)
scan('the verb deleted', P1.replace('(*SKIP)', ''), S1, F1)
print('   {:<44} {}'.format('positions where upstream\'s own `$` holds',
                            [m.start() for m in compile_free(r'(?m)$', 0).finditer(S1)]))
print('   this port answers [(9, 10)] alone, which is the first match and no other.')

# ---------------------------------------------------------------------------------------------
# NOT CLASSIFIED, shape one. Seed 20260913 row 116388, a SUBSTITUTION: the outcome is a string and
# a count, so there are no match positions in the row for a tell to refute. The `$` argument holds
# once the matches are asked for separately - upstream replaces at three spans, and `$` is true at
# the end of the subject only - but that is a second question, not something the row carries.
# ---------------------------------------------------------------------------------------------
P2, S2, F2 = r'(?r)(?:\d*?(*SKIP)\U0001D518|a)$', 'aa\U0001D518\U0001D518', 0xA
print(f'\n== row 116388  reversed sub  subject {S2!r}')
replaced('as recorded (upstream)', P2, '<\t', S2, F2)
replaced('the verb made (*PRUNE)', P2.replace('(*SKIP)', '(*PRUNE)'), '<\t', S2, F2)
scan('where upstream replaces', P2, S2, F2)
scan('with (*PRUNE)', P2.replace('(*SKIP)', '(*PRUNE)'), S2, F2)
print('   this port replaces once, at the last span, giving the (*PRUNE) answer.')

# ---------------------------------------------------------------------------------------------
# NOT CLASSIFIED, shape two. Seed 4242 row 117071: overlapped, but the pattern has no `$` and no
# groups, so neither tell exists. What refutes upstream here is its own single-shot answer over the
# truncated subject - legitimate for THIS pattern because it holds no end-sensitive item (no `$`,
# no `\b`, no `\Z`), so truncating cannot change what anything in it means. Upstream's scan reports
# a match ENDING at 7 that its own reversed search over [0, 7) says does not exist.
# ---------------------------------------------------------------------------------------------
P3, S3, F3 = r'(?r)\w{1,3}?(*SKIP).(?:\p{L}(*SKIP)){2,3}', '_ ___\U00010400\U00010400\U00010400', 0x8
print(f'\n== row 117071  overlapped, no $ and no groups  subject {S3!r}')
scan('as recorded (upstream)', P3, S3, F3, overlapped=True)
scan('the verbs made (*PRUNE)', P3.replace('(*SKIP)', '(*PRUNE)'), S3, F3, overlapped=True)
scan('the verbs deleted', P3.replace('(*SKIP)', ''), S3, F3, overlapped=True)
for end in (8, 7, 6):
    print('   {:<44} {}'.format(f'its own search over [0, {end})', compile_free(P3, F3).search(S3, 0, end)))
print('   this port answers [(3, 8)] alone - codepoints; the wave records UTF-16.')

# ---------------------------------------------------------------------------------------------
# THE SAME CARRY-OVER, LOSING A MATCH. Seed 20260914 row 3679, found by S40d while running a
# control at a seed no slice had used. Upstream's scan reports ONE match where this port reports
# two, which is the mirror of the shape above: the `slice_end` a `(*SKIP)` moved makes the next
# attempt run in a view of the subject that is too short, so the scanner never finds a match its own
# matcher finds at once. Neither of the row's other tells reaches it - the pattern has no `$`, and
# the capture tell reads a match upstream reported rather than one it lost.
# ---------------------------------------------------------------------------------------------
P4, S4, F4 = r'(?r)\p{Lu}*(*SKIP)B(?P<g1>(?:\D{2,4}(*SKIP)a|.))', 'B_\ra', 0x400
print(f'\n== row 3679  overlapped, upstream one match SHORT  subject {S4!r}')
scan('as recorded (upstream)', P4, S4, F4, overlapped=True)
scan('the verbs made (*PRUNE)', P4.replace('(*SKIP)', '(*PRUNE)'), S4, F4, overlapped=True)
scan('the verbs deleted', P4.replace('(*SKIP)', ''), S4, F4, overlapped=True)
end = len(S4)
while 0 <= end <= len(S4):
    match = compile_free(P4, F4).search(S4, 0, end)
    print('   {:<44} {}'.format(f'its own search over [0, {end})', match and match.span()))
    if match is None:
        break
    end = match.span()[1] - 1
print('   this port answers [(0, 4), (0, 2)] - both of them, which is upstream\'s own stepwise answer.')
