# S75 (2026-09-20): the owner's `(foobar){e}` case, which the demo drew as eleven `d` markers on
# top of each other.
#
#     pattern  (foobar){e}
#     subject  xirefoabralfobarxie
#
# The drawing was wrong and the engine was right, so this probe is the half that proves the second
# half: what upstream answers, span by span, with the errors each match spent. Run it before
# changing how the markers are drawn, because a marker row built against a wrong span count would
# look correct and be measuring the demo's own bug.
#
# `{e}` carries NO bound, so every position matches and the answer is long. The two rows the slice
# is about are the last two: a match of a single character that spent one substitution and five
# deletions, and an EMPTY match at the end of the subject that spent six. Six deletions in one
# place is six marks stacked at one x-position under the old drawing, and an empty match has no
# character to hang a highlight on at all.
#
# Pair: tools/probes/s75-stacked-deletions.cs runs the same case through this port.
#
#     python tools/probes/s75-stacked-deletions.py

import regex

PATTERN = '(foobar){e}'
SUBJECT = 'xirefoabralfobarxie'

print(f'regex {regex.__version__}')
print(f'pattern {PATTERN!r}  subject {SUBJECT!r}')
print()
print(f'{"#":>3}  {"span":>9}  {"text":<10}  {"s":>2} {"i":>2} {"d":>2}  fuzzy_changes')

compiled = regex.compile(PATTERN, regex.VERSION1)
for number, match in enumerate(compiled.finditer(SUBJECT), start=1):
    subs, inss, dels = match.fuzzy_changes
    substitutions, insertions, deletions = match.fuzzy_counts
    span = f'({match.start()},{match.end()})'
    print(
        f'{number:>3}  {span:>9}  {match.group()!r:<10}  '
        f'{substitutions:>2} {insertions:>2} {deletions:>2}  '
        f'subs={subs} ins={inss} dels={dels}'
    )
