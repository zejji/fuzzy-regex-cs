# S57 (2026-09-20): the 58 gap-test provenance rows that the sitting-1 audit never actually asked.
#
# Sitting 1 scripted the owner's 2026-09-15 provenance rule into a replay - extract each quoted
# upstream call out of each Gaps comment, re-run it on regex 2026.9.10, compare - and reported
# "599 replayed | 396 mechanically agreed | 203 to the agent | 0 DIFFERENT".
#
# 58 OF THE 599 NEVER REACHED UPSTREAM. The replay died in its own harness, so those rows were
# neither confirmed nor contradicted, and all 58 fell into the 203 the agent judged - 29% of them.
# "0 DIFFERENT" was therefore a statement about 541 asked rows, not 599. Three instrument defects,
# all in .scratch/replay-provenance.py:
#
#   1. IT PREFIXED 'regex.' ONTO A BARE OP CALL (its lines 75-77). A comment quoting a COMPILED
#      pattern's method - p.match(subject, pos) - became module-level regex.match(pattern, string),
#      so the subject was read as the pattern and pos as the subject: TypeError, 26 rows.
#   2. COMMENT SHORTHAND IS NOT A PYTHON NAME. VERSION1, F|I, LONG, pat, s, subject, that, FUZZY
#      are written for a human: NameError, 32 rows.
#   3. IT READ ONE LINE AT A TIME, so a call split across two comment lines was truncated.
#
# This probe re-asks all 58 with the pattern and subject their own test states, taking each from the
# test body a few lines under the comment rather than inventing one. EVERY ONE CONFIRMS: the audit's
# conclusion survives, now over 599 asked rows instead of 541.
#
# The megabyte subjects are reconstructed from OptimiserTrapsTests.cs:73 (lay the 44-character
# filler down until at least 'size', then append the tail once) and the reconstruction is
# self-checking against that file's own stated lengths, so a wrong subject cannot quietly produce a
# wrong verdict.
#
# ONE DEFECT IN THE COMMENTS THEMSELVES came out of it, fixed in the same commit:
# ReverseMatchingTests.cs:290 quoted its calls WITHOUT the 'partial=True' that is the whole
# question, so as written the first line answered None rather than the partial it records. The
# recorded ANSWERS were right; the quoted call did not reproduce them.
import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

SENTENCE = "the quick brown fox jumps over the lazy dog "
MEGABYTE = 1024 * 1024


def pad(tail: str, size: int = MEGABYTE) -> str:
    """OptimiserTrapsTests.cs:73."""
    out, n = [], 0
    while n < size:
        out.append(SENTENCE)
        n += len(SENTENCE)
    out.append(tail)
    return "".join(out)


LONG = pad("a needle in a haystack.")
LONG_PARTIAL = pad("a needle in a hay")
DENSE = pad("a needle in a haystack.", 100 * 1024)
FUZZY = SENTENCE + "and finds a haystakc."
EMOJI = "\U0001F600\U0001F601\U0001F602"
SPINE = "\U0001F600ab"
SURROGATES = "\ud800" + "ab" + "\U0001F600" + "cd"
GB, FR = "\U0001F1EC\U0001F1E7", "\U0001F1EB\U0001F1F7"
VERB_PAT = r"(?r)(?:\p{L}+(*SKIP)\w|A)(?P<g1>(?:[a-f]{1,3}?(*SKIP)A|[\w\s]))"
V1, F, I, M = regex.VERSION1, regex.FULLCASE, regex.IGNORECASE, regex.MULTILINE

print(f"regex {regex.__version__}")
for name, got, want in (("LONG", len(LONG), 1048631), ("LONG_PARTIAL", len(LONG_PARTIAL), 1048625), ("DENSE", len(DENSE), 102455)):
    ok = "OK" if got == want else "MISMATCH"
    print(f"  len({name}) = {got}  (OptimiserTrapsTests.cs:102 states {want})  {ok}")
    if got != want:
        raise SystemExit("subject reconstruction is wrong; every verdict below would be meaningless")
print()


def spans(it):
    return [m.span() for m in it]


def sp(m):
    return None if m is None else (m.span(), m.partial)


# (where the claim is written, the claim as the comment states it, the call)
CASES = [
    ("DemoEngineContract:406", "a partial", lambda: sp(regex.compile(r"\d{4}-\d{2}-\d{2}", V1).search("2026-09", partial=True))),
    ("DemoEngineContract:433", "a complete match", lambda: sp(regex.compile(r"\d{4}-\d{2}-\d{2}", V1).search("2026-09-19", partial=True))),
    ("DemoEngineContract:454", "'ba'", lambda: regex.compile(r"(a)(b)", V1).sub(r"\2\1", "ab")),
    ("DemoEngineContract:475", "'$1'", lambda: regex.compile(r"(a)", V1).sub("$1", "a")),
    ("DemoEngineContract:519", "(0,5) and (10,16)", lambda: spans(regex.compile(r"\L<f>", V1, f=["apple", "cherry"]).finditer("apple pie cherry"))),
    ("Boundary:33", "[(0,2), (2,4)]", lambda: spans(regex.finditer(r"\X", GB + FR))),
    ("Boundary:52", "[(0,2), (2,3)]", lambda: spans(regex.finditer(r"\X", GB + "\U0001F1EB"))),
    ("OptimiserTraps:218", "[(0,0)]", lambda: spans(regex.finditer("^$", ""))),
    ("OptimiserTraps:219", "[(0,0), (1,1)]", lambda: spans(regex.finditer("^$", "\n", M))),
    ("OptimiserTraps:310", "None", lambda: regex.search(r"\b\B", LONG)),
    ("OptimiserTraps:343", "(54,63) counts (0,2,1)", lambda: (lambda m: (m.span(), m.fuzzy_counts))(regex.search("(?:haystack){e<=3}", FUZZY))),
    ("OptimiserTraps:344 (?e)", "(56,63) counts (0,0,1)", lambda: (lambda m: (m.span(), m.fuzzy_counts))(regex.search("(?e)(?:haystack){e<=3}", FUZZY))),
    ("OptimiserTraps:344 (?b)", "(56,63) counts (0,0,1)", lambda: (lambda m: (m.span(), m.fuzzy_counts))(regex.search("(?b)(?:haystack){e<=3}", FUZZY))),
    ("OptimiserTraps:364", "214490", lambda: len(list(regex.finditer("[a-z]{3}[^a-z]", LONG)))),
    ("OptimiserTraps:365", "23832", lambda: len(list(regex.finditer("QUICK", LONG, I | F)))),
    ("OptimiserTraps:367", "(1048610, 1048616)", lambda: regex.search("needle", LONG).span()),
    ("OptimiserTraps:395", "None", lambda: regex.search("(?r)zebra", LONG)),
    ("OptimiserTraps:406", "(1048608, 1048625) partial", lambda: sp(regex.compile(r"a needle in a haystack\.").search(LONG_PARTIAL, partial=True))),
    ("OptimiserTraps:404", "214493", lambda: len(list(regex.finditer(r"\w+", LONG)))),
    ("OptimiserTraps:422", "20957", lambda: len(list(regex.finditer(r"\w+", DENSE)))),
    ("OptimiserTraps:459", "[(0,3), (4,9)]", lambda: spans(list(regex.finditer(r"\w+", LONG))[:2])),
    ("OptimiserTraps:458", "1048631", lambda: len(regex.sub(r"(\w+) (\w+)", r"\2 \1", LONG))),
    ("OptimiserTraps:474", "214494", lambda: len(regex.split(r"\w+", LONG))),
    ("Repeat:150", "(0, 3)", lambda: regex.match(".{3}", SURROGATES).span()),
    ("Repeat:151", "(0, 5)", lambda: regex.match(".+?c", SURROGATES).span()),
    ("Repeat:152", "(0, 6)", lambda: regex.match(".+d", SURROGATES).span()),
    ("ReverseMatching:290", "(2, 2) partial True", lambda: sp(regex.compile(r"(?r)([\p{L}\p{N}])??", F | I).fullmatch("AAB", 2, 2, partial=True))),
    ("ReverseMatching:291", "(2, 2) partial False", lambda: sp(regex.compile(r"(?r)([\p{L}\p{N}])??", F | I).match("AAB", 2, 2, partial=True))),
    ("ReverseMatching:292", "(2, 2) partial False", lambda: sp(regex.compile(r"(?r)([\p{L}\p{N}])??", F | I).search("AAB", 2, 2, partial=True))),
    ("ReverseMatching:293", "(0, 0) partial False", lambda: sp(regex.compile(r"(?r)([\p{L}\p{N}])??", F | I).fullmatch("AAB", 0, 0, partial=True))),
    ("ReverseMatching:294", "(2, 2) partial False", lambda: sp(regex.compile(r"([\p{L}\p{N}])??", F | I).fullmatch("AAB", 2, 2, partial=True))),
    ("FuzzyBestMatch:560", "None", lambda: sp(regex.compile(r"(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)").search("ab.", partial=True))),
    ("FuzzyBestMatch:562", "(2, 3) partial", lambda: sp(regex.compile(r"(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)").match("ab.", 2, partial=True))),
    ("BacktrackingVerb:310", "(0, 5), g1 (4, 5)", lambda: (lambda m: (m.span(), m.span("g1")))(regex.compile(VERB_PAT).search("AAAA00", 0, 5))),
    ("BacktrackingVerb:470", "[(0,3), (1,2)]", lambda: spans(regex.finditer(r"(?r)(?:.{2}(*SKIP)A|x)$", "bxA", M, overlapped=True))),
    ("BacktrackingVerb:630", "(0,3)(1,4)(2,4)(3,4)(4,7)(5,7)", lambda: spans(regex.finditer(r"(?:[^\d](*SKIP)){2,3}", "\r\naabb ", M, overlapped=True))),
    ("BacktrackingVerb:638", "(0,2)(1,3)(2,3)(3,5)", lambda: spans(regex.compile(r"(?:[^\d](*SKIP)){2}").finditer("abcde", overlapped=True))),
    ("BacktrackingVerb:639", "(2, 4)", lambda: regex.compile(r"(?:[^\d](*SKIP)){2}").match("abcde", 2).span()),
    ("Iteration:93", "[(1,3), (0,2)]", lambda: spans(regex.finditer("(?r)..", EMOJI, overlapped=True))),
    ("MatchSpine:51", "(0, 1)", lambda: regex.match(".", "\U0001F600").span()),
    ("MatchSpine:58", "(1, 3)", lambda: regex.search(".b", SPINE).span()),
    ("MatchSpine:69 $", "(2, 2)", lambda: regex.search("$", "ab").span()),
    ("MatchSpine:69 empty", "(0, 0)", lambda: regex.search("", "ab").span()),
    # Measured by S40c on regex 2026.7.19, so this is the row where the version drift could bite;
    # it does not. Its greedy twin is the control the comment names at :335, asked here too.
    ("PartialMatching:333", "(0, 2) partial", lambda: sp(regex.compile(r"^([^a-f]??)([\ ])$", V1 | M | I).search(" \r", partial=True))),
    ("PartialMatching:335 greedy", "no partial", lambda: sp(regex.compile(r"^([^a-f]?)([\ ])$", V1 | M | I).search(" \r", partial=True))),
    ("PartialMatching:421", "(0, 4)", lambda: regex.compile(r"(?:\w{2,}(*SKIP)\w|\w)\B").search("a.Aa", partial=True).span()),
    ("PartialMatching:423", "(2, 4)", lambda: regex.compile(r"(?:\w{2,}(*SKIP)\w|\w)\B").match("a.Aa", 2, partial=True).span()),
    ("PartialMatching:424", "(4, 4)", lambda: regex.compile(r"(?:\w{2,}(*SKIP)\w|\w)\B").match("a.Aa", 4, partial=True).span()),
    ("ReplaceSlice:97", "'aXaaa'", lambda: regex.compile("a").sub("X", "aaaaa", 1, pos=1)),
    ("ReplaceSlice:105", "'aaaXX'", lambda: regex.compile("a").sub("X", "aaaaa", pos=-2)),
    ("ReplaceSlice:106", "'aaaaa'", lambda: regex.compile("a").sub("X", "aaaaa", pos=99)),
    ("ReplaceSlice:107", "'XXXXX'", lambda: regex.compile("a").sub("X", "aaaaa", endpos=99)),
    ("ReversedPartialSlice:281", "(1, 2)", lambda: regex.compile(r"\Bb").search("ab", 1).span()),
    ("GroupCall:119", "None", lambda: regex.compile(r"(?(DEFINE)(?<a>a))(?<=(?&a))c").match("ac", pos=1)),
    ("GroupCall:128 search", "(2, 3), captures ['ab']", lambda: (lambda m: (m.span(), m.captures("ab")))(regex.compile(r"(?(DEFINE)(?<ab>ab))(?<=(?&ab))c").search("abcd"))),
    ("GroupCall:129 match", "None", lambda: regex.compile(r"(?(DEFINE)(?<ab>ab))(?<=(?&ab))c").match("abcd", pos=2)),
    ("GroupCall:132 c?", "(1, 2)", lambda: regex.compile(r"(?(DEFINE)(?<a>a))(?<=(?&a))c?").match("ac", pos=1).span()),
    ("GroupCall:132 c*", "(1, 2)", lambda: regex.compile(r"(?(DEFINE)(?<a>a))(?<=(?&a))c*").match("ac", pos=1).span()),
    ("GroupCall:133 c+", "None", lambda: regex.compile(r"(?(DEFINE)(?<a>a))(?<=(?&a))c+").match("ac", pos=1)),
    ("GroupCall:133 [c]", "None", lambda: regex.compile(r"(?(DEFINE)(?<a>a))(?<=(?&a))[c]").match("ac", pos=1)),
    ("GroupCall:133 (?:c)", "None", lambda: regex.compile(r"(?(DEFINE)(?<a>a))(?<=(?&a))(?:c)").match("ac", pos=1)),
    ("FuzzyCost:24 abc", "'abc'", lambda: regex.search("(?:abc){i<=99999999999}", "abc").group()),
    ("FuzzyCost:24 axbxc", "'axbxc' (0,2,0)", lambda: (lambda m: (m.group(), m.fuzzy_counts))(regex.search("(?:abc){i<=99999999999}", "axbxc"))),
    ("FuzzyCost:25 axbxc", "'axbxc' (0,2,0)", lambda: (lambda m: (m.group(), m.fuzzy_counts))(regex.search("(?:abc){i<=4294967295}", "axbxc"))),
    ("FuzzyCost:26 axbxc", "None", lambda: regex.search("(?:abc){99999999999i<=1}", "axbxc")),
    ("FuzzyCost:27 axbxc", "None", lambda: regex.search("(?:abc){4294967295i<=1}", "axbxc")),
]

for label, claim, call in CASES:
    try:
        got = call()
    except Exception as exc:  # noqa: BLE001 - upstream raising IS the answer
        got = f"{type(exc).__name__}: {exc}"
    print(f"{label:26} claims {claim:32} upstream: {got!r}")
