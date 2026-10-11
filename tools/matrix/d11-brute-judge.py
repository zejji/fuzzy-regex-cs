r"""D11 round 3: the independent brute-force judge.

This file shares no code with the design model (`d11-direction-model.py`) or with the earlier models.
It asks one question, section 1 of the design note: could a longer text make this a match?

    python tools/probes/d11-brute-judge.py OP PATTERN SUBJECT     # one row, prints the verdict

The judge is upstream `regex` WITHOUT partial matching:

- if `regex.<op>(pattern, t)` matches, the answer is that complete match, `F(s,e)`;
- otherwise every non-empty continuation `w` of up to MAX_LEN characters over ALPHABET (plus the
  pattern's own ASCII letters and digits) is tried. Forward, `w` goes on the right of `t`; under `(?r)`
  it goes on the left;
- forward `match` / `fullmatch`: a partial `P(0,|t|)` when some `t + w` matches;
- forward `search`: `P(s,|t|)` for the least start `s` of `regex.search(t + w)` over all `w`, with a
  start inside `w` reported as `P(|t|,|t|)` (the convention upstream and the port share);
- reversed `match` / `fullmatch`: `P(0,|t|)` when some `w + t` matches;
- reversed `search`: `P(0,e)` for the greatest end `e`, in `t`'s coordinates, of `regex.search(w + t)`
  over all `w`; an end inside `w` is reported as `P(0,0)`.

A None from the judge is a lower bound: a completion needing more than MAX_LEN characters, or a
character outside the alphabet, is not found. Where upstream's own non-partial matcher is wrong, the
judge inherits the error (the design note lists the known cases).
"""

import itertools
import sys

import regex

# Word characters (a, b, 1, the Hebrew letter alef), non-word characters (space, colon, double quote)
# and the newline, which `$`, `^` and `.` treat specially.
ALPHABET = "ab1א :\"\n"
MAX_LEN = 4


def continuations(pattern, max_len=MAX_LEN):
    alphabet = sorted(set(ALPHABET) | {c for c in pattern if c.isascii() and c.isalnum()})
    for n in range(1, max_len + 1):
        for w in itertools.product(alphabet, repeat=n):
            yield "".join(w)


def leftmost_live(compiled, subject, max_len, reverse):
    """The partial start under BESTMATCH or ENHANCEMATCH: the least start (greatest end, reversed)
    from which some continuation matches, asked as an anchored match at each start.

    Owner ruling 2026-09-30 (DECISIONS, option A). Under those flags `search` does not return the
    leftmost match, so "the least start of `search(t + w)`" is not the partial rule there. The flags
    only rank matches, so whether some match starts at `s` is the question, and an anchored `match`
    at `s` asks it. A start inside `w` is reported as `|t|`, as above.
    """
    n = len(subject)
    words = list(continuations(compiled.pattern, max_len))
    if not reverse:
        for s in range(n + 1):
            for w in words:
                text = subject + w
                # At |t|, any start in w counts; a lookbehind still sees the text before `pos`.
                found = compiled.search(text, s) if s == n else compiled.match(text, s)
                if found is not None:
                    return ("P", s, n), w
        return None, None
    # SHORTCUT: reversed, the end is fixed with `endpos`, which hides a lookahead's view past it, so
    # a completion that needs one is missed (a None stays a lower bound). Upgrade: anchor the end
    # with a pattern rewrite rather than the slice. No reversed (?b)/(?e) partial row needed it on
    # 2026-10-01.
    for e in range(n, -1, -1):
        for w in words:
            text = w + subject
            found = compiled.search(text, 0, len(w)) if e == 0 else compiled.match(text, 0, len(w) + e)
            if found is not None:
                return ("P", 0, e), w
    return None, None


def judge(op, pattern, subject, max_len=MAX_LEN):
    """Returns (verdict, witness): verdict is ('F', s, e), ('P', s, e) or None; witness is a w."""
    compiled = regex.compile(pattern)
    reverse = bool(compiled.flags & regex.REVERSE)
    n = len(subject)
    complete = getattr(compiled, op)(subject)
    if complete is not None:
        return ("F",) + complete.span(), None
    if op == "search" and compiled.flags & (regex.BESTMATCH | regex.ENHANCEMATCH):
        return leftmost_live(compiled, subject, max_len, reverse)
    if op == "search":
        best, witness = None, None
        for w in continuations(pattern, max_len):
            text = w + subject if reverse else subject + w
            m = compiled.search(text)
            if m is None:
                continue
            if reverse:
                e = max(m.end() - len(w), 0)
                if best is None or e > best:
                    best, witness = e, w
                    if best == n:
                        break
            else:
                s = min(m.start(), n)
                if best is None or s < best:
                    best, witness = s, w
                    if best == 0:
                        break
        if best is None:
            return None, None
        return (("P", 0, best) if reverse else ("P", best, n)), witness
    for w in continuations(pattern, max_len):
        text = w + subject if reverse else subject + w
        if getattr(compiled, op)(text) is not None:
            return ("P", 0, n), w
    return None, None


def show(verdict):
    return "None" if verdict is None else f"{verdict[0]}({verdict[1]},{verdict[2]})"


if __name__ == "__main__":
    op, pattern, subject = sys.argv[1:4]
    v, w = judge(op, pattern, subject.encode().decode("unicode_escape"))
    print(show(v), "witness", ascii(w))
