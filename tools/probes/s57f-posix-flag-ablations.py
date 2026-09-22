"""Which flags does each of S57f's two POSIX rows need, measured on upstream alone?

Rows 73420 and 76118 of the 6000-row gate at seed 20260922 are both pinned under
`posix-fuzzy-contradicts-its-own-flagless-answer`. This probe shows what upstream itself answers as
each flag is taken away, which is the half of the ablation that does not need the port.

Row 73420 is the conjunction shape: BESTMATCH and POSIX together destroy a replacement that either
one alone still makes, and both of upstream's own bestmatch-free and POSIX-free answers are the
single replacement this port makes.

Row 76118 is the over-charge shape: upstream charges one substitution to a span its own POSIX-free
engine matches exactly. Note the last block. Dropping `(?e)` or `(?r)` does not move upstream at
all - it answers (0, 2) with one substitution under every spelling but the POSIX-free one - although
the two engines do agree once either is dropped. So those two flags change this port's answer, not
upstream's, and only POSIX changes upstream's.

Measured 2026-09-22, regex 2026.9.10:

    === row 73420, sub over '\U0001d518\U00010400\U0001d518\U00010400\U00010400' ===
    as drawn      0 replacements, subject unchanged
    no (?b)       1 replacement  '\U00010400\tA'
    no (?p)       1 replacement  '\U00010400\tA'
    no IGNORECASE 0 replacements, subject unchanged

    === row 76118, finditer over ' A' ===
    as drawn     [((0, 2), (1, 0, 0))]
    no (?e)      [((0, 2), (1, 0, 0))]
    no (?r)      [((0, 2), (1, 0, 0))]
    no POSIX     [((0, 2), (0, 0, 0))]

Run: python tools/probes/s57f-posix-flag-ablations.py
"""

import regex

POSIX = 0x10000
IGNORECASE = 0x2

ROW_73420_PATTERN = (
    r"(?b)(?r)(?p)^\L<w1>{1<=e<=2}(?:\p{Lu}(\p{ASCII})\s){s<=1,i<=1,d<=1}$"
)
ROW_73420_SUBJECT = "\U0001d518\U00010400\U0001d518\U00010400\U00010400"
ROW_73420_TEMPLATE = "\\1\\t\\x41"  # the eight characters the row records, escapes and all
ROW_73420_W1 = ["\U00010400", "\U0001d518"]

ROW_76118_PATTERN = r"(?e)(?r)^[^a-f]?(?(?=\w)\D)(?:A[A-Z]?){e<=2,s<=1}$"
ROW_76118_SUBJECT = " A"
ROW_76118_FLAGS = 65546  # IGNORECASE | MULTILINE | POSIX


def show_sub(label, pattern, flags):
    compiled = regex.compile(pattern, flags=flags, w1=ROW_73420_W1)
    out, count = compiled.subn(ROW_73420_TEMPLATE, ROW_73420_SUBJECT, count=1)
    if count == 0:
        print(f"{label:13} 0 replacements, subject unchanged")
    else:
        print(f"{label:13} {count} replacement  {ascii(out)}")


def show_finditer(label, pattern, flags):
    ms = list(regex.compile(pattern, flags=flags).finditer(ROW_76118_SUBJECT))
    print(f"{label:12} {[(m.span(), m.fuzzy_counts) for m in ms]}")


print(f"=== row 73420, sub over {ascii(ROW_73420_SUBJECT)} ===")
show_sub("as drawn", ROW_73420_PATTERN, IGNORECASE)
show_sub("no (?b)", ROW_73420_PATTERN.replace("(?b)", ""), IGNORECASE)
show_sub("no (?p)", ROW_73420_PATTERN.replace("(?p)", ""), IGNORECASE)
show_sub("no IGNORECASE", ROW_73420_PATTERN, 0)

print()
print(f"=== row 76118, finditer over {ascii(ROW_76118_SUBJECT)} ===")
show_finditer("as drawn", ROW_76118_PATTERN, ROW_76118_FLAGS)
show_finditer("no (?e)", ROW_76118_PATTERN.replace("(?e)", ""), ROW_76118_FLAGS)
show_finditer("no (?r)", ROW_76118_PATTERN.replace("(?r)", ""), ROW_76118_FLAGS)
show_finditer("no POSIX", ROW_76118_PATTERN, ROW_76118_FLAGS & ~POSIX)
