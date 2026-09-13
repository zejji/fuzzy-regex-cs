"""Ground truth for S39's gap tests: fuzzy strings, backreferences and the repeat-one loops.

Run: python tools/probes/upstream-fuzzy-strings.py
Measured against regex 2026.7.19 on 2026-09-13; every line it prints is quoted beside the
assertion it pins in tests/FuzzyRegex.Tests/Gaps/Engine/FuzzyStringTests.cs.
"""

import regex


def out(line):
    print(line.encode("unicode_escape").decode("ascii"))


def show(label, how, pattern, subject, **kwargs):
    p = regex.compile(pattern)
    m = getattr(p, how)(subject, **kwargs)
    if m is None:
        out(f"{label} {how}({pattern!r}, {subject!r}): None")
        return
    extra = f" partial={m.partial}" if kwargs.get("partial") else ""
    out(
        f"{label} {how}({pattern!r}, {subject!r}): span={m.span()} "
        f"counts={m.fuzzy_counts} changes={m.fuzzy_changes}{extra}"
    )


print(f"regex {regex.__version__}")

# --- plain STRING, one of each error type -----------------------------------
show("sub", "match", "(?:abc){e<=1}", "axc")
show("ins", "match", "(?:abc){e<=1}", "abxc")
show("del", "match", "(?:abc){e<=1}", "ac")
show("trailing-ins", "fullmatch", "(?:abc){e<=1}", "abcx")
show("leading-ins", "search", "(?:abc){e<=1}", "xabc")

# --- STRING_REV -------------------------------------------------------------
show("rev-sub", "match", "(?r)(?:abc){e<=1}", "axc")
show("rev-ins", "match", "(?r)(?:abc){e<=1}", "abxc")
show("rev-del", "match", "(?r)(?:abc){e<=1}", "ac")

# --- STRING_IGN -------------------------------------------------------------
show("ign-sub", "match", "(?i)(?:abc){e<=1}", "AxC")
show("ign-rev-sub", "match", "(?ri)(?:abc){e<=1}", "AxC")

# --- STRING_FLD: a multi-character fold --------------------------------------
show("fld-exact", "match", "(?fi)(?:straße){e<=1}", "STRASSE")
show("fld-sub", "match", "(?fi)(?:straße){e<=1}", "STRASSX")
show("fld-inside", "match", "(?fi)(?:straße){e<=1}", "STRASXE")
show("fld-st", "match", "(?fi)(?:ﬆx){e<=1}", "STY")
show("fld-rev", "match", "(?fir)(?:straße){e<=1}", "STRASSX")

# --- REF_GROUP --------------------------------------------------------------
show("ref-sub", "match", "(abc)(?:\\1){e<=1}", "abcabx")
show("ref-del", "match", "(abc)(?:\\1){e<=1}", "abcab")
show("ref-ins", "match", "(abc)(?:\\1){e<=1}", "abcabxc")
show("ref-ign", "match", "(?i)(abc)(?:\\1){e<=1}", "abcABX")
show("ref-rev", "match", "(?r)(?:\\1){e<=1}(abc)", "abxabc")
show("ref-fld", "match", "(?fi)(straße)(?:\\1){e<=1}", "straßeSTRASSX")

# --- the *_REPEAT_ONE fuzzy loops -------------------------------------------
show("greedy-one", "match", "(?:a+x){e<=1}", "aaay")
show("greedy-one2", "search", "(?:[a-c]+x){e<=1}", "zabcy")
show("lazy-one", "match", "(?:a+?x){e<=1}", "aaay")
show("lazy-one2", "match", "(?:a*?x){e<=1}", "aay")

# --- astral: change positions are codepoints upstream, code units here -------
show("astral", "match", "(?:\U0001f600bc){e<=1}", "\U0001f600bx")
