using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Quantifiers;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_search_star_plus</c>
/// (lines 66-80).
/// </summary>
/// <remarks>
/// Upstream's <c>span(0)</c> and <c>span()</c> are the same call, so its ten assertions are six
/// distinct behaviours here; the pairs are folded and the provenance names both indices.
/// </remarks>
public sealed class SearchStarPlusTests
{
    [Test]
    [Arguments("a*", "xxx", 0, 0)]
    [Arguments("x*", "axx", 0, 0)]
    [Arguments("x+", "axx", 1, 3)]
    [Property("Upstream", "RegexTests.test_search_star_plus#1-4")]
    public void Search_spans(string pattern, string subject, int start, int end)
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_search_star_plus#5")]
    public void Search_for_an_absent_literal_does_not_match() =>
        FuzzyRegex.Match("aaa", "x").Success.Should().BeFalse();

    [Test]
    [Arguments("a*", "xxx", 0, 0)]
    [Arguments("x*", "xxxa", 0, 3)]
    [Property("Upstream", "RegexTests.test_search_star_plus#6-9")]
    public void MatchAtStart_spans(string pattern, string subject, int start, int end)
    {
        Match m = FuzzyRegex.MatchAtStart(subject, pattern);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_search_star_plus#10")]
    public void MatchAtStart_requires_the_pattern_to_start_at_the_beginning() =>
        FuzzyRegex.MatchAtStart("xxx", "a+").Success.Should().BeFalse();
}
