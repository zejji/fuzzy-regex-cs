r"""Does a second engine report row 97332's partial, the one no longer subject can complete?

Row 97332 of the 6000-row date-seed gate has upstream `regex` answering a partial `fullmatch` over
'.a' for `(\S??)\.`, where this port answers nothing. Spec amendment 16 asks for a real run of a
second engine rather than an argument, so this drives libpcre2-8-0.dll the way
`tools/probes/pcre2-partial-truncation-assertions.py` does.

PCRE2 rejects PCRE2_ENDANCHORED together with either partial option (error -34, "bad option value"),
so the fullmatch door is asked as ANCHORED plus a trailing `\z`, which reaches the same end of the
subject. The last block shows the refusal, so the workaround is visible rather than assumed.

    python tools/probes/pcre2-fullmatch-partial-with-no-completion.py

WHAT IT MEASURED, PCRE2 10.47 (2025-10-21), on 2026-09-22:

  PCRE2 10.47 2025-10-21
    '(\\S??)\\.'   '.a'     plain=match (0,1)      SOFT=match (0,1)        HARD=match (0,1)
    '(\\S??)\\.\\z' '.a'     plain=None             SOFT=None               HARD=None
    '(\\S??)\\.'   '.ab'    plain=match (0,1)      SOFT=match (0,1)        HARD=match (0,1)
    '(\\S??)\\.\\z' '.ab'    plain=None             SOFT=None               HARD=None
    '(\\S??)\\.'   '.'      plain=match (0,1)      SOFT=match (0,1)        HARD=match (0,1)
    '(\\S??)\\.\\z' '.'      plain=match (0,1)      SOFT=match (0,1)        HARD=PARTIAL (0,1)
    '(\\S??)\\.'   ''       plain=None             SOFT=None               HARD=None
    '(\\S??)\\.\\z' ''       plain=None             SOFT=None               HARD=None
    '(\\S??)ab\\z' 'aba'    plain=None             SOFT=None               HARD=None
    '(\\S??)ab\\z' 'abab'   plain=None             SOFT=None               HARD=None

    and the ENDANCHORED (true fullmatch) door, which PCRE2 refuses with partial
    '(\\S??)\\.'   '.a'     plain=None             SOFT=error -34: bad option value
    '(\\S??)\\.'   '.'      plain=match (0,1)      SOFT=error -34: bad option value

PCRE2 agrees with this port on every row where upstream reports the phantom. '.a', '.ab', 'aba' and
'abab' are None under both partial modes, because the match failed on text PCRE2 held rather than
running out of text - which is the same rule upstream states for itself and does not follow here.

The '.' row is the control that says PCRE2's partial machinery is switched on and reachable: under
PARTIAL_HARD, `\z` at the end of the available text is unresolved, so PCRE2 escalates the complete
match to a partial. Under PARTIAL_SOFT the complete match wins. That is PCRE2's `hitend` model, the
one this port already follows for boundaries (docs/DIVERGENCES.md, slice S57d).

One row where PCRE2 is stricter than either Python engine: the empty subject is None, where upstream
and this port both answer a partial at (0, 0). PCRE2 reports a partial only once it has inspected a
character, so it has nothing to report over ''. That difference is not in dispute on row 97332 and
is recorded here so the table is not read as agreement on every line.

This is upstream issue 367's family - a partial no continuation can complete - and it does NOT
repeat S49's finding. There (`tools/probes/pcre2-partial-truncation-assertions.py`, 2026-09-14)
PCRE2 granted the same permissive partials upstream did, on rows where a LOOKAROUND was evaluated at
the truncation point, so 367 was judged a documentation gap. Here no assertion sits at the
truncation point, PCRE2 refuses the partial, and upstream grants one at '.a' and refuses it at '.ab'.
"""

import ctypes

# Git for Windows ships this; the same path the other pcre2-* probes here use.
lib = ctypes.CDLL(r"C:\Program Files\Git\mingw64\bin\libpcre2-8-0.dll")

PARTIAL_SOFT, PARTIAL_HARD = 0x10, 0x20
ANCHORED, ENDANCHORED = 0x80000000, 0x20000000
NOMATCH, PARTIAL = -1, -2

lib.pcre2_compile_8.restype = ctypes.c_void_p
lib.pcre2_compile_8.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_uint32,
                                ctypes.POINTER(ctypes.c_int), ctypes.POINTER(ctypes.c_size_t),
                                ctypes.c_void_p]
lib.pcre2_match_data_create_from_pattern_8.restype = ctypes.c_void_p
lib.pcre2_match_data_create_from_pattern_8.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
lib.pcre2_match_8.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_size_t, ctypes.c_size_t,
                              ctypes.c_uint32, ctypes.c_void_p, ctypes.c_void_p]
lib.pcre2_get_ovector_pointer_8.restype = ctypes.POINTER(ctypes.c_size_t)
lib.pcre2_get_ovector_pointer_8.argtypes = [ctypes.c_void_p]
lib.pcre2_get_error_message_8.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_size_t]


def error_message(code):
    buffer = ctypes.create_string_buffer(256)
    lib.pcre2_get_error_message_8(code, buffer, 256)
    return buffer.value.decode()


def run(pattern, subject, compile_options=0, match_options=0):
    error, offset = ctypes.c_int(), ctypes.c_size_t()
    code = lib.pcre2_compile_8(pattern.encode(), len(pattern.encode()), compile_options,
                               ctypes.byref(error), ctypes.byref(offset), None)
    if not code:
        return f"compile error {error.value} at {offset.value}"
    data = lib.pcre2_match_data_create_from_pattern_8(code, None)
    rc = lib.pcre2_match_8(code, subject.encode(), len(subject.encode()), 0, match_options, data,
                           None)
    ovector = lib.pcre2_get_ovector_pointer_8(data)
    if rc == NOMATCH:
        return "None"
    if rc == PARTIAL:
        return f"PARTIAL ({ovector[0]},{ovector[1]})"
    if rc < 0:
        return f"error {rc}: {error_message(rc)}"
    return f"match ({ovector[0]},{ovector[1]})"


version = ctypes.create_string_buffer(256)
lib.pcre2_config_8(11, version)  # PCRE2_CONFIG_VERSION
print("PCRE2", version.value.decode())

ROWS = [
    (r"(\S??)\.", ".a"),        # the minimised row, as a `match` door
    (r"(\S??)\.\z", ".a"),      # and as the fullmatch door, where upstream answers a partial
    (r"(\S??)\.", ".ab"),
    (r"(\S??)\.\z", ".ab"),     # one character on, where upstream answers None
    (r"(\S??)\.", "."),
    (r"(\S??)\.\z", "."),       # a subject the pattern really does fullmatch
    (r"(\S??)\.", ""),
    (r"(\S??)\.\z", ""),
    (r"(\S??)ab\z", "aba"),     # the two-character literal, upstream's other pair of phantoms
    (r"(\S??)ab\z", "abab"),
]

for pattern, subject in ROWS:
    print(f"  {pattern!r:14} {subject!r:8} "
          f"plain={run(pattern, subject, ANCHORED):16} "
          f"SOFT={run(pattern, subject, ANCHORED, PARTIAL_SOFT):18} "
          f"HARD={run(pattern, subject, ANCHORED, PARTIAL_HARD)}")

print("\n  and the ENDANCHORED (true fullmatch) door, which PCRE2 refuses with partial")
for pattern, subject in ((r"(\S??)\.", ".a"), (r"(\S??)\.", ".")):
    print(f"  {pattern!r:14} {subject!r:8} "
          f"plain={run(pattern, subject, ANCHORED | ENDANCHORED):16} "
          f"SOFT={run(pattern, subject, ANCHORED | ENDANCHORED, PARTIAL_SOFT)}")
