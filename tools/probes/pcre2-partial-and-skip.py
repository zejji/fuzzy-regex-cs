"""Drives PCRE2 directly - the libpcre2-8-0.dll that Git for Windows ships - for the cases where
this port and upstream `regex` disagree, so a second engine's *actual* answer is on record rather
than one derived from its documentation. Run: python tools/probes/pcre2-partial-and-skip.py

Recorded 2026-09-12 against PCRE2 10.47; the results are in docs/plan/2026-09-12-divergence-research.md.
Derivation from pcre2partial's prose predicted PARTIAL for 'ba??x' on 'baa' and for an empty
subject; the binary answers None to both. That is why this file exists.
"""
import ctypes, ctypes.util
import os, shutil
def _find_pcre2():
    for c in [os.environ.get("PCRE2_DLL"), r"C:Program FilesGitmingw64binlibpcre2-8-0.dll", shutil.which("libpcre2-8-0.dll"), "libpcre2-8.so.0", "libpcre2-8.so"]:
        if c and (os.path.exists(c) or not os.sep in c):
            try: ctypes.CDLL(c); return c
            except OSError: pass
    raise SystemExit("no PCRE2 library found; set PCRE2_DLL")
lib = ctypes.CDLL(r"C:\Program Files\Git\mingw64\bin\libpcre2-8-0.dll")
PARTIAL_SOFT, PARTIAL_HARD, NO_START_OPTIMIZE, MULTILINE = 0x10, 0x20, 0x10000, 0x400
NOMATCH, PARTIAL = -1, -2
lib.pcre2_compile_8.restype = ctypes.c_void_p
lib.pcre2_compile_8.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_uint32, ctypes.POINTER(ctypes.c_int), ctypes.POINTER(ctypes.c_size_t), ctypes.c_void_p]
lib.pcre2_match_data_create_from_pattern_8.restype = ctypes.c_void_p
lib.pcre2_match_data_create_from_pattern_8.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
lib.pcre2_match_8.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_size_t, ctypes.c_size_t, ctypes.c_uint32, ctypes.c_void_p, ctypes.c_void_p]
lib.pcre2_get_ovector_pointer_8.restype = ctypes.POINTER(ctypes.c_size_t)
lib.pcre2_get_ovector_pointer_8.argtypes = [ctypes.c_void_p]
buf = ctypes.create_string_buffer(256); lib.pcre2_config_8(11, buf); print("PCRE2", buf.value.decode())  # PCRE2_CONFIG_VERSION = 11
def run(pat, subj, copts=0, mopts=0, start=0):
    err = ctypes.c_int(); off = ctypes.c_size_t()
    code = lib.pcre2_compile_8(pat.encode(), len(pat.encode()), copts, ctypes.byref(err), ctypes.byref(off), None)
    assert code, f"compile failed {pat!r} err={err.value}"
    md = lib.pcre2_match_data_create_from_pattern_8(code, None)
    rc = lib.pcre2_match_8(code, subj.encode(), len(subj.encode()), start, mopts, md, None)
    ov = lib.pcre2_get_ovector_pointer_8(md)
    if rc == NOMATCH: return "None"
    if rc == PARTIAL: return f"PARTIAL ({ov[0]},{ov[1]})"
    if rc < 0: return f"error {rc}"
    return f"match ({ov[0]},{ov[1]})"
print("\n# Partial matching: SOFT / HARD")
for pat, subj in [("ba??x","baa"),("ba??x","bab"),("ba??x","ba"),("ba?x","baa"),(".{0,2}?x","baa"),("ba*?x","baa"),("a(bc)*",""),(r"\b$",""),(r"$","")]:
    print(f"{pat!r:10} {subj!r:6} SOFT={run(pat,subj,0,PARTIAL_SOFT):16} HARD={run(pat,subj,0,PARTIAL_HARD)}")
print("\n# (*SKIP) case 1, with and without start optimisation")
for pat, subj, start in [(r"(?:..(*SKIP)x|q)x","ab cd xx",0),(r"(?:..(*SKIP)x|q)x","ab cd xx",4),(r"(?:..(*SKIP)x|q)x","abcdxxx",0),(r"(?:aa(*SKIP)x|M)x","aaaaxx",0),(r"..(*SKIP)xx","cd xxx",0)]:
    print(f"{pat!r:22} {subj!r:11} start={start} optimised={run(pat,subj,0,0,start):14} no_start_optimize={run(pat,subj,NO_START_OPTIMIZE,0,start)}")
ANCHORED = 0x80000000
print("\n# Partial, ANCHORED (= match at start), SOFT / HARD")
for pat, subj in [("ba??x","baa"),("ba??x","bab"),("ba??x","ba"),(".{0,2}?x","baa"),("ba*?x","baa"),("a(bc)*",""),(r"\b$",""),(r"\b","")]:
    print(f"{pat!r:10} {subj!r:6} SOFT={run(pat,subj,ANCHORED,PARTIAL_SOFT):16} HARD={run(pat,subj,ANCHORED,PARTIAL_HARD)}")
