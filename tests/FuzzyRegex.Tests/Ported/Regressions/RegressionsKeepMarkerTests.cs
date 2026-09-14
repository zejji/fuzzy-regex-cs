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
        Match m = Upstream.Match("abcd", @"(ab\Kcd)");

        m.Value.Should().Be("cd");
        m.Groups[1].Value.Should().Be("abcd");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#164")]
    public void Keep_marker_outside_a_group_shrinks_every_match_in_a_findall() =>
        Upstream.Matches("abcdefgh", @"\w\w\K\w\w").Select(static m => m.Value).Should().Equal("cd", "gh");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#165")]
    public void Keep_marker_inside_a_group_does_not_shrink_the_group_itself() =>
        // Groups[1], not Value: upstream's findall yields the ONE group's text when the pattern has
        // exactly one (pattern_findall, upstream/src/_regex.c:22440), and the whole point of the
        // upstream assertion is that \K shrinks the match without shrinking the group. Ported as
        // m.Value in Phase 1, which asserted the opposite of what upstream checks; found by S25.
        // Measured 2026-09-01: regex.findall(r'(\w\w\K\w\w)', 'abcdefgh') is ['abcd', 'efgh'] where
        // [m[0] for m in regex.finditer(...)] is ['cd', 'gh']. DECISIONS 2026-09-01.
        Upstream
            .Matches("abcdefgh", @"(\w\w\K\w\w)")
            .Select(static m => m.Groups[1].Value)
            .Should()
            .Equal("abcd", "efgh");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#166")]
    public void Keep_marker_drops_the_suffix_from_the_overall_match_when_searching_right_to_left()
    {
        Match m = Upstream.Match("abcd", @"(?r)(ab\Kcd)");

        m.Value.Should().Be("ab");
        m.Groups[1].Value.Should().Be("abcd");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#167")]
    public void Keep_marker_outside_a_group_shrinks_every_match_when_searching_right_to_left() =>
        Upstream.Matches("abcdefgh", @"(?r)\w\w\K\w\w").Select(static m => m.Value).Should().Equal("ef", "ab");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#168")]
    public void Keep_marker_inside_a_group_does_not_shrink_the_group_when_searching_right_to_left() =>
        // Groups[1] for the same reason as #165 above; measured 2026-09-01,
        // regex.findall(r'(?r)(\w\w\K\w\w)', 'abcdefgh') is ['efgh', 'abcd'] where the matches
        // themselves are 'ef' and 'ab'.
        Upstream
            .Matches("abcdefgh", @"(?r)(\w\w\K\w\w)")
            .Select(static m => m.Groups[1].Value)
            .Should()
            .Equal("efgh", "abcd");
}
