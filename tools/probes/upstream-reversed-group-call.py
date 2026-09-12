# S34 item 2 (2026-09-12): a group called from a lookahead under (?r). Upstream records a span
# whose end precedes its start for a variable repeat, and its own inline copy of the same body
# records the forward span this port records. Upstream issue 614, fixed in 2026.8.30.
import regex


def show(pat, subj):
    m = regex.compile(pat).search(subj)
    if m is None:
        print(f"{pat!r} on {subj!r}: None")
        return
    print(
        f"{pat!r} on {subj!r}: match {m.span()} {m.group()!r} "
        + " ".join(f"g{i}={m.spans(i)}{m.captures(i)}" for i in range(1, m.re.groups + 1))
    )


# The call.
show(r'(?r)(?<g>[ab]+)(?=(?&g))b', 'abbaa')
# The same body written out instead of called: upstream's own answer for the shape.
show(r'(?r)(?<g>[ab]+)(?=([ab]+))b', 'abbaa')
# A non-repeat body through a call, which upstream gets right.
show(r'(?r)(?<g>[ab])(?=(?&g))b', 'abb')
# Is it the repeat, or the call? A repeat body written out, through a call of a different group.
show(r'(?r)(?<g>[ab]+)(?=(?&h))b(?<h>[ab]+){0}', 'abbaa')
# A bounded repeat instead of +.
show(r'(?r)(?<g>[ab]{1,3})(?=(?&g))b', 'abbaa')
show(r'(?r)(?<g>[ab]{2})(?=(?&g))b', 'abbaa')
# A group call NOT inside a lookahead, under (?r).
show(r'(?r)(?<g>[ab]+)(?&g)', 'abba')
