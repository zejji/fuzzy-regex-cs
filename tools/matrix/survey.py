r"""The engine survey for the 2026-09-30 complete matrix: run rows through every engine that can
express them, and record where the engines agree.

    python tools/matrix/survey.py <rows.jsonl> --out <dir> [--engines regex,pcre2,perl,...]
    python tools/matrix/survey.py --self-test

A row is a JSON object: id, cell (optional), pattern, flags (upstream's int, default 0), subject,
operation (search, match, fullmatch or finditer) and partial (default false). Upstream's `regex`
reads the row as written. Every other engine gets a TRANSLATION into its own dialect, or "n/a" with
the reason: a construct it does not have (a fuzzy constraint, a group call in JavaScript) is not a
disagreement, it is a question that engine cannot be asked. A pattern an engine refuses to compile
is recorded as "error" with the message, and counted apart from the answers.

Engines, all measured, none recalled:
  regex   python `regex` 2026.9.10 (upstream; one data point, not the judge)
  pcre2   PCRE2 10.47 through ctypes (survey_worker.py says why not the pip binding)
  perl    Perl 5.42.3 (cygwin)
  node    node 24 (V8 Irregexp)
  dotnet  .NET 10 System.Text.RegularExpressions (backtracking; RightToLeft for reverse rows)
  re      python `re` (CPython 3.14)
  boost   Boost.Regex a640597 (standalone headers, Perl syntax) in WSL, ASCII rows without a slice
          or partial matching (survey_worker_boost.cpp)
  tre     TRE 0.8.0 in WSL, fuzzy rows with one whole-pattern budget, search only
          (tools/probes/tre-fuzzy-check.py's gate; TRE is leftmost-longest, so only "is there a
          match" and its cheapest cost are compared, never the span)

Caps: each engine runs as ONE worker process per batch that answers rows in order, one flushed line
each. A watchdog here kills the worker when a row takes longer than --row-timeout seconds or the
worker's process tree passes --max-rss-mb, records that row as "timeout" or "memory", and restarts
the worker after it. So every row of every engine has a time and a memory cap, including node and
re, which have no regex timeout of their own. TRE runs as one WSL batch under an overall timeout.

Spans are normalised to codepoints. Output in --out: results.jsonl (one line per row per engine),
rows.jsonl (each row with its constructs and verdict), summary.json and summary.md (per cell: rows,
answering engines, agree, disagree, and the disagreeing rows).

Copied from matrix/answer-key 3e255f4e for the harness's check C7 (tools/matrix/c7.py), with these
changes, each found on the harness's 12,457 rows (2026-09-30):
  - the leading-flags reader took a call as flags: `(?1)x` gave "inline flag 1", so 155 rows that
    start with a numbered call were out of every engine's dialect; it now reads flag letters only;
  - "a scoped flag after the start" matched `(?R)`, so 249 rows with a whole-pattern call after the
    start were refused; it now matches flag letters only;
  - rows may carry pos and endpos (upstream's slice). PCRE2 gets the subject cut at endpos and a
    start offset, Perl the cut subject with pos() set, re its own pos/endpos; node and .NET say n/a
    (.NET's beginning/length hides the text before the start from lookbehind, which upstream's pos
    does not);
  - Perl's "match" was a search whose answer must start at 0, which is wrong when `\K` moves the
    reported start: `a\Kb` match 'ab' answered None. It is now `\G(?:...)` searched from pos, and
    "fullmatch" `\G(?:...)\z` (both were `\A`, which cannot anchor at a pos after 0). A pattern that
    calls itself whole keeps the old reading, and is n/a if it also has `\K`;
  - constructs.json is read from this directory, else from the matrix/answer-key branch.
"""

import argparse
import collections
import hashlib
import importlib.util
import json
import os
import queue
import re
import subprocess
import sys
import threading
import time
from pathlib import Path

import psutil
import regex

HERE = Path(__file__).resolve().parent
TOOLS = HERE.parent


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


_matrix = None


def tags_of(row):
    """The construct ids of tools/matrix/constructs.json that a row uses."""
    global _matrix
    if _matrix is None:
        _matrix = _load("interaction_matrix", TOOLS / "interaction-matrix.py")
    tags = _matrix.constructs(row)
    if re.search(r"(?<!\\)(?:\\\\)*\\K", row["pattern"]):
        tags.add("keep")
    if row["operation"] == "finditer":
        tags.discard("op-finditer")
        tags.add("op-finditer")
    if "(?(DEFINE)" in row["pattern"]:
        tags.add("call")
    return tags


def _constructs_text():
    local = HERE / "constructs.json"
    if local.exists():
        return local.read_text(encoding="utf-8")
    return subprocess.run(["git", "show", "matrix/answer-key:tools/matrix/constructs.json"], cwd=HERE,
                          capture_output=True, text=True, encoding="utf-8", check=True,
                          stdin=subprocess.DEVNULL).stdout


_CONSTRUCTS = json.loads(_constructs_text())
FAMILY = {c["id"]: c["family"] for c in _CONSTRUCTS["constructs"]}
RISKY = _CONSTRUCTS["risky_families"]


def families_of(tags):
    return {FAMILY[t] for t in tags if t in FAMILY}


# ---------------------------------------------------------------- analysis of a row
_FLAG_BITS = {2: "i", 8: "m", 16: "s"}
_REGEX_ONLY_BITS = {4096: "BESTMATCH", 32768: "ENHANCEMATCH", 16384: "FULLCASE", 256: "VERSION1",
                    2048: "WORD", 65536: "POSIX", 128: "ASCII", 64: "VERBOSE"}
_HARMLESS_BITS = {32: "UNICODE", 8192: "VERSION0"}
REVERSE_BIT = 1024
# Flag letters only: `(?1)` and `(?R)` are calls, not flags.
_FLAG_GROUP = r"(?:[abefiLmprsuwx]|V[01])+"
_LEADING = re.compile(r"^\(\?(" + _FLAG_GROUP + r")\)")


def analyse(row):
    """Everything a translation needs: flags as letters, reverse, group names, and regex-only reasons."""
    pattern, bits = row["pattern"], int(row.get("flags") or 0)
    info = {"letters": "", "reverse": bool(bits & REVERSE_BIT), "regex_only": []}
    for bit, letter in _FLAG_BITS.items():
        if bits & bit:
            info["letters"] += letter
    for bit, name in _REGEX_ONLY_BITS.items():
        if bits & bit:
            info["regex_only"].append(f"flag {name}")
    while True:
        m = _LEADING.match(pattern)
        if not m:
            break
        body = m.group(1)
        if body.startswith("V0"):
            body = body[2:]
        elif body.startswith("V1"):
            info["regex_only"].append("inline V1")
            body = body[2:]
        for c in body:
            if c in "ims":
                info["letters"] += c
            elif c == "r":
                info["reverse"] = True
            elif c == "u":
                pass
            else:
                info["regex_only"].append(f"inline flag {c}")
        pattern = pattern[m.end():]
    info["body"] = pattern
    compiled = regex.compile(row["pattern"], bits, **(row.get("namedLists") or {}))
    names = {v: k for k, v in compiled.groupindex.items()}
    info["ngroups"] = compiled.groups
    info["groupnames"] = [names.get(g) for g in range(1, compiled.groups + 1)]
    return info


# Regex-only syntax, found on the pattern with escapes kept (each is (reason, regex)).
_REGEX_ONLY_SYNTAX = (
    ("a fuzzy constraint", r"\{[^{}]*(?:[eids]\s*<|<\s*[eids]|\d[ids]\s*\+|^[eids]\})"),
    ("a named list", r"\\L<"),
    ("\\m or \\M word boundaries", r"(?<!\\)(?:\\\\)*\\[mM]"),
    ("a scoped regex-only flag", r"\(\?[imsx]*[befprwaLV][a-zA-Z0-9-]*[:)]"),
    ("a scoped flag after the start", r"(?<=.)\(\?" + _FLAG_GROUP + r"\)"),
    ("a grapheme or property escape", r"(?<!\\)(?:\\\\)*\\[XpPN]"),
)
_CALL = re.compile(r"\(\?(?:R|[+-]?\d+|&\w+|P>\w+)\)|\(\?\(DEFINE\)")
_WHOLE_CALL = re.compile(r"\(\?(?:R|0)\)")
_VERB = re.compile(r"\(\*[A-Z]")
_COND = re.compile(r"\(\?\(")
_COND_LOOK = re.compile(r"\(\?\(\?<?[=!]")
_BRANCH_RESET = re.compile(r"\(\?\|")
_ATOMIC = re.compile(r"\(\?>")
_POSSESSIVE = re.compile(r"(?<!\\)(?:[*+?]|\})\+")
_LOOKBEHIND = re.compile(r"\(\?<[=!]")
_KEEP = re.compile(r"(?<!\\)(?:\\\\)*\\K")
_SEARCH_ANCHOR = re.compile(r"(?<!\\)(?:\\\\)*\\G")
_STRING_ANCHOR = re.compile(r"(?<!\\)(?:\\\\)*\\[AZ]")


def _backrefs(p, style):
    r"""Rewrite regex's backreference spellings (\g<n>, \g<name>, (?P=name)) for another engine."""
    def g(m):
        ref = m.group(1)
        if style in ("pcre", "perl"):
            return f"\\g{{{ref}}}" if ref.isdigit() else f"\\k<{ref}>"
        if style == "re":
            return f"(?:\\{ref})" if ref.isdigit() else f"(?P={ref})"
        return f"(?:\\{ref})" if ref.isdigit() else f"\\k<{ref}>"
    p = re.sub(r"\\g<(\w+)>", g, p)
    if style == "re":
        p = re.sub(r"\(\?<(?![=!])(\w+)>", r"(?P<\1>", p)
    if style in ("dotnet", "node"):
        p = re.sub(r"\(\?P=(\w+)\)", lambda m: f"\\k<{m.group(1)}>", p)
        p = p.replace("(?P<", "(?<")
    return p


def _boost_dollar(p):
    r"""Every `$` outside a set as Perl's single-line `$`, (?=\n?\z). Boost compiles `$` under
    (?-m) to the end of the buffer only (basic_regex_parser.hpp:357, no_mod_m), where Perl, PCRE2
    and upstream also match before a final newline: 'a$' over 'a\n' is (0, 1) in all three and no
    match in Boost (measured 2026-09-30). A POSIX class inside a set, `[:alpha:]`, does not end
    the set, and a comment, `(?#...)`, is copied through unchanged."""
    out, k, in_set = [], 0, False
    while k < len(p):
        c = p[k]
        if c == "\\":
            out.append(p[k:k + 2])
            k += 2
            continue
        if in_set and p.startswith("[:", k) and p.find(":]", k + 2) != -1:
            end = p.find(":]", k + 2) + 2
            out.append(p[k:end])
            k = end
            continue
        if not in_set and p.startswith("(?#", k):
            end = p.find(")", k)
            end = len(p) if end == -1 else end + 1
            out.append(p[k:end])
            k = end
            continue
        if in_set:
            if c == "]":
                in_set = False
        elif c == "[":
            in_set = True
            out.append(c)
            k += 1
            if k < len(p) and p[k] == "^":
                out.append("^")
                k += 1
            if k < len(p) and p[k] == "]":
                out.append("]")  # a leading ] is a literal
                k += 1
            continue
        elif c == "$":
            out.append(r"(?=\n?\z)")
            k += 1
            continue
        out.append(c)
        k += 1
    return "".join(out)


def _upstream_escapes(p, engine):
    r"""Upstream's \v, \uXXXX and \UXXXXXXXX in the engine's spelling, or None when the engine cannot
    say them. Upstream's \v is U+000B alone, where Perl, PCRE2 and Boost read it as any vertical
    whitespace; Perl reads \u as "uppercase the next character", Boost as an uppercase class, and
    PCRE2 refuses it (measured through this survey, 2026-09-30). \x{...} means the codepoint in
    Perl and PCRE2; Boost has no ICU here, so its \u and \U rows are refused."""
    out, k = [], 0
    while k < len(p):
        c = p[k]
        if c != "\\":
            out.append(c)
            k += 1
            continue
        e = p[k + 1:k + 2]
        if e == "v":
            out.append("\\x{0B}")
            k += 2
        elif e in ("u", "U"):
            if engine == "boost":
                return None
            width = 4 if e == "u" else 8
            out.append("\\x{" + p[k + 2:k + 2 + width] + "}")
            k += 2 + width
        else:
            out.append(p[k:k + 2])
            k += 2
    return "".join(out)


def translate(row, info, engine):
    """The engine's own row, or a string saying why the row is out of its dialect."""
    p = info["body"]
    op = row["operation"]
    partial = bool(row.get("partial"))
    pos, endpos = row.get("pos"), row.get("endpos")
    sliced = pos is not None or endpos is not None
    if engine == "regex":
        return {"pattern": row["pattern"], "flags": int(row.get("flags") or 0), "subject": row["subject"],
                "op": op, "partial": partial, "pos": pos, "endpos": endpos,
                "namedLists": row.get("namedLists") or {}}
    if sliced and engine not in ("pcre2", "perl", "re"):
        return "a pos/endpos slice"
    if sliced and (endpos if endpos is not None else len(row["subject"])) < (pos or 0):
        return "endpos before pos"
    if engine == "tre":
        return "tre is asked through its own gate"
    if engine == "fuzzyref":
        if not re.search(_REGEX_ONLY_SYNTAX[0][1], p):
            return "not a fuzzy row"
        if int(row.get("flags") or 0) or info["body"] != row["pattern"]:
            return "flags, which the reference matcher does not have"
        if partial or op == "finditer":
            return "partial or finditer, which the reference matcher does not have"
        # The matcher has numbered groups only: name -> number, which keeps every group's number.
        number = {n: k + 1 for k, n in enumerate(info["groupnames"]) if n}
        q = re.sub(r"\(\?P?<(?![=!])(\w+)>", "(", row["pattern"])
        q = re.sub(r"\(\?\((\w+)\)", lambda m: f"(?({number.get(m.group(1), m.group(1))})", q)
        return {"pattern": q, "subject": row["subject"], "op": op}
    if engine == "brute":
        if not partial or op == "finditer":
            return "not a partial match/search/fullmatch row"
        bits = int(row.get("flags") or 0)
        if bits & ~REVERSE_BIT:
            return "flags other than REVERSE"
        pattern = ("(?r)" if bits & REVERSE_BIT else "") + row["pattern"]
        return {"pattern": pattern, "subject": row["subject"], "op": op}
    if info["regex_only"]:
        return "; ".join(info["regex_only"])
    for reason, rx in _REGEX_ONLY_SYNTAX:
        if re.search(rx, p):
            return reason
    if partial and engine != "pcre2":
        return "partial matching"
    if partial and op == "finditer":
        return "partial finditer"
    if info["reverse"] and engine != "dotnet":
        return "reverse matching"
    has_call, has_verb = bool(_CALL.search(p)), bool(_VERB.search(p))
    if engine in ("dotnet", "node", "re"):
        if has_call:
            return "a group call"
        if has_verb:
            return "a backtracking verb"
        if _BRANCH_RESET.search(p):
            return "a branch reset"
        if _KEEP.search(p):
            return "\\K"
    if engine in ("node", "re") and _SEARCH_ANCHOR.search(p):
        return "\\G"
    if engine == "node":
        for name, rx in (("a conditional", _COND), ("an atomic group", _ATOMIC),
                         ("a possessive quantifier", _POSSESSIVE), ("\\A or \\Z", _STRING_ANCHOR)):
            if rx.search(p):
                return name
    if engine == "dotnet" and _POSSESSIVE.search(p):
        return "a possessive quantifier"
    if engine == "re" and _COND_LOOK.search(p):
        return "a lookaround condition"
    if engine == "boost" and not (row["subject"] + p).isascii():
        # SHORTCUT: without ICU, Boost's char traits classify by the C locale, so \w, \s, case
        # folding and the dot see a UTF-8 subject byte by byte. Upgrade: build against ICU and use
        # boost::u32regex, which reads codepoints.
        return "non-ASCII text (Boost is built without ICU)"
    if engine in ("pcre2", "perl", "boost"):
        # .NET, node and re read \v, \u and \U as upstream does.
        p = _upstream_escapes(p, engine)
        if p is None:
            return "\\u or \\U (Boost is built without ICU)"
    style = {"pcre2": "pcre", "perl": "perl", "dotnet": "dotnet", "node": "node", "re": "re", "boost": "pcre"}[engine]
    p = _backrefs(p, style)
    if engine in ("pcre2", "perl", "dotnet", "boost"):
        p = re.sub(r"(?<!\\)((?:\\\\)*)\\Z", r"\1\\z", p)
    if engine in ("perl", "boost"):
        p = re.sub(r"\(\?\((?!\?|<|'|\d|R|DEFINE)(\w+)\)", r"(?(<\1>)", p)
    if engine == "boost":
        # Boost spells Python's named group, reference and call in Perl's way.
        p = re.sub(r"\(\?P<(\w+)>", r"(?<\1>", p)
        p = re.sub(r"\(\?P=(\w+)\)", r"\\k<\1>", p)
        p = re.sub(r"\(\?P>(\w+)\)", r"(?&\1)", p)
        # Boost's line separators include \r and \f (perl_matcher_common.hpp is_separator), so ^, $
        # and the dot read those subjects differently from Perl and upstream, which use \n only.
        if any(c in row["subject"] for c in "\r\f"):
            return "a \\r or \\f in the subject (Boost treats them as line separators)"
        if re.search(r"\(\?[a-zA-Z]*m[a-zA-Z]*(?:-[a-zA-Z]*)?:|\(\?[a-zA-Z]*-[a-zA-Z]*m", p):
            return "a scoped m flag (the $ rewrite below cannot see its scope)"
        if "m" not in info["letters"]:
            p = _boost_dollar(p)
    letters = info["letters"]
    out = {"subject": row["subject"], "op": op, "partial": partial, "ngroups": info["ngroups"],
           "groupnames": info["groupnames"]}
    if engine == "re":
        flags = 0
        for c, bit in (("i", re.I), ("m", re.M), ("s", re.S)):
            if c in letters:
                flags |= bit
        out.update(pattern=p, flags=flags)
        return out
    if engine in ("pcre2", "perl", "re"):
        out["pos"], out["endpos"] = pos, endpos
    if engine in ("perl", "dotnet") and op == "fullmatch":
        if _WHOLE_CALL.search(p):
            return "fullmatch of a pattern that calls itself (the \\A...\\z wrapper would be recursed into)"
        # Perl searches from pos() (survey_worker.pl), so \G is where the slice starts; .NET has no slice.
        p = f"\\G(?:{p})\\z" if engine == "perl" else f"\\A(?:{p})\\z"
        out["op"] = "search"
    if engine == "perl" and op == "match":
        if not _WHOLE_CALL.search(p):
            p = f"\\G(?:{p})"
            out["op"] = "search"
        elif _KEEP.search(p):
            return "match of a pattern that calls itself and has \\K (no anchor to wrap it in)"
    if engine == "pcre2" and op == "fullmatch" and partial:
        # PCRE2 10.47 refuses PCRE2_ENDANCHORED together with PCRE2_PARTIAL_SOFT at match time
        # ("bad option value", measured 2026-09-30), so a partial fullmatch is an anchored match
        # of (?:...)\z instead.
        if _WHOLE_CALL.search(p):
            return "partial fullmatch of a pattern that calls itself (the \\z wrapper would be recursed into)"
        p = f"(?:{p})\\z"
        out["op"] = "match"
    if engine == "node" and op == "fullmatch":
        p = f"(?:{p})$"
        out["op"] = "match"
        if "m" in letters:
            return "fullmatch under MULTILINE"
    if engine == "node":
        if "m" in letters:
            letters = letters  # JS m flag has the same meaning for ^ and $
        if any(ord(ch) > 0xFFFF for ch in row["subject"] + p):
            letters += "u"
    if engine == "dotnet" and info["reverse"]:
        letters += "r"
    out.update(pattern=p, flags=letters)
    return out


# ---------------------------------------------------------------- running the workers
def _commands(engine, rows_path, start):
    py = sys.executable
    if engine in ("regex", "re", "pcre2", "fuzzyref", "brute"):
        return [py, str(HERE / "survey_worker.py"), engine, rows_path, str(start)], None
    if engine == "perl":
        return ["perl", str(HERE / "survey_worker.pl"), rows_path, str(start)], {"PERL_SIGNALS": "unsafe"}
    if engine == "node":
        return ["node", str(HERE / "survey_worker.mjs"), rows_path, str(start)], None
    if engine == "dotnet":
        return ["dotnet", "run", "-c", "Release",
                str(HERE / "survey_bcl.cs"), "--", rows_path, str(start)], None
    if engine == "boost":
        # The outer `timeout` is a last guard: killing wsl.exe from here need not end the Linux
        # process, so the worker cannot outlive an hour even if the watchdog loses it.
        return ["wsl", "-d", WSL_DISTRO, "--", "timeout", "3600", _boost_worker(),
                _wsl_path(rows_path), str(start)], None
    raise ValueError(engine)


# Boost.Regex, standalone headers, built in WSL on first use (measured 2026-09-30: the clone is
# 30 MB, the build about 20 s). The binary's name carries a hash of the source, so an edited
# worker is rebuilt and a stale one is never run.
WSL_DISTRO = "Ubuntu"
BOOST_REPO, BOOST_COMMIT, BOOST_DIR = "https://github.com/boostorg/regex.git", "a640597", "/tmp/boost-regex"


def _wsl_path(path):
    p = Path(path).resolve()
    return f"/mnt/{p.drive[0].lower()}{p.as_posix()[2:]}"


def _boost_worker():
    if "boost-binary" in _BUILT:
        return _BUILT["boost-binary"]
    src = HERE / "survey_worker_boost.cpp"
    digest = hashlib.sha1(src.read_bytes()).hexdigest()[:12]
    binary = f"/tmp/boost-survey/worker-{digest}"
    script = (
        f"set -e; test -x {binary} && exit 0; "
        f"test -d {BOOST_DIR} || git clone -q {BOOST_REPO} {BOOST_DIR}; "
        f"git -C {BOOST_DIR} checkout -q {BOOST_COMMIT}; mkdir -p /tmp/boost-survey; "
        f"g++ -O2 -std=c++17 -I{BOOST_DIR}/include -o {binary} '{_wsl_path(src)}'"
    )
    done = subprocess.run(["wsl", "-d", WSL_DISTRO, "--", "bash", "-c", script], stdin=subprocess.DEVNULL,
                          capture_output=True, text=True, timeout=600)
    if done.returncode != 0:
        raise RuntimeError(f"the Boost worker did not build: {done.stderr.strip()[:300]}")
    _BUILT["boost-binary"] = binary
    return binary


_BUILT = {}


def _tree_rss(proc):
    try:
        p = psutil.Process(proc.pid)
        return sum(c.memory_info().rss for c in [p, *p.children(recursive=True)])
    except psutil.Error:
        return 0


def _kill(proc):
    try:
        p = psutil.Process(proc.pid)
        for c in p.children(recursive=True):
            c.kill()
        p.kill()
    except psutil.Error:
        pass


def run_engine(engine, jobs, workdir, row_timeout, max_rss_mb, startup_timeout=180):
    """Answer every job with one engine; returns {i: result}. jobs are translated rows with 'i'."""
    if not jobs:
        return {}
    rows_path = str(workdir / f"in-{engine}.jsonl")
    with open(rows_path, "w", encoding="utf-8", newline="\n") as f:
        for j in jobs:
            f.write(json.dumps(j, ensure_ascii=False) + "\n")
    results, start = {}, 0
    while start < len(jobs):
        cmd, extra = _commands(engine, rows_path, start)
        env = dict(os.environ, **(extra or {}))
        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                stdin=subprocess.DEVNULL, env=env)
        lines = queue.Queue()

        def pump():
            for raw in proc.stdout:
                lines.put(raw.decode("utf-8", "replace").strip())
            lines.put(None)

        threading.Thread(target=pump, daemon=True).start()
        ready, deadline, fault = False, time.monotonic() + startup_timeout, None
        while True:
            try:
                line = lines.get(timeout=0.25)
            except queue.Empty:
                line = ""
            if line is None:
                break
            if line == "READY":
                ready, deadline = True, time.monotonic() + row_timeout
                _BUILT[engine] = True
                continue
            if line.startswith("{"):
                res = json.loads(line)
                results[res["i"]] = res
                start += 1
                deadline = time.monotonic() + row_timeout
                continue
            if _tree_rss(proc) > max_rss_mb * 1024 * 1024:
                fault = "memory"
            elif time.monotonic() > deadline:
                fault = "timeout" if ready else "startup"
            if fault:
                _kill(proc)
                break
        proc.wait(timeout=30)
        if start >= len(jobs):
            break
        if fault == "startup" or not ready:
            for j in jobs[start:]:
                results[j["i"]] = {"i": j["i"], "status": "error", "error": f"{engine} worker did not start"}
            break
        # The row being answered when the worker died or was killed.
        results[jobs[start]["i"]] = {"i": jobs[start]["i"], "status": fault or "crash"}
        start += 1
    return results


def run_tre(rows, infos, timeout=300):
    tre = _load("tre_fuzzy_check", TOOLS / "probes" / "tre-fuzzy-check.py")
    jobs, out = [], {}
    for k, row in enumerate(rows):
        verdict = tre._dialect(dict(row, flags=int(row.get("flags") or 0)))
        # The probe's gate lets a backtracking verb through: `(*PRUNE)` is not a `(?` group, and
        # its characters are all in the ERE core, so TRE parsed `(?:a+(*PRUNE)b){i<=1}` as a
        # different pattern and "disagreed" on 4 of 30 fuzzy+verb rows (measured 2026-09-30).
        if not isinstance(verdict, str) and "(*" in row["pattern"]:
            verdict = "a backtracking verb, which TRE does not have"
        if isinstance(verdict, str):
            out[k] = {"i": k, "status": "n/a", "reason": verdict}
            continue
        body, budget = verdict
        jobs.append({"number": k, "body": body, "budget": budget, "subject": row["subject"]})
    if jobs:
        here = TOOLS / "probes" / "tre-fuzzy-check.py"
        inside = "/mnt/" + here.drive[0].lower() + here.as_posix()[2:]
        try:
            done = subprocess.run(["wsl", "-d", "Ubuntu", "--", tre.WSL_PYTHON, inside, "--in-wsl"],
                                  input=json.dumps(jobs), capture_output=True, text=True,
                                  encoding="utf-8", timeout=timeout, check=False)
            answers = {a["number"]: a for a in json.loads(done.stdout)} if done.returncode == 0 else {}
        except (subprocess.TimeoutExpired, json.JSONDecodeError):
            answers = {}
        for j in jobs:
            a = answers.get(j["number"])
            if a is None:
                out[j["number"]] = {"i": j["number"], "status": "error", "error": "no TRE answer"}
            elif "error" in a:
                out[j["number"]] = {"i": j["number"], "status": "error", "error": a["error"]}
            else:
                out[j["number"]] = {"i": j["number"], "status": "match" if a["matched"] else "none",
                                    "cost": a["cost"], "unit": "cp"}
    return out


# ---------------------------------------------------------------- normalising and comparing
def _to_cp(subject, unit):
    if unit == "cp":
        return None
    table, pos = {}, 0
    for k, ch in enumerate(subject):
        table[pos] = k
        pos += len(ch.encode("utf-8")) if unit == "utf8" else (2 if ord(ch) > 0xFFFF else 1)
    table[pos] = len(subject)
    return table


def normalise(res, subject):
    table = _to_cp(subject, res.get("unit", "cp"))
    if table is None:
        return res
    conv = lambda s: None if s is None else [table.get(s[0], s[0]), table.get(s[1], s[1])]  # noqa: E731
    if res.get("span") is not None:
        res["span"] = conv(res["span"])
    if res.get("groups"):
        res["groups"] = [conv(g) for g in res["groups"]]
    if res.get("captures"):
        res["captures"] = [[conv(c) for c in g] for g in res["captures"]]
    for m in res.get("matches") or []:
        m["span"] = conv(m["span"])
    res["unit"] = "cp"
    return res


ANSWERS = {"match", "none", "partial", "matches"}


def key(res, engine):
    """What two engines must share to agree. TRE is asked existence only."""
    s = res["status"]
    if engine == "tre":
        return ("exists" if s == "match" else "none",)
    if s == "match":
        return ("match", tuple(res["span"]), tuple(tuple(g) if g else None for g in res.get("groups") or []))
    if s == "partial":
        return ("partial", tuple(res["span"]))
    if s == "matches":
        return ("matches", tuple(tuple(m["span"]) + (m.get("partial", False),) for m in res["matches"]))
    return ("none",)


def span_key(k):
    return k[:2] if k[0] == "match" else k


JUDGES = ("fuzzyref", "brute")


def judged(row_results):
    """The key judges' verdicts against upstream and the surveyed engines: for each judge that
    answered, which engines give its span (and, for the fuzzy reference, its fuzzy counts)."""
    out = {}
    for j in JUDGES:
        jr = row_results.get(j)
        if not jr or jr["status"] not in ANSWERS:
            continue
        jk = key(jr, j)
        agree, differ = [], []
        for e, r in row_results.items():
            if e in JUDGES or e == "tre" or r["status"] not in ANSWERS:
                continue
            ek = key(r, e)
            same = span_key(ek) == span_key(jk) if j == "brute" or jk[0] != "match" else ek == jk
            if same and j == "fuzzyref" and e == "regex" and jr.get("fuzzy_counts") != r.get("fuzzy_counts"):
                same = False
            (agree if same else differ).append(e)
        out[j] = {"agree": sorted(agree), "differ": sorted(differ)}
    return out


def verdict(row_results):
    judges = judged(row_results)
    row_results = {e: r for e, r in row_results.items() if e not in JUDGES}
    answered = {e: r for e, r in row_results.items() if r["status"] in ANSWERS}
    full = {e: key(r, e) for e, r in answered.items()}
    exist = {e: ("none",) if k == ("none",) else ("exists",) for e, k in full.items()}
    precise = {e: k for e, k in full.items() if e != "tre"}
    out = {"answered": sorted(answered)}
    if judges:
        out["judges"] = judges
    if len(answered) < 2:
        out["verdict"] = "single" if answered else "none-answered"
        return out
    groups = collections.defaultdict(list)
    for e, k in precise.items():
        groups[k].append(e)
    exist_agree = len(set(exist.values())) == 1
    if len(groups) <= 1:
        out["verdict"] = "agree" if exist_agree else "disagree-existence"
    else:
        spans = {span_key(k) for k in precise.values()}
        out["verdict"] = "disagree-groups" if len(spans) == 1 and exist_agree else "disagree"
    if len(groups) > 1 or not exist_agree:
        out["split"] = [sorted(v) for v in groups.values()]
        if "tre" in full:
            out["split"].append([f"tre:{full['tre'][0]}"])
    caps = {e: r.get("captures") for e, r in answered.items() if r.get("captures") is not None}
    if len(caps) == 2 and len({json.dumps(v) for v in caps.values()}) == 2 and out["verdict"] == "agree":
        out["captures_differ"] = True
    return out


ENGINES = ["regex", "pcre2", "perl", "node", "dotnet", "re", "boost", "tre", "fuzzyref", "brute"]
ROW_TIMEOUT = {"brute": 30.0, "fuzzyref": 20.0}


def survey(rows, out_dir, engines, row_timeout=3.0, max_rss_mb=1500):
    out_dir.mkdir(parents=True, exist_ok=True)
    infos, per_engine_jobs, results = [], collections.defaultdict(list), collections.defaultdict(dict)
    for k, row in enumerate(rows):
        row.setdefault("id", f"r{k}")
        try:
            info = analyse(row)
        except regex.error as e:
            info = None
            results[k]["regex"] = {"status": "error", "error": f"compile: {e}"}
        infos.append(info)
        for engine in engines:
            if engine == "tre" or info is None:
                continue
            t = translate(row, info, engine)
            if isinstance(t, str):
                results[k][engine] = {"status": "n/a", "reason": t}
            else:
                t["i"] = k
                per_engine_jobs[engine].append(t)
    for engine in engines:
        if engine == "tre":
            for k, res in run_tre(rows, infos).items():
                results[k]["tre"] = res
            continue
        t0 = time.monotonic()
        answers = run_engine(engine, per_engine_jobs[engine], out_dir,
                             max(row_timeout, ROW_TIMEOUT.get(engine, 0)), max_rss_mb)
        for k, res in answers.items():
            results[k][engine] = normalise(res, rows[k]["subject"])
        print(f"  {engine:7} {len(per_engine_jobs[engine]):>6} rows in {time.monotonic() - t0:6.1f} s",
              file=sys.stderr)
    cells = collections.defaultdict(collections.Counter)
    with open(out_dir / "results.jsonl", "w", encoding="utf-8", newline="\n") as fr, \
            open(out_dir / "rows.jsonl", "w", encoding="utf-8", newline="\n") as fo:
        for k, row in enumerate(rows):
            for engine in engines:
                res = results[k].get(engine, {"status": "n/a", "reason": "not run"})
                fr.write(json.dumps({"id": row["id"], "engine": engine, **res}, ensure_ascii=False) + "\n")
            v = verdict({e: r for e, r in results[k].items() if e in engines})
            tags = sorted(tags_of(row)) if infos[k] else []
            row_out = dict(row, constructs=tags, families=sorted(families_of(tags)), **v)
            fo.write(json.dumps(row_out, ensure_ascii=False) + "\n")
            cell = row.get("cell", "uncelled")
            c = cells[cell]
            c["rows"] += 1
            c[v["verdict"]] += 1
            for e in v["answered"]:
                c["answered:" + e] += 1
            for j, jv in v.get("judges", {}).items():
                c[f"judged:{j}"] += 1
                for e in jv["differ"]:
                    c[f"judge-differs:{j}:{e}"] += 1
            if v.get("captures_differ"):
                c["captures_differ"] += 1
    summary = {cell: dict(c) for cell, c in sorted(cells.items())}
    (out_dir / "summary.json").write_text(json.dumps(summary, indent=1), encoding="utf-8")
    return summary


def _translation_checks():
    r"""Translations that need no engine to check, each a case a blind review found wrong
    (2026-09-30): a POSIX class or a comment holding `$`, and upstream's \v, \u and \U, which Perl,
    PCRE2 and Boost read differently (measured through this survey that day: `a\v` over 'a\n' is
    None upstream and (0, 2) in all three; Perl reads `a` as 'u0061'; PCRE2 refuses \u)."""
    assert _boost_dollar("a$") == "a(?=\\n?\\z)"
    assert _boost_dollar("[[:alpha:]$]") == "[[:alpha:]$]"
    assert _boost_dollar("[a[:digit:]$]$") == "[a[:digit:]$](?=\\n?\\z)"
    assert _boost_dollar("(?#x$y)a$") == "(?#x$y)a(?=\\n?\\z)"

    def pattern(p, engine):
        row = {"pattern": p, "subject": "a", "operation": "search"}
        t = translate(row, analyse(row), engine)
        return t if isinstance(t, str) else t["pattern"]

    for engine in ("pcre2", "perl", "boost"):
        assert pattern("a\\v", engine) == "a\\x{0B}", engine
        assert pattern("[\\v]", engine) == "[\\x{0B}]", engine
        assert pattern("\\\\v", engine) == "\\\\v", engine
    for engine in ("pcre2", "perl"):
        assert pattern("\\u0061[\\u0062]", engine) == "\\x{0061}[\\x{0062}]", engine
        assert pattern("\\U00000061", engine) == "\\x{00000061}", engine
    assert pattern("\\u0061", "boost").startswith("\\u or \\U"), pattern("\\u0061", "boost")
    assert pattern("\\U00000061", "boost").startswith("\\u or \\U")


def _self_test():
    _translation_checks()
    print("translation checks: ok")
    rows = [
        {"id": "t1", "pattern": r"(a)(?1)b", "subject": "aab", "operation": "search"},
        {"id": "t2", "pattern": r"(?(DEFINE)(?<c>a))(?&c)b", "subject": "ab", "operation": "match"},
        {"id": "t3", "pattern": r"a(?=b)", "subject": "xab", "operation": "finditer"},
        {"id": "t4", "pattern": r"(?:abc){e<=1}", "subject": "xabd", "operation": "search"},
        {"id": "t5", "pattern": r"abc", "subject": "ab", "operation": "match", "partial": True},
        {"id": "t6", "pattern": r"(?r)a+", "subject": "baab", "operation": "search"},
        {"id": "t7", "pattern": r"(?<x>a)|(b)", "subject": "b", "operation": "fullmatch"},
        {"id": "t8", "pattern": r"a+(*SKIP)b|a", "subject": "aac", "operation": "search"},
    ]
    out = Path(os.environ.get("TEMP", ".")) / "matrix-survey-selftest"
    survey(rows, out, ENGINES)
    for line in open(out / "rows.jsonl", encoding="utf-8"):
        r = json.loads(line)
        print(r["id"], r["verdict"], r["answered"], r.get("split", ""))
    for line in open(out / "results.jsonl", encoding="utf-8"):
        r = json.loads(line)
        if r["status"] not in ("n/a",):
            print("  ", r["id"], r["engine"], r["status"], r.get("span"), r.get("groups"),
                  r.get("matches"), r.get("error", ""))
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("rows", nargs="?")
    ap.add_argument("--out", type=Path)
    ap.add_argument("--engines", default=",".join(ENGINES))
    ap.add_argument("--row-timeout", type=float, default=3.0)
    ap.add_argument("--max-rss-mb", type=int, default=1500)
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        return _self_test()
    rows = [json.loads(line) for line in open(a.rows, encoding="utf-8") if line.strip()]
    summary = survey(rows, a.out, a.engines.split(","), a.row_timeout, a.max_rss_mb)
    for cell, c in summary.items():
        print(f"{cell:40} rows {c['rows']:>4}  agree {c.get('agree', 0):>4}  "
              f"disagree {c.get('disagree', 0) + c.get('disagree-existence', 0):>4}  "
              f"groups-only {c.get('disagree-groups', 0):>3}  single {c.get('single', 0):>4}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
