using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A copy of a called group is prepared as the group would be if it were written out at the call
/// (D50, ledger entry 66).
/// </summary>
/// <remarks>
/// <para>
/// A group called with another direction or fuzziness than where it is written is compiled again
/// as a copy. Upstream compiles that copy from the group as parsed (<c>upstream/regex/_main.py</c>:637,
/// <c>_regex_core.py</c>:4449), before <c>optimise</c> and <c>pack_characters</c>, so a run of
/// characters in the copy is never packed into a string and never meets a full case fold: under
/// <c>(?fi)</c>, <c>SS</c> written out matches 'ß', and a call's copy of it does not.
/// </para>
/// <para>
/// Every expected answer is this port's and upstream's answer for the pattern with the call
/// written out as the body it calls (regex 2026.9.10, measured 2026-10-03), quoted beside it.
/// </para>
/// </remarks>
public sealed class CalledCopyTests
{
    // A backwards copy, for a call in a lookbehind.
    //   search('(?fi)(?(DEFINE)(?P<g1>SS))(?<=(?&g1))', 'ß')   None upstream
    //   search('(?fi)(?<=(?:SS))', 'ß')                          (1, 1), the call written out
    [Test]
    public void A_backwards_copy_is_packed_so_a_full_case_fold_matches()
    {
        Match m = new FuzzyRegex("(?fi)(?(DEFINE)(?P<g1>SS))(?<=(?&g1))").Match("ß");

        (m.Success, m.Index, m.Length).Should().Be((true, 1, 0));
    }

    // A fuzzy copy, for a call in a fuzzy section.
    //   search('(?fi)(?(DEFINE)(?P<g1>SS))(?:(?&g1)){i<=1}', 'ß')   None upstream
    //   search('(?fi)(?:(?:SS)){i<=1}', 'ß')                          (0, 1) with no errors
    [Test]
    public void A_fuzzy_copy_is_packed_so_a_full_case_fold_matches()
    {
        Match m = new FuzzyRegex("(?fi)(?(DEFINE)(?P<g1>SS))(?:(?&g1)){i<=1}").Match("ß");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    // A fuzzy copy is not optimised, as a group written out in a fuzzy section is not. The
    // pattern's own optimiser pass rewrote the atomic group's body in place to the set [ab], and
    // the copy was compiled from that tree. In a fuzzy section an alternation's first alternative
    // can match with an error, and the atomic group then keeps that match: here 'a' is deleted,
    // so 'c' must meet 'b' and fails. The set, which has no first alternative, matched 'b'.
    //   search('(?(DEFINE)(?P<g1>(?>a|b)c))(?:(?&g1)){d<=1}', 'b')   (0, 1) with a deletion upstream
    //   search('(?:(?:(?>a|b)c)){d<=1}', 'b')                          None, the call written out
    //   search('(?i)(?(DEFINE)(?P<g1>(?>a|b)))(?:a(?&g1)){s<=1}', 'ab')   (0, 2) with no errors upstream
    //   search('(?i)(?:a(?:(?>a|b))){s<=1}', 'ab')                          (0, 2) with one substitution
    [Test]
    public void A_fuzzy_copy_is_not_left_optimised_by_the_patterns_own_pass()
    {
        new FuzzyRegex("(?(DEFINE)(?P<g1>(?>a|b)c))(?:(?&g1)){d<=1}").Match("b").Success.Should().BeFalse();

        Match m = new FuzzyRegex("(?i)(?(DEFINE)(?P<g1>(?>a|b)))(?:a(?&g1)){s<=1}").Match("ab");
        (m.Success, m.Index, m.Length).Should().Be((true, 0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    // The control: a forward exact call needs no copy, and runs the group as written, packed.
    //   search('(?fi)(?(DEFINE)(?P<g1>SS))(?&g1)', 'ß')   (0, 1) upstream too
    [Test]
    public void A_call_with_the_groups_own_features_runs_the_group_as_written()
    {
        Match m = new FuzzyRegex("(?fi)(?(DEFINE)(?P<g1>SS))(?&g1)").Match("ß");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    // The copy is compiled from a tree the pattern's own optimiser pass never touched, so its code
    // is the code of the group written out where the call is. Each row puts an atomic group, whose
    // Optimise rewrites its body in place, or a fuzzy section with a scoped flag, which a second
    // PackCharacters packs differently, under one kind of node. If that node's Clone shared it
    // instead of copying it, the pattern's pass would rewrite the node the copy is made from, and
    // the copy's code would no longer appear in the written-out form's code. Each row fails with
    // its own node's Clone returning the node itself (checked 2026-10-03; a set or a named list
    // shares nothing that can change, see RegexBase.Clone).
    [Test]
    [Arguments("Branch", "(?(DEFINE)(?P<g1>(?:(?>a|b)c|z)))(?:(?&g1)){d<=1}", "(?:(?P<g1>(?:(?>a|b)c|z))){d<=1}")]
    [Arguments("GreedyRepeat", "(?(DEFINE)(?P<g1>(?:(?>a|b)c)+))(?:(?&g1)){d<=1}", "(?:(?P<g1>(?:(?>a|b)c)+)){d<=1}")]
    [Arguments("LookAround", "(?(DEFINE)(?P<g1>(?=(?>a|b)c)))(?:(?&g1)){d<=1}", "(?:(?P<g1>(?=(?>a|b)c))){d<=1}")]
    [Arguments(
        "LookAroundConditional",
        "(?(DEFINE)(?P<g1>(?(?=(?>a|b)c)x|y)))(?:(?&g1)){d<=1}",
        "(?:(?P<g1>(?(?=(?>a|b)c)x|y))){d<=1}"
    )]
    [Arguments("Fuzzy", "(?(DEFINE)(?P<g1>(?:ss|ﬀ(?-i:ss)){e<=1}))(?<=(?&g1))", "(?<=(?P<g1>(?:ss|ﬀ(?-i:ss)){e<=1}))")]
    [Arguments("Atomic", "(?(DEFINE)(?P<g1>(?>a|b)c))(?:(?&g1)){d<=1}", "(?:(?P<g1>(?>a|b)c)){d<=1}")]
    public void A_copy_compiles_to_the_code_of_its_group_written_out(string node, string called, string writtenOut)
    {
        uint[] call = [.. PatternCompiler.Compile(called).Code];
        uint[] written = [.. PatternCompiler.Compile(writtenOut).Code];

        // The one copy follows the pattern's SUCCESS: CALL_REF and its reference, the group, and
        // the END of the CALL_REF.
        int[] successes =
        [
            .. Enumerable
                .Range(0, call.Length - 1)
                .Where(i => call[i] == (uint)Opcode.Success && call[i + 1] == (uint)Opcode.CallRef),
        ];
        successes.Should().ContainSingle("{0}'s pattern has one copy", node);
        uint[] copy = call[(successes[0] + 3)..^1];

        bool found = Enumerable
            .Range(0, written.Length - copy.Length + 1)
            .Any(j => written.AsSpan(j, copy.Length).SequenceEqual(copy));
        found.Should().BeTrue("the copy under {0} is the group written out: {1}", node, writtenOut);
    }
}
