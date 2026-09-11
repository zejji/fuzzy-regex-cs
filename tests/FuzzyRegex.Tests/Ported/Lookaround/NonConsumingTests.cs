using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Lookaround;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_non_consuming</c>
/// (lines 544-556).
/// </summary>
public sealed class NonConsumingTests
{
    [Test]
    [Arguments(@"(a(?=\s[^a]))", "a b")]
    [Arguments(@"(a(?=\s[^a]*))", "a b")]
    [Arguments(@"(a(?=\s[abc]))", "a b")]
    [Arguments(@"(a(?=\s[abc]*))", "a bc")]
    [Arguments(@"(a)(?=\s\1)", "a a")]
    [Arguments(@"(a)(?=\s\1*)", "a aa")]
    [Arguments(@"(a)(?=\s(abc|a))", "a a")]
    [Arguments(@"(a(?!\s[^a]))", "a a")]
    [Arguments(@"(a(?!\s[abc]))", "a d")]
    [Arguments(@"(a)(?!\s\1)", "a b")]
    [Arguments(@"(a)(?!\s(abc|a))", "a b")]
    [Property("Upstream", "RegexTests.test_non_consuming#1-11")]
    public void Group_before_a_non_consuming_lookahead_captures_a(string pattern, string subject) =>
        FuzzyRegex.MatchAtStart(subject, pattern).Groups[1].Value.Should().Be("a");
}
