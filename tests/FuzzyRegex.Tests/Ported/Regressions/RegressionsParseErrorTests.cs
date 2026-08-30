using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about parse-error messages and positions.
/// </summary>
public sealed class RegressionsParseErrorTests
{
    // Hg issue 58: bad named character escape sequences like "\N{1}" treats as "N".
    [Test]
    [Skip("needs:parse-errors - \\N{name} character-name escapes are not implemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#42")]
    public void Undefined_character_name_reports_its_position()
    {
        Action act = () => _ = new FuzzyRegex(@"\N{1}");

        act.Should().Throw<FuzzyRegexParseException>().WithMessage("undefined character name at position 5");
    }

    // Hg issue 80: Escape characters throws an exception.
    [Test]
    [Skip("needs:parse-errors - a trailing backslash in a replacement string does not raise")]
    [Property("Upstream", "RegexTests.test_hg_bugs#55")]
    public void Trailing_backslash_in_a_replacement_string_reports_its_position()
    {
        Action act = () => FuzzyRegex.Replace("x", "x", "\\");

        act.Should().Throw<FuzzyRegexParseException>().WithMessage("bad escape (end of pattern) at position 1");
    }

    // Hg issue 95: 'pos' for regex.error.
    [Test]
    [Skip("needs:parse-errors - multiple-repeat detection does not report a position")]
    [Property("Upstream", "RegexTests.test_hg_bugs#64")]
    public void Multiple_repeat_operators_report_the_position_of_the_second_one()
    {
        Action act = () => _ = new FuzzyRegex(@".???");

        act.Should().Throw<FuzzyRegexParseException>().WithMessage("multiple repeat at position 3");
    }

    // Hg issue 132: index out of range on null property \p{}.
    [Test]
    [Skip("needs:parse-errors - an empty \\p{} property name does not report a position")]
    [Property("Upstream", "RegexTests.test_hg_bugs#111")]
    public void Empty_property_name_reports_its_position()
    {
        Action act = () => _ = new FuzzyRegex(@"\p{}");

        act.Should().Throw<FuzzyRegexParseException>().WithMessage("unknown property at position 4");
    }
}
