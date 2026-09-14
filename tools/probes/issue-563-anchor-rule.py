# Upstream issue 563 (and 564): the evidence that the "no insertion at the search anchor" rule
# (_regex.c:10214) is applied at exactly ONE position out of every position a search visits, and
# that upstream's own answers are inconsistent because of it.
#
#     python tools/probes/issue-563-anchor-rule.py
#
# WHAT IT MEASURED on 2026-09-14 against regex 2026.9.10. Each section below prints the rows the
# S50 fix and its tests rest on; the comments record what upstream answered.
#
# 1. THE ISSUE, as reported. One leading space, or a first word that starts with the literal, is
#    the whole difference - so position 0 is the only thing that fails:
#       \m(?:Y){i}\M  over 'XY YX'   -> ['YX']            <- 'XY' is missing
#       \m(?:X){i}\M  over 'XY YX'   -> ['XY', 'YX']
#       \m(?:Y){i}\M  over ' XY YX'  -> ['XY', 'YX']
#
# 2. THE MECHANISM, isolated. The SAME subject and the SAME winning span behave differently purely
#    according to where the search was told to start, because search_anchor is set once per search
#    (init_match, _regex.c:3410) and never per candidate position:
#       search(' XY', pos=0) -> span (1, 3) 'XY'
#       search(' XY', pos=1) -> None
#
# 3. WHY THE FIX IS A GENERALISATION OF UPSTREAM'S OWN BEHAVIOUR, not a new rule. '^' and '\A'
#    already escape the rule, because basic_match turns a pattern anchored to the start of the
#    string into an anchored match and stops searching. So upstream KEEPS a match that begins with
#    an inserted character - but only when the anchoring applies:
#       ^(?:abc){i<=1}      over 'xabc'    -> ['xabc']
#       \A(?:abc){i<=1}     over 'xabc'    -> ['xabc']
#       (?m)^(?:abc){i<=1}  over 'xabc'    -> []          <- the same pattern, no anchoring
#       (?=x)(?:abc){i<=1}  over 'xabc'    -> []
#
# 4. UPSTREAM ALREADY ALLOWS A RUNAWAY LEADING INSERTION - everywhere except the anchor. These two
#    subjects differ by one leading space, and the answer at the same relative position differs:
#       \m(?:Y){i}\M  over 'q XY YX'   -> ['XY', 'YX']
#       \m(?:Y){i}\M  over ' q XY YX'  -> ['q XY', 'YX']  <- 'q ' swallowed, off the anchor
#
# 5. THE ROWS THE FIX MUST NOT MOVE. With no assertion before the fuzzy item, or with one that
#    holds one character later too, "start searching one character later" really is the same match
#    minus an insertion, and upstream's rule is doing its job:
#       (?:abc){i<=1}            over 'xabc'  -> ['abc']
#       (?<![0-9])(?:abc){i<=1}  over 'xabc'  -> ['abc']
#       (?:Y){i}                 over 'qXY'   -> ['XY']
#
# 6. ISSUE 564, which the reporter suspected was related and is: the same rule, reached through
#    BESTMATCH's re-anchoring, so that the LOOSER budget finds fewer matches.
#       (?b)\m(?:Y){1i+1d+1s<=1}\M  over ' XY Z'  -> ['XY', 'Z']
#       (?b)\m(?:Y){1i+1d+1s<=2}\M  over ' XY Z'  -> ['Z']

import os
import sys

sys.path.insert(0, os.path.join('.venvs', 'regex-2026.9.10', 'Lib', 'site-packages'))
import regex  # noqa: E402

SECTIONS = [
    ('1. the issue as reported', [
        (r'\m(?:Y){i}\M', 'XY YX'),
        (r'\m(?:X){i}\M', 'XY YX'),
        (r'\m(?:Y){i}\M', ' XY YX'),
    ]),
    ('3. the ^/\\A exemption, and the same pattern without it', [
        (r'^(?:abc){i<=1}', 'xabc'),
        (r'\A(?:abc){i<=1}', 'xabc'),
        (r'(?m)^(?:abc){i<=1}', 'xabc'),
        (r'(?=x)(?:abc){i<=1}', 'xabc'),
    ]),
    ('4. a runaway leading insertion is fine off the anchor', [
        (r'\m(?:Y){i}\M', 'q XY YX'),
        (r'\m(?:Y){i}\M', ' q XY YX'),
    ]),
    ('5. rows the fix must not move', [
        (r'(?:abc){i<=1}', 'xabc'),
        (r'(?<![0-9])(?:abc){i<=1}', 'xabc'),
        (r'(?:Y){i}', 'qXY'),
    ]),
    ('6. issue 564, the looser budget finding fewer matches', [
        (r'(?b)\m(?:Y){1i+1d+1s<=1}\M', ' XY Z'),
        (r'(?b)\m(?:Y){1i+1d+1s<=2}\M', ' XY Z'),
    ]),
]


def main():
    print('regex', regex.__version__)

    for title, rows in SECTIONS:
        print()
        print(title)
        for pattern, subject in rows:
            print('  %-28s over %-12r -> %r' % (pattern, subject, regex.findall(pattern, subject)))

    print()
    print('2. the mechanism: one span, two search starts')
    compiled = regex.compile(r'\m(?:Y){i}\M')
    for start in (0, 1):
        match = compiled.search(' XY', start)
        found = (match.span(), match.group()) if match else None
        print('  search(%r, pos=%d) -> %r' % (' XY', start, found))


if __name__ == '__main__':
    main()
