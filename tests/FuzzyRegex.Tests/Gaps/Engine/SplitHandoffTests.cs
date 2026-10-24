using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Witnesses for the hand-back of BasicMatchCore's hot locals across a call: since the split's
/// step 1 each call that may move <c>node</c> or <c>stringPos</c> gets a copy and the core copies it
/// back. Each case here goes red when one of those copy-backs is deleted, which no other test in
/// the suite noticed (mutation run, 2026-10-24).
/// </summary>
/// <remarks>
/// <para>
/// Every expected value was measured against <c>regex</c> 2026.9.29 on 2026-10-24.
/// </para>
/// <para>
/// Two copy-backs have no witness because none can exist: <c>gfoldedPos</c> after
/// <c>AdvanceRefGroupFld</c> and <c>foldChangesStart</c> after <c>AdvanceStringFldRev</c>. The core
/// reads those values again only on a fresh entry, which sets them first, or on a retry entry, which
/// follows a <c>BacktrackFuzzyRetry*Fld</c> call that has just written them from the backtracking stack
/// (<c>RetryFuzzyMatchGroupFld</c> and <c>RetryFuzzyMatchStringFld</c>). The copies stay so that every
/// call site follows the same rule.
/// </para>
/// </remarks>
public sealed class SplitHandoffTests
{
    [Test]
    // search('(?:a.c){e<=1}', 'a\nc'): span=(0, 3) counts=(1, 0, 0) changes=([1], [], [])
    [Arguments("a\nc", 0, 3, 1, 0, 0)]
    // search('(?:a.c){e<=1}', 'x a\nc'): span=(2, 5) counts=(1, 0, 0)
    [Arguments("x a\nc", 2, 5, 1, 0, 0)]
    // search('(?:a.c){e<=1}', 'a\nxc'): span=(0, 4) counts=(0, 1, 0)
    [Arguments("a\nxc", 0, 4, 0, 1, 0)]
    public void A_fuzzy_dot_that_fails_at_a_line_break_goes_on_from_the_item_its_edit_moved_to(
        string subject,
        int index,
        int end,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        // The '.' (ANY) arm hands a failed character to FuzzyMatchItem, which moves 'node' past the
        // dot for a substitution; the core must take that node back.
        Match m = new FuzzyRegex("(?:a.c){e<=1}").Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((index, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }
}
