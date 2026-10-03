using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A fuzzy constraint written after a group belongs to the group, so a call to that group runs it
/// fuzzily, with a fresh budget each time it is entered (D63, decided 2026-10-03).
/// </summary>
/// <remarks>
/// <para>
/// The parser moves a constraint written after a group inside it, so <c>(ab){e&lt;=1}</c> is the
/// same tree as <c>((?:ab){e&lt;=1})</c> (<c>upstream/regex/_regex_core.py</c>:596-600,
/// <c>apply_constraint</c>). A call to group 1 therefore answers as <c>(?:ab){e&lt;=1}</c> written
/// at the call site. A group that sits inside a section without owning it, as in
/// <c>(?:(ab)){e&lt;=1}</c>, is called exactly from outside the section.
/// </para>
/// <para>
/// Every expected answer is upstream's (regex 2026.9.10, measured 2026-10-03), and each row is one
/// where reading the constraint as applying from outside, the way a quantifier does, would answer
/// differently. Design note: <c>docs/plan/2026-10-03-d63-call-into-fuzzy-group.md</c>.
/// </para>
/// </remarks>
public sealed class ConstrainedGroupCallTests
{
    // index -1 means no match. The counts are (substitutions, insertions, deletions).
    [Test]
    // The decision's probes.
    [Arguments("(ab){e<=1}(?1)", "abxb", 0, 4, 1, 0, 0)]
    [Arguments("(ab){e<=1}(?1)", "xbxb", 0, 4, 2, 0, 0)]
    [Arguments("(?1)(ab){e<=1}", "xbab", 0, 4, 1, 0, 0)]
    [Arguments("(?:(ab)){e<=1}(?1)", "abxb", -1, 0, 0, 0, 0)]
    // One row per shape of the design note's grid.
    [Arguments("(ab){e<=1}(?1)", "aa", 0, 2, 0, 0, 2)]
    [Arguments("(abc){s<=1}x(?1)", "aacxaac", 0, 7, 2, 0, 0)]
    [Arguments("(?1)x(ab){e<=1}", "axa", 0, 3, 0, 0, 2)]
    [Arguments("(?:x(ab)){e<=1}(?1)", "aba", -1, 0, 0, 0, 0)]
    [Arguments("((?:ab){e<=1})(?1)", "aa", 0, 2, 0, 0, 2)]
    [Arguments("(ab){e<=1}(?:x(?1)){s<=1}", "abxxb", 0, 5, 1, 0, 0)]
    [Arguments("(ab){e<=1}(c(?1)){i<=1}(?2)", "acabcaab", 0, 8, 0, 1, 1)]
    // Reversed, BESTMATCH and ENHANCEMATCH.
    [Arguments("(?r)(ab){e<=1}(?1)", "aa", 0, 2, 0, 0, 2)]
    [Arguments("(?b)(abc){s<=1}x(?1)", "aacxaac", 0, 7, 2, 0, 0)]
    [Arguments("(?e)(?1)x(ab){e<=1}", "axa", 0, 3, 0, 0, 2)]
    public void A_call_answers_as_the_group_with_its_constraint_written_at_the_call_site(
        string pattern,
        string subject,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        if (index < 0)
        {
            m.Success.Should().BeFalse();
            return;
        }

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }

    // The row D63 was filed on. The first section matches 'a' and deletes 'b', and the call's own
    // section deletes 'a' and matches 'b': one error in each budget. Upstream answers None, but not
    // because of the call: it misses the same match with the call written out,
    // (ab){e<=1}(?:ab){e<=1} over 'ab', because it never goes back to delete an item that matched
    // exactly. That is the divergence "A fuzzy item that matched exactly can still be deleted"
    // (DIVERGENCES.md, ledgers 42 and 44), where this port is right.
    [Test]
    [Arguments("abx")]
    [Arguments("ab")]
    public void The_d63_witness_matches_with_one_deletion_in_each_section(string subject)
    {
        Match m = FuzzyRegex.Match(subject, "(?<o>(?<i>a)b){e<=1}(?&o)");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
        (m.Groups["o"].Index, m.Groups["o"].Length).Should().Be((0, 1));
    }
}
