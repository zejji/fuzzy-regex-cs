"""Whole-plane sweep: does upstream regex's fold_case(FULL) disagree with CPython's
str.casefold() anywhere outside the two Turkic codepoints ledger entry 7 is about?

str.casefold() implements Unicode default full case folding (CaseFolding.txt C+F), so any
mismatch is a defect in the regex module's generated folding table, not a matter of
interpretation. This sweeps all 0x110000 codepoints (skipping the UTF-16 surrogate range,
which holds no assigned characters) rather than trusting that U+0049/U+0130 are the only
two rows affected.

Run it:

    python tools/probes/upstream-turkic-fold-sweep.py

Real run, 2026-09-14, regex 2026.9.10, CPython 3.14.6:

    fold_case(FULL) vs str.casefold(): 30 mismatches
      U+0049  regex=0049            casefold=0069
      U+0130  regex=0130            casefold=0069 0307
      U+A7CE  regex=A7CF            casefold=A7CE
      U+A7D2  regex=A7D3            casefold=A7D2
      U+A7D4  regex=A7D5            casefold=A7D4
      U+16EA0  regex=16EBB           casefold=16EA0
      U+16EA1  regex=16EBC           casefold=16EA1
      ... (23 more rows, all U+16EA2-U+16EB8, same shape: regex maps forward within a script
      block that casefold() maps to itself) ...
      U+16EB8  regex=16ED3           casefold=16EB8

    simple fold spot checks (fold_case(UNICODE|IGNORECASE)):
      U+0041 -> 0061
      U+0049 -> 0049
      U+0053 -> 0073
      U+0130 -> 0130
      U+0131 -> 0131
      U+1E9E -> 00DF
      U+03A3 -> 03C3
      U+03C2 -> 03C3

    Only the first two full-fold mismatches (U+0049, U+0130) are the Turkic divergence entry 7
    pins - those are exactly the T-row codepoints the port's TurkicDefaults.cs corrects. The
    other 28 (U+A7CE, U+A7D2, U+A7D4, and the U+16EA0-U+16EB8 run) are outside this probe's
    question and neither S45 nor entry 7 makes any claim about them. They are the OPPOSITE
    direction - `regex` folds U+A7CE to U+A7CF where `casefold` leaves it alone - and every one
    of them is a recent UCD addition, so a version skew between CPython's bundled tables and the
    regex module's generated ones is the likely explanation. **That has not been measured**, and
    the 28 have never been triaged. They belong to the Phase 6 issue sweep (S49/S50), recorded
    here so they are not lost.
"""
import sys
from regex import _regex
from regex._regex_core import FULL_CASE_FOLDING as FULL, UNICODE, IGNORECASE

SIMPLE = UNICODE | IGNORECASE

def h(s):
    return ' '.join('%04X' % ord(c) for c in s)

full_diffs = []
for cp in range(0x110000):
    if 0xD800 <= cp <= 0xDFFF:
        continue
    c = chr(cp)
    got = _regex.fold_case(FULL, c)
    want = c.casefold()
    if got != want:
        full_diffs.append((cp, got, want))

print('fold_case(FULL) vs str.casefold(): %d mismatches' % len(full_diffs))
for cp, got, want in full_diffs[:60]:
    print('  U+%04X  regex=%-14s  casefold=%s' % (cp, h(got), h(want)))

# Simple fold sanity: a handful of known values.
print()
print('simple fold spot checks (fold_case(UNICODE|IGNORECASE)):')
for cp in (0x41, 0x49, 0x53, 0x130, 0x131, 0x1E9E, 0x3A3, 0x3C2):
    print('  U+%04X -> %s' % (cp, h(_regex.fold_case(SIMPLE, chr(cp)))))
