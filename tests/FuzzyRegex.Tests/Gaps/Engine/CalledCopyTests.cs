using AwesomeAssertions;

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
}
