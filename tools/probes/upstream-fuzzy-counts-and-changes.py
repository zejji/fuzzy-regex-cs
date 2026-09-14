"""Upstream's answers to ledger entry 11's mechanisms C and D, and to the new finding S48b's fix
turned up: a POSIX + BESTMATCH overlapped scan loses its LONGEST match, which is the one thing
leftmost-longest promises.

Run:  python tools/probes/upstream-fuzzy-counts-and-changes.py

The port half is tools/probes/port-fuzzy-counts-and-changes.ps1. Spans here are CODEPOINT indices,
where the port's are UTF-16 code units, so the astral rows need converting before the two are read
side by side.

`fuzzy_changes` is never read on a POSIX row: that access faults the interpreter with 0xC0000005
and no `except` clause can see it (ledger entry 9). Every POSIX row below therefore shows the span
and the counts only, which upstream computes correctly and which the probe prints before it could
die.
"""

import sys

sys.stdout.reconfigure(encoding="utf-8")

import regex

print("regex", regex.__version__)

# Flags, as the ledger records them against each row.
C_WORST_FLAGS = 258  # 0x102: VERSION1 | IGNORECASE
C_TWIN_FLAGS = 16642  # 0x4102: FULLCASE | VERSION1 | IGNORECASE
D_FLAGS = 130  # 0x82: ASCII | IGNORECASE


def show(label, pattern, subject, flags, overlapped=False, limit=12):
    print(f"\n{label}")
    compiled = regex.compile(pattern, flags)
    posix = bool(compiled.flags & regex.POSIX)
    rows = []
    for n, m in enumerate(compiled.finditer(subject, overlapped=overlapped), 1):
        # Reading fuzzy_changes on a POSIX row takes the process down - ledger entry 9.
        extra = "" if posix else f" {m.fuzzy_changes}"
        rows.append(f"({m.start()},{m.end()}) {m.fuzzy_counts}{extra}")
        if n >= limit:
            break
    print("  " + ("  ".join(rows) if rows else "no matches"))


print("\n===== ledger 11 mechanism C, the worst measured case =====")
print("Overlapped finditer, flags 258. Spans are codepoints: (3,5) and (3,4) are the port's (4,8)")
print("and (4,6) in UTF-16. Counts agree with the port; upstream's own change KINDS, read with")
print("POSIX removed so they can be read at all, are what the port used to report.")
C_WORST = (
    r"(?r)(?p)(?!(?:[^[\p{L}--[a-z]]]\w([\p{L}||\p{N}])){s<=1})"
    r"(?:([a]+?)(?P<g3>\p{L})){1i+2d+1s<=3:[^a-z]}"
)
C_WORST_SUBJECT = "ﬃﬃ\U00010400\U00010400\U00010400"
show("  as recorded, with (?p)", C_WORST, C_WORST_SUBJECT, C_WORST_FLAGS, overlapped=True)
show("  (?p) removed, so the changes can be read", C_WORST.replace("(?p)", ""), C_WORST_SUBJECT, C_WORST_FLAGS, overlapped=True)

print("\n===== ledger 11 mechanism C's twin, and UPSTREAM CONTRADICTING ITSELF =====")
print("Four doors onto one subject. (0,9) - the whole subject, and the LONGEST match there is -")
print("is answered by three of them and dropped by the POSIX+BESTMATCH scan alone. Upstream's own")
print("fullmatch at those very flags answers (0,9), which is what makes this upstream's bug rather")
print("than a ranking choice: POSIX is leftmost-LONGEST, so the longest match is the one row it")
print("owes the caller above all others. This port answers (0,9) at all four doors since S48b.")
C_TWIN_BASE = r"(\w)(?:\s(?:([\p{L}\p{N}]{2,})){e<=2,s<=1}){1<=e<=2}"
C_TWIN_SUBJECT = "A\rA\xdf\xdf aaa"
for prefix in ("(?b)(?r)(?p)", "(?b)(?r)", "(?r)(?p)", "(?r)"):
    show(f"  prefix {prefix}", prefix + C_TWIN_BASE, C_TWIN_SUBJECT, C_TWIN_FLAGS, overlapped=True)

print("\n  anchored, upstream - the question that settles it:")
for prefix in ("(?b)(?r)(?p)", "(?b)(?r)", "(?r)"):
    m = regex.compile(prefix + C_TWIN_BASE, C_TWIN_FLAGS).fullmatch(C_TWIN_SUBJECT)
    answer = None if m is None else ((m.start(), m.end()), m.fuzzy_counts)
    print(f"    fullmatch {prefix:<14} -> {answer}")

print("\n===== ledger 11 mechanism D, still unfixed upstream =====")
print("One substitution counted, one DELETION reported, on the search AND on the anchored retry -")
print("so S47's leak-free question does not resolve it and upstream has no answer to compare the")
print("port's change list against. The port reports a substitution, agreeing with its own counts.")
D_PATTERN = "(?e)([abz])[a\\d]{0,}?(?<=(?:(\\d?)[A-Z]\U0001F600){s<=1,i<=1,d<=1})\\b"
D_SUBJECT = "\U0001F600\r\n\U0001F600AA"
compiled = regex.compile(D_PATTERN, D_FLAGS)
m = compiled.search(D_SUBJECT)
print(f"  search:    ({m.start()}, {m.end()}) counts {m.fuzzy_counts} changes {m.fuzzy_changes}")
a = compiled.match(D_SUBJECT, m.start(), m.end())
print(f"  anchored:  ({a.start()}, {a.end()}) counts {a.fuzzy_counts} changes {a.fuzzy_changes}")

print("\n===== ledger 9's remaining port-side count bug, upstream's side of it =====")
print("Upstream answers two errors under POSIX; this port answered three before S48b, for the same")
print("span its own flagless engine fits in two. fuzzy_changes is not read: POSIX row.")
L9 = r"(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}"
for label, flags in (("POSIX", regex.POSIX), ("no POSIX", 0)):
    m = regex.compile(L9, flags).fullmatch("+ aBA")
    if m is None:
        print(f"  {label:<9} no match")
    else:
        extra = "" if flags & regex.POSIX else f" changes {m.fuzzy_changes}"
        print(f"  {label:<9} ({m.start()}, {m.end()}) counts {m.fuzzy_counts}{extra}")
