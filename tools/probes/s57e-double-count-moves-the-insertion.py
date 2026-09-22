"""Ledger entry 12's doubled term is what moves the recorded insertion, measured on upstream.

The row, from `pwsh -File tools/run-oracle.ps1 -Generator fuzzy-anchored -Count 2000
-Seeds 1234567`, row 1982:

    (?b)(?r)\\m(?:.fo){e<=2}  as a finditer over 'x fx'
    upstream  span=(0, 4) counts=(1, 1, 0) changes=([4], [2], [])
    this port span=(0, 4) counts=(1, 1, 0) changes=([4], [1], [])

Nothing is lost here. Both engines answer the same span at the same cost and put the
substitution in the same place; they disagree only about where the one insertion is recorded,
and two alignments of that cost exist for this subject.

END_FUZZY's backtrack arm is the only place a trailing insertion can come from, and upstream
guards it with

    total_errors(state->fuzzy_counts) + total_errors(inner_counts) < state->max_errors

at upstream/src/_regex.c:15516-15519. END_FUZZY merged `inner_counts` into
`state->fuzzy_counts` on the way in (:12475-12513), so the two terms are the same errors
added twice. Ledger entry 12 is that double count, and S46 dropped the second term from this
port on 2026-09-14.

This probe builds upstream with the second term deleted and runs the row. If the double count
is what separates the engines, the patched build answers where this port answers.

The port side of the same experiment cannot be run from here; it is one edit in
src/FuzzyRegex/Engine/Matcher.cs, recorded as a control in the S57e closing notes.

Three more rows run through the same two builds, and they are a different symptom. Putting
`fuzzy-anchored` on the default generator list gave the 6000-row gate three questions where
upstream under `(?b)` answers a match this port beats: three substitutions where this port spends
one insertion and one deletion, or one substitution and one insertion. Upstream's README calls
`BESTMATCH` a search for the best match, so an answer costing three errors where one costing two
exists is upstream failing its own rule. The flagless control cannot settle these, because a
flagless engine returns the FIRST match it finds and never claims to rank. The patched build can,
and does: with the doubled term deleted, upstream answers this port's answer on all three
(positions in codepoints, as upstream reports them; this port reports UTF-16 code units).

    row 1  (?b)\\B(?:\U0001f600\U0001d7eex\\s){e<=3}      over '\U0001f600x \U0001f600'
           shipped (0, 4) (3, 0, 0) s:1,2,3   patched (0, 4) (0, 1, 1) i:3 d:1
    row 2  (?b)(?i)\\m(a\U0001f600)(?:(?:\\1)){e<=3:\\w}  over 'a\U0001f600\U0001f600b'
           shipped None                       patched (0, 4) (0, 1, 1) i:3 d:2
    row 3  (?b)(?e)\\b(?:\\d+\\d\\s){e<=3}                over '215x b'
           shipped (0, 6) (3, 0, 0) s:3,4,5   patched (0, 6) (1, 1, 0) s:3 i:5

The port side of those three is the same one-line control, run over
tools/probes/s57e-gate-rows.jsonl: restoring the doubled term makes this port answer what the
shipped upstream answers, 3 rows agreeing where 0 did.

Build machinery is S47c's, imported from tools/probes/upstream-bestmatch-lost-candidate.py.

Usage: python tools/probes/s57e-double-count-moves-the-insertion.py
Measured 2026-09-22 on regex 2026.9.10 and Python 3.14.
"""

import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
PROBE = os.path.join(HERE, "upstream-bestmatch-lost-candidate.py")

spec = importlib.util.spec_from_file_location("lostcand", PROBE)
lostcand = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lostcand)

# END_FUZZY's "try another insertion" backtrack arm, with the doubled term removed.
GUARD_ANCHOR = """            if (insertion_permitted(state, inner_node, inner_counts) &&
              total_errors(state->fuzzy_counts) + total_errors(inner_counts) <
              state->max_errors && fuzzy_ext_match(state, inner_node,
              state->text_pos)) {"""

GUARD = """            if (insertion_permitted(state, inner_node, inner_counts) &&
              total_errors(state->fuzzy_counts) <
              state->max_errors && fuzzy_ext_match(state, inner_node,
              state->text_pos)) {"""

# The row, plus the controls that say what the term does and does not reach. The flagless row is
# the discriminator: deleting a term from a guard can only ever ALLOW more insertions, so if the
# flagless answer moved too, the term would be doing something wider than entry 12 claims.
ROWS = r"""
import regex
print('    using', regex._regex.__file__)

rows = [
    ('THE ROW', 'search', r'(?b)(?r)\m(?:.fo){e<=2}', 'x fx'),
    ('control: the row without (?b)', 'search', r'(?r)\m(?:.fo){e<=2}', 'x fx'),
    ('control: the row without (?r)', 'search', r'(?b)\m(?:.fo){e<=2}', 'x fx'),
    ('control: entry 12s lost match', 'fullmatch', r'(?b)(?:x){e<=3}', 'xyz'),
    ('control: that row without (?b)', 'fullmatch', r'(?:x){e<=3}', 'xyz'),
]
for label, operation, pattern, subject in rows:
    m = getattr(regex, operation)(pattern, subject)
    if m is None:
        print('    %-32s %-26s -> None' % (label, pattern))
    else:
        print('    %-32s %-26s -> span=%s counts=%s changes=%s'
          % (label, pattern, m.span(), m.fuzzy_counts, m.fuzzy_changes))
"""


# The three rows the 6000-row gate drew once `fuzzy-anchored` joined the default generator list.
# They are not the moved-insertion shape: on each of them this port answers a CHEAPER match than
# upstream does, and upstream's own flagless answer is no help, because a flagless engine returns
# the first match it finds and never claims to be ranking. The question is whether the cheaper
# match is one the doubled term refuses, so each row is printed against both builds. Patterns and
# subjects go through `ascii()` because two of them are astral and the console is cp1252.
GATE_ROWS = r"""
import regex
print('    using', regex._regex.__file__)

rows = [
    ('gate row 1', 'fullmatch', '(?b)\\B(?:\U0001f600\U0001d7eex\\s){e<=3}', '\U0001f600x \U0001f600'),
    ('gate row 2', 'fullmatch', '(?b)(?i)\\m(a\U0001f600)(?:(?:\\1)){e<=3:\\w}', 'a\U0001f600\U0001f600b'),
    ('gate row 3', 'fullmatch', '(?b)(?e)\\b(?:\\d+\\d\\s){e<=3}', '215x b'),
]
for label, operation, pattern, subject in rows:
    m = getattr(regex, operation)(pattern, subject)
    print('    %s %s over %s' % (label, ascii(pattern), ascii(subject)))
    if m is None:
        print('        -> None')
    else:
        print('        -> span=%s counts=%s changes=%s'
          % (m.span(), m.fuzzy_counts, m.fuzzy_changes))
"""


def main():
    # The repo root carries no `regex` package, so this run picks up the installed one.
    lostcand.say("--- upstream as shipped ---")
    out, err = lostcand.run_in(ROOT, ROWS)
    lostcand.say(out)
    lostcand.say(err)
    out, err = lostcand.run_in(ROOT, GATE_ROWS)
    lostcand.say(out)
    lostcand.say(err)

    lostcand.say("--- upstream with the doubled term deleted from END_FUZZY ---")
    tree = lostcand.build("regex-s57e-nodoublecount", [(GUARD_ANCHOR, GUARD)])
    out, err = lostcand.run_in(tree, ROWS)
    lostcand.say(out)
    lostcand.say(err)
    out, err = lostcand.run_in(tree, GATE_ROWS)
    lostcand.say(out)
    lostcand.say(err)


if __name__ == "__main__":
    sys.exit(main())
