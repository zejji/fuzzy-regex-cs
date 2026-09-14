using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The <c>{...:test}</c> constraint: one arm of upstream's <c>fuzzy_ext_match</c>
/// (<c>upstream/src/_regex.c:9938</c>) and <c>fuzzy_ext_match_group_fld</c> (<c>:10033</c>) at a
/// time, forward and reversed, plain and case-insensitive.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value was measured against <c>regex</c> 2026.7.19 on 2026-09-13 by
/// <c>tools/probes/upstream-fuzzy-ext-test.py</c>, and its line is quoted beside the assertion.
/// </para>
/// <para>
/// The constraint says which subject characters an error may touch. Only substitution and insertion
/// consult it - <c>next_fuzzy_match_item</c>'s INS and SUB arms, <c>:10131</c> and <c>:10160</c> -
/// because a deletion touches no subject character at all.
/// </para>
/// <para>
/// <b>Two of upstream's switches are asymmetric, and the tests below pin both.</b>
/// <c>fuzzy_ext_match</c> lists <c>SET_*</c> and <c>SET_*_IGN</c> but no <c>SET_*_REV</c> or
/// <c>SET_*_IGN_REV</c>, and <c>fuzzy_ext_match_group_fld</c> lists no <c>SET_*_IGN</c> either. An
/// opcode with no arm falls off the end of the switch to <c>return TRUE</c>, so those tests
/// constrain nothing. A test that is not a character class at all - <c>.</c> compiles to ANY - is a
/// no-op everywhere for the same reason.
/// </para>
/// </remarks>
public sealed class FuzzyTestConstraintTests
{
    [Test]
    public void A_character_test_allows_only_an_error_on_that_character()
    {
        // char-ins-ok: span=(0, 3) group='axc' counts=(0, 1, 0) changes=([], [1], [])
        Match ins = new FuzzyRegex("(?:[ab][cd]){e<=1:x}").FullMatch("axc");

        ins.Success.Should().BeTrue();
        ins.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        ins.FuzzyChanges.Insertions.Should().Equal(1);

        // char-ins-no: None
        new FuzzyRegex("(?:[ab][cd]){e<=1:x}")
            .FullMatch("azc")
            .Success.Should()
            .BeFalse();

        // char-sub-ok: span=(0, 3) group='acx' counts=(1, 0, 0) changes=([2], [], [])
        Match sub = new FuzzyRegex("(?:[ab][cd][ef]){e<=1:x}").FullMatch("acx");

        sub.Success.Should().BeTrue();
        sub.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        sub.FuzzyChanges.Substitutions.Should().Equal(2);

        // char-sub-no: None
        new FuzzyRegex("(?:[ab][cd][ef]){e<=1:x}")
            .FullMatch("acz")
            .Success.Should()
            .BeFalse();
    }

    [Test]
    public void A_negated_test_is_the_same_node_with_match_false()
    {
        // A one-character negated class optimises to CHARACTER with node->match FALSE rather than to
        // a set, so this is the arm's `== test_node->match` comparison and not a set membership.

        // char-neg-ins-ok: span=(0, 3) group='azc' counts=(0, 1, 0) changes=([], [1], [])
        Match ok = new FuzzyRegex("(?:[ab][cd]){e<=1:[^x]}").FullMatch("azc");

        ok.Success.Should().BeTrue();
        ok.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));

        // char-neg-ins-no: None
        new FuzzyRegex("(?:[ab][cd]){e<=1:[^x]}")
            .FullMatch("axc")
            .Success.Should()
            .BeFalse();
    }

    [Test]
    public void A_range_test_and_a_property_test_each_have_their_own_arm()
    {
        // range-ins-ok: span=(0, 3) group='a5c' counts=(0, 1, 0) changes=([], [1], [])
        // range-ins-no: None
        new FuzzyRegex("(?:[ab][cd]){e<=1:[0-9]}")
            .FullMatch("a5c")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?:[ab][cd]){e<=1:[0-9]}").FullMatch("axc").Success.Should().BeFalse();

        // prop-ins-ok: span=(0, 3) group='a5c' counts=(0, 1, 0) changes=([], [1], [])
        // prop-ins-no: None
        new FuzzyRegex(@"(?:[ab][cd]){e<=1:\d}")
            .FullMatch("a5c")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex(@"(?:[ab][cd]){e<=1:\d}").FullMatch("axc").Success.Should().BeFalse();
    }

    [Test]
    public void A_set_test_has_an_arm_going_forward()
    {
        // Upstream did not support a set as a fuzzy test until issue 371
        // (upstream/changelog.txt:543-545). '[0-9x]' is a union of two disjoint pieces, so it cannot
        // collapse to a RANGE or a CHARACTER.

        // set-ins-ok: span=(0, 3) group='axc' counts=(0, 1, 0) changes=([], [1], [])
        // set-ins-no: None
        new FuzzyRegex("(?:[ab][cd]){e<=1:[0-9x]}")
            .FullMatch("axc")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?:[ab][cd]){e<=1:[0-9x]}").FullMatch("azc").Success.Should().BeFalse();

        // set-sub-ok: span=(0, 3) group='acx' counts=(1, 0, 0) changes=([2], [], [])
        // set-sub-no: None
        new FuzzyRegex("(?:[ab][cd][ef]){e<=1:[0-9x]}")
            .FullMatch("acx")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?:[ab][cd][ef]){e<=1:[0-9x]}").FullMatch("acz").Success.Should().BeFalse();
    }

    [Test]
    public void The_other_three_set_opcodes_reach_the_same_arm()
    {
        // '[0-9x]' above is a SET_UNION. SET_DIFF, SET_INTER and SET_SYM_DIFF need the set-operator
        // syntax, which needs '(?V1)' - without it '[[a-z]--[aeiou]]' is `error: expected }`, so
        // these three are a V1-only shape and the generator, which draws V0 patterns, cannot reach
        // them. Upstream's switch gives all four the same arm; these are the rows that say so.

        // setdiff-ins-ok / setdiff-ins-no
        new FuzzyRegex("(?V1)(?:[ab][cd]){e<=1:[[a-z]--[aeiou]]}")
            .FullMatch("axc")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?V1)(?:[ab][cd]){e<=1:[[a-z]--[aeiou]]}").FullMatch("aec").Success.Should().BeFalse();

        // setinter-ins-ok / setinter-ins-no
        new FuzzyRegex("(?V1)(?:[ab][cd]){e<=1:[[a-z]&&[t-z]]}")
            .FullMatch("axc")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?V1)(?:[ab][cd]){e<=1:[[a-z]&&[t-z]]}").FullMatch("aec").Success.Should().BeFalse();

        // setsymdiff-ins-ok / setsymdiff-ins-no
        new FuzzyRegex("(?V1)(?:[ab][cd]){e<=1:[[a-z]~~[a-w]]}")
            .FullMatch("axc")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?V1)(?:[ab][cd]){e<=1:[[a-z]~~[a-w]]}").FullMatch("aec").Success.Should().BeFalse();
    }

    [Test]
    public void A_test_that_is_not_a_character_class_constrains_nothing()
    {
        // '.' compiles to ANY, which fuzzy_ext_match's switch does not list, so the function falls
        // through to `return TRUE` and the constraint allows every error - including one on a
        // newline, which ANY itself would not match.

        // any-noop: span=(0, 3) group='a\nc' counts=(0, 1, 0) changes=([], [1], [])
        Match m = new FuzzyRegex("(?:[ab][cd]){e<=1:.}").FullMatch("a\nc");

        m.Success.Should().BeTrue();
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Insertions.Should().Equal(1);
    }

    [Test]
    public void The_IGN_arms_fold_the_subject_character_before_testing_it()
    {
        // char-ign-ok / char-ign-no
        new FuzzyRegex("(?i)(?:[ab][cd]){e<=1:x}")
            .FullMatch("aXc")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?i)(?:[ab][cd]){e<=1:x}").FullMatch("aZc").Success.Should().BeFalse();

        // range-ign-ok / range-ign-no
        new FuzzyRegex("(?i)(?:[ab][cd]){e<=1:[x-z]}")
            .FullMatch("aYc")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?i)(?:[ab][cd]){e<=1:[x-z]}").FullMatch("aQc").Success.Should().BeFalse();

        // prop-ign-ok: '\p{Lu}' under (?i) matches a lowercase 'x'.
        new FuzzyRegex(@"(?i)(?:[ab][cd]){e<=1:\p{Lu}}")
            .FullMatch("axc")
            .Success.Should()
            .BeTrue();

        // set-ign-ok / set-ign-no
        new FuzzyRegex("(?i)(?:[ab][cd]){e<=1:[0-9x]}")
            .FullMatch("aXc")
            .Success.Should()
            .BeTrue();
        new FuzzyRegex("(?i)(?:[ab][cd]){e<=1:[0-9x]}").FullMatch("aZc").Success.Should().BeFalse();
    }

    [Test]
    public void The_REV_arms_test_the_character_before_the_position()
    {
        // Inside a lookbehind the section runs right-to-left, so the test node is a '_REV' opcode and
        // the arm reads `char_at(pos - 1)` against `pos > slice_start`. Where the constraint refuses
        // the substitution the match still succeeds by deleting instead, which is what makes the
        // counts rather than the success the measurement here.
        //
        // S40a ASKED FOR THE POSITIONS TO BE ASSERTED AND S47 IS WHERE THEY COULD BE. Until then a
        // fuzzy section inside a lookbehind reported change POSITIONS that contradicted its own
        // counts here - `(?<=(?:[ab][cd]){e<=1:x})$` against 'axc' answered `(1, 0, 0)` with a
        // *deletion* at 1 where upstream answers a substitution at 2 - because a failed search
        // attempt at an earlier position left its change on the list for the winning one to report.
        // That is ledger entry 11, mechanism A, fixed in S47 by clearing the change list on a search
        // restart; the first pair below now asserts the position upstream gives.
        //
        // THE REST STAY COUNTS-ONLY, and that is the test's own scope rather than a leftover: what
        // is under test is whether each `_REV` arm reads the character BEFORE the position, and the
        // counts are what discriminate a constraint that refused a substitution from one that
        // permitted it. Every row's upstream positions are recorded in the comment beside it.

        // char-rev-ok: counts=(1, 0, 0) changes=([2], [], [])
        // char-rev-no: counts=(0, 0, 1) changes=([], [], [2])
        Match charRevOk = new FuzzyRegex("(?<=(?:[ab][cd]){e<=1:x})$").Match("axc");
        charRevOk.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        charRevOk.FuzzyChanges.Substitutions.Should().Equal(2);

        Match charRevNo = new FuzzyRegex("(?<=(?:[ab][cd]){e<=1:x})$").Match("azc");
        charRevNo.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        charRevNo.FuzzyChanges.Deletions.Should().Equal(2);

        // range-rev-ok / range-rev-no
        new FuzzyRegex("(?<=(?:[ab][cd]){e<=1:[0-9]})$")
            .Match("a5c")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0));
        new FuzzyRegex("(?<=(?:[ab][cd]){e<=1:[0-9]})$").Match("axc").FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));

        // prop-rev-ok / prop-rev-no
        new FuzzyRegex(@"(?<=(?:[ab][cd]){e<=1:\d})$")
            .Match("a5c")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0));
        new FuzzyRegex(@"(?<=(?:[ab][cd]){e<=1:\d})$").Match("axc").FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));

        // char-ign-rev-ok / char-ign-rev-no / range-ign-rev-no
        new FuzzyRegex("(?i)(?<=(?:[ab][cd]){e<=1:x})$")
            .Match("aXc")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0));
        new FuzzyRegex("(?i)(?<=(?:[ab][cd]){e<=1:x})$").Match("aZc").FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        new FuzzyRegex("(?i)(?<=(?:[ab][cd]){e<=1:[x-z]})$")
            .Match("aQc")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(0, 0, 1));
    }

    [Test]
    public void A_reversed_set_test_constrains_nothing_because_upstream_has_no_arm_for_it()
    {
        // fuzzy_ext_match lists SET_DIFF..SET_UNION and their _IGN forms but no _REV or _IGN_REV
        // form, so a reversed set test falls through to `return TRUE`. Forward, '[0-9x]' refuses a
        // substitution on 'z'; reversed, it permits it - which is how these two rows differ from the
        // reversed CHARACTER, RANGE and PROPERTY rows above.

        // set-rev-noop: counts=(1, 0, 0) changes=([2], [], [])
        new FuzzyRegex("(?<=(?:[ab][cd]){e<=1:[0-9x]})$")
            .Match("azc")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0));

        // set-ign-rev-noop: counts=(1, 0, 0) changes=([2], [], [])
        new FuzzyRegex("(?i)(?<=(?:[ab][cd]){e<=1:[0-9x]})$")
            .Match("aZc")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void An_error_inside_a_case_folding_asks_the_folded_character()
    {
        // 'ß' full-folds to "ss", so `(?fi)(ß)(?:\1){e<=1:...}` against "ßsx" puts the error inside a
        // folding and reaches fuzzy_ext_match_group_fld rather than fuzzy_ext_match. The character
        // the constraint sees is the folded one - 'x' at text_pos 2 - not the group's.

        // gfld ...{e<=1:s} 'ßss': counts=(0, 0, 0) - an exact match never consults the constraint.
        Fold("s", "ßss").FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // gfld ...{e<=1:s} 'ßsx': None, and ...{e<=1:x}: counts=(1, 0, 0) changes=([2], [], [])
        Fold("s", "ßsx").Success.Should().BeFalse();

        Match sub = Fold("x", "ßsx");

        sub.Success.Should().BeTrue();
        sub.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        sub.FuzzyChanges.Substitutions.Should().Equal(2);

        // RANGE_IGN and PROPERTY_IGN have arms here too.
        Fold("[a-w]", "ßsx").Success.Should().BeFalse();
        Fold("[a-z]", "ßsx").Success.Should().BeTrue();
        Fold(@"\d", "ßsx").Success.Should().BeFalse();
        Fold(@"\w", "ßsx").Success.Should().BeTrue();
    }

    [Test]
    public void A_case_insensitive_set_test_inside_a_folding_constrains_nothing()
    {
        // fuzzy_ext_match_group_fld lists SET_DIFF..SET_UNION but no SET_*_IGN, so under (?fi) - the
        // only way to reach the function at all - a set test has no arm and returns TRUE. '[sq]' does
        // not contain 'x', and the substitution on 'x' is permitted anyway.

        // gfld ...{e<=1:[sq]} 'ßsx': counts=(1, 0, 0) changes=([2], [], [])
        Match m = Fold("[sq]", "ßsx");

        m.Success.Should().BeTrue();
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void The_folded_test_asks_the_character_at_the_fold_position_not_the_first_one()
    {
        // The arm reads `folded_char_at(text_pos, folded_pos)`, and this is the only shape where the
        // index matters. 'ﬆ' (U+FB06) folds to "st" and 'ß' folds to "ss": against each other the
        // comparison agrees on the first folded character and differs on the second, so the error is
        // tried at folded_pos 1, where the character is 't' and not the 's' that folded_pos 0 gives.
        // Every other folding this port produces - "ss", "ff" - has two identical characters and
        // could not tell the two apart. The oracle generator reaches this shape on about one row in
        // two thousand, so these six assertions are the real coverage of the index, not the wave.

        // gfld-pos s: None / gfld-pos t: span=(0, 2) counts=(1, 0, 0) changes=([1], [], [])
        FoldPos("s").Success.Should().BeFalse();
        FoldPos("[a-s]").Success.Should().BeFalse();

        Match m = FoldPos("t");

        m.Success.Should().BeTrue();
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1);

        // gfld-pos [a-t] / gfld-pos [st]: both contain 't', so both allow it.
        FoldPos("[a-t]").Success.Should().BeTrue();
        FoldPos("[st]").Success.Should().BeTrue();
    }

    [Test]
    public void The_reversed_folded_arms_read_one_character_and_one_fold_position_back()
    {
        // '(?r)' reaches these; a lookbehind does not, because the group has to be captured before
        // '\1' is read and a right-to-left lookbehind reads '\1' first.

        // gfld-rev x / [a-z] / [sq] / \w: span=(0, 3) counts=(1, 0, 0) changes=([2], [], [])
        // gfld-rev s / q / [a-w] / [^x] / \d: None
        FoldRev("x").Success.Should().BeTrue();
        FoldRev("[a-z]").Success.Should().BeTrue();
        FoldRev(@"\w").Success.Should().BeTrue();

        FoldRev("s").Success.Should().BeFalse();
        FoldRev("q").Success.Should().BeFalse();
        FoldRev("[a-w]").Success.Should().BeFalse();
        FoldRev(@"\d").Success.Should().BeFalse();

        // '[^x]' is a negated CHARACTER_IGN_REV rather than a set, so its arm is consulted and its
        // node->match is FALSE.
        FoldRev("[^x]").Success.Should().BeFalse();

        // '[sq]' is a real SET_UNION_IGN_REV, which has no arm in either switch.
        FoldRev("[sq]").Success.Should().BeTrue();
    }

    [Test]
    public void A_zero_error_budget_is_a_constraint_upstream_does_not_elide()
    {
        // Upstream issue 596 reports '{e<=0}' costing a 210x slowdown against the plain pattern
        // because the FUZZY node is built and stepped through regardless. Eliding it is Phase 7's
        // question; what this pins is that the answers are identical.

        // zero-budget-match: span=(0, 2) counts=(0, 0, 0) / zero-budget-miss: None
        Match ok = new FuzzyRegex("(?:[ab][cd]){e<=0}").FullMatch("ac");

        ok.Success.Should().BeTrue();
        ok.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
        new FuzzyRegex("(?:[ab][cd]){e<=0}").FullMatch("axc").Success.Should().BeFalse();

        // zero-budget-test: a test constraint on a budget of zero is reached and never consulted.
        new FuzzyRegex("(?:[ab][cd]){e<=0:x}")
            .FullMatch("ac")
            .Success.Should()
            .BeTrue();
    }

    [Test]
    public void A_test_upstream_refuses_at_run_time_is_still_refused_here()
    {
        // Upstream's parser accepts each of these and emits exactly this port's bytecode; its C
        // engine then answers `RuntimeError: invalid RE code`, measured by the probe. S15 refuses
        // them at compile time and NodeGraphTests pins the other five; this is the backreference one
        // the slice file names, which no test held before. It is refused earlier than those five and
        // so carries the unresolved-backreference wording rather than the bare one, which is why the
        // assertion is on the substring.
        Action compile = static () => _ = new FuzzyRegex(@"(a)(?:abc){e<=1:\1}");

        compile.Should().Throw<NotSupportedException>().WithMessage("*invalid RE code*");
    }

    private static Match Fold(string test, string subject) =>
        new FuzzyRegex($@"(?fi)(\N{{LATIN SMALL LETTER SHARP S}})(?:\1){{e<=1:{test}}}").FullMatch(subject);

    // 'ß' folds to "ss" and 'ﬆ' folds to "st", so the two agree on the first folded character and
    // differ on the second.
    private static Match FoldPos(string test) =>
        new FuzzyRegex($@"(?fi)(\N{{LATIN SMALL LETTER SHARP S}})(?:\1){{e<=1:{test}}}").FullMatch("ßﬆ");

    private static Match FoldRev(string test) =>
        new FuzzyRegex($@"(?fir)(?:\1){{e<=1:{test}}}(\N{{LATIN SMALL LETTER SHARP S}})").Match("sxß");
}
