#!/usr/bin/env python
r"""S44: issue 613's clamp costs upstream a partial match it used to find.

Found by the Phase 6 sync's own gate - the 6000-row default wave at seed 20260913, row 98050,
`partial` generator - and it is the ONE row of 378,000 that the move from 2026.7.19 to 2026.9.10
changed. Minimised from

    \b([\u0130]+)\1(?:.{3}?(*SKIP)[^[\p{L}--[a-z]]]|\S)   on  '\u0130\u0130SsS', flags 266

to three ASCII characters and no flags.

    >>> regex.compile(r'(a+)\1x(*SKIP)b').search('aax', partial=True)

    2026.7.19  -> (0, 3) partial, group 1 == (0, 1)      <- this port's answer
    2026.9.10  -> (3, 3) partial, group 1 unset

The match upstream used to find is real and reachable: `(a+)` takes 'aa', `\1` cannot match 'aa'
at 2, so the repeat RETREATS to 'a', `\1` matches 'a' at 1, 'x' matches at 2, and 'b' runs off
the end of the subject - which is precisely what a partial match is. 2026.9.10 never gets there,
because commit `b77694a` (issue 613) clamps the GREEDY_REPEAT_ONE retreat limit down to the
current position, and a `(*SKIP)` to the right of the repeat has already moved `slice_start`
above it. The clamp stops the runaway retreat it was written for AND the one legitimate step
this match needed.

Run it against whichever interpreter you want to judge, and against PCRE2 with `--pcre2`:

    python tools/probes/upstream-skip-blocks-a-repeat-retreat.py
    python tools/probes/upstream-skip-blocks-a-repeat-retreat.py --pcre2
"""

from __future__ import annotations

import sys

CASES = (
    (r"(a+)\1x(*SKIP)b", "aax", "the minimised row"),
    (r"(a+)\1x(*PRUNE)b", "aax", "CONTROL: (*PRUNE) prunes the same backtracking and moves no "
                                 "bound - both releases answer (0, 3)"),
    (r"(a+)\1xb", "aax", "CONTROL: no verb at all - both releases answer (0, 3)"),
    (r"(a+)\1x(*SKIP)b", "aaxb", "CONTROL: the same pattern with the 'b' present, so the match "
                                 "is COMPLETE rather than partial - both releases find it"),
    (r"(a+)\1x(*SKIP)b", "aaxyz", "CONTROL: a subject that cannot complete - both answer the "
                                  "zero-width partial, so the difference is not 'any (*SKIP) row'"),
    (r"(a+)\1a(*SKIP)b", "aaa", "CONTROL: no retreat needed - both releases answer (0, 3)"),
)


def with_regex() -> None:
    import regex

    print("regex", regex.__version__)
    for pattern, subject, note in CASES:
        compiled = regex.compile(pattern)
        match = compiled.search(subject, partial=True)
        if match is None:
            answer = "None"
        else:
            spans = [match.span(i) for i in range(compiled.groups + 1)]
            answer = f"{match.span()} partial={match.partial} groups={spans}"
        print(f"  {pattern!r} on {subject!r}\n    -> {answer}  # {note}")


def with_pcre2() -> None:
    """PCRE2's answer to the same question, run rather than derived from its documentation.

    PCRE2 has no `\\1`-with-a-quantified-group difference from `regex` here and supports both
    (*SKIP) and partial matching, so the shape transfers directly. Loaded the way
    tools/probes/pcre2-partial-and-skip.py does.
    """
    import ctypes

    lib = ctypes.CDLL(r"C:\Program Files\Git\mingw64\bin\libpcre2-8-0.dll")
    PARTIAL_SOFT = 0x10
    NOMATCH, PARTIAL = -1, -2
    lib.pcre2_compile_8.restype = ctypes.c_void_p
    lib.pcre2_compile_8.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_uint32,
                                    ctypes.POINTER(ctypes.c_int), ctypes.POINTER(ctypes.c_size_t),
                                    ctypes.c_void_p]
    lib.pcre2_match_data_create_from_pattern_8.restype = ctypes.c_void_p
    lib.pcre2_match_data_create_from_pattern_8.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
    lib.pcre2_match_8.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_size_t,
                                  ctypes.c_size_t, ctypes.c_uint32, ctypes.c_void_p,
                                  ctypes.c_void_p]
    lib.pcre2_get_ovector_pointer_8.restype = ctypes.POINTER(ctypes.c_size_t)
    lib.pcre2_get_ovector_pointer_8.argtypes = [ctypes.c_void_p]

    buf = ctypes.create_string_buffer(256)
    lib.pcre2_config_8(11, buf)
    print("PCRE2", buf.value.decode())

    for pattern, subject, note in CASES:
        err, off = ctypes.c_int(), ctypes.c_size_t()
        code = lib.pcre2_compile_8(pattern.encode(), len(pattern.encode()), 0,
                                   ctypes.byref(err), ctypes.byref(off), None)
        if not code:
            print(f"  {pattern!r} on {subject!r}\n    -> compile error {err.value}  # {note}")
            continue
        data = lib.pcre2_match_data_create_from_pattern_8(code, None)
        rc = lib.pcre2_match_8(code, subject.encode(), len(subject.encode()), 0,
                               PARTIAL_SOFT, data, None)
        if rc == NOMATCH:
            answer = "None"
        elif rc == PARTIAL:
            ov = lib.pcre2_get_ovector_pointer_8(data)
            answer = f"PARTIAL ({ov[0]}, {ov[1]})"
        elif rc > 0:
            ov = lib.pcre2_get_ovector_pointer_8(data)
            answer = "MATCH " + " ".join(f"({ov[2 * i]}, {ov[2 * i + 1]})" for i in range(rc))
        else:
            answer = f"rc={rc}"
        print(f"  {pattern!r} on {subject!r}\n    -> {answer}  # {note}")


if __name__ == "__main__":
    if "--pcre2" in sys.argv:
        with_pcre2()
    else:
        with_regex()
