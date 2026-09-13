# S40d (2026-09-13): does a reversed overlapped scan step by "the last match's END minus one", and
# does `search(subject, 0, endpos)` reproduce that step exactly?
#
# tools/record-oracle.py's `_anchored_scan` records upstream's own answer to an overlapped `(*SKIP)`
# scan asked one match at a time from a fresh state. It refused every reversed row until S40d,
# because stepping a reversed scan means moving `endpos`, which truncates the subject and changes
# what `$`, `\Z`, `\b`, `\B`, `\m` and `\M` mean. That is a property of the PATTERN, so the refusal
# now reads the pattern - and this is the measurement that says the walk is upstream's own step for
# everything else.
#
# The step is `state->text_pos = state->match_pos + step` with `step = -1` when reversed
# (upstream/src/_regex.c:20903), and `match_pos` is where the attempt anchored, which for a reversed
# attempt is the match's end. None of the shapes below carries an end-sensitive item; the zero-width
# ones are here because the forward half of the walk needs `must_advance` and an overlapped scan
# does not, and that argument has to hold on this side too.
import sys

import regex

sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

R = 0x400


def walk(pattern, subject, flags=0):
    """The scan one match at a time, each step a fresh reversed search bounded by the new endpos."""
    compiled = regex.compile(pattern, flags | R, cache_pattern=False)
    found = []
    end = len(subject)
    while 0 <= end <= len(subject) and len(found) < len(subject) + 2:
        match = compiled.search(subject, 0, end)
        if match is None:
            break
        found.append(match.span())
        end = match.span()[1] - 1
    return found


def scan(pattern, subject, flags=0):
    compiled = regex.compile(pattern, flags | R, cache_pattern=False)
    return [m.span() for m in compiled.finditer(subject, overlapped=True)]


print(f'regex {regex.__version__}')

CASES = [
    (r'a.', 'aaa'),
    (r'\w{1,2}', 'abc'),
    (r'a??', 'aa'),
    (r'(a)(b)?', 'abab'),
    (r'.{2,3}', 'abcde'),
    (r'x*', 'axxa'),
    (r'(?:ab|b)', 'abab'),
]
for pattern, subject in CASES:
    scanned, walked = scan(pattern, subject), walk(pattern, subject)
    print(f'   (?r){pattern:<12} {subject!r:<10} scan={scanned}')
    print(f'   {"":<16} {"":<10} walk={walked}  {"SAME" if scanned == walked else "DIFFER"}')

# And the refusal's own evidence, kept beside it: a pattern that DOES read the end of the subject
# disagrees, which is why `_reads_the_end_of_the_subject` exists rather than a blanket reversed walk.
print('\nthe refusal, on a pattern that reads the end of the subject:')
for pattern, subject in ((r'\b', 'bab'), (r'.$', 'aba'), (r'.\Z', 'aba'), (r'.(?=a)', 'aaa')):
    scanned, walked = scan(pattern, subject), walk(pattern, subject)
    print(f'   (?r){pattern:<12} {subject!r:<10} scan={scanned}')
    print(f'   {"":<16} {"":<10} walk={walked}  {"SAME" if scanned == walked else "DIFFER"}')
