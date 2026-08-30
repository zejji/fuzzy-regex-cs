using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Pins the one place the port's <c>long</c> widths cannot hold what upstream's Python
/// <c>int</c> holds: the product of two maximal repeat counts.
/// </summary>
/// <remarks>
/// <para>
/// A repeat count is capped at <c>UNLIMITED - 1</c> = 4294967294 (<c>is_above_limit</c>,
/// <c>upstream/regex/_regex_core.py:623</c>), so one repeat's width fits comfortably. Nest two and
/// the product is 4294967294 squared = 1.8e19, which does not fit in <see cref="long"/>;
/// <c>Widths.Multiply</c> saturates at <see cref="long.MaxValue"/> instead. Every consumer either
/// takes <c>min(w, UNLIMITED)</c> (<c>:3021</c>, <c>:3028</c>) or asks <c>w &gt;= UNLIMITED</c>
/// (<c>:4466</c>), and both a saturated width and the true one are far above UNLIMITED, so the
/// two are indistinguishable. This test is what says so out loud.
/// </para>
/// <para>
/// No corpus row can cover it. Upstream's own suite never writes a count this large, and it could
/// not: <c>regex.compile('a{4294967294}')</c> spends about twenty minutes in the C compiler and
/// then dies. Measured on 2026-08-30 against <c>regex</c> 2026.7.19:
/// </para>
/// <code>
/// $ python .scratch/oracle2.py "a{4294967294}"
/// pattern: 'a{4294967294}'
/// ERR MemoryError  pos= None
/// </code>
/// <para>
/// Everything before that C call is O(1) - parse, optimise, compile and flatten all measured
/// 0.000s at a count of 8,000,000 - so the expected values below come from intercepting
/// <c>regex._regex.compile</c> and reading the arguments it is handed without calling through
/// (<c>.scratch/oracle5.py</c>, the same technique the corpus recorder uses):
/// </para>
/// <code>
/// 'a{4294967294}'
///    code       = [12, 3, 97, 29, 4294967294, 4294967294, 12, 1, 97, 20, 1]
///    req_offset = 0  req_chars = [97]  req_flags = 0  groups = 0
/// '(?:a{4294967294}){0,4294967294}a'
///    code       = [12, 3, 97, 29, 0, 4294967294, 29, 4294967294, 4294967294,
///                  12, 1, 97, 20, 20, 12, 1, 97, 1]
///    req_offset = -1  req_chars = [97]  req_flags = 0  groups = 0
/// '(?:a{65535}){0,65535}a'
///    code       = [12, 3, 97, 29, 0, 65535, 29, 65535, 65535, 12, 1, 97, 20, 20, 12, 1, 97, 1]
///    req_offset = 4294836225  req_chars = [97]  req_flags = 0  groups = 0
/// </code>
/// <para>
/// The trailing character is an <c>a</c> rather than a <c>b</c> deliberately: a different one
/// makes the first set two members wide, which needs <c>SetUnion</c> and so waits for S10.
/// </para>
/// </remarks>
public sealed class RepeatWidthOverflowTests
{
    /// <summary>
    /// The saturating case. The required string is the trailing character, and its offset is the
    /// outer repeat's maximum width, which overflows; upstream reports -1 for "the offset is not
    /// usable", reached through <c>req_offset &gt;= UNLIMITED</c>.
    /// </summary>
    [Test]
    public void A_width_that_overflows_a_long_still_reports_upstreams_unusable_offset()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?:a{4294967294}){0,4294967294}a");

        compiled
            .Code.Should()
            .Equal(12u, 3, 97, 29, 0, 4294967294, 29, 4294967294, 4294967294, 12, 1, 97, 20, 20, 12, 1, 97, 1);
        compiled.ReqOffset.Should().Be(-1);
        compiled.ReqChars.Should().Equal(97);
    }

    /// <summary>
    /// The control: the same shape with counts small enough that the product fits, so the offset
    /// is the real width and no saturation is involved. Without this row the test above would
    /// still pass if <c>Widths.Multiply</c> returned a constant.
    /// </summary>
    [Test]
    public void A_width_that_fits_reports_the_real_offset()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?:a{65535}){0,65535}a");

        compiled.Code.Should().Equal(12u, 3, 97, 29, 0, 65535, 29, 65535, 65535, 12, 1, 97, 20, 20, 12, 1, 97, 1);
        compiled.ReqOffset.Should().Be(4294836225);
        compiled.ReqChars.Should().Equal(97);
    }

    /// <summary>
    /// A single maximal count, which does not overflow anything, but is the pattern the C compiler
    /// cannot digest - so it is only ever checked here, against captured arguments.
    /// </summary>
    [Test]
    public void A_single_maximal_count_compiles_to_upstreams_bytecode()
    {
        CompiledPattern compiled = PatternCompiler.Compile("a{4294967294}");

        compiled.Code.Should().Equal(12u, 3, 97, 29, 4294967294, 4294967294, 12, 1, 97, 20, 1);
        compiled.ReqOffset.Should().Be(0);
        compiled.ReqChars.Should().Equal(97);
    }
}
