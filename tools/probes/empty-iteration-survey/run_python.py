"""Python-hosted engines: re, regex (mrab), PCRE2 (pcre2 binding), RE2 (google-re2)."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "pylib"))  # google-re2 installed locally with pip --target
import re, regex

def battery():
    for line in open(os.path.join(HERE, "battery.tsv"), encoding="utf-8"):
        if line.startswith("#") or not line.strip(): continue
        f = line.rstrip("\n").split("\t")
        yield f[0], f[1], f[2]

def fmt(span, g):
    return f"span={span[0]},{span[1]} g1={'unset' if g is None else repr(g)}"

def run(name, search):
    for cid, pat, subj in battery():
        try:
            print(f"{name}\t{cid}\t{search(pat, subj)}")
        except Exception as e:
            print(f"{name}\t{cid}\terror: {type(e).__name__}: {e}")

def pyre(mod):
    def s(p, t):
        m = mod.search(p, t)
        return "nomatch" if m is None else fmt(m.span(), m.group(1))
    return s

run(f"python-re {sys.version.split()[0]}", pyre(re))
run(f"regex {regex.__version__} V0", pyre(regex))
try:
    import pcre2
    def s(p, t):
        m = pcre2.compile(p).search(t) if hasattr(pcre2.compile(p), "search") else pcre2.compile(p).match(t)
        if m is None: return "nomatch"
        return fmt((m.start(), m.end()), m.group(1) if m.start(1) >= 0 else None) if hasattr(m, "start") else str(m)
    run(f"pcre2 {pcre2.__libpcre2_version__}", s)
except ImportError as e:
    print("pcre2\t-\tunavailable", e)
try:
    import re2
    def s(p, t):
        m = re2.search(p, t)
        return "nomatch" if m is None else fmt(m.span(), m.group(1))
    run("re2 (google-re2 " + __import__("importlib.metadata").metadata.version("google-re2") + ")", s)
except ImportError as e:
    print("re2\t-\tunavailable", e)
