"""Upstream answers for the demo's v2 feature samples (S72).

Every expected span, count and replaced string in `demo/wwwroot/examples.json`,
`tests/FuzzyRegex.Tests/Gaps/Demo/` and the v2 page tests comes from THIS script's output, run
against the real Python `regex` module - never from the demo's own output, which would be the demo
grading its own homework (S72's slice file, "Verification").

Run:  python tools/probes/demo-v2-expectations.py

Indices are printed as UTF-16 code units, because that is what the demo's JSON carries and what a
JavaScript string slices with. Every subject here is BMP-only, so the conversion is the identity;
it is done anyway so that a later sample with an astral character cannot quietly print codepoints.
"""

import regex

VERSION = (regex.__version__, regex.DEFAULT_VERSION)


def utf16(text: str, index: int) -> int:
    """The UTF-16 code-unit offset of a codepoint offset."""
    return len(text[:index].encode("utf-16-le")) // 2


# Every compile below adds VERSION1. Upstream still defaults to V0; this port defaults to version 1
# ("Version 1 is the default", docs/COMPARISON.md), so a V0 expectation would be an expectation for
# a different engine than the one the demo runs.
V1 = regex.VERSION1


def show(match, subject: str) -> str:
    if match is None:
        return "    no match"
    start = utf16(subject, match.start())
    end = utf16(subject, match.end())
    lines = [f"    match       utf16 index={start} length={end - start}  text={match.group()!r}"]
    if match.fuzzy_counts != (0, 0, 0):
        sub, ins, dele = match.fuzzy_counts
        lines.append(f"    fuzzy_counts (sub, ins, del) = ({sub}, {ins}, {dele})")
    if match.partial:
        lines.append("    partial = True")
    for number in range(1, (match.re.groups or 0) + 1):
        if match.start(number) < 0:
            lines.append(f"    group {number}          no match")
            continue
        gstart = utf16(subject, match.start(number))
        gend = utf16(subject, match.end(number))
        lines.append(f"    group {number}          utf16 index={gstart} length={gend - gstart}  text={match.group(number)!r}")
    return "\n".join(lines)


def search(label: str, pattern: str, subject: str, **kwargs) -> None:
    keywords = "".join(f", {name}={value!r}" for name, value in kwargs.items() if name != "flags")
    flags = kwargs.pop("flags", 0)
    flagtext = f", flags={flags}" if flags else ""
    print(f"{label}: compile({pattern!r}{flagtext} | VERSION1).search({subject!r}{keywords})")
    print(show(regex.compile(pattern, flags | V1).search(subject, **kwargs), subject))


def finditer(label: str, pattern: str, subject: str, flags: int = 0) -> None:
    flagtext = f", flags={flags}" if flags else ""
    print(f"{label}: compile({pattern!r}{flagtext}).finditer({subject!r})")
    found = list(regex.compile(pattern, flags | V1).finditer(subject))
    if not found:
        print("    no match")
    for match in found:
        print(show(match, subject))


def substitute(label: str, pattern: str, replacement: str, subject: str) -> None:
    print(f"{label}: sub({pattern!r}, {replacement!r}, {subject!r})")
    print(f"    replaced = {regex.sub(pattern, replacement, subject, flags=V1)!r}")


print(f"regex {VERSION[0]} (DEFAULT_VERSION={VERSION[1]}), UTF-16 offsets")
print()

# --- fuzzy budgets -----------------------------------------------------------------------------
# The three forms the slice names: a total budget, per-kind budgets, and a weighted cost equation.
# The cost case is docs/COMPARISON.md's own worked example, so the demo and the documentation say
# the same thing about it.
search("fuzzy-total", "(?:colour){e<=2}", "the color of the collar")
search("fuzzy-per-kind", "(?:foobar){i<=1,d<=1,s<=1}", "xfoobat")
search("fuzzy-cost", "(foobar){i<=1,d<=2,s<=3,2d+1s<4}", "3oifaowefbaoraofuiebofasebfaobfaorfeoaro")

# --- BESTMATCH and ENHANCEMATCH, as a pair on one subject --------------------------------------
# COMPARISON.md's own subject for both, so the page shows the documented answer. The plain run is
# printed beside each flagged one because a sample whose answer is the same either way demonstrates
# nothing (S72 "Hunt").
search("plain-for-bestmatch", "(foobar){e}", "xirefoabralfobarxie")
search("bestmatch", "(foobar){e}", "xirefoabralfobarxie", flags=regex.BESTMATCH)
search("enhancematch", "(foobar){e}", "xirefoabralfobarxie", flags=regex.ENHANCEMATCH)

# --- named lists -------------------------------------------------------------------------------
# \L<name> against a supplied list, with a fuzzy budget on top: the list is matched as a set of
# literal alternatives, and the budget lets a misspelling reach one.
print("named-list: compile(r'(?:\\L<fruit>){e<=1}', fruit=['apple', 'banana', 'cherry']).finditer('aple bananna cherry')")
subject = "aple bananna cherry"
for match in regex.compile(r"(?:\L<fruit>){e<=1}", V1, fruit=["apple", "banana", "cherry"]).finditer(subject):
    print(show(match, subject))

# --- POSIX leftmost-longest --------------------------------------------------------------------
# Without POSIX the alternation takes the first branch that matches; with it, the longest.
search("leftmost-first", "a|ab|abc", "abcd")
search("posix-leftmost-longest", "a|ab|abc", "abcd", flags=regex.POSIX)

# --- partial matching --------------------------------------------------------------------------
# "so far, so good": the subject ran out before the pattern did, which is how a search box tells a
# half-typed entry from a wrong one.
search("partial-prefix", r"\d{4}-\d{2}-\d{2}", "2026-09", partial=True)
search("partial-whole", r"\d{4}-\d{2}-\d{2}", "2026-09-19", partial=True)
search("partial-wrong", r"\d{4}-\d{2}-\d{2}", "not a date", partial=True)

# --- reverse searching -------------------------------------------------------------------------
# The last match instead of the first, found by searching from the right rather than by walking
# every match and keeping the last.
search("forward-first", r"\w+", "one two three")
search("reverse-last", r"\w+", "one two three", flags=regex.REVERSE)

# --- replace templates -------------------------------------------------------------------------
# Upstream's template language, which is \1 and \g<name>, not $1.
substitute("replace-numbered", r"(\d{4})-(\d{2})", r"\2/\1", "2026-09 and 1999-12")
substitute("replace-named", r"(?<year>\d{4})-(?<month>\d{2})", r"\g<month>/\g<year>", "2026-09 and 1999-12")
substitute("replace-fuzzy", r"(?:colour){e<=1}", "colour", "the color of the collar")

# --- timeouts ----------------------------------------------------------------------------------
# No upstream counterpart is printed: the demo's 2-second budget is its own trust-boundary
# contract, not a parity claim, and the sample's expected answer is the demo's error sentence.
print("timeout: no upstream expectation - the demo's own MatchTimeout contract")
