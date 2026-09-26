"""A random grid of exact patterns with (*SKIP), (*PRUNE) and (*F), run on PCRE2, Perl, upstream
`regex` and this port with and without ledger entry 45's timing, and the rows where they disagree.

Run from the repo root after a Release build of src/FuzzyRegex:

    python tools/probes/skip-timing-grid.py [--rows 10000] [--seed 45]

PCRE2 is Git for Windows' libpcre2-8-0.dll, compiled with NO_START_OPTIMIZE, as
tools/probes/skip-then-prune-perl-pcre2.py drives it. Perl (search rows only) reads the rows on
stdin, since Cygwin's perl mangles braces and backslashes in argv; each row runs plain and with a
code block before every verb, which its optimiser cannot move a verb across. The port runs through
tools/probes/skip-timing-grid-port.ps1 (pwsh 7), once as built and once with
PatternObject.SkipMovesTheSliceWhenItRuns set, which is upstream's timing.

Operations: search, finditer, overlapped finditer and partial search forward; search, finditer
and overlapped finditer under (?r). PCRE2 has no reverse matching, so a (?r) row is put to it as
the mirror image: the pattern with every sequence reversed and ^ and $ swapped (reverse rows hold
no lookarounds), over the reversed subject, and the spans are mapped back. Scans follow Python's
rule: after an empty match the next one may start at the same place only if it is not empty
(PCRE2_NOTEMPTY_ATSTART); an overlapped scan starts again one on from the last match's start.
Partial rows use PCRE2_PARTIAL_SOFT: a complete match anywhere beats a partial.
"""
import argparse, ctypes, json, os, random, subprocess, sys, tempfile

import regex

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
NO_START_OPTIMIZE, NOTEMPTY_ATSTART, PARTIAL_SOFT = 0x10000, 0x8, 0x10

# ---- patterns -------------------------------------------------------------------------------------


def gen(rng, depth, lookarounds):
    """An alternation: a list of sequences, each a list of items."""
    return [gen_seq(rng, depth, lookarounds) for _ in range(rng.choice((1, 1, 2, 2, 3)))]


def gen_seq(rng, depth, lookarounds):
    return [gen_item(rng, depth, lookarounds) for _ in range(rng.randint(1, 3 if depth > 0 else 2))]


def gen_item(rng, depth, lookarounds):
    r = rng.random()
    if r < 0.18:
        return ("verb", rng.choice(("SKIP", "SKIP", "PRUNE", "PRUNE", "F")))
    if r < 0.24:
        return ("anc", rng.choice(("^", "$", r"\b")))
    if r < 0.30 and lookarounds:
        if rng.random() < 0.5 and depth > 0:
            return ("look", rng.choice(("(?=", "(?!")), gen(rng, depth - 1, lookarounds))
        return ("lb", rng.choice(("(?<=", "(?<!")), rng.choice(("a", "b", "[ab]", ".")))
    if r < 0.55 and depth > 0:
        atom = ("grp", rng.choice(("(?:", "(?:", "(", "(?>")), gen(rng, depth - 1, lookarounds))
    else:
        atom = ("lit", rng.choice(("a", "a", "b", "b", "c", ".", "[ab]", "[^a]")))
    if rng.random() < 0.4:
        q = rng.choice(("*", "+", "?", "{0,2}", "{1,2}"))
        q += rng.choice(("", "", "?", "+")) if rng.random() < 0.5 else ""
        return ("q", atom, q)
    return atom


def render(alt):
    return "|".join("".join(render_item(i) for i in seq) for seq in alt)


def render_item(i):
    k = i[0]
    if k == "verb":
        return f"(*{i[1]})"
    if k in ("anc", "lit"):
        return i[1]
    if k == "lb":
        return f"{i[1]}{i[2]})"
    if k in ("grp", "look"):
        return f"{i[1]}{render(i[2])})"
    return render_item(i[1]) + i[2]


def mirror(alt):
    return [[mirror_item(i) for i in reversed(seq)] for seq in alt]


def mirror_item(i):
    k = i[0]
    if k == "anc":
        return ("anc", {"^": "$", "$": "^"}.get(i[1], i[1]))
    if k == "grp":
        return ("grp", i[1], mirror(i[2]))
    if k == "q":
        return ("q", mirror_item(i[1]), i[2])
    assert k in ("verb", "lit"), i
    return i


def has(alt, verb):
    return verb in render(alt)


# ---- PCRE2 ----------------------------------------------------------------------------------------


class Pcre2:
    def __init__(self, pattern):
        err, off = ctypes.c_int(), ctypes.c_size_t()
        b = pattern.encode()
        self.code = lib.pcre2_compile_8(b, len(b), NO_START_OPTIMIZE, ctypes.byref(err), ctypes.byref(off), None)
        if not self.code:
            raise ValueError(f"PCRE2 compile error {err.value}")
        self.md = lib.pcre2_match_data_create_from_pattern_8(self.code, None)

    def at(self, subject, start, opts=0):
        b = subject.encode()
        rc = lib.pcre2_match_8(self.code, b, len(b), start, opts, self.md, None)
        if rc == -1:
            return None
        ov = lib.pcre2_get_ovector_pointer_8(self.md)
        if rc == -2:
            return ("P", ov[0], ov[1])
        if rc < 0:
            raise ValueError(f"PCRE2 match error {rc}")
        return ("M", ov[0], ov[1])


def scan(find, n, overlapped):
    """Python's scan over a find(start, notempty_atstart) -> (s, e) | None."""
    out, pos, notempty = [], 0, False
    while pos <= n:
        m = find(pos, notempty)
        if m is None:
            break
        s, e = m
        out.append((s, e))
        if overlapped:
            pos, notempty = s + 1, False
        elif e == s:
            pos, notempty = e, True
        else:
            pos, notempty = e, False
    return out


def pcre2_answer(row, alt):
    subj, op, rev = row["subj"], row["op"], row["rev"]
    n = len(subj)
    p = Pcre2(render(mirror(alt)) if rev else row["pat"])
    s = subj[::-1] if rev else subj
    flip = (lambda a, b: (n - b, n - a)) if rev else (lambda a, b: (a, b))
    if op in ("search", "partial"):
        m = p.at(s, 0, PARTIAL_SOFT if op == "partial" else 0)
        if m is None:
            return "None"
        a, b = flip(m[1], m[2])
        return f"{'P' if m[0] == 'P' else ''}({a},{b})"

    def find(pos, notempty):
        m = p.at(s, pos, NOTEMPTY_ATSTART if notempty else 0)
        return None if m is None else (m[1], m[2])

    return " ".join(f"({a},{b})" for a, b in (flip(x, y) for x, y in scan(find, n, op == "overlapped")))


def upstream_answer(row):
    pat = row["pat"]
    try:
        c = regex.compile(pat)
        if row["op"] in ("search", "partial"):
            m = c.search(row["subj"], partial=row["op"] == "partial", timeout=2)
            if m is None:
                return "None"
            return f"{'P' if m.partial else ''}({m.start()},{m.end()})"
        ms = c.finditer(row["subj"], overlapped=row["op"] == "overlapped", timeout=2)
        return " ".join(f"({m.start()},{m.end()})" for m in ms)
    except Exception as ex:  # noqa: BLE001 - a probe records what it saw
        return f"error {type(ex).__name__}"


# ---- Perl -----------------------------------------------------------------------------------------

PERL = r'''
use re "eval";
while (my $line = <STDIN>) {
    chomp $line; $line =~ s/\r$//;
    my ($p, $s) = split /\t/, $line, 2; $s = "" unless defined $s;
    my $r = eval { ($s =~ /$p/) ? "($-[0],$+[0])" : "None" };
    print((defined $r ? $r : "error"), "\n");
}
'''


def perl_answers(rows):
    lines = []
    for r in rows:
        lines.append(f"{r['pat']}\t{r['subj']}")
    plain = subprocess.run(["perl", "-e", PERL], input="\n".join(lines) + "\n", capture_output=True, text=True).stdout.split("\n")
    inst_lines = [regex.sub(r"\(\*(SKIP|PRUNE)\)", r"(?{1})(*\1)", r["pat"]) + "\t" + r["subj"] for r in rows]
    inst = subprocess.run(["perl", "-e", PERL], input="\n".join(inst_lines) + "\n", capture_output=True, text=True).stdout.split("\n")
    return plain, inst


# ---- the port -------------------------------------------------------------------------------------


def port_answers(rows, upstream_timing):
    with tempfile.TemporaryDirectory() as d:
        src, dst = os.path.join(d, "rows.jsonl"), os.path.join(d, "out.txt")
        with open(src, "w", encoding="utf-8", newline="\n") as f:
            for r in rows:
                f.write(json.dumps({"pat": r["pat"], "subj": r["subj"], "op": r["op"]}) + "\n")
        here = os.path.dirname(os.path.abspath(__file__))
        args = ["pwsh", "-NoProfile", "-File", os.path.join(here, "skip-timing-grid-port.ps1"), src, dst]
        if upstream_timing:
            args.append("-UpstreamSkipTiming")
        subprocess.run(args, check=True)
        with open(dst, encoding="utf-8") as f:
            return f.read().split("\n")[: len(rows)]


# ---- driver ---------------------------------------------------------------------------------------


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rows", type=int, default=10000)
    ap.add_argument("--seed", type=int, default=45)
    ap.add_argument("--show", type=int, default=40)
    a = ap.parse_args()
    rng = random.Random(a.seed)
    ops = ["search"] * 30 + ["finditer"] * 15 + ["overlapped"] * 15 + ["partial"] * 10
    ops += ["rsearch"] * 15 + ["rfinditer"] * 8 + ["roverlapped"] * 7
    rows, alts = [], []
    while len(rows) < a.rows:
        op = rng.choice(ops)
        rev = op in ("rsearch", "rfinditer", "roverlapped")
        alt = gen(rng, rng.choice((1, 2)), lookarounds=not rev)
        if not has(alt, "(*SKIP)") or (rng.random() < 0.7 and not has(alt, "(*PRUNE)")):
            continue
        pat = render(alt)
        subj = "".join(rng.choice("aabbc") for _ in range(rng.randint(0, 7)))
        row = {"pat": ("(?r)" if rev else "") + pat, "subj": subj, "op": op.lstrip("r") if rev else op, "rev": rev}
        try:
            row["pcre2"] = pcre2_answer(row, alt)
        except ValueError:
            continue
        rows.append(row)
        alts.append(alt)
    for r in rows:
        r["upstream"] = upstream_answer(r)
    fwd_search = [r for r in rows if r["op"] == "search" and not r["rev"]]
    plain, inst = perl_answers(fwd_search)
    for r, p, i in zip(fwd_search, plain, inst):
        r["perl"], r["perl_inst"] = p, i
    after = port_answers(rows, False)
    before = port_answers(rows, True)
    for r, x, y in zip(rows, after, before):
        r["after"], r["before"] = x, y

    def tally(key):
        bad = [r for r in rows if r[key] != r["pcre2"]]
        by_op = {}
        for r in bad:
            k = ("r" if r["rev"] else "") + r["op"]
            by_op[k] = by_op.get(k, 0) + 1
        return bad, by_op

    print(f"rows {len(rows)}, seed {a.seed}; by op:",
          {k: sum(1 for r in rows if ("r" if r["rev"] else "") + r["op"] == k) for k in sorted(set(ops))})
    for key in ("upstream", "before", "after"):
        bad, by_op = tally(key)
        print(f"{key:9} disagrees with PCRE2 on {len(bad):5}  {by_op}")
    pr = [r for r in fwd_search if r["perl"] != r["pcre2"]]
    pi = [r for r in fwd_search if r["perl_inst"] != r["pcre2"]]
    pa = [r for r in fwd_search if r["after"] != r["perl_inst"]]
    print(f"Perl (forward search, {len(fwd_search)} rows): plain vs PCRE2 {len(pr)}, instrumented vs PCRE2 {len(pi)}, "
          f"port after vs instrumented Perl {len(pa)}")
    moved = [r for r in rows if r["after"] != r["before"]]
    fixed = [r for r in moved if r["after"] == r["pcre2"]]
    broke = [r for r in moved if r["before"] == r["pcre2"]]
    print(f"rows the fix moved {len(moved)}: now agree with PCRE2 {len(fixed)}, newly disagree {len(broke)}")
    out = os.path.join(tempfile.gettempdir(), f"skip-timing-grid-{a.seed}.jsonl")
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for r in rows:
            f.write(json.dumps(r) + "\n")
    print("rows written to", out)
    for r in [r for r in rows if r["after"] != r["pcre2"]][: a.show]:
        print(f"  {('r' if r['rev'] else '') + r['op']:11} {r['pat']!r:44} {r['subj']!r:9} PCRE2 {r['pcre2']:22} "
              f"after {r['after']:22} before {r['before']:22} upstream {r['upstream']}")


if __name__ == "__main__":
    main()
