using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Three patterns on which <b>upstream itself</b> raises a Python internal error rather than
/// <c>regex.error</c>. The port has to fail on them too, and these tests say so.
/// </summary>
/// <remarks>
/// <para>
/// Found by the S10 differential wave (24,884 patterns compiled on both sides), not by reading the
/// source. Each is a place where upstream's optimiser reduces a set to an <c>AnyAll</c> and then
/// calls a method <c>AnyAll</c> does not have, or where two version flags collide. All three are
/// upstream bugs, so there is no "right" answer to port - what matters is that the pattern is
/// rejected rather than quietly compiled, and it very nearly was not: an earlier draft of this
/// slice gave every node an ignored <c>inSet</c> parameter, which compiled
/// <c>(?V1)[a--[\s\S]]</c> that upstream rejects.
/// </para>
/// <para>
/// Measured against the local oracle (<c>regex</c> 2026.7.19) on 2026-08-30:
/// </para>
/// <code>
/// '[^\s\S]'          flags=I    AttributeError: 'AnyAll' object has no attribute 'rebuild'
/// '(?V1)[a--[\s\S]]' flags=I    TypeError: RegexBase.optimise() got an unexpected keyword argument 'in_set'
/// '(?V1)a'           flags=V0   KeyError: regex.V0|V1
/// </code>
/// <para>
/// The exception <i>types</i> and message texts are Python's and are not ported; PORTMAP's "Where
/// we diverge" records that. What is pinned here is that each pattern is rejected, and with
/// something that is not a <see cref="FuzzyRegexParseException"/> - because upstream does not
/// treat these as parse errors either, and a corpus row would assert the message if it ever did.
/// </para>
/// </remarks>
public sealed class UpstreamInternalErrorTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _noNamedLists = new(StringComparer.Ordinal);

    /// <summary>
    /// <c>\s</c> and <c>\S</c> are complementary properties, so <c>SetUnion.optimise</c> returns
    /// <c>AnyAll()</c> (<c>:3936-3937</c>); <c>parse_set</c> then calls <c>with_flags</c> on it for
    /// the leading <c>^</c>, and <c>AnyAll</c> has no <c>rebuild</c>.
    /// </summary>
    [Test]
    public void A_negated_set_of_complementary_properties_is_rejected()
    {
        Action compile = () =>
            PatternCompiler.Compile(@"[^\s\S]", RegexFlags.IgnoreCase, _noNamedLists, PatternCompiler.DefaultVersion);

        compile.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// The same <c>AnyAll</c> as the first operand of a set difference, which
    /// <c>SetDiff.optimise</c> hands to <c>optimise(info, reverse, in_set)</c> - a signature
    /// <c>AnyAll</c> does not have.
    /// </summary>
    [Test]
    [Arguments(@"(?V1)[a--[\s\S]]")]
    [Arguments(@"(?V1)[[\s\S]--a]")]
    public void A_set_operation_on_a_reduced_any_all_is_rejected(string pattern)
    {
        Action compile = () =>
            PatternCompiler.Compile(pattern, RegexFlags.IgnoreCase, _noNamedLists, PatternCompiler.DefaultVersion);

        compile.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// An inline <c>(?V1)</c> under the <c>V0</c> flag leaves both version bits set, and
    /// <c>Info.__init__</c> looks the pair up in <c>DEFAULT_FLAGS</c>. S08 saw this coming and left
    /// it to "whichever slice surfaces the version flags"; the differential wave confirms the
    /// route.
    /// </summary>
    [Test]
    public void An_inline_version_flag_conflicting_with_the_argument_flag_is_rejected()
    {
        Action compile = () =>
            PatternCompiler.Compile("(?V1)a", RegexFlags.Version0, _noNamedLists, PatternCompiler.DefaultVersion);

        compile.Should().Throw<ArgumentOutOfRangeException>();
    }
}
