"""Hand-built oracle rows for the two S39 controls the `fuzzy` generator barely reaches.

The generator draws `(?fi)` and a backreference independently, so a fuzzy REF_GROUP_FLD that has to
spend a DELETION is about one row in six hundred - control S39-E fired on 0 and 1 of 600 at the two
seeds it was run at, which is a coincidence rather than a measurement. Control S39-D, an error
landing at the very end of a subject character's folding, is nearly as thin.

These rows make both shapes the normal case instead of the exception. Every one has a pattern whose
literal side needs the full fold on exactly one character (so `Sequence._fix_full_casefold`'s known
upstream bug, S35, cannot fire) and a subject built so the match needs the error kind the control
breaks.

Run::

    python tools/probes/fuzzy-group-fld-rows.py
    pwsh -File tools/run-oracle.ps1 -Rows .scratch/fuzzy-group-fld-rows.jsonl
"""

import json
import pathlib

# (group text, reference subject, constraint). The group's text folds longer than it is written, so
# the reference walks two foldings at different speeds - which is the only thing
# `fuzzy_match_group_fld` exists for.
GROUP_ROWS = [
    ("straße", "STRASE", "{d<=1}"),
    ("straße", "STRAS", "{d<=2}"),
    ("straße", "TRASSE", "{d<=1}"),
    ("maße", "MASE", "{d<=1}"),
    ("maße", "MAS", "{d<=2}"),
    ("ßa", "SA", "{d<=1}"),
    ("ßab", "SAB", "{d<=1}"),
    ("ﬆx", "STX", "{d<=1}"),
    ("ﬆx", "SX", "{d<=1}"),
    ("ﬆxy", "STY", "{d<=1}"),
    ("aßa", "ASSA", "{d<=1}"),
    ("aßa", "ASA", "{d<=1}"),
    ("aßa", "ASSAB", "{e<=1}"),
    ("ßa", "SSAX", "{e<=1}"),
]

# (pattern literal, subject, constraint) for the STRING_FLD half: the error has to land at the LAST
# position of a subject character's folding, which is the bound control S39-D tightens.
STRING_ROWS = [
    ("straße", "STRASSX", "{s<=1}"),
    ("straße", "STRASXE", "{s<=1}"),
    ("straße", "STRAXSE", "{s<=1}"),
    ("ßa", "SXA", "{s<=1}"),
    ("ßa", "XSA", "{s<=1}"),
    ("ßax", "SSXX", "{s<=1}"),
    ("ﬆx", "STY", "{s<=1}"),
    ("ﬆx", "SXX", "{s<=1}"),
    ("xﬆ", "XSY", "{s<=1}"),
    ("ßa", "SS", "{d<=1}"),
    # The insertion half. An insertion whose new position is the LAST index of the current
    # folding is exactly the bound control S39-D tightens, so every one of these puts a spare
    # character next to a character whose folding is longer than one.
    ("ßa", "SSAA", "{i<=1}"),
    ("ßa", "SSXA", "{i<=1}"),
    ("ßa", "XSSA", "{i<=1}"),
    ("ßa", "SXSA", "{i<=1}"),
    ("ßa", "SSAXX", "{i<=2}"),
    ("aßa", "AXSSA", "{i<=1}"),
    ("aßa", "ASXSA", "{i<=1}"),
    ("aßa", "ASSXA", "{i<=1}"),
    ("ﬆx", "STXA", "{i<=1}"),
    ("ﬆx", "SXTX", "{i<=1}"),
    ("ﬆx", "XSTX", "{i<=1}"),
    ("ﬆx", "STXX", "{i<=1}"),
    ("xﬆx", "XSXTX", "{i<=1}"),
    ("straße", "STRASSXE", "{i<=1}"),
    ("straße", "STRASXSE", "{i<=1}"),
    ("maße", "MASSXE", "{i<=1}"),
    ("maße", "MASXSE", "{i<=1}"),
    ("maße", "MAXSSE", "{i<=1}"),
]

OPERATIONS = ("search", "match", "fullmatch", "finditer")

rows = []

for group, reference, constraint in GROUP_ROWS:
    pattern = f"(?fi)({group})(?:\\1){constraint}"
    for operation in OPERATIONS:
        rows.append(
            {
                "generator": "fuzzy-group-fld-probe",
                "pattern": pattern,
                "flags": 0,
                "namedLists": {},
                "subject": group + reference,
                "operation": operation,
            }
        )

for literal, subject, constraint in STRING_ROWS:
    pattern = f"(?fi)(?:{literal}){constraint}"
    for operation in OPERATIONS:
        rows.append(
            {
                "generator": "fuzzy-string-fld-probe",
                "pattern": pattern,
                "flags": 0,
                "namedLists": {},
                "subject": subject,
                "operation": operation,
            }
        )

out = pathlib.Path(".scratch/fuzzy-group-fld-rows.jsonl")
out.parent.mkdir(exist_ok=True)
with out.open("w", encoding="utf-8") as handle:
    for row in rows:
        handle.write(json.dumps(row) + "\n")

print(f"wrote {out}: {len(rows)} rows")
