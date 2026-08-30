using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Pins upstream's fallback when <c>\g&lt;</c> starts something that is not a group reference:
/// the whole escape degrades to the literal characters that spelled it, rather than raising.
/// </summary>
/// <remarks>
/// <para>
/// These are gap tests: no upstream test compiles either pattern, so nothing in the ported suite
/// or the compile-parity corpus covers the fallback. Both were raised by the S07 blind review and
/// reproduced against upstream <c>regex</c> 2026.7.19 on 2026-08-30 by intercepting
/// <c>regex._regex.compile</c> and reading the code list it is handed:
/// </para>
/// <code>
/// '\\g&lt;99999999999' -> [74, 16, 13, 103, 60, 57, 57, 57, 57, 57, 57, 57, 57, 57, 57, 57, 1]
/// '\\g&lt;\xe9'        -> [74, 16, 3, 103, 60, 233, 1]
/// </code>
/// </remarks>
public sealed class GroupReferenceFallbackTests
{
    /// <summary>
    /// A group number too large for a 32-bit int. Upstream's <c>int()</c> is arbitrary precision,
    /// so it never overflows - the reference simply fails to resolve and the escape falls back to
    /// literals. A port that lets <c>int.Parse</c> throw loses that fallback.
    /// </summary>
    [Test]
    public void Group_ref_whose_number_overflows_an_int_falls_back_to_literals()
    {
        CompiledPattern compiled = PatternCompiler.Compile(@"\g<99999999999");

        // g < 9 9 9 9 9 9 9 9 9 9 9
        compiled.Code.Should().Equal(74u, 16, 13, 103, 60, 57, 57, 57, 57, 57, 57, 57, 57, 57, 57, 57, 1);
    }

    /// <summary>
    /// The same overflowing number on the <em>delimited</em> path, which does not fall back to
    /// literals: upstream raises "invalid group reference" and our port throws its
    /// <c>needs:backrefs</c> seam, because S11 owns <c>RefGroup</c>. What S07 has to guarantee is
    /// only that the number does not blow up on the way there - an <see cref="OverflowException"/>
    /// is neither upstream's error nor the seam, and it escapes <c>parse_escape</c>'s catch.
    /// </summary>
    /// <remarks>
    /// Found by the S07 review of the <c>ParseName</c> fix: <c>Info.IsOpenGroup</c> parsed the
    /// same name with <c>int.Parse</c> one call later, so the delimited form was still broken.
    /// Upstream, 2026-08-30: <c>'\\g&lt;99999999999&gt;'</c> and <c>'\\g&lt;2147483648&gt;'</c>
    /// both raise <c>error: invalid group reference at position 3</c>.
    /// </remarks>
    [Test]
    public void A_group_number_too_large_for_an_int_does_not_overflow_on_the_delimited_path()
    {
        Action compile = () => PatternCompiler.Compile(@"\g<99999999999>");

        compile.Should().NotThrow<OverflowException>();
    }

    /// <summary>
    /// A non-ASCII, undelimited name. The name never terminates with <c>&gt;</c>, so this is not a
    /// reference, and the escape degrades to literals.
    /// </summary>
    /// <remarks>
    /// Reaching the fallback means getting past <c>IsDigitName</c>, which threw a
    /// <c>needs:unicode-tables</c> seam on any non-ASCII name until S09 gave it Python's
    /// Unicode-aware <c>str.isdigit</c>.
    /// </remarks>
    [Test]
    public void Group_ref_with_a_non_ascii_undelimited_name_falls_back_to_literals()
    {
        CompiledPattern compiled = PatternCompiler.Compile("\\g<\u00e9");

        // g < e-acute
        compiled.Code.Should().Equal(74u, 16, 3, 103, 60, 233, 1);
    }
}
