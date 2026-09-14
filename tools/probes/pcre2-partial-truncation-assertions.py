r"""What a second engine does when an assertion sits at the truncation point of a partial match.

S49 (2026-09-14) needed this to judge two open upstream issues that pull in opposite directions:

  * issue 367 says partial matching is too PERMISSIVE - it reports a partial where no continuation
    of the subject could ever complete the match;
  * issue 589 says it is too STRICT - it reports no match at all for a prefix whose continuation
    demonstrably does match ('True' is a prefix of 'Truest').

Both are really one question: when a lookaround or a word boundary is evaluated at the end of the
available text, is its verdict final or provisional? Amendment 16 asks for a real run of a second
engine rather than an argument, so this drives libpcre2-8-0.dll directly, the way
`tools/probes/pcre2-partial-and-skip.py` does.

    python tools/probes/pcre2-partial-truncation-assertions.py

WHAT IT MEASURED, PCRE2 10.47 (2025-10-21), on 2026-09-14:

  367  PCRE2 answers PARTIAL on every row where upstream `regex` and this port answer partial,
       including the reporter's `(?!.+).*` over '1'. So reporting a partial that no continuation
       can complete is what a mature engine does too, and 367 is a documentation gap rather than
       an engine bug. Upstream's own wording promises more than any engine delivers: "whether a
       complete match COULD BE POSSIBLE if the string had not been truncated"
       (`upstream/docs/Features.html:576`). Deciding that in general is not possible - the issue's
       own example encodes primality.

  589  PCRE2 DISAGREES with upstream, and agrees with the reporter: `(?!(True|False)\b)(.*)` over
       'True' gives PARTIAL (0,4) under PARTIAL_SOFT and under PARTIAL_HARD, where upstream and
       this port both answer None. The mechanism is visible in the bare `True\b` row - SOFT says
       `match (0,4)` but HARD says `PARTIAL (0,4)` - so PCRE2 treats a boundary at the end of the
       available text as UNRESOLVED and escalates rather than deciding it against text it has not
       seen. `True\B` over 'True' is the same story from the other side: PARTIAL, not None.

  So upstream is internally consistent - it resolves every assertion against the truncated text -
  but that rule produces a false NEGATIVE in 589, which is the dangerous direction for the
  incremental-input use case its own documentation advertises. 367's false positive is shared by
  PCRE2 and is undecidable in general; 589's false negative is not shared and is decidable.

  One thing PCRE2 will not do at all: PCRE2_ENDANCHORED with either partial option is rejected
  with error -34, "bad option value". A fullmatch-shaped partial is a combination PCRE2 considers
  contradictory, so the 589 rows below use ANCHORED with a trailing `(.*)`, which reaches the same
  end of the subject.
"""

import ctypes

# Git for Windows ships this; the same path the other pcre2-* probes here use.
lib = ctypes.CDLL(r"C:\Program Files\Git\mingw64\bin\libpcre2-8-0.dll")

PARTIAL_SOFT, PARTIAL_HARD = 0x10, 0x20
ANCHORED, ENDANCHORED = 0x80000000, 0x20000000
NOMATCH, PARTIAL = -1, -2

lib.pcre2_compile_8.restype = ctypes.c_void_p
lib.pcre2_compile_8.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_uint32,
                                ctypes.POINTER(ctypes.c_int), ctypes.POINTER(ctypes.c_size_t), ctypes.c_void_p]
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
    rc = lib.pcre2_match_8(code, subject.encode(), len(subject.encode()), 0, match_options, data, None)
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

print("\n# PCRE2 refuses a fullmatch-shaped partial outright, so the 589 rows use ANCHORED")
for pattern, subject in (("abc", "abc"), ("abc", "ab")):
    print(f"  {pattern!r:8} {subject!r:8} ENDANCHORED={run(pattern, subject, ANCHORED | ENDANCHORED):14}"
          f"  +PARTIAL_SOFT={run(pattern, subject, ANCHORED | ENDANCHORED, PARTIAL_SOFT)}")

print("\n# 367: is a partial that no continuation can complete still reported? (upstream: yes)")
p367 = r"(?!(1{2,})\1+$)((?:11)+)$"
for n in (2, 3, 4, 5, 9):
    subject = "1" * n
    print(f"  n={n}  plain={run(p367, subject, ANCHORED):14} "
          f"SOFT={run(p367, subject, ANCHORED, PARTIAL_SOFT):18} "
          f"HARD={run(p367, subject, ANCHORED, PARTIAL_HARD)}")
print(f"  {r'(?!.+).*'!r} over '1'  plain={run(r'(?!.+).*', '1', ANCHORED):14} "
      f"SOFT={run(r'(?!.+).*', '1', ANCHORED, PARTIAL_SOFT):18} "
      f"HARD={run(r'(?!.+).*', '1', ANCHORED, PARTIAL_HARD)}")

print("\n# 589: is an assertion at the end of the available text final or provisional?")
for pattern, subject in ((r"(?!(True|False)\b)(.*)", "True"),
                         (r"(?!(True|False)\b)(.*)", "Truest"),
                         (r"(?!True\b).*", "True"),
                         (r"True\b", "True"),
                         (r"True\b", "Truest"),
                         (r"\bTrue\b", "True"),
                         (r"True\B", "True")):
    print(f"  {pattern!r:26} {subject!r:9} plain={run(pattern, subject, ANCHORED):14} "
          f"SOFT={run(pattern, subject, ANCHORED, PARTIAL_SOFT):16} "
          f"HARD={run(pattern, subject, ANCHORED, PARTIAL_HARD)}")
