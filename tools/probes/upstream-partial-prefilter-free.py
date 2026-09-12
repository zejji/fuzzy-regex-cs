# S34 item 3 (2026-09-12): the bounded-lazy-repeat partial rows, with and without upstream's
# required-string prefilter, the way tools/record-oracle.py records `partial-sliced`.
import regex

inner = regex._regex.compile


def without_required_string(*args):
    args = list(args)
    args[7] = -1
    args[8] = None
    return inner(*args)


def both(pat, subj, pos, endpos):
    plain = regex.compile(pat, cache_pattern=False)
    regex._regex.compile = without_required_string
    try:
        free = regex.compile(pat, cache_pattern=False)
    finally:
        regex._regex.compile = inner

    a = plain.search(subj, pos, endpos, partial=True)
    b = free.search(subj, pos, endpos, partial=True)
    print(
        ascii(
            f"{pat!r} on {subj!r}[{pos}:{endpos}]: plain {a.span() if a else None} "
            f"partial={a.partial if a else '-'}   prefilter-free {b.span() if b else None} "
            f"partial={b.partial if b else '-'}"
        )
    )


both(r'(?r)A(.??)', '_ﬃ', 0, 2)
both(r'(?r)A(.?)', '_ﬃ', 0, 2)
both(r'(?r)A(.)', '_ﬃ', 0, 2)
both(r'(?r)A', '_ﬃ', 0, 2)
both(r'(?r)A(.??)', 'ab', 0, 2)
both(r'^([A-Z]??)__$', '__aA ', 0, 5)
