"""Ground truth for S40's `{...:test}` constraint - upstream `fuzzy_ext_match` (src/_regex.c:9938).

Run with the upstream interpreter, e.g. `.venvs/regex-2026.9.10/Scripts/python.exe`, or with the
PyPI fallback the oracle itself uses. The version it ran on is the first line of its output.

The constraint says which subject characters an error is allowed to touch. Only substitution and
insertion consult it (`next_fuzzy_match_item`'s INS and SUB arms, :10131 and :10160); deletion does
not, because a deletion touches no subject character.

Two upstream asymmetries this probe exists to pin, both visible in the switch's arm list:

* `fuzzy_ext_match` has no `SET_*_REV` or `SET_*_IGN_REV` arm, so a reversed *set* test falls off
  the end of the switch to `return TRUE` and constrains nothing. A reversed CHARACTER, PROPERTY or
  RANGE test has its `_REV` arm and does constrain.
* `fuzzy_ext_match_group_fld` (:10033) has no `SET_*_IGN` arm either, so a case-insensitive set test
  is a no-op when the error lands inside a case folding.

Anything that is not one of the listed opcodes - `.` compiles to ANY, which has no arm anywhere -
is likewise a no-op constraint.

The *non*-`_IGN` arms of `fuzzy_ext_match_group_fld` are unreachable: that function runs only when
both sides are being full-case-folded, which needs `(?fi)`, and the fuzzy-test grammar accepts only
a character set - `{e<=1:(?-i:x)}` is `error: expected character set`, measured below - so a test
compiled under `(?fi)` is always `_IGN`. They are ported anyway, because upstream writes them.
"""

import regex


def show(label, m):
    if m is None:
        print(f"{label}: None")
        return
    print(
        f"{label}: span={m.span()} group={ascii(m.group())} counts={m.fuzzy_counts} "
        f"changes={m.fuzzy_changes}"
    )


print("regex", regex.__version__)

# --- forward arms, one per matches_* family --------------------------------------------------
# CHARACTER: only an inserted 'x' is allowed.
show("char-ins-ok", regex.fullmatch(r"(?:[ab][cd]){e<=1:x}", "axc"))
show("char-ins-no", regex.fullmatch(r"(?:[ab][cd]){e<=1:x}", "azc"))
show("char-sub-ok", regex.fullmatch(r"(?:[ab][cd][ef]){e<=1:x}", "acx"))
show("char-sub-no", regex.fullmatch(r"(?:[ab][cd][ef]){e<=1:x}", "acz"))

# CHARACTER with match=FALSE: '[^x]' negates the test node.
show("char-neg-ins-ok", regex.fullmatch(r"(?:[ab][cd]){e<=1:[^x]}", "azc"))
show("char-neg-ins-no", regex.fullmatch(r"(?:[ab][cd]){e<=1:[^x]}", "axc"))

# RANGE: '[0-9]' is a single range.
show("range-ins-ok", regex.fullmatch(r"(?:[ab][cd]){e<=1:[0-9]}", "a5c"))
show("range-ins-no", regex.fullmatch(r"(?:[ab][cd]){e<=1:[0-9]}", "axc"))

# PROPERTY: '\d'.
show("prop-ins-ok", regex.fullmatch(r"(?:[ab][cd]){e<=1:\d}", "a5c"))
show("prop-ins-no", regex.fullmatch(r"(?:[ab][cd]){e<=1:\d}", "axc"))

# SET: a union of two disjoint pieces, which cannot collapse to a RANGE.
show("set-ins-ok", regex.fullmatch(r"(?:[ab][cd]){e<=1:[0-9x]}", "axc"))
show("set-ins-no", regex.fullmatch(r"(?:[ab][cd]){e<=1:[0-9x]}", "azc"))
show("set-sub-ok", regex.fullmatch(r"(?:[ab][cd][ef]){e<=1:[0-9x]}", "acx"))
show("set-sub-no", regex.fullmatch(r"(?:[ab][cd][ef]){e<=1:[0-9x]}", "acz"))

# The other three set opcodes need the set-operator syntax, which needs '(?V1)'. Without it
# '[[a-z]--[aeiou]]' is `error: expected }`, so SET_DIFF, SET_INTER and SET_SYM_DIFF as a fuzzy test
# are a V1-only shape.
show("setdiff-ins-ok", regex.fullmatch(r"(?V1)(?:[ab][cd]){e<=1:[[a-z]--[aeiou]]}", "axc"))
show("setdiff-ins-no", regex.fullmatch(r"(?V1)(?:[ab][cd]){e<=1:[[a-z]--[aeiou]]}", "aec"))
show("setinter-ins-ok", regex.fullmatch(r"(?V1)(?:[ab][cd]){e<=1:[[a-z]&&[t-z]]}", "axc"))
show("setinter-ins-no", regex.fullmatch(r"(?V1)(?:[ab][cd]){e<=1:[[a-z]&&[t-z]]}", "aec"))
show("setsymdiff-ins-ok", regex.fullmatch(r"(?V1)(?:[ab][cd]){e<=1:[[a-z]~~[a-w]]}", "axc"))
show("setsymdiff-ins-no", regex.fullmatch(r"(?V1)(?:[ab][cd]){e<=1:[[a-z]~~[a-w]]}", "aec"))

# ANY has no arm: '.' constrains nothing, so even a newline passes it.
show("any-noop", regex.fullmatch(r"(?:[ab][cd]){e<=1:.}", "a\nc"))

# --- the _IGN arms ---------------------------------------------------------------------------
show("char-ign-ok", regex.fullmatch(r"(?i)(?:[ab][cd]){e<=1:x}", "aXc"))
show("char-ign-no", regex.fullmatch(r"(?i)(?:[ab][cd]){e<=1:x}", "aZc"))
show("range-ign-ok", regex.fullmatch(r"(?i)(?:[ab][cd]){e<=1:[x-z]}", "aYc"))
show("range-ign-no", regex.fullmatch(r"(?i)(?:[ab][cd]){e<=1:[x-z]}", "aQc"))
show("prop-ign-ok", regex.fullmatch(r"(?i)(?:[ab][cd]){e<=1:\p{Lu}}", "axc"))
show("set-ign-ok", regex.fullmatch(r"(?i)(?:[ab][cd]){e<=1:[0-9x]}", "aXc"))
show("set-ign-no", regex.fullmatch(r"(?i)(?:[ab][cd]){e<=1:[0-9x]}", "aZc"))

# --- the _REV arms, reached from inside a lookbehind ------------------------------------------
show("char-rev-ok", regex.search(r"(?<=(?:[ab][cd]){e<=1:x})$", "axc"))
show("char-rev-no", regex.search(r"(?<=(?:[ab][cd]){e<=1:x})$", "azc"))
show("range-rev-ok", regex.search(r"(?<=(?:[ab][cd]){e<=1:[0-9]})$", "a5c"))
show("range-rev-no", regex.search(r"(?<=(?:[ab][cd]){e<=1:[0-9]})$", "axc"))
show("prop-rev-ok", regex.search(r"(?<=(?:[ab][cd]){e<=1:\d})$", "a5c"))
show("prop-rev-no", regex.search(r"(?<=(?:[ab][cd]){e<=1:\d})$", "axc"))
show("char-ign-rev-ok", regex.search(r"(?i)(?<=(?:[ab][cd]){e<=1:x})$", "aXc"))
show("char-ign-rev-no", regex.search(r"(?i)(?<=(?:[ab][cd]){e<=1:x})$", "aZc"))
show("range-ign-rev-no", regex.search(r"(?i)(?<=(?:[ab][cd]){e<=1:[x-z]})$", "aQc"))

# SET_*_REV has no arm: a reversed set test constrains nothing, where the forward one does.
show("set-rev-noop", regex.search(r"(?<=(?:[ab][cd]){e<=1:[0-9x]})$", "azc"))
show("set-ign-rev-noop", regex.search(r"(?i)(?<=(?:[ab][cd]){e<=1:[0-9x]})$", "aZc"))

# --- the folded-group arms -------------------------------------------------------------------
# 'ss' folds to 'ss' and the group folds too, so an error can land inside a folding and reach
# fuzzy_ext_match_group_fld rather than fuzzy_ext_match.
for pat, sub in [
    # CHARACTER_IGN: the arm is consulted and the folded character is 'x', not 's'.
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:s}", "ßss"),
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:s}", "ßsx"),
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:x}", "ßsx"),
    # RANGE_IGN and PROPERTY_IGN have arms too.
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:[a-w]}", "ßsx"),
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:[a-z]}", "ßsx"),
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:\d}", "ßsx"),
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:\w}", "ßsx"),
    # SET_*_IGN has no arm in fuzzy_ext_match_group_fld, so it constrains nothing.
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:[sq]}", "ßsx"),
    # No error at all: the constraint is never consulted.
    (r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:\d}", "ßss"),
]:
    show(f"gfld {ascii(pat)} {ascii(sub)}", regex.fullmatch(pat, sub))

# WHICH character of the folding the test sees. 'ﬆ' (U+FB06) folds to "st" and 'ß' folds to "ss", so
# against each other the comparison agrees on the first folded character and differs on the second:
# the error is tried at `folded_pos == 1`, where `folded_char_at` answers 't' and not the 's' that
# `folded_pos == 0` would give. A test containing one and not the other is the only way to tell.
# Every other folding this port can produce - "ss", "ff" - has two identical characters and cannot.
for test in ["s", "t", "[a-s]", "[a-t]", "[st]"]:
    show(
        f"gfld-pos {test}",
        regex.fullmatch(
            r"(?fi)(\N{LATIN SMALL LETTER SHARP S})(?:\1){e<=1:%s}" % test,
            "ßﬆ",
        ),
    )

# The reversed folded-group arms read `folded_char_at(text_pos - 1, folded_pos - 1)`. `(?r)` is what
# reaches them: a lookbehind will not, because the group has to be captured before `\1` is read and
# a right-to-left lookbehind reads `\1` first.
for test in ["s", "x", "q", "[a-w]", "[a-z]", "[^x]", "[sq]", r"\d", r"\w"]:
    pat = r"(?fir)(?:\1){e<=1:%s}(\N{LATIN SMALL LETTER SHARP S})" % test
    show(f"gfld-rev {test}", regex.search(pat, "sxß"))

# --- '{e<=0}' is a no-op constraint upstream does not elide (issue 596) ------------------------
show("zero-budget-match", regex.fullmatch(r"(?:[ab][cd]){e<=0}", "ac"))
show("zero-budget-miss", regex.fullmatch(r"(?:[ab][cd]){e<=0}", "axc"))
show("zero-budget-test", regex.fullmatch(r"(?:[ab][cd]){e<=0:x}", "ac"))

# --- the tests upstream's parser accepts and its engine then refuses ---------------------------
for pat in [r"a{e<=1:\X}", r"a{e<=1:\b}", r"a{e<=1:\A}", r"a{e<=1:\Z}"]:
    try:
        print(f"reject {ascii(pat)}: {regex.fullmatch(pat, 'a')!r}")
    except Exception as exc:  # noqa: BLE001 - the exception type is the measurement
        print(f"reject {ascii(pat)}: {type(exc).__name__}: {exc}")

try:
    print(
        "reject backref test:",
        repr(regex.fullmatch(r"(a)(?:abc){e<=1:\1}", "aabc")),
    )
except Exception as exc:  # noqa: BLE001
    print(f"reject backref test: {type(exc).__name__}: {exc}")

try:
    print("reject inline flags:", repr(regex.fullmatch(r"(?:ab){e<=1:(?-i:x)}", "axb")))
except Exception as exc:  # noqa: BLE001
    print(f"reject inline flags: {type(exc).__name__}: {exc}")

try:
    print(
        "reject named list:",
        repr(regex.fullmatch(r"a{e<=1:\L<w>}", "a", w=["x"])),
    )
except Exception as exc:  # noqa: BLE001
    print(f"reject named list: {type(exc).__name__}: {exc}")
