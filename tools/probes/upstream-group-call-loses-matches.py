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


# S40a (2026-09-13), seed 20260913 row 93133, and the same family seen one row further on: this port
# matched, reached the substitution's template and REFUSED it, where upstream lost the match and
# never expanded a template at all. The report therefore shows an exception against a no-op
# substitution, which looks like a crash in this port until the template is asked of upstream
# separately - which is what this section does. `{1[2]}` names a third capture of a group that made
# one, and upstream raises IndexError for it the moment it has a match to expand against.
PAT_93133 = r'(?r)\b(?<g>[ab]+)(?=(?&g))'
NOCALL_93133 = r'(?r)\b(?<g>[ab]+)'
INLINED_93133 = r'(?r)\b(?<g>[ab]+)(?=[ab]+)'
SUBJECT_93133 = 'ba)((a)((a'
TEMPLATE_93133 = '{{{0[-1]}ab{1[2]}'


def answer(label, call):
    try:
        print(f'   {label:<44} {call()!r}')
    except Exception as e:  # noqa: BLE001 - the exception is the answer
        print(f'   {label:<44} {type(e).__name__}: {e}')


print('\n== seed 20260913 row 93133 - the lost match seen as an exception in the port')
print(f'   subject {SUBJECT_93133!r} template {TEMPLATE_93133!r}')
answer('the row as recorded', lambda: regex.subf(PAT_93133, TEMPLATE_93133, SUBJECT_93133))
answer('does upstream match at all', lambda: regex.search(PAT_93133, SUBJECT_93133))
answer('the called body written out', lambda: regex.search(INLINED_93133, SUBJECT_93133))
answer('the lookahead deleted - it matches', lambda: regex.search(NOCALL_93133, SUBJECT_93133))
answer('...and the SAME template then raises', lambda: regex.subf(NOCALL_93133, TEMPLATE_93133, SUBJECT_93133))
answer('group 1 made this many captures', lambda: regex.search(NOCALL_93133, SUBJECT_93133).captures(1))

# And the measurement that says the port's exception is upstream's own answer rather than a crash:
# every index form a format field can take, over a pattern both engines match. The port's answers
# are recorded beside upstream's - measured 2026-09-13 against
# src/FuzzyRegex/bin/Debug/net10.0/FuzzyRegex.dll, and pinned by
# tests/FuzzyRegex.Tests/Ported/Format/SubscriptedCapturesTests.cs for the forms it covers.
OURS_BY_FIELD = {
    '{0}': "'ab'",
    '{0[0]}': "'ab'",
    '{0[-1]}': "'ab'",
    '{0[-2]}': 'rejected',
    '{1[0]}': "'a'",
    '{1[2]}': 'rejected',
    '{2[-1]}': "'b'",
}

print('\n== every index form a format field can take, on (a)(b) over "ab"')
print(f'   {"field":<10} {"upstream":<44} port')
for field, ours in OURS_BY_FIELD.items():
    try:
        theirs = repr(regex.subf(r'(a)(b)', field, 'ab'))
    except Exception as e:  # noqa: BLE001 - the exception is the answer
        theirs = f'{type(e).__name__}: {e}'
    print(f'   {field:<10} {theirs:<44} {ours}')

# S43 (2026-09-13), seed 99991 row 10201 of a 6000-row `fuzzy,interactions` wave, and the strongest
# reproduction this family has. Every row above needs a wave-sized pattern and resisted minimisation;
# this one cuts to two constructs over a subject of repeated 'a's, and then loses the match at EVERY
# subject length rather than at one, with no partial asked for anywhere. The lookbehind consumes
# nothing, so it cannot remove a match - this family's argument without a zero-width REPEAT in it.
#
# The wave drew the row with partial=True, where upstream degrades the complete match to a PARTIAL
# and leaves the trailing group unset rather than losing it outright; the last block below shows
# that door beside this one.
CALLED_10201 = r'(?P<g1>[[:alpha:]])(?:(?(1)(?<=(?&g1))[^\p{L}]|[A-Z]))([^\d]*)'
INLINED_10201 = r'(?P<g1>[[:alpha:]])(?:(?(1)(?<=[[:alpha:]])[^\p{L}]|[A-Z]))([^\d]*)'


def span_of(pattern, subject, **kwargs):
    m = regex.compile(pattern).search(subject, timeout=10.0, **kwargs)
    if m is None:
        return 'None'
    return f'{m.span()}{"P" if m.partial else ""}'


print('\n== seed 99991 row 10201 - the call loses the match at every subject length')
print(f'   {"subject":<12} {"upstream":<10} {"inlined":<10} call written out as the class it calls')
for n in range(1, 7):
    subject = 'a' * n + ' '
    called = span_of(CALLED_10201, subject)
    inlined = span_of(INLINED_10201, subject)
    lost = '   <-- lost' if called == 'None' and inlined != 'None' else ''
    print(f'   {subject!r:<12} {called:<10} {inlined:<10}{lost}')

print('\n   the same row asked with partial=True, which is how the wave drew it')
for n in (1, 4, 6):
    subject = 'a' * n + ' '
    partial = span_of(CALLED_10201, subject, partial=True)
    print(f'   {subject!r:<12} {partial:<10} {span_of(INLINED_10201, subject):<10} degraded to a partial, group 2 unset')

# ...and cutting row 10201 went all the way down, which no earlier row of this family did. THREE
# ITEMS: a named group, a lookbehind that CALLS it, and one more item. The entry's "the minimal form
# is not established" paragraph was written before this and is corrected there. Every earlier attempt
# shrank a row that held the call inside a CONDITIONAL inside a REPEAT, and cut away the repeat or
# the conditional - the pieces the surrounding match needed - rather than the call's own setting.
MINIMAL = r'(?P<g1>\w)(?<=(?&g1))\W'

print('\n== the minimal form, and the isolation - all over "aaaa " unless the row says otherwise')
print(f'   {"upstream":<10} variant')
for pattern, why in (
    (MINIMAL, 'the call'),
    (r'(?P<g1>\w)(?<=(?P>g1))\W', 'the other call syntax'),
    (r'(?<=(?&g1))\W(?P<g1>\w)', 'the call BEFORE the group it calls'),
    (r'(?P<g1>\w)(?<=\w)\W', 'the class written out - the answer upstream owes both'),
    (r'(?P<g1>\w)(?<=[a-z])\W', 'a different class that also matches'),
    (r'(?P<g1>\w)(?=\W)\W', 'a lookAHEAD instead, so the directions agree'),
    (r'(?P<g1>\w)(?=(?&g1))\w', 'a lookahead that CALLS, directions agreeing'),
):
    print(f'   {span_of(pattern, "aaaa "):<10} {why}')
print(f'   {span_of(r"(?P<g1>\w)(?&g1)\W", "aaa "):<10} the call where it CONSUMES, no lookaround   (over "aaa ")')

print('\n   and it is no subject-length threshold - the call loses it at every length')
print('   THE REPRODUCTION MUST USE THREE CHARACTERS, NOT TWO. At two this port answers None as')
print('   well, and not because it shares this bug: a call counts towards min_width at the width')
print('   of the group it calls even inside a zero-width lookaround, so min_width is 3 here and')
print("   do_exact_match's width early-out refuses a two-character subject before matching starts.")
print('   That inflation is upstream\'s, this port reproduces it deliberately (S40c), and it MASKS')
print('   this entry at exactly one length. One more character separates the two.')
print(f'   {"subject":<12} {"call":<10} {"written out":<12} this port, the call')
OURS_BY_SUBJECT = {1: 'None', 2: '(1, 3)', 3: '(2, 4)', 4: '(3, 5)', 5: '(4, 6)', 6: '(5, 7)', 7: '(6, 8)'}
for n in range(1, 8):
    subject = 'a' * n + ' '
    inline = span_of(r'(?P<g1>\w)(?<=\w)\W', subject)
    masked = '   <-- masked, see above' if n == 1 else ''
    print(f'   {subject!r:<12} {span_of(MINIMAL, subject):<10} {inline:<12} {OURS_BY_SUBJECT[n]}{masked}')
