using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D51 (owner ruling (a), 2026-09-30): a group call runs like a subroutine, so the captures the
/// called body makes are thrown away when the call returns, as in Perl 5.42 and PCRE2 10.47. The
/// group is left exactly as it was before the call: its span, and its capture list.
/// </summary>
/// <remarks>
/// <para>
/// DIVERGES FROM UPSTREAM, deliberately. Upstream 2026.9.10 restores only each group's
/// <c>current</c> on return (<c>push_groups</c>, <c>upstream/src/_regex.c:2490</c>, saves nothing
/// else at <c>GROUP_CALL</c>, <c>:13405</c>; <c>GROUP_RETURN</c> pops it at <c>:13516</c>), so the callee's captures stay on the list.
/// That breaks .NET's <see cref="Group"/> contract: <c>(?(DEFINE)(?&lt;c&gt;a))(?&amp;c)b</c> over
/// 'ab' gave Success false with one capture listed. Ledger entry 63; the oracle entry is
/// <c>group-call-discards-its-captures-on-return</c>.
/// </para>
/// <para>
/// Every expected value is the answer of the same pattern with each call replaced by a
/// non-capturing copy of the called body (the matrix's check C2). The comment beside each row
/// quotes PCRE2 10.47 (pip <c>pcre2</c> 0.7.1) and Perl 5.42.3 as (start, end) per group, and
/// upstream's capture list, all measured 2026-09-30 with <c>tools/probes/d51-call-capture-survey.py</c>. PCRE2 and
/// Perl report only the last capture of a group, so they settle Success and Value; the capture
/// list follows from the rule. Where a row uses fuzziness, reversal or a partial match, which
/// neither engine has, the written-out form is the authority.
/// </para>
/// </remarks>
public sealed class CallCaptureTests
{
    /// <summary>
    /// Each group rendered as <c>(start,end)[captures]</c>, or <c>unset[captures]</c> when it has no
    /// value, after the overall span.
    /// </summary>
    private static string Render(Match m)
    {
        if (!m.Success)
        {
            return "None";
        }

        return $"({m.Index},{m.Index + m.Length}){(m.PartialMatch ? "P" : "")} "
            + string.Join(" ", m.Groups.Values.Skip(1).Select(static g => RenderGroup(g)));
    }

    private static string RenderGroup(Group g) =>
        (g.Success ? $"({g.Index},{g.Index + g.Length})" : "unset")
        + "["
        + string.Join(",", g.Captures.Select(static c => $"({c.Index},{c.Index + c.Length})"))
        + "]";

    [Test]
    public void A_group_that_only_ran_inside_a_call_has_no_value_and_no_captures()
    {
        // The first ruling witness. PCRE2 (0,2) c=(-1,-1); Perl (0,2) c unset; upstream c=(-1,-1)
        // with captures ['a'].
        Match m = FuzzyRegex.Match("ab", "(?(DEFINE)(?<c>a))(?&c)b");

        (m.Index, m.Length).Should().Be((0, 2));
        m.Groups["c"].Success.Should().BeFalse();
        m.Groups["c"].Captures.Should().BeEmpty();
    }

    [Test]
    public void A_call_after_the_group_leaves_only_the_groups_own_capture()
    {
        // The second ruling witness. PCRE2 and Perl (0,3) c=(0,1); upstream c=(0,1) with captures
        // ['a', 'a'].
        Match m = FuzzyRegex.Match("aab", "(?<c>a)(?&c)b");

        m.Groups["c"].Value.Should().Be("a");
        m.Groups["c"].Index.Should().Be(0);
        m.Groups["c"].Captures.Select(static c => (c.Index, c.Length)).Should().Equal((0, 1));
    }

    [Test]
    // The matrix triage witness. PCRE2 and Perl (0,2) g1=(0,1); upstream captures ['a', 'a'].
    [Arguments("(?P<g1>a)(?1)", "aa", "fullmatch", "(0,2) (0,1)[(0,1)]")]
    // A backreference after the call reads the caller's capture: PCRE2 and Perl (0,3) c=(0,1),
    // and None over 'abb'; upstream agrees on both and lists ['a', 'b'].
    [Arguments(@"(?<c>[ab])(?&c)\1", "aba", "search", "(0,3) (0,1)[(0,1)]")]
    [Arguments(@"(?<c>[ab])(?&c)\1", "abb", "search", "None")]
    // A group set only inside a call does not exist afterwards, for a backreference or a
    // conditional: PCRE2, Perl and upstream give None, (0,2) with c unset, and None.
    [Arguments(@"(?(DEFINE)(?<c>a))(?&c)\1", "aa", "search", "None")]
    [Arguments("(?(DEFINE)(?<c>a))(?&c)(?(1)x|y)", "ay", "search", "(0,2) unset[]")]
    [Arguments("(?(DEFINE)(?<c>a))(?&c)(?(1)x|y)", "ax", "search", "None")]
    [Arguments("(?:(?<c>a)|x)(?1)?(?(1)y|z)", "xaz", "search", "(0,3) unset[]")]
    // A group inside the called group. PCRE2 answers span 0-3 with o at 0-1 and i unset, and so
    // does Perl; upstream lists o as 'x' then 'xy', and i as 'y'.
    [Arguments("(?<o>x(?<i>y)?)(?&o)", "xxy", "search", "(0,3) (0,1)[(0,1)] unset[]")]
    [Arguments(@"(?<o>x(?<i>y)?)(?&o)\2", "xyxyy", "search", "(0,5) (0,2)[(0,2)] (1,2)[(1,2)]")]
    [Arguments("(?<o>x(?<i>y)?)(?&o)(?(2)z|w)", "xxyw", "search", "(0,4) (0,1)[(0,1)] unset[]")]
    // Recursion: PCRE2 and Perl (0,7) p=(5,6); upstream lists ['a', 'b', '(b)', 'c'], the 'b'
    // coming from inside the recursion.
    [Arguments(@"\((?<p>[^()]|(?R))*\)", "(a(b)c)", "search", "(0,7) (5,6)[(1,2),(2,5),(5,6)]")]
    [Arguments("a(?:b(?R)?c)?(?<g>d)?", "abadcd", "search", "(0,6) (5,6)[(5,6)]")]
    // A group calling itself: PCRE2 and Perl (0,3) c=(0,3); upstream ['a', 'ba', 'bba'].
    [Arguments("(?<c>a|b(?&c))", "bba", "search", "(0,3) (0,3)[(0,3)]")]
    // A call inside a repeat: PCRE2 and Perl (0,4) c=(0,1), and (0,4) c=(2,3); upstream lists
    // ['a', 'a', 'a'] and ['a', 'b', 'b', 'a'].
    [Arguments("(?<c>a)(?:(?&c))+b", "aaab", "search", "(0,4) (0,1)[(0,1)]")]
    [Arguments("(?:(?<c>[ab])(?&c))+", "abba", "search", "(0,4) (2,3)[(0,1),(2,3)]")]
    // A call inside a lookaround: PCRE2 and Perl (0,1) c=(0,1), and (0,0) c=(0,1); upstream
    // ['a', 'a'] both times.
    [Arguments("(?<c>a)(?=(?&c))", "aa", "search", "(0,1) (0,1)[(0,1)]")]
    [Arguments("(?=(?<c>a)(?&c))", "aa", "search", "(0,0) (0,1)[(0,1)]")]
    // F30's rule still holds: a discarded lookaround leaves nothing either. PCRE2 and Perl (0,4)
    // g1=(0,1); upstream ['a', 'a'].
    [Arguments("(a)(?:(?!.(?1))|.)+?b", "aaab", "search", "(0,4) (0,1)[(0,1)]")]
    // A reversed call, written out as (?r)b(?:a)(?<c>a): c=(2,3) with one capture; upstream lists
    // ['a', 'a']. PCRE2 has no reverse matching.
    [Arguments("(?r)b(?&c)(?<c>a)", "baa", "search", "(0,3) (2,3)[(2,3)]")]
    public void A_call_keeps_none_of_the_captures_its_body_made(
        string pattern,
        string subject,
        string operation,
        string expected
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "fullmatch", StringComparison.Ordinal)
            ? regex.FullMatch(subject)
            : regex.Match(subject);

        Render(m).Should().Be(expected);
    }

    [Test]
    public void A_fuzzy_call_keeps_its_errors_but_not_its_captures()
    {
        // Written out, (?<c>ab)(?:ab){s<=1} over 'abxb' is (0,4) with one substitution and c = [ab].
        // Upstream gives the same span and counts with captures ['ab', 'xb'].
        Match m = FuzzyRegex.Match("abxb", "(?<c>ab)(?:(?&c)){s<=1}");

        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.Groups["c"].Captures.Select(static c => c.Value).Should().Equal("ab");
    }

    [Test]
    public void A_partial_match_after_a_call_keeps_only_the_callers_capture()
    {
        // Upstream: regex.search(r'(?<c>a)(?&c)b', 'aa', partial=True) is (0, 2) partial with
        // captures ['a', 'a']. Written out, (?<c>a)(?:a)b gives c = [a].
        Match m = new FuzzyRegex("(?<c>a)(?&c)b").Match("aa", partial: true);

        Render(m).Should().Be("(0,2)P (0,1)[(0,1)]");
    }

    /// <summary>
    /// D62 (owner ruling 2026-09-30): a partial result reports, for each group, what every
    /// completion of the path the partial took would report. A call that has not returned when the
    /// text runs out returns in any completion, and its return puts the caller's groups back, so the
    /// partial reports the groups as the outermost open call's caller had them.
    /// </summary>
    /// <remarks>
    /// Each expected value is the port's answer for the same pattern with the calls written out as
    /// non-capturing copies, which upstream 2026.9.10 gives too (measured 2026-09-30); upstream's
    /// answer for the call form, quoted per row, reports the unfinished call's captures. PCRE2 leaves
    /// the groups of a partial undefined (pcre2partial: "the values in the rest of the ovector are
    /// undefined") and Perl has no partial matching, so neither contradicts the rule.
    /// </remarks>
    [Test]
    // The witness; written out (?<o>(?<i>a)b)(?:(?:a)b). Upstream: i = (2,3), [(0,1), (2,3)].
    [Arguments("(?<o>(?<i>a)b)(?&o)", "aba", "(0,3)P (0,2)[(0,2)] (0,1)[(0,1)]")]
    // Two open calls, i calling j; written out (?<j>ce)(?<i>(?<k>b)(?:ce))(?:(?:b)(?:ce)).
    // Upstream: j [(0,2), (3,5)], k = (5,6).
    [Arguments("(?<j>ce)(?<i>(?<k>b)(?&j))(?&i)", "cebcebc", "(0,7)P (0,2)[(0,2)] (2,5)[(2,5)] (2,3)[(2,3)]")]
    // Two nested open calls of the same group, restored from the OUTER one: written out
    // (?<o>(?<i>a)(?:(?:a)(?:(?:a)b)?b)?b), i = (0,1) and o unset. Upstream: i = (2,3). A restore
    // from the innermost frame gives i = (1,2) with [(0,1), (1,2)] (blind review, 2026-09-30).
    [Arguments("(?<o>(?<i>a)(?&o)?b)", "aaa", "(0,3)P unset[] (0,1)[(0,1)]")]
    // Whole-pattern recursion; written out a(?<g>b)(?:a(?:b)(?:a(?:b)c)?c)?c. Upstream: g = (3,4).
    [Arguments("a(?<g>b)(?R)?c", "abab", "(0,4)P (1,2)[(1,2)]")]
    // Reversed, running out at the left; written out (?r)(?:b(?:a))(?<o>b(?<i>a)). Upstream: i = (0,1).
    [Arguments("(?r)(?&o)(?<o>b(?<i>a))", "aba", "(0,3)P (1,3)[(1,3)] (2,3)[(2,3)]")]
    // A fuzzy call, the substitution made inside it; written out (?<o>(?<i>a)b)(?:(?:a)b){s<=1}.
    // Upstream: i = (2,3).
    [Arguments("(?<o>(?<i>a)b)(?:(?&o)){s<=1}", "abx", "(0,3)P (0,2)[(0,2)] (0,1)[(0,1)]")]
    // Path-dependent. The partial runs out in the third iteration's first alternative, inside the
    // call (?&o) at 4, so the completion of that path returns from the call, restoring o = (2,4)
    // and i = (2,3) from the second iteration, and then needs 'c'. Upstream: i = (4,5), the
    // unfinished call's.
    [Arguments("^(?:(?&o)c|(?<o>(?<i>a)b))+$", "ababa", "(0,5)P (2,4)[(0,2),(2,4)] (2,3)[(0,1),(2,3)]")]
    // No call: a group still open at the edge stays unset, and one closed before it keeps its
    // value, here and upstream. D62 must not change this.
    [Arguments("(?<a>x)(?<b>y(?<c>z)w)", "xyz", "(0,3)P (0,1)[(0,1)] unset[] (2,3)[(2,3)]")]
    public void A_partial_that_ends_inside_an_unreturned_call_reports_the_callers_groups(
        string pattern,
        string subject,
        string expected
    ) => Render(new FuzzyRegex(pattern).Match(subject, partial: true)).Should().Be(expected);

    [Test]
    public void A_complete_match_through_the_same_call_is_unchanged_by_the_partial_rule()
    {
        // Only a partial result reaches the D62 restore (Matcher.BasicMatch asserts it); the same
        // pattern over a subject it completes on gives the D51 answer. PCRE2 and Perl: o = (0,2),
        // i = (0,1).
        Render(new FuzzyRegex("(?<o>(?<i>a)b)(?&o)").Match("abab", partial: true))
            .Should()
            .Be("(0,4) (0,2)[(0,2)] (0,1)[(0,1)]");
        Render(new FuzzyRegex("(?<o>(?<i>a)b)(?&o)").Match("abab")).Should().Be("(0,4) (0,2)[(0,2)] (0,1)[(0,1)]");
    }

    [Test]
    public void Backtracking_into_a_call_that_returned_gets_back_the_captures_it_made()
    {
        // The call to d returns with group 2 = 'a'@0; the caller's own call of group 2 then writes
        // 'b'@3 into the slot the return freed, and fails. Backtracking into d must see 'a' again,
        // so that \2 matches at 1 and the whole pattern matches (0,4). Reading 'b' there instead
        // makes the attempt fail. PCRE2 and Perl (0,4) with both groups unset; upstream (0,4).
        Match m = FuzzyRegex.Match("aaab", @"(?(DEFINE)(?<d>(.)(?:.)?\2))(?&d)(?:(?2)x)?ab");

        Render(m).Should().Be("(0,4) unset[] unset[]");
    }

    [Test]
    public void A_format_template_reads_only_the_captures_the_group_made_itself()
    {
        // Upstream 2026.9.10 lists group 1 as ['a', 'b'] here, the call's capture included, so
        // subf gives 'b' for {1[-1]} (though the group's value is 'a') and 'a' for {1[-2]}.
        // Here the list is ['a'] alone: {1[-1]} is the value, and {1[-2]} is out of range, as
        // upstream's own IndexError is for '([ab])' with no call. Measured 2026-10-10.
        FuzzyRegex.ReplaceFormat("ab", "([ab])(?1)", "{1[-1]}").Should().Be("a");

        Action pastTheEnd = static () => FuzzyRegex.ReplaceFormat("ab", "([ab])(?1)", "{1[-2]}");

        pastTheEnd.Should().Throw<ArgumentException>().WithMessage("capture index out of range*");
    }
}
