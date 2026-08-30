using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.FullMatch;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fullmatch</c>
/// (lines 2933-2954).
/// </summary>
/// <remarks>
/// Upstream's <c>pos</c>/<c>endpos</c> keyword arguments become the instance
/// <c>FuzzyRegex.FullMatch(string, int, int, bool)</c>
/// overload's <c>beginning</c>/<c>length</c>, with <c>length = endpos - beginning</c>. The
/// <c>(?r)</c> variants (#7-12) are right-to-left, not duplicates of #1-6, and are tagged
/// <c>needs:right-to-left</c> even though every expected value is identical to its unflagged
/// sibling.
/// </remarks>
public sealed class FullMatchTests
{
    [Test]
    [Skip("needs:full-match - Pattern.FullMatch is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#1")]
    public void Full_match_succeeds_when_the_pattern_covers_the_whole_subject() =>
        FuzzyRegex.FullMatch("abc", "abc").Success.Should().BeTrue();

    [Test]
    [Skip("needs:full-match - Pattern.FullMatch is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#2")]
    public void Full_match_fails_when_trailing_text_is_left_over() =>
        FuzzyRegex.FullMatch("abcx", "abc").Success.Should().BeFalse();

    [Test]
    [Skip("needs:full-match - Pattern.FullMatch is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#3")]
    public void Full_match_succeeds_when_endpos_excludes_the_trailing_text() =>
        new FuzzyRegex("abc").FullMatch("abcx", length: 3).Success.Should().BeTrue();

    [Test]
    [Skip("needs:full-match - Pattern.FullMatch is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#4")]
    public void Full_match_succeeds_when_pos_skips_the_leading_text() =>
        new FuzzyRegex("abc").FullMatch("xabc", beginning: 1).Success.Should().BeTrue();

    [Test]
    [Skip("needs:full-match - Pattern.FullMatch is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#5")]
    public void Full_match_fails_when_pos_skips_the_leading_text_but_trailing_text_remains() =>
        new FuzzyRegex("abc").FullMatch("xabcy", beginning: 1).Success.Should().BeFalse();

    [Test]
    [Skip("needs:full-match - Pattern.FullMatch is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#6")]
    public void Full_match_succeeds_when_pos_and_endpos_bracket_exactly_the_match() =>
        new FuzzyRegex("abc").FullMatch("xabcy", beginning: 1, length: 3).Success.Should().BeTrue();

    [Test]
    [Skip("needs:right-to-left - (?r) right-to-left full matching is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#7")]
    public void Reversed_full_match_succeeds_when_the_pattern_covers_the_whole_subject() =>
        FuzzyRegex.FullMatch("abc", "(?r)abc").Success.Should().BeTrue();

    [Test]
    [Skip("needs:right-to-left - (?r) right-to-left full matching is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#8")]
    public void Reversed_full_match_fails_when_trailing_text_is_left_over() =>
        FuzzyRegex.FullMatch("abcx", "(?r)abc").Success.Should().BeFalse();

    [Test]
    [Skip("needs:right-to-left - (?r) right-to-left full matching is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#9")]
    public void Reversed_full_match_succeeds_when_endpos_excludes_the_trailing_text() =>
        new FuzzyRegex("(?r)abc").FullMatch("abcx", length: 3).Success.Should().BeTrue();

    [Test]
    [Skip("needs:right-to-left - (?r) right-to-left full matching is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#10")]
    public void Reversed_full_match_succeeds_when_pos_skips_the_leading_text() =>
        new FuzzyRegex("(?r)abc").FullMatch("xabc", beginning: 1).Success.Should().BeTrue();

    [Test]
    [Skip("needs:right-to-left - (?r) right-to-left full matching is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#11")]
    public void Reversed_full_match_fails_when_pos_skips_the_leading_text_but_trailing_text_remains() =>
        new FuzzyRegex("(?r)abc").FullMatch("xabcy", beginning: 1).Success.Should().BeFalse();

    [Test]
    [Skip("needs:right-to-left - (?r) right-to-left full matching is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fullmatch#12")]
    public void Reversed_full_match_succeeds_when_pos_and_endpos_bracket_exactly_the_match() =>
        new FuzzyRegex("(?r)abc").FullMatch("xabcy", beginning: 1, length: 3).Success.Should().BeTrue();
}
