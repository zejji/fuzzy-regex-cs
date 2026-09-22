"""Ledger entries 29 and 30: two ways a fuzzy full-folded backreference loses a match upstream.

Run: python tools/probes/s84-full-fold-backreference.py

Entry 29 (leftovers): a group that ends half-way through a subject character's folding backtracks
instead of charging the rest as an edit. Each case is asked under Version 1 (full case folding,
which builds a REF_GROUP_FLD item), under Version 0 (where sharp s is one character), and for the
first two as a literal with the same folded text, which upstream matches.

Entry 30 (retry): a retried edit compares the character it used up again. It needs no ligature,
so V1 and V0 mean the same pattern; the third line drops IGNORECASE, which also avoids
REF_GROUP_FLD.

The last block is the six wave rows, each asked as recorded and with (?:\\1) replaced by the
group's text, the second being the answer this port gives.
"""
import regex

V1 = regex.I | regex.V1
V0 = regex.I | regex.V0


def show(m):
    if m is None:
        return "None"
    if isinstance(m, list):
        return ascii(m)
    return (f"span={m.span()} partial={m.partial} fuzzy_counts={m.fuzzy_counts} "
            f"fuzzy_changes={m.fuzzy_changes}")


def ask(name, pattern, subject, flags):
    print(f"{name} search({ascii(pattern)}, {ascii(subject)}) -> {show(regex.search(pattern, subject, flags))}")


print("regex", regex.__version__)

print("-- entry 29: leftovers")
for pattern, subject, literal, literal_subject in [
    (r"(s)(?:\1){e<=1}", "sß", r"(?:sss){e<=1}", "ßß"),
    (r"(s)(?:\1){i<=1}", "sß", r"(?:sss){i<=1}", "ßß"),
    (r"(?r)(?:\1){e<=1}(s)", "ßs", None, None),
    (r"(as)(?:\1){e<=1}", "asaß", None, None),
    (r"(s)(?:\1){e<=1}x", "sßx", None, None),
    (r"(?b)(s)(?:\1){e<=1}", "sß", None, None),
    (r"(?e)(s)(?:\1){e<=1}", "sß", None, None),
]:
    ask("V1", pattern, subject, V1)
    ask("V0", pattern, subject, V0)
    if literal:
        ask("V1 literal", literal, literal_subject, V1)

print("-- entry 30: retry")
for pattern, subject in [
    (r"(ab)(?:\1){e<=1}", "abxab"),
    (r"(ab)(?:\1){e<=1}c", "abxabc"),
    (r"(?r)(?:\1){e<=1}(ab)", "abxab"),
]:
    ask("V1", pattern, subject, V1)
    ask("V0", pattern, subject, V0)
    ask("V1 case-sensitive", pattern, subject, regex.V1)

# S83's best-match case: the one-deletion match exists, and the literal form finds it.
ask("V1", r"(?b)(?f)(ßa)(?:\1){s<=1,i<=1,d<=1}", "ßasa", V1)
ask("V1", r"(?b)(?f)(ßa)(?:\1){d<=1}", "ßasa", V1)
ask("V1 literal", r"(?b)(?f)(ßa)(?:ßa){s<=1,i<=1,d<=1}", "ßasa", V1)

print("-- the wave rows, as recorded (V0 with (?f)) and with the group written out")
WAVE = [
    ("seed 7 row 6208", r"(?fi)(a😀)(?:b\p{L}\b(?:\1)){1<=e<=2:[^t]}", "a😀", "a😀b😀ﬆ", "split", dict(maxsplit=3)),
    ("seed 7 row 6243", r"(?fi)(ﬀo)(?:a*?😀(?:[ab]+(?:\1)){e<=3:[a-s]}){e<=3,1i+1d+2s<=3:f}", "ﬀo", "ﬀoaa😀affo",
     "fullmatch", dict(pos=0, endpos=9)),
    ("seed 7 row 6250", r"(?b)(?fi)(ßa)(?:(?:\1)\B0a😀){s<=1,i<=1,d<=1}", "ßa", "ßasa0a😀", "match", {}),
    ("seed 7 row 6471", r"(?fi)\m(ßa)(?:(?:\1)b+?𝟮.){e<=3,1i+1d+2s<=3}", "ßa", "ßaTssAb𝟮TA", "fullmatch", {}),
    ("seed 4242 row 6114", r"(?fi)(?r)(?:\p{Nd}\A(?:\1)){e<=3}(oba)", "oba", "‍OBaToba", "match", {}),
    ("seed 20260922 row 6591", r"(?e)(?fi)\m(ﬆx)(?:(?:\1)[ab]*?a){e<=3:\w}", "ﬆx", "ﬆxX", "fullmatch",
     dict(pos=0, endpos=3, partial=True)),
]
for label, pattern, group, subject, operation, kwargs in WAVE:
    written = pattern.replace(r"(?:\1)", "(?:" + regex.escape(group) + ")")
    for name, p in (("as recorded", pattern), ("written out", written)):
        result = getattr(regex.compile(p, regex.V0), operation)(subject, **kwargs)
        print(f"{label} {name} {operation}({ascii(p)}, {ascii(subject)}, {kwargs}) -> {show(result)}")
