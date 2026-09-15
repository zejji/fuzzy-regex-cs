# The three unexpected `verbs` rows at 2000 rows (seeds 7 and 4242) after S35, 2026-09-12: all are
# reversed overlapped scans with (*SKIP) where upstream reports MORE matches than this port. For each,
# ask upstream's own match()/search() at every endpos so S36 can judge which side is right.
import regex
def spans(ms): return [m.span() for m in ms]
cases = [
    (r'(?r)(?:[a\d]*(*SKIP)\D|\p{Nd})(?:[\p{L}\p{N}]{1,3}(*SKIP)\S|.)((?>\s+(*PRUNE)A))', 'İİAAA AS', 0),
    (r'(?r)(?:[^\d]{2,4}(*SKIP)A|\U0001F600)$', '\U0001F600\U0001F600\U00010400\U00010400\U0001F600\r\n\U0001F600A', regex.M),
    (r'(?r)([^a]{2,4}(*SKIP)[a\d])((?:[^\d]++(*SKIP)\s|\ ))', 'b0 0\n A', 0),
]
for p, s, f in cases:
    c = regex.compile(p, f)
    print(repr(p)[:70]); print("  overlapped finditer:", spans(c.finditer(s, overlapped=True)))
    print("  plain finditer:     ", spans(c.finditer(s)))
    print("  search(pos=0,endpos=e) for each e:", [(e, c.search(s, 0, e).span() if c.search(s, 0, e) else None) for e in range(len(s), 0, -1)])
    print("  match(endpos=e) for each e:      ", [(e, c.match(s, 0, e).span() if c.match(s, 0, e) else None) for e in range(len(s), 0, -1)])

# ---------------------------------------------------------------------------------------------
# THE FOURTH FACT, added by S52 sitting 10 (2026-09-15, regex 2026.9.10) for seed 4242 row 119927
# of the 6000-row gate: the carried slice_end moving a CAPTURE'S END while every whole-match span
# stays put. The three cases above compile WITH upstream's required-string prefilter and the
# `verbs` generator is recorded WITHOUT it (tools/record-oracle.py:325); on this row that is the
# difference between reproducing it and not, so this section asks the way the wave does.
_REQ_OFFSET_ARG, _REQ_CHARS_ARG = 7, 8  # tools/probes/gate-divergence-doors.py:81
_inner = regex._regex.compile


def _without_required_string(*args):
    args = list(args)
    args[_REQ_OFFSET_ARG] = -1
    args[_REQ_CHARS_ARG] = None
    return _inner(*args)


def compile_prefilter_free(pattern):
    regex._regex.compile = _without_required_string
    try:
        return regex.compile(pattern, cache_pattern=False)
    finally:
        regex._regex.compile = _inner


def with_captures(matches):
    return [(m.span(), m.span("g1")) for m in matches]


ROW = "(?r)^(?:[^a]*?(*SKIP)\\w|‍)(?P<g1>\\S*(*SKIP)A)"
SUBJECT = "aa‍‍AAa"

print()
print("=== seed 4242 row 119927, prefilter-free, (span, g1 span) per match")
print("  as drawn          ", with_captures(compile_prefilter_free(ROW).finditer(SUBJECT, overlapped=True)))
print("  (*SKIP)->(*PRUNE) ", with_captures(compile_prefilter_free(ROW.replace("(*SKIP)", "(*PRUNE)")).finditer(SUBJECT, overlapped=True)))
print("  verb deleted      ", with_captures(compile_prefilter_free(ROW.replace("(*SKIP)", "")).finditer(SUBJECT, overlapped=True)))

# Upstream's own scan taken one match at a time: each `search` is a fresh attempt, so no bound a
# previous match's (*SKIP) moved is still moved. This is the walk the recorder stores as the row's
# `anchoredScan`, and the second arm of `overlapped-skip-stale-slice-reversed`'s predicate.
_walk = []
for _end in range(len(SUBJECT), -1, -1):
    _m = compile_prefilter_free(ROW).search(SUBJECT, 0, _end)
    if _m is not None and (not _walk or (_m.span(), _m.span("g1")) != _walk[-1]):
        _walk.append((_m.span(), _m.span("g1")))
print("  stepwise walk     ", _walk)
print("  with the prefilter", with_captures(regex.compile(ROW, cache_pattern=False).finditer(SUBJECT, overlapped=True)))

print("  each drawn match's capture against its own span:")
for _i, (_whole, _g1) in enumerate(with_captures(compile_prefilter_free(ROW).finditer(SUBJECT, overlapped=True))):
    print(f"    match {_i}: span {_whole}  g1 {_g1}  capture outside its own match: {_g1[0] < _whole[0] or _g1[1] > _whole[1]}")
print("  the pattern holds no lookaround and no \\K:", not any(t in ROW for t in ("(?=", "(?!", "(?<=", "(?<!", "\\K")))
