using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_448951</c> (lines 815-822).
/// </summary>
/// <remarks>
/// Upstream loops <c>for op in '', '?', '*':</c> over the quantifier applied inside the group;
/// the three iterations are unrolled into <c>[Arguments]</c> rows.
/// </remarks>
public sealed class Bug448951Tests
{
    [Test]
    [Arguments("")]
    [Arguments("?")]
    [Arguments("*")]
    [Property("Upstream", "RegexTests.test_bug_448951#1")]
    public void A_leading_optional_group_may_be_absent(string op)
    {
        Match m = FuzzyRegex.MatchAtStart("z", $"((.{op}):)?z");

        m.Value.Should().Be("z");
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[2].Success.Should().BeFalse();
    }

    [Test]
    [Arguments("")]
    [Arguments("?")]
    [Arguments("*")]
    [Property("Upstream", "RegexTests.test_bug_448951#2")]
    public void A_leading_optional_group_captures_greedily_when_present(string op)
    {
        Match m = FuzzyRegex.MatchAtStart("a:z", $"((.{op}):)?z");

        m.Value.Should().Be("a:z");
        m.Groups[1].Value.Should().Be("a:");
        m.Groups[2].Value.Should().Be("a");
    }
}
