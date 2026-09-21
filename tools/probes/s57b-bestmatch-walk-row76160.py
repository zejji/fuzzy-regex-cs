"""S57b: seed 7 row 76160, the row three earlier sittings left unjudged, and its minimisation.

    python tools/probes/s57b-bestmatch-walk-row76160.py

The row is the eighth of `bestmatch-walk-truncated-by-a-skip`. Its `(?b)` + `(*PRUNE)` door is
unusable: 3,092 seconds on 2026-09-21 and then `MemoryError` rather than an answer, which is why
this probe does not ask it. What stands in its place is the MINIMISED row, found by running the
gate's own comparer over every one-edit shortening of the row and keeping any that still diverged,
nine rounds down to `(?b:(}){e}(*SKIP)|)` over a lone sharp s at the row's own flags. Every control on the
minimised row answers in milliseconds.

Both rows are read out of `tools/probes/s57b-gate-rows.jsonl`, so nothing here is transcribed.

Measured 2026-09-21 on regex 2026.9.10.
"""
import json

import regex

MINIMISED = '(?b:(}){e}(*SKIP)|)'
SUBJECT = '\xdf'


def load(number):
    for line in open('tools/probes/s57b-gate-rows.jsonl', encoding='utf-8').read().splitlines():
        row = json.loads(line)
        if row['s57bRow'] == number:
            return row['pattern'], row['subject'], row['flags']
    raise AssertionError('row %s is not in the gate file' % number)


def ask(pattern, subject, flags, label):
    """Print the split the row is drawn as, and the first match's span and cost beside it."""
    try:
        compiled = regex.compile(pattern, flags)
        answer = ascii(compiled.split(subject, timeout=120.0))
        first = compiled.search(subject, timeout=120.0)
        answer += '   search ' + ('%s %s' % (first.span(0), first.fuzzy_counts) if first else 'None')
    except Exception as e:                      # noqa: BLE001 - a refusal is an answer
        answer = '%s: %s' % (type(e).__name__, e)
    print('  %-22s %s' % (label, answer))


def bestmatch_free(pattern):
    """The same pattern with `(?b)` taken out, in whichever of its two spellings the row uses."""
    return pattern.replace('(?b)', '', 1) if '(?b)' in pattern else pattern.replace('(?b:', '(?:', 1)


drawn_pattern, drawn_subject, drawn_flags = load(76160)
print('regex', regex.__version__, 'flags', hex(drawn_flags))

for name, pattern, subject in (
    ('DRAWN row 76160', drawn_pattern, drawn_subject),
    ('MINIMISED', MINIMISED, SUBJECT),
):
    print('===', name, ascii(pattern), 'over', ascii(subject))
    ask(pattern, subject, drawn_flags, 'as drawn')
    if name == 'MINIMISED':
        ask(pattern.replace('(*SKIP)', '(*PRUNE)'), subject, drawn_flags, '(*SKIP) -> (*PRUNE)')
    else:
        print('  %-22s %s' % ('(*SKIP) -> (*PRUNE)', 'MemoryError after 3,092 seconds, not asked here'))
    ask(pattern.replace('(*SKIP)', ''), subject, drawn_flags, 'the verb deleted')

    # The classifying line of `bestmatch-walk-truncated-by-a-skip`: with `(?b)` gone the three
    # spellings have to agree, or the verb's pruning rather than its moved bound is what decides
    # the row.
    free = bestmatch_free(pattern)
    assert free != pattern, pattern
    ask(free, subject, drawn_flags, 'no (?b), (*SKIP)')
    ask(free.replace('(*SKIP)', '(*PRUNE)'), subject, drawn_flags, 'no (?b), (*PRUNE)')
    ask(free.replace('(*SKIP)', ''), subject, drawn_flags, 'no (?b), verb deleted')
