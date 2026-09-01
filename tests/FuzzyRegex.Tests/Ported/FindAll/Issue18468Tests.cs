using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.FindAll;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_issue_18468</c>
/// (lines 2955-3030), the <c>regex.findall</c> assertions.
/// </summary>
/// <remarks>
/// <para>
/// There is no <c>findall</c> on this API; every assertion here uses
/// <see cref="FuzzyRegex.Matches(string, string, FuzzyRegexOptions, IReadOnlyDictionary{string, IReadOnlyCollection{string}})"/> instead, per the same convention as <c>FindAllTests</c>.
/// </para>
/// <para>
/// Upstream loops <c>for string in "a:b::c:::d", StrSubclass("a:b::c:::d"):</c>; both iterations
/// run the same source line, so they share one assertion index here rather than getting two - the
/// <c>StrSubclass</c> iteration is not separately ported. The following <c>bytes</c>/
/// <c>bytearray</c>/<c>memoryview</c>/<c>BytesSubclass</c> loop (assertions #20-22) has no
/// equivalent in this char-based engine and is not ported.
/// </para>
/// </remarks>
public sealed class Issue18468Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#17")]
    public void Matches_value_is_the_whole_match_when_the_pattern_has_no_groups() =>
        FuzzyRegex.Matches("a:b::c:::d", ":+").Select(m => m.Value).Should().Equal(":", "::", ":::");

    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#18")]
    public void Matches_group_one_value_is_used_when_the_pattern_has_exactly_one_group() =>
        FuzzyRegex.Matches("a:b::c:::d", "(:+)").Select(m => m.Groups[1].Value).Should().Equal(":", "::", ":::");

    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#19")]
    public void Matches_group_one_value_for_a_two_group_pattern() =>
        FuzzyRegex.Matches("a:b::c:::d", "(:)(:*)").Select(m => m.Groups[1].Value).Should().Equal(":", ":", ":");

    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#19")]
    public void Matches_group_two_value_for_a_two_group_pattern() =>
        FuzzyRegex.Matches("a:b::c:::d", "(:)(:*)").Select(m => m.Groups[2].Value).Should().Equal("", ":", "::");
}
