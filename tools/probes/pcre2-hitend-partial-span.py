r"""Which span, and which groups, a second engine reports for a partial it derived from `hitend`.

Ledger entry 21 settles WHETHER a boundary at the end of the available text should escalate to a
partial; `tools/probes/pcre2-partial-truncation-assertions.py` is the evidence for that. S57d has to
implement it, and implementing it forces a question that probe never asked, because every row in it
is ANCHORED at offset 0 and so has only one candidate start: WHICH SPAN does the escalated partial
report?

PCRE2 is the model this port is following, so the answer is measured here rather than argued. The
rows below drive libpcre2-8-0.dll the same way the other pcre2-* probes do, and each one isolates a
single question.

  A. The start offset. An unanchored search whose only end-reaching attempt begins at a non-zero
     offset tells us whether ovector[0] is that attempt's start or the start of the subject.
  B. Leftmost. Two end-reaching attempts, so we can see which of them is reported.
  C. Capture groups. Whether a hitend-derived partial carries group spans at all.
  D. Zero-width. Whether an attempt that has consumed nothing still escalates. This is the row that
     bears on S50's first narrowing, which this port keeps.

    python tools/probes/pcre2-hitend-partial-span.py

WHAT IT MEASURED, PCRE2 10.47 (2025-10-21), on 2026-09-21: see the closing notes of
`docs/plan/slices/done/S57d-*.md`, which quote the output beside the port's answers.
"""

import ctypes

# Git for Windows ships this; the same path the other pcre2-* probes here use.
lib = ctypes.CDLL(r"C:\Program Files\Git\mingw64\bin\libpcre2-8-0.dll")

PARTIAL_SOFT, PARTIAL_HARD = 0x10, 0x20
ANCHORED, NO_START_OPTIMIZE = 0x80000000, 0x10000
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
lib.pcre2_get_ovector_count_8.restype = ctypes.c_uint32
lib.pcre2_get_ovector_count_8.argtypes = [ctypes.c_void_p]
lib.pcre2_get_error_message_8.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_size_t]

UNSET = ctypes.c_size_t(-1).value


def error_message(code):
    buffer = ctypes.create_string_buffer(256)
    lib.pcre2_get_error_message_8(code, buffer, 256)
    return buffer.value.decode()


def run(pattern, subject, compile_options=0, match_options=0, groups=False):
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
    if rc < 0 and rc != PARTIAL:
        return f"error {rc}: {error_message(rc)}"
    label = "PARTIAL" if rc == PARTIAL else "match"
    answer = f"{label} ({ovector[0]},{ovector[1]})"
    if groups:
        # On a partial, rc is PARTIAL rather than a pair count, so read every pair the match data
        # holds and let the unset sentinel say which ones took part.
        pairs = lib.pcre2_get_ovector_count_8(data)
        spans = []
        for i in range(1, pairs):
            start, end = ovector[2 * i], ovector[2 * i + 1]
            spans.append("unset" if start == UNSET else f"({start},{end})")
        answer += "  groups: " + (", ".join(spans) if spans else "none")
    return answer


version = ctypes.create_string_buffer(256)
lib.pcre2_config_8(11, version)  # PCRE2_CONFIG_VERSION
print("PCRE2", version.value.decode())

print("\n# A. Unanchored search: is ovector[0] the end-reaching attempt's start, or the subject's?")
# 'cd' sits at offset 3 of 'xabcd', and \B there is decided against the end of the subject: after a
# word character with nothing to its right, a boundary IS present, so \B fails - but it failed only
# because the text stopped. No earlier start can match 'cd' at all.
for pattern, subject in ((r"cd\B", "xabcd"), (r"cd\b", "xabcd"), (r"d\B", "xabcd")):
    print(f"  {pattern!r:10} {subject!r:9} plain={run(pattern, subject):14} "
          f"SOFT={run(pattern, subject, 0, PARTIAL_SOFT):16} "
          f"HARD={run(pattern, subject, 0, PARTIAL_HARD)}")

print("\n# B. Two end-reaching attempts and no match at all: which start is reported?")
# Over 'ac', 'a*c\B' reaches the end from start 0 (consuming 'ac') and again from start 1
# (consuming 'c'), and the \B fails both times, so nothing else can decide the answer.
for pattern, subject in ((r"a*c\B", "ac"), (r"a*c\B", "xac"), (r"a+\B", "aa"), (r"a+\B", "baa")):
    print(f"  {pattern!r:10} {subject!r:9} plain={run(pattern, subject):14} "
          f"SOFT={run(pattern, subject, 0, PARTIAL_SOFT):16} "
          f"HARD={run(pattern, subject, 0, PARTIAL_HARD)}")

print("\n# C. Does a hitend-derived partial carry capture groups?")
# pcre2partial(3), "Partial matching using pcre2_match()": on a partial match only ovector[0] and
# ovector[1] are set, and "the rest of the ovector is undefined". The garbage below is that
# sentence measured: these pairs were never written, so nothing can be read off them.
for pattern, subject in ((r"(a)(b)\B", "ab"), (r"(?!(True|False)\b)(.*)", "True")):
    print(f"  {pattern!r:24} {subject!r:8} SOFT={run(pattern, subject, ANCHORED, PARTIAL_SOFT, groups=True)}")
    print(f"  {'':24} {'':8} HARD={run(pattern, subject, ANCHORED, PARTIAL_HARD, groups=True)}")

print("\n# D. Zero-width: does an attempt that consumed nothing still escalate?")
# '\b' over '' reads for a neighbour that is not there. If PCRE2 escalated here it would report a
# zero-width partial at the truncation point, which this port's `search-start-partial` pin refuses.
for pattern, subject in ((r"\b", ""), (r"\bz", ""), (r"\Bz", ""), (r"\b", "a"), (r"\bz", "a"), (r"z\b", "")):
    print(f"  {pattern!r:6} {subject!r:5} plain={run(pattern, subject):14} "
          f"SOFT={run(pattern, subject, 0, PARTIAL_SOFT):16} "
          f"HARD={run(pattern, subject, 0, PARTIAL_HARD)}")

print("\n# E. Is D the boundary escalating, or a start optimisation? (PCRE2_NO_START_OPTIMIZE)")
# `PartialMatchingTests.A_reverse_search_for_a_boundary_at_the_end_of_an_empty_subject_finds_no_
# partial_here` says PCRE2's partial on the empty subject comes from a rule of PCRE2's own - the
# "next pattern item must be one that inspects a character" test - rather than from the boundary.
# A bare '\b' has no next item at all, so if that reading were right these rows would not be
# partial; and turning the start optimisations off removes the other candidate explanation.
for pattern, subject in ((r"\b", ""), (r"\B", ""), (r"\b", "a"), (r"\bz", ""), (r"$", ""), (r"\b\b", "")):
    print(f"  {pattern!r:6} {subject!r:5} SOFT={run(pattern, subject, 0, PARTIAL_SOFT):16} "
          f"SOFT+NO_START_OPTIMIZE={run(pattern, subject, NO_START_OPTIMIZE, PARTIAL_SOFT)}")
