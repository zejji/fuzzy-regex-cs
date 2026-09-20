"""The upstream answers S60's required-string prefilter pins assert.

S60 ports `locate_required_string` (`upstream/src/_regex.c:11082`) and the case-sensitive FORWARD
`string_search` arm it calls (`:6596`); `string_search_rev` and the folded arms are Phase 7, so the
reverse rows below record what the hole costs. A prefilter changes which start positions the
engine is offered, so every expected value in
`tests/FuzzyRegex.Tests/Gaps/Engine/RequiredStringPrefilterTests.cs` comes from a real upstream run
here, per the port-slice provenance rule: an expectation copied from the port's own output is not
evidence, because the port is what is under test.

Run:  python tools/probes/upstream-required-string-prefilter.py

Three groups, and the reason each is here:

1. **The first-unit versus required-unit trap** (slice item 15, from PCRE2 `pcre2_study.c`:
   "Patterns such as /a*a/ don't work if both the start unit and required unit are the same").
   If the locator steps back from the required string by `req_offset` and the pattern's own first
   unit can also be that character, a naive implementation searches from the wrong place.

2. **The verb-slice rule** (slice item 5, ROADMAP owner rule 2026-09-12). `(*SKIP)` and `(*PRUNE)`
   move the slice mid-attempt. A prefilter that takes its bounds from anywhere but the live state
   re-searches below a position the verb committed past. These rows are the control: this port's
   answers here are PERMANENT and upstream's are recorded for the record, not as the target -
   where the two differ, `docs/DIVERGENCES.md` and the ledger say which is which.

3. **The offset and reverse arms**, which are what `req_offset` and `string_search_rev` decide.

4. **A whole-subject walk**, because the locator's `req_pos` cache lives across a scan and a stale
   one shows up only on the second and third occurrence, not the first.

Both `regex.search` AND `regex.match` are printed for every row. They can differ precisely because
upstream's `search_start` prefilter screens start positions with predicates its own matcher does
not use (S22); S60 does NOT port `search_start`, so `search` is the row that matters here and
`match` is the cross-check that the locator alone changed nothing.
"""

import sys

import regex

# The astral rows print characters cp1252 has no encoding for, and a Windows console defaults to
# cp1252. Without this the probe dies part-way through with a UnicodeEncodeError.
sys.stdout.reconfigure(encoding="utf-8")

V = regex.VERSION1


def show(label, pattern, subject, flags=0, partial=False, pos=None, endpos=None):
    """Print one row's search and match answers, spans and groups."""
    kwargs = {}
    if pos is not None:
        kwargs["pos"] = pos
    if endpos is not None:
        kwargs["endpos"] = endpos
    if partial:
        kwargs["partial"] = True

    out = [f"{label:<46} {pattern!r:<40} {subject[:40]!r}"]
    for name in ("search", "match"):
        try:
            m = getattr(regex, name)(pattern, subject, flags | V, **kwargs)
        except Exception as exc:  # noqa: BLE001 - the answer for a raising row IS the exception
            out.append(f"  {name}: raised {type(exc).__name__}: {exc}")
            continue
        if m is None:
            out.append(f"  {name}: None")
        else:
            out.append(
                f"  {name}: span={m.span()} text={m.group()!r}"
                f" partial={m.partial} groups={m.groups()}"
            )
    print("\n".join(out))
    print()


def show_finditer(label, pattern, subject):
    """Print every span a whole-subject walk yields, which is what the ReqPos cache can spoil."""
    spans = [m.span() for m in regex.finditer(pattern, subject, V)]
    print(f"{label:<46} {pattern!r:<40} {subject[:40]!r}")
    print(f"  finditer: {spans}")
    print()


print(f"regex {regex.__version__}\n")

print("=== 1. first unit == required unit (slice item 15) ===\n")
show("a*a on 'a'", r"a*a", "a")
show("a*a on 'aaa'", r"a*a", "aaa")
show("a*a on 'baa'", r"a*a", "baa")
show("a*ab on 'aab'", r"a*ab", "aab")
show("a*ab on 'xaab'", r"a*ab", "xaab")
show("(a|a)*b, no b", r"(a|a)*b", "a" * 22)
show("(a|a)*b, with b", r"(a|a)*b", "a" * 8 + "b")

print("=== 2. the verb slice (slice item 5) ===\n")
show("SKIP before a required string", r"(?:a(*SKIP)x|b)needle", "aqbneedle")
show("SKIP then required string", r"a(*SKIP)bc", "aabc")
show("PRUNE then required string", r"a(*PRUNE)bc", "aabc")
show("SKIP inside a scan", r"\w+(*SKIP)needle", "xx needle")
# The row the whole slice's verb guard is pinned on: upstream's req_offset=3 starts at 3, the
# '(*SKIP)' steps 3 to 5, and 4 - the position that matches - is never tried. The port answers
# (4, 8) deliberately (ROADMAP owner rule 2026-09-12), so upstream's answer here is the record of
# the difference, not the target.
show("SKIP over the offset jump", r"(?:..(*SKIP)x|q)x", "ab cd xx")
show("SKIP, required string earlier", r"needle(*SKIP)x", "needleyneedlex")
show("FAIL after a required string", r"needle(*FAIL)", "a needle here")

print("=== 3. offset and reverse arms ===\n")
show("required string at a fixed offset", r"..needle", "xy needle no")
show("offset with a variable head", r"a+needle", "zzaaneedle")
show("reverse, plain", r"(?r)needle", "needle and needle")
show("reverse with an offset", r"(?r)needle..", "needle and needlexy")
show("required string absent", r"xyz+needle", "nothing here at all")
show("required string, pos bounded", r"needle", "needle needle", pos=3)
show("required string, endpos bounded", r"needle", "needle needle", endpos=5)

print("=== 4. partial matching over a required string ===\n")
show("partial, string truncated", r"needle", "a nee", partial=True)
show("partial, string complete", r"needlex", "a needle", partial=True)
show("partial, reverse", r"(?r)needle", "edle x", partial=True)

print("=== 5. zero-width and empty, with a required string ===\n")
show("required string, empty-matching head", r"x*needle", "  needle")
show("alternation, no single required string", r"needle|haystack", "a haystack")
show("required string inside a group", r"(needle)", "a needle")

print("=== 6. a long subject and the astral offset (S60 sitting 2) ===\n")
# 'Matcher.StringSearch' sweeps in one pass, but any block-at-a-time sweep - the 64 Ki chunking
# S60 tried, a Boyer-Moore skip table later - can drop an occurrence lying across a boundary.
# These rows are what a subject long enough to have boundaries answers.
CHUNK = 0x10000
show("needle straddling a 64 Ki boundary", r"needle", "x" * (CHUNK - 2) + "needle" + "x" * 100)
show("needle just before a boundary", r"needle", "x" * (CHUNK - 6) + "needle" + "x" * 100)
show("needle just after a boundary", r"needle", "x" * CHUNK + "needle" + "x" * 100)

# The anchored limit is 'slice_start + req_offset + value_count' CHARACTERS upstream. An astral
# needle is two UTF-16 code units per character, which is where a transliterated addition breaks.
show("astral backreference, mixed widths", "(\U0001F600.)\\1", "\U0001F600a\U0001F600a")
show("astral required string", "\U0001F600needle", "ab\U0001F600needle")

print("=== 7. a walk over a repeated required string ===\n")
show_finditer("every occurrence, not just the first", r"needle", "needle x needle y needle")
