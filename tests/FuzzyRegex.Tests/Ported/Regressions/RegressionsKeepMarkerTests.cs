using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about <c>\K</c> (Hg issue 151: keep out of match).
/// </summary>
public sealed class RegressionsKeepMarkerTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#163")]
    public void Keep_marker_drops_the_prefix_from_the_overall_match_but_not_from_the_group()
    {
        Match m = FuzzyRegex.Match("abcd", @"(ab\Kcd)");

        m.Value.Should().Be("cd");
        m.Groups[1].Value.Should().Be("abcd");
    }

    [Test]
    [Skip("needs:find-all - the boundary opcodes land in S20; FuzzyRegex.Matches is S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#164")]
    public void Keep_marker_outside_a_group_shrinks_every_match_in_a_findall() =>
        FuzzyRegex.Matches("abcdefgh", @"\w\w\K\w\w").Select(m => m.Value).Should().Equal("cd", "gh");

    [Test]
    [Skip("needs:find-all - the boundary opcodes land in S20; FuzzyRegex.Matches is S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#165")]
    public void Keep_marker_inside_a_group_does_not_shrink_the_group_itself() =>
        FuzzyRegex.Matches("abcdefgh", @"(\w\w\K\w\w)").Select(m => m.Value).Should().Equal("abcd", "efgh");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#166")]
    public void Keep_marker_drops_the_suffix_from_the_overall_match_when_searching_right_to_left()
    {
        Match m = FuzzyRegex.Match("abcd", @"(?r)(ab\Kcd)");

        m.Value.Should().Be("ab");
        m.Groups[1].Value.Should().Be("abcd");
    }

    [Test]
    [Skip("needs:find-all - the boundary opcodes land in S20; FuzzyRegex.Matches is S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#167")]
    public void Keep_marker_outside_a_group_shrinks_every_match_when_searching_right_to_left() =>
        FuzzyRegex.Matches("abcdefgh", @"(?r)\w\w\K\w\w").Select(m => m.Value).Should().Equal("ef", "ab");

    [Test]
    [Skip("needs:find-all - the boundary opcodes land in S20; FuzzyRegex.Matches is S25")]
    [Property("Upstream", "RegexTests.test_hg_bugs#168")]
    public void Keep_marker_inside_a_group_does_not_shrink_the_group_when_searching_right_to_left() =>
        FuzzyRegex.Matches("abcdefgh", @"(?r)(\w\w\K\w\w)").Select(m => m.Value).Should().Equal("efgh", "abcd");
}
