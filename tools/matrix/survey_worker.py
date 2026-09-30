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
import os
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
        lists = (row.get("namedLists") or {}) if name == "regex" else {}
        pattern = mod.compile(row["pattern"], flags, **lists)
        kw = {"timeout": 2.0} if name == "regex" else {}
        if name == "regex" and row.get("partial"):
            kw["partial"] = True
        # pos and endpos are the row's slice (survey.py passes them to regex and re only).
        args = [row["subject"], row.get("pos") or 0]
        if row.get("endpos") is not None:
            args.append(row["endpos"])
        op = row["op"]
        if op == "finditer":
            out = []
            for m in pattern.finditer(*args, **kw):
                out.append({"span": _span(m), "partial": bool(getattr(m, "partial", False))})
                if len(out) > 50:
                    break
            return {"status": "matches", "matches": out, "unit": "cp"}
        m = getattr(pattern, op)(*args, **kw)
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
        copts = UTF | UCP | int(os.environ.get("SURVEY_PCRE2_COPTS", "0"), 0)
        for letter, bit in (("i", CASELESS), ("m", MULTILINE), ("s", DOTALL)):
            if letter in row["flags"]:
                copts |= bit
        err, off = ctypes.c_int(), ctypes.c_size_t()
        code = lib.pcre2_compile_8(pat, len(pat), copts, ctypes.byref(err), ctypes.byref(off), None)
        if not code:
            return {"status": "error", "error": "compile: " + message(err.value)}
        md = lib.pcre2_match_data_create_from_pattern_8(code, None)
        # A slice: the subject ends at endpos, and the match starts at pos (a start offset, so a
        # lookbehind still sees the text before it and ^ does not match there, as in upstream).
        text = row["subject"]
        if row.get("endpos") is not None:
            text = text[:row["endpos"]]
        subj = text.encode()
        first = len(text[:row.get("pos") or 0].encode())
        op = row["op"]
        try:
            def run(start, mopts):
                rc = lib.pcre2_match_8(code, subj, len(subj), start, mopts, md, ctx)
                ov = lib.pcre2_get_ovector_pointer_8(md)
                return rc, ov

            ngroups = row["ngroups"]
            if op == "finditer":
                out, start, mopts = [], first, 0
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
            rc, ov = run(first, mopts)
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


# ---------------------------------------------------------------- the two KEY judges
def _load(name, path):
    import importlib.util
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _fuzzyref_engine():
    """tools/probes/fuzzy-reference-matcher.py: a complete, ordered first-match search."""
    from pathlib import Path
    ref = _load("fuzzy_reference_matcher", Path(__file__).resolve().parents[1] / "probes" / "fuzzy-reference-matcher.py")
    # The ruled rule for an iteration that matches empty text only by deleting (ledger 44, F17/F18,
    # DIVERGENCES "A fuzzy repeat takes an iteration that matches no text by deleting only when
    # something needs it"). The matcher's default, "perl", is the literal reading the ruling rejected.
    ref.EMPTY_DELETION_ITERATIONS = "needed"

    def fuzzy_in_lookaround(p):
        # The matcher runs a lookaround body with no fuzzy state (look_groups: counts=None), so a
        # fuzzy section INSIDE a lookaround is outside what it models.
        stack, k = [], 0
        while k < len(p):
            c = p[k]
            if c == "\\":
                k += 2
                continue
            if c == "(":
                stack.append(p.startswith(("(?=", "(?!", "(?<=", "(?<!"), k))
            elif c == ")" and stack:
                stack.pop()
            elif c == "{" and any(stack) and "<" in p[k:p.find("}", k)]:
                return True
            k += 1
        return False

    def answer(row):
        if fuzzy_in_lookaround(row["pattern"]):
            return {"status": "n/a", "reason": "a fuzzy section inside a lookaround, which the matcher does not model"}
        try:
            ref.compile_pattern(row["pattern"])
        except Exception as e:  # noqa: BLE001 - outside the matcher's subset
            return {"status": "n/a", "reason": f"outside the reference matcher's subset: {e}"[:200]}
        m = getattr(ref, row["op"])(row["pattern"], row["subject"])
        if m is None:
            return {"status": "none", "unit": "cp"}
        groups = [None if g[0] < 0 else list(g) for g in m.groups]
        return {"status": "match", "span": list(m.span), "groups": groups,
                "fuzzy_counts": list(m.fuzzy_counts), "unit": "cp"}

    return answer


def _brute_engine():
    """The D11 brute-force partial judge (maint/d11-partial-boundary:tools/probes/d11-brute-judge.py),
    copied beside this file: could a longer text make this match? Continuations up to 4 characters."""
    from pathlib import Path
    judge = _load("d11_brute_judge", Path(__file__).resolve().parent / "d11_brute_judge.py")
    judge.ALPHABET = judge.ALPHABET + "c"

    def answer(row):
        v, w = judge.judge(row["op"], row["pattern"], row["subject"], max_len=4)
        if v is None:
            return {"status": "none", "unit": "cp"}
        kind = "partial" if v[0] == "P" else "match"
        res = {"status": kind, "span": [v[1], v[2]], "unit": "cp", "witness": w}
        if kind == "match":
            res["groups"] = None  # the judge reports spans only; survey.py compares spans for it
        return res

    return answer


def main(argv):
    engine, path, start = argv[0], argv[1], int(argv[2])
    answer = {"pcre2": _pcre2_engine, "fuzzyref": _fuzzyref_engine, "brute": _brute_engine}.get(
        engine, lambda: _python_engine(engine))()
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
