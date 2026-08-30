using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Format;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_format</c> (lines 2919-2932):
/// <c>str.format</c>-style replacement templates, by group number and by group name, through
/// <c>subf</c>, <c>subfn</c> and <c>Match.expandf</c>.
/// </summary>
/// <remarks>
/// Every result here is a plain ASCII string comparison, verified against the local oracle
/// 2026-08-30 and matching upstream verbatim.
/// </remarks>
public sealed class FormatTests
{
    [Test]
    [Skip("needs:format - ReplaceFormat is not implemented yet")]
    [Property("Upstream", "RegexTests.test_format#1")]
    public void ReplaceFormat_reorders_groups_by_number() =>
        FuzzyRegex.ReplaceFormat("foo bar", @"(\w+) (\w+)", "{0} => {2} {1}").Should().Be("foo bar => bar foo");

    [Test]
    [Skip("needs:format - ReplaceFormat is not implemented yet")]
    [Property("Upstream", "RegexTests.test_format#2")]
    public void ReplaceFormat_reorders_groups_by_name() =>
        FuzzyRegex.ReplaceFormat("foo bar", @"(?<word1>\w+) (?<word2>\w+)", "{word2} {word1}").Should().Be("bar foo");

    [Test]
    [Skip("needs:format - the ReplaceFormat(replacement, count, out replacements) overload is not implemented yet")]
    [Property("Upstream", "RegexTests.test_format#3")]
    public void ReplaceFormat_reports_the_string_and_the_replacement_count_by_number()
    {
        string result = new FuzzyRegex(@"(\w+) (\w+)").ReplaceFormat(
            "foo bar",
            "{0} => {2} {1}",
            -1,
            out int replacements
        );

        result.Should().Be("foo bar => bar foo");
        replacements.Should().Be(1);
    }

    [Test]
    [Skip("needs:format - the ReplaceFormat(replacement, count, out replacements) overload is not implemented yet")]
    [Property("Upstream", "RegexTests.test_format#4")]
    public void ReplaceFormat_reports_the_string_and_the_replacement_count_by_name()
    {
        string result = new FuzzyRegex(@"(?<word1>\w+) (?<word2>\w+)").ReplaceFormat(
            "foo bar",
            "{word2} {word1}",
            -1,
            out int replacements
        );

        result.Should().Be("bar foo");
        replacements.Should().Be(1);
    }

    [Test]
    [Skip("needs:format - Match.ResultFormat is not implemented yet")]
    [Property("Upstream", "RegexTests.test_format#5")]
    public void ResultFormat_reorders_groups_by_number_from_an_existing_match() =>
        FuzzyRegex
            .MatchAtStart("foo bar", @"(\w+) (\w+)")
            .ResultFormat("{0} => {2} {1}")
            .Should()
            .Be("foo bar => bar foo");
}
