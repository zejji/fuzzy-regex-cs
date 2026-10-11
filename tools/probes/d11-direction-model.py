r"""D11 round 3: a model of the "undetermined dominates" design, checked against an independent judge.

    python tools/probes/d11-direction-model.py row OP PATTERN SUBJECT   # one row: model vs judge
    python tools/probes/d11-direction-model.py rows                     # the named rows (design note)
    python tools/probes/d11-direction-model.py grid N SEED [--jobs J]   # random grid, N patterns
    python tools/probes/d11-direction-model.py perf                     # section 9's measurements

The model shares no code with the judge (`d11-brute-judge.py`, upstream `regex` without partial) or
with round 2's `d11-universe-model.py`. It is a backtracking matcher written in continuation-passing
style, with two modes:

- concrete: an ordinary matcher over a known text. It is checked against upstream on every grid row
  (the held text, and 24 sampled `t + w`), so a row where the model's own regex semantics differ from
  upstream's is reported as a semantics row and kept out of the gate;
- abstract: the second pass of the design. The text past the edge (right of `t`; left of it under
  `(?r)`) is unknown. The four mechanisms of section 4 are marked M1-M4 below.

Design terms used in the code:

  touch mark    an entry the path pushed when it read `w`: an undetermined predicate that passed, a
                negative lookaround that passed undetermined, a branch taken on an undetermined test,
                a run-out inside a body. Backtracking past it removes it.
  taint         a flag on a construct instance C whose cut was skipped (cut_taint), or on a verb scope
                S where a verb was skipped (verb_taint). It lives while C (or S) is on the path.
  blocked(s,E)  the cut rule: a cut, or a definite decision, at construct E is taken only when no
                touch mark is newer than E, no cut_taint is on a construct newer than E, and no
                verb_taint is on E or a newer construct.
"""

import json
import os
import random
import sys
import threading
import time

sys.setrecursionlimit(1_000_000)
threading.stack_size(128 * 1024 * 1024)

UNK = object()          # an unknown character (inside w)
UNKNOWN_GROUP = "?"     # a group whose value the second pass does not know
ROOT = object()         # the target of a whole-match success


class Budget(Exception):
    pass


class ForkBudget(Exception):
    """M4: the attempt kept too many decisions both ways; the call answers a partial."""


class Prune(Exception):
    def __init__(self, scope, skip_pos):
        self.scope, self.skip_pos = scope, skip_pos


# ------------------------------------------------------------------------------------------ parser

class Parser:
    def __init__(self, pattern):
        self.p, self.i, self.ngroups = pattern, 0, 0
        self.reverse = self.multiline = False
        while self.p.startswith("(?", self.i) and self.i + 2 < len(self.p) and self.p[self.i + 2] in "rm":
            j = self.p.index(")", self.i)
            flags = self.p[self.i + 2:j]
            if not set(flags) <= set("rm"):
                break
            self.reverse |= "r" in flags
            self.multiline |= "m" in flags
            self.i = j + 1
        self.reps = 0
        self.tree = self.alt()
        if self.i != len(self.p):
            raise SyntaxError(f"trailing text at {self.i}")

    def peek(self, s):
        return self.p.startswith(s, self.i)

    def alt(self):
        branches = [self.seq()]
        while self.peek("|"):
            self.i += 1
            branches.append(self.seq())
        return branches[0] if len(branches) == 1 else ("alt", branches)

    def seq(self):
        items = []
        while self.i < len(self.p) and self.p[self.i] not in "|)":
            items.append(self.quant(self.atom()))
        return ("seq", items)

    def group_body(self):
        body = self.alt()
        if not self.peek(")"):
            raise SyntaxError("missing )")
        self.i += 1
        return body

    def atom(self):
        p = self.p
        c = p[self.i]
        if self.peek("(*"):
            j = p.index(")", self.i)
            verb = p[self.i + 2:j]
            self.i = j + 1
            return ("verb", {"F": "FAIL"}.get(verb, verb))
        if self.peek("(?:"):
            self.i += 3
            return self.group_body()
        if self.peek("(?>"):
            self.i += 3
            return ("atomic", self.group_body())
        for prefix, behind, neg in (("(?=", False, False), ("(?!", False, True), ("(?<=", True, False), ("(?<!", True, True)):
            if self.peek(prefix):
                self.i += len(prefix)
                first, at = self.ngroups, self.i
                body = self.group_body()
                return ("look", behind, neg, body, frozenset(range(first + 1, self.ngroups + 1)), has_verb(self.p[at:self.i]))
        if self.peek("(?("):
            self.i += 3
            if p[self.i].isdigit():
                j = p.index(")", self.i)
                idx = int(p[self.i:j])
                self.i = j + 1
                test = ("group", idx)
            else:
                for prefix, behind, neg in (("?=", False, False), ("?!", False, True), ("?<=", True, False), ("?<!", True, True)):
                    if self.peek(prefix):
                        self.i += len(prefix)
                        first, at = self.ngroups, self.i
                        body = self.group_body()
                        test = ("look", behind, neg, body, frozenset(range(first + 1, self.ngroups + 1)), has_verb(self.p[at:self.i]))
                        break
                else:
                    raise SyntaxError("bad condition")
            yes = self.seq()
            no = ("seq", [])
            if self.peek("|"):
                self.i += 1
                no = self.seq()
            if not self.peek(")"):
                raise SyntaxError("condition with more than two branches")
            self.i += 1
            return ("cond", test, yes, no)
        if c == "(":
            self.i += 1
            self.ngroups += 1
            idx = self.ngroups
            return ("group", idx, self.group_body())
        if c == "[":
            j = p.index("]", self.i + 1)
            chars = frozenset(p[self.i + 1:j])
            self.i = j + 1
            return ("char", lambda x, cs=chars: x in cs)
        if c == "\\":
            e = p[self.i + 1]
            self.i += 2
            if e.isdigit():
                return ("ref", int(e))
            if e in "bBmMAZ":
                return ("pred", e)
            if e == "w":
                return ("char", isword)
            if e == "W":
                return ("char", lambda x: not isword(x))
            if e == "d":
                return ("char", lambda x: x.isdigit())
            if e == "n":
                return ("char", lambda x: x == "\n")
            return ("char", lambda x, e=e: x == e)
        self.i += 1
        if c == ".":
            return ("char", lambda x: x != "\n")
        if c in "^$":
            return ("pred", c)
        return ("char", lambda x, c=c: x == c)

    def quant(self, node):
        p = self.p
        if self.i >= len(p) or p[self.i] not in "*+?{":
            return node
        c = p[self.i]
        if c == "{":
            j = p.index("}", self.i)
            lo, _, hi = p[self.i + 1:j].partition(",")
            mn, mx = int(lo), (int(hi) if hi else (int(lo) if "," not in p[self.i + 1:j] else 10 ** 9))
            self.i = j + 1
        else:
            mn, mx = {"*": (0, 10 ** 9), "+": (1, 10 ** 9), "?": (0, 1)}[c]
            self.i += 1
        mode = "greedy"
        if self.i < len(p) and p[self.i] == "?":
            mode, self.i = "lazy", self.i + 1
        elif self.i < len(p) and p[self.i] == "+":
            mode, self.i = "possessive", self.i + 1
        self.reps += 1
        return ["rep", node, mn, mx, mode, self.reps, False]


def has_verb(text):
    return "(*PRUNE" in text or "(*SKIP" in text


def isword(c):
    return c.isalnum() or c == "_"


def mark_guardable(node, inside, has_ref, in_look=False):
    """Guards (the engine's repeat guards, modelled for performance) go on unbounded repeats that are
    not inside another repeat, and only when the pattern has no backreference or group condition
    (upstream's RE_STATUS_REF rule, _regex.c:23400-23420). A repeat inside a lookaround body or a
    condition test is marked "look": in the second pass it records a guard only while no taint is on
    the path (section 4, repeat guards)."""
    kind = node[0]
    if kind == "rep":
        ok = not inside and not has_ref and node[3] >= 10 ** 9
        node[6] = ("look" if in_look else True) if ok else False
        mark_guardable(node[1], True, has_ref, in_look)
        return
    if kind == "look":
        mark_guardable(node[3], inside, has_ref, True)
        return
    if kind == "cond":
        if node[1][0] == "look":
            mark_guardable(node[1][3], inside, has_ref, True)
        mark_guardable(node[2], inside, has_ref, in_look)
        mark_guardable(node[3], inside, has_ref, in_look)
        return
    if kind == "seq" or kind == "alt":
        for x in node[1]:
            mark_guardable(x, inside, has_ref, in_look)
    elif kind == "group":
        mark_guardable(node[2], inside, has_ref, in_look)
    elif kind == "atomic":
        mark_guardable(node[1], inside, has_ref, in_look)


# ----------------------------------------------------------------------------------------- matcher

class Inst:
    """One entry into a construct: its sequence number orders it against every other entry on the
    path (the engine's Bstack index)."""
    __slots__ = ("seq", "cut_taint", "verb_taint", "open")

    def __init__(self, seq):
        self.seq, self.cut_taint, self.verb_taint, self.open = seq, False, False, True


class Frames:
    __slots__ = ("inst", "parent")

    def __init__(self, inst, parent):
        self.inst, self.parent = inst, parent


class St:
    __slots__ = ("pos", "groups", "touch", "frames", "look", "scope", "tags", "undet")

    def __init__(self, pos, groups, touch, frames, look, scope, tags, undet):
        self.pos, self.groups, self.touch, self.frames = pos, groups, touch, frames
        self.look, self.scope, self.tags, self.undet = look, scope, tags, undet

    def but(self, **kw):
        s = St(self.pos, self.groups, self.touch, self.frames, self.look, self.scope, self.tags, self.undet)
        for k, v in kw.items():
            setattr(s, k, v)
        return s


class Look:
    __slots__ = ("inst", "end", "groups", "has_verb")

    def __init__(self, inst, end, groups, has_verb):
        self.inst, self.end, self.groups, self.has_verb = inst, end, groups, has_verb


# Tags a success path carries, for classifying phantoms.
T_RUNOUT_TOP, T_RUNOUT_BODY, T_UNDET, T_MAYBE, T_BOTH, T_UNKGROUP, T_SKIPCUT, T_SEARCHCONV, T_BUDGET = (1 << i for i in range(9))
TAG_NAMES = ["runout-top", "runout-body", "undetermined", "neg-maybe", "both-branches", "unknown-group", "skipped-cut", "search-convention", "budget"]


class Run:
    def __init__(self, parsed, text, partial, guards="on", budget=2_000_000, variant="design"):
        self.P, self.text, self.n, self.partial = parsed, text, len(text), partial
        self.fwd = not parsed.reverse
        self.ml = parsed.multiline
        self.seq = 0
        self.steps = 0
        self.budget = budget
        self.guard_mode = guards
        self.guards = set()
        self.touches_total = 0
        self.variant = variant
        self.events = {}
        self.forks = 0
        self.memo = {}          # the lookaround body memo, second pass, shared by every attempt

    # --- text access

    def ch(self, i):
        if 0 <= i < self.n:
            return self.text[i]
        if self.partial and ((self.fwd and i >= self.n) or (not self.fwd and i < 0)):
            return UNK
        return None

    def new(self):
        self.seq += 1
        return Inst(self.seq)

    def event(self, name):
        self.events[name] = self.events.get(name, 0) + 1
        if name in FORK_EVENTS:
            self.forks += 1
            if self.partial and self.variant != "no-budget" and self.forks > 256 * (self.n + 1):
                raise ForkBudget()

    def body_events(self, look, s):
        """A lookaround or condition-test body, as (target, state) events. In the second pass the
        body's outcome at (node, position, captures in) is the same in every attempt: nothing in it
        depends on the path outside the body (section 4.2, the memo). So it is recorded once and
        replayed: each success as its captures and whether it was blocked, and how the body ended."""
        E, gen = self.look_body(look, s)
        if not self.partial or self.variant == "no-memo":
            return E, gen
        key = (id(look), s.pos, s.groups)
        entry = self.memo.get(key)
        if entry is None:
            entry = self.memo[key] = {"recs": [], "end": None}
        return E, self.memo_gen(s, E, gen, entry)

    def memo_gen(self, s, E, gen, entry):
        recs = entry["recs"]
        i = 0
        try:
            while i < len(recs):
                groups, blocked, tags, undet = recs[i]
                i += 1
                self.event("memo-replay")
                touch = s.touch
                if blocked:
                    self.seq += 1
                    touch = self.seq
                yield (E, s.but(groups=groups, tags=s.tags | tags, undet=s.undet + undet,
                                frames=Frames(E, s.frames), touch=touch))
            if entry["end"] == "pruned":
                raise Prune(E, None)
            if entry["end"] == "done":
                return
            # Not recorded this far (an earlier attempt stopped early): run the body, skipping what
            # is recorded, and record the rest.
            j = 0
            try:
                for tgt, st in gen:
                    j += 1
                    if j <= len(recs):
                        continue
                    recs.append((st.groups, self.blocked(st, E), st.tags, st.undet - s.undet))
                    yield (tgt, st)
            except Prune as e:
                if e.scope is E:
                    entry["end"] = "pruned"
                raise
            entry["end"] = "done"
        finally:
            gen.close()

    def touch(self, s, tag):
        """Push a touch mark (the engine pushes a no-op Bstack entry and records its index)."""
        self.seq += 1
        self.touches_total += 1
        return s.but(touch=self.seq, tags=s.tags | tag)

    def blocked(self, s, E):
        """The cut rule (M2)."""
        if self.variant == "plain-count":        # the direction as first stated: no taint
            return s.touch > E.seq
        if s.touch > E.seq:
            return True
        f = s.frames
        while f is not None:
            inst = f.inst
            if inst.seq < E.seq:
                break
            if self.variant == "taint-inclusive" and inst.cut_taint:
                return True
            if self.variant == "verb-taint-exclusive" and inst.verb_taint and inst.seq == E.seq:
                f = f.parent
                continue
            if inst.verb_taint or (inst.cut_taint and inst.seq > E.seq):
                return True
            f = f.parent
        return False

    # --- M1: three-valued predicates

    def pred(self, kind, p):
        """True, False or None (undetermined over every non-empty w)."""
        partial_fwd = self.partial and self.fwd
        partial_rev = self.partial and not self.fwd
        n = self.n
        if kind in "bBmM":
            left, right = self.ch(p - 1), self.ch(p)
            if left is UNK or right is UNK:
                known = right if left is UNK else left
                kw = known is not None and isword(known)
                # The unknown side can be a word or a non-word character.
                outcomes = set()
                for uw in (True, False):
                    lw, rw = (uw, kw) if left is UNK else (kw, uw)
                    outcomes.add({"b": lw != rw, "B": lw == rw, "m": (not lw) and rw, "M": lw and not rw}[kind])
                return outcomes.pop() if len(outcomes) == 1 else None
            lw = left is not None and isword(left)
            rw = right is not None and isword(right)
            return {"b": lw != rw, "B": lw == rw, "m": (not lw) and rw, "M": lw and not rw}[kind]
        if kind == "$":
            if self.ml:
                c = self.ch(p)
                if c is UNK:
                    return None
                return c is None or c == "\n"
            if partial_fwd:
                return None if p == n else False
            return p == n or (p == n - 1 and self.text[p] == "\n")
        if kind == "Z":
            return False if partial_fwd else p == n
        if kind in "^A":
            if kind == "^" and self.ml:
                if p == 0 and not partial_rev:
                    return True
                c = self.ch(p - 1)
                if c is UNK:
                    return None
                return c == "\n"
            return False if partial_rev else p == 0
        raise ValueError(kind)

    # --- the matcher

    def m(self, node, s, k, d):
        self.steps += 1
        if self.steps > self.budget:
            raise Budget()
        kind = node[0]
        if kind == "seq":
            items = node[1] if d > 0 else node[1][::-1]
            return self.seq_run(items, 0, s, k, d)
        if kind == "char":
            return self.m_char(node[1], s, k, d)
        if kind == "alt":
            return self.m_alt(node[1], s, k, d)
        if kind == "pred":
            return self.m_pred(node[1], s, k)
        if kind == "group":
            return self.m_group(node[1], node[2], s, k, d)
        if kind == "atomic":
            return self.m_atomic(node[1], s, k, d)
        if kind == "look":
            return self.m_look(node, s, k)
        if kind == "cond":
            return self.m_cond(node, s, k, d)
        if kind == "ref":
            return self.m_ref(node[1], s, k, d)
        if kind == "verb":
            return self.m_verb(node[1], s, k)
        if kind == "rep":
            if node[4] == "possessive":
                return self.m_atomic(("rep", node[1], node[2], node[3], "greedy", node[5], node[6]), s, k, d)
            return self.rep(node, s, 0, k, d)
        raise ValueError(kind)

    def seq_run(self, items, i, s, k, d):
        if i == len(items):
            yield from k(s)
            return
        yield from self.m(items[i], s, lambda s1: self.seq_run(items, i + 1, s1, k, d), d)

    def m_alt(self, branches, s, k, d):
        for b in branches:
            yield from self.m(b, s, k, d)

    def runout(self, s):
        """A consuming item needs a character of w. Top level: the rest is presumed (limit 2) and the
        path succeeds. Inside a lookaround or condition body: the body is presumed to succeed, and
        the groups it could set are unknown."""
        if s.look is None:
            yield (ROOT, s.but(tags=s.tags | T_RUNOUT_TOP))
            return
        L = s.look
        # The rest of the body is not run, so its cuts are not taken: in the universes where w lets
        # the path go on, every construct still open inside the body may commit, and a verb after
        # the edge may unwind to its scope. Both are skipped cuts (the run-out taint).
        if self.variant != "no-runout-taint":
            f = s.frames
            while f is not None and f.inst.seq > L.inst.seq:
                # Only a construct still open at the edge can commit, or be a verb's scope, after it.
                if f.inst.open or self.variant == "runout-taint-all":
                    f.inst.cut_taint = True
                    if L.has_verb:
                        f.inst.verb_taint = True
                f = f.parent
            if L.has_verb:
                L.inst.verb_taint = True
        groups = tuple(UNKNOWN_GROUP if i in L.groups else g for i, g in enumerate(s.groups))
        s2 = self.touch(s, T_RUNOUT_BODY).but(groups=groups)
        self.event("runout-body")
        yield from L.end(s2)

    def m_char(self, test, s, k, d):
        i = s.pos if d > 0 else s.pos - 1
        c = self.ch(i)
        if c is UNK:
            yield from self.runout(s)
        elif c is not None and test(c):
            yield from k(s.but(pos=s.pos + d))

    def m_pred(self, kind, s, k):
        r = self.pred(kind, s.pos)
        if r is None:
            self.event("M1-undetermined")
            yield from k(self.touch(s, T_UNDET).but(undet=s.undet + 1))
        elif r:
            yield from k(s)

    def m_group(self, idx, body, s, k, d):
        start = s.pos

        def close(s1):
            span = (min(start, s1.pos), max(start, s1.pos))
            g = list(s1.groups)
            g[idx] = span
            return k(s1.but(groups=tuple(g)))
        yield from self.m(body, s, close, d)

    def m_ref(self, idx, s, k, d):
        g = s.groups[idx] if idx < len(s.groups) else None
        if g is None:
            return
        if g == UNKNOWN_GROUP:
            self.event("ref-unknown")
            yield from self.runout(s.but(tags=s.tags | T_UNKGROUP))
            return
        value = self.text[g[0]:g[1]]
        L = len(value)
        for j in range(L):
            c = self.ch(s.pos + j) if d > 0 else self.ch(s.pos - 1 - j)
            want = value[j] if d > 0 else value[L - 1 - j]
            if c is UNK:
                yield from self.runout(s)
                return
            if c != want:
                return
        yield from k(s.but(pos=s.pos + d * L))

    def m_verb(self, verb, s, k):
        if verb == "FAIL":
            return
        yield from k(s)
        # Backtracking has reached the verb.
        scope = s.scope
        if not self.partial or not self.blocked(s, scope):
            raise Prune(scope, s.pos if verb == "SKIP" else None)
        self.event("verb-skipped")
        scope.verb_taint = True

    def enter(self, s, look=None):
        E = self.new()
        s_in = s.but(frames=Frames(E, s.frames), scope=E)
        if look is not None:
            s_in.look = look
        return E, s_in

    def m_atomic(self, body, s, k, d):
        E, s_in = self.enter(s)
        gen = self.m(body, s_in, lambda s1: iter(((E, s1),)), d)
        try:
            for tgt, st in gen:
                if tgt is not E:
                    yield (tgt, st)
                    continue
                out = st.but(scope=s.scope, look=s.look)
                if not self.partial or not self.blocked(st, E):
                    gen.close()
                    E.open = False
                    yield from k(out)
                    return
                self.event("cut-skipped")
                E.cut_taint = True
                E.open = False
                yield from k(out.but(tags=out.tags | T_SKIPCUT))
                E.open = True
        except Prune as e:
            if e.scope is not E:
                raise
        finally:
            gen.close()

    def look_body(self, look, s):
        _, behind, neg, body, groupset, verbs = look
        E = self.new()
        L = Look(E, lambda s1: iter(((E, s1),)), groupset, verbs)
        s_in = s.but(frames=Frames(E, s.frames), scope=E, look=L)
        return E, self.m(body, s_in, L.end, -1 if behind else 1)

    def m_look(self, look, s, k):
        neg = look[2]
        E, gen = self.body_events(look, s)
        if not neg:
            seen = set()
            try:
                for tgt, st in gen:
                    if tgt is not E:
                        yield (tgt, st)
                        continue
                    out = st.but(pos=s.pos, scope=s.scope, look=s.look)
                    again = self.partial and self.variant != "no-dedup" and st.groups in seen
                    if not self.partial or not self.blocked(st, E):
                        gen.close()
                        E.open = False
                        if again:
                            self.event("dedup")
                            return
                        yield from k(out)
                        return
                    self.event("cut-skipped")
                    E.cut_taint = True
                    if again:
                        # The same captures were continued already, with at least these marks, and
                        # that continuation failed: the continuation cannot depend on anything else.
                        self.event("dedup")
                        continue
                    seen.add(st.groups)
                    E.open = False
                    yield from k(out.but(tags=out.tags | T_SKIPCUT))
                    E.open = True
            except Prune as e:
                if e.scope is not E:
                    raise
            finally:
                gen.close()
            return
        try:
            for tgt, st in gen:
                if tgt is not E:
                    yield (tgt, st)
                    continue
                if not self.partial or not self.blocked(st, E):
                    return                    # a definite body success: the assertion fails
                self.event("neg-maybe")
                E.cut_taint = True            # a body success in some universes only
        except Prune as e:
            if e.scope is not E:
                raise
        finally:
            gen.close()
        yield from k(self.touch(s, T_MAYBE) if E.cut_taint else s)

    def m_cond(self, node, s, k, d):
        _, test, yes, no = node
        if test[0] == "group":
            g = s.groups[test[1]] if test[1] < len(s.groups) else None
            if g == UNKNOWN_GROUP:
                self.event("cond-unknown-group")
                yield from self.m(yes, self.touch(s, T_BOTH | T_UNKGROUP), k, d)
                yield from self.m(no, self.touch(s, T_BOTH | T_UNKGROUP), k, d)
            else:
                yield from self.m(yes if g is not None else no, s, k, d)
            return
        neg = test[2]
        E, gen = self.body_events(test, s)
        pruned = False
        seen = set()
        try:
            for tgt, st in gen:
                if tgt is not E:
                    yield (tgt, st)
                    continue
                definite = not self.partial or not self.blocked(st, E)
                if neg:
                    if definite:
                        gen.close()
                        yield from self.m(no, s, k, d)
                        return
                    self.event("cond-maybe")
                    E.cut_taint = True
                    continue
                out = st.but(pos=s.pos, scope=s.scope, look=s.look)
                again = self.partial and self.variant != "no-dedup" and st.groups in seen
                if definite:
                    gen.close()
                    E.open = False
                    if again:
                        self.event("dedup")
                        return
                    yield from self.m(yes, out, k, d)
                    return
                self.event("cond-maybe")
                E.cut_taint = True
                if again:
                    self.event("dedup")
                    continue
                seen.add(st.groups)
                E.open = False
                yield from self.m(yes, out.but(tags=out.tags | T_SKIPCUT), k, d)
                E.open = True
        except Prune as e:
            if e.scope is not E:
                raise
            pruned = True
        finally:
            gen.close()
        # The test body is exhausted (or a verb ended it): the positive test is false and the
        # negative test true, and in the universes where the body succeeded the other branch runs.
        s_both = self.touch(s, T_BOTH) if E.cut_taint else s
        if neg:
            yield from self.m(yes, s_both, k, d)
            if E.cut_taint:
                yield from self.m(no, self.touch(s, T_BOTH), k, d)
        else:
            yield from self.m(no, s_both, k, d)

    def rep(self, node, s, count, k, d):
        _, body, mn, mx, mode, rid, guardable = node
        guard = guardable and count >= mn and self.guard_mode != "off"
        key = (rid, s.pos)
        if guard and key in self.guards:
            self.event("guard-hit")
            return
        yielded = False
        touches_at_entry = self.touches_total

        def after(s1):
            if s1.pos == s.pos and count + 1 > mn:
                # An empty iteration ends the repeat.
                return k(s1) if mode == "greedy" else iter(())
            return self.rep(node, s1, count + 1, k, d)

        if mode == "lazy":
            if count >= mn:
                for x in k(s):
                    yielded = True
                    yield x
            if count < mx:
                for x in self.m(body, s, after, d):
                    yielded = True
                    yield x
        else:
            if count < mx:
                for x in self.m(body, s, after, d):
                    yielded = True
                    yield x
            if count >= mn:
                for x in k(s):
                    yielded = True
                    yield x
        if guard and not yielded:
            if self.guard_mode == "untouched" and self.touches_total != touches_at_entry:
                return
            if guardable == "look" and self.partial and self.variant != "look-guards-always":
                f = s.frames
                while f is not None:
                    if f.inst.cut_taint or f.inst.verb_taint:
                        self.event("guard-refused")
                        return
                    f = f.parent
            self.guards.add(key)

    # --- attempts

    def attempt(self, start, op):
        self.guards = set()
        self.forks = 0
        R = self.new()
        ng = self.P.ngroups + 1
        s0 = St(start, (None,) * ng, 0, Frames(R, None), None, R, 0, 0)
        end = self.n if self.fwd else 0

        def fin(s):
            if op == "fullmatch" and (self.partial or s.pos != end):
                # M3: a second-pass success needs w to be empty, so fullmatch backtracks.
                return iter(())
            return iter(((ROOT, s),))
        gen = self.m(self.P.tree, s0, fin, 1 if self.fwd else -1)
        try:
            for tgt, st in gen:
                if tgt is ROOT:
                    return st, None
        except Prune as e:
            return None, e.skip_pos
        except ForkBudget:
            self.event("budget-fired")
            return s0.but(tags=T_BUDGET), None
        finally:
            gen.close()
        return None, None


def run_op(parsed, op, text, partial, guards="on", budget=2_000_000, variant="design", stats=None):
    """Concrete: ('F', s, e) or None. Abstract (the second pass): ('P', s, e, tags) or None."""
    R = Run(parsed, text, partial, guards, budget, variant)
    n = R.n
    try:
        if op in ("match", "fullmatch"):
            start = 0 if R.fwd else n
            st, _ = R.attempt(start, op)
            if st is None:
                return None
            if partial:
                return ("P", 0, n, st.tags, st.undet)
            return ("F", min(start, st.pos), max(start, st.pos))
        if R.fwd:
            s = 0
            while s <= n:
                st, skip = R.attempt(s, op)
                if st is not None:
                    return ("P", s, n, st.tags, st.undet) if partial else ("F", s, st.pos)
                s = max(s + 1, skip) if skip is not None else s + 1
            return ("P", n, n, T_SEARCHCONV, 0) if partial else None
        s = n
        while s >= 0:
            st, skip = R.attempt(s, op)
            if st is not None:
                return ("P", 0, s, st.tags, st.undet) if partial else ("F", st.pos, s)
            s = min(s - 1, skip) if skip is not None else s - 1
        return ("P", 0, 0, T_SEARCHCONV, 0) if partial else None
    finally:
        if stats is not None:
            stats["steps"] = stats.get("steps", 0) + R.steps
            for k, v in R.events.items():
                stats[k] = stats.get(k, 0) + v


def pattern_has_ref(pattern):
    """Upstream turns repeat guards off where a backreference or a group condition follows
    (RE_STATUS_REF, _regex.c:23400-23420); a lookaround condition does not."""
    return any(f"\\{i}" in pattern or f"(?({i})" in pattern for i in range(1, 10))


def design(pattern, op, subject, guards="on", variant="design", stats=None, budget=2_000_000):
    """The design's answer: the first pass concretely, then the abstract second pass."""
    parsed = Parser(pattern)
    mark_guardable(parsed.tree, False, pattern_has_ref(pattern))
    first = run_op(parsed, op, subject, False, guards, budget, variant, stats)
    if first is not None:
        return first
    return run_op(parsed, op, subject, True, guards, budget, variant, stats)


# --------------------------------------------------------------------------------------- the judge

def load_judge():
    import importlib.util
    here = os.path.dirname(os.path.abspath(__file__))
    spec = importlib.util.spec_from_file_location("d11_brute_judge", os.path.join(here, "d11-brute-judge.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def show(v):
    return "None" if v is None else f"{v[0]}({v[1]},{v[2]})"


def concrete_agrees(pattern, op, text):
    """The model's own concrete matcher against upstream on one known text."""
    import regex
    parsed = Parser(pattern)
    mark_guardable(parsed.tree, False, True)
    mine = run_op(parsed, op, text, False)
    theirs = getattr(regex, op)(pattern, text)
    return (mine is None) == (theirs is None) and (mine is None or mine[1:3] == theirs.span())


def classify(truth, got, reverse):
    """agree / miss / phantom / span-phantom / span-miss."""
    if (truth is None) and (got is None):
        return "agree"
    if truth is None:
        return "phantom"
    if got is None:
        return "miss"
    if truth[0] == "F" or got[0] == "F":
        return "agree" if truth[:3] == got[:3] else "semantics"
    if truth[1:3] == got[1:3]:
        return "agree"
    # A partial at a less constrained span than the truth is over-approximation; the reverse is a miss.
    if reverse:
        return "span-phantom" if got[2] > truth[2] else "span-miss"
    return "span-phantom" if got[1] < truth[1] else "span-miss"


def phantom_class(got):
    tags, undet = got[3], got[4]
    names = [TAG_NAMES[i] for i in range(len(TAG_NAMES)) if tags >> i & 1]
    if tags & T_BUDGET:
        return "budget"
    if tags & T_SEARCHCONV:
        return "search-convention"
    if tags & T_RUNOUT_TOP:
        return "limit2:runout-top" + ("+" + "+".join(x for x in names if x != "runout-top") if len(names) > 1 else "")
    if undet >= 2 and not tags & (T_RUNOUT_BODY | T_MAYBE | T_BOTH | T_UNKGROUP | T_SKIPCUT):
        return "limit1:independent-decisions"
    return "+".join(names) or "no-tag"


# --------------------------------------------------------------------------------------------- rows

NAMED = [
    # (id, op, pattern, subject) - section 2's rows the model can parse, and round 3's.
    ("B1", "match", r"a\m", "a"), ("B2", "match", r" \M", " "), ("B3", "fullmatch", r"a\B", "a"),
    ("B6", "match", r"a\b(?<=b)", "a"), ("C1", "match", r"aa\B", "aa"),
    ("C2", "fullmatch", r"(?!(True|False)\b)(.*)", "True"), ("C6", "match", r"(?r)\Ba", "a"),
    ("C7", "match", r"a(?=:a)", "a:"), ("C8", "fullmatch", r".*(?<=z)", "ab"),
    ("J1", "match", r"a\b\B", "a"), ("J2", "match", r"a\B$", "a"),
    ("L1", "search", r"(?=..)[ab]", "x"), ("L2", "match", r"(?=..)[ab]", "x"), ("L3", "match", r"(?=a.)b", "a"),
    ("L4", "match", r"(?(?=..)a|b)", "a"), ("L5", "match", r"(?(?=..)a|b)", "c"),
    ("L6", "match", r"(?!.a|x)x", "x"), ("L7", "match", r"(?!a(?:\B(?<=b)|))a", "a"),
    ("A4", "search", r"\ma", "b "), ("A5", "match", r"(?r)[a1]\M", ""),
    ("E1", "match", r"a\Zb", "a"), ("E2", "match", "a$\\n", "a"), ("E3", "match", "a$\n\\b", "a\n"),
    ("P1", "search", r"(?r)\b$", ""), ("P2", "match", r"(?r)\b$", ""), ("P3", "search", r"\b", ""),
    ("S1", "search", r"(?<=y)a", "x"), ("S2", "search", r"(?m)^a", "x"),
    ("V1", "match", r"(?:a\b(*PRUNE)(?<=b)|a.)", "a"), ("V2", "match", r"(?:a\b(*SKIP)(?<=b)|a.)", "a"),
    ("V3", "search", r"a\b(*SKIP)(?<=b)|a.", "a"), ("V4", "match", r"(?:a(*PRUNE)\b(?<=b)|a.)", "a"),
    ("D1", "match", r"(?>x\b|)(?<!x)x", "x"), ("D2", "match", r"(?>ab\b|a)(?<=a)b", "ab"),
    ("D3", "match", r"(?:a\b)?+(?<!a)a", "a"), ("D4", "match", r"(?>a\Z|)(?<!a)a", "a"),
    ("R1", "match", r"(?r)(?<=b)\Aa", "a"), ("R2", "match", r"(?r)(?<=b)^a", "a"),
    ("R3", "match", "(?rm)(?<=\\n)^a", "a"),
    ("N1", "match", r"(?>x\b|x|)(?<!x)x", "x"), ("N2", "match", r"(?>a\Z|a|)(?<!a)a", "a"),
    ("N3", "match", r"(?:a\Z(?<=b)|a(*PRUNE)(?<=b)|a)", "a"), ("N4", "match", r"a\b(?:(*PRUNE)(?<=b)|)", "a"),
    ("N5", "match", r"(?>a\b(?:|()))(?(1)|(?<=b))", "a"), ("N6", "match", r"(?=a\b(?:|()))(?(1)|(?<=b))", "a"),
    ("N7", "match", r"b(?!(?!\w.))", "b"), ("N8", "match", r".(?!(?>\wa|(?<!b))(?<!a))", " b"),
    # Round 2's misses (the brief), and round 3's own witnesses.
    ("Q1", "match", r"(?!(?>a\b|)(?<!a)a(*PRUNE)(?<=b)|a)", "a"),
    ("Q2", "match", r"(?=a(.))(?(1)a|b)", "a"),
    ("Q3", "match", r"(?!a.(*PRUNE)b|a)a", "a"),
    ("T1", "match", r"(?:(?>a\B|)(?<!a)a(*PRUNE)(?<=b)|a\B)", "a"),
    ("T2", "match", r"(?!a\B(*PRUNE)(?<=b)|a)", "a"),
    ("T3", "match", r"(?=a(.))\1", "a"),
    ("T4", "match", r"(?(?=a\b)(?<=b)|a)", "a"),
    ("T5", "match", r"(?(?!a\b)a|(?<=b))", "a"),
    ("T6", "match", r"(?:(?(?=a\B(*PRUNE)(?<=b))x|a\B))", "a"),
    ("T7", "search", r"(?:a\b(*SKIP)(?<=b)|a\B)", "aa"),
    ("T8", "match", r"(?:(?=a\B|)(?<!a)a(*PRUNE)(?<=b)|a\B)", "a"),
]


def check_row(op, pattern, subject, judge, guards="on", variant="design"):
    import regex
    truth, witness = judge.judge(op, pattern, subject)
    got = design(pattern, op, subject, guards, variant)
    kind = classify(truth, got, bool(regex.compile(pattern).flags & regex.REVERSE))
    note = ""
    if kind in ("miss", "span-miss", "semantics") and witness is not None:
        text = witness + subject if regex.compile(pattern).flags & regex.REVERSE else subject + witness
        if not concrete_agrees(pattern, op if op != "search" else "search", text):
            kind, note = "semantics", f"model's concrete matcher differs from upstream on {ascii(text)}"
    return truth, witness, got, kind, note


def cmd_rows(args):
    judge = load_judge()
    variant = args[0] if args else "design"
    tally = {}
    for rid, op, pattern, subject in NAMED:
        truth, witness, got, kind, note = check_row(op, pattern, subject, judge, variant=variant)
        tally[kind] = tally.get(kind, 0) + 1
        cls = phantom_class(got) if kind in ("phantom", "span-phantom") else ""
        print(f"{rid:3} {op:9} {ascii(pattern):44} {ascii(subject):8} truth={show(truth):8} model={show(got):8} {kind} {cls} {note}")
    print(tally)


# --------------------------------------------------------------------------------------------- grid

ATOMS = ["a", "b", ".", "[ab]", r"\w", r"\W", " ", r"\n"]
PREDS = [r"\b", r"\B", r"\m", r"\M", "$", r"\Z", "^", r"\A"]
QUANTS = ["*", "+", "?", "*+", "?+", "++", "*?", "{1,2}"]
VERBS = ["(*PRUNE)", "(*SKIP)", "(*FAIL)"]


def gen_pattern(rng, depth, ctx):
    """One random item list; ctx counts the groups opened so far."""
    items = []
    for _ in range(rng.randint(1, 3)):
        r = rng.random()
        if depth > 0 and r < 0.45:
            kind = rng.choice(["cap", "alt", "atomic", "la", "nla", "lb", "nlb", "condg", "condl", "condnl", "rep"])
            x = gen_pattern(rng, depth - 1, ctx)
            y = gen_pattern(rng, depth - 1, ctx)
            if kind == "cap":
                ctx["groups"] += 1
                items.append(f"({x})")
            elif kind == "alt":
                items.append(f"(?:{x}|{y})")
            elif kind == "atomic":
                items.append(f"(?>{x}|{y})")
            elif kind == "la":
                items.append(f"(?={x})")
            elif kind == "nla":
                items.append(f"(?!{x})")
            elif kind == "lb":
                items.append(f"(?<={x})")
            elif kind == "nlb":
                items.append(f"(?<!{x})")
            elif kind == "condg":
                if ctx["groups"] == 0:
                    ctx["groups"] += 1
                    items.append(f"({x})")
                else:
                    items.append(f"(?({rng.randint(1, ctx['groups'])}){x}|{y})")
            elif kind == "condl":
                z = gen_pattern(rng, depth - 1, ctx)
                items.append(f"(?(?={z}){x}|{y})")
            elif kind == "condnl":
                z = gen_pattern(rng, depth - 1, ctx)
                items.append(f"(?(?!{z}){x}|{y})")
            else:
                items.append(f"(?:{x}){rng.choice(QUANTS)}")
        elif r < 0.62:
            items.append(rng.choice(PREDS))
        elif r < 0.72:
            items.append(rng.choice(VERBS))
        elif r < 0.77 and ctx["groups"] > 0:
            items.append(f"\\{rng.randint(1, ctx['groups'])}")
        else:
            a = rng.choice(ATOMS)
            if rng.random() < 0.2:
                a += rng.choice(QUANTS)
            items.append(a)
    return "".join(items)


SUBJ_ALPHA = ["a", "b", " ", "\n"]


def grid_rows(n, seed):
    import regex
    rng = random.Random(seed)
    rows = []
    while len(rows) < n:
        ctx = {"groups": 0}
        body = gen_pattern(rng, 2, ctx)
        flags = rng.choice(["", "", "", "(?r)", "(?m)", "(?rm)"])
        pattern = flags + body
        try:
            regex.compile(pattern)
            Parser(pattern)
        except Exception:  # noqa: BLE001 - an uncompilable random pattern is skipped
            continue
        for _ in range(4):
            length = rng.randint(0, 3)
            subject = "".join(rng.choice(SUBJ_ALPHA) for _ in range(length))
            rows.append((rng.choice(["match", "search", "fullmatch"]), pattern, subject))
    return rows[:n]


def tree_features(node, ctx=(), out=None):
    """Constructs present in the pattern, with the contexts the brief asks about."""
    if out is None:
        out = set()
    kind = node[0]
    inside = set(ctx)
    if kind == "verb":
        out.add("verb:" + node[1])
        if node[1] != "FAIL":
            for c in ("rep", "cond", "look", "atomic"):
                if c in inside:
                    out.add(f"verb-in-{c}")
    elif kind == "atomic":
        out.add("atomic")
        if "atomic" in inside:
            out.add("atomic-in-atomic")
        if "look" in inside:
            out.add("atomic-in-look")
        tree_features(node[1], ctx + ("atomic",), out)
    elif kind == "look":
        out.add(("neg-" if node[2] else "") + ("lookbehind" if node[1] else "lookahead"))
        if "look" in inside:
            out.add("look-in-look")
        if "atomic" in inside:
            out.add("look-in-atomic")
        tree_features(node[3], ctx + ("look",), out)
    elif kind == "cond":
        if node[1][0] == "group":
            out.add("group-cond")
        else:
            out.add("look-cond" + ("-neg" if node[1][2] else ""))
            tree_features(node[1][3], ctx + ("cond", "look"), out)
        tree_features(node[2], ctx + ("cond",), out)
        tree_features(node[3], ctx + ("cond",), out)
    elif kind == "rep":
        out.add("possessive" if node[4] == "possessive" else "repeat")
        tree_features(node[1], ctx + ("rep",) + (("atomic",) if node[4] == "possessive" else ()), out)
    elif kind in ("seq", "alt"):
        for x in node[1]:
            tree_features(x, ctx, out)
    elif kind == "group":
        out.add("capture")
        tree_features(node[2], ctx, out)
    elif kind == "ref":
        out.add("backref")
    elif kind == "pred":
        out.add("pred:" + node[1])
    return out


PIECES = ["a", r"a\b", r"a\B", ".", r"\b", r"\B", "(?<=b)", "(?<!a)", "(?<=a)", "", "a.", r"(?>a\b|)",
          r"(?=a\B|)", r"\m", "$", r"(?>a|a\B)", r"a\b|", "[ab]*", r"(?!a\b)", "()", r"\1", "(a)"]
SHAPES = [
    "(?:(?>{0}|{1}){2}(*{v}){3}|{4})",       # a later cut or verb after a skipped cut (the taint)
    "(?{la}{0}(*{v}){1}|{2}){3}",             # a verb in a lookaround body, the edge before or after it
    "(?{la}(?>{0}|{1}){2}(*{v}){3}|{4})",
    "(?{la}{0}(?>{1}|{2}){3}){4}",            # a run-out inside an atomic inside a body
    "(?:{0}|)(?{la}[ab]*{1}){2}",             # one lookaround reached from two starts (guards)
    "(?:(?(?{la}{0}(*{v}){1}){2}|{3}))",      # verbs in a condition test
    "(?:(?(?{la}{0}){1}|{2})){q}{3}",         # a condition in a repeat
    "(?:{0}(*{v}){1}|{2}){q}{3}",             # a verb in a repeat
    "(?>(?>{0}|{1}){2}|{3}){4}",              # nested atomics
    "(?{la}(?{la2}{0}|{1}){2}|{3}){4}",       # nested lookarounds
]
SHAPE_SUBJECTS = ["a", "aa", "b", "ab", "ba", " a", "", "a ", "aab", "\n", "a\n"]


def shape_rows(n, seed):
    import regex
    rng = random.Random(seed)
    rows = []
    while len(rows) < n:
        shape = rng.choice(SHAPES)
        fill = [rng.choice(PIECES) for _ in range(5)]
        pattern = shape.format(*fill, v=rng.choice(["PRUNE", "SKIP"]), la=rng.choice(["=", "!", "<=", "<!"]),
                               la2=rng.choice(["=", "!"]), q=rng.choice(["*", "+", "?", "*+"]))
        pattern = rng.choice(["", "", "", "(?r)", "(?m)"]) + pattern
        try:
            regex.compile(pattern)
            Parser(pattern)
        except Exception:  # noqa: BLE001 - an uncompilable pattern is skipped
            continue
        for _ in range(3):
            rows.append((rng.choice(["match", "match", "search", "fullmatch"]), pattern, rng.choice(SHAPE_SUBJECTS)))
    return rows[:n]


def features(pattern, subject):
    parsed = Parser(pattern)
    f = tree_features(parsed.tree)
    if parsed.reverse:
        f.add("flag:r")
    if parsed.multiline:
        f.add("flag:m")
    if "\\n" in pattern or "\n" in pattern or "\n" in subject:
        f.add("newline")
    return f


VARIANTS = ("plain-count", "no-runout-taint", "runout-taint-all")


def grid_worker(chunk):
    import regex
    judge = load_judge()
    out = []
    rng = random.Random(hash(chunk[0]) & 0xffff)
    for op, pattern, subject in chunk:
        rec = {"op": op, "pattern": pattern, "subject": subject}
        try:
            reverse = bool(regex.compile(pattern).flags & regex.REVERSE)
            # Semantics check of the model's concrete matcher: the held text and 24 sampled t + w.
            texts = [subject] + [(lambda w: w + subject if reverse else subject + w)(
                "".join(rng.choice("ab \n1:") for _ in range(rng.randint(1, 3)))) for _ in range(24)]
            if not all(concrete_agrees(pattern, op, t) for t in texts):
                # Judged anyway, and reported apart: the model's answer here rests on semantics
                # that differ from upstream's.
                truth, _ = judge.judge(op, pattern, subject)
                got = design(pattern, op, subject)
                rec.update(kind="semantics", raw=classify(truth, got, reverse), truth=show(truth), got=show(got))
                out.append(rec)
                continue
            stats = {}
            truth, witness = judge.judge(op, pattern, subject)
            got = design(pattern, op, subject, stats=stats)
            kind = classify(truth, got, reverse)
            if kind in ("miss", "span-miss") and witness is not None:
                text = witness + subject if reverse else subject + witness
                if not concrete_agrees(pattern, op, text):
                    kind = "semantics"
            rec.update(kind=kind, truth=show(truth), got=show(got), witness=witness,
                       events={k: v for k, v in stats.items() if k != "steps"})
            # Ablations: each part of the design removed in turn, and the repeat guards off.
            for variant in VARIANTS:
                try:
                    alt = design(pattern, op, subject, variant=variant)
                    rec["ablate:" + variant] = classify(truth, alt, reverse)
                except Budget:
                    rec["ablate:" + variant] = "budget"
            try:
                rec["guards-off-same"] = show(design(pattern, op, subject, guards="off")) == show(got)
            except Budget:
                rec["guards-off-same"] = "budget"
            if kind in ("phantom", "span-phantom"):
                rec["class"] = phantom_class(got)
            if got is not None and got[0] == "P":
                rec["gotclass"] = phantom_class(got)
        except Budget:
            rec["kind"] = "budget"
        except RecursionError:
            rec["kind"] = "recursion"
        out.append(rec)
    return out


def cmd_grid(args):
    from multiprocessing import Pool
    n, seed = int(args[0]), int(args[1])
    jobs = int(args[args.index("--jobs") + 1]) if "--jobs" in args else 20
    out_path = args[args.index("--out") + 1] if "--out" in args else None
    rows = shape_rows(n, seed) if "--shapes" in args else grid_rows(n, seed)
    chunks = [rows[i:i + 25] for i in range(0, len(rows), 25)]
    t0 = time.time()
    results = []
    with Pool(jobs) as pool:
        for part in pool.imap_unordered(grid_worker, chunks):
            results.extend(part)
    tally, classes, feat_tab, ablate, guards_diff = {}, {}, {}, {}, 0
    for r in results:
        tally[r["kind"]] = tally.get(r["kind"], 0) + 1
        if "class" in r:
            classes[r["class"]] = classes.get(r["class"], 0) + 1
        for key, v in r.items():
            if key.startswith("ablate:"):
                cell = ablate.setdefault(key[7:], {})
                cell[v] = cell.get(v, 0) + 1
        if r.get("guards-off-same") is False:
            guards_diff += 1
        second = bool(r.get("events"))
        for feat in features(r["pattern"], r["subject"]):
            cell = feat_tab.setdefault(feat, {"rows": 0, "second-pass-fired": 0, "miss": 0, "phantom": 0, "semantics": 0})
            cell["rows"] += 1
            cell["second-pass-fired"] += second
            if r["kind"] in ("miss", "span-miss"):
                cell["miss"] += 1
            elif r["kind"] in ("phantom", "span-phantom"):
                cell["phantom"] += 1
            elif r["kind"] == "semantics":
                cell["semantics"] += 1
        for ev in r.get("events", {}):
            cell = feat_tab.setdefault("event:" + ev, {"rows": 0})
            cell["rows"] += 1
    print(f"rows {len(results)} in {time.time() - t0:.0f}s: {tally}")
    print("ablations (design with one part removed):", ablate)
    print("guards on vs off, rows whose answer differs:", guards_diff)
    print("phantom classes:", json.dumps(dict(sorted(classes.items(), key=lambda kv: -kv[1])), indent=1))
    for f in sorted(feat_tab):
        print(f"  {f:22}", feat_tab[f])
    for r in results:
        if r["kind"] in ("miss", "span-miss"):
            print("MISS", r)
    for variant in VARIANTS:
        shown = 0
        for r in results:
            if r.get("ablate:" + variant) in ("miss", "span-miss") and shown < 3:
                print(f"witness for {variant}:", r["op"], ascii(r["pattern"]), ascii(r["subject"]), r["truth"])
                shown += 1
    if out_path:
        with open(out_path, "w", encoding="utf-8", newline="\n") as f:
            for r in results:
                f.write(json.dumps(r) + "\n")


# --------------------------------------------------------------------------------------------- perf

PERF = [
    (r"(?:(?=.*z)a)*b", lambda n: "a" * n + "c"),
    (r"(?:(?=.*z|)a)*b", lambda n: "a" * n + "c"),
    (r"(?>a*)*b", lambda n: "a" * n + "c"),
    (r"(?>a*)*b", lambda n: "a" * n),
    (r"(?:(?>a|ab))*c", lambda n: "ab" * n),
    (r"(?:(?>a(?=.*z)|ab))*c", lambda n: "ab" * n),
    (r"(?:(?>a\b|a))*c", lambda n: "a" * n),
    (r"(?:(?!.*z)a)*b", lambda n: "a" * n + "c"),
    (r"(?:(?(?=.*z)a|a))*b", lambda n: "a" * n + "c"),
    (r"(?=(?:a*)*b)", lambda n: "a" * n + "c"),
    (r"(?=(?:a|a\b)*b)", lambda n: "a" * n),
    (r"(a)(?:(?=.*z|)a)*b\1", lambda n: "a" * n + "c"),
    (r"(?:(?=.*z|)a)*b", lambda n: "a" * n),
    (r"(?:(?=(.*)z|())a)*b\1\2", lambda n: "a" * n + "c"),
]


def cmd_perf(args):
    """Steps (node visits) of the first pass and of the second pass, and wall time of both."""
    ns = [int(x) for x in args] if args else [10, 20, 100, 1000]
    for guards in ("on", "untouched", "off"):
        for pattern, subj in PERF:
            cells = []
            for n in ns:
                parsed = Parser(pattern)
                mark_guardable(parsed.tree, False, pattern_has_ref(pattern))
                first, second = {}, {}
                t0 = time.perf_counter()
                try:
                    r = run_op(parsed, "match", subj(n), False, guards, 5_000_000, "design", first)
                    if r is None:
                        r = run_op(parsed, "match", subj(n), True, guards, 5_000_000, "design", second)
                    res = show(r)
                except Budget:
                    res = "BUDGET"
                dt = (time.perf_counter() - t0) * 1000
                forks = sum(second.get(e, 0) for e in FORK_EVENTS)
                cells.append(f"n={n}: {res} {first.get('steps', 0)}+{second.get('steps', 0)} forks={forks} {dt:.0f}ms")
                if res == "BUDGET":
                    break
            print(f"guards={guards:9} {pattern:30} " + " | ".join(cells), flush=True)


FORK_EVENTS = ("cut-skipped", "cond-maybe", "cond-unknown-group", "neg-maybe", "verb-skipped")


def budget_worker(chunk):
    out = []
    for rec in chunk:
        parsed = Parser(rec["pattern"])
        mark_guardable(parsed.tree, False, pattern_has_ref(rec["pattern"]))
        first, second = {}, {}
        try:
            r = run_op(parsed, rec["op"], rec["subject"], False, "on", 2_000_000, "design", first)
            if r is None:
                run_op(parsed, rec["op"], rec["subject"], True, "on", 2_000_000, "design", second)
        except Budget:
            second["steps"] = 10 ** 9
        forks = sum(second.get(e, 0) for e in FORK_EVENTS)
        out.append((first.get("steps", 0), second.get("steps", 0), forks, len(rec["subject"])))
    return out


def cmd_budget(args):
    """Replays grid rows (the --out files) through the model and counts how often a second-pass
    budget of K times the first pass plus F steps would fire."""
    from multiprocessing import Pool
    rows = []
    for path in args:
        with open(path, encoding="utf-8") as f:
            rows.extend(json.loads(line) for line in f)
    rows = [r for r in rows if r.get("kind") not in ("semantics", "budget", "recursion")]
    chunks = [rows[i:i + 50] for i in range(0, len(rows), 50)]
    with Pool(22) as pool:
        pairs = [p for part in pool.map(budget_worker, chunks) for p in part]
    second_rows = [(a, b) for a, b, _, _ in pairs if b > 0]
    forks = sorted(f / (n + 1) for _, b, f, n in pairs if b > 0)
    for q in (0.5, 0.99, 0.999, 1.0):
        print(f"  forks per (|t| + 1), quantile {q}: {forks[min(len(forks) - 1, int(q * len(forks)))]:.2f}")
    print(f"rows {len(pairs)}, with a second pass {len(second_rows)}")
    ratios = sorted(b / max(a, 1) for a, b in second_rows)
    for q in (0.5, 0.9, 0.99, 0.999, 1.0):
        print(f"  second/first steps, quantile {q}: {ratios[min(len(ratios) - 1, int(q * len(ratios)))]:.2f}")
    for k in (4, 16, 64):
        for floor in (1_000, 10_000):
            fired = sum(1 for a, b in second_rows if b > k * a + floor)
            print(f"  budget {k} x first + {floor}: fires on {fired} rows")


def main():
    cmd, args = sys.argv[1], sys.argv[2:]
    if cmd == "budget":
        cmd_budget(args)
        return
    if cmd == "row":
        judge = load_judge()
        op, pattern, subject = args[0], args[1], args[2].encode().decode("unicode_escape")
        variant = args[3] if len(args) > 3 else "design"
        truth, witness, got, kind, note = check_row(op, pattern, subject, judge, variant=variant)
        print(f"truth={show(truth)} witness={witness!r} model={show(got)} {kind} {note}",
              phantom_class(got) if got and got[0] == "P" else "")
    elif cmd == "rows":
        cmd_rows(args)
    elif cmd == "grid":
        cmd_grid(args)
    elif cmd == "perf":
        cmd_perf(args)


if __name__ == "__main__":
    result = {}
    t = threading.Thread(target=lambda: result.setdefault("r", main()))
    t.start()
    t.join()
