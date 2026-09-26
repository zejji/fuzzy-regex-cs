r"""A small reference matcher for fuzzy regex semantics (queue item 1, F-A).

Purpose: grade the port's fuzzy engine against a search that is complete and
ordered, so no path is skipped. It is slow on purpose: every choice is an
explicit branch of a depth-first generator search, and the first path that
reaches the end of the pattern wins (first match, not best match).

Usage as a library:
    ref = importlib.import_module(...)   # or runpy / importlib.util
    ref.search(pattern, text, pos=0)  -> Result or None
    ref.match(pattern, text, pos=0)   -> Result or None
    ref.fullmatch(pattern, text, pos=0) -> Result or None
Result has .span, .groups (spans, (-1, -1) when unset) and .fuzzy_counts
(substitutions, insertions, deletions), matching regex's match object.

Command line: python fuzzy-reference-matcher.py search "(?:cats|cat){e<=1}" cat

Subset: literals (letters, digits, backslash-escaped punctuation), '.',
classes [abc] [^a] [a-c] and \d, '|', (?:...) and (...), greedy and lazy ?,
*, +, {m}, {m,}, {m,n}, fuzzy constraints on any atom: {e<=n},
{i<=a,s<=b,d<=c}, {d}, {1<=e<=2}, exclusive '<', cost equations
{2i+2d+1s<=4}, the anchors ^ and $ (no MULTILINE), backreferences \1-\9
(matched item by item, so a fuzzy section can edit them), conditionals
(?(1)yes|no), and the verbs (*SKIP), (*PRUNE), (*FAIL) / (*F). No flags,
lookarounds, atomic groups or fuzzy tests ({s<=1:[a-z]}).

Order rules (citations are to upstream/src/_regex.c and upstream/README.rst
of mrab-regex 2026.9.10):

1. An item inside a fuzzy section is tried exactly first. Only when the exact
   test fails are the errors tried, in the order substitution, insertion,
   deletion: RE_FUZZY_SUB 0, RE_FUZZY_INS 1, RE_FUZZY_DEL 2 (_regex.c:177-179)
   and the loop `for (data.fuzzy_type = 0; ...; data.fuzzy_type++)` in
   fuzzy_match_item (_regex.c:10216-10226); retry_fuzzy_match_item resumes at
   the next type (_regex.c:10300).
2. Substitution consumes one text character and moves to the next item;
   insertion consumes one text character and retries the SAME item; deletion
   consumes nothing and moves to the next item (next_fuzzy_match_item,
   _regex.c:10126-10178). A deletion is "a pattern item absent from the text"
   (README.rst:538-566).
3. THE REFERENCE RULE THAT UPSTREAM BREAKS: a deletion is also offered after
   an item matched exactly, tried when everything after the exact match has
   failed and before any earlier choice is retried. README.rst:609:
   fullmatch("(?:cats|cat){e<=1}", "cat") has fuzzy_counts (0, 0, 1), branch
   1 with 's' deleted, found before branch 2. Upstream pushes no retry after
   an exact success (fuzzy-findings F-A), so it sometimes skips this path.
4. At the end of a section, 0 trailing insertions are tried first, then 1, 2,
   ... on backtracking (the RE_OP_END_FUZZY backtrack handler,
   _regex.c:15488-15560).
5. When searching, an item may not take an insertion at the position where
   the search began (search_anchor, set once in init_match, _regex.c:3410;
   rule at _regex.c:10211-10214). The section-end insertion (rule 4) has no
   such check. match() and fullmatch() have no such restriction.
6. Error limits are checked before each error (this_error_permitted,
   _regex.c:9676-9690); minimums at the section end (fuzzy_within_constraints,
   _regex.c:9709-9745). Here minimums are checked after the trailing
   insertions of rule 4, so a trailing insertion can meet a minimum.
7. Constraint defaults: if any of i, s, d has its own limit, the unnamed
   types are not permitted (README.rst:565, "{i<=2,d<=2,e<=3} ... but no
   substitutions"); a cost equation permits only the types it names
   (measured 2026-09-26: fullmatch "(?:ab){1s<=1}" "b" is None).
8. Normal backtracking: the most recent choice point is retried first;
   alternation left to right; greedy repeats try one more iteration first,
   lazy ones one fewer. An iteration that matches empty text is accepted and
   ends the loop once the minimum count is reached (Perl rule; iterations
   below the minimum continue). With fuzzy matching this matters when
   deletions empty a repeat body; see EMPTY_DELETION_ITERATIONS, whose
   "needed" and "unrestricted" modes also follow upstream in treating an
   error-free empty iteration that changed a referenced group as progress
   (_regex.c:12552).
9. (*SKIP): when backtracking reaches it, the attempt ends and the next one
   starts where the verb was executed (README.rst:209; perlre v5.42), or one
   character later if that is not past the current start. (*PRUNE) ends the
   attempt and the next starts one character later. (*FAIL) fails.
"""

import re
import sys
from dataclasses import dataclass, replace

INF = float("inf")
# Rule 3 switch. False gives upstream's behaviour (no deletion after an exact
# match), which lets a grader tell a skipped path from any other difference.
DELETE_AFTER_EXACT = True
# Rule 8 switch for a repeat iteration that matched empty text only by taking
# deletions. "perl" (default): the literal "(?:x|)" reading, the same rule as
# any empty iteration (Perl 5.42: "(?:c|(?{$n++}))+" over "b" runs the empty
# branch once). "reject": such an iteration fails; a grader uses it to tell
# this class of difference apart. Upstream follows neither (measured
# 2026-09-26): match "(?:b*){d<=2}" "bba" -> (0, 0, 2), two empty iterations,
# but match "(?:b*){d<=2}" "bb" -> (0, 0, 0).
# "minimum": such an iteration fails once the repeat's minimum is met (rule B
# of docs/plan/2026-09-26-empty-iteration-survey.md; it loses matches).
# "needed": such an iteration is allowed only if (a) the repeat is below its
# minimum, or (b) its errors advance a minimum that is still unmet in an open
# fuzzy section: a d minimum by deleting, an e minimum by any error (an empty
# iteration consumes no text, so it can only delete and never advances an s or
# i minimum), or (c) it changed the span of a group that a backreference or
# conditional tests (upstream's progress rule, _regex.c:12726, without the
# fuzzy edits that upstream also counts, :10487); otherwise it fails. Each (b)
# iteration raises a count toward a finite minimum, so (b) allows at most the
# sum of the minimums. An error-free empty iteration follows upstream: a
# referenced group's span change is progress and the loop goes on, otherwise
# the iteration is accepted and the loop ends. A capture state already seen at
# this position in the current run of empty iterations is not a change, so (c)
# terminates too.
# "unrestricted": such an iteration may always go round again (error-free ones
# as in "needed"); only the error budget stops it, so use it with finite limits.
# It is the oracle the "needed" sweeps compare against.
EMPTY_DELETION_ITERATIONS = "perl"
# "unrestricted" only: the most empty iterations in a row at one position, for
# patterns whose budget does not bound them (a fuzzy section inside the
# repeat body restarts its counts each iteration). None = no cap.
UNRESTRICTED_EMPTY_RUN = None
# "needed" only: also prune an iteration that consumed text when an earlier
# path of the same repeat invocation reached the same state (a memo of the
# repeat; exact, like the empty-iteration check). Part of the recommended rule;
# False leaves only the empty-iteration check, for measuring what each adds.
NEEDED_DEDUP_ALL = True
TYPES = ("s", "i", "d")


# ---------------------------------------------------------------- the tree


@dataclass(frozen=True)
class Item:  # one text character: literal, '.', or class
    test: object
    text: str


@dataclass(frozen=True)
class Seq:
    parts: tuple


@dataclass(frozen=True)
class Alt:
    branches: tuple


@dataclass(frozen=True)
class Group:
    index: int  # 0 = non-capturing
    body: object


@dataclass(frozen=True)
class Repeat:
    body: object
    lo: int
    hi: float
    greedy: bool


@dataclass(frozen=True)
class Anchor:
    kind: str  # ^ or $


@dataclass(frozen=True)
class Backref:
    index: int


@dataclass(frozen=True)
class Cond:
    index: int
    yes: object
    no: object


@dataclass(frozen=True)
class Verb:
    name: str  # SKIP, PRUNE, FAIL


@dataclass(frozen=True)
class Limits:
    mins: tuple  # per type s, i, d, then e
    maxs: tuple
    costs: tuple  # s, i, d
    max_cost: float


@dataclass(frozen=True)
class Fuzzy:
    body: object
    limits: Limits


# ---------------------------------------------------------------- the parser


class Parser:
    def __init__(self, pattern):
        self.p, self.i, self.groups = pattern, 0, 0
        self.referenced = set()  # groups a backreference or conditional tests

    def peek(self, n=1):
        return self.p[self.i : self.i + n]

    def take(self, s):
        if not self.p.startswith(s, self.i):
            raise ValueError(f"expected {s!r} at {self.i} in {self.p!r}")
        self.i += len(s)

    def parse(self):
        node = self.alternation()
        if self.i != len(self.p):
            raise ValueError(f"unexpected {self.peek()!r} at {self.i}")
        return node

    def alternation(self):
        branches = [self.sequence()]
        while self.peek() == "|":
            self.take("|")
            branches.append(self.sequence())
        return branches[0] if len(branches) == 1 else Alt(tuple(branches))

    def sequence(self):
        parts = []
        while self.peek() and self.peek() not in "|)":
            parts.append(self.quantified())
        return Seq(tuple(parts))

    def quantified(self):
        node = self.atom()
        while True:
            c = self.peek()
            if c in ("?", "*", "+"):
                self.take(c)
                lo, hi = {"?": (0, 1), "*": (0, INF), "+": (1, INF)}[c]
            elif c == "{" and self.brace_is_repeat():
                lo, hi = self.repeat_bounds()
            elif c == "{" and self.brace_is_constraint():
                node = self.fuzzy(node, self.limits())
                continue
            else:
                return node
            greedy = self.peek() != "?"
            if not greedy:
                self.take("?")
            node = Repeat(node, lo, hi, greedy)

    @staticmethod
    def fuzzy(node, limits):
        """A capture group keeps its errors inside: (dog){e<=1} captures " dog"
        with the inserted space (README.rst:598), and trailing insertions are
        captured too (measured 2026-09-26: fullmatch "(b){i<=1}" "ba" -> group
        (0, 2)). So the section goes inside the group."""
        if isinstance(node, Group) and node.index:
            return Group(node.index, Fuzzy(node.body, limits))
        return Fuzzy(node, limits)

    def brace_is_repeat(self):
        end = self.p.index("}", self.i)
        return all(ch.isdigit() or ch == "," for ch in self.p[self.i + 1 : end])

    def repeat_bounds(self):
        end = self.p.index("}", self.i)
        body = self.p[self.i + 1 : end]
        self.i = end + 1
        lo, _, hi = body.partition(",")
        if "," not in body:
            return int(lo), int(lo)
        return int(lo or 0), (int(hi) if hi else INF)

    def brace_is_constraint(self):
        """Upstream's grammar (_regex_core.py:680-745): a term led by a digit must have a
        minimum AND a maximum ("1<=e<=2"); "{1<=d}" is not a constraint at all, so the braces
        are literal text (measured 2026-09-26: fullmatch "(?:b){1<=d}" "b{1<=d}" matches)."""
        end = self.p.find("}", self.i)
        if end < 0:
            return False
        for term in (t.strip() for t in self.p[self.i + 1 : end].split(",")):
            if re.search(r"\d[sid]", term):  # a cost equation
                continue
            if not term or not term[0].isdigit():
                continue
            m = re.fullmatch(r"\d+<=?[sied]<=?\d+", term)
            if not m:
                return False
        return True

    def limits(self):
        end = self.p.index("}", self.i)
        terms = self.p[self.i + 1 : end].split(",")
        self.i = end + 1
        return parse_limits(terms)

    def atom(self):
        c = self.peek()
        if self.peek(2) == "(*":
            end = self.p.index(")", self.i)
            name = self.p[self.i + 2 : end]
            self.i = end + 1
            return Verb({"F": "FAIL"}.get(name, name))
        if self.peek(3) == "(?(":
            self.take("(?(")
            end = self.p.index(")", self.i)
            index = int(self.p[self.i : end])
            self.i = end + 1
            yes, no = self.sequence(), Seq(())
            if self.peek() == "|":
                self.take("|")
                no = self.sequence()
            self.take(")")
            self.referenced.add(index)
            return Cond(index, yes, no)
        if c in "^$":
            self.take(c)
            return Anchor(c)
        if c == "(":
            index = 0
            if self.peek(3) == "(?:":
                self.take("(?:")
            elif self.peek(2) == "(?":  # lookarounds, flags, named groups, atomic groups ...
                raise ValueError(f"unsupported construct {self.p[self.i:self.i + 4]!r} at {self.i} in {self.p!r}")
            else:
                self.take("(")
                self.groups += 1
                index = self.groups
            body = self.alternation()
            self.take(")")
            return Group(index, body)
        if c == "[":
            return self.char_class()
        if c == ".":
            self.take(".")
            return Item(lambda ch: ch != "\n", ".")
        if c == "\\":
            self.i += 2
            ch = self.p[self.i - 1]
            if ch in "123456789":
                self.referenced.add(int(ch))
                return Backref(int(ch))
            if ch == "d":
                return Item(lambda x: x.isdigit(), "\\d")
            return Item(lambda x, ch=ch: x == ch, ch)
        self.i += 1
        return Item(lambda x, c=c: x == c, c)

    def char_class(self):
        end = self.p.index("]", self.i + 1)
        body = self.p[self.i + 1 : end]
        self.i = end + 1
        negate = body.startswith("^")
        body = body[1:] if negate else body
        chars = set()
        j = 0
        while j < len(body):
            if j + 2 < len(body) and body[j + 1] == "-":
                chars.update(chr(k) for k in range(ord(body[j]), ord(body[j + 2]) + 1))
                j += 3
            else:
                chars.add(body[j])
                j += 1
        return Item(lambda x: (x in chars) != negate, "[" + "^" * negate + body + "]")


def parse_limits(terms):
    """Constraint terms -> Limits. See order rule 7 for the defaults."""
    mins = {t: 0 for t in "side"}
    maxs = {}
    costs, max_cost = {"s": 1, "i": 1, "d": 1}, INF
    for term in (t.strip() for t in terms):
        if re.search(r"\d[sid]", term):  # a cost equation, e.g. 2i+2d+1s<=4
            costs, max_cost = parse_cost(term)
            for t in TYPES:
                maxs.setdefault(t, INF if costs[t] < INF else 0)
            continue
        lo, kind, hi = split_bounds(term)
        mins[kind], maxs[kind] = lo, hi
    named = [t for t in TYPES if t in maxs]
    for t in TYPES:
        maxs.setdefault(t, 0 if named else INF)
    maxs.setdefault("e", INF)
    return Limits(
        tuple(mins[t] for t in "side"),
        tuple(maxs[t] for t in "side"),
        tuple(costs[t] if costs[t] < INF else 0 for t in TYPES),
        max_cost,
    )


def split_bounds(term):
    """'1<=e<=2', 'e<3', 'd', '0<e' -> (min, kind, max)."""
    lo, hi = 0, INF
    kind = next(ch for ch in term if ch in "sied")
    left, right = term.split(kind, 1)
    if left:
        n = int(left.rstrip("<="))
        lo = n if left.endswith("<=") else n + 1
    if right:
        n = int(right.lstrip("<="))
        hi = n if right.startswith("<=") else n - 1
    return lo, kind, hi


def parse_cost(term):
    """'2i+2d+1s<=4' -> ({type: cost}, max). Unnamed types are not permitted."""
    lhs, op, rhs = term.partition("<=") if "<=" in term else term.partition("<")
    max_cost = int(rhs) if op == "<=" else int(rhs) - 1
    costs = {t: INF for t in TYPES}
    for part in lhs.split("+"):
        costs[part[-1]] = int(part[:-1] or 1)
    return costs, max_cost


# ---------------------------------------------------------------- the search


@dataclass(frozen=True)
class State:
    pos: int
    groups: tuple  # spans, index 0 unused
    totals: tuple  # s, i, d of finished sections
    counts: tuple  # s, i, d of the open section, or None outside fuzzy
    limits: object  # Limits of the open section, or None
    outer: tuple  # saved (counts, limits) of enclosing sections
    anchor: int  # search start (rule 5), or -1 for match/fullmatch
    path: tuple = ()  # the errors taken: (kind, text position, item)


class Prune(Exception):
    """Backtracking reached (*PRUNE) or (*SKIP): end this attempt."""

    def __init__(self, restart):
        self.restart = restart  # None = next character, else SKIP position


class Ctx:
    def __init__(self, text, referenced=frozenset()):
        self.text = text
        self.referenced = referenced


def run(node, st, ctx, k):
    """Yield every final state reachable from st, in search order."""
    kind = type(node)
    if kind is Item:
        return item(node, st, ctx, k)
    if kind is Seq:
        return seq(node.parts, st, ctx, k)
    if kind is Alt:
        return alt(node, st, ctx, k)
    if kind is Group:
        return group(node, st, ctx, k)
    if kind is Repeat:
        return repeat(node, 0, st, ctx, k)
    if kind is Fuzzy:
        return fuzzy(node, st, ctx, k)
    if kind is Anchor:
        return anchor(node, st, ctx, k)
    if kind is Backref:
        return backref(node, st, ctx, k)
    if kind is Cond:
        return run(node.yes if st.groups[node.index] != (-1, -1) else node.no, st, ctx, k)
    return verb(node, st, ctx, k)


def anchor(node, st, ctx, k):
    n = len(ctx.text)
    if node.kind == "^":
        ok = st.pos == 0
    else:  # $: the end, or before a final newline
        ok = st.pos == n or (st.pos == n - 1 and ctx.text[-1] == "\n")
    return k(st) if ok else iter(())


def backref(node, st, ctx, k):
    start, end = st.groups[node.index]
    if start < 0:  # a reference to an unset group fails
        return iter(())
    items = tuple(Item(lambda x, ch=ch: x == ch, ch) for ch in ctx.text[start:end])
    return seq(items, st, ctx, k)


def seq(parts, st, ctx, k):
    if not parts:
        return k(st)
    return run(parts[0], st, ctx, lambda s: seq(parts[1:], s, ctx, k))


def alt(node, st, ctx, k):
    for branch in node.branches:  # left to right (rule 8)
        yield from run(branch, st, ctx, k)


def group(node, st, ctx, k):
    if node.index == 0:
        return run(node.body, st, ctx, k)
    start = st.pos

    def close(s):
        g = list(s.groups)
        g[node.index] = (start, s.pos)
        return k(replace(s, groups=tuple(g)))

    return run(node.body, st, ctx, close)


def repeat(node, count, st, ctx, k, seen=(), reached=None):
    """seen: the capture states at the start of the empty iterations already
    run at this position (modes "needed" and "unrestricted"). reached: the
    states that error-spending empty iterations of this invocation of the
    repeat have already led to (mode "needed"); see empty_state_key."""
    mode = EMPTY_DELETION_ITERATIONS
    if reached is None:
        reached = set()

    def more(s):
        if s.pos != st.pos:
            if mode == "needed" and NEEDED_DEDUP_ALL:
                key = empty_state_key(node, count + 1, s, ctx, ())
                if key in reached:
                    return iter(())
                reached.add(key)
            return repeat(node, count + 1, s, ctx, k, (), reached)
        spent = s.path != st.path  # this iteration took errors
        if mode in ("needed", "unrestricted"):
            now_seen = seen + (st.groups,)
            # Upstream counts only a REFERENCED group whose span changed (_regex.c:12726).
            refs = sorted(ctx.referenced)
            changed = any(s.groups[g] != st.groups[g] for g in refs) and all(
                any(s.groups[g] != old[g] for g in refs) for old in now_seen)
            if not spent:  # upstream's rule for an error-free empty iteration
                if changed or count + 1 < node.lo:
                    return repeat(node, count + 1, s, ctx, k, now_seen, reached)
                return k(s)
            if mode == "unrestricted":
                if UNRESTRICTED_EMPTY_RUN is not None and len(now_seen) > UNRESTRICTED_EMPTY_RUN:
                    return iter(())
                return repeat(node, count + 1, s, ctx, k, now_seen)
            if not (changed or count < node.lo or advances_unmet_minimum(st, s)):
                return iter(())
            # Admitted. Prune it if an earlier path of this invocation already
            # reached the same state: the future is the same, and that path
            # was explored to the end first, so this one can add no match.
            key = empty_state_key(node, count + 1, s, ctx, now_seen)
            if key in reached:
                return iter(())
            reached.add(key)
            return repeat(node, count + 1, s, ctx, k, now_seen, reached)
        if spent and mode == "reject":
            return iter(())
        if spent and mode == "minimum" and count + 1 > node.lo:
            return iter(())
        if count + 1 >= node.lo:  # empty iteration ends the loop
            return k(s)
        return repeat(node, count + 1, s, ctx, k)

    can_more = count < node.hi
    can_stop = count >= node.lo
    if node.greedy:
        if can_more:
            yield from run(node.body, st, ctx, more)
        if can_stop:
            yield from k(st)
    else:
        if can_stop:
            yield from k(st)
        if can_more:
            yield from run(node.body, st, ctx, more)


def verb(node, st, ctx, k):
    if node.name == "FAIL":
        return
    yield from k(st)
    raise Prune(st.pos if node.name == "SKIP" else None)  # rule 9


def item(node, st, ctx, k):
    text, pos = ctx.text, st.pos
    exact = pos < len(text) and node.test(text[pos])
    if exact:
        yield from k(replace(st, pos=pos + 1))
    if st.counts is None:
        return
    if not exact:  # rule 1: substitution and insertion only on a mismatch
        if pos < len(text) and permitted(st, 0):
            yield from k(add_error(st, 0, pos + 1, node))
        if pos < len(text) and pos != st.anchor and permitted(st, 1):  # rule 5
            yield from item(node, add_error(st, 1, pos + 1, node), ctx, k)
    if (DELETE_AFTER_EXACT or not exact) and permitted(st, 2):  # rule 3
        yield from k(add_error(st, 2, pos, node))


def permitted(st, t):
    """this_error_permitted (_regex.c:9676): room for one more error of type t."""
    lim, c = st.limits, st.counts
    cost = sum(n * w for n, w in zip(c, lim.costs)) + lim.costs[t]
    return c[t] < lim.maxs[t] and sum(c) < lim.maxs[3] and cost <= lim.max_cost


def add_error(st, t, pos, node=None):
    c = list(st.counts)
    c[t] += 1
    step = ("sid"[t], st.pos, node.text if node else "<end>")
    return replace(st, counts=tuple(c), pos=pos, path=st.path + (step,))


def empty_state_key(node, count, st, ctx, seen):
    """Everything the rest of the match can depend on after an iteration of
    this repeat invocation (whose node, continuation, open sections' limits
    and search anchor are fixed): the text position; the count, clipped to
    what the repeat can still tell apart; the error counts of every open
    section; the spans of the groups the pattern tests; and the tested part
    of the (c) cycle guard. Untested groups and the totals of closed outermost
    sections change the reported result, never whether a match is found; and
    two paths with equal keys are never on one branch, because each admitted
    iteration raises the count, an error count or a tested span."""
    refs = sorted(ctx.referenced)
    clipped = min(count, node.lo) if node.hi == INF else count
    return (st.pos, clipped, st.counts, tuple(saved for saved, _ in st.outer),
            tuple(st.groups[g] for g in refs),
            frozenset(tuple(old[g] for g in refs) for old in seen))


def advances_unmet_minimum(before, after):
    """True if the errors taken between the two states raise a count that an
    open fuzzy section has a minimum for and has not yet reached: a per-type
    minimum by errors of that type, an e minimum by any error. An enclosing
    section counts the errors of the sections open inside it, as END_FUZZY adds
    the inner counts to the outer ones (_regex.c:12475-12481); a section closed
    inside the iteration has already added its errors to after.counts."""
    if before.counts is None:
        return False
    return unmet_minimum_advanced_by(before, tuple(a - b for a, b in zip(after.counts, before.counts)))


def unmet_minimum_advanced_by(st, delta):
    """True if errors counted as delta (s, i, d) would raise a count that an
    open section has a minimum for and has not yet reached, in state st."""
    if st.counts is None or not any(delta):
        return False
    counts, lim = st.counts, st.limits
    chain = [(counts, lim)]
    for saved, limits in reversed(st.outer):
        if saved is None:
            break
        counts = tuple(a + b for a, b in zip(saved, counts))
        chain.append((counts, limits))
    for counts, lim in chain:
        if any(delta[t] and counts[t] < lim.mins[t] for t in range(3)):
            return True
        if sum(counts) < lim.mins[3]:
            return True
    return False


def within(counts, lim):
    """fuzzy_within_constraints (_regex.c:9709): mins and maxes at section end."""
    full = counts + (sum(counts),)
    cost = sum(n * w for n, w in zip(counts, lim.costs))
    return all(lo <= n <= hi for lo, n, hi in zip(lim.mins, full, lim.maxs)) and cost <= lim.max_cost


def fuzzy(node, st, ctx, k):
    inner = replace(st, counts=(0, 0, 0), limits=node.limits, outer=st.outer + ((st.counts, st.limits),))
    return run(node.body, inner, ctx, lambda s: end_fuzzy(s, ctx, k))


def end_fuzzy(st, ctx, k):
    """Rule 4: 0, 1, 2 ... trailing insertions; rule 6: then check the limits."""
    while True:
        if within(st.counts, st.limits):
            (counts, limits), outer = st.outer[-1], st.outer[:-1]
            totals = st.totals
            if counts is None:  # outermost section: its errors join the result
                totals = tuple(a + b for a, b in zip(totals, st.counts))
            else:  # nested: the inner errors also count against the outer section
                counts = tuple(a + b for a, b in zip(counts, st.counts))
            yield from k(replace(st, totals=totals, counts=counts, limits=limits, outer=outer))
        if st.pos >= len(ctx.text) or not permitted(st, 1):
            return
        st = add_error(st, 1, st.pos + 1)  # a trailing insertion


@dataclass(frozen=True)
class Result:
    span: tuple
    groups: tuple
    fuzzy_counts: tuple
    path: tuple = ()


def attempt(tree, ngroups, text, start, anchor, must_end, referenced=frozenset()):
    st = State(start, ((-1, -1),) * (ngroups + 1), (0, 0, 0), None, None, (), anchor)
    for final in run(tree, st, Ctx(text, referenced), lambda s: iter([s])):
        if must_end and final.pos != len(text):
            continue
        return Result((start, final.pos), final.groups[1:], final.totals, final.path)
    return None


def compile_pattern(pattern):
    p = Parser(pattern)
    tree = p.parse()
    return tree, p.groups, frozenset(p.referenced)


def search(pattern, text, pos=0):
    tree, ngroups, referenced = compile_pattern(pattern)
    start = pos
    while start <= len(text):
        try:
            found = attempt(tree, ngroups, text, start, pos, False, referenced)
        except Prune as cut:
            restart = cut.restart
            start = restart if restart is not None and restart > start else start + 1
            continue
        if found:
            return found
        start += 1
    return None


def match(pattern, text, pos=0, must_end=False):
    tree, ngroups, referenced = compile_pattern(pattern)
    try:
        return attempt(tree, ngroups, text, pos, -1, must_end, referenced)
    except Prune:
        return None


def fullmatch(pattern, text, pos=0):
    return match(pattern, text, pos, must_end=True)


if __name__ == "__main__":
    api, pattern, text = sys.argv[1:4]
    print(globals()[api](pattern, text))
