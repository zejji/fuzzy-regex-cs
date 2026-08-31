using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.UnicodeProperties;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_properties</c> (lines 1100-1103,
/// 1105-1106 and 1122): general categories by one- and two-letter name, and the inline
/// case-insensitive flag beside them.
/// </summary>
public sealed class GeneralCategoryTests
{
    [Test]
    [Arguments(@"\p{L}", "A")]
    [Arguments(@"\p{L}", "a")]
    [Arguments(@"\p{Lu}", "A")]
    [Arguments(@"\p{Ll}", "a")]
    [Property("Upstream", "RegexTests.test_properties#55-58")]
    public void General_category_matches_a_letter_of_that_category(string pattern, string subject) =>
        new FuzzyRegex(pattern).IsMatchAtStart(subject).Should().BeTrue();

    /// <remarks>
    /// Upstream repeats assertion #58 verbatim at line 1122, after the grapheme block. Kept as its
    /// own test so the assertion index in the provenance stays truthful.
    /// </remarks>
    [Test]
    [Property("Upstream", "RegexTests.test_properties#69")]
    public void Lowercase_letter_category_still_matches_after_the_grapheme_block() =>
        new FuzzyRegex(@"\p{Ll}").IsMatchAtStart("a").Should().BeTrue();

    [Test]
    [Arguments("a")]
    [Arguments("A")]
    [Property("Upstream", "RegexTests.test_properties#59-60")]
    public void Inline_ignore_case_matches_either_case_of_a_literal(string subject) =>
        new FuzzyRegex("(?i)a").IsMatchAtStart(subject).Should().BeTrue();
}
