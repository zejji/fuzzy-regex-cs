# What a non-zero `pos` does to everything EXCEPT the reversed partial run-out question, which
# ledger entry 24 and slice S52d move to the slice start. These are the cells the fix must NOT
# change, measured rather than assumed: `^`, `\A`, `\b`, `\B` and lookbehind keep Python `re`'s
# whole-string view of `pos`, and a reversed run-out is a PARTIAL and never a complete match.
#
# S52d, 2026-09-16. Run: python tools/probes/upstream-non-zero-pos-guards.py
import regex

print("regex", regex.__version__)


def show(label, m):
    print(f"  {label:<52} {'None' if m is None else f'({m.start()}, {m.end()}) partial={m.partial}'}")


print()
print("=== the argument the ruling rests on: a match may not use text before pos")
show("(?r)ab .search('abc', 1)", regex.compile("(?r)ab").search("abc", 1))
show("ab     .search('abc', 1)", regex.compile("ab").search("abc", 1))

print()
print("=== what a non-zero pos must keep seeing")
show("(?<=a)b .search('ab', 1)", regex.compile("(?<=a)b").search("ab", 1))
show("^b      .search('ab', 1)", regex.compile("^b").search("ab", 1))
show(r"\Ab     .search('ab', 1)", regex.compile(r"\Ab").search("ab", 1))
show(r"\bb     .search('a b', 2)", regex.compile(r"\bb").search("a b", 2))
show(r"\Bb     .search('ab', 1)", regex.compile(r"\Bb").search("ab", 1))

print()
print("=== a reversed run-out is a partial, never a complete match")
show("(?r)ab .match('xab', 2, 3)", regex.compile("(?r)ab").match("xab", 2, 3))
show("(?r)ab .match('xab', 2, 3, partial=True)", regex.compile("(?r)ab").match("xab", 2, 3, partial=True))
show("(?r)a  .match('xyz', 1, 1)", regex.compile("(?r)a").match("xyz", 1, 1))
