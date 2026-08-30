using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Pins Python's <c>int()</c> where upstream's parser calls it on a group name: it accepts any
/// Unicode <b>decimal</b> digit, so <c>\g&lt;١&gt;</c> is a reference to group 1, and it rejects a
/// <c>str.isdigit</c> digit that has no decimal value, so <c>(?P=²)</c> blows up.
/// </summary>
/// <remarks>
/// <para>
/// Gap tests: upstream's suite never writes a non-ASCII group name, so neither the ported suite nor
/// the compile-parity corpus reaches any of this. Raised by the S11 blind review, which found the
/// port throwing <see cref="FormatException"/> on all three - <c>BigInteger.Parse</c> takes only
/// ASCII digits under the invariant culture, while Python's <c>int()</c> reads
/// <c>Numeric_Type=Decimal</c>.
/// </para>
/// <para>
/// Measured against the local oracle (<c>regex</c> 2026.7.19, CPython 3.14.6) on 2026-08-30 by
/// intercepting <c>regex._regex.compile</c>:
/// </para>
/// <code>
/// '(x)(?P=١)'    -> [30, 1, 1, 1, 12, 1, 120, 20, 46, 0, 1, 1]
/// '(x)\g&lt;١&gt;'    -> [30, 1, 1, 1, 12, 1, 120, 20, 46, 0, 1, 1]
/// '(x)(?(١)a|b)' -> [30, 1, 1, 1, 12, 1, 120, 20, 32, 1, 12, 1, 97, 36, 12, 1, 98, 20, 1]
/// '(x)(?P=²)'    -> ValueError: invalid literal for int() with base 10: '²'
/// </code>
/// </remarks>
public sealed class UnicodeDigitGroupNameTests
{
    /// <summary>ARABIC-INDIC DIGIT ONE names group 1 in all three places a group name is read.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="expected">Upstream's bytecode.</param>
    [Test]
    [Arguments("(x)(?P=١)", new uint[] { 30, 1, 1, 1, 12, 1, 120, 20, 46, 0, 1, 1 })]
    [Arguments("(x)\\g<١>", new uint[] { 30, 1, 1, 1, 12, 1, 120, 20, 46, 0, 1, 1 })]
    [Arguments("(x)(?(١)a|b)", new uint[] { 30, 1, 1, 1, 12, 1, 120, 20, 32, 1, 12, 1, 97, 36, 12, 1, 98, 20, 1 })]
    public void A_group_name_of_non_ascii_decimal_digits_is_a_group_number(string pattern, uint[] expected)
    {
        CompiledPattern compiled = PatternCompiler.Compile(pattern);

        compiled.Code.Should().Equal(expected);
    }

    /// <summary>
    /// SUPERSCRIPT TWO satisfies <c>str.isdigit</c> but has no <i>decimal</i> value, so upstream
    /// reaches <c>int("²")</c> and lets its <c>ValueError</c> escape <c>regex.compile</c>. There is
    /// no specified behaviour to port - what matters is that the pattern is rejected, and with
    /// something that is not a <see cref="FuzzyRegexParseException"/>, since upstream does not
    /// treat it as a parse error either. Same rule as <see cref="UpstreamInternalErrorTests"/>.
    /// </summary>
    [Test]
    [Arguments("(x)(?P=²)")]
    [Arguments("(x)(?(²)a|b)")]
    public void A_group_name_of_digits_with_no_decimal_value_is_rejected(string pattern)
    {
        Action compile = () => PatternCompiler.Compile(pattern);

        compile.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// The same character where a group number is <em>not</em> allowed stays an ordinary
    /// "bad character in group name": upstream's <c>not allow_numeric or int(name) &lt; min_group</c>
    /// short-circuits before the conversion, so the <c>ValueError</c> never happens.
    /// </summary>
    [Test]
    public void A_numeric_name_where_numbers_are_not_allowed_never_reaches_the_conversion()
    {
        Action compile = () => PatternCompiler.Compile("(?<²>x)");

        compile.Should().Throw<FuzzyRegexParseException>().WithMessage("bad character in group name");
    }
}
