using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Pins <c>standardise_name</c> and the <c>numeric_to_rational</c> / <c>float_to_rational</c> pair
/// behind it (<c>upstream/regex/_regex_core.py:1688-1725</c>), which turn the text inside
/// <c>\p{...}</c> into the key the property dictionary is looked up with.
/// </summary>
/// <remarks>
/// <para>
/// The numeric path exists for <c>\p{Numeric_Value=1/2}</c>, but it is tried on <b>every</b>
/// property and value name, so its acceptance rule is Python's <c>float()</c> - which is not
/// <see cref="double.TryParse(string, out double)"/>: it takes <c>inf</c>, <c>infinity</c> and
/// <c>nan</c> in any case, and underscores between digits. Which branch a name takes is
/// observable, because the fallback strips <c>_</c>, <c>-</c> and spaces and upper-cases while the
/// numeric path rewrites the value.
/// </para>
/// <para>
/// No corpus row exercises any of this: upstream's suite only writes ordinary names. Every
/// expected value below was measured against the local oracle (<c>regex</c> 2026.7.19) on
/// 2026-08-30 by calling <c>regex._regex_core.standardise_name</c> directly.
/// </para>
/// </remarks>
public sealed class PropertyNameTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _noNamedLists = new(StringComparer.Ordinal);

    [Test]
    [Arguments("Lu", "LU")]
    [Arguments("Alphabetic", "ALPHABETIC")]
    [Arguments("General_Category", "GENERALCATEGORY")]
    [Arguments("1", "1")]
    [Arguments("12", "12")]
    [Arguments("1.5", "3/2")]
    [Arguments("0.5", "1/2")]
    [Arguments(".5", "1/2")]
    [Arguments("5.", "5")]
    [Arguments("1/2", "1/2")]
    [Arguments("3/4", "3/4")]
    [Arguments("22/7", "22/7")]
    [Arguments("-1", "-1")]
    [Arguments("-1/2", "-1/2")]
    [Arguments("1e5", "100000")]
    [Arguments("1E5", "100000")]
    [Arguments("1e-9", "0")]
    // float("1_0") is 10.0 - an underscore between digits is legal in a Python float literal.
    [Arguments("1_0", "10")]
    // float("_1") and float("1_") both raise, so these take the strip-and-upper-case fallback.
    [Arguments("_1", "1")]
    [Arguments("1_", "1")]
    [Arguments("1 0", "10")]
    [Arguments("1-0", "10")]
    [Arguments(" 1 ", "1")]
    // int(nan) raises ValueError, which standardise_name catches; int(inf) raises OverflowError,
    // which it does not - that case is the test below.
    [Arguments("nan", "NAN")]
    [Arguments("NaN", "NAN")]
    // float division by zero raises ZeroDivisionError, also caught.
    [Arguments("1/0", "1/0")]
    [Arguments("0/0", "0/0")]
    // Three parts is upstream's bare `raise ValueError()`.
    [Arguments("1/2/3", "1/2/3")]
    [Arguments("1.2.3", "1.2.3")]
    [Arguments("0x10", "0X10")]
    [Arguments("1j", "1J")]
    public void Standardise_name_matches_upstream(string name, string expected) =>
        ParseFunctions.StandardiseName(name).Should().Be(expected);

    /// <summary>
    /// <c>\p{Infinity}</c> reaches <c>int(float("Infinity"))</c>, whose <c>OverflowError</c>
    /// <c>standardise_name</c> does <b>not</b> catch, so it leaves <c>compile</c> as itself rather
    /// than as a parse error. Measured: <c>regex.compile(r'\p{Infinity}')</c> raises
    /// <c>OverflowError: cannot convert float infinity to integer</c>.
    /// </summary>
    [Test]
    [Arguments(@"\p{Infinity}")]
    [Arguments(@"\p{inf}")]
    [Arguments(@"\p{INF}")]
    public void An_infinite_property_name_overflows_rather_than_failing_to_parse(string pattern)
    {
        Action compile = () => PatternCompiler.Compile(pattern, 0, _noNamedLists, PatternCompiler.DefaultVersion);

        // FuzzyRegexParseException derives from Exception, not from OverflowException, so asserting
        // the thrown type is OverflowException is already the assertion that it is not a parse error.
        compile.Should().Throw<OverflowException>().WithMessage("cannot convert float infinity to integer");
    }

    /// <summary>
    /// A qualifier that standardises to nothing is not a qualifier. <c>standardise_name</c> strips
    /// <c>_</c>, <c>-</c> and spaces, so <c>\p{_:Lu}</c> arrives at <c>lookup_property</c> with an
    /// empty property name, and upstream's <c>if property:</c> - which tests the <b>standardised</b>
    /// value, not the raw one (<c>upstream/regex/_regex_core.py:1743</c>, standardised at
    /// <c>:1734</c>) - is false. The lookup
    /// then falls through to the general-category, script and block tables, and to the POSIX branch,
    /// exactly as if the qualifier had not been written.
    /// </summary>
    /// <remarks>
    /// Measured against the local oracle on 2026-08-30 by intercepting <c>regex._regex.compile</c>.
    /// 37 is <see cref="Opcode.Property"/>; <c>\p{_:gc}</c> resolves as the binary-property fallback
    /// and so comes out negated.
    /// </remarks>
    [Test]
    [Arguments(@"\p{_:Lu}", new uint[] { 37, 1, 1966090, 1 })]
    [Arguments(@"\p{-:Lu}", new uint[] { 37, 1, 1966090, 1 })]
    [Arguments(@"\p{ :Lu}", new uint[] { 37, 1, 1966090, 1 })]
    [Arguments(@"\p{_ -:Lu}", new uint[] { 37, 1, 1966090, 1 })]
    [Arguments(@"\p{_=Lu}", new uint[] { 37, 1, 1966090, 1 })]
    [Arguments(@"\p{_:alpha}", new uint[] { 37, 1, 1, 1 })]
    [Arguments(@"\p{_:Greek}", new uint[] { 37, 1, 5570565, 1 })]
    [Arguments(@"\p{_:gc}", new uint[] { 37, 0, 1966080, 1 })]
    [Arguments(@"\P{_:Lu}", new uint[] { 37, 0, 1966090, 1 })]
    [Arguments(@"\p{^_:Lu}", new uint[] { 37, 0, 1966090, 1 })]
    // The POSIX branch is the one that needs "no property" rather than "no qualifier written".
    [Arguments(@"[[:_:alnum:]]", new uint[] { 37, 1, 4980737, 1 })]
    [Arguments(@"[[:_:alpha:]]", new uint[] { 37, 1, 1, 1 })]
    public void A_qualifier_that_standardises_to_nothing_is_not_a_qualifier(string pattern, uint[] expectedCode)
    {
        CompiledPattern compiled = PatternCompiler.Compile(pattern, 0, _noNamedLists, PatternCompiler.DefaultVersion);

        compiled.Code.Should().Equal(expectedCode);
    }

    /// <summary>
    /// A numeric property value really does resolve through the rational form. Only a value the
    /// strip-and-upper-case fallback would leave <b>different</b> tests that: <c>1/2</c> and
    /// <c>3/4</c> are fixed points of the fallback, so those rows would still compile with the
    /// numeric path deleted. <c>0.5</c>, <c>1.5</c> and <c>1e-1</c> would not - the fallback gives
    /// <c>0.5</c>, <c>1.5</c> and <c>1E1</c>, none of which is a Numeric_Value key.
    /// </summary>
    [Test]
    [Arguments(@"\p{nv=1/2}")]
    [Arguments(@"\p{numeric value=0.5}")]
    [Arguments(@"\p{Numeric_Value=1/2}")]
    [Arguments(@"\p{nv=1.5}")]
    [Arguments(@"\p{nv=1e-1}")]
    public void A_numeric_property_value_resolves_through_its_rational_form(string pattern)
    {
        Action compile = () => PatternCompiler.Compile(pattern, 0, _noNamedLists, PatternCompiler.DefaultVersion);

        compile.Should().NotThrow();
    }
}
