using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Pins the one place a fuzzy constraint's numbers cannot hold what upstream's Python <c>int</c>
/// holds: a cost limit or a cost coefficient above <c>UNLIMITED</c> (4294967295).
/// </summary>
/// <remarks>
/// <para>
/// A repeat count is capped by <c>is_above_limit</c> (<c>upstream/regex/_regex_core.py:623</c>),
/// but a fuzzy cost is not: <c>parse_cost_limit</c> (<c>:750-760</c>) and <c>parse_cost_term</c>
/// (<c>:808-818</c>) both call <c>int(digits)</c> and range-check nothing, so upstream puts the
/// unbounded value straight into the code list. This port's code words are <see cref="uint"/>
/// (S06), so <c>Fuzzy.CodeWord</c> clamps each one at <c>UNLIMITED</c> as it is emitted.
/// </para>
/// <para>
/// The saturation is not observable in matching. Measured against <c>regex</c> 2026.7.19 on
/// 2026-08-31 (<c>.scratch/s13_bigcost.py</c>), the huge limit and <c>UNLIMITED</c> behave
/// identically, and the huge coefficient and <c>UNLIMITED</c> do too:
/// </para>
/// <code>
/// (?:abc){i&lt;=99999999999}   search('abc') -> 'abc'   search('axbxc') -> 'axbxc' (0, 2, 0)
/// (?:abc){i&lt;=4294967295}   search('abc') -> 'abc'   search('axbxc') -> 'axbxc' (0, 2, 0)
/// (?:abc){99999999999i&lt;=1} search('abc') -> 'abc'   search('axbxc') -> None
/// (?:abc){4294967295i&lt;=1} search('abc') -> 'abc'   search('axbxc') -> None
/// </code>
/// <para>
/// It could not be, either: telling 4294967295 insertions from 4294967296 needs a subject that
/// long, and upstream's own C engine stores each code word in an <c>RE_CODE</c>, which is an
/// <c>RE_UINT32</c> (<c>upstream/src/_regex.c:58</c>), so the value it actually matches with can
/// never exceed <c>UNLIMITED</c> whatever the Python list holds. What diverges is the intermediate
/// list the compile-parity corpus records, which is why this is a gap test and not a corpus row -
/// upstream's own suite writes no cost this large, so the corpus has no such row to hold it.
/// </para>
/// <para>
/// The clamp is at the <b>code word</b> and nowhere earlier, which is what makes that last sentence
/// true, and getting there took two attempts - both caught by an S13 blind review pass, and both
/// pinned by the tests below. Clamping in <c>ParseCostLimit</c> at <c>UNLIMITED</c> made
/// <c>{i&lt;4294967296}</c> subtract from the ceiling and cap at 4294967294, and made
/// <c>{4294967296&lt;=i&lt;=4294967295}</c> compile. Clamping there at <see cref="long.MaxValue"/>
/// only moved the same fault upwards: with the ceiling equal to the arithmetic type's maximum,
/// <c>{9223372036854775807&lt;i&lt;=…}</c> overflowed to a negative minimum, and any two distinct
/// values above the ceiling compared equal. The parse-time values are therefore
/// <see cref="System.Numerics.BigInteger"/>, as unbounded as upstream's <c>int</c>.
/// </para>
/// <para>
/// The expected code words below are upstream's own, captured by
/// <c>.scratch/s13_record.py</c> on 2026-08-31, except for the one saturated word in each of the
/// first two, which is called out in the assertion.
/// </para>
/// </remarks>
public sealed class FuzzyCostLimitOverflowTests
{
    /// <summary>
    /// A maximum cost above <c>UNLIMITED</c>. Upstream's word 5 is 99999999999; ours saturates.
    /// </summary>
    [Test]
    public void A_cost_limit_above_unlimited_saturates_at_unlimited()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?:abc){i<=99999999999}");

        // Upstream: [27, 0, 0, 0, 0, 99999999999, 0, 0, 0, 4294967295, 1, 1, 1, 4294967295, ...]
        //                             ^^^^^^^^^^^ the only word that differs
        compiled
            .Code.Should()
            .Equal(27u, 0, 0, 0, 0, 4294967295, 0, 0, 0, 4294967295, 1, 1, 1, 4294967295, 74, 4, 3, 97, 98, 99, 20, 1);
    }

    /// <summary>
    /// A cost-equation coefficient above <c>UNLIMITED</c>. Upstream's word 11 is 99999999999.
    /// </summary>
    [Test]
    public void A_cost_coefficient_above_unlimited_saturates_at_unlimited()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?:abc){99999999999i<=1}");

        // Upstream: [27, 0, 0, 0, 0, 4294967295, 0, 0, 0, 4294967295, 0, 99999999999, 0, 1, ...]
        //                                                                ^^^^^^^^^^^
        compiled
            .Code.Should()
            .Equal(27u, 0, 0, 0, 0, 4294967295, 0, 0, 0, 4294967295, 0, 4294967295, 0, 1, 74, 4, 3, 97, 98, 99, 20, 1);
    }

    /// <summary>
    /// The control: the largest values that fit, which upstream and this port agree on word for
    /// word. Without these two rows the tests above would still pass if every cost were forced to
    /// <c>UNLIMITED</c>.
    /// </summary>
    [Test]
    [Arguments(
        "(?:abc){i<=4294967295}",
        new uint[] { 27, 0, 0, 0, 0, 4294967295, 0, 0, 0, 4294967295, 1, 1, 1, 4294967295, 74, 4, 3, 97, 98, 99, 20, 1 }
    )]
    [Arguments(
        "(?:abc){4294967295i<=1}",
        new uint[] { 27, 0, 0, 0, 0, 4294967295, 0, 0, 0, 4294967295, 0, 4294967295, 0, 1, 74, 4, 3, 97, 98, 99, 20, 1 }
    )]
    [Arguments(
        "(?:abc){i<=7}",
        new uint[] { 27, 0, 0, 0, 0, 7, 0, 0, 0, 4294967295, 1, 1, 1, 4294967295, 74, 4, 3, 97, 98, 99, 20, 1 }
    )]
    public void A_cost_that_fits_compiles_to_upstreams_exact_bytecode(string pattern, uint[] code) =>
        PatternCompiler.Compile(pattern).Code.Should().Equal(code);

    /// <summary>
    /// An <i>exclusive</i> limit one above <c>UNLIMITED</c>: upstream subtracts one from the true
    /// value and lands exactly on <c>UNLIMITED</c>, so there is nothing to clamp and the bytecode
    /// agrees word for word. Clamping before the subtraction would give 4294967294.
    /// </summary>
    /// <remarks>
    /// Measured against regex 2026.7.19 on 2026-08-31: <c>(?:abc){i&lt;4294967296}</c> compiles to
    /// <c>[27, 0, 0, 0, 0, 4294967295, 0, 0, 0, 4294967295, 1, 1, 1, 4294967295, 74, 4, 3, 97, 98,
    /// 99, 20, 1]</c>.
    /// </remarks>
    [Test]
    public void An_exclusive_limit_one_above_unlimited_subtracts_before_it_clamps() =>
        PatternCompiler
            .Compile("(?:abc){i<4294967296}")
            .Code.Should()
            .Equal(27u, 0, 0, 0, 0, 4294967295, 0, 0, 0, 4294967295, 1, 1, 1, 4294967295, 74, 4, 3, 97, 98, 99, 20, 1);

    /// <summary>
    /// A minimum above the maximum, with both above <c>UNLIMITED</c>: upstream's
    /// <c>not 0 &lt;= min_cost &lt;= max_cost</c> rejects it, and so must this port. Clamping both
    /// to <c>UNLIMITED</c> first would make them equal and let the pattern through.
    /// </summary>
    /// <remarks>
    /// Measured against regex 2026.7.19 on 2026-08-31:
    /// <c>(?:abc){4294967296&lt;=i&lt;=4294967295}</c> raises <c>error</c> with
    /// <c>msg='bad fuzzy cost limit'</c> and <c>pos=23</c>.
    /// </remarks>
    [Test]
    public void A_minimum_above_the_maximum_is_rejected_even_when_both_are_above_unlimited()
    {
        Action compile = () => PatternCompiler.Compile("(?:abc){4294967296<=i<=4294967295}");

        var error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error.Message.Should().Be("bad fuzzy cost limit");
        error.Offset.Should().Be(23);
    }

    /// <summary>
    /// The control for the test above: the same shape with the minimum equal to the maximum, which
    /// upstream accepts. Both words are above <c>UNLIMITED</c>, so both are clamped, and this is
    /// the one row where the clamp is visible in a limit pair.
    /// </summary>
    /// <remarks>
    /// Upstream's own words are <c>[27, 0, 0, 0, 4294967296, 4294967296, ...]</c>, captured by
    /// <c>.scratch/s13_record.py</c> on 2026-08-31.
    /// </remarks>
    /// <summary>
    /// Costs at and above <see cref="long.MaxValue"/> on <b>both</b> sides of a comparison, which
    /// upstream compiles and which any parse-time ceiling rejects: with the ceiling at
    /// <see cref="long.MaxValue"/> the first row's <c>min_cost += 1</c> overflows to a negative
    /// minimum, and the second and third rows have two distinct true values that a ceiling makes
    /// equal in the wrong direction.
    /// </summary>
    /// <remarks>
    /// All three measured against regex 2026.7.19 on 2026-08-31 (<c>.scratch/s13_bigmin.py</c>
    /// for the accept, <c>.scratch/s13_record.py</c> for the words). Every emitted limit here is
    /// above <c>UNLIMITED</c>, so all three clamp to the same code word - the point is that they
    /// compile at all.
    /// </remarks>
    [Test]
    [Arguments("(?:abc){9223372036854775807<i<=9223372036854775808}")]
    [Arguments("(?:abc){9223372036854775807<=i<9223372036854775808}")]
    [Arguments("(?:abc){9223372036854775808<=i<=9223372036854775808}")]
    public void Costs_at_and_above_a_longs_range_still_compile(string pattern) =>
        PatternCompiler
            .Compile(pattern)
            .Code.Should()
            .Equal(
                27u,
                0,
                0,
                0,
                4294967295,
                4294967295,
                0,
                0,
                0,
                4294967295,
                1,
                1,
                1,
                4294967295,
                74,
                4,
                3,
                97,
                98,
                99,
                20,
                1
            );

    /// <summary>
    /// The control for the row above: the same shape one step smaller, where the minimum really is
    /// above the maximum and upstream rejects it. Without it, the test above would pass if every
    /// cost comparison were removed.
    /// </summary>
    /// <remarks>
    /// Measured on 2026-08-31: <c>regex.compile('(?:abc){2&lt;=i&lt;=1}')</c> raises
    /// <c>error</c> with <c>msg='bad fuzzy cost limit'</c>.
    /// </remarks>
    [Test]
    public void A_minimum_above_the_maximum_is_still_rejected_at_ordinary_sizes()
    {
        Action compile = () => PatternCompiler.Compile("(?:abc){2<=i<=1}");

        compile.Should().Throw<FuzzyRegexParseException>().WithMessage("bad fuzzy cost limit");
    }

    [Test]
    public void A_minimum_equal_to_the_maximum_compiles_with_both_words_clamped() =>
        PatternCompiler
            .Compile("(?:abc){4294967296<=i<=4294967296}")
            .Code.Should()
            .Equal(
                27u,
                0,
                0,
                0,
                4294967295,
                4294967295,
                0,
                0,
                0,
                4294967295,
                1,
                1,
                1,
                4294967295,
                74,
                4,
                3,
                97,
                98,
                99,
                20,
                1
            );
}
