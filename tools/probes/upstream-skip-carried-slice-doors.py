"""The six `(*SKIP)` rows S52's second sitting measured and did NOT judge, with their controls.

These are the six rows of the three-seed 2000-row wave of commit 407c0cb that the entries in
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` do not account for. Every one of them shares
one measured shape, and this probe is the shape written down so the next sitting starts from
evidence rather than from a description:

    upstream as drawn, with `(*SKIP)`      one answer
    upstream with `(*SKIP)` -> `(*PRUNE)`  THIS PORT'S ANSWER, on all six
    upstream with the verb deleted         a third answer on four of the six

`(*PRUNE)` prunes exactly the backtracking `(*SKIP)` prunes; the only thing `(*SKIP)` does that
`(*PRUNE)` does not is move a slice bound (`upstream/src/_regex.c:14545` forwards, `:14551` under
`(?r)`). So a difference between the two lines is about the bound and not about what the pattern
means - the argument four entries in that file already rest on.

NOTHING HERE IS A JUDGEMENT YET. `(*SKIP)` is documented to do something `(*PRUNE)` does not, so
the two answering differently is the ordinary case on a SCAN, where a bump-along is real. What
each row owes is the second half: upstream contradicting ITSELF, as every judged `(*SKIP)` entry
in that file carries. **Three of the six have it and three do not**, measured 2026-09-15:

* row 24018 - a `match` is ONE attempt. There is no next attempt for `(*SKIP)` to move its start
  to, so within it `(*SKIP)` prunes exactly what `(*PRUNE)` prunes and the two MUST agree. They do
  not: upstream's `(*SKIP)` answer is its VERB-FREE answer, character for character, so the
  pruning both verbs owe simply did not happen. This port answers upstream's own `(*PRUNE)` line.
* rows 25854 and 38151 - the `stepwise` line below is upstream's own scan taken one match at a
  time, which is `anchoredScan`'s argument (S34): each `search` starts a fresh attempt, so no
  bound a previous match's `(*SKIP)` moved is still moved. On both rows it lands on THIS PORT'S
  answer span for span, where upstream's own continuous scan does not.
* rows 24737, 24224 and 38101 - NOT settled here, and the `stepwise` line is not evidence about
  them. 24737 is a partial `search`, where the two passes over one attempt are the mechanism
  `partial-retry-carried-slice-forward` covers and the control is upstream's own anchored
  `match(pos, endpos, partial=True)`. On 24224 (a reversed `split`) the stepwise walk finds the
  same NUMBER of separators this port's split implies but not the same spans, so it models the
  scan only loosely; on 38101 (a reversed `subf`) it finds a match where this port replaces
  nothing. Both need a control that reproduces the operation rather than approximating it.

Run::

    python tools/probes/upstream-skip-carried-slice-doors.py

The recorder writes the same control per row as `pruneOutcome` (S52), so a wave carries it too.
"""

import regex

# (seed, row, generator, pattern, flags, subject, operation, template, count, named lists)
ROWS = [
    (
        7, 24018, "interactions",
        r"\L<w1>{e<=2}(?:\D(*SKIP)\S|\p{Lu})", 0x0, "ßß", "match-partial", None, 0,
        {"w1": ["sı", "İ", "ﬁ", "ﬁı"]},
    ),
    (
        7, 24737, "interactions",
        r"(?:(?:a[\p{L}\p{N}]?(?:(.+?)){e<=2:\s}){1i+2d+1s<=3}(*SKIP)\W|\w)(\p{Ll}{3,3}?)+\K",
        0x8, "\U0001F600\U0001F600aa\U00010428\U00010428 ", "search-partial", None, 0, {},
    ),
    (
        7, 25854, "interactions",
        r"(?b)(?:(?:\W{2,}[^\d]*?){1<=e<=2}(*SKIP)\D|\w)(\p{Lu}{2,3}){0,0}",
        0x2, "b\r\nabA\n_", "finditer", None, 0, {},
    ),
    (
        7, 38151, "verbs",
        r"(?r)\p{ASCII}{1,3}(?![a](*SKIP))s(?:[^\p{L}]*+(*SKIP)\W|s)[^a]*(?<=\W(*PRUNE))[A-Z]",
        0x4002, "aas\rs\r\ns", "finditer-overlapped", None, 0, {},
    ),
    (
        20260915, 24224, "interactions",
        r"(?r)(?:\s*?(*SKIP)\W|[^a])(\S{1,})$", 0x8, "ﬀﬀ\r\nﬀﬀss\rS",
        "split", None, 0, {},
    ),
    (
        20260915, 38101, "verbs",
        r"(?r)(\D+(*PRUNE)[^\p{L}])(?:[^a-f](*PRUNE)){1,3}?((?>\p{Lu}{1,3}?(*SKIP)\D))$",
        0xA, "a\r\na\U0001D518\U0001D518\U00010428\r\U00010428\U0001D518", "subf",
        "-{0[0]}{0[-1]}{0[-2]}", 0, {},
    ),
]

# `verbs` and `partial-sliced` are recorded with upstream's required-string prefilter neutralised
# (PREFILTER_FREE_GENERATORS in tools/record-oracle.py), so the probe has to ask the same way or it
# is not reproducing the row.
PREFILTER_FREE = ("verbs", "partial-sliced")
_REQ_OFFSET_ARG, _REQ_CHARS_ARG = 7, 8
_inner = regex._regex.compile


def _without_required_string(*args):
    args = list(args)
    args[_REQ_OFFSET_ARG] = -1
    args[_REQ_CHARS_ARG] = None
    return _inner(*args)


def compile_row(generator: str, pattern: str, flags: int, lists: dict):
    if generator not in PREFILTER_FREE:
        return regex.compile(pattern, flags, cache_pattern=False, **lists)
    regex._regex.compile = _without_required_string
    try:
        return regex.compile(pattern, flags, cache_pattern=False, **lists)
    finally:
        regex._regex.compile = _inner


def describe(m) -> str:
    if m is None:
        return "None"
    bits = [f"{m.span()}"]
    for n in range(1, m.re.groups + 1):
        bits.append(f"g{n}={m.span(n)}" if m.span(n) != (-1, -1) else f"g{n}=unset")
    if m.partial:
        bits.append("PARTIAL")
    if any(m.fuzzy_counts):
        bits.append(f"counts={m.fuzzy_counts} changes={m.fuzzy_changes}")
    return " ".join(bits)


def answer(generator, pattern, flags, subject, operation, template, count, lists) -> str:
    try:
        compiled = compile_row(generator, pattern, flags, lists)
        if operation == "match-partial":
            return describe(compiled.match(subject, partial=True))
        if operation == "search-partial":
            return describe(compiled.search(subject, partial=True))
        if operation in ("finditer", "finditer-overlapped"):
            found = list(compiled.finditer(subject, overlapped=operation.endswith("overlapped")))
            return f"{len(found)} | " + " || ".join(describe(m) for m in found)
        if operation == "split":
            parts = compiled.split(subject)
            return f"{len(parts)} | " + " ".join(repr(p) for p in parts)
        if operation == "subf":
            return repr(compiled.subfn(template, subject, count=count))
        raise SystemExit(f"unhandled operation {operation}")
    except Exception as e:  # noqa: BLE001 - the exception IS upstream's answer on row 38101
        return f"{type(e).__name__}: {e}"


if __name__ == "__main__":
    print("regex", regex.__version__)
    for seed, number, generator, pattern, flags, subject, operation, template, count, lists in ROWS:
        print()
        print(f"--- seed {seed} row {number}  {generator}  {operation}  flags={flags:#x}")
        print("    pattern           " + ascii(pattern))
        print("    subject           " + ascii(subject))
        if lists:
            print("    lists             " + ascii(str(lists)))
        args = (generator, flags, subject, operation, template, count, lists)
        print("    as drawn          " + ascii(answer(args[0], pattern, *args[1:])))
        print("    (*SKIP)->(*PRUNE) " + ascii(answer(args[0], pattern.replace("(*SKIP)", "(*PRUNE)"), *args[1:])))
        print("    verb deleted      " + ascii(answer(args[0], pattern.replace("(*SKIP)", ""), *args[1:])))

        # UPSTREAM'S OWN SCAN, TAKEN ONE MATCH AT A TIME. `finditer`, `split` and `subn` keep one
        # match state across the whole scan, so a bound a `(*SKIP)` moved in one match is still moved
        # in the next; asking `search(subject, pos)` again starts a fresh attempt with `slice_start`
        # at `pos`, which is the same question without the carry-over. This is `anchoredScan`'s
        # argument (S34), computed here for the forward doors as well.
        if operation in ("finditer", "finditer-overlapped", "split", "subf"):
            compiled = compile_row(generator, pattern, flags, lists)
            reverse = "(?r)" in pattern or flags & 0x400
            overlapped = operation.endswith("overlapped")
            spans = []
            # A reversed scan walks right to left, so the moving bound is `endpos`; an overlapped
            # scan advances one position from the match START rather than its end.
            pos, endpos = 0, len(subject)
            while len(spans) < 12:
                if reverse and endpos < 0:
                    break
                if not reverse and pos > len(subject):
                    break
                m = compiled.search(subject, pos, endpos) if reverse else compiled.search(subject, pos)
                if m is None:
                    break
                spans.append(m.span())
                if reverse:
                    endpos = m.end() - 1 if overlapped else m.start()
                    if m.end() == m.start():
                        endpos = m.start() - 1
                elif overlapped:
                    pos = m.start() + 1
                else:
                    pos = m.end() if m.end() > m.start() else m.start() + 1
            how = ("reversed " if reverse else "") + ("overlapped " if overlapped else "") + "search"
            print(f"    stepwise {how:17} {len(spans)} | " + " || ".join(str(s) for s in spans))
