using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about escape sequences.
/// </summary>
public sealed class RegressionsEscapeTests
{
    // Hg issue 35: regex.compile("\ ", regex.X) causes "_regex_core.error: bad escape".
    [Test]
    [Skip("needs:escapes - an escaped space under IgnorePatternWhitespace raises bad escape")]
    [Property("Upstream", "RegexTests.test_hg_bugs#13")]
    public void Escaped_space_under_verbose_mode_matches_a_literal_space()
    {
        Match m = FuzzyRegex.MatchAtStart(" ", @"\ ", FuzzyRegexOptions.IgnorePatternWhitespace);

        m.Success.Should().BeTrue();
    }

    // Git issue 527: `VERBOSE`/`X` flag breaks `\N` escapes.
    [Test]
    [Skip("needs:escapes - \\N{name} character-name escapes are not implemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#447")]
    public void Named_character_escape_matches_the_letter_it_names()
    {
        Match m = new FuzzyRegex(@"\N{LATIN SMALL LETTER A}").MatchAtStart("a");

        m.Index.Should().Be(0);
        m.Length.Should().Be(1);
    }

    [Test]
    [Skip("needs:escapes - \\N{name} character-name escapes are not implemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#448")]
    public void Named_character_escape_matches_the_letter_it_names_under_verbose_mode()
    {
        Match m = new FuzzyRegex(@"\N{LATIN SMALL LETTER A}", FuzzyRegexOptions.IgnorePatternWhitespace).MatchAtStart(
            "a"
        );

        m.Index.Should().Be(0);
        m.Length.Should().Be(1);
    }
}
