using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CharacterClasses;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_category</c> (lines 672-673) and
/// <c>test_not_literal</c> (lines 675-677).
/// </summary>
public sealed class CategoryAndNegatedSetTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_category#1")]
    public void Whitespace_class_captures_a_space() =>
        FuzzyRegex.MatchAtStart(" ", "(\\s)").Groups[1].Value.Should().Be(" ");

    [Test]
    [Property("Upstream", "RegexTests.test_not_literal#1")]
    public void Negated_set_captures_the_character_after_whitespace() =>
        FuzzyRegex.Match(" b", "\\s([^a])").Groups[1].Value.Should().Be("b");

    [Test]
    [Property("Upstream", "RegexTests.test_not_literal#2")]
    [Skip("needs:quantifiers - '([^a]*)' repeats a negated set")]
    public void Negated_set_star_captures_the_run_after_whitespace() =>
        FuzzyRegex.Match(" bb", "\\s([^a]*)").Groups[1].Value.Should().Be("bb");
}
