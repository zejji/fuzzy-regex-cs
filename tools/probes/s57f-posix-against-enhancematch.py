r"""Does upstream's `(?e)` shrink a POSIX span to spend fewer errors?

Row 77887 of the 6000-row date-seed gate: under `(?e)(?r)(?p)` upstream answers the LONGER span,
codepoints (0, 9), spending a substitution and an insertion, where this port answers (0, 8) with one
substitution. Two readings, and this probe separates them:

  (1) upstream's improvement loop stopped one step early - the one-error fit was available to it,
      exactly as ledger entry 25 says of `enhancematch-loses-a-candidate`; or
  (2) POSIX's longest-match rule outranks the error count, so the longer, dearer span is the right
      answer and this port is wrong to shrink it.

Part A asks upstream a plain ASCII question where the two rules disagree and no other flag is in
play. Part B asks whether upstream can reach this port's answer on the real row when the trailing
fuzzy section is tightened to a SUBSET of what the row already permits.

    python tools/probes/s57f-posix-against-enhancematch.py

WHAT IT MEASURED, regex 2026.9.10, on 2026-09-22:

  2026.9.10

  A  POSIX against ENHANCEMATCH, plain ASCII
     (?:ab|abcd){e<=2} over 'abcz': 'ab' is exact, 'abcd' costs one substitution
    (?p)                               (0, 4) errors=[0, 2, 0] 'abcz'
    (?p)(?e)                           (0, 2) errors=[0, 0, 0] 'ab'
    (?e)                               (0, 2) errors=[0, 0, 0] 'ab'
    (none)                             (0, 2) errors=[0, 0, 0] 'ab'

     the same question with the long branch dearer still
    (?p)                               (0, 6) errors=[3, 0, 0] 'abczzz'
    (?p)(?e)                           (0, 2) errors=[0, 0, 0] 'ab'
    (?e)                               (0, 2) errors=[0, 0, 0] 'ab'
    (none)                             (0, 2) errors=[0, 0, 0] 'ab'

     and with the reverse flag, as row 77887 carries it
    (?r)(?p)                           (0, 4) errors=[1, 0, 0] 'abcz'
    (?r)(?p)(?e)                       (0, 2) errors=[0, 0, 0] 'ab'
    (?r)(?e)                           (2, 4) errors=[2, 0, 0] 'cz'
    (?r)                               (2, 4) errors=[2, 0, 0] 'cz'

  B  row 77887 itself: is this port's one-error fit available to upstream?
    as drawn                           (0, 9) errors=[1, 1, 0] '𝟮🏻A😀\r Aaa'
    tail {s<=1,d<=1}                   (0, 8) errors=[1, 0, 0] '𝟮🏻A😀\r Aa'
    tail {s<=1}                        (0, 8) errors=[1, 0, 0] '𝟮🏻A😀\r Aa'
    tail {e<=1}                        (0, 8) errors=[1, 0, 0] '𝟮🏻A😀\r Aa'
    no (?e)                            (0, 9) errors=[1, 1, 0] '𝟮🏻A😀\r Aaa'
    no (?e), tail {s<=1,d<=1}          (0, 8) errors=[1, 0, 0] '𝟮🏻A😀\r Aa'
    no (?p)                            (3, 8) errors=[1, 0, 0] '😀\r Aa'
    no (?p), tail {s<=1,d<=1}          (3, 8) errors=[1, 0, 0] '😀\r Aa'

Reading (2) is dead. Part A's second line is upstream shrinking its own POSIX span from four
characters to two in order to spend no errors, and the reversed spelling on row 77887's own flags
does the same. So upstream itself ranks the improvement loop above the longest-match rule.

Reading (1) holds on the real row. `{e<=1}`, `{s<=1}` and `{s<=1,d<=1}` each permit a SUBSET of the
row's own `{s<=1,i<=1,d<=1}` - one error of any kind is one substitution, one insertion or one
deletion - and under each of them the same engine answers codepoints (0, 8) with one substitution,
which is this port's answer. A fit upstream can reach through a narrower section was reachable
through the wider one, so the improvement loop stopped one step early.

`errors` is `match.fuzzy_counts`, which upstream orders (substitutions, insertions, deletions).
Spans are Python codepoint indices; this port's report renders row 77887's UTF-16 spans, where the
subject's four astral characters make (0, 8) read as (0, 11) and (0, 9) as (0, 12).
"""

import sys

import regex

sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")


def fit(label, pattern, subject, **kw):
    try:
        m = regex.compile(pattern, **kw).search(subject)
    except Exception as exc:                                  # noqa: BLE001 - probe
        print("  %-34s ERROR %s" % (label, exc))
        return
    if m is None:
        print("  %-34s no match" % label)
        return
    print("  %-34s %s errors=%s %r" % (label, (m.start(), m.end()),
                                       list(m.fuzzy_counts), m.group(0)))


print(regex.__version__)

print("\nA  POSIX against ENHANCEMATCH, plain ASCII")
print("   (?:ab|abcd){e<=2} over 'abcz': 'ab' is exact, 'abcd' costs one substitution")
for flags in ("(?p)", "(?p)(?e)", "(?e)", ""):
    fit("%-9s" % (flags or "(none)"), flags + r"(?:ab|abcd){e<=2}", "abcz")

print("\n   the same question with the long branch dearer still")
for flags in ("(?p)", "(?p)(?e)", "(?e)", ""):
    fit("%-9s" % (flags or "(none)"), flags + r"(?:ab|abcdef){e<=3}", "abczzz")

print("\n   and with the reverse flag, as row 77887 carries it")
for flags in ("(?r)(?p)", "(?r)(?p)(?e)", "(?r)(?e)", "(?r)"):
    fit("%-11s" % flags, flags + r"(?:ab|abcd){e<=2}", "abcz")

print("\nB  row 77887 itself: is this port's one-error fit available to upstream?")
P = (r"(?e)(?r)(?p)\b(\d+)*(?:(?:(\p{Lu})[A-Z]*){e<=2:[a-z]}(*PRUNE)\s|\S)"
     r"(?:\p{ASCII}[^a]{2,}?" + "\U0001D7EE" + r"){s<=1,i<=1,d<=1}")
S = "\U0001D7EE\U0001F3FBA\U0001F600\r Aaa"
fit("as drawn", P, S)
fit("tail {s<=1,d<=1}", P.replace("{s<=1,i<=1,d<=1}", "{s<=1,d<=1}"), S)
fit("tail {s<=1}", P.replace("{s<=1,i<=1,d<=1}", "{s<=1}"), S)
fit("tail {e<=1}", P.replace("{s<=1,i<=1,d<=1}", "{e<=1}"), S)
fit("no (?e)", P.replace("(?e)", ""), S)
fit("no (?e), tail {s<=1,d<=1}",
    P.replace("(?e)", "").replace("{s<=1,i<=1,d<=1}", "{s<=1,d<=1}"), S)
fit("no (?p)", P.replace("(?p)", ""), S)
fit("no (?p), tail {s<=1,d<=1}",
    P.replace("(?p)", "").replace("{s<=1,i<=1,d<=1}", "{s<=1,d<=1}"), S)
