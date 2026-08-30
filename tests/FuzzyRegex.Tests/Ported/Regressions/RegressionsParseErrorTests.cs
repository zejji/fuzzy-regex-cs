using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about parse-error messages and positions.
/// </summary>
/// <remarks>
/// The three still-skipped tests here were transcribed in Phase 1 as <c>"&lt;msg&gt; at position
/// N"</c>, which is Python's <c>str(error)</c> and not what this port raises: S01 split the two
/// apart, so <see cref="Exception.Message"/> carries upstream's <c>error.msg</c> and
/// <see cref="FuzzyRegexParseException.Offset"/> carries its <c>pos</c>. The slice that turns
/// each of them on has to split its assertion the same way the one below is split.
/// </remarks>
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
    // Tagged needs:parse-errors, but the blocker its reason named - multiple-repeat detection -
    // is apply_quantifier, which S08 ports. Turned on there rather than waiting for a slice that
    // says "parse-errors" on the tin.
    //
    // Upstream asserts with assertRaisesRegex against MULTIPLE_REPEAT, which is the bare string
    // "multiple repeat" (test_regex.py line 41), so its own check is a substring of str(error).
    // Measured against regex 2026.7.19 on 2026-08-30:
    //     $ python .scratch/oracle2.py ".???"
    //     pattern: '.???'
    //     ERR error multiple repeat at position 3 pos= 3
    // The message and the offset are asserted separately because this port keeps them apart, and
    // the offset is the whole point of hg issue 95.
    [Property("Upstream", "RegexTests.test_hg_bugs#64")]
    public void Multiple_repeat_operators_report_the_position_of_the_second_one()
    {
        Action act = () => _ = new FuzzyRegex(@".???");

        var error = act.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be("multiple repeat");
        error.Offset.Should().Be(3);
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
