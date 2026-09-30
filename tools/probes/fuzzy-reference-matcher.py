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
(?(1)yes|no), the verbs (*SKIP), (*PRUNE), (*FAIL) / (*F), and the
lookarounds (?=...), (?!...), (?<=...) and (?<!...) (rule 10). Added
2026-09-30 (rules 11-18): the zero-width escapes \A \Z \G \b \B \m \M, \K,
atomic groups (?>...) and possessive repeats, branch reset (?|...),
conditionals with a lookaround test, a fuzzy section inside a lookahead,
IGNORECASE (with or without FULLCASE) over ASCII text, and finditer. No other
flags, no calls and no fuzzy tests ({s<=1:[a-z]}).

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
   Upstream checks them first (_regex.c:12461-12462, before the frame that
   offers trailing insertions is pushed at :12500-12511), so a section that
   ends below its minimum never tries one (ledger entry 51, known defect D9).
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
10. A lookaround is atomic, and its body is exact even inside a fuzzy section
   (the port compiles it so, src/FuzzyRegex/Parsing/Nodes.cs, and upstream
   likewise, measured 2026-09-28: search "(?:b(?=c)){s<=1}" "bxc" is (1, 2)).
   A lookbehind's body must end where the lookbehind stands; a capture-free
   body is assumed, since the order of its candidate starts is not modelled.
   THE REFERENCE RULE THAT UPSTREAM BREAKS (ledger entry 50): a lookaround
   that fails is a zero-width item that failed, so inside a fuzzy section an
   insertion is tried in front of it and the lookaround is tried again one
   character on, as upstream does for ^ and $ (_regex.c:12060-12075). Rule 5
   applies. Upstream never fuzzes a lookaround (_regex.c:12918-13000,
   :17115-17168). LOOKAROUND_INSERTION = False gives upstream's behaviour.

Rules 11-18 (2026-09-30) are modelled from written rules only: upstream's
README.rst, the answer key (docs/plan/2026-09-30-complete-matrix.md, A2 and
A3) and the rulings it cites. Anything they leave open is rejected, not
guessed; each rule says what it rejects.

11. Zero-width escapes. \A is the start of the text and \Z its very end
   (Python re docs: "\Z Matches only at the end of the string"); \G is where
   this search began, or where the previous match ended in finditer (A2
   "search-anchor", README "Search anchor"); \b and \B are a word boundary and
   its opposite, \m the start and \M the end of a word (README "Added \m and
   \M"), a word character being one \w matches. Each is a zero-width item, so
   a failing one inside a fuzzy section takes the insertion that ^ and $ take
   (rule 10's reference rule, one rule for every zero-width item).
12. \K: the reported match starts where \K was last passed on the path that
   succeeds; groups are unchanged (A2 "keep", README "Added \K"). Rejected
   inside a lookaround, where perlre calls it "not well defined", and under
   finditer (rule 18).
13. Atomic groups and possessive repeats: once the body has matched, its
   choice points are discarded; X*+ is (?>X*) (A2 "atomic, possessive",
   pcre2pattern "Atomic grouping and possessive quantifiers"). A verb inside
   one is rejected: README.rst says its effect stays inside the group, while
   PCRE2 lets it end the attempt (docs/plan/2026-09-26-verb-confinement-
   survey.md), so no single written rule decides it. Rejected inside a
   lookbehind, whose body is matched backwards, so the first match differs.
14. Branch reset: each alternative numbers its groups from the same start (A2
   "branch-reset"; pcre2pattern "Duplicate group numbers"). A named group in
   one is rejected (the harness turns names into numbers first, and the
   naming rule is the separate OPEN-3 ruling).
15. A conditional whose test is a lookaround runs the test exactly and
   atomically (A3 "fuzzy + conditional", "lookaround + conditional"). Captures
   made by a test that succeeds are kept (A2 "conditional"); so are those of a
   negative test that fails, its body having matched (owner ruling OPEN-2,
   D54). Backtracking onto a verb in the test makes the test's body fail
   (A3 "lookaround + verb": a positive condition false, a negative one true).
16. A fuzzy section inside a lookahead's body: its errors count in the
   result (A3 "fuzzy + lookaround"). Rejected inside a lookbehind (its errors
   depend on the order in which the body is matched backwards, which no rule
   states) and when the lookahead itself stands inside a fuzzy section
   (whether the errors also spend the outer budget is not written down).
17. IGNORECASE: a literal, class or backreference matches a character if it
   matches either case of it; a negated class matches if it matches neither.
   Over ASCII text simple and full case folding agree (README "Case-
   insensitive matches in Unicode"; ASCII has no multi-character folds), so
   FULLCASE is accepted too; a non-ASCII pattern or text is rejected.
18. finditer: each match's search starts where the previous match ended; after
   an empty match the next one may not be empty at that same position, but a
   non-empty match may start there (A2 "op-finditer", Python re docs for
   finditer). Rules 5 and 11 take the new search start as their anchor.
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
# Rule 10 switch: False gives upstream's behaviour (a failing lookaround is
# never passed by an insertion).
LOOKAROUND_INSERTION = True
# Rule 6 switch: False gives upstream's order (a section below its minimum at
# its end fails before any trailing insertion is tried).
MINIMUM_AFTER_TRAILING_INSERTIONS = True
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
    kind: str  # ^ or $, or A Z G b B m M (rule 11)


@dataclass(frozen=True)
class Keep:  # \K (rule 12)
    pass


@dataclass(frozen=True)
class Atomic:  # (?>...) and possessive repeats (rule 13)
    body: object


@dataclass(frozen=True)
class CondLook:  # (?(?=...)yes|no) and the other lookaround tests (rule 15)
    test: object  # a Look
    yes: object
    no: object


@dataclass(frozen=True)
class Backref:
    index: int


@dataclass(frozen=True)
class Cond:
    index: int
    yes: object
    no: object


@dataclass(frozen=True)
class Look:
    ahead: bool
    positive: bool
    body: object


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


def children(node):
    return {Seq: lambda n: n.parts, Alt: lambda n: n.branches, Group: lambda n: (n.body,),
            Repeat: lambda n: (n.body,), Cond: lambda n: (n.yes, n.no), Look: lambda n: (n.body,),
            Fuzzy: lambda n: (n.body,), Atomic: lambda n: (n.body,),
            CondLook: lambda n: (n.test, n.yes, n.no)}.get(type(node), lambda n: ())(node)


def contains(node, kind, pred=lambda n: True):
    """True if node or a node inside it is a `kind` for which pred holds."""
    if isinstance(node, kind) and pred(node):
        return True
    return any(contains(c, kind, pred) for c in children(node))


def check_fuzzy_lookaheads(node, in_fuzzy=False):
    """Rule 16: a lookahead holding a fuzzy section must not itself stand in one."""
    if isinstance(node, Look) and in_fuzzy and contains(node.body, Fuzzy):
        raise ValueError("unsupported fuzzy section inside a lookaround inside a fuzzy section")
    inside = in_fuzzy or isinstance(node, Fuzzy)
    for c in children(node):
        check_fuzzy_lookaheads(c, inside)


def fold(chars, ignorecase):
    """Rule 17: a set of characters and the other case of each, under IGNORECASE."""
    return chars | {c.swapcase() for c in chars} if ignorecase else chars


def is_word(ch):
    return ch.isalnum() or ch == "_"


class Parser:
    def __init__(self, pattern, ignorecase=False):
        self.p, self.i, self.groups = pattern, 0, 0
        self.referenced = set()  # groups a backreference or conditional tests
        self.ignorecase = ignorecase
        if ignorecase and not pattern.isascii():
            raise ValueError("unsupported: IGNORECASE over a non-ASCII pattern (rule 17)")

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
            elif self.peek() == "+":
                # A possessive repeat is an atomic group round a greedy one (rule 13).
                self.take("+")
                node = self.atomic(Repeat(node, lo, hi, True))
                continue
            node = Repeat(node, lo, hi, greedy)

    def atomic(self, body):
        if contains(body, Verb):  # rule 13: no written rule for a verb inside one
            raise ValueError(f"unsupported verb inside an atomic group in {self.p!r}")
        return Atomic(body)

    def branch_reset(self):
        """Rule 14: every alternative numbers its groups from the same start."""
        self.take("(?|")
        base = top = self.groups
        branches = []
        while True:
            self.groups = base
            branches.append(self.sequence())
            top = max(top, self.groups)
            if self.peek() != "|":
                break
            self.take("|")
        self.take(")")
        self.groups = top
        return branches[0] if len(branches) == 1 else Alt(tuple(branches))

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
        if self.p.startswith(("(?(?=", "(?(?!", "(?(?<=", "(?(?<!"), self.i):  # rule 15
            self.take("(?")
            test = self.atom()
            yes, no = self.sequence(), Seq(())
            if self.peek() == "|":
                self.take("|")
                no = self.sequence()
            self.take(")")
            return CondLook(test, yes, no)
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
            elif self.peek(3) in ("(?=", "(?!") or self.peek(4) in ("(?<=", "(?<!"):
                ahead = self.peek(3) in ("(?=", "(?!")
                self.i += 3 if ahead else 4
                positive = self.p[self.i - 1] == "="
                body = self.alternation()
                self.take(")")
                # Outside the subset (rule 10): the body runs with no fuzzy state, so a section's
                # errors inside it would be dropped; and a lookbehind is tried start by start, left
                # to right, so a verb or a capture in it would act in an order no engine uses.
                # A fuzzy section in a lookahead is rule 16 (checked after parsing, as it depends on
                # what encloses the lookahead); in a lookbehind it is outside the subset.
                if not ahead and contains(body, Fuzzy):
                    raise ValueError(f"unsupported fuzzy section inside a lookbehind in {self.p!r}")
                # An atomic group or possessive repeat also commits to the first match in an order,
                # and a lookbehind's body is matched backwards (A2 "lookaround"); measured
                # 2026-09-30: "(?:ab|a)(?<=(?:a*a)?+)" over "aba" is (0, 2) in upstream and the port, but
                # (0, 1) matched forwards. Rule 13 applies only outside a lookbehind.
                if not ahead and (contains(body, Verb) or contains(body, Group, lambda g: g.index)
                                  or contains(body, Atomic)):
                    raise ValueError(f"unsupported verb, capture or atomic group inside a lookbehind in {self.p!r}")
                # Rule 12 has no rule for \K inside a lookaround: perlre, "The use of \K inside of
                # another lookaround assertion is allowed, but the behaviour is currently not well
                # defined" (fetched 2026-09-30); upstream and the port move the start (row 4301).
                if contains(body, Keep):
                    raise ValueError(f"unsupported \\K inside a lookaround in {self.p!r}")
                return Look(ahead, positive, body)
            elif self.peek(3) == "(?>":  # rule 13
                self.take("(?>")
                body = self.alternation()
                self.take(")")
                return self.atomic(body)
            elif self.peek(3) == "(?|":  # rule 14
                return self.branch_reset()
            elif self.peek(2) == "(?":  # flags, named groups, calls ...
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
            if ch == "w":
                return Item(is_word, "\\w")
            if ch in "AZGbBmM":  # rule 11
                return Anchor(ch)
            if ch == "K":  # rule 12
                return Keep()
            if ch.isalnum():
                # \A, \b, \G, \K, \Z, \g<name> ... are not literals; read as one they silently
                # answered another pattern ("\G\Ab" matched the text "GAb"; matrix triage, 2026-09-30).
                raise ValueError(f"unsupported escape \\{ch} at {self.i - 2} in {self.p!r}")
            return Item(lambda x, s=fold({ch}, self.ignorecase): x in s, ch)
        self.i += 1
        return Item(lambda x, s=fold({c}, self.ignorecase): x in s, c)

    def char_class(self):
        end = self.p.index("]", self.i + 1)
        body = self.p[self.i + 1 : end]
        if "\\" in body:
            raise ValueError(f"unsupported escape in a class at {self.i} in {self.p!r}")
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
        chars = fold(chars, self.ignorecase)  # rule 17: a negated class then matches neither case
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
    keep: int = -1  # where \K was last passed (rule 12), or -1


class Prune(Exception):
    """Backtracking reached (*PRUNE) or (*SKIP): end this attempt."""

    def __init__(self, restart):
        self.restart = restart  # None = next character, else SKIP position


class Ctx:
    def __init__(self, text, referenced=frozenset(), start=0, ignorecase=False):
        self.text = text
        self.referenced = referenced
        self.start = start  # where this search began, for \G (rule 11)
        self.ignorecase = ignorecase


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
    if kind is Look:
        return lookaround(node, st, ctx, k)
    if kind is CondLook:
        return cond_look(node, st, ctx, k)
    if kind is Atomic:
        return atomic(node, st, ctx, k)
    if kind is Keep:
        return k(replace(st, keep=st.pos))
    return verb(node, st, ctx, k)


def look_state(node, st, ctx, in_test=False):
    """Rule 10: the state after the body's first match (its groups, and the errors of a fuzzy
    section in it, rule 16), or None if it has none. in_test: the lookaround is a conditional's
    test, where a verb backtracked onto makes the body fail whatever its sign (rule 15)."""
    inner = replace(st, counts=None, limits=None, outer=())
    if node.ahead:
        try:
            for s in run(node.body, inner, ctx, lambda s: iter([s])):
                return s
        except Prune:
            # Backtracking onto a verb inside a negative assertion makes the assertion true
            # (pcre2pattern "Backtracking verbs in assertions"). Measured 2026-09-30: search
            # "(?!a(*PRUNE)(*F))a" "a" is (0, 1) in upstream, PCRE2 10.47 and Perl 5.42.3. Inside
            # a positive one it acts on the whole match, so it goes on up.
            if node.positive and not in_test:
                raise
        return None
    for start in range(st.pos, -1, -1):
        for s in run(node.body, replace(inner, pos=start), ctx, lambda s: iter([s]) if s.pos == st.pos else iter(())):
            return s
    return None


def carried(st, body):
    """st moved on by a lookaround body's match: its captures and its errors, not its position."""
    return replace(st, groups=body.groups, totals=body.totals, path=body.path)


def lookaround(node, st, ctx, k):
    body = look_state(node, st, ctx)
    if (body is not None) == node.positive:
        yield from k(carried(st, body) if node.positive else st)
        return
    if LOOKAROUND_INSERTION and st.counts is not None:
        if st.pos < len(ctx.text) and st.pos != st.anchor and permitted(st, 1):  # rule 5
            yield from lookaround(node, add_error(st, 1, st.pos + 1), ctx, k)


def cond_look(node, st, ctx, k):
    """Rule 15: the test runs exactly and atomically; its captures are kept when its body
    matched (a positive test that holds, or a negative one that fails)."""
    body = look_state(node.test, st, ctx, in_test=True)
    holds = (body is not None) == node.test.positive
    return run(node.yes if holds else node.no, st if body is None else carried(st, body), ctx, k)


def atomic(node, st, ctx, k):
    """Rule 13: the body's first match, with its choice points discarded."""
    for s in run(node.body, st, ctx, lambda s: iter([s])):
        return k(s)
    return iter(())


def anchor(node, st, ctx, k):
    n, pos, text = len(ctx.text), st.pos, ctx.text
    before = pos > 0 and is_word(text[pos - 1])
    after = pos < n and is_word(text[pos])
    ok = {"^": pos == 0,
          "$": pos == n or (pos == n - 1 and text[-1] == "\n"),  # the end, or before a final newline
          "A": pos == 0, "Z": pos == n, "G": pos == ctx.start,  # rule 11
          "b": before != after, "B": before == after,
          "m": not before and after, "M": before and not after}[node.kind]
    if ok:
        yield from k(st)
        return
    # A failing anchor in a fuzzy section may be passed by inserting a text character in front of
    # it (upstream _regex.c:12060-12075, the rule 10 cites; rule 5 applies). Measured 2026-09-30:
    # fullmatch "(?:a$){i<=1}" "ab" is (0, 2) with one insertion in upstream and the port.
    if st.counts is not None and st.pos < n and st.pos != st.anchor and permitted(st, 1):
        yield from anchor(node, add_error(st, 1, st.pos + 1), ctx, k)


def backref(node, st, ctx, k):
    start, end = st.groups[node.index]
    if start < 0:  # a reference to an unset group fails
        return iter(())
    items = tuple(Item(lambda x, s=fold({ch}, ctx.ignorecase): x in s, ch) for ch in ctx.text[start:end])
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
    first = True
    while True:
        if within(st.counts, st.limits):
            (counts, limits), outer = st.outer[-1], st.outer[:-1]
            totals = st.totals
            if counts is None:  # outermost section: its errors join the result
                totals = tuple(a + b for a, b in zip(totals, st.counts))
            else:  # nested: the inner errors also count against the outer section
                counts = tuple(a + b for a, b in zip(counts, st.counts))
            yield from k(replace(st, totals=totals, counts=counts, limits=limits, outer=outer))
        elif first and not MINIMUM_AFTER_TRAILING_INSERTIONS:
            return
        first = False
        if st.pos >= len(ctx.text) or not permitted(st, 1):
            return
        st = add_error(st, 1, st.pos + 1)  # a trailing insertion


@dataclass(frozen=True)
class Result:
    span: tuple
    groups: tuple
    fuzzy_counts: tuple
    path: tuple = ()


@dataclass(frozen=True)
class Compiled:
    tree: object
    ngroups: int
    referenced: frozenset
    ignorecase: bool


def attempt(c, text, start, anchor, must_end, search_start, not_empty=False):
    """The first match of an attempt at start. not_empty: rule 18, a match may not end at start."""
    st = State(start, ((-1, -1),) * (c.ngroups + 1), (0, 0, 0), None, None, (), anchor)
    for final in run(c.tree, st, Ctx(text, c.referenced, search_start, c.ignorecase), lambda s: iter([s])):
        if must_end and final.pos != len(text):
            continue
        if not_empty and final.pos == start:
            continue
        begin = start if final.keep < 0 else final.keep  # rule 12
        return Result((begin, final.pos), final.groups[1:], final.totals, final.path)
    return None


IGNORECASE, FULLCASE = 2, 16384


def compile_pattern(pattern, flags=0):
    if flags & ~(IGNORECASE | FULLCASE) or flags == FULLCASE:
        raise ValueError(f"unsupported flags {flags}")
    p = Parser(pattern, bool(flags & IGNORECASE))
    tree = p.parse()
    check_fuzzy_lookaheads(tree)
    if contains(tree, CondLook, lambda n: contains(n.test, Fuzzy)):
        raise ValueError(f"unsupported fuzzy section in a conditional's test in {pattern!r}")  # rule 15
    return Compiled(tree, p.groups, frozenset(p.referenced), bool(flags & IGNORECASE))


def checked(c, text):
    if c.ignorecase and not text.isascii():
        raise ValueError("unsupported: IGNORECASE over non-ASCII text (rule 17)")
    return c


def search_from(c, text, pos, not_empty=False):
    """One search from pos: rule 5's anchor and the \\G position are pos for every attempt."""
    start = pos
    while start <= len(text):
        try:
            found = attempt(c, text, start, pos, False, pos, not_empty and start == pos)
        except Prune as cut:
            restart = cut.restart
            start = restart if restart is not None and restart > start else start + 1
            continue
        if found:
            return found
        start += 1
    return None


def search(pattern, text, pos=0, flags=0):
    return search_from(checked(compile_pattern(pattern, flags), text), text, pos)


def match(pattern, text, pos=0, must_end=False, flags=0):
    c = checked(compile_pattern(pattern, flags), text)
    try:
        return attempt(c, text, pos, -1, must_end, pos)
    except Prune:
        return None


def fullmatch(pattern, text, pos=0, flags=0):
    return match(pattern, text, pos, must_end=True, flags=flags)


def finditer(pattern, text, pos=0, flags=0):
    """Rule 18: each search starts where the last match ended; after an empty match, the next
    may not be empty at that position."""
    c = checked(compile_pattern(pattern, flags), text)
    if contains(c.tree, Keep):
        # Whether "empty" means the reported span or the text the attempt consumed is not written
        # down for \K, so the rule for the next search's start is not either.
        raise ValueError("unsupported: \\K under finditer")
    out, start, not_empty = [], pos, False
    while start <= len(text):
        found = search_from(c, text, start, not_empty)
        if found is None:
            break
        out.append(found)
        not_empty = found.span[0] == found.span[1]
        start = found.span[1]
    return out


if __name__ == "__main__":
    api, pattern, text = sys.argv[1:4]
    print(globals()[api](pattern, text))
