using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Quantifiers;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_repeated_repeats</c>
/// (lines 1408-1415).
/// </summary>
public sealed class RepeatedRepeatsTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_repeated_repeats#1")]
    public void Nested_plus_matches_the_whole_run()
    {
        Match m = Upstream.Match("aaa", "(?:a+)+");

        m.Index.Should().Be(0);
        m.Length.Should().Be(3);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_repeated_repeats#2")]
    public void Nested_repeat_of_a_repeated_group_matches_the_whole_run()
    {
        Match m = Upstream.Match("abcabc", "(?:(?:ab)+c)+");

        m.Index.Should().Be(0);
        m.Length.Should().Be(6);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_repeated_repeats#3")]
    public void Nested_plus_with_a_bounded_outer_repeat_matches_the_whole_run()
    {
        Match m = Upstream.Match("aaa", "(?:a+){2,}");

        m.Index.Should().Be(0);
        m.Length.Should().Be(3);
    }
}
