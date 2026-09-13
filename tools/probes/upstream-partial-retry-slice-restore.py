# S40b (2026-09-13): the partial pass inherits a slice a (*SKIP) moved.
#
# A search with partial=True runs do_match_2 twice (upstream/src/_regex.c:18160): a non-partial pass
# first, then - only if that failed - a partial one from the SAME text_pos. Upstream restores text_pos
# and NOTHING ELSE, so a (*SKIP) in the first pass leaves slice_start (:14553, or slice_end when the
# node is RE_STATUS_REVERSE) moved for the second, and the second pass's search retry then jumps every
# start position below it. Upstream is largely masked by its 'search_start' prefilter, which this port
# does not have; this port is not.
#
# What this probe is for: the slice file asks whether the restore goes at BOTH ends or the FORWARD end
# only, and the evidence is what upstream answers on the reversed row that the both-ends restore
# introduces. Every case below also asks upstream's own anchored 'match' at each start (or, on a
# reversed row, each endpos), because a search that reports a position its own anchored matcher beats
# is not leftmost - which settles a row without needing upstream at all.
import sys

import regex

sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

CASES = [
    # The defect, minimised by S40a from seed-7 row 97927. Upstream (1, 1); this port (2, 0).
    ('minimised forward', r'\b\D(*SKIP)z', ' A', 0x0),
    # S37's pinned answer, which S40a says is this same defect seen from the other side.
    ('S37 pinned', r'(?:\w{2,}(*SKIP)\w|\w)\B', 'a.Aa', 0x0),
    # THE THREE REVERSED ROWS, exactly as the 6000-row three-seed gate drew them on 2026-09-13 -
    # pattern, subject and flags copied out of TestResults/oracle/wave-<seed>.jsonl, not retyped.
    # Under (?r) the verb moves slice_END rather than slice_start, and these are the rows where
    # restoring that end makes this port answer something upstream does not. S40a saw the first of
    # them and read it as a reason NOT to restore; all three say the opposite.
    ('seed 7 row 101560', r'(?r)\b(?:[^a-f](*SKIP)[\p{L}\p{N}]|[[:digit:]])(?P<g1>[A-Z]{0,})', 'a\n', 0x8),
    ('seed 20260913 row 96397', r'(?r)\M(?:[^[\p{L}--[a-z]]](*SKIP)[a-f]|[A-Z])', 'A\U00010428', 0x4102),
    (
        'seed 20260913 row 99556',
        r'(?r)(?:[^a-f](*SKIP)\S|[\p{L}\p{N}])(?(?<![\w--[0-9]])[\p{L}\p{N}]|[\w--[0-9]])',
        '\U00010400_  ',
        0x100,
    ),
]
# The four FORWARD wave rows S40b fixed are not here: they no longer diverge, so the gate itself is
# their check. A hand-invented subject for a recorded pattern is a different row, so the recorded
# rows are replayed with `tools/run-oracle.ps1 -Rows <file> -SkipRecord` rather than retyped.


def show(m):
    return None if m is None else (m.span(), 'partial' if m.partial else 'complete')


print(f'regex {regex.__version__}')
for label, pat, subject, flags in CASES:
    compiled = regex.compile(pat, flags, cache_pattern=False)
    found = compiled.search(subject, partial=True)
    print(f'\n== {label}  {pat!r} on {subject!r} (len {len(subject)}) flags 0x{flags:x}')
    print(f'   search(partial=True)                 {show(found)}')

    # A reversed pattern is anchored by its END, so sweep the bound that actually moves the anchor.
    # Same spelling test as upstream-search-start-whole-region-partial.py.
    reversed_row = '(?r' in pat or bool(flags & 0x400)
    answered = 0
    for bound in range(len(subject) + 1):
        anchored = (
            compiled.match(subject, 0, bound, partial=True)
            if reversed_row
            else compiled.match(subject, bound, partial=True)
        )
        if anchored is not None:
            answered += 1
            where = f'endpos={bound}' if reversed_row else f'pos={bound}'
            print(f'   match({where}, partial=True)         {show(anchored)}')
    if answered == 0:
        print(f'   no anchored partial anywhere         ({"endpos" if reversed_row else "pos"} 0..{len(subject)})')

    # The verb is the whole mechanism: with it gone nothing moves a slice, so the two passes see the
    # same bounds and the divergence has to go with it.
    for replacement, name in (('', 'verb deleted'), ('(*PRUNE)', 'verb -> (*PRUNE)')):
        other = regex.compile(pat.replace('(*SKIP)', replacement), flags, cache_pattern=False)
        print(f'   {name:20} search(partial)   {show(other.search(subject, partial=True))}')

    # And the non-partial search, which is the first of the two passes on its own: if it succeeds
    # there is no retry and the defect cannot arise, so a row that reaches here failed it.
    print(f'   search(no partial)                   {show(compiled.search(subject))}')
