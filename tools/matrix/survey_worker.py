"""One engine's half of tools/matrix/survey.py, for the three engines that run inside Python.

    python tools/matrix/survey_worker.py <regex|re|pcre2> <rows.jsonl> <start-index>

Reads the engine's translated rows from a FILE (never stdin), answers rows from <start-index> on,
and prints one JSON line per row, flushed, after a first line READY. survey.py watches the output
and kills the process when a row takes longer than its per-row limit or the process tree grows past
its memory cap, then restarts it after that row. So this file needs no timeout of its own, but the
engines that have one use it too (regex's timeout=, PCRE2's match, depth and heap limits).

Spans are reported in the unit named by "unit": codepoints for regex and re, UTF-8 bytes for PCRE2
(survey.py converts). PCRE2 is driven through ctypes on Git for Windows' libpcre2-8-0.dll, which is
PCRE2 10.47 (2025-10-21), the same release pip `pcre2` 0.7.1 wraps: the pip binding has no partial
matching and no ANCHORED/ENDANCHORED options, so it cannot ask this survey's questions.
"""

import ctypes
import json
import sys


def _span(m, g=0):
    s = m.span(g)
    return None if s[0] < 0 else list(s)


def _groups(m, n):
    return [_span(m, g) for g in range(1, n + 1)]


# ---------------------------------------------------------------- python regex and re
def _python_engine(name):
    if name == "regex":
        import regex as mod
    else:
        import re as mod

    def answer(row):
        flags = row["flags"]
        pattern = mod.compile(row["pattern"], flags)
        kw = {"timeout": 2.0} if name == "regex" else {}
        if name == "regex" and row.get("partial"):
            kw["partial"] = True
        op = row["op"]
        if op == "finditer":
            out = []
            for m in pattern.finditer(row["subject"], **kw):
                out.append({"span": _span(m), "partial": bool(getattr(m, "partial", False))})
                if len(out) > 50:
                    break
            return {"status": "matches", "matches": out, "unit": "cp"}
        m = getattr(pattern, op)(row["subject"], **kw)
        if m is None:
            return {"status": "none", "unit": "cp"}
        res = {"status": "partial" if getattr(m, "partial", False) else "match", "span": _span(m),
               "groups": _groups(m, pattern.groups), "unit": "cp"}
        if name == "regex":
            res["captures"] = [[list(s) for s in m.spans(g)] for g in range(1, pattern.groups + 1)]
            res["fuzzy_counts"] = list(m.fuzzy_counts)
        return res

    return answer


# ---------------------------------------------------------------- PCRE2 via ctypes
_DLL = r"C:\Program Files\Git\mingw64\bin\libpcre2-8-0.dll"
UTF, UCP, CASELESS, MULTILINE, DOTALL = 0x80000, 0x20000, 0x8, 0x400, 0x20
ANCHORED, ENDANCHORED = 0x80000000, 0x20000000
NOTEMPTY_ATSTART, PARTIAL_SOFT = 0x8, 0x10
NOMATCH, PARTIAL = -1, -2


def _pcre2_engine():
    lib = ctypes.CDLL(_DLL)
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
    lib.pcre2_get_ovector_count_8.restype = ctypes.c_uint32
    lib.pcre2_get_ovector_count_8.argtypes = [ctypes.c_void_p]
    lib.pcre2_match_context_create_8.restype = ctypes.c_void_p
    lib.pcre2_match_context_create_8.argtypes = [ctypes.c_void_p]
    for f in ("pcre2_set_match_limit_8", "pcre2_set_depth_limit_8", "pcre2_set_heap_limit_8"):
        getattr(lib, f).argtypes = [ctypes.c_void_p, ctypes.c_uint32]
    lib.pcre2_get_error_message_8.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_size_t]
    lib.pcre2_code_free_8.argtypes = [ctypes.c_void_p]
    lib.pcre2_match_data_free_8.argtypes = [ctypes.c_void_p]
    ctx = lib.pcre2_match_context_create_8(None)
    lib.pcre2_set_match_limit_8(ctx, 5_000_000)
    lib.pcre2_set_depth_limit_8(ctx, 100_000)
    lib.pcre2_set_heap_limit_8(ctx, 200_000)  # KiB: 200 MB

    def message(rc):
        buf = ctypes.create_string_buffer(256)
        lib.pcre2_get_error_message_8(rc, buf, 256)
        return buf.value.decode()

    def answer(row):
        pat = row["pattern"].encode()
        copts = UTF | UCP
        for letter, bit in (("i", CASELESS), ("m", MULTILINE), ("s", DOTALL)):
            if letter in row["flags"]:
                copts |= bit
        err, off = ctypes.c_int(), ctypes.c_size_t()
        code = lib.pcre2_compile_8(pat, len(pat), copts, ctypes.byref(err), ctypes.byref(off), None)
        if not code:
            return {"status": "error", "error": "compile: " + message(err.value)}
        md = lib.pcre2_match_data_create_from_pattern_8(code, None)
        subj = row["subject"].encode()
        op = row["op"]
        try:
            def run(start, mopts):
                rc = lib.pcre2_match_8(code, subj, len(subj), start, mopts, md, ctx)
                ov = lib.pcre2_get_ovector_pointer_8(md)
                return rc, ov

            ngroups = row["ngroups"]
            if op == "finditer":
                out, start, mopts = [], 0, 0
                while start <= len(subj) and len(out) <= 50:
                    rc, ov = run(start, mopts)
                    if rc == NOMATCH:
                        if mopts == 0:
                            break
                        start += 1  # ASCII-or-UTF-8: step one byte, then skip continuation bytes
                        while start < len(subj) and (subj[start] & 0xC0) == 0x80:
                            start += 1
                        mopts = 0
                        continue
                    if rc < 0:
                        return {"status": "error", "error": message(rc)}
                    out.append({"span": [ov[0], ov[1]], "partial": False})
                    start = ov[1]
                    mopts = NOTEMPTY_ATSTART | ANCHORED if ov[0] == ov[1] else 0
                return {"status": "matches", "matches": out, "unit": "utf8"}
            mopts = {"search": 0, "match": ANCHORED, "fullmatch": ANCHORED | ENDANCHORED}[op]
            if row.get("partial"):
                mopts |= PARTIAL_SOFT
            rc, ov = run(0, mopts)
            if rc == NOMATCH:
                return {"status": "none", "unit": "utf8"}
            if rc == PARTIAL:
                return {"status": "partial", "span": [ov[0], ov[1]], "groups": [None] * ngroups,
                        "unit": "utf8"}
            if rc < 0:
                return {"status": "error", "error": message(rc)}
            unset = ctypes.c_size_t(-1).value
            groups = []
            for g in range(1, ngroups + 1):
                if g < rc and ov[2 * g] != unset:
                    groups.append([ov[2 * g], ov[2 * g + 1]])
                else:
                    groups.append(None)
            return {"status": "match", "span": [ov[0], ov[1]], "groups": groups, "unit": "utf8"}
        finally:
            lib.pcre2_match_data_free_8(md)
            lib.pcre2_code_free_8(code)

    return answer


def main(argv):
    engine, path, start = argv[0], argv[1], int(argv[2])
    answer = _pcre2_engine() if engine == "pcre2" else _python_engine(engine)
    with open(path, encoding="utf-8") as f:
        rows = [json.loads(line) for line in f if line.strip()]
    sys.stdout.reconfigure(encoding="utf-8")
    print("READY", flush=True)
    for row in rows[start:]:
        try:
            res = answer(row)
        except Exception as e:  # noqa: BLE001 - an engine refusing a row is an answer
            kind = type(e).__name__
            res = {"status": "timeout" if kind == "TimeoutError" else "error",
                   "error": f"{kind}: {e}"[:300]}
        res["i"] = row["i"]
        print(json.dumps(res), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
