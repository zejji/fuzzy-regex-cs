r"""The six `(*SKIP)` rows S52 measured, with the control that judges each one.

These are the six rows of the three-seed 2000-row wave of commit 407c0cb that the entries in
`tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs` did not account for. S52's second sitting
measured the shape they share and judged none of them; its third sitting judged all six, and this
probe is the evidence, re-runnable from the committed tree::

    python tools/probes/upstream-skip-carried-slice-doors.py

The shape they share, printed for every row as the first three lines of its block:

    upstream as drawn, with `(*SKIP)`      one answer
    upstream with `(*SKIP)` -> `(*PRUNE)`  THIS PORT'S ANSWER, on all six
    upstream with the verb deleted         a THIRD, distinct answer on one of the six - row 25854.
                                           It repeats the `as drawn` line on rows 24018 and 24737
                                           and the `(*PRUNE)` line on the other three.

`(*PRUNE)` prunes exactly the backtracking `(*SKIP)` prunes; the only thing `(*SKIP)` does that
`(*PRUNE)` does not is move a slice bound (`upstream/src/_regex.c:14553` under `(?r)`, `:14555`
forwards). So a difference between the two lines is about the bound and not about what the pattern
means. THAT IS NOT A JUDGEMENT ON ITS OWN: `(*SKIP)` is documented to do something `(*PRUNE)` does
not, so the two answering differently is the ordinary case on a SCAN, where a bump-along is real.
What each row needs is the second half - upstream contradicting ITSELF - and the six fall into
three families, one control each.

EVERY `upstream/src/_regex.c` LINE NUMBER HERE IS AGAINST THE PIN THIS PROBE WAS MEASURED ON,
2026.9.10, and was re-read out of the file rather than carried over. Other files in this repo cite
`:14545` and `:14551` for the two `RE_OP_SKIP` writes and `:20903` for the scanner's overlapped
step; today those lines are a `TRACE` call, a blank line and a comment terminator. Reconciling them
repo-wide is a maintenance job, not this probe's.

FAMILY 1, rows 24018 and 24737: THE PARTIAL CALL ANSWERS A MATCH THAT IS NOT PARTIAL.
    A `partial` request runs a non-partial pass and then a partial one from the same `text_pos`
    (`do_match`, the `text_pos` save at `:18159`); upstream restores `text_pos` and nothing else, so a bound the verb moved
    in the first pass is still moved in the second. The tell needs no model of that at all:

        as drawn, partial=True    a match with the PARTIAL flag CLEAR
        as drawn, no partial      None

    `partial=True` is documented to ALSO allow a partial match, so it cannot conjure a complete one
    the same engine denies without it. The `no partial` block below is where that is printed. Row
    24018 adds the scan question's answer: a `match` is ONE attempt, so `(*SKIP)` has no next
    attempt to move the start of and must prune exactly what `(*PRUNE)` prunes. Classified by
    `partial-retry-carried-slice-forward`.

FAMILY 2, rows 24224 and 38101: `$` IS TRUE WHERE THE VERB LEFT THE BOUND.
    Upstream has eight predicates that ask whether a position is at an edge of the text, and SEVEN
    read a text bound. `try_match_END_OF_LINE` (`:7110`) alone reads `slice_end`, which is the field
    `RE_OP_SKIP` writes under `(?r)` - and the seven include its OWN Unicode twin, so upstream's `$`
    disagrees with itself in one file:

        try_match_START_OF_LINE          :7360   text_pos <= state->text_start
        try_match_START_OF_LINE_U        :7367   -> {ascii,unicode}_at_line_start, :902 / :1945
                                                    text_pos <= state->text_start
        try_match_START_OF_STRING        :7373   text_pos <= state->text_start
        try_match_END_OF_STRING          :7123   text_pos >= state->text_end
        try_match_END_OF_STRING_LINE     :7129   text_pos >= state->text_end
        try_match_END_OF_STRING_LINE_U   :7136   text_pos >= state->text_end
        try_match_END_OF_LINE_U          :7117   -> {ascii,unicode}_at_line_end, :922 / :1966
                                                    text_pos >= state->text_end
        try_match_END_OF_LINE            :7110   text_pos >= state->SLICE_END   <- the odd one out

    (`try_match_START_OF_WORD` and `try_match_END_OF_WORD` are word edges, not text edges, and are
    not in the count.) The block below prints where upstream's own `$` is true, asked one anchored
    position at a time, beside the end upstream actually reports; the same pattern with `$` spelled
    out as what `$` is defined to be, which gives this port's answer; and upstream's own plain
    `search`, which is what says how far the stale bound travelled.

    ROW 38101 CARRIES THE `(?w)` CONTROL AND ROW 24224 CANNOT, and the reason is narrower than it
    first looks. `(?w)` compiles `$` to `END_OF_LINE_U` (`regex/_regex_core.py:506-510`), the twin
    that reads `text_end` - but it is NOT a clean swap of one bound for another, because it also
    changes which positions are line ends, on BOTH rows and in both directions:

        row 24224   `$` true at [3, 10]    `(?w)$` true at [2, 8, 10]    phantom end 8
        row 38101   `$` true at [2, 10]    `(?w)$` true at [1, 7, 10]    phantom end 5

    So "`(?w)` moves the line ends" does not separate them - it is true of both. What separates them
    is whether it moves THE PHANTOM POSITION. On 24224 the phantom end 8 becomes a genuine `(?w)`
    line end, so a `(?w)` run that stops reporting the separator cannot tell a bound that stopped
    being read from a line end that started existing; on 38101 the phantom end 5 is not a line end
    under either spelling, so a `(?w)` run that answers None says the bound was the only thing
    holding the match up. This probe derives that condition per row rather than listing it, and
    prints both position lists either way.

    THE STEPWISE LINE IS NOT THE CONTROL FOR THESE TWO, and it points the wrong way, which is why it
    is labelled. `search(subject, 0, endpos)` sets `slice_end` to `endpos` legitimately - the very
    bound the defect leaves stale - so a walk reproduces the bug rather than testing it. The
    `endpos reproduces it verb-free` line below is that, measured: the phantom span comes back on
    the verb-free pattern too. Classified by `end-of-line-reads-a-skip-moved-slice`.

FAMILY 3, rows 25854 and 38151: UPSTREAM'S OWN SCAN, TAKEN ONE MATCH AT A TIME, IS THIS PORT'S.
    `finditer` keeps one match state across the whole scan, so a bound a `(*SKIP)` moved in one
    match is still moved in the next; asking `search` again starts a fresh attempt. That is
    `anchoredScan`'s argument (S34), and the recorder refuses to record it for either of these rows
    - row 25854 because a forward non-overlapped walk needs `must_advance`, row 38151 because its
    lookahead makes a truncated subject a different question. The walk is computed here anyway
    because on THESE TWO rows it is sound, and each reason is narrow:

    * Row 25854: `must_advance` is set only after a ZERO-WIDTH match - `state->must_advance =
      state->text_pos == state->match_pos` (`:20932`) - and no match in this row's walk is
      zero-width, so `search(subject, m.end())` is the scanner's own step. The walk asserts that and
      bails out loudly if a zero-width match ever appears.
    * Row 38151: the walk takes upstream's own reversed overlapped step, `state->text_pos =
      state->match_pos + step` with `step` of -1 under `(?r)` (`:20927-20928`), and the lookahead
      the refusal fires on sits where both questions read the subject identically.

    Classified by `skip-carried-slice-on-a-scan-with-no-walk`.

    Seed 20260923 row 3752 (S87) is a forward `split`, sound for row 25854's reason: both matches
    in its walk are one codepoint wide. It has no `(*PRUNE)` control, because upstream's
    `(*PRUNE)` and verb-free spellings never return (ledger entry 32), so its walk is the whole
    judgement: (0, 1) then (3, 4), where upstream's split stops after the first.

Measured 2026-09-15 against regex 2026.9.10. The recorder writes the `(*PRUNE)` control per row as
`pruneOutcome` (S52), so a wave carries the first three lines of every block without this probe.
"""

import multiprocessing
import sys

import regex

# (seed, row, family, generator, pattern, flags, subject, operation, template, count, named lists)
ROWS = [
    (
        7, 24018, 1, "interactions",
        r"\L<w1>{e<=2}(?:\D(*SKIP)\S|\p{Lu})", 0x0, "ßß", "match-partial", None, 0,
        {"w1": ["sı", "İ", "ﬁ", "ﬁı"]},
    ),
    (
        7, 24737, 1, "interactions",
        r"(?:(?:a[\p{L}\p{N}]?(?:(.+?)){e<=2:\s}){1i+2d+1s<=3}(*SKIP)\W|\w)(\p{Ll}{3,3}?)+\K",
        0x8, "\U0001F600\U0001F600aa\U00010428\U00010428 ", "search-partial", None, 0, {},
    ),
    (
        7, 25854, 3, "interactions",
        r"(?b)(?:(?:\W{2,}[^\d]*?){1<=e<=2}(*SKIP)\D|\w)(\p{Lu}{2,3}){0,0}",
        0x2, "b\r\nabA\n_", "finditer", None, 0, {},
    ),
    (
        7, 38151, 3, "verbs",
        r"(?r)\p{ASCII}{1,3}(?![a](*SKIP))s(?:[^\p{L}]*+(*SKIP)\W|s)[^a]*(?<=\W(*PRUNE))[A-Z]",
        0x4002, "aas\rs\r\ns", "finditer-overlapped", None, 0, {},
    ),
    (
        20260915, 24224, 2, "interactions",
        r"(?r)(?:\s*?(*SKIP)\W|[^a])(\S{1,})$", 0x8, "ﬀﬀ\r\nﬀﬀss\rS",
        "split", None, 0, {},
    ),
    (
        20260915, 38101, 2, "verbs",
        r"(?r)(\D+(*PRUNE)[^\p{L}])(?:[^a-f](*PRUNE)){1,3}?((?>\p{Lu}{1,3}?(*SKIP)\D))$",
        0xA, "a\r\na\U0001D518\U0001D518\U00010428\r\U00010428\U0001D518", "subf",
        "-{0[0]}{0[-1]}{0[-2]}", 0, {},
    ),
    (
        20260923, 3752, 3, "interactions",
        r"(?b)\b\K(?:(?:\U0001D7EEa(?:[[:alpha:]]+?){s<=1:\W}){s<=1,i<=1,d<=1}(*SKIP)\S|\S)",
        0x400A, "\U0001D7EE\r\n\U0001D518\U0001D518\U0001D518\rAa", "split", None, 0, {},
    ),
]

# Seconds before a line gives up. Row 3752's `(*PRUNE)` and verb-free spellings never return on
# upstream: a rejected fuzzy section leaves `total_errors` stale and `do_best_fuzzy_match` re-finds
# one match for ever (ledger entry 32). Each line runs in a child process so a hang is an answer.
LINE_TIMEOUT = 10

# Nothing about a family-2 row is listed here. The end upstream reports, and whether the `(?w)`
# control can isolate anything on that row, are both DERIVED in the block below - the first from
# upstream's own scan spans against the positions its own `$` holds at, the second from whether the
# phantom end is itself a `(?w)` line end. An earlier revision of this probe carried a hand-written
# table for the second and got the REASON wrong: `(?w)` moves the line ends on both rows, and only
# the phantom position decides.

# `$` written as what `$` is DEFINED to be under MULTILINE - the end of the TEXT, or before a line
# terminator - in a spelling no slice bound can answer. `(?!\n|.)` is false wherever a character
# remains, DOTALL or not, and is the only arm a truncated view could change.
DOLLAR_SPELLED_OUT = r"(?:(?=\n)|(?!\n|.))"

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


def say(text: str) -> None:
    sys.stdout.buffer.write(text.encode("utf-8", "backslashreplace") + b"\n")


def describe(m) -> str:
    if m is None:
        return "None"
    bits = [f"{m.span()}"]
    for n in range(1, m.re.groups + 1):
        bits.append(f"g{n}={m.span(n)}" if m.span(n) != (-1, -1) else f"g{n}=unset")
    bits.append("PARTIAL" if m.partial else "not-partial")
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


def _answer_into(queue, args) -> None:
    queue.put(answer(*args))


def bounded_answer(*args) -> str:
    """`answer`, or `HANGS` if upstream has not returned within LINE_TIMEOUT seconds."""
    queue = multiprocessing.Queue()
    child = multiprocessing.Process(target=_answer_into, args=(queue, args))
    child.start()
    child.join(LINE_TIMEOUT)
    if child.is_alive():
        child.terminate()
        child.join()
        return f"HANGS (no answer in {LINE_TIMEOUT} s)"
    return queue.get()


def without_the_partial(generator, pattern, flags, subject, operation, lists) -> str:
    """The same call with no `partial=True`, which is family 1's whole control."""
    compiled = compile_row(generator, pattern, flags, lists)
    if operation == "match-partial":
        return describe(compiled.match(subject))
    return describe(compiled.search(subject))


def dollar_positions(subject: str, flags: int, unicode_lines: bool = False) -> list[int]:
    """Where upstream's own `$` is true, asked one anchored position at a time.

    With ``unicode_lines`` it asks ``(?w)$`` instead, which compiles to ``RE_OP_END_OF_LINE_U`` -
    the twin predicate that reads ``text_end``. That is a different QUESTION as well as a different
    predicate, because ``(?w)`` also makes ``\\r`` and the other Unicode separators line ends; see
    the module docstring for why that makes it a control on one row and not on the other.
    """
    end = regex.compile("(?w)$" if unicode_lines else "$", flags, cache_pattern=False)
    return [p for p in range(len(subject) + 1) if end.match(subject, p) is not None]


def stepwise(compiled, subject: str, reverse: bool, overlapped: bool) -> str:
    """Upstream's own scan taken one match at a time, each step a fresh attempt.

    Returns a string rather than spans so the zero-width bail-out can say so in place. See the
    module docstring: the non-overlapped step is the scanner's own only while no match is
    zero-width, because `must_advance` (`:20932`) is what a fresh `search` cannot express.
    """
    spans = []
    pos, endpos = 0, len(subject)
    while len(spans) < 12:
        if reverse and endpos < 0:
            break
        if not reverse and pos > len(subject):
            break
        m = compiled.search(subject, pos, endpos) if reverse else compiled.search(subject, pos)
        if m is None:
            break
        if m.end() == m.start() and not overlapped:
            return (f"{len(spans)} spans then a ZERO-WIDTH match at {m.start()} - the walk is NOT "
                    "upstream's step past one, see the docstring")
        spans.append(m.span())
        if reverse:
            endpos = m.end() - 1 if overlapped else m.start()
        elif overlapped:
            pos = m.start() + 1
        else:
            pos = m.end()
    return f"{len(spans)} | " + " || ".join(str(s) for s in spans)


if __name__ == "__main__":
    print("regex", regex.__version__)
    for seed, number, family, generator, pattern, flags, subject, operation, template, count, lists in ROWS:
        print()
        print(f"--- seed {seed} row {number}  family {family}  {generator}  {operation}  flags={flags:#x}")
        print("    pattern           " + ascii(pattern))
        print("    subject           " + ascii(subject))
        if lists:
            print("    lists             " + ascii(str(lists)))
        rest = (flags, subject, operation, template, count, lists)
        print("    as drawn          " + ascii(bounded_answer(generator, pattern, *rest)))
        print("    (*SKIP)->(*PRUNE) " + ascii(bounded_answer(generator, pattern.replace("(*SKIP)", "(*PRUNE)"), *rest)))
        print("    verb deleted      " + ascii(bounded_answer(generator, pattern.replace("(*SKIP)", ""), *rest)))

        if family == 1:
            # The whole control: a partial call cannot answer a match the same engine denies when
            # asked without the flag. Every line below is the SAME call with `partial=True` dropped.
            for label, pat in (
                ("as drawn", pattern),
                ("(*SKIP)->(*PRUNE)", pattern.replace("(*SKIP)", "(*PRUNE)")),
                ("verb deleted", pattern.replace("(*SKIP)", "")),
            ):
                got = without_the_partial(generator, pat, flags, subject, operation, lists)
                print(f"    no partial, {label:17} {ascii(got)}")

        if family == 2:
            # Upstream's own plain `search` over the whole subject, on each of the three lines. This
            # is what says HOW FAR the stale bound travelled: on 24224 the three agree, so only a
            # later match of the scan diverges and the bound crossed BETWEEN matches; on 38101 the
            # `(*SKIP)` line alone answers, so a failed attempt moved the bound and a later attempt
            # inside the SAME call read it.
            for label, pat in (
                ("as drawn", pattern),
                ("(*SKIP)->(*PRUNE)", pattern.replace("(*SKIP)", "(*PRUNE)")),
                ("verb deleted", pattern.replace("(*SKIP)", "")),
            ):
                got = compile_row(generator, pat, flags, lists).search(subject)
                print(f"    plain search, {label:17} {got.span() if got else None}")

            # THE PHANTOM END, MEASURED. Every span upstream's own scan of the drawn pattern
            # reports, against every position upstream's own `$` holds at: the ends that are not in
            # that list are the ones this entry is about. Nothing here is asserted from a constant.
            dollars = dollar_positions(subject, flags)
            scanned = [m.span() for m in compile_row(generator, pattern, flags, lists).finditer(subject)]
            phantom = [span for span in scanned if span[1] not in dollars]
            print(f"    upstream's own `$` is true at  {dollars}")
            print(f"    upstream's own scan spans      {scanned}")
            print(f"    ends where its own `$` is FALSE {[span[1] for span in phantom]}"
                  "   <- the whole of the finding")

            if not phantom:
                # A family-2 row with no end to explain is a row that does not belong in family 2,
                # and saying so beats crashing three lines later on an empty list.
                print("    NO PHANTOM END ON THIS ROW - every span upstream reports ends where its "
                      "own `$` holds, so family 2's argument does not apply to it")
                continue

            spelled = pattern.replace("$", DOLLAR_SPELLED_OUT)
            print("    `$` spelled out   " + ascii(answer(generator, spelled, *rest)))

            # THE `(?w)` CONTROL, and whether this row can take it, DERIVED rather than listed.
            # `(?w)` compiles `$` to END_OF_LINE_U (regex/_regex_core.py:506-510), the twin that
            # reads `text_end`. It is not a clean swap of one bound for another: it ALSO changes
            # which positions are line ends, on both of these rows and in both directions. What
            # decides whether it isolates anything is narrower - whether the PHANTOM POSITION is one
            # of the ones it changes. If `(?w)$` is true there, a `(?w)` run cannot tell a bound
            # that stopped being read from a line end that started existing, and it would confirm
            # the finding for the wrong reason.
            end = phantom[0][1]
            w_dollars = dollar_positions(subject, flags, unicode_lines=True)
            print(f"    (?w)$ is true at  {w_dollars}   (plain $ at {dollars}) - `(?w)` moves the "
                  "line ends on any row, so what matters is the phantom end alone")
            if end in w_dollars:
                print(f"    (?w) CANNOT ISOLATE here: the phantom end {end} is itself a `(?w)` line "
                      "end, so a (?w) run would answer for the wrong reason")
            else:
                print(f"    the phantom end {end} is NOT a `(?w)` line end either, so the twin "
                      "isolates the bound:")
                print("    (?w)$, the twin   " + ascii(answer(generator, "(?w)" + pattern, *rest)))

            # The caution, measured: an explicit `endpos` sets `slice_end` legitimately, so it brings
            # the phantom span back even with no verb in the pattern at all.
            bare = compile_row(generator, pattern.replace("(*SKIP)", ""), flags, lists)
            got = bare.search(subject, 0, end)
            print(f"    endpos={end} reproduces it verb-free  {got.span() if got else None}"
                  "   <- so a walk is NOT a control here")

        if family == 3:
            compiled = compile_row(generator, pattern, flags, lists)
            reverse = "(?r)" in pattern or bool(flags & 0x400)
            overlapped = operation.endswith("overlapped")
            how = ("reversed " if reverse else "") + ("overlapped " if overlapped else "") + "search"
            print(f"    stepwise {how:17} " + stepwise(compiled, subject, reverse, overlapped))
