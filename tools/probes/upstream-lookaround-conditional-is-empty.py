#!/usr/bin/env python
"""S44, upstream issue 611: `LookAroundConditional.is_empty()` had its boolean precedence wrong.

`_regex_core.py:3287` read, at the 2026.8.12 pin (it is `:3289-3290` at 2026.9.10),

    return (self.subpattern.is_empty() and self.yes_item.is_empty() or
      self.no_item.is_empty())

which Python groups as `(a and b) or c`, so a lookaround conditional with an EMPTY NO-BRANCH
reported itself empty whatever its test and yes-branch were. Commit `1c90270`, released in
2026.8.30, drops the `or` arm:

    return self.subpattern.is_empty() and self.yes_item.is_empty()

Upstream titles it "Heap out-of-bounds write at compile time". Four places consult `is_empty()`
and each is reached by one of the cases below:

  :585   parse_quantifier - a quantifier on a "zero-width" item is DROPPED
  :1045  parse_conditional - a group-existence conditional whose branches are both "empty"
         becomes an empty Sequence, taking the groups inside it with it
  :2085  Atomic.optimise - an "empty" atomic group collapses to its subpattern
  :3164  LookAround.optimise - a POSITIVE lookaround whose subpattern is "empty" collapses to
         that subpattern, so a zero-width test starts CONSUMING

Run it against whichever interpreter you want to judge:

    python tools/probes/upstream-lookaround-conditional-is-empty.py

Verdict, measured 2026-09-13. Every case below is answered one way by 2026.7.19 (which is the
old pin 2026.8.12 byte for byte under `src/` and `regex/`) and another by 2026.9.10. The fourth
case is upstream's crash reaching Python: 2026.7.19 raises `MemoryError`, 2026.9.10 answers
no matches.

The `:2085` case was added later the same day, by S44's blind review, which found that the case
list covered three sites and not four. Its "before" answer is the only one here NOT taken from a
2026.7.19 interpreter - that release is not installed on this machine and the driver cannot
install it - but from 2026.9.10 with the pre-`1c90270` expression monkeypatched back onto
`LookAroundConditional.is_empty`, which is the whole of what the commit changed:

    (?>(?(?=b)b*|))b on 'bbb'   as shipped -> None      patched back -> (0, 3)
    (?>(?(?=b)b*|))b on 'bb'    as shipped -> None      patched back -> (0, 2)
    (?>(?(?=a)a*|))a on 'aaa'   as shipped -> None      patched back -> (0, 3)
"""

from __future__ import annotations

import regex

CASES = (
    (r"(?(?=a)b|)*", "ab", "search", ":585 - the quantifier is dropped, so the empty no-branch "
                                     "cannot match at 0"),
    (r"(?(?=a)b|)*", "aab", "finditer", ":585 again, seen across a whole scan"),
    (r"(x)(?(1)(?(?=a)b|)|)", "xa", "search",
     ":1045 - both branches read 'empty', so the whole conditional is dropped"),
    (r"(x)(?(1)(?(?=a)(b)|)|)", "xa", "finditer",
     ":1045 with a GROUP inside the dropped branch - the group-count desync, and the case that "
     "reaches Python as MemoryError"),
    (r"(?=(?(?=)b|))a", "abab", "search",
     ":3164 - the positive lookaround collapses, so its test starts consuming"),
    (r"(?(?=)b|){2,3}", "b", "search", ":585 with a bounded quantifier"),
    (r"(?>(?(?=b)b*|))b", "bbb", "search",
     ":2085 - the atomic group reads 'empty' and collapses to its subpattern, so 'b*' loses its "
     "atomicity and backtracks. Added 2026-09-13 by S44's blind review, which found this site "
     "was the one the case list did not reach"),
)


def main() -> None:
    print("regex", regex.__version__)
    for pattern, subject, operation, note in CASES:
        compiled = regex.compile(pattern)
        try:
            if operation == "finditer":
                answer = [m.span() for m in compiled.finditer(subject)]
            else:
                match = getattr(compiled, operation)(subject)
                answer = None if match is None else match.span()
        except MemoryError:
            # Not a real allocation failure: it is how the group-count desync surfaces in Python
            # on 2026.7.19. Caught so the remaining cases still print.
            answer = "MemoryError"
        print(f"  {pattern!r} on {subject!r} ({operation})\n    -> {answer}  # {note}")


if __name__ == "__main__":
    main()
