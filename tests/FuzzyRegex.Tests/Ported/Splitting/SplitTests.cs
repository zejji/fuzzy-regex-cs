using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Splitting;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_re_split</c> (lines 256-305).
/// </summary>
/// <remarks>
/// Only the <c>sys.version_info >= (3, 7, 0)</c> branch of upstream's version guard (assertions
/// #2-5) is ported; the pre-3.7 branch produced a different split count for a zero-width match
/// and is not a useful oracle. Upstream's <c>regex.splititer</c> is not ported - see
/// <c>docs/PORTMAP.md</c> - so each assertion that only re-runs the previous one through
/// <c>splititer</c> is recorded as NOT PORTED rather than written.
/// </remarks>
public sealed class SplitTests
{
    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#1")]
    public void Split_on_a_literal_colon_keeps_empty_pieces() =>
        FuzzyRegex.Split(":a:b::c", ":").Should().Equal("", "a", "b", "", "c");

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#2")]
    public void Split_on_a_star_quantifier_also_splits_on_the_empty_matches_between_characters() =>
        FuzzyRegex.Split(":a:b::c", ":*").Should().Equal("", "", "a", "", "b", "", "c", "");

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#3")]
    public void Split_with_a_capturing_star_group_keeps_the_empty_captures_too() =>
        FuzzyRegex
            .Split(":a:b::c", "(:*)")
            .Should()
            .Equal("", ":", "", "", "a", ":", "", "", "b", "::", "", "", "c", "", "");

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#4")]
    public void Split_with_a_non_capturing_star_group_behaves_like_the_uncaptured_form() =>
        FuzzyRegex.Split(":a:b::c", "(?::*)").Should().Equal("", "", "a", "", "b", "", "c", "");

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#5")]
    public void Split_with_a_repeated_capturing_group_reports_null_for_iterations_that_did_not_capture() =>
        FuzzyRegex
            .Split(":a:b::c", "(:)*")
            .Should()
            .Equal("", ":", "", null, "a", ":", "", null, "b", ":", "", null, "c", null, "");

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#6")]
    public void Split_on_a_character_class_plus_quantifier_keeps_the_captured_run() =>
        FuzzyRegex.Split(":a:b::c", "([b:]+)").Should().Equal("", ":", "a", ":b::", "c");

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#7")]
    public void Split_on_an_alternation_of_two_capturing_groups_nulls_the_one_that_did_not_match() =>
        FuzzyRegex
            .Split(":a:b::c", "(b)|(:+)")
            .Should()
            .Equal("", null, ":", "a", null, ":", "", "b", null, "", null, "::", "c");

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#8")]
    public void Split_on_an_alternation_of_two_non_capturing_groups_reports_no_group_pieces() =>
        FuzzyRegex.Split(":a:b::c", "(?:b)|(?::+)").Should().Equal("", "a", "", "", "c");

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#9")]
    public void Split_on_a_literal_delimiter_that_repeats() =>
        FuzzyRegex.Split("xaxbxc", "x").Should().Equal("", "a", "b", "c");

    // NOT PORTED: assertion #10 (lines 283-284) re-runs assertion #9 through regex.splititer,
    // which is not ported - see docs/PORTMAP.md.

    [Test]
    [Skip("needs:splitting - right-to-left matching works from S23; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_re_split#11")]
    public void Split_right_to_left_on_a_literal_delimiter() =>
        FuzzyRegex.Split("xaxbxc", "(?r)x").Should().Equal("c", "b", "a", "");

    // NOT PORTED: assertion #12 (lines 287-288) re-runs assertion #11 through regex.splititer,
    // which is not ported - see docs/PORTMAP.md.

    [Test]
    [Skip("needs:splitting - Pattern.Split is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_split#13")]
    public void Split_on_an_alternation_of_two_groups_only_one_of_which_can_match() =>
        FuzzyRegex.Split("xaxbxc", "(x)|(y)").Should().Equal("", "x", null, "a", "x", null, "b", "x", null, "c");

    // NOT PORTED: assertion #14 (lines 292-293) re-runs assertion #13 through regex.splititer,
    // which is not ported - see docs/PORTMAP.md.

    [Test]
    [Skip("needs:splitting - right-to-left matching works from S23; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_re_split#15")]
    public void Split_right_to_left_on_an_alternation_of_two_groups() =>
        FuzzyRegex.Split("xaxbxc", "(?r)(x)|(y)").Should().Equal("c", "x", null, "b", "x", null, "a", "x", null, "");

    // NOT PORTED: assertion #16 (lines 297-298) re-runs assertion #15 through regex.splititer,
    // which is not ported - see docs/PORTMAP.md.

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_re_split#17")]
    public void Split_on_a_word_boundary_keeps_both_words_and_the_separators() =>
        FuzzyRegex.Split("a b c", @"(?V1)\b").Should().Equal("", "a", " ", "b", " ", "c", "");

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_re_split#18")]
    public void Split_on_a_start_of_word_boundary_keeps_the_leading_edge_with_the_next_word() =>
        FuzzyRegex.Split("a b c", @"(?V1)\m").Should().Equal("", "a ", "b ", "c");

    [Test]
    [Skip("needs:splitting - the boundary opcodes land in S20; FuzzyRegex.Split is S25")]
    [Property("Upstream", "RegexTests.test_re_split#19")]
    public void Split_on_an_end_of_word_boundary_keeps_the_trailing_edge_with_the_previous_word() =>
        FuzzyRegex.Split("a b c", @"(?V1)\M").Should().Equal("a", " b", " c", "");
}
