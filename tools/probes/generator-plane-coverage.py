"""Which generators actually draw astral characters, SMP digits, emoji clusters and `\\X`.

S52, 2026-09-15. This is the measurement the all-planes widening was scoped from, and it is a probe
rather than a scratch file because its BEFORE numbers are the evidence that the widening was needed
and its AFTER numbers are the evidence that it worked. Reconstructing either from prose is what made
S18's and S19's controls unreproducible.

    python tools/probes/generator-plane-coverage.py

It generates rows WITHOUT recording them - upstream is never asked - so it takes a few seconds and
needs no oracle. Rows per generator and the seeds are fixed below so two runs are comparable.

BEFORE (commit 58977bb, the same command):

    generator        astral subj  astral pat  SMP digit  emoji ZWJ    \\X
    literals                 430         270          0          0     0   of 1200
    classes                  300           0        125          0     0   of 1200
    groups                   281           0          0          0     0   of 1200
    quantifiers              222           0          0          0     0   of 1200
    boundaries               447         227          0        241   277   of 1200
    backrefs                 245           0          0          0     0   of 1200
    substitution             484           0          0          0     0   of 1200
    iteration                350           0          0          0     0   of 1200
    interactions             430         160          0          0     0   of 1200
    fuzzy                    118           0          0          0     0   of 1200

Astral SUBJECTS were already everywhere - 118 to 484 rows in every one of the 21 generators. What
was missing was SMP digits (only `classes` and `reverse`), emoji modifiers, ZWJ and `\\X` (only
`boundaries` and `reverse`), and above all an astral character in the PATTERN, which seven
generators never produced at all because they build patterns from a fixed atom list rather than
from a slice of the subject.
"""

import importlib.util
import pathlib
import random
import sys
import unicodedata

SEEDS = (7, 4242, 20260915)
COUNT = 400

_spec = importlib.util.spec_from_file_location(
    "record_oracle", pathlib.Path(__file__).resolve().parent.parent / "record-oracle.py"
)
_recorder = importlib.util.module_from_spec(_spec)
sys.modules["record_oracle"] = _recorder
_spec.loader.exec_module(_recorder)


def main() -> int:
    header = ("generator", "astral subj", "astral pat", "SMP digit", "emoji ZWJ", "\\X")
    print(f"{header[0]:<16} {header[1]:>11} {header[2]:>11} {header[3]:>10} {header[4]:>10} {header[5]:>5}")
    print("-" * 70)

    for name in _recorder.GENERATORS:
        subject_astral = pattern_astral = smp_digit = cluster = grapheme = total = 0
        for seed in SEEDS:
            for row in _recorder._generate(name, random.Random(f"{seed}:{name}"), COUNT):
                total += 1
                subject, pattern = row["subject"], row["pattern"]
                if any(ord(c) > 0xFFFF for c in subject):
                    subject_astral += 1
                if any(ord(c) > 0xFFFF for c in pattern):
                    pattern_astral += 1
                if any(ord(c) > 0xFFFF and unicodedata.category(c) == "Nd" for c in subject):
                    smp_digit += 1
                if "‍" in subject or any(0x1F3FB <= ord(c) <= 0x1F3FF for c in subject):
                    cluster += 1
                if "\\X" in pattern:
                    grapheme += 1
        print(
            f"{name:<16} {subject_astral:>11} {pattern_astral:>11} {smp_digit:>10} "
            f"{cluster:>10} {grapheme:>5}   of {total}"
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())
