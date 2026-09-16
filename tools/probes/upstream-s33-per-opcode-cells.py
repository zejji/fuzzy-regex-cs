# The cells S33 pinned in
# `PartialMatchingTests.The_narrowed_slice_partial_is_a_per_opcode_answer_and_not_a_general_rule`,
# re-measured on the current release. S33 read them as "a per-opcode answer, not a general rule";
# ledger entry 24 and the owner's ruling of 2026-09-15 (Option B) read the same numbers as upstream
# contradicting itself, and S52d makes this port answer all of them the one way.
#
# S52d, 2026-09-16. Run: python tools/probes/upstream-s33-per-opcode-cells.py
import regex

print("regex", regex.__version__)


def show(label, m):
    print(f"  {label:<48} {'None' if m is None else f'({m.start()}, {m.end()}) partial={m.partial}'}")


print()
print("=== the five S33 pinned as None: a reversed pattern at the left edge of a narrowed slice")
for pattern in ("(?r)a", "(?r)ab*", "(?r)a(b)*", "(?r)a(bc)+"):
    show(f"{pattern:<12}.match('abc', 1, 1, partial=True)", regex.compile(pattern).match("abc", 1, 1, partial=True))
show("(?r)qz|qzzz .match('qz', 1, 2, partial=True)", regex.compile("(?r)qz|qzzz").match("qz", 1, 2, partial=True))

print()
print("=== the same shapes where the slice starts where the subject does: upstream answers a partial")
show("(?r)a       .match('abc', 0, 0, partial=True)", regex.compile("(?r)a").match("abc", 0, 0, partial=True))
show("(?r)ab*     .match('abc', 0, 0, partial=True)", regex.compile("(?r)ab*").match("abc", 0, 0, partial=True))
show("(?r)a(b)*   .match('abc', 0, 0, partial=True)", regex.compile("(?r)a(b)*").match("abc", 0, 0, partial=True))
show("(?r)a(bc)+  .match('abc', 0, 0, partial=True)", regex.compile("(?r)a(bc)+").match("abc", 0, 0, partial=True))
show("(?r)qz|qzzz .match('qz', 0, 1, partial=True)", regex.compile("(?r)qz|qzzz").match("qz", 0, 1, partial=True))

print()
print("=== and the Rule B half S33 also pinned, which the ruling agrees with and does not move")
show("(?r)a(bc)*  .match('abc', 1, 1, partial=True)", regex.compile("(?r)a(bc)*").match("abc", 1, 1, partial=True))
show("(?r)a(bc)*  .match('abc', 2, 2, partial=True)", regex.compile("(?r)a(bc)*").match("abc", 2, 2, partial=True))
show("(?r)ab|abcd .match('ab', 1, 2, partial=True)", regex.compile("(?r)ab|abcd").match("ab", 1, 2, partial=True))
print(
    "  (?r)a(bc)*  .finditer('abab', 1, 4, partial=True)     "
    + str([(m.span(), m.partial) for m in regex.compile("(?r)a(bc)*").finditer("abab", 1, 4, partial=True)])
)
