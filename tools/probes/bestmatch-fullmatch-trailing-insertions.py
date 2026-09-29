"""Is `regex.fullmatch(r'(?b)(?:b){e<=2}', 'bba')` returning None a new upstream bug?

Written 2026-09-28 against `regex` 2026.9.10, to triage a candidate reported as "BESTMATCH
retries under fullmatch lose a match". The answer this probe gives is NO, IT IS NOT NEW: it
is ledger entry 12, the doubled trailing-insertion guard at `_regex.c:15516-15517`, in the
shape of entry 12's own reproduction `(?b)(?:x){e<=3}` over 'xyz'. The body happening to
equal the first trailing character ('b' over 'bba') changes nothing.

Run it:

    python tools/probes/bestmatch-fullmatch-trailing-insertions.py

What each block isolates, and what 2026.9.10 answers:

1. The flag. Plain and `(?e)` fullmatch (0, 3) with two insertions; `(?b)` answers None under
   `{e<=2}`, `{i<=2}` and `{e<=9}`, so the budget is not the limit.
2. The door. `match` and `search` answer (0, 1) with no error under every flag, which is
   right: a match free to stop early needs no insertion. Only something that forces the
   match to reach the end - `fullmatch`, or a trailing `$` under plain `match` - needs the
   trailing insertions, and `(?b)` loses the match under BOTH. So the defect is not in
   fullmatch's retries: `$` reaches it with no fullmatch at all.
3. The error kind. 'abb' is lost too: its flagless fit is insertions at 0 and 2, and the one
   at 2 is trailing. Substitution and deletion budgets cannot fullmatch these subjects at
   all, with the flag or without it, so insertions are the only kind in play.
4. The count. One trailing insertion ('bb') survives `(?b)`; two do not. That is entry 12's
   `k <= 1` boundary for a width-1 body.
5. The slice. `pos`/`endpos` round the same question move nothing.
6. `(?r)`. Reversed, the same two-insertion fit is lost in the same way.

With `--patched` it also builds upstream with the doubled term deleted from END_FUZZY's
backtrack arm (`_regex.c:15516-15517`), using S47c's machinery in
upstream-bestmatch-lost-candidate.py (needs the Visual Studio C++ build tools), and runs the
same rows against it. That is the upstream half of the attribution; the port half is
`PatternObject.DoubleCountTrailingInsertions` in FuzzyBestMatchTests.
"""

import importlib.util
import os
import shutil
import sys

import regex


def show(m):
    if m is None:
        return "None"
    return f"{m.span()} counts={m.fuzzy_counts} changes={m.fuzzy_changes}"


def block(title, rows):
    print(title)
    for label, fn in rows:
        print(f"  {label:<46} {show(fn())}")
    print()


print(f"regex {regex.__version__}\n")

block("1. flag, fullmatch over 'bba'", [
    (f"{flag}(?:b){{{budget}}}", lambda flag=flag, budget=budget:
        regex.fullmatch(f"{flag}(?:b){{{budget}}}", "bba"))
    for flag in ("", "(?e)", "(?b)")
    for budget in ("e<=2", "i<=2", "e<=9")
])

block("2. door, (?:b){e<=2} over 'bba'", [
    (f"{flag} {door}", lambda flag=flag, door=door: {
        "match": lambda p: regex.match(p, "bba"),
        "search": lambda p: regex.search(p, "bba"),
        "fullmatch": lambda p: regex.fullmatch(p, "bba"),
        "match endpos=3": lambda p: regex.compile(p).match("bba", 0, 3),
        "match $": lambda p: regex.match(p + "$", "bba"),
    }[door](f"{flag}(?:b){{e<=2}}"))
    for flag in ("", "(?b)")
    for door in ("match", "search", "fullmatch", "match endpos=3", "match $")
])

block("3. error kind, fullmatch", [
    (f"{flag}(?:b){{{budget}}} over {subject!r}", lambda flag=flag, budget=budget, subject=subject:
        regex.fullmatch(f"{flag}(?:b){{{budget}}}", subject))
    for subject in ("abb", "bba")
    for budget in ("s<=2", "d<=2", "i<=2")
    for flag in ("", "(?b)")
])

block("4. count, (?:b){e<=2} fullmatch", [
    (f"{flag} over {subject!r}", lambda flag=flag, subject=subject:
        regex.fullmatch(f"{flag}(?:b){{e<=2}}", subject))
    for subject in ("b", "bb", "ba", "bba", "baa", "bbb")
    for flag in ("", "(?b)")
])

block("5. slice, (?b)(?:b){e<=2} fullmatch", [
    ("over 'xbbay' pos=1 endpos=4", lambda: regex.compile(r"(?b)(?:b){e<=2}").fullmatch("xbbay", 1, 4)),
    ("over 'bbay' endpos=3", lambda: regex.compile(r"(?b)(?:b){e<=2}").fullmatch("bbay", 0, 3)),
    ("flagless over 'xbbay' pos=1 endpos=4", lambda: regex.compile(r"(?:b){e<=2}").fullmatch("xbbay", 1, 4)),
])

block("6. reversed, fullmatch over 'bba'", [
    (f"{flag}(?r)(?:b){{e<=2}}", lambda flag=flag: regex.fullmatch(f"{flag}(?r)(?:b){{e<=2}}", "bba"))
    for flag in ("", "(?e)", "(?b)")
])


PATCHED_ROWS = r"""
import regex
print('  using', regex._regex.__file__)
for op, pattern, subject in [
    ('fullmatch', r'(?b)(?:b){e<=2}', 'bba'),
    ('fullmatch', r'(?b)(?:b){i<=2}', 'bba'),
    ('fullmatch', r'(?b)(?r)(?:b){e<=2}', 'bba'),
    ('match', r'(?b)(?:b){e<=2}$', 'bba'),
    ('fullmatch', r'(?b)(?:b){e<=2}', 'abb'),
    ('fullmatch', r'(?:b){e<=2}', 'bba'),
    ('fullmatch', r'(?b)(?:b){e<=1}', 'bba'),
]:
    m = getattr(regex, op)(pattern, subject)
    print('  %-9s %-22s %-5s %s' % (op, pattern, subject,
        None if m is None else (m.span(), m.fuzzy_counts, m.fuzzy_changes)))
"""

if "--patched" in sys.argv:
    here = os.path.dirname(os.path.abspath(__file__))
    spec = importlib.util.spec_from_file_location(
        "lostcand", os.path.join(here, "upstream-bestmatch-lost-candidate.py"))
    lostcand = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(lostcand)
    anchor = """              total_errors(state->fuzzy_counts) + total_errors(inner_counts) <
              state->max_errors && fuzzy_ext_match(state, inner_node,"""
    fixed = """              total_errors(state->fuzzy_counts) <
              state->max_errors && fuzzy_ext_match(state, inner_node,"""
    print("7. upstream with the doubled term deleted")
    tree = lostcand.build("regex-bestmatch-fullmatch", [(anchor, fixed)])
    out, err = lostcand.run_in(tree, PATCHED_ROWS)
    print(out + err)
    shutil.rmtree(tree)
