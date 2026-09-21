r"""When does upstream report a partial match, and when does it refuse one?

S57d makes this port answer a partial where a word or grapheme boundary was decided against the end
of the available text, which is ledger entry 21 and PCRE2's `hitend` model. Upstream does not, so
the two engines now differ and the difference needs a rule rather than a list of rows. This probe is
that rule, measured.

The decisive pair is the first two rows. Both patterns reach the same `\B` at the same position of
the same subject, and both fail there. `a+` can ask for a third 'a' and be told the text has run
out; `aa` cannot. Only the first answers a partial, so it is the node running out of text that
upstream reports, never the boundary failing at the end of it.

    python tools/probes/upstream-partial-needs-text-exhaustion.py

WHAT IT MEASURED, regex 2026.9.10, on 2026-09-21:

  'a+\\B'                              'aa'     search=match (0, 1)     match=match (0, 1)     fullmatch=PARTIAL (0, 2)
  'aa\\B'                              'aa'     search=PARTIAL (1, 2)   match=None             fullmatch=None
  'a{2}\\B'                            'aa'     search=PARTIAL (1, 2)   match=None             fullmatch=None
  'a+b'                                'aa'     search=PARTIAL (0, 2)   match=PARTIAL (0, 2)   fullmatch=PARTIAL (0, 2)
  'x*\\B'                              'a'      search=PARTIAL (1, 1)   match=None             fullmatch=None
  'a?\\B'                              'a'      search=PARTIAL (1, 1)   match=None             fullmatch=None
  '^a\\B'                              'a'      search=None             match=None             fullmatch=None
  'True\\B'                            'True'   search=PARTIAL (4, 4)   match=None             fullmatch=None
  '(?!(True|False)\\b)(.*)'            'True'   search=match (1, 4)     match=None             fullmatch=None
  '(?r)\\b$'                           ''       search=PARTIAL (0, 0)   match=None             fullmatch=None
  '(?r)\\Ba'                           'a'      search=PARTIAL (0, 0)   match=None             fullmatch=None
  '(?r)\\Ba+'                          'a'      search=PARTIAL (0, 1)   match=PARTIAL (0, 1)   fullmatch=PARTIAL (0, 1)
  '(?r)\\b(?(?!\\p{L}).|[^a])\\K(\\s)' '\r\n'   search=PARTIAL (0, 0)   match=None             fullmatch=None

Four things follow, and each one pins a test:

  * The boundary never escalates. `aa\B` and `a{2}\B` refuse a partial at a position where `a+\B`
    grants one.
  * A repeat that is already satisfied still escalates, because it still asks. `x*\B` over 'a'
    answers a partial at 1 having consumed nothing, since the `x*` reads for an 'x' and the text
    stops. This is why almost every divergence S57d creates is in the anchored doors: an unanchored
    search retries at the end of the subject, where the first consuming node runs out and upstream
    answers that partial anyway. `^a\B` is the exception that shows the search door can differ too,
    because no later start position exists for the retry to use.
  * `(?r)\b$` over '' is the `search-start-partial` row (docs/DIVERGENCES.md). It is upstream's own
    search prefilter and not this rule: nothing is consumed and nothing runs out, and upstream's
    other two doors answer None, as the rule says they should.
  * Under `(?r)` the rule is the same one, read in the direction of travel: a reversed match runs
    out of text at position 0. `(?r)\Ba` and `(?r)\Ba+` over 'a' are the decisive pair again, and
    they answer as their forward twins do. The last row is the reversed row the wave found, and its
    fullmatch door is None for the same reason `aa\B` is: the `\b` fails at 0 having asked for
    nothing. Its search door answers a zero-width partial at 0, which is the `search-start-partial`
    prefilter above rather than this rule.
"""

import regex

print("regex", regex.__version__)

ROWS = [
    (r"a+\B", "aa", 0),    # a+ asks for a third 'a' and the text stops
    (r"aa\B", "aa", 0),    # same boundary, same position, nothing can run out
    (r"a{2}\B", "aa", 0),  # and the same with a counted repeat rather than two literals
    (r"a+b", "aa", 0),     # a node that runs out, with no boundary anywhere in the pattern
    (r"x*\B", "a", 0),     # a satisfied repeat that reads past the end anyway
    (r"a?\B", "a", 0),
    (r"^a\B", "a", 0),     # start-anchored, so the search cannot retry at the end of the subject
    (r"True\B", "True", 0),
    (r"(?!(True|False)\b)(.*)", "True", 0),
    (r"(?r)\b$", "", 0),
    # Reversed, where the end of the available text is its START. Rows 11 and 12 are the reversed
    # twin of the decisive pair: both consume the 'a' backwards and then ask a boundary at 0, and
    # `a+` can ask for a character before the text where `a` cannot.
    (r"(?r)\Ba", "a", 0),
    (r"(?r)\Ba+", "a", 0),
    # Row 6027 of the seed-20260921 6000-row gate, the one reversed row three seeds of the wave
    # found. Only its fullmatch door is asked there.
    (r"(?r)\b(?(?!\p{L}).|[^a])\K(\s)", "\r\n", regex.I | regex.V1),
]


def show(match):
    if match is None:
        return "None"
    return f"{'PARTIAL' if match.partial else 'match'} {match.span()}"


for pattern, subject, flags in ROWS:
    compiled = regex.compile(pattern, flags)
    doors = "   ".join(
        f"{door}={show(getattr(compiled, door)(subject, partial=True)):14}"
        for door in ("search", "match", "fullmatch")
    )
    print(f"  {pattern!r:36} {subject!r:8} {doors}")
