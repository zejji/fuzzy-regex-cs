using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_re_subn</c> (lines 249-254).
/// </summary>
public sealed class SubnTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_re_subn#1")]
    public void Replace_reports_the_string_and_the_replacement_count_case_insensitively()
    {
        string result = Upstream.Compile("(?i)b+").Replace("bbbb BBBB", "x", -1, out int replacements);

        result.Should().Be("x x");
        replacements.Should().Be(2);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_subn#2")]
    public void Replace_reports_the_string_and_the_replacement_count()
    {
        string result = Upstream.Compile("b+").Replace("bbbb BBBB", "x", -1, out int replacements);

        result.Should().Be("x BBBB");
        replacements.Should().Be(1);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_subn#3")]
    public void Replace_reports_zero_replacements_when_nothing_matches()
    {
        string result = Upstream.Compile("b+").Replace("xyz", "x", -1, out int replacements);

        result.Should().Be("xyz");
        replacements.Should().Be(0);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_subn#4")]
    public void Replace_counts_every_empty_match_of_a_star_quantifier()
    {
        string result = Upstream.Compile("b*").Replace("xyz", "x", -1, out int replacements);

        result.Should().Be("xxxyxzx");
        replacements.Should().Be(4);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_subn#5")]
    public void Replace_with_a_count_stops_early_and_reports_only_that_many_replacements()
    {
        string result = Upstream.Compile("b*").Replace("xyz", "x", 2, out int replacements);

        result.Should().Be("xxxyz");
        replacements.Should().Be(2);
    }
}
