"""Row 5 of `partial-retry-reversed-slice`, where upstream answers a LONGER partial, not a shorter one.

S52, 2026-09-15. Row 33858 of the seed-20260915 2000-row default wave (`pwsh -File
tools/run-oracle.ps1 -Count 2000`, the full generator list, 42,000 rows a seed). Rows 1 to 4 of the
entry are upstream answering a shorter partial than this port; this one is upstream answering the
whole subject where this port stops at 2. The mechanism is the same moved `slice_end`, and the
`(*PRUNE)` control is what says so: `(*PRUNE)` prunes backtracking exactly as `(*SKIP)` does and
moves no bound, so if the bound move is the cause then `(*PRUNE)` must give this port's answer.

    python tools/probes/upstream-partial-retry-reversed-longer.py

The port half is tools/probes/port-partial-retry-reversed-longer.ps1, run after a Debug build.
"""

import regex

SUBJECT = "00a ."
TEMPLATE = r"(?r)(?:[A-Z]{VERB}.|\d)([^\p{L}])\B"


def answer(m):
    if m is None:
        return None
    return (m.span(), m.span(1) if m.lastindex else None, "partial" if m.partial else "complete")


def show(title, fn):
    try:
        print(f"{title:46}: {ascii(fn())}")
    except Exception as e:  # noqa: BLE001
        print(f"{title:46}: {type(e).__name__}: {ascii(str(e))}")


print(f"regex {regex.__version__}, subject {ascii(SUBJECT)}")
print()
print("=== the verb control, which is the whole of the evidence")
for verb, label in (("(*SKIP)", "as drawn, (*SKIP)"), ("(*PRUNE)", "verb -> (*PRUNE)"), ("", "verb deleted")):
    pattern = TEMPLATE.replace("{VERB}", verb)
    show(f"  {label:18} search(partial=True)",
         lambda p=pattern: answer(regex.compile(p).search(SUBJECT, partial=True)))

print()
print("=== upstream's own matcher at every endpos a reversed search tries - it names NEITHER answer")
compiled = regex.compile(TEMPLATE.replace("{VERB}", "(*SKIP)"))
for end in range(len(SUBJECT), -1, -1):
    show(f"  match(0, {end}, partial=True)",
         lambda e=end: answer(compiled.match(SUBJECT, 0, e, partial=True)))

print()
print("=== the forward twin, which does not diverge at all: the bound a verb moves is direction-bound")
show("  forward (*SKIP)  search(partial=True)",
     lambda: answer(regex.compile(r"(?:[A-Z](*SKIP).|\d)([^\p{L}])\B").search(SUBJECT, partial=True)))
show("  forward (*PRUNE) search(partial=True)",
     lambda: answer(regex.compile(r"(?:[A-Z](*PRUNE).|\d)([^\p{L}])\B").search(SUBJECT, partial=True)))
