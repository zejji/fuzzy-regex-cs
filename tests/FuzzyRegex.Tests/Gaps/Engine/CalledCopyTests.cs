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
        // The call kept as a call (D101 would write it out): this is about the copy a call needs.
        uint[] call = [.. PatternCompiler.Compile(called, upstreamCalls: true).Code];
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

    // D125 (port and upstream). A copy of g1 is compiled after the pattern's SUCCESS, and upstream's
    // copy records g1's end index again, after every group of the pattern; lastindex is the matched
    // group with the highest end index, so it named g1. The copy now keeps the group's own order
    // (NodeCompiler.BuildGroup). g1 is recursive so that the call is kept rather than written out
    // (D101), with the call inside it so that neither direction is left recursive. The expected
    // values are regex 2026.9.29's for the written-out form, G = x|y[xy]*y:
    //   (?P<g1>G)(?:(?<!G))?(a)(b)    'xab' search and match -> 3, 'g1', ((0,3),(0,1),(1,2),(2,3))
    //   (?P<g1>G)(?<=G)(a)(b)         'xab' search -> 3, 'g1', same regs
    //   (?r)(a)(b)(?:(?!G))?(?P<g1>G) 'abx' search -> 1, None, ((0,3),(0,1),(1,2),(2,3))
    //   (?r)(a)(b)(?=G)(?P<g1>G)      'abx' search -> 1, None, same regs
    // lastgroup is g1 where lastindex is 3: upstream's lastgroup is the named group with the highest
    // end index. Upstream's call forms are no reference: over 'xab' it answers None (ledger entry 8,
    // min-width inflation), and over a longer text it has this defect: the forward rows over 'cxab'
    // give lastindex 1, the reversed rows over 'abxc' give 3, 'g1' (measured 2026-10-31).
    [Test]
    [Arguments(@"(?P<g1>x|y(?P>g1)y)(?:(?<!(?P>g1)))?(a)(b)", "xab", false, 3, "g1")]
    [Arguments(@"(?P<g1>x|y(?P>g1)y)(?:(?<!(?P>g1)))?(a)(b)", "xab", true, 3, "g1")]
    [Arguments(@"(?P<g1>x|y(?P>g1)y)(?<=(?P>g1))(a)(b)", "xab", false, 3, "g1")]
    [Arguments(@"(?r)(a)(b)(?:(?!(?P>g1)))?(?P<g1>x|y(?P>g1)y)", "abx", false, 1, null)]
    [Arguments(@"(?r)(a)(b)(?=(?P>g1))(?P<g1>x|y(?P>g1)y)", "abx", false, 1, null)]
    public void A_call_in_a_lookaround_the_other_way_does_not_change_the_last_group(
        string pattern,
        string subject,
        bool atStart,
        int lastIndex,
        string? lastGroup
    )
    {
        var regex = new FuzzyRegex(pattern);

        Match m = atStart ? regex.MatchAtStart(subject) : regex.Match(subject);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 3));
        (m.Groups[1].Index, m.Groups[2].Index, m.Groups[3].Index).Should().Be((0, 1, 2));
        (m.LastGroupNumber, m.LastGroupName).Should().Be((lastIndex, lastGroup));
    }

    // D125's other copy: a call inside a fuzzy section to a group written outside one. regex
    // 2026.9.29, search over 'xxab':
    //   (?P<g1>x|y[xy]*y)(?:x|y[xy]*y){e<=1}(a)(b)       (0, 4), lastindex 3, 'g1' (written out)
    //   (?P<g1>x|y(?P>g1)y)(?:(?P>g1)){e<=1}(a)(b)       (0, 4), lastindex 1, 'g1' (upstream's copy)
    [Test]
    public void A_call_in_a_fuzzy_section_does_not_change_the_last_group()
    {
        Match m = new FuzzyRegex(@"(?P<g1>x|y(?P>g1)y)(?:(?P>g1)){e<=1}(a)(b)").Match("xxab");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 4));
        (m.LastGroupNumber, m.LastGroupName).Should().Be((3, "g1"));
    }

    // Controls for D125, right before the fix as well: a call in a lookahead the same way makes no
    // copy, and nested groups. regex 2026.9.29: 'xax' search -> 2, 'g1', ((0,2),(0,1),(1,2)); 'xa'
    // match -> 1, None, ((0,2),(0,2),(0,1),(1,2)).
    [Test]
    [Arguments(@"(?P<g1>x)(a)(?=(?P>g1))", "xax", false, 2, "g1")]
    [Arguments(@"((?P<g1>x)(a))", "xa", true, 1, null)]
    public void A_call_the_same_way_and_nested_groups_keep_the_last_group(
        string pattern,
        string subject,
        bool atStart,
        int lastIndex,
        string? lastGroup
    )
    {
        var regex = new FuzzyRegex(pattern);

        Match m = atStart ? regex.MatchAtStart(subject) : regex.Match(subject);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 2));
        (m.LastGroupNumber, m.LastGroupName).Should().Be((lastIndex, lastGroup));
    }
}
