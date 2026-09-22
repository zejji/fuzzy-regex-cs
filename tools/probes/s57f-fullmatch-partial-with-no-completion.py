r"""Upstream reports a partial fullmatch that no longer subject can ever complete.

Row 97332 of the 6000-row date-seed gate. `regex.compile(r'(\S??)\.').fullmatch('.a',
partial=True)` answers a partial over the whole of '.a', although the pattern matches at most two
characters and a two-character match must end in '.'. This port answers None, which is what
`upstream/README.rst:270` defines a partial to mean and what its own example at :288 does with the
negative case.

    python tools/probes/s57f-fullmatch-partial-with-no-completion.py

WHAT IT MEASURED, regex 2026.9.10, on 2026-09-22:

  regex 2026.9.10

  The drawn row and its minimisation
    '(\\S??)\\.\\b' '.\r'    I|M   PARTIAL (0, 2)
    '(\\S??)\\.'   '.a'           PARTIAL (0, 2)
    '(\\S??)[.]'   '.a'           PARTIAL (0, 2)
    '\\.'          '.a'           None
    '(\\S?)\\.'    '.a'           None
    '(\\S??)\\.'   '.'            match (0, 1)
    '(\\S??)\\.'   ''             PARTIAL (0, 0)

  Upstream against itself as the subject grows
    '(\\S??)\\.'   '.a'           PARTIAL (0, 2)
    '(\\S??)\\.'   '.ab'          None
    '(\\S??)ab'    'aba'          PARTIAL (0, 3)
    '(\\S??)ab'    'abab'         PARTIAL (0, 4)

  README.rst:288, the documented negative case
    '\\d{4}'       'a'            None

  Can any longer subject complete '(\S??)\.' over '.a'?
    alphabet '.aX \t', suffixes of length 0 to 3: 156 tried, 0 complete

Three things follow.

  * The lazy repeat is the whole of it. `\.` on '.a' is None on both engines, and the greedy `(\S?)`
    spelling is None too, so the partial needs the LAZY optional repeat in front of the literal.
  * It is not the required-string prefilter. `(\S??)[.]` has a character class where the string node
    was, and it answers the same phantom.
  * Upstream contradicts itself as the subject grows. '.ab' and 'abab' are both longer than their
    pattern's maximum width, so neither can complete; one is None and the other a partial. A rule
    would answer both the same way.

The brute-force line is the direct evidence for "no completion exists": every subject of the form
'.a' + s, for every s up to three characters over an alphabet holding the pattern's own literal, a
non-space, a letter outside it, a space and a tab, is asked as a complete fullmatch. None matches,
because `(\S??)\.` matches at most two characters and any two-character match ends in '.'.
"""

import itertools

import regex

print("regex", regex.__version__)

DRAWN = [
    # The row as the gate drew it: flags 0xa is IGNORECASE|MULTILINE, and the subject's second
    # character is a carriage return rather than a letter. Both are inert.
    (r"(\S??)\.\b", "\r".join([".", ""]), regex.I | regex.M, "I|M"),
    (r"(\S??)\.", ".a", 0, ""),
    (r"(\S??)[.]", ".a", 0, ""),     # a class, not a string node, so not the required-string search
    (r"\.", ".a", 0, ""),            # the repeat deleted: both engines say None
    (r"(\S?)\.", ".a", 0, ""),       # greedy rather than lazy
    (r"(\S??)\.", ".", 0, ""),       # a subject the pattern really does fullmatch
    (r"(\S??)\.", "", 0, ""),        # and one where a partial is genuinely available
]

GROWS = [
    (r"(\S??)\.", ".a"),
    (r"(\S??)\.", ".ab"),
    (r"(\S??)ab", "aba"),
    (r"(\S??)ab", "abab"),
]

DOCUMENTED = [(r"\d{4}", "a")]     # upstream/README.rst:288, "It'll never match."

ALPHABET = ".aX \t"


def show(match):
    if match is None:
        return "None"
    return f"{'PARTIAL' if match.partial else 'match'} {match.span()}"


def line(pattern, subject, flags=0, label=""):
    answer = regex.compile(pattern, flags).fullmatch(subject, partial=True)
    print(f"    {pattern!r:14} {subject!r:8} {label:5} {show(answer)}")


print("\n  The drawn row and its minimisation")
for pattern, subject, flags, label in DRAWN:
    line(pattern, subject, flags, label)

print("\n  Upstream against itself as the subject grows")
for pattern, subject in GROWS:
    line(pattern, subject)

print("\n  README.rst:288, the documented negative case")
for pattern, subject in DOCUMENTED:
    line(pattern, subject)

print(r"""
  Can any longer subject complete '(\S??)\.' over '.a'?""")
compiled = regex.compile(r"(\S??)\.")
tried = completed = 0
for width in range(4):
    for suffix in itertools.product(ALPHABET, repeat=width):
        tried += 1
        if compiled.fullmatch(".a" + "".join(suffix)) is not None:
            completed += 1
print(f"    alphabet {ALPHABET!r}, suffixes of length 0 to 3: "
      f"{tried} tried, {completed} complete")
