using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Lookaround;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_lookbehind</c>
/// (lines 1417-1463).
/// </summary>
/// <remarks>
/// The final upstream assertion, <c>repr(type(regex.compile(r"(a)\2(b)"))) ==
/// self.PATTERN_CLASS</c> (lines 1462-1463), is a Python type-identity check and does not port.
/// </remarks>
public sealed class LookbehindTests
{
    [Test]
    [Skip("needs:lookaround - the engine has no lookbehind opcodes yet")]
    [Property("Upstream", "RegexTests.test_lookbehind#1")]
    public void Lookbehind_asserting_a_preceding_digit_run_after_a_letter_matches()
    {
        Match m = FuzzyRegex.Match("a123", @"123(?<=a\d+)");

        m.Index.Should().Be(1);
        m.Length.Should().Be(3);
    }

    [Test]
    [Skip("needs:lookaround - the engine has no lookbehind opcodes yet")]
    [Property("Upstream", "RegexTests.test_lookbehind#2")]
    public void Lookbehind_asserting_a_preceding_digit_run_after_a_letter_fails_without_the_letter() =>
        FuzzyRegex.Match("b123", @"123(?<=a\d+)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - the engine has no lookbehind opcodes yet")]
    [Property("Upstream", "RegexTests.test_lookbehind#3")]
    public void Negative_lookbehind_fails_when_the_preceding_letter_is_present() =>
        FuzzyRegex.Match("a123", @"123(?<!a\d+)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - the engine has no lookbehind opcodes yet")]
    [Property("Upstream", "RegexTests.test_lookbehind#4")]
    public void Negative_lookbehind_matches_when_the_preceding_letter_is_absent()
    {
        Match m = FuzzyRegex.Match("b123", @"123(?<!a\d+)");

        m.Index.Should().Be(1);
        m.Length.Should().Be(3);
    }

    [Test]
    [Skip("needs:lookaround - the engine has no lookbehind opcodes yet")]
    [Property("Upstream", "RegexTests.test_lookbehind#5")]
    public void Positive_lookbehind_between_two_groups_matches_when_it_holds() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?<=b)(c)").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - the engine has no lookbehind opcodes yet")]
    [Property("Upstream", "RegexTests.test_lookbehind#6")]
    public void Positive_lookbehind_between_two_groups_fails_when_it_does_not_hold() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?<=c)(c)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - the engine has no lookahead opcodes yet")]
    [Property("Upstream", "RegexTests.test_lookbehind#7")]
    public void Positive_lookahead_between_two_groups_matches_when_it_holds() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?=c)(c)").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - the engine has no lookahead opcodes yet")]
    [Property("Upstream", "RegexTests.test_lookbehind#8")]
    public void Positive_lookahead_between_two_groups_fails_when_it_does_not_hold() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?=b)(c)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#9")]
    public void Lookbehind_with_a_conditional_on_group_2_choosing_the_wrong_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?<=(?(2)x|c))c").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#10")]
    public void Lookbehind_with_a_conditional_on_group_2_choosing_the_untaken_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?<=(?(2)b|x))c").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#11")]
    public void Lookbehind_with_a_conditional_on_group_2_choosing_the_right_branch_matches() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?<=(?(2)x|b))c").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#12")]
    public void Lookbehind_with_a_conditional_on_group_1_choosing_the_wrong_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?<=(?(1)c|x))c").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#13")]
    public void Lookbehind_with_a_conditional_on_group_1_choosing_the_right_branch_matches() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?<=(?(1)b|x))c").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#14")]
    public void Lookahead_with_a_conditional_on_group_2_choosing_the_right_branch_matches() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?=(?(2)x|c))c").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#15")]
    public void Lookahead_with_a_conditional_on_group_2_choosing_the_untaken_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?=(?(2)c|x))c").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#16")]
    public void Lookahead_with_a_conditional_on_group_2_choosing_the_right_branch_matches_again() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?=(?(2)x|c))c").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#17")]
    public void Lookahead_with_a_conditional_on_group_1_choosing_the_wrong_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?=(?(1)b|x))c").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#18")]
    public void Lookahead_with_a_conditional_on_group_1_choosing_the_right_branch_matches() =>
        FuzzyRegex.MatchAtStart("abc", "(?:(a)|(x))b(?=(?(1)c|x))c").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#19")]
    public void Lookbehind_after_a_captured_group_with_a_conditional_on_group_2_choosing_the_wrong_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?<=(?(2)x|c))(c)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#20")]
    public void Lookbehind_after_a_captured_group_with_a_conditional_on_group_2_choosing_the_untaken_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?<=(?(2)b|x))(c)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#21")]
    public void Lookbehind_after_a_captured_group_with_a_conditional_on_group_1_choosing_the_wrong_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?<=(?(1)c|x))(c)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#22")]
    public void Lookbehind_after_a_captured_group_with_a_conditional_on_group_1_choosing_the_right_branch_matches() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?<=(?(1)b|x))(c)").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#23")]
    public void Lookahead_after_a_captured_group_with_a_conditional_on_group_2_choosing_the_right_branch_matches() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?=(?(2)x|c))(c)").Success.Should().BeTrue();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#24")]
    public void Lookahead_after_a_captured_group_with_a_conditional_on_group_2_choosing_the_untaken_branch_fails() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?=(?(2)b|x))(c)").Success.Should().BeFalse();

    [Test]
    [Skip("needs:lookaround - S21 delivered the group-existence conditional; only the enclosing lookbehind is left")]
    [Property("Upstream", "RegexTests.test_lookbehind#25")]
    public void Lookahead_after_a_captured_group_with_a_conditional_on_group_1_choosing_the_right_branch_matches() =>
        FuzzyRegex.MatchAtStart("abc", "(a)b(?=(?(1)c|x))(c)").Success.Should().BeTrue();
}
