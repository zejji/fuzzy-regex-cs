r"""Upstream's half of `skip-moved-slice-changes-the-match-set`, seed 20260920 row 74399.

A reversed OVERLAPPED scan whose pattern carries a `(*SKIP)`. Upstream reports four matches and this
port reports three, and the two scans are not a prefix of one another: upstream's second and third
matches are at codepoints (2, 4) and (2, 3) with no errors, where this port's second is at (1, 3)
with one substitution. So `overlapped-skip-extra-match-reversed`, which demands that this port's
scan be a prefix of upstream's and that each extra match refute itself, cannot take the row.

What this probe shows is that upstream's own two verb controls both answer this port's scan exactly,
match for match and error for error:

  * `(*SKIP)` spelled `(*PRUNE)` - the same pruning of backtracking, without the one thing `(*SKIP)`
    adds, which is moving a slice bound (upstream/src/_regex.c:14553 reversed, :14555 forwards);
  * the verb deleted altogether.

It also shows why the `$`-style assertion tell that judges the neighbouring rows is unavailable
here: the pattern ends in `\b`, and `\b` is true in the untruncated subject at both of the positions
upstream's extra matches end at, so nothing about those matches refutes itself.

    python tools/probes/s57b-skip-moved-slice-changes-the-match-set.py

POSITIONS ARE PYTHON CODEPOINTS. The subject is four astral characters after an 'a', so the oracle
report's UTF-16 numbers are larger; each match prints both.

The port half is the row's own `pruneOutcome` replayed through the real comparer,
`pwsh -File tools/run-oracle.ps1 -Rows tools/probes/s57b-gate-rows.jsonl`. Measured 2026-09-21
against regex 2026.9.10.
"""

import regex

PATTERN = r"(?r)(?:\s??(*SKIP)[^a]|[\w--[0-9]])\L<w1>{s<=1:\w}\b"
SUBJECT = "a\U0001f3fb\U00010400\U0001f600\U0001d518"
FLAGS = 0x100
LISTS = {"w1": ["‍‍", "\U0001d518aA", "\U0001f3fb", "\U0001f600\U0001d518"]}


def utf16(position: int) -> int:
    return len(SUBJECT[:position].encode("utf-16-le")) // 2


def describe(m) -> str:
    start, end = m.span(0)
    errors = ""
    if m.fuzzy_counts != (0, 0, 0):
        kinds = ("s", "i", "d")
        listed = " ".join(f"{k}:{list(p)}" for k, p in zip(kinds, m.fuzzy_changes) if p)
        errors = f" counts={m.fuzzy_counts} {listed}"
    return f"({start}, {end})/utf16({utf16(start)}, {utf16(end)}){errors}"


def scan(label: str, pattern: str) -> None:
    compiled = regex.compile(pattern, FLAGS, **LISTS)
    found = list(compiled.finditer(SUBJECT, overlapped=True))
    print(f"  {label:<20} {len(found)} | " + " || ".join(describe(m) for m in found))


def main() -> int:
    print("regex", regex.__version__)
    print(f"subject {ascii(SUBJECT)}   {len(SUBJECT)} codepoints, {utf16(len(SUBJECT))} UTF-16 units")

    print("\nTHE SCAN, and upstream's own two verb controls")
    scan("as drawn", PATTERN)
    scan("(*SKIP) -> (*PRUNE)", PATTERN.replace("(*SKIP)", "(*PRUNE)"))
    scan("verb deleted", PATTERN.replace("(*SKIP)", ""))

    # The tell the neighbouring entries use, and why it says nothing here: `\b` holds at both of the
    # codepoints upstream's extra matches end at, so neither match needs a moved bound to be legal.
    print("\nWHERE `\\b` IS TRUE in the untruncated subject")
    holds = [p for p in range(len(SUBJECT) + 1) if regex.compile(r"\b", FLAGS).match(SUBJECT, p)]
    print(f"  codepoints {holds}")
    print("  upstream's extra matches end at codepoints 4 and 3, both in that list")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
