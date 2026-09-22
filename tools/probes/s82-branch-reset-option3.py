"""What upstream answers for every pattern S82's tests use.

S82 makes this port number a branch reset by option 3: in a branch reset, a group never takes a
number another group in the same branch will use. Upstream numbers by option 1 (the maintainer's
"current behaviour"), so the two engines part company exactly where a branch's later named group
reuses a number an earlier group in the same branch has already taken.

The rows below are the slice's whole test set. Each prints upstream's groups(), so a test that
asserts the same values can cite this run, and a test that asserts different ones is a measured
divergence rather than a guess. Run:

    python tools/probes/s82-branch-reset-option3.py

Measured against regex 2026.9.10 on 2026-09-22.
"""

import regex

# (label, pattern, subject). The "decoy" rows are the pre-scan hazards: something in the second
# branch that LOOKS like a named group definition and is not, so both engines must number the
# branch the same way. The "definition" rows are real definitions, where option 3 diverges.
ROWS = [
    ("shape 1, unnamed first", r"(?|(?P<bug>xxx)(!)|(!)(?P<bug>BUG))", "!BUG"),
    ("shape 2, unnamed first", r"(?|(?P<n>a)(b)|(c)(?P<n>d))", "cd"),
    ("shape 3, name in the middle", r"(?|(?P<n>a)(b)(c)|(x)(?P<n>y)(z))", "xyz"),
    ("corpus row 614", r"(?|(?<a>a)(?<b>b)|(c)(?<a>d))(e)", "cde"),
    ("unchanged: two names, either order", r"(?|(?<n>a)(b)|(?<m>c)(?<n>d))", "cd"),
    ("unchanged: two names, other order", r"(?|(?<n>a)(b)|(?<n>c)(?<m>d))", "cd"),
    ("unchanged: name first in branch", r"(?|(?<a>a)(?<b>b)|(?<b>c)(d))(e)", "cde"),
    ("unchanged: no names at all", r"(a)(?|(b)|(b))(d)", "abd"),
    ("decoy: character class", r"(?|(?<n>a)(b)|(X)[(?<n>z)](Y))", "X(Y"),
    ("decoy: comment", r"(?|(?<n>a)(b)|(X)(?#(?<n>z\))(Y))", "XY"),
    ("decoy: escaped paren", r"(?|(?<n>a)(b)|(X)\(?<n>z(Y))", "X(<n>zY"),
    ("decoy: lookbehind", r"(?|(?<n>a)(b)|(X)(?<=X)(Y))", "XY"),
    ("decoy: negative lookbehind", r"(?|(?<n>a)(b)|(X)(?<!q)(Y))", "XY"),
    ("decoy: name reference, not a definition", r"(?|(?<n>a)(b)|(X)(?P=n)?(Y))", "XY"),
    ("decoy: \\g reference, not a definition", r"(?|(?<n>a)(b)|(X)\g<n>?(Y))", "XY"),
    ("decoy: the (?'n') spelling is not one", r"(?|(?<n>a)(b)|(X)(?'n'z)(Y))", "XzY"),
    ("decoy: fresh name claims a new number", r"(?|(?<n>a)(b)|(X)(?<fresh>y))", "Xy"),
    ("decoy: verbose comment", "(?x)(?|(?<n>a)(b)|(X) # (?<n>z)\n (Y))", "XY"),
    ("definition: literal backslash before it", r"(?|(?<n>a)(b)|(X)\\(?<n>z)(Y))", "X\\zY"),
    ("definition: after a conditional, upstream", r"(?|(?<n>a)(b)|(X)(?(n)p|q)(?<n>Z))", "XpZ"),
    ("definition: after a conditional, option 3", r"(?|(?<n>a)(b)|(X)(?(n)p|q)(?<n>Z))", "XqZ"),
    ("definition: after a nested set, V1", r"(?V1)(?|(?<n>a)(b)|(X)[[a-z]--[q]](?<n>Z))", "XzZ"),
    ("definition: after a nested set, V0", r"(?V0)(?|(?<n>a)(b)|(X)[[a-z]--[q]](?<n>Z))", "XzZ"),
    ("decoy: inside a nested set, V1", r"(?V1)(?|(?<n>a)(b)|(X)[[(?<n>z)]](Y))", "X(Y"),
    ("definition: after a set holding ], V1", r"(?V1)(?|(?<n>a)(b)|(X)[]q](?<n>Z))", "X]Z"),
    ("definition: after a set holding ], V0", r"(?V0)(?|(?<n>a)(b)|(X)[]q](?<n>Z))", "X]Z"),
    ("definition: after a lookbehind", r"(?|(?<n>a)(b)|(X)(?<=X)(?<n>Z))", "XZ"),
    ("definition: with a later reference", r"(?|(?<n>a)(b)|(X)(?<n>q)\g<n>(Y))", "XqqY"),
    ("definition: inside a nested branch reset", r"(?|(?<n>a)(b)|(X)(?|(?<n>y)|(q))(Z))", "XyZ"),
    ("definition: verbose, name written with spaces", "(?x)(?|(?<n>a)(b)|(X) (?< n >y) (Z))", "XyZ"),
    ("definition: after a character class", r"(?|(?<n>a)(b)|(X)[qz](?<n>Z))", "XqZ"),
]

# Spellings this port's parser recognises, and one it does not. parse_name reads to ">" or ")",
# so (?'a'x) is not a named group in either engine; the pre-scan must not treat "'" as a
# delimiter. Printed with the error rather than the groups.
SPELLINGS = [
    (r"(?P<a>x)", "x"),
    (r"(?<a>x)", "x"),
    (r"(?'a'x)", "x"),
]


def main() -> None:
    print(f"regex {regex.__version__}")
    for label, pattern, subject in ROWS:
        try:
            m = regex.compile(pattern).match(subject)
        except Exception as e:  # noqa: BLE001 - the point is to print whatever it raises
            print(f"{label:44} {pattern!r:52} ERROR {type(e).__name__}: {e}")
            continue
        if m is None:
            print(f"{label:44} {pattern!r:52} NO MATCH on {subject!r}")
            continue
        print(
            f"{label:44} {pattern!r:52} on {subject!r:10} "
            f"groups={m.groups()} groupindex={dict(m.re.groupindex)} "
            f"captures={ {name: m.captures(name) for name in m.re.groupindex} }"
        )

    print()
    for pattern, subject in SPELLINGS:
        try:
            m = regex.compile(pattern).match(subject)
            print(f"{pattern!r:12} groups={m.groups()} groupindex={dict(m.re.groupindex)}")
        except Exception as e:  # noqa: BLE001
            print(f"{pattern!r:12} ERROR {type(e).__name__}: {e}")


if __name__ == "__main__":
    main()
