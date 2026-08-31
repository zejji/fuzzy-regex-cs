using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CharacterClasses;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_anyall</c> (lines 539-542).
/// </summary>
public sealed class AnyAllTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_anyall#1")]
    public void Dot_matches_a_newline_under_singleline() =>
        FuzzyRegex.MatchAtStart("a\nb", "a.b", FuzzyRegexOptions.Singleline).Value.Should().Be("a\nb");

    [Test]
    [Property("Upstream", "RegexTests.test_anyall#2")]
    public void Dot_star_matches_across_newlines_under_singleline() =>
        FuzzyRegex.MatchAtStart("a\n\nb", "a.*b", FuzzyRegexOptions.Singleline).Value.Should().Be("a\n\nb");
}
