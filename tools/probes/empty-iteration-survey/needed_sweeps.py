"""Sweeps for the "needed" empty-iteration rule (2026-09-26 survey, revised recommendation).

Each sweep runs the reference matcher (tools/probes/fuzzy-reference-matcher.py) in mode "needed"
and compares whether a match EXISTS against:
  - mode "unrestricted" (an error-spending empty iteration may always go round again; bounded by
    the error budget, and by UNRESTRICTED_EMPTY_RUN where the budget restarts per iteration);
  - where the whole pattern is one fuzzy section around an exact regex, a brute force: is there a
    string in the exact regex's language that aligns to the text (fullmatch) or a prefix of it
    (match) within the limits?
Every case runs under a 2 s deadline; a timeout or RecursionError is reported.

    python tools/probes/empty-iteration-survey/needed_sweeps.py [s1 s2 s3 s4 s5 counts]

s1: 9000-case grid (items x quantifiers x constraints x texts, match and fullmatch).
s2: 13608-case nested and lazy grid (match, fullmatch, search).
s3: entry 33's case and variants with captures, conditionals and backreferences.
s4: minimum error constraints ({2<=d<=2}, {1<=e<=3}, minimum plus cost equation, nested sections).
s5: termination with unbounded budgets ({d}, {e}, {1<=d}), minimums deletions cannot reach
    ({1<=s<=1,d}, {1<=i<=1}), and nested repeats.
s4cap: the nested s4 cases where the oracle times out, re-run at empty-run caps 1, 2 and 3.
rand: the second review's random nested sweep (seeds 3 and 4), with the 20 slowest cases timed
    against mode "minimum" and upstream.
counts: fuzzy_counts of "needed" against regex plain, (?e), (?b) on s1/s2 and fuzzy.tsv.
"""
import importlib.util
import itertools
import os
import sys
import threading
import time

import regex

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("ref", os.path.join(HERE, "..", "fuzzy-reference-matcher.py"))
ref = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ref)
ref.DELETE_AFTER_EXACT = True
ref.UNRESTRICTED_EMPTY_RUN = 6
# NEEDED_DEDUP_ALL=0 in the environment turns off the repeat memo (pruning repeated states after
# iterations that consume text), leaving only the empty-iteration state check.
ref.NEEDED_DEDUP_ALL = os.environ.get("NEEDED_DEDUP_ALL", "1") == "1"

DEADLINE = [0.0]
_run = ref.run


class Timeout(Exception):
    pass


def _timed_run(node, st, ctx, k):
    if time.monotonic() > DEADLINE[0]:
        raise Timeout
    return _run(node, st, ctx, k)


ref.run = _timed_run


def ref_result(mode, rule, pattern, text, limit=2.0):
    """(span, fuzzy_counts), None, 'TIMEOUT' or 'RECURSION'."""
    ref.EMPTY_DELETION_ITERATIONS = rule
    DEADLINE[0] = time.monotonic() + limit
    try:
        m = getattr(ref, mode)(pattern, text)
    except Timeout:
        return "TIMEOUT"
    except RecursionError:
        return "RECURSION"
    return None if m is None else (m.span, m.fuzzy_counts)


def upstream(mode, pattern, text, flag=""):
    try:
        m = getattr(regex, mode)(flag + pattern, text, timeout=2)
    except TimeoutError:
        return "TIMEOUT"
    except Exception as e:  # MemoryError, regex.error
        return type(e).__name__
    return None if m is None else (m.span(), m.fuzzy_counts)


# ---------------------------------------------------------------- brute force

_lang = {}


def language(exact, alphabet, maxlen):
    key = (exact, alphabet, maxlen)
    if key not in _lang:
        rx = regex.compile(exact)
        _lang[key] = [
            w for n in range(maxlen + 1) for w in map("".join, itertools.product(alphabet, repeat=n)) if rx.fullmatch(w)
        ]
    return _lang[key]


_align = {}


def aligns(w, t, lim):
    """Some alignment of pattern string w to text t within lim (substitution only on a mismatch)."""
    key = (w, t, lim)
    if key in _align:
        return _align[key]
    maxs, costs, max_cost = lim.maxs, lim.costs, lim.max_cost

    def ok(c):
        return all(n <= m for n, m in zip(c, maxs)) and sum(c) <= maxs[3] and sum(
            n * x for n, x in zip(c, costs)) <= max_cost

    n, m = len(w), len(t)
    R = [[set() for _ in range(m + 1)] for _ in range(n + 1)]
    R[0][0].add((0, 0, 0))
    for a in range(n + 1):
        for b in range(m + 1):
            for s, i, d in R[a][b]:
                if a < n and b < m:
                    if w[a] == t[b]:
                        R[a + 1][b + 1].add((s, i, d))
                    elif ok((s + 1, i, d)):
                        R[a + 1][b + 1].add((s + 1, i, d))
                if b < m and ok((s, i + 1, d)):
                    R[a][b + 1].add((s, i + 1, d))
                if a < n and ok((s, i, d + 1)):
                    R[a + 1][b].add((s, i, d + 1))
    res = any(ref.within(c, lim) for c in R[n][m])
    _align[key] = res
    return res


def brute(mode, exact, cons, text):
    lim = ref.parse_limits(cons.strip("{}").split(","))
    budget = lim.maxs[2] if lim.maxs[2] < ref.INF else 3
    budget = min(budget, lim.maxs[3] if lim.maxs[3] < ref.INF else 3)
    alphabet = "".join(sorted(set(ch for ch in exact + text if ch.isalpha())))
    words = language(exact, alphabet, len(text) + budget)
    ends = [len(text)] if mode == "fullmatch" else range(len(text) + 1)
    return any(aligns(w, text[:e], lim) for e in ends for w in words)


# ---------------------------------------------------------------- sweeps


class Tally:
    def __init__(self, name):
        self.name, self.n, self.lost, self.extra, self.brute_lost, self.brute_model = name, 0, [], [], [], []
        self.timeouts, self.brute_checked, self.brute_extra = [], 0, []

    def add(self, mode, p, t, need, unres, br=None):
        self.n += 1
        for label, r in (("needed", need), ("unrestricted", unres)):
            if r in ("TIMEOUT", "RECURSION"):
                self.timeouts.append((label, r, mode, p, t))
        # The brute force is independent of both rules, so it counts even when one timed out.
        if br is not None:
            self.brute_checked += 1
            if not br and need not in (None, "TIMEOUT", "RECURSION"):
                self.brute_extra.append((mode, p, t, need, unres))
            if br and need is None:
                (self.brute_model if unres is None else self.brute_lost).append((mode, p, t, need, unres))
        if need in ("TIMEOUT", "RECURSION") or unres in ("TIMEOUT", "RECURSION"):
            return
        # search: a match must also start where the oracle's does, or it was lost at that start.
        start = (lambda r: None if r is None else r[0][0])
        if need is None and unres is not None or (
                mode == "search" and None not in (need, unres) and start(need) > start(unres)):
            self.lost.append((mode, p, t, need, unres))
        if need is not None and unres is None or (
                mode == "search" and None not in (need, unres) and start(need) < start(unres)):
            self.extra.append((mode, p, t, need, unres))

    def report(self):
        print(f"{self.name}: cases {self.n}; needed-vs-unrestricted existence (and start, for search): lost {len(self.lost)}, "
              f"needed-only {len(self.extra)}; brute force says a match exists but needed has none: "
              f"{len(self.brute_lost) + len(self.brute_model)} (of which unrestricted also none: "
              f"{len(self.brute_model)}); brute force checked {self.brute_checked}, says none but needed matched "
              f"{len(self.brute_extra)}; timeouts/recursion {len(self.timeouts)} "
              f"(in needed: {sum(1 for x in self.timeouts if x[0] == 'needed')})")
        for label, rows in (("LOST", self.lost), ("NEEDED-ONLY", self.extra), ("BRUTE", self.brute_lost),
                            ("BRUTE-MODEL", self.brute_model[:5]), ("BRUTE-EXTRA", self.brute_extra),
                            ("TIMEOUT", self.timeouts[:20])):
            for row in rows[:10]:
                print("  ", label, row)


def s1():
    items = ['b', 'ab', 'b|c', '(b)', 'b?', 'b*', 'bc?', '(?:b|)', 'b+?', 'b*?']
    quants = ['*', '+', '?', '{2}', '{2,}', '{0,2}', '{3}', '*?', '+?', '{2,}?']
    cons = ['{d<=1}', '{d<=2}', '{1<=d<=2}', '{e<=1}', '{1<=e<=2}']
    texts = ['', 'b', 'bb', 'bba', 'ab', 'abab', 'a', 'c', 'bcb']
    tally, minimum_lost = Tally("s1"), 0
    for it, q, c, t in itertools.product(items, quants, cons, texts):
        exact = f'(?:(?:{it}){q})'
        p = exact + c
        for mode in ('match', 'fullmatch'):
            need, unres = ref_result(mode, "needed", p, t), ref_result(mode, "unrestricted", p, t)
            if ref_result(mode, "minimum", p, t) is None and unres not in (None, "TIMEOUT", "RECURSION"):
                minimum_lost += 1
            tally.add(mode, p, t, need, unres, brute(mode, exact, c, t))
    tally.report()
    print(f"   (old rule B, mode 'minimum', loses {minimum_lost} of the same cases)")


def s2():
    inner = ['(?:b*)', '(?:b+)', '(?:b{2})', '(?:ab?)', '(?:b*?)', '(?:(b)c)']
    q = ['*', '+', '{2}', '{2,}', '*?', '{0,3}']
    cons = ['{d<=1}', '{d<=2}', '{e<=2}']
    texts = ['', 'b', 'bb', 'bab', 'abc', 'bcbc', 'bbb']
    tally = Tally("s2")
    for i, q1, q2, c, t in itertools.product(inner, q, q, cons, texts):
        exact = f'(?:(?:{i}{q1}){q2})'
        p = exact + c
        for mode in ('match', 'fullmatch', 'search'):
            need, unres = ref_result(mode, "needed", p, t), ref_result(mode, "unrestricted", p, t)
            br = brute(mode, exact, c, t) if mode != 'search' and len(t) <= 3 else None
            tally.add(mode, p, t, need, unres, br)
    tally.report()


def s3():
    firsts = ['(?(1)c|z)', '(?(1)c)', '(?(2)c|z)', '\\1c']
    seconds = ['()(?:x){d<=1}', '(?:()x){d<=1}', '(x?)(?:y){d<=1}', '()(?:xy){d<=2}', '(?:(x)|())(?:y){d<=1}']
    quants = ['*', '+', '{2,}', '*?', '{0,4}']
    tails = ['$', '', 'c$', '\\1$']
    texts = ['', 'c', 'cc', 'zc', 'xc', 'cx', 'xcc']
    tally = Tally("s3")
    rows = []
    for a, b, q, tail, t in itertools.product(firsts, seconds, quants, tails, texts):
        body = f'(?:{a}|{b})'
        if a.startswith('(?(2)') and b.count('(') - b.count('(?') < 2:
            continue
        p = body + q + tail
        for mode in ('search', 'fullmatch'):
            need, unres = ref_result(mode, "needed", p, t), ref_result(mode, "unrestricted", p, t)
            tally.add(mode, p, t, need, unres)
            rows.append((mode, p, t, need, unres))
    # backreference and outer-section variants
    extra = [
        ('search', '(?:(?(1)c|z)|()(?:x){d<=1})*$', 'c'),
        ('search', '^(?:(?(1)c|z)|()(?:x){d<=1})*$', 'c'),
        ('search', '(?:(?(1)c|z)|()x)*${d<=2}', 'c'),
        ('search', '(?:(?:(?(1)c|z)|()x)*$){d<=2}', 'c'),
        ('search', '(?:(?:(?(1)c|z)|()x)*$){1<=d<=2}', 'c'),
        ('search', '(?:(b)|(?:x){d<=1})*\\1', 'b'),
        ('search', '(?:(b?)(?:x){d<=1})*\\1c', 'bc'),
        ('search', '(?:(b?)(?:x){d<=1})+?\\1c', 'c'),
        ('search', '(?:(?:(x)|())y)*(?(1)q|c)', 'c'),
        ('search', '(?:(?:(?:(x)|())y)*(?(2)c|q)){d<=2}', 'c'),
        ('search', '(?:(?:(?:(x)|())y)*(?(2)c|q)){d<=2}', 'q'),
        ('search', '(?:\\d+a0b+?){d<=2}', '67a0bab'),
    ]
    for mode, p, t in extra:
        need, unres = ref_result(mode, "needed", p, t), ref_result(mode, "unrestricted", p, t)
        tally.add(mode, p, t, need, unres)
        print("   s3 row", mode, p, repr(t), "needed", need, "unrestricted", unres, "upstream", upstream(mode, p, t))
    tally.report()


def s4():
    items = ['b', 'ab', '(b)', 'b?', 'b|c', '(?:b|)']
    quants = ['*', '+', '{2,}', '{0,2}', '*?', '{2}']
    cons = ['{1<=d<=1}', '{2<=d<=2}', '{1<=d<=3}', '{1<=e<=3}', '{2<=e<=3}', '{1<=i<=2,d<=2}',
            '{1<=s<=1,d<=2}', '{1<=e<=3,2i+2d+1s<=4}', '{2<=d<=3,1<=e<=3}']
    texts = ['', 'b', 'bb', 'bba', 'ab', 'c', 'abab']
    tally = Tally("s4 flat")
    for it, q, c, t in itertools.product(items, quants, cons, texts):
        exact = f'(?:(?:{it}){q})'
        p = exact + c
        for mode in ('match', 'fullmatch'):
            need, unres = ref_result(mode, "needed", p, t), ref_result(mode, "unrestricted", p, t)
            tally.add(mode, p, t, need, unres, brute(mode, exact, c, t))
    tally.report()
    tally = Tally("s4 nested")
    inner_cons = ['{d<=1}', '{e<=1}', '{1<=d<=1}']
    outer_cons = ['{1<=d<=3}', '{2<=d<=3}', '{2<=e<=4}', '{1<=e<=3,2i+2d+1s<=5}']
    for it, q, ci, co, t in itertools.product(items[:4], quants, inner_cons, outer_cons, texts):
        for p in (f'(?:(?:(?:{it}){ci}){q}){co}', f'(?:(?:(?:{it}){q}){ci}c?){co}', f'(?:(?:{it}){q}(?:x){ci}){co}'):
            for mode in ('match', 'fullmatch'):
                need, unres = ref_result(mode, "needed", p, t), ref_result(mode, "unrestricted", p, t)
                tally.add(mode, p, t, need, unres)
    tally.report()


def s5():
    inner = ['b', 'b*', '(b)', '(b|)', '()', 'b?', '(?:b|c)*', '()(?(1)b|c)', '(b?)\\1']
    quants = ['*', '+', '*?', '{2,}']
    # The last four have a minimum that deletions cannot reach (s or i) beside an unbounded or
    # per-iteration deletion budget: the second review's non-terminating shapes.
    cons = ['{d}', '{e}', '{1<=d}', '{i,d}', '{2<=e}', '{1<=s<=1,d}', '{1<=i<=1,d}', '{1<=s<=1}', '{1<=i<=1}']
    texts = ['', 'b', 'bb', 'bab', 'xyz']
    n, bad, slowest = 0, [], (0.0, None)
    for i, q1, q2, c, t in itertools.product(inner, quants, quants, cons, texts):
        for p in (f'(?:(?:(?:{i}){q1}){q2}){c}', f'(?:(?:(?:{i}){c}){q1}){q2}',
                  f'(?:(?:(?:(?:{i}){{d<=1}}){q1}){q2}){c}'):
            for mode in ('match', 'search', 'fullmatch'):
                n += 1
                t0 = time.monotonic()
                r = ref_result(mode, "needed", p, t)
                dt = time.monotonic() - t0
                if dt > slowest[0]:
                    slowest = (dt, (mode, p, t, r))
                if r in ("TIMEOUT", "RECURSION"):
                    bad.append((mode, p, t, r))
    print(f"s5: cases {n}; timeouts/recursion {len(bad)}; slowest {slowest[0]:.3f}s {slowest[1]}")
    for row in bad[:15]:
        print("  ", row)


def s4cap():
    """The nested s4 cases where "unrestricted" times out at its default cap of 6 empty iterations
    in a row, re-run at caps 1, 2 and 3 with a 120 s limit."""
    items = ['b', 'ab', '(b)', 'b?']
    quants = ['*', '+', '{2,}', '{0,2}', '*?', '{2}']
    inner_cons = ['{d<=1}', '{e<=1}', '{1<=d<=1}']
    outer_cons = ['{1<=d<=3}', '{2<=d<=3}', '{2<=e<=4}', '{1<=e<=3,2i+2d+1s<=5}']
    texts = ['', 'b', 'bb', 'bba', 'ab', 'c', 'abab']
    n = agree = 0
    for it, q, ci, co, t in itertools.product(items, quants, inner_cons, outer_cons, texts):
        for p in (f'(?:(?:(?:{it}){ci}){q}){co}', f'(?:(?:(?:{it}){q}){ci}c?){co}', f'(?:(?:{it}){q}(?:x){ci}){co}'):
            for mode in ('match', 'fullmatch'):
                if ref_result(mode, "unrestricted", p, t) != "TIMEOUT":
                    continue
                n += 1
                need = ref_result(mode, "needed", p, t)
                capped = []
                for cap in (1, 2, 3):
                    ref.UNRESTRICTED_EMPTY_RUN = cap
                    capped.append(ref_result(mode, "unrestricted", p, t, limit=120))
                ref.UNRESTRICTED_EMPTY_RUN = 6
                # A low cap can be too tight to reach a minimum; what matters is that no cap finds a
                # match "needed" misses and the loosest cap agrees with it.
                same = (capped[-1] is None) == (need is None) and not (
                    need is None and any(c is not None for c in capped))
                agree += same
                print("  ", mode, p, repr(t), "needed", need, "unrestricted cap 1/2/3", capped,
                      "" if same else "EXISTENCE DIFFERS")
    print(f"s4cap: {n} oracle timeouts re-run; existence agrees with needed: {agree}")


# The second blind review's random nested sweep (scratchpad review-ei2/bf.py, 2026-09-26),
# regenerated with the same generator, constraint list and draw order.
RAND_CONS = ['{d<=1}', '{d<=2}', '{e<=1}', '{e<=2}', '{1<=e<=2}', '{1<=d<=2}', '{2<=d<=3}', '{1<=s<=1,d<=2}',
             '{1<=i<=1,d<=2}', '{s<=1,1<=d<=2}', '{1<=e<=3,2i+2d+1s<=4}', '{2<=e<=3,1s+1d<=3}', '{i<=1,1<=d<=1}']


def _gen(rng, depth=0):
    r = rng.random()
    if depth > 2 or r < 0.35:
        a = rng.choice(['a', 'b', 'c', 'b'])
    elif r < 0.55:
        a = '(?:' + _gen(rng, depth + 1) + '|' + rng.choice([_gen(rng, depth + 1), '']) + ')'
    elif r < 0.7:
        a = '(' + _gen(rng, depth + 1) + ')'
    else:
        a = '(?:' + _gen(rng, depth + 1) + _gen(rng, depth + 1) + ')'
    q = rng.choice(['', '', '*', '+', '?', '*?', '+?', '??', '{2}', '{2,}', '{0,2}', '{1,3}?', '{2,}?'])
    return a + q


def rand(seeds=(3, 4), n=300):
    import random
    texts = [''.join(p) for k in range(4) for p in itertools.product('abc', repeat=k)]
    tally = Tally("rand")
    timings = []
    for seed in seeds:
        rng = random.Random(seed)
        for _ in range(n):
            ex = '(?:' + _gen(rng) + rng.choice(['', _gen(rng)]) + ')'
            try:
                regex.compile(ex)
            except Exception:
                continue
            c = rng.choice(RAND_CONS)
            p = ex + c
            for t in rng.sample(texts, 6):
                for mode in ('match', 'fullmatch'):
                    t0 = time.monotonic()
                    need = ref_result(mode, "needed", p, t)
                    dt = time.monotonic() - t0
                    unres = ref_result(mode, "unrestricted", p, t)
                    tally.add(mode, p, t, need, unres, brute(mode, ex, c, t))
                    timings.append((dt, mode, p, t))
    tally.report()
    timings.sort(reverse=True)
    print("   slowest 20 under needed: needed s | minimum s | upstream s (2 s limit)")
    for dt, mode, p, t in timings[:20]:
        t0 = time.monotonic()
        ref_result(mode, "minimum", p, t)
        dm = time.monotonic() - t0
        t0 = time.monotonic()
        u = upstream(mode, p, t)
        du = time.monotonic() - t0
        print(f"   {dt:6.2f} | {dm:6.2f} | {du:5.2f} {'TIMEOUT' if u == 'TIMEOUT' else ''} {mode} {p} {t!r}")


def s6():
    """The third review's targeted grid (review-ei3/t7.py): an empty iteration that is possible only
    before a consuming one (^, or a conditional that makes the deletion dearer once a group is set)."""
    xs = ['^z', 'z', '(?(1)zz|z)', '(?(1)z|zz)', '^zz']
    ys = ['(aa)', 'aa', '(a)b', 'a']
    qs = ['*', '+', '*?', '{0,3}', '{2,}']
    cs = ['{1<=d<=1}', '{2<=d<=2}', '{1<=e<=2}', '{1<=d<=2}', '{2<=d<=3,e<=3}']
    texts = ['', 'a', 'aa', 'ab', 'aab', 'aaaa']
    tally = Tally("s6")
    for x, y, q, c, t in itertools.product(xs, ys, qs, cs, texts):
        if '(?(1)' in x and '(' not in y:
            continue
        exact = f'(?:(?:{x}|{y}){q})'
        for mode in ('match', 'fullmatch'):
            tally.add(mode, exact + c, t, ref_result(mode, "needed", exact + c, t),
                      ref_result(mode, "unrestricted", exact + c, t), brute(mode, exact, c, t))
    tally.report()


def timing():
    """The review rows that were slow or lost, and the (a|b|c|d)* family, against rule B and upstream."""
    rows = [("fullmatch", "(?:(?:(?:(?:a??a??)??|(?:a{1,3}?|){2}){1,3}?|){2,}){1<=s<=1,d<=2}", "cac"),
            ("fullmatch", "(?:(?:^z|aa)*){1<=d<=1}", "aa"),
            ("fullmatch", "(?:(?:(?(1)zz|z)|(aa))*){1<=d<=1}", "aa"),
            ("fullmatch", "(?:(?:^z|aa)*?){1<=d<=1}", "aa"),
            ("fullmatch", "(?:(?:^z|aa)+){1<=e<=1}", "aa"),
            ("fullmatch", "(?:(?:^z|(?:a){e<=0})+){2<=d<=2}", "a"),
            ("search", "(?:(?:(?(1)zz|z)|(aa))*){1<=d<=1}$", "aa"),
            ("fullmatch", "(?:b*){1<=s<=1,d}", ""),
            ("fullmatch", "(?:(?:(?:x){d<=1})*){1<=s<=1}", ""),
            ("match", "(?:b*){1<=i<=1,d}", "")]
    rows += [("fullmatch", f"(?:(?:a|b|c|d)*){{{n}<=d<={n}}}", "x") for n in range(2, 13)]
    print("   needed | minimum (rule B) | unrestricted | upstream; seconds")
    for mode, p, t in rows:
        cells = []
        for rule in ("needed", "minimum", "unrestricted"):
            t0 = time.monotonic()
            r = ref_result(mode, rule, p, t, limit=60)
            cells.append(f"{r} {time.monotonic() - t0:.3f}")
        t0 = time.monotonic()
        u = upstream(mode, p, t)
        cells.append(f"{u} {time.monotonic() - t0:.3f}")
        print("  ", mode, p, repr(t), " | ".join(cells), flush=True)


def perf():
    """Growth of the families the fourth review found slow, at several sizes: "needed" (with the
    repeat memo), "needed" with only the empty-iteration check, rule B, and upstream (10 s limit)."""
    fams = [
        ('fullmatch', "(?:(?:a|b|c|d)*){3<=d<=3}", lambda n: 'a' * n + 'x'),
        ('fullmatch', "(?:(?:(?:a|b|c|d)*)*){3<=d<=3}", lambda n: 'a' * n + 'x'),
        ('fullmatch', "(?:(?:a|b|c|d)*(?:a|b|c|d)*){4<=d<=4}", lambda n: 'a' * n + 'x'),
        ('search', "(?:(?:(?:a|b)*?)+?){2<=d<=2}c", lambda n: 'ab' * (n // 2)),
        ('fullmatch', "(?:(?:(a)|b|c|d)*(?(1)q|r)){2<=d<=2}", lambda n: 'a' * n + 'x'),
        ('fullmatch', "(?:(?:(?:a|b|c|d){1<=d<=1})*){4<=d<=4}", lambda n: 'a' * n + 'x'),
    ]
    print("   size | needed | needed, empty check only | rule B | upstream; seconds")
    for mode, p, tf in fams:
        for n in (4, 8, 16, 32):
            t = tf(n)
            cells = []
            for rule, memo in (("needed", True), ("needed", False), ("minimum", True)):
                ref.NEEDED_DEDUP_ALL = memo
                t0 = time.monotonic()
                r = ref_result(mode, rule, p, t, limit=30)
                cells.append(f"{time.monotonic() - t0:7.3f}{' TIMEOUT' if r == 'TIMEOUT' else ''}")
            ref.NEEDED_DEDUP_ALL = True
            t0 = time.monotonic()
            try:
                u = getattr(regex, mode)(p, t, timeout=10)
                u = "" if u is not None or True else ""
            except Exception as e:
                u = " " + type(e).__name__
            cells.append(f"{time.monotonic() - t0:7.3f}{u}")
            print(f"   {mode} {p} n={n}: " + " | ".join(cells), flush=True)


def counts():
    """fuzzy_counts: needed vs upstream plain, (?e), (?b), on s1 grid (no-min constraints) and fuzzy.tsv."""
    from collections import Counter
    items = ['b', 'ab', 'b|c', '(b)', 'b?', 'b*', 'bc?', '(?:b|)', 'b+?', 'b*?']
    quants = ['*', '+', '?', '{2}', '{2,}', '{0,2}', '{3}', '*?', '+?', '{2,}?']
    cons = ['{d<=1}', '{d<=2}', '{1<=d<=2}', '{e<=1}', '{1<=e<=2}']
    texts = ['', 'b', 'bb', 'bba', 'ab', 'abab', 'a', 'c', 'bcb']
    tab = Counter()
    examples = {}
    with open(os.path.join(HERE, "needed_counts.tsv"), "w", encoding="utf-8", newline="\n") as out:
        out.write("mode\tpattern\ttext\tneeded\tunrestricted\tplain\te\tb\n")
        for it, q, c, t in itertools.product(items, quants, cons, texts):
            p = f'(?:(?:{it}){q}){c}'
            for mode in ('match', 'fullmatch'):
                need = ref_result(mode, "needed", p, t)
                unres = ref_result(mode, "unrestricted", p, t)
                ups = [upstream(mode, p, t, f) for f in ("", "(?e)", "(?b)")]
                out.write("\t".join(map(str, (mode, p, repr(t), need, unres, *ups))) + "\n")
                for name, u in zip(("plain", "(?e)", "(?b)"), ups):
                    if isinstance(u, str):
                        key = (name, "upstream " + u)
                    elif need is None or u is None:
                        key = (name, "both none" if need is None and u is None else "one none")
                    else:
                        a, b = sum(need[1]), sum(u[1])
                        key = (name, "same" if need == u else "fewer errors" if a < b else "more errors" if a > b
                               else "same total, different span/types")
                    tab[key] += 1
                    examples.setdefault(key, (mode, p, t, need, u))
    for key in sorted(tab):
        print("counts s1", key, tab[key], "e.g.", examples[key])


def modes():
    """Every rule on the s1, s2 and s4-flat grids: matches lost against "unrestricted", and the
    reference matcher's total time per rule (a rough measure of search size, not engine speed)."""
    grids = []
    for it, q, c, t in itertools.product(['b', 'ab', 'b|c', '(b)', 'b?', 'b*', 'bc?', '(?:b|)', 'b+?', 'b*?'],
                                         ['*', '+', '?', '{2}', '{2,}', '{0,2}', '{3}', '*?', '+?', '{2,}?'],
                                         ['{d<=1}', '{d<=2}', '{1<=d<=2}', '{e<=1}', '{1<=e<=2}'],
                                         ['', 'b', 'bb', 'bba', 'ab', 'abab', 'a', 'c', 'bcb']):
        grids += [(m, f'(?:(?:{it}){q}){c}', t) for m in ('match', 'fullmatch')]
    for i, q1, q2, c, t in itertools.product(['(?:b*)', '(?:b+)', '(?:b{2})', '(?:ab?)', '(?:b*?)', '(?:(b)c)'],
                                             *[['*', '+', '{2}', '{2,}', '*?', '{0,3}']] * 2,
                                             ['{d<=1}', '{d<=2}', '{e<=2}'],
                                             ['', 'b', 'bb', 'bab', 'abc', 'bcbc', 'bbb']):
        grids += [(m, f'(?:(?:{i}{q1}){q2}){c}', t) for m in ('match', 'fullmatch', 'search')]
    for it, q, c, t in itertools.product(['b', 'ab', '(b)', 'b?', 'b|c', '(?:b|)'],
                                         ['*', '+', '{2,}', '{0,2}', '*?', '{2}'],
                                         ['{1<=d<=1}', '{2<=d<=2}', '{1<=d<=3}', '{1<=e<=3}', '{2<=e<=3}',
                                          '{1<=i<=2,d<=2}', '{1<=s<=1,d<=2}', '{1<=e<=3,2i+2d+1s<=4}',
                                          '{2<=d<=3,1<=e<=3}'],
                                         ['', 'b', 'bb', 'bba', 'ab', 'c', 'abab']):
        grids += [(m, f'(?:(?:{it}){q}){c}', t) for m in ('match', 'fullmatch')]
    oracle = [ref_result(m, "unrestricted", p, t) for m, p, t in grids]
    print(f"modes: {len(grids)} cases")
    for rule in ("unrestricted", "perl", "reject", "minimum", "needed"):
        t0 = time.monotonic()
        res = [ref_result(m, rule, p, t) for m, p, t in grids]
        dt = time.monotonic() - t0
        lost = sum(1 for r, o in zip(res, oracle) if r is None and o is not None)
        same = sum(1 for r, o in zip(res, oracle) if r == o)
        print(f"   {rule:12s} lost {lost:5d}  identical result to unrestricted {same:5d}  time {dt:.1f}s")


def tre():
    """TRE (WSL) against needed, regex plain, (?e) and (?b) on the greedy part of the s1 grid without
    minimums, and needed on fuzzy.tsv. TRE is minimum cost, so compare total errors."""
    import subprocess
    from collections import Counter
    items = ['b', 'ab', 'b|c', '(b)', 'b?', 'b*', 'bc?', '(?:b|)']
    quants = ['*', '+', '?', '{2}', '{2,}', '{0,2}', '{3}']
    cons = {'{d<=1}': (0, 0, 1, 1), '{d<=2}': (0, 0, 2, 2), '{e<=1}': (1, 1, 1, 1)}
    texts = ['', 'b', 'bb', 'bba', 'ab', 'abab', 'a', 'c', 'bcb']
    cases, lines = [], []
    for it, q, c, t in itertools.product(items, quants, cons, texts):
        p = f'(?:(?:{it}){q}){c}'
        ere = f'(({it}){q})'.replace('(?:', '(')
        for mode, form in (('match', '^%s'), ('fullmatch', '^%s$')):
            lines.append("\t".join((str(len(cases)), form % ere, t, *map(str, cons[c]))))
            cases.append((mode, p, t))
    win = HERE.replace("\\", "/")
    wsl_dir = subprocess.run(["wsl", "-d", "Ubuntu", "--", "wslpath", "-a", win], capture_output=True,
                             text=True).stdout.strip()
    # One process per case under `timeout 2`, so a case TRE cannot finish is reported, not waited on.
    # The cases go through a file: piping them to wsl.exe's stdin delivered nothing (2026-09-26).
    import tempfile
    with tempfile.NamedTemporaryFile("w", suffix=".tsv", delete=False, encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    cases_wsl = subprocess.run(["wsl", "-d", "Ubuntu", "--", "wslpath", "-a", f.name.replace("\\", "/")],
                               capture_output=True, text=True).stdout.strip()
    script = (f"cd '{wsl_dir}' && gcc tre_batch.c -ltre -o /tmp/tre_batch && "
              "while IFS= read -r l; do printf '%s\\n' \"$l\" | timeout 2 /tmp/tre_batch "
              f"|| printf '%s\\tTIMEOUT\\n' \"${{l%%\t*}}\"; done < '{cases_wsl}'")
    out = subprocess.run(["wsl", "-d", "Ubuntu", "-e", "bash", "-c", script], stdin=subprocess.DEVNULL,
                         capture_output=True, text=True, timeout=3600).stdout
    os.unlink(f.name)
    tre_res = {}
    for row in out.splitlines():
        cid, r = row.split("\t")
        if r == "TIMEOUT":
            tre_res[int(cid)] = "TIMEOUT"
            continue
        if r.startswith("span"):
            span, sid = r.split(" ")
            tre_res[int(cid)] = sum(map(int, sid.split("=")[1].strip("()").split(",")))
        else:
            tre_res[int(cid)] = None
    tab, ex = Counter(), {}
    for n, (mode, p, t) in enumerate(cases):
        tr = tre_res.get(n, "missing")
        others = {"needed": ref_result(mode, "needed", p, t)}
        for name, f in (("plain", ""), ("(?e)", "(?e)"), ("(?b)", "(?b)")):
            others[name] = upstream(mode, p, t, f)
        for name, r in others.items():
            if isinstance(r, str):
                key = (name, "error " + r)
            elif tr in ("TIMEOUT", "missing"):
                key = (name, "TRE " + tr)
            elif tr is None or r is None:
                key = (name, "existence same" if (tr is None) == (r is None) else "existence differs")
            else:
                a = sum(r[1])
                key = (name, "same total as TRE" if a == tr else "more errors than TRE" if a > tr else "fewer than TRE")
            tab[key] += 1
            ex.setdefault(key, (mode, p, t, r, tr))
    print(f"tre: cases {len(cases)}")
    for key in sorted(tab):
        print("  ", key, tab[key], "e.g.", ex[key])
    for line in open(os.path.join(HERE, "fuzzy.tsv"), encoding="utf-8"):
        if line.startswith("#") or not line.strip():
            continue
        cid, item, subj, n = line.rstrip("\n").split("\t")[:4]
        p = f"(?:{item}){{d<={n}}}"
        print("   fuzzy.tsv", cid, p, repr(subj), "needed match", ref_result("match", "needed", p, subj),
              "plain", upstream("match", p, subj), "(?b)", upstream("match", p, subj, "(?b)"))


if __name__ == "__main__":
    sys.setrecursionlimit(100000)
    threading.stack_size(256 * 1024 * 1024 - 4096)
    wanted = sys.argv[1:] or ["s1", "s2", "s3", "s4", "s5", "s6", "s4cap", "rand", "modes", "timing"]

    def main():
        for name in wanted:
            t0 = time.monotonic()
            globals()[name]()
            print(f"# {name} took {time.monotonic() - t0:.0f}s", flush=True)

    th = threading.Thread(target=main)
    th.start()
    th.join()
