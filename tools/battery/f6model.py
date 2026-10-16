"""A guard-free backtracking model of the exact empty-iteration rules, for the battery's bed screen.

Copied from the 2026-10-15 bed triage (.scratch/s1016/triage-bed/mini.py, NOTES.md there). It walks
the stdlib re parse tree with no guards and no memo, leftmost-first, with a switchable rule for an
iteration that matched empty:
  'A'    after the minimum, no new iteration once the last one matched empty (sre's MAX_UNTIL and
         MIN_UNTIL last_ptr check; Perl and PCRE2 agree).
  'A12'  as A, but an iteration that changed the span of a group the pattern tests (a backreference
         or a group condition) also counts as progress: upstream regex's rule, _regex.c:12550-12553.
  'A12u' A12 with the minimum unrolled, as upstream compiles it. On 6,000 bed rows where re and
         regex agree, A12u equalled regex on all 5,970 it finished; on the 182 bed rows where the
         port and regex differ from re, it equals regex on every one. KNOWN-DEFECTS F6 keeps this
         rule, so a bed row whose port answer equals A12u is that decided rule, not a finding.

    run(pattern, subject, op, rule) -> None | {"span": [s, e], "groups": [...]} | "SKIP"
    safe_run(...) also returns "LIMIT", "NI:<what>" (outside the model) or "RECURSION".
"""
import sys, threading
from re import _parser as P, _constants as C
sys.setrecursionlimit(1_000_000)

class Limit(Exception): pass
WORD = lambda ch: ch.isalnum() or ch == '_'

def cat_ok(cat, ch):
    if cat is C.CATEGORY_DIGIT: return ch.isdigit()
    if cat is C.CATEGORY_NOT_DIGIT: return not ch.isdigit()
    if cat is C.CATEGORY_SPACE: return ch in ' \t\n\r\f\v'
    if cat is C.CATEGORY_NOT_SPACE: return ch not in ' \t\n\r\f\v'
    if cat is C.CATEGORY_WORD: return WORD(ch)
    if cat is C.CATEGORY_NOT_WORD: return not WORD(ch)
    raise NotImplementedError(cat)

def in_ok(items, ch):
    neg = False; hit = False
    for op, av in items:
        if op is C.NEGATE: neg = True
        elif op is C.LITERAL: hit |= ord(ch) == av
        elif op is C.RANGE: hit |= av[0] <= ord(ch) <= av[1]
        elif op is C.CATEGORY: hit |= cat_ok(av, ch)
        else: raise NotImplementedError(op)
    return hit != neg

def refs(sub, out):
    for op, av in sub:
        if op is C.GROUPREF: out.add(av)
        elif op is C.GROUPREF_EXISTS: out.add(av[0]); refs(av[1], out); av[2] and refs(av[2], out)
        elif op is C.SUBPATTERN: refs(av[3], out)
        elif op in (C.MAX_REPEAT, C.MIN_REPEAT): refs(av[2], out)
        elif op is C.BRANCH: [refs(b, out) for b in av[1]]
        elif op in (C.ASSERT, C.ASSERT_NOT): refs(av[1], out)
    return out

def run(pattern, s, op, rule, limit=3_000_000):
    t = P.parse(pattern)
    if t.state.flags & ~(C.SRE_FLAG_UNICODE): return 'SKIP'
    ng = t.state.groups - 1
    referenced = refs(t, set())
    steps = [0]
    n = len(s)

    def seq(items, i, pos, caps, k):
        steps[0] += 1
        if steps[0] > limit: raise Limit
        if i == len(items): return k(pos, caps)
        o, av = items[i]
        nxt = lambda p, c: seq(items, i + 1, p, c, k)
        if o is C.LITERAL: return nxt(pos + 1, caps) if pos < n and ord(s[pos]) == av else None
        if o is C.NOT_LITERAL: return nxt(pos + 1, caps) if pos < n and ord(s[pos]) != av else None
        if o is C.ANY: return nxt(pos + 1, caps) if pos < n and s[pos] != '\n' else None
        if o is C.IN: return nxt(pos + 1, caps) if pos < n and in_ok(av, s[pos]) else None
        if o is C.AT:
            if av is C.AT_BEGINNING or av is C.AT_BEGINNING_STRING: ok = pos == 0
            elif av is C.AT_END: ok = pos == n or (pos == n - 1 and s[pos] == '\n')
            elif av is C.AT_END_STRING: ok = pos == n
            elif av in (C.AT_BOUNDARY, C.AT_NON_BOUNDARY):
                b = (pos > 0 and WORD(s[pos - 1])) != (pos < n and WORD(s[pos]))
                ok = b if av is C.AT_BOUNDARY else not b
            else: raise NotImplementedError(av)
            return nxt(pos, caps) if ok else None
        if o is C.BRANCH:
            for b in av[1]:
                r = seq(list(b), 0, pos, caps, nxt)
                if r is not None: return r
            return None
        if o is C.SUBPATTERN:
            g, sub = av[0], av[3]
            if not g: return seq(list(sub), 0, pos, caps, nxt)
            def close(p, c, start=pos, g=g):
                c2 = list(c); c2[g] = (start, p); return nxt(p, tuple(c2))
            return seq(list(sub), 0, pos, caps, close)
        if o is C.GROUPREF:
            sp = caps[av]
            if sp is None: return None
            txt = s[sp[0]:sp[1]]
            return nxt(pos + len(txt), caps) if s.startswith(txt, pos) else None
        if o is C.GROUPREF_EXISTS:
            g, yes, no = av
            branch = yes if caps[g] is not None else (no or [])
            return seq(list(branch), 0, pos, caps, nxt)
        if o is C.ASSERT or o is C.ASSERT_NOT:
            direction, sub = av
            if direction == 1:
                r = seq(list(sub), 0, pos, caps, lambda p, c: c)
            else:
                lo, hi = sub.getwidth()
                if lo != hi: raise NotImplementedError('var lookbehind')
                r = None if pos - lo < 0 else seq(list(sub), 0, pos - lo, caps, lambda p, c: c if p == pos else None)
            if o is C.ASSERT: return None if r is None else nxt(pos, r)
            return nxt(pos, caps) if r is None else None
        if o is C.MAX_REPEAT or o is C.MIN_REPEAT:
            mn, mx, body = av[0], av[1], list(av[2])
            greedy = o is C.MAX_REPEAT
            if rule == 'A':
                # sre_lib.h MAX_UNTIL / MIN_UNTIL (3.14): min-phase iterations always run and do
                # not set last_ptr; after that a new iteration needs ptr != last_ptr, the start of
                # the last iteration begun in the >= min phase.
                def loop(count, pos, caps, last_ptr):
                    def it(lp):
                        return seq(body, 0, pos, caps, lambda p, c: loop(count + 1, p, c, lp))
                    if count < mn: return it(last_ptr)
                    may_iter = count < mx and pos != last_ptr
                    if greedy:
                        if may_iter:
                            r = it(pos)
                            if r is not None: return r
                        return nxt(pos, caps)
                    r = nxt(pos, caps)
                    if r is not None: return r
                    return it(pos) if may_iter else None
                return loop(0, pos, caps, None)
            # rule A12: _regex.c END_GREEDY_REPEAT (:12550-12600): after each iteration,
            # changed = moved or a referenced group's span changed; body only if changed and
            # count < max; tail if not changed or count >= min.
            def after(count, pos, caps, changed):
                def it():
                    return seq(body, 0, pos, caps,
                               lambda p, c: after(count + 1, p, c, p != pos or any(caps[g] != c[g] for g in referenced)))
                if rule == 'A12u':
                    # hypothesis: the minimum is unrolled, so the min iterations always run and
                    # the remaining {0, max-min} repeat starts fresh (no changed check on entry).
                    if count < mn:
                        return seq(body, 0, pos, caps, lambda p, c: after(count + 1, p, c, True))
                    if count == mn: changed = True
                try_body = changed and count < mx
                try_tail = (not changed) or count >= mn
                if greedy:
                    if try_body:
                        r = it()
                        if r is not None: return r
                    return nxt(pos, caps) if try_tail else None
                if try_tail:
                    r = nxt(pos, caps)
                    if r is not None: return r
                return it() if try_body else None
            return after(0, pos, caps, True)
        raise NotImplementedError(o)

    caps0 = (None,) * (ng + 1)
    def final(p, c, start):
        if op == 'fullmatch' and p != n: return None
        return {'span': [start, p], 'groups': [list(x) if x else None for x in c[1:]]}
    starts = range(n + 1) if op == 'search' else [0]
    for st in starts:
        r = seq(list(t), 0, st, caps0, lambda p, c, st=st: final(p, c, st))
        if r is not None: return r
    return None

def safe_run(pattern, s, op, rule):
    out = {}
    def go():
        try: out['r'] = run(pattern, s, op, rule)
        except Limit: out['r'] = 'LIMIT'
        except NotImplementedError as e: out['r'] = 'NI:' + str(e)
        except RecursionError: out['r'] = 'RECURSION'
    threading.stack_size(250 * 1024 * 1024)
    th = threading.Thread(target=go); th.start(); th.join()
    return out.get('r', 'CRASH')

if __name__ == '__main__':
    p, s, op = sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else 'search'
    for rule in ('A', 'A12', 'A12u'): print(rule, safe_run(p, s, op, rule))
