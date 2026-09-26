"""(*SKIP) followed by (*PRUNE): which verb decides the next start, measured in Perl and PCRE2.

Run: python tools/probes/skip-then-prune-perl-pcre2.py   (needs perl on PATH and Git for
Windows' libpcre2-8-0.dll, as tools/probes/pcre2-partial-and-skip.py does).

perlre (v5.42) and pcre2pattern give a verb its effect when backtracking passes it on failure. So
when a path runs (*SKIP) and then (*PRUNE), and fails, backtracking reaches the (*PRUNE) first and
the next attempt starts one character later; the (*SKIP) never acts. Upstream `regex` (and this
port, which follows it) moves the slice start the moment (*SKIP) runs (_regex.c RE_OP_SKIP, :14544;
Matcher.cs's Opcode.Skip arm), so the (*PRUNE) cannot undo it. Part 2 prints rows where the two
readings differ; part 1 prints the two fuzzy rows of ledger entry 44's review, spelt without
fuzzy matching so that Perl and PCRE2 can run them: a section `(?:(*SKIP)a*(*PRUNE)){1<=s<=1,d<=2}`
is `(*SKIP)` then a loop of `a` or, on a mismatch, the section's one substitution (a named group
marks it used), then `(*PRUNE)`, then the minimum; a lazy `(?:S)*?b` is `b|S(?:b|S(?:...))`, each
level with its own group. Deletions are left out because an empty deleting iteration of the inner
`a*` is never admitted (ledger 44), and insertions because naming s and d forbids them. Perl's
rows are run with a code block in front of every verb as well, which its optimiser cannot move a
verb across; both answers are printed.
Recorded 2026-09-26: Perl 5.42.3, PCRE2 10.47, regex 2026.9.10.
"""
import ctypes, os, re, subprocess

lib = ctypes.CDLL(r"C:\Program Files\Git\mingw64\bin\libpcre2-8-0.dll")
lib.pcre2_compile_8.restype = ctypes.c_void_p
lib.pcre2_compile_8.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_uint32, ctypes.POINTER(ctypes.c_int),
                                ctypes.POINTER(ctypes.c_size_t), ctypes.c_void_p]
lib.pcre2_match_data_create_from_pattern_8.restype = ctypes.c_void_p
lib.pcre2_match_data_create_from_pattern_8.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
lib.pcre2_match_8.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_size_t, ctypes.c_size_t, ctypes.c_uint32,
                              ctypes.c_void_p, ctypes.c_void_p]
lib.pcre2_get_ovector_pointer_8.restype = ctypes.POINTER(ctypes.c_size_t)
lib.pcre2_get_ovector_pointer_8.argtypes = [ctypes.c_void_p]
NO_START_OPTIMIZE = 0x10000


def pcre2(pattern, subject):
    err, off = ctypes.c_int(), ctypes.c_size_t()
    code = lib.pcre2_compile_8(pattern.encode(), len(pattern.encode()), NO_START_OPTIMIZE, ctypes.byref(err),
                               ctypes.byref(off), None)
    md = lib.pcre2_match_data_create_from_pattern_8(code, None)
    if lib.pcre2_match_8(code, subject.encode(), len(subject.encode()), 0, 0, md, None) < 0:
        return "none"
    ov = lib.pcre2_get_ovector_pointer_8(md)
    return f"({ov[0]}, {ov[1]})"


def perl(pattern, subject, instrumented=False):
    if instrumented:
        pattern = re.sub(r"\(\*(SKIP|PRUNE)\)", r"(?{1})(*\1)", pattern)
    # Through the environment, not argv: Cygwin's perl expands braces and backslashes in arguments.
    code = 'use re "eval"; my ($p, $s) = @ENV{"PAT", "SUBJ"}; print(($s =~ /$p/) ? "($-[0], $+[0])" : "none")'
    env = dict(os.environ, PAT=pattern, SUBJ=subject)
    return subprocess.run(["perl", "-e", code], capture_output=True, text=True, env=env).stdout


def section(k, prune):
    return (rf"(*SKIP)(?:a|(?!a)(?(<g{k}>)(*F))(?<g{k}>[\s\S]))*" + ("(*PRUNE)" if prune else "")
            + rf"(?(<g{k}>)|(*F))")


def lazy_loop(prune, depth=5):
    inner = "(*F)"
    for k in range(depth, 0, -1):
        inner = f"(?:b|{section(k, prune)}{inner})"
    return inner


print("# part 1: the review's rows, subject 'aab'")
for fuzzy, prune in (("(?:(?:(*SKIP)a*(*PRUNE)){1<=s<=1,d<=2})*?b", True), ("(?:(?:(*SKIP)a*){1<=s<=1,d<=2})*?b", False)):
    p = lazy_loop(prune)
    print(f"{fuzzy:48} PCRE2 {pcre2(p, 'aab'):8} Perl {perl(p, 'aab', True):8} (plain: {perl(p, 'aab')})")

print("# part 2: (*SKIP) then (*PRUNE), exact matching")
import regex
for p, t in [(r"aa(*SKIP)x(*PRUNE)y|a", "aaxz"), (r"aa(*SKIP)b(*PRUNE)(*F)|a", "aab"),
             (r"(?:aa(*SKIP)b(*PRUNE)(*F)b|a)?a", "aaaab"), (r"aa(*SKIP)b?b(*PRUNE)(?:a|b)(*SKIP)|a", "aabx")]:
    m = regex.search(p, t)
    print(f"{p:40} {t:6} PCRE2 {pcre2(p, t):8} Perl {perl(p, t, True):8} regex {m.span() if m else None}")
