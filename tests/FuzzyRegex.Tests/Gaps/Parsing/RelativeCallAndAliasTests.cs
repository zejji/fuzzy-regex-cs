using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Parsing;

/// <summary>
/// Pins the group bookkeeping the compile-parity corpus cannot reach: relative group calls,
/// branch reset that reuses a group name, and the private alias a named group nested inside itself
/// is given.
/// </summary>
/// <remarks>
/// <para>
/// These are gap tests. Upstream's own suite never writes <c>(?+1)</c> or <c>(?-1)</c>, so the
/// corpus has <b>zero</b> rows for a relative call and only seven for branch reset - and the slice
/// file names this bookkeeping as the most error-prone in the parser. Without these tests, a
/// wrong relative offset or an alias that never gets fixed up would be invisible to the ratchet.
/// </para>
/// <para>
/// Every expectation below was recorded from upstream <c>regex</c> 2026.7.19 on 2026-08-30 by
/// intercepting <c>regex._regex.compile</c> and reading the arguments it was handed
/// (<c>.scratch/s11_record.py</c>, the same machinery as
/// <c>tools/record-compile-corpus.py</c>), not derived by reading the Python.
/// </para>
/// </remarks>
public sealed class RelativeCallAndAliasTests
{
    /// <summary><c>(?-1)</c> calls the group that closed most recently.</summary>
    [Test]
    public void A_backward_relative_call_resolves_to_the_previous_group()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(a)(?-1)");

        compiled.Code.Should().Equal(11u, 0, 30, 1, 1, 1, 12, 1, 97, 20, 20, 31, 0, 1);
        compiled.GroupCount.Should().Be(1);
    }

    /// <summary><c>(?-2)</c> counts back two, not one.</summary>
    [Test]
    public void A_backward_relative_call_counts_back_by_its_offset()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(a)(b)(?-2)");

        compiled.Code.Should().Equal(11u, 0, 30, 1, 1, 1, 12, 1, 97, 20, 20, 30, 1, 2, 2, 12, 1, 98, 20, 31, 0, 1);
        compiled.GroupCount.Should().Be(2);
    }

    /// <summary><c>(?+1)</c> calls a group that has not been parsed yet.</summary>
    /// <remarks>
    /// The forward call makes the required string unreachable from the start, so
    /// <c>req_offset</c> is -1 rather than 0 - which is what a wrong <c>max_width</c> on
    /// <c>CallGroup</c> would change.
    /// </remarks>
    [Test]
    public void A_forward_relative_call_resolves_to_a_group_defined_later()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?+1)(a)");

        compiled.Code.Should().Equal(31u, 0, 11, 0, 30, 1, 1, 1, 12, 1, 97, 20, 20, 1);
        compiled.ReqOffset.Should().Be(-1);
        compiled.ReqChars.Should().Equal(97);
    }

    /// <summary>
    /// <c>(?-0)</c> is <c>group_count - 0 + 1</c>, which is the <i>next</i> group, not the previous
    /// one: the two signs are not symmetric, and only the minus arm carries the <c>+ 1</c>.
    /// </summary>
    [Test]
    public void A_backward_relative_call_of_zero_names_the_next_group()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?-0)(a)");

        compiled.Code.Should().Equal(31u, 0, 11, 0, 30, 1, 1, 1, 12, 1, 97, 20, 20, 1);
    }

    /// <summary>A relative offset that lands at or below group 0 is rejected while parsing.</summary>
    [Test]
    [Arguments("(?-1)")]
    [Arguments("(?-2)(a)")]
    [Arguments("(?+0)")]
    public void A_relative_call_off_the_start_of_the_pattern_is_rejected(string pattern)
    {
        Action compile = () => PatternCompiler.Compile(pattern);

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .Which.Should()
            .Match<FuzzyRegexParseException>(e => e.Message == "invalid relative group number" && e.Offset == 4);
    }

    /// <summary>
    /// A relative offset past the last group is rejected later, by <c>fix_groups</c>, and so
    /// carries the different message and the position of the call rather than of the digits.
    /// </summary>
    [Test]
    public void A_relative_call_past_the_last_group_is_rejected_as_an_unknown_group()
    {
        Action compile = static () => PatternCompiler.Compile("(a)(?+1)");

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .Which.Should()
            .Match<FuzzyRegexParseException>(static e => e.Message == "unknown group" && e.Offset == 5);
    }

    /// <summary>
    /// <c>(?+)</c> is not a relative call at all: <c>parse_paren</c> only takes that branch when a
    /// digit follows, so this falls through to the flags parser and fails there.
    /// </summary>
    [Test]
    public void A_relative_call_with_no_digits_falls_through_to_the_flags_parser()
    {
        Action compile = static () => PatternCompiler.Compile("(?+)");

        compile
            .Should()
            .Throw<FuzzyRegexParseException>()
            .Which.Should()
            .Match<FuzzyRegexParseException>(static e => e.Message == "unknown extension" && e.Offset == 2);
    }

    /// <summary>
    /// Both branches of a branch reset may define the same name, and they share one group number.
    /// </summary>
    [Test]
    public void A_branch_reset_reusing_a_group_name_keeps_one_group()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?|(?<x>a)|(?<x>b))");

        compiled
            .Code.Should()
            .Equal(65u, 3, 74, 1, 2, 97, 98, 20, 10, 30, 1, 1, 1, 12, 1, 97, 20, 36, 30, 1, 1, 1, 12, 1, 98, 20, 20, 1);
        compiled.GroupCount.Should().Be(1);
        // Rendered rather than compared with BeEquivalentTo, which cannot run under Native AOT
        // (see Equivalence).
        Equivalence.Lines(compiled.GroupIndex).Should().Equal("x=1");
    }

    /// <summary>
    /// Two conditionals that name the same group by number and by name are the same node, so
    /// <c>Branch.optimise</c> hoists them as a common prefix and reduces the two tails to a set.
    /// </summary>
    /// <remarks>
    /// Upstream compares <c>self.group</c>, which <c>fix_groups</c> has replaced with the resolved
    /// number by then; the text the pattern wrote is gone. A port that also compared the text left
    /// the branch unhoisted and emitted a <c>BRANCH</c> with the conditional duplicated - which is
    /// what the S11 blind review found. Both spellings compile to the identical code upstream,
    /// measured 2026-08-30.
    /// </remarks>
    [Test]
    [Arguments(@"(?<one>x)(?:(?(1)a|b)c|(?(one)a|b)d)")]
    [Arguments(@"(?<one>x)(?:(?(one)a|b)c|(?(1)a|b)d)")]
    [Arguments(@"(?<one>x)(?:(?(one)a|b)c|(?(one)a|b)d)")]
    public void Conditionals_naming_one_group_two_ways_are_hoisted_as_a_common_prefix(string pattern)
    {
        CompiledPattern compiled = PatternCompiler.Compile(pattern);

        compiled
            .Code.Should()
            .Equal(30u, 1, 1, 1, 12, 1, 120, 20, 32, 1, 12, 1, 97, 36, 12, 1, 98, 20, 65, 1, 74, 1, 2, 100, 99, 20, 1);
    }

    /// <summary>
    /// A named group nested inside itself is given a negative private alias while it is open, and
    /// <c>Group._compile</c> turns that back into <c>group_count - alias</c> for the private slot
    /// while the public slot keeps the real number. Here the inner copy compiles as private 2,
    /// public 1, and the whole pattern still reports one group.
    /// </summary>
    /// <remarks>
    /// The alias also has to survive as far as the group call: <c>(?&amp;x)</c> resolves against
    /// <c>defined_groups</c>, which is keyed by the number the group was <i>defined</i> under.
    /// </remarks>
    [Test]
    public void A_named_group_nested_inside_itself_gets_a_private_alias_the_compiler_fixes_up()
    {
        CompiledPattern compiled = PatternCompiler.Compile("(?<x>a(?<x>b))(?&x)");

        compiled.Code.Should().Equal(11u, 0, 30, 1, 1, 1, 12, 1, 97, 30, 1, 2, 1, 12, 1, 98, 20, 20, 20, 31, 0, 1);
        compiled.GroupCount.Should().Be(1);
        Equivalence.Lines(compiled.GroupIndex).Should().Equal("x=1");
    }
}
