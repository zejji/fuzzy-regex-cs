"""The harness's validation controls: one row per check that must fire, one that must stay quiet.

    python tools/matrix/controls.py OUT.jsonl
    python tools/matrix/run.py OUT.jsonl --run controls --chunk 100

A control that must fire is a known bug, either still on main or switched back on for that row
alone through the oracle's own ablation switches (the row's `ablate` field, read by
port-runner.cs); nothing in src/ is reverted. `expect` names the check and the status it must get.
"""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import tagger  # noqa: E402

CONTROLS = [
    # C1: an ExpectedDivergences example (ledger entry 42, fuzzy-exact-item-offered-as-a-deletion).
    ({"pattern": "(?:a){d<=1}a", "subject": "a", "operation": "match"}, "C1", "expected"),
    ({"pattern": "a", "subject": "xa", "operation": "search"}, "C1", "pass"),
    # C1 on finditer rows with a slice, which the recorder asks since 2026-10-01 (they were C1x's):
    # ledger entry 42's pinned divergence (the port deletes an exactly matched item, upstream never
    # does). Measured 2026-09-30: port (0,2) with one deletion then (3,6), upstream (3,6) only.
    ({"pattern": "(?:ab){d<=1}b", "subject": "abxabb", "operation": "finditer", "pos": 0, "endpos": 6}, "C1", "expected"),
    # A divergence no entry classifies still fails: BESTMATCH after a (*SKIP) (D56, ledger 5's sixth
    # door), whose entry is keyed on judged rows, and this scan form is not one of them.
    ({"pattern": "(?b)x(*SKIP)(?:ba+){e<=1}", "subject": "xbaabbbx", "operation": "finditer", "pos": 0, "endpos": 8}, "C1", "fail"),
    # C4: a \K passed before the edge moves a partial's reported start; the judges report the
    # attempt's start, so this is a judge convention and passes (upstream gives P(3,4) too).
    ({"pattern": "(?:ba)*+.\\K\\w.", "subject": "baab", "operation": "match", "partial": True}, "C4", "pass"),
    ({"pattern": "a", "subject": "xaa", "operation": "finditer", "pos": 1, "endpos": 3}, "C1", "pass"),
    # C2: D40, upstream's call features (tools/probes/call-site-features-rows.jsonl row 1).
    ({"pattern": "(?P<g3>a)(?P<g4>(?&g3))(?:(?&g4)){s<=1}", "subject": "aab", "operation": "search",
      "writtenOut": {"8": "(?P<g3>a)(?P<g4>(?:a))(?:(?:(?:a))){s<=1}"}, "ablate": "callfeatures"}, "C2", "fail"),
    # The same row unablated still fails C2, on its captures only: g3 keeps the captures its calls
    # made, (1,2) and (2,3), which D51 (owner ruling (a)) says a return discards. So the quiet
    # control is a capture-free recursion, compared at depths 4-6.
    ({"pattern": "(?P<g3>a)(?P<g4>(?&g3))(?:(?&g4)){s<=1}", "subject": "aab", "operation": "search",
      "writtenOut": {"8": "(?P<g3>a)(?P<g4>(?:a))(?:(?:(?:a))){s<=1}"}}, "C2", "fail"),
    ({"pattern": "a(?R)?b", "subject": "xaabbb", "operation": "search",
      "writtenOut": {"4": "a(?:a(?:a(?:a(?:a(?:(?!))?b)?b)?b)?b)?b",
                     "5": "a(?:a(?:a(?:a(?:a(?:a(?:(?!))?b)?b)?b)?b)?b)?b",
                     "6": "a(?:a(?:a(?:a(?:a(?:a(?:a(?:(?!))?b)?b)?b)?b)?b)?b)?b"}}, "C2", "pass"),
    # C3: D37, the whole-pattern call that never returns, without the oracle's exemption from
    # Matcher.AssertMatchIsClosed (ExpectedDivergences' fuzzy-whole-pattern-call-returns-to-its-caller row 1).
    ({"pattern": "(?b)(?:b||b(?0)*){e<=2}", "subject": "azx", "operation": "fullmatch", "ablate": "d37"}, "C3", "fail"),
    ({"pattern": "(?b)(?:b||b(?0)*){e<=2}", "subject": "azx", "operation": "fullmatch"}, "C3", "pass"),
    # C4: D26, still open on main (docs/KNOWN-DEFECTS.md): None, but ':' completes it.
    ({"pattern": "(?:a$(?<=:)){i<=1}", "subject": "a", "operation": "match", "partial": True}, "C4", "fail"),
    ({"pattern": "ab", "subject": "a", "operation": "match", "partial": True}, "C4", "pass"),
    # C5: every repeat's failure memo forced on, including where Optimiser.KeepFailureMemosSound
    # withdraws it because a group test reads the captures. Measured 2026-09-30: (0,3) with the
    # memo as compiled, (1,3) with it forced on. (PatternObject.UseCallMemo's documented witnesses no
    # longer change with the call memo forced on, and memo-grid.cs "unsafe" found none in 25,864
    # rows, so the call memo has no firing control.)
    ({"pattern": "(?:(a)|.)*(?(1)x|y)", "subject": "aby", "operation": "search", "ablate": "unsaferepeatmemo"}, "C5", "fail"),
    ({"pattern": "(?:(a)|.)*(?(1)x|y)", "subject": "aby", "operation": "search"}, "C5", "pass"),
    # C6: ledger entry 42 switched off; the reference's rule 3 offers the deletion after an exact
    # match, so it answers (0,1) with one deletion where the ablated port answers None.
    ({"pattern": "(?:a){d<=1}a", "subject": "a", "operation": "match", "ablate": "exactdeletion"}, "C6", "fail"),
    ({"pattern": "(?:a){d<=1}a", "subject": "a", "operation": "match"}, "C6", "pass"),
    # C7: D51, inherited from upstream and still on main (docs/KNOWN-DEFECTS.md): the port keeps the
    # capture the call made, (0,1), in c's history although c is unset; PCRE2 and Perl leave c unset
    # with nothing captured, which the owner's ruling (a) adopts.
    ({"pattern": "(?(DEFINE)(?<c>a))(?&c)b", "subject": "ab", "operation": "search"}, "C7", "fail"),
    # Not a call: `(a|b)(?1)\1` over 'aba' fails C7 too, because the port's history for group 1 ends
    # with (1,2), the capture its call made, where the value and both engines say (0,1) (D51 again,
    # measured 2026-09-30). The quiet control is a lookahead's capture read by a backreference
    # (answer key A2): (0,2), group 1 (0,1), in PCRE2, Perl and the port.
    ({"pattern": "(?=(a))a\\1?", "subject": "aa", "operation": "search"}, "C7", "pass"),
    # C6 judge defects found by the triage (2026-09-30), each once a false failure: an escape the
    # reference cannot model must leave the row unjudged (it read \G\A as the text "GA"), and a verb
    # inside a negative lookahead makes the assertion true rather than ending the attempt.
    ({"pattern": "\\G\\Ab", "subject": "b", "operation": "search"}, "C6", "n/a"),
    ({"pattern": "(?:(?!(?:a(*PRUNE)(*F)|xb))aba){1<=d<=2}", "subject": "aba", "operation": "search"}, "C6", "pass"),
]


def main(out: str) -> int:
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for n, (row, check, status) in enumerate(CONTROLS):
            row = {"flags": 0, "namedLists": {}, **row}
            row["tags"] = sorted(tagger.confirmed(row))
            row["cell"] = f"control-{check}-{status}"
            row["expect"] = [check, status]
            row["id"] = n
            f.write(json.dumps(row, ensure_ascii=True) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
