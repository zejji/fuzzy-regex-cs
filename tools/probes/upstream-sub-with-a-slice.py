"""Upstream's ``sub``/``subn``/``subf``/``subfn`` with ``pos`` and ``endpos``.

S53b gives ``Replace`` and ``ReplaceFormat`` a ``beginning``/``length`` pair. The claim the slice
makes about it - "replacements happen only inside the slice, and the text outside it is copied
through unchanged" - is a claim about upstream, so it is measured here rather than read off
``_regex.c``. Every expected value in ``Gaps/Substitution/ReplaceSliceTests.cs`` is a line of this
probe's output.

Run:  python tools/probes/upstream-sub-with-a-slice.py
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")

print("regex", regex.__version__)


def show(label, call):
    try:
        print(f"  {label:52} = {call()!r}")
    except Exception as exc:  # noqa: BLE001
        print(f"  {label:52} ! {type(exc).__name__}: {exc}")


a = regex.compile("a")

print()
print("1. the slice bounds the matching; the text outside it is copied through")
show("sub('X', 'aaaaa')", lambda: a.sub("X", "aaaaa"))
show("sub('X', 'aaaaa', pos=1)", lambda: a.sub("X", "aaaaa", pos=1))
show("sub('X', 'aaaaa', endpos=3)", lambda: a.sub("X", "aaaaa", endpos=3))
show("sub('X', 'aaaaa', pos=1, endpos=3)", lambda: a.sub("X", "aaaaa", pos=1, endpos=3))
show("subn('X', 'aaaaa', pos=1, endpos=3)", lambda: a.subn("X", "aaaaa", pos=1, endpos=3))
show("subf('[{0}]', 'aaaaa', pos=1, endpos=3)", lambda: a.subf("[{0}]", "aaaaa", pos=1, endpos=3))
show("subfn('[{0}]', 'aaaaa', pos=1, endpos=3)", lambda: a.subfn("[{0}]", "aaaaa", pos=1, endpos=3))
show("sub('X', 'aaaaa', 1, pos=1)  [count and slice]", lambda: a.sub("X", "aaaaa", 1, pos=1))

print()
print("2. out-of-range and inverted bounds are clamped, not rejected")
show("sub('X', 'aaaaa', pos=-2)", lambda: a.sub("X", "aaaaa", pos=-2))
show("sub('X', 'aaaaa', pos=99)", lambda: a.sub("X", "aaaaa", pos=99))
show("sub('X', 'aaaaa', endpos=99)", lambda: a.sub("X", "aaaaa", endpos=99))
show("sub('X', 'aaaaa', pos=3, endpos=1)", lambda: a.sub("X", "aaaaa", pos=3, endpos=1))

print()
print("3. the slice start IS the start, for an anchor and for \\b")
show("'^a'.sub('X', 'aaaaa', pos=1)", lambda: regex.compile("^a").sub("X", "aaaaa", pos=1))
show(r"'\ba'.sub('X', 'a aaa', pos=2)", lambda: regex.compile(r"\ba").sub("X", "a aaa", pos=2))
show(
    "'(?<=a)b'.sub('X', 'abab', pos=1)  [lookbehind reads before it]",
    lambda: regex.compile("(?<=a)b").sub("X", "abab", pos=1),
)

print()
print("4. the min-width shortcut is measured against the SLICE, so no template compile")
show(r"'xx'.sub('\\g<bad', 'xxxxx', pos=4)", lambda: regex.compile("xx").sub(r"\g<bad", "xxxxx", pos=4))
show(r"'xx'.sub('\\g<bad', 'xxxxx')", lambda: regex.compile("xx").sub(r"\g<bad", "xxxxx"))

print()
print("5. reversed")
show("'(?r)a'.sub('X', 'aaaaa', pos=1, endpos=3)", lambda: regex.compile("(?r)a").sub("X", "aaaaa", pos=1, endpos=3))
show("'(?r)a'.subn('X', 'aaaaa', pos=1, endpos=3)", lambda: regex.compile("(?r)a").subn("X", "aaaaa", pos=1, endpos=3))

print()
print("6. a zero-width pattern inside a slice")
show("'x*'.sub('-', 'abxd', pos=1, endpos=3)", lambda: regex.compile("x*").sub("-", "abxd", pos=1, endpos=3))
show("'x*'.sub('-', 'abxd')", lambda: regex.compile("x*").sub("-", "abxd"))

print()
print("7. an astral subject, so the UTF-16 translation is visible")
show(
    "'.'.subn('-', '\\U0001F600ab', pos=1, endpos=2)  [codepoints]",
    lambda: regex.compile(".").subn("-", "\U0001F600ab", pos=1, endpos=2),
)
