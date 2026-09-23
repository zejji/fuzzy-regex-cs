# S37 (2026-09-12): the second symptom of upstream's 'search_start' prefilter. Its partial arms set
# new_position->text_pos to the end of the text or of the slice (upstream/src/_regex.c:8471, :8487)
# while the match start stays where the search began, so the partial it reports covers the WHOLE of
# what was searched - and upstream's own 'match' over that span denies it. This port answers the
# partial its slow path actually reached.
#
# ON FIVE OF THE SIX ROWS that is upstream's own answer, once upstream is asked at that position
# instead. NOT on the third. Seed 20260912's row 2689 is reversed, where 'match(pos=...)' anchors at
# the END, so the sweep below varies 'endpos' on a '(?r)' row: upstream then answers (0, 1) partial
# and (0, 2) and (0, 3) COMPLETE, and never this port's zero-width partial at (0, 0). That row is
# judged on the verb evidence alone - delete the '(*SKIP)' and upstream's search gives this port's
# answer - which is why the entry in ExpectedDivergences.cs lists judged ROWS rather than
# predicating on a rule. The S37 blind review found the claim stated as a rule and it was not one.
#
# S52 (2026-09-15) added the last two, from the LONG-subject generators, and they are the cleanest
# of the six: on both, all four controls agree on this port's answer. Note that the arm in
# ExpectedDivergences.cs carries SEVEN rows - S43's row 5, '(?r)^(?(?<![^\p{L}])\p{L}|...' on '\r.',
# has never been in this probe and is evidenced in S43's own notes instead.
#
# Three rows of a 6000-row 'interactions' wave - one at seed 4242 and TWO at seed 20260912, none at
# seed 7 - every one with a '(*SKIP)', plus the family minimised by hand, plus S52's two.
import sys

import regex

sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

CASES = [
    ('minimised', r'(?:\w{2,}(*SKIP)\w|\w)\B', 'a.Aa', 0x0),
    ('seed 4242 row 3497', r'\b(?:.+(*SKIP)[^\d]|\p{Lu})([^[\p{L}--[a-z]]])+(?(?=\W)[\w--[0-9]])', 'ﬃ\nﬃaa', 0x410A),
    ('seed 20260912 row 2689', r'(?r)[a](\D)*(?:[a-f](*SKIP)[^a-f]|[[a-f]~~[d-k]])\b', 'AA\U0001D518\U00010400', 0x4102),
    ('seed 20260912 row 4313', r'\m(?:[\p{L}\p{N}]{2,}(*SKIP)\p{ASCII}|\w)\B', 'a\U0001F600Aa', 0x108),
    # S52 sitting 7's two, from the LONG-subject generators - and the length is not what found them.
    # Both were drawn on subjects of 3,363 and 18,759 characters and both delta-debug down to THREE
    # codepoints with the whole signature intact, so what the 'partial-long' wrapper contributed is
    # its astral alphabet and these pattern shapes, not its length. Every character of both minima is
    # astral or a line break.
    ('seed 7 row 6997', r'(?r)(?:[a-f](*PRUNE)\d|[[:digit:]])(?(?<![[:digit:]])[abz])(?:\p{Nd}(*SKIP)\s|\p{L})', '\U0001D518\U0001F600\n', 0x0),
    ('seed 20260915 row 7094', r'(?:[\p{L}\p{N}](*SKIP)\p{Nd}|\p{Ll})(\S)*?(?P<g2>\S?)(?:(?(2)(?=(?P>g2))\p{Nd}|.))', '\U00010400\U0001F3FB\U00010400', 0x8),
    # S87 (2026-09-23): a 'partial-sliced' row, so the searched region is the slice [0, 2) and not
    # the subject. The fifth field is (pos, endpos). Its generator is recorded prefilter-free, and
    # the required-string prefilter changes none of the lines below (measured both ways).
    ('seed 20260923 row 5185', r'(?:[[:digit:]]?(*SKIP)[^\d]|\s)([_])?\1\g<1>\b', 'BB__', 0x2, (0, 2)),
]


def show(m):
    return None if m is None else (m.span(), 'partial' if m.partial else 'complete')


print(f'regex {regex.__version__}')
for label, pat, subject, flags, *region in CASES:
    pos, endpos = region[0] if region else (0, len(subject))
    c = regex.compile(pat, flags, cache_pattern=False)
    found = c.search(subject, pos, endpos, partial=True)
    sliced = f' slice [{pos}, {endpos})' if region else ''
    print(f'\n== {label}  {pat!r} on {subject!r} (len {len(subject)}){sliced} flags 0x{flags:x}')
    print(f'   search(partial=True)                {show(found)}')
    if found:
        start, end = found.span()
        # The discriminator tools/record-oracle.py records as `searchOnlyPartial`.
        print(f'   match over that span                {show(c.match(subject, start, end, partial=True))}')
    # Where this port's partial comes from: the position its slow path ran out of text at. A reversed
    # row is anchored at the END by `match`, so sweep the bound that actually moves the anchor, and
    # say so when upstream answers nothing anywhere rather than printing an empty sweep.
    # '(?r' rather than '(?r)', matching ExpectedDivergences.IsReversed: '(?ri)' and '(?i)(?r)' are
    # reversed too, and swept on the wrong bound they make this probe print "no match anywhere" for a
    # row upstream does answer. No generator emits those spellings; the cases here are hand-copied.
    reversed_row = '(?r' in pat or bool(flags & 0x400)
    answered = 0
    for bound in range(pos, endpos + 1):
        answer = (c.match(subject, pos, bound, partial=True) if reversed_row
                  else c.match(subject, bound, endpos, partial=True))
        if answer is not None:
            answered += 1
            label = f'endpos={bound}' if reversed_row else f'pos={bound}'
            print(f'   match({label}, partial=True)        {show(answer)}')
    if answered == 0:
        print(f'   no match(..., partial=True) anywhere    ({"endpos" if reversed_row else "pos"} swept {pos}..{endpos})')
    # And that the verb is what puts upstream on the prefilter's path.
    for replacement, name in (('', 'verb deleted'), ('(*PRUNE)', 'verb -> (*PRUNE)')):
        other = regex.compile(pat.replace('(*SKIP)', replacement), flags, cache_pattern=False)
        print(f'   {name:20} search(partial)  {show(other.search(subject, pos, endpos, partial=True))}')
