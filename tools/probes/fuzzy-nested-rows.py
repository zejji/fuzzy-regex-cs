"""Oracle rows where a nested fuzzy section is entered AFTER the outer one has spent an error.

That is the only shape in which the inner section starting from the outer's counts rather than
from zero changes an answer, and S38's `fuzzy` generator does not reach it: control E
(`nested-section-inherits-the-outer-counts`, which comments out the `Array.Clear(state.FuzzyCounts)`
in the forward `FUZZY` arm) is silent at both of its seeds on 600 generated rows.

Written out so the control stays reproducible. To re-run it::

    python tools/probes/fuzzy-nested-rows.py
    pwsh -File tools/run-oracle.ps1 -Rows .scratch/fuzzy-nested-rows.jsonl

Honest engine, 2026-09-13: 24 agree, 0 diverge. With the `Array.Clear` commented out: 3 agree,
21 diverge. The same shapes are pinned as a test in
`tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyMatchingTests.cs`.
"""

import json
import pathlib

CASES = [
    (r"(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=2}", "xxeg"),
    (r"(?:[ab](?:[cd][ef]){e<=1}[gh]){e<=2}", "xceg"),
    (r"(?:[ab](?:[cd][ef]){s<=1}[gh]){e<=3}", "xxxg"),
    (r"(?:[ab][cd](?:[ef][gh]){e<=1}){e<=3}", "xxxh"),
    (r"(?:[ab][cd](?:[ef][gh]){1<=e<=1}){e<=3}", "xxeh"),
    (r"(?:[ab](?:[cd]){e<=1}(?:[ef]){e<=1}){e<=3}", "xxx"),
    (r"(?:[ab](?:[cd][ef]){i<=1}[gh]){e<=2}", "xcxeg"),
    (r"(?:[ab](?:[cd][ef]){d<=1}[gh]){e<=2}", "xeg"),
]

rows = [
    {
        "generator": "fuzzy",
        "pattern": pattern,
        "flags": 0,
        "namedLists": {},
        "subject": subject,
        "operation": operation,
    }
    for pattern, subject in CASES
    for operation in ("search", "match", "fullmatch")
]

path = pathlib.Path(__file__).resolve().parents[2] / ".scratch" / "fuzzy-nested-rows.jsonl"
path.parent.mkdir(exist_ok=True)
path.write_text("\n".join(json.dumps(row) for row in rows) + "\n", encoding="utf-8")
print(len(rows), "rows ->", path)
