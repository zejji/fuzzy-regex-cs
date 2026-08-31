using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about case-insensitive matching.
/// </summary>
public sealed class RegressionsIgnoreCaseTests
{
    // Hg issue 32: regex.search("a(bc)d", "abcd", regex.I|regex.V1) returns None.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#9")]
    public void Case_insensitive_search_with_V1_semantics_matches_a_literal_around_a_group() =>
        FuzzyRegex
            .Match("abcd", "a(bc)d", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version1)
            .Value.Should()
            .Be("abcd");

    // Hg issue 33: regex.search("([\da-f:]+)$", "E", regex.I|regex.V1) returns None.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#10")]
    public void Case_insensitive_character_class_with_V1_semantics_matches_an_uppercase_hex_digit() =>
        FuzzyRegex
            .Match("E", @"([\da-f:]+)$", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version1)
            .Value.Should()
            .Be("E");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#11")]
    public void Case_insensitive_character_class_with_V1_semantics_matches_a_lowercase_hex_digit() =>
        FuzzyRegex
            .Match("e", @"([\da-f:]+)$", FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Version1)
            .Value.Should()
            .Be("e");

    // Hg issue 61: regex.search("[^a]", "A", regex.I).group(0) returns '' incorrectly.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#45")]
    public void Case_insensitive_negated_class_does_not_match_the_negated_letters_case_variant() =>
        FuzzyRegex.Match("A", "(?i)[^a]").Success.Should().BeFalse();
}
