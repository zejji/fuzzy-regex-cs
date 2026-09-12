# S34 item 3 (2026-09-12): making the lazy quantifier greedy removes upstream's phantom partial,
# which is what says the bounded LAZY repeat is the cause.
import regex

CASES = [
    # The wave's row, then the same pattern with the lazy quantifier made greedy, removed,
    # and made a plain optional - the three ways to ask whether '??' is what does it.
    (r'^([A-Z]??)__$', '__aA '),
    (r'^([A-Z]?)__$', '__aA '),
    (r'^([A-Z])__$', '__aA '),
    (r'^__$', '__aA '),
    (r'^[A-Z]??__$', '__aA '),
    # The judged family, for comparison.
    (r'ba??x', 'baa'),
    (r'ba?x', 'baa'),
    # Minimising the subject.
    (r'^([A-Z]??)__$', '__a'),
    (r'^([A-Z]??)__$', '__A'),
    (r'^[A-Z]??__$', '__a'),
]

for pat, subj in CASES:
    c = regex.compile(pat)
    m = c.search(subj, partial=True)
    full = c.search(subj)
    print(
        f"{pat!r} on {subj!r}: partial-search {m.span() if m else None} "
        f"partial={m.partial if m else '-'}   plain-search {full.span() if full else None}"
    )
