"""Ledger entry 31: a fuzzy deletion in a full-folded item's leftovers deletes nothing.

Run: python tools/probes/s85-leftover-take-back.py

When a full-folded literal runs out part-way through a subject character's folding, upstream's
leftovers loop (_regex.c:14856) asks for a fuzzy edit, and a deletion there only moves the pattern
position, which is already at the end. So the item cannot end before the folding.

The first block is the literal arm: one subject character more makes a one-deletion match vanish
or move. The second is the free-deletion loop, which never ends. The third is the backreference
arm beside its literal form. The last is the row whose recorded leak-free control cuts off the
folding (see the entry's note in tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs).
"""
import regex

V1 = regex.I | regex.V1


def show(m):
    if m is None:
        return "None"
    return f"span={m.span()} fuzzy_counts={m.fuzzy_counts} fuzzy_changes={m.fuzzy_changes}"


def ask(pattern, subject, *args):
    try:
        answer = show(regex.compile(pattern, V1).search(subject, *args))
    except MemoryError:
        answer = "MemoryError"
    extra = f", {', '.join(map(str, args))}" if args else ""
    print(f"V1 search({ascii(pattern)}, {ascii(subject)}{extra}) -> {answer}")


print("regex", regex.__version__)

print("-- literal arm")
for pattern, subject in [
    (r"(?:sss){d<=1}", "ß"),
    (r"(?:sss){d<=1}", "ßß"),
    (r"(?r)(?:sss){d<=1}", "ßß"),
    (r"(?:ff){d<=2}", "ﬃ"),
    (r"(?:xfff){i<=1,d<=2}", "ﬀﬃfi"),
]:
    ask(pattern, subject)

print("-- free deletions")
ask(r"(?:sss){0d+1s+1i<=1:[x]}", "ßß")
ask(r"(?r)(?:sss){0d+1s+1i<=1:[x]}", "ßß")

print("-- backreference arm, then its literal form")
for pattern, subject in [
    (r"(s)(?:\1){d<=1}", "sß"),
    (r"(s)(?:s){d<=1}", "sß"),
    (r"(?r)(?:\1){d<=1}(s)", "ßs"),
    (r"(?r)(?:s){d<=1}(s)", "ßs"),
]:
    ask(pattern, subject)

print("-- a lookaround's edit at the item's start, then the forms that give the alignment")
for pattern, subject in [
    (r"(?f)(s)(?=(?:x){s<=1})(?:\1){d<=1}", "sß"),
    (r"(?f)(s)(?=(?:x){s<=1})(?:s){d<=1}", "sß"),
    (r"(?rf)(?:\1){d<=1}(?<=(?:x){s<=1})(s)", "ßs"),
    (r"(?rf)(?:s){d<=1}(?<=(?:x){s<=1})(s)", "ßs"),
    (r"(?=(?:x){s<=1})(?:ff){d<=2}", "ﬃ"),
    (r"(?=(?:x){s<=1})(?:yz){d<=2}", "a"),
    (r"(?r)(?:fi){d<=2}(?<=(?:x){s<=1})", "ﬃ"),
    (r"(?r)(?:yz){d<=2}(?<=(?:x){s<=1})", "a"),
]:
    ask(pattern, subject)

print("-- the leak-free control's endpos cuts the folding off")
row = r"(?f)(f)(?:b(?:\1)){e<=3,1i+1d+2s<=3}"
ask(row, "fSﬄ")
compiled = regex.compile(row, V1)
print("V1 match(pos=0)          ->", show(compiled.match("fSﬄ", 0)))
print("V1 match(pos=0, endpos=2) ->", show(compiled.match("fSﬄ", 0, 2)))
