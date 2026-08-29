using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Flags;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_scoped_and_inline_flags</c>
/// (lines 1394-1406).
/// </summary>
public sealed class ScopedAndInlineFlagsTests
{
    [Test]
    [Skip("needs:inline-flags - the parser does not compile flags yet")]
    [Property("Upstream", "RegexTests.test_scoped_and_inline_flags#1")]
    public void Inline_ignore_case_at_the_start_applies_to_the_rest_of_the_pattern()
    {
        Match m = FuzzyRegex.Match("ab", "(?i)Ab");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }

    [Test]
    [Skip("needs:inline-flags - the parser does not compile flags yet")]
    [Property("Upstream", "RegexTests.test_scoped_and_inline_flags#2")]
    public void Scoped_ignore_case_group_applies_only_inside_the_group()
    {
        Match m = FuzzyRegex.Match("ab", "(?i:A)b");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }

    [Test]
    [Skip("needs:inline-flags - the parser does not compile flags yet")]
    [Property("Upstream", "RegexTests.test_scoped_and_inline_flags#3")]
    public void Inline_ignore_case_after_the_pattern_content_does_not_apply_to_it() =>
        // Changed to positional flags in regex 2023.12.23.
        FuzzyRegex.Match("ab", "A(?i)b").Success.Should().BeFalse();

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?V0)/(?V1) yet")]
    [Property("Upstream", "RegexTests.test_scoped_and_inline_flags#4")]
    public void Inline_V0_flag_does_not_make_the_pattern_case_insensitive() =>
        FuzzyRegex.Match("ab", "(?V0)Ab").Success.Should().BeFalse();

    [Test]
    [Skip("needs:version-flags - the parser does not accept (?V0)/(?V1) yet")]
    [Property("Upstream", "RegexTests.test_scoped_and_inline_flags#5")]
    public void Inline_V1_flag_does_not_make_the_pattern_case_insensitive() =>
        FuzzyRegex.Match("ab", "(?V1)Ab").Success.Should().BeFalse();

    [Test]
    [Skip("needs:inline-flags - the parser does not compile flags yet")]
    [Property("Upstream", "RegexTests.test_scoped_and_inline_flags#6")]
    public void Inline_minus_i_turns_off_a_construct_time_ignore_case_option() =>
        FuzzyRegex.Match("ab", "(?-i)Ab", FuzzyRegexOptions.IgnoreCase).Success.Should().BeFalse();

    [Test]
    [Skip("needs:inline-flags - the parser does not compile flags yet")]
    [Property("Upstream", "RegexTests.test_scoped_and_inline_flags#7")]
    public void Scoped_minus_i_group_turns_off_a_construct_time_ignore_case_option() =>
        FuzzyRegex.Match("ab", "(?-i:A)b", FuzzyRegexOptions.IgnoreCase).Success.Should().BeFalse();

    [Test]
    [Skip("needs:inline-flags - the parser does not compile flags yet")]
    [Property("Upstream", "RegexTests.test_scoped_and_inline_flags#8")]
    public void Inline_minus_i_after_the_pattern_content_does_not_turn_off_ignore_case_for_it()
    {
        Match m = FuzzyRegex.Match("ab", "A(?-i)b", FuzzyRegexOptions.IgnoreCase);

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }
}
