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
/// <c>(?r)</c> variants (#7-12) are right-to-left, not duplicates of #1-6, and were tagged
/// <c>needs:right-to-left</c> until S23 delivered that direction, even though every expected value
/// is identical to its unflagged sibling.
/// </remarks>
public sealed class FullMatchTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#1")]
    public void Full_match_succeeds_when_the_pattern_covers_the_whole_subject() =>
        Upstream.FullMatch("abc", "abc").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#2")]
    public void Full_match_fails_when_trailing_text_is_left_over() =>
        Upstream.FullMatch("abcx", "abc").Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#3")]
    public void Full_match_succeeds_when_endpos_excludes_the_trailing_text() =>
        Upstream.Compile("abc").FullMatch("abcx", length: 3).Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#4")]
    public void Full_match_succeeds_when_pos_skips_the_leading_text() =>
        Upstream.Compile("abc").FullMatch("xabc", beginning: 1).Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#5")]
    public void Full_match_fails_when_pos_skips_the_leading_text_but_trailing_text_remains() =>
        Upstream.Compile("abc").FullMatch("xabcy", beginning: 1).Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#6")]
    public void Full_match_succeeds_when_pos_and_endpos_bracket_exactly_the_match() =>
        Upstream.Compile("abc").FullMatch("xabcy", beginning: 1, length: 3).Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#7")]
    public void Reversed_full_match_succeeds_when_the_pattern_covers_the_whole_subject() =>
        Upstream.FullMatch("abc", "(?r)abc").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#8")]
    public void Reversed_full_match_fails_when_trailing_text_is_left_over() =>
        Upstream.FullMatch("abcx", "(?r)abc").Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#9")]
    public void Reversed_full_match_succeeds_when_endpos_excludes_the_trailing_text() =>
        Upstream.Compile("(?r)abc").FullMatch("abcx", length: 3).Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#10")]
    public void Reversed_full_match_succeeds_when_pos_skips_the_leading_text() =>
        Upstream.Compile("(?r)abc").FullMatch("xabc", beginning: 1).Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#11")]
    public void Reversed_full_match_fails_when_pos_skips_the_leading_text_but_trailing_text_remains() =>
        Upstream.Compile("(?r)abc").FullMatch("xabcy", beginning: 1).Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_fullmatch#12")]
    public void Reversed_full_match_succeeds_when_pos_and_endpos_bracket_exactly_the_match() =>
        Upstream.Compile("(?r)abc").FullMatch("xabcy", beginning: 1, length: 3).Success.Should().BeTrue();
}
