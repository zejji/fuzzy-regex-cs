"""The five grounds the FREE-tier invariants of `docs/ORACLE-INVARIANTS.md` rest on, measured.

S52c sitting 2. Every FREE-tier check the recorder runs on every wave row asserts something about
upstream's own match object. This probe asks upstream directly, so the checker's narrowings are
recorded as measurements rather than as reasoning.

Run: `.venvs/regex-2026.9.10/Scripts/python.exe tools/probes/upstream-free-tier-invariant-grounds.py`
"""

import regex

print(f"regex {regex.__version__}")
print()

# ---------------------------------------------------------------------------------------------
# 1. Does `lastgroup` name the same group as `lastindex`?
#
# `docs/ORACLE-INVARIANTS.md`'s `lastindex-participated` says "and `lastgroup` names the same
# group". `tools/record-oracle.py:977-981` claims the opposite - that `lastgroup` names the last
# NAMED group even when an unnamed one succeeded later. One of the two is wrong.
# ---------------------------------------------------------------------------------------------
print("1. lastindex versus lastgroup")
for pattern, subject in (
    (r"(?P<x>a)(b)", "ab"),
    (r"(a)(?P<x>b)", "ab"),
    (r"((a))", "a"),
    (r"(?P<x>a)|(b)", "a"),
    (r"(?P<x>a)(?P<y>b)", "ab"),
):
    m = regex.match(pattern, subject)
    names = {number: name for name, number in m.re.groupindex.items()}
    print(f"   {pattern!r:24} on {subject!r:6} lastindex={m.lastindex} "
          f"lastgroup={m.lastgroup!r}  (group {m.lastindex} is named {names.get(m.lastindex)!r})")
print()

# ---------------------------------------------------------------------------------------------
# 2. Can `lastindex` name a group that did not participate?
# ---------------------------------------------------------------------------------------------
print("2. does lastindex ever name a group whose span is (-1, -1)?")
for pattern, subject in (
    (r"(a)?(b)", "b"),
    (r"(?:(a)|(b))+", "ab"),
    (r"(a)(?=(b))", "ab"),
    (r"(?P<x>a)?b", "b"),
):
    m = regex.match(pattern, subject)
    span = None if m.lastindex is None else m.span(m.lastindex)
    print(f"   {pattern!r:20} on {subject!r:5} lastindex={m.lastindex} span={span}")
print()

# ---------------------------------------------------------------------------------------------
# 3. `\K` and a group call put a group span OUTSIDE the reported match span.
#
# The narrowing `group-spans-inside-match` already carries. Measured rather than assumed, because
# the checker skips every row whose pattern carries either and a skip for a reason that is not
# real is a hole.
# ---------------------------------------------------------------------------------------------
print("3. group spans outside the match span")
for pattern, subject in (
    (r"(a)\Kb", "ab"),
    (r"(?P<g>a)\K(?P<h>b)", "ab"),
    (r"(a)b(?1)", "aba"),
    (r"(?P<g>a)b(?&g)", "aba"),
):
    m = regex.match(pattern, subject)
    if m is None:
        print(f"   {pattern!r:22} on {subject!r:6} no match")
        continue
    outside = [n for n in range(1, m.re.groups + 1)
               if m.span(n) != (-1, -1) and not (m.start() <= m.start(n) and m.end(n) <= m.end())]
    print(f"   {pattern!r:22} on {subject!r:6} match={m.span()} "
          f"groups={[m.span(n) for n in range(1, m.re.groups + 1)]} outside={outside}")
print()

# ---------------------------------------------------------------------------------------------
# 4. Are `captures(g)` and `spans(g)` the same list seen two ways, including group 0 and a group
#    that repeated? And can a lookahead leave a capture whose span is outside the match?
# ---------------------------------------------------------------------------------------------
print("4. captures(g) against spans(g)")
for pattern, subject in (
    (r"(?:(\w)\d)+", "a1b2c3"),
    (r"(a)(?=(b))", "ab"),
    (r"(a)\Kb", "ab"),
    (r"(?:(a)|(b))+", "ab"),
):
    m = regex.match(pattern, subject)
    bad = []
    for n in range(0, m.re.groups + 1):
        caps, spans = m.captures(n), m.spans(n)
        if len(caps) != len(spans) or any(c != m.string[a:b] for c, (a, b) in zip(caps, spans)):
            bad.append((n, caps, spans))
    print(f"   {pattern!r:18} on {subject!r:8} "
          f"captures(1)={m.captures(1)} spans(1)={m.spans(1)} mismatches={bad}")
print()

# ---------------------------------------------------------------------------------------------
# 5. Does a group's span always sit inside the match on a LOOKAHEAD, with no \K and no call?
#    A lookahead consumes nothing, so a group inside one can match text past the match end.
#    If it can, `group-spans-inside-match` needs a third narrowing and the wave would flood.
# ---------------------------------------------------------------------------------------------
print("5. a group inside a lookahead or lookbehind")
for pattern, subject, kwargs in (
    (r"a(?=(b))", "ab", {}),
    (r"(?<=(a))b", "ab", {}),
    (r"a(?!(c))", "ab", {}),
    (r"(?:(?=(bc))b)", "bc", {}),
):
    m = regex.match(pattern, subject, **kwargs)
    if m is None:
        print(f"   {pattern!r:18} on {subject!r:5} no match")
        continue
    outside = [n for n in range(1, m.re.groups + 1)
               if m.span(n) != (-1, -1) and not (m.start() <= m.start(n) and m.end(n) <= m.end())]
    print(f"   {pattern!r:18} on {subject!r:5} match={m.span()} "
          f"groups={[m.span(n) for n in range(1, m.re.groups + 1)]} outside={outside}")
print()

# ---------------------------------------------------------------------------------------------
# 6. Does reading `captures(g)` on a POSIX fuzzy match that spent an error kill the interpreter,
#    the way reading `fuzzy_changes` does (ledger entry 9)?
#
#    `tools/probes/upstream-posix-fuzzy-safe-attributes.py` measured `span`, `spans`, `lastindex`,
#    `lastgroup` and `partial` as safe and never asked about `captures`, and the recorder's
#    `captures-are-the-texts-of-spans` check reads it on every match. If this segfaults, the check
#    has to carry the same POSIX guard `fuzzyChanges` carries. Run LAST, because a fault here takes
#    the process with it and everything above would be lost.
# ---------------------------------------------------------------------------------------------
print("6. captures() on a POSIX fuzzy match that spent an error")
for pattern, subject in (
    (r"(?p)(?:abc){e<=1}", "abd"),
    (r"(?p)(?P<g>abc){e<=1}", "abd"),
    (r"(?p)(?:(\w)(?:x){e<=1})+", "axbybz"),
):
    m = regex.compile(pattern).match(subject)
    print(f"   {pattern!r:26} on {subject!r:8} match={m.span()} counts={m.fuzzy_counts}")
    for n in range(m.re.groups + 1):
        print(f"      captures({n})={m.captures(n)!r} spans({n})={m.spans(n)!r}")
print("   reached the end without faulting")
print()

# ---------------------------------------------------------------------------------------------
# 7. Is an IndexError out of `subf` the MATCHER falling over, or the TEMPLATE answering?
#
#    `no-fault-where-a-twin-answers` fired on exactly two rows of S52c's first corrected three-seed
#    wave, both `subf`, both raising IndexError while matching, both with an ablation twin that
#    answered normally. If that is the matcher, it is a ledger entry. If it is `str.format` indexing
#    the MATCHED TEXT - which is legitimately a different length in the twin - then the invariant is
#    what is wrong, and `subf` has to come out of the fault limb.
#
#    Both rows are reproduced here from the wave, template and all.
# ---------------------------------------------------------------------------------------------
print("7. an IndexError out of subf")
for pattern, template, subject, verb, free_verb in (
    (r"(?r)\p{ASCII}{1,3}(*SKIP)ﬀ$", "{0[2]}{0[2]}{0[-2]}", "sﬀﬀ",
     "(*SKIP)", "(*PRUNE)"),
    (r"(?r)(?>\w{1,3}?(*PRUNE)[^a])(?:\p{ASCII}+?(*PRUNE)b|0)", "{0[-2]}{0[-2]}", " 0.b_ba\r",
     "(?>", "(?:"),
):
    # `ascii()` everywhere, because a Windows console is cp1252 and a printable non-ASCII character
    # in a repr kills this probe with a UnicodeEncodeError halfway through its own evidence.
    def answer(compiled, label):
        try:
            print(f"      {label} -> {ascii(compiled.subf(template, subject))}")
        except Exception as e:  # noqa: BLE001 - the exception IS the measurement
            print(f"      {label} -> {type(e).__name__}: {e}")

    def matched(compiled, label):
        m = compiled.search(subject)
        text = "-" if m is None else m.group(0)
        print(f"      {label} matches {m if m is None else m.span()} = {ascii(text)}, "
              f"{0 if m is None else len(text)} characters")

    compiled = regex.compile(pattern)
    twin = regex.compile(pattern.replace(verb, free_verb))
    print(f"   {ascii(pattern)}")
    print(f"      template {ascii(template)} on {ascii(subject)}")
    matched(compiled, "the row       ")
    answer(compiled, "the row       subf")
    matched(twin, f"the twin ({verb} -> {free_verb})")
    answer(twin, f"the twin      subf")
    # And the same template against a plainly long enough subject, which is the control: if this
    # renders, the template is not intrinsically bad and the index was out of range for the match.
    try:
        print(f"      same template on 'abcdefgh' -> "
              f"{ascii(regex.subf('abcdefgh', template, 'abcdefgh'))}")
    except Exception as e:  # noqa: BLE001
        print(f"      same template on 'abcdefgh' -> {type(e).__name__}: {e}")
