"""Row 6 of `partial-retry-reversed-slice`: the anchored, fuzzy one, and the Phase 6 gate's last red row.

S57, 2026-09-20. Row 525 of `pwsh -File tools/run-oracle.ps1 -Generator fuzzy,interactions
-Seeds 99991,57057` (300 rows a generator), and row 225 of the 6000-row `interactions` wave at the
same seed - one row drawn twice, not two rows. Upstream answers a partial of (0, 2); this port
answers (0, 1). It is the fifth row of the entry to have upstream answering the LONGER partial, and
the first that is ANCHORED (`^`) and carries a fuzzy section, so the two arguments the entry rests
on are both re-run here rather than assumed from row 5.

    python tools/probes/upstream-partial-retry-reversed-anchored.py

The port half is tools/probes/s57-skip-partial-span.cs (`dotnet run -c Release`).
"""

import regex

SUBJECT = "\r\n\U0001F600"
TEMPLATE = (
    r"(?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}"
    r"(?:[a-f]{VERB}\s|\p{Nd})"
)


def answer(m):
    if m is None:
        return None
    return (m.span(), m.groups(), "partial" if m.partial else "complete", m.fuzzy_counts)


def show(title, fn):
    try:
        print(f"{title:46}: {ascii(fn())}")
    except Exception as e:  # noqa: BLE001
        print(f"{title:46}: {type(e).__name__}: {ascii(str(e))}")


print(f"regex {regex.__version__}, subject {ascii(SUBJECT)}")
print()
print("=== the verb control: (*PRUNE) prunes the same backtracking and moves no bound")
for verb, label in (("(*SKIP)", "as drawn, (*SKIP)"), ("(*PRUNE)", "verb -> (*PRUNE)"), ("", "verb deleted")):
    pattern = TEMPLATE.replace("{VERB}", verb)
    show(f"  {label:18} search(partial=True)",
         lambda p=pattern: answer(regex.compile(p, flags=regex.V0).search(SUBJECT, partial=True)))

print()
print("=== upstream's own matcher at every endpos a reversed search tries")
compiled = regex.compile(TEMPLATE.replace("{VERB}", "(*SKIP)"), flags=regex.V0)
for end in range(len(SUBJECT), -1, -1):
    show(f"  match(0, {end}, partial=True)",
         lambda e=end: answer(compiled.match(SUBJECT, 0, e, partial=True)))

print()
print("=== the fuzzy section and the anchor, removed one at a time: neither is what moves the span")
show("  no fuzzy         search(partial=True)",
     lambda: answer(regex.compile(
         r"(?r)^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]]))(?:[a-f](*SKIP)\s|\p{Nd})",
         flags=regex.V0).search(SUBJECT, partial=True)))
show("  no anchor        search(partial=True)",
     lambda: answer(regex.compile(
         r"(?r)(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}"
         r"(?:[a-f](*SKIP)\s|\p{Nd})",
         flags=regex.V0).search(SUBJECT, partial=True)))

print()
print("=== the forward twin, which does not diverge: the bound a verb moves is direction-bound")
show("  forward (*SKIP)  search(partial=True)",
     lambda: answer(regex.compile(
         r"^(?:[^a-f]{3,}(?P<g1>[a-f])(?P<g2>[[:digit:]])){s<=1,i<=1,d<=1}"
         r"(?:[a-f](*SKIP)\s|\p{Nd})",
         flags=regex.V0).search(SUBJECT, partial=True)))
