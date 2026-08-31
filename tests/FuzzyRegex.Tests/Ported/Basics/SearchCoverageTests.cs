using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Basics;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_search_coverage</c>
/// (lines 679-681).
/// </summary>
public sealed class SearchCoverageTests
{
    [Test]
    // Retagged in S16: the engine matches literals now, but this pattern is a character class
    // followed by a capture group, so it is blocked on S17 and then S18, not on the spine.
    [Property("Upstream", "RegexTests.test_search_coverage#1")]
    public void Search_captures_the_character_after_whitespace() =>
        FuzzyRegex.Match(" b", "\\s(b)").Groups[1].Value.Should().Be("b");

    [Test]
    // Retagged in S16: 'a' matches, '\s' is a set and waits on S17.
    [Property("Upstream", "RegexTests.test_search_coverage#2")]
    public void Search_matches_a_literal_followed_by_whitespace() =>
        FuzzyRegex.Match("a ", "a\\s").Value.Should().Be("a ");
}
