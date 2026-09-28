using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A fuzzy section's minimum error count can be met by a text character inserted at the end of the
/// section, as its maximum already allows.
/// </summary>
/// <remarks>
/// <para>
/// An insertion is a text character the pattern does not account for, and it may stand after the
/// last item of a section as well as between two of them. Upstream tries those trailing insertions
/// when backtracking into its <c>END_FUZZY</c> (<c>upstream/src/_regex.c</c>:15512-15563), but the
/// frame that offers them is pushed only after the forward <c>END_FUZZY</c> has checked the
/// section's minimums (:12461-12462 against :12500-12511). A section that reaches its end below its
/// minimum backtracks at once, and never tries the insertion that would have met it. So
/// <c>(?:ab){1&lt;=e&lt;=2}c</c> over 'abxc' is (0, 4) with one insertion upstream, where a string's
/// own retry (:14764-14768) inserts the 'x' before the section ends, while
/// <c>(?:a){1&lt;=e&lt;=1}c</c> over 'axc' is None. Known defect D9, finding F-D; ledger entry 51.
/// </para>
/// <para>
/// The expected answers are those of <c>tools/probes/fuzzy-reference-matcher.py</c>, whose rule 6
/// checks a minimum after the trailing insertions. Every upstream answer quoted below was measured
/// on <c>regex</c> 2026.9.10 on 2026-09-28.
/// </para>
/// </remarks>
public sealed class FuzzyMinimumErrorTests
{
    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer. Upstream: None on every row.
    // The first three were queue item 8's red rows.
    [Test]
    [Arguments(@"(?:a){1<=e<=2}b", "aab", 3, 1)]
    [Arguments(@"(?:[ab]){1<=e<=2}a", "bba", 3, 1)]
    [Arguments(@"(a)(?:\1){1<=e<=2}b", "aaab", 4, 1)]
    [Arguments(@"(?:a){1<=i<=2}b", "aab", 3, 1)]
    [Arguments(@"(?:a){2<=i<=2}b", "aaab", 4, 2)]
    [Arguments(@"(?:a){2<=e<=2}b", "aaab", 4, 2)]
    [Arguments(@"(?:a){1<=e<=2}c", "axc", 3, 1)]
    [Arguments(@"(?:(?:a){1<=e<=1}){1<=e<=2}b", "aab", 3, 1)]
    [Arguments(@"(?:(?:a){1<=e<=1}b){e<=1}", "aab", 3, 1)]
    public void A_minimum_is_met_by_an_insertion_at_the_end_of_the_section(
        string pattern,
        string text,
        int length,
        int insertions
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, insertions, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void The_insertion_stands_after_the_exact_item_and_inside_an_enclosing_group()
    {
        // match('((?:a){1<=e<=2})b', 'aab')  None
        Match m = new FuzzyRegex("((?:a){1<=e<=2})b").MatchAtStart("aab");

        (m.Index, m.Length).Should().Be((0, 3));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 2));
        m.FuzzyChanges.Insertions.Should().Equal(1);
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_full_match_can_meet_the_minimum_with_its_last_character()
    {
        // fullmatch('(?:a){1<=e<=2}', 'aa')  None
        Match m = new FuzzyRegex("(?:a){1<=e<=2}").FullMatch("aa");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    [Arguments(@"(?:a){1<=e<=2}b", "aab", 0, 3)]
    [Arguments(@"(?:a){1<=e<=1}b", "aaab", 1, 3)]
    public void A_search_finds_the_earlier_start(string pattern, string text, int index, int length)
    {
        // search('(?:a){1<=e<=2}b', 'aab')   (2, 3), one deletion
        // search('(?:a){1<=e<=1}b', 'aaab')  (3, 4), one deletion
        // The section-end insertion takes no notice of where the search began (reference rule 5).
        Match m = new FuzzyRegex(pattern).Match(text);

        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_reversed_section_meets_its_minimum_with_an_insertion_to_its_left()
    {
        // match('(?r)b(?:a){1<=e<=2}', 'baa')   None
        // search('(?r)b(?:a){1<=e<=2}', 'baa')  (0, 1), one deletion
        var regex = new FuzzyRegex("(?r)b(?:a){1<=e<=2}");
        Match anchored = regex.MatchAtStart("baa");
        Match search = regex.Match("baa");

        (anchored.Success, anchored.Index, anchored.Length).Should().Be((true, 0, 3));
        // A reversed insertion is recorded at the position before the step, as upstream records one:
        // search('(?r)x(?:a){i<=1}', 'xya') has fuzzy_changes ([], [2], []).
        anchored.FuzzyChanges.Insertions.Should().Equal(2);
        (search.Index, search.Length).Should().Be((0, 3));
        search.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    [Arguments("(?b)")]
    [Arguments("(?e)")]
    public void Best_and_enhanced_matching_see_the_insertion(string flag)
    {
        // search('(?b)(?:a){1<=e<=2}b', 'aab')  (2, 3), one deletion; (?e) the same
        // fullmatch of either over 'aab'        None
        var regex = new FuzzyRegex(flag + "(?:a){1<=e<=2}b");
        Match search = regex.Match("aab");
        Match full = regex.FullMatch("aab");

        (search.Index, search.Length).Should().Be((0, 3));
        search.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        (full.Success, full.Index, full.Length).Should().Be((true, 0, 3));
        full.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    // Upstream and this port agree on every row: the controls. An insertion raises neither the
    // substitution nor the deletion count, so it cannot meet an s or d minimum; the fuzzy test
    // [x] refuses the 'a' the insertion would take; one error allows one insertion, not the two
    // 'aaab' needs; and where the minimum is already met the insertion comes on backtracking,
    // after a substitution, as it always has.
    [Test]
    [Arguments(@"(?:a){1<=s<=1,i<=1}b", "aab")]
    [Arguments(@"(?:a){1<=d<=1,i<=1}b", "aab")]
    [Arguments(@"(?:a){1<=e<=2:[x]}b", "aab")]
    [Arguments(@"(?:a){1<=e<=1}b", "aaab")]
    public void An_insertion_that_cannot_meet_the_minimum_is_not_a_match(string pattern, string text)
    {
        new FuzzyRegex(pattern).MatchAtStart(text).Success.Should().BeFalse();
    }

    // Upstream and this port agree.
    [Test]
    [Arguments(@"(?:a){1<=e<=2}b", "cab", 1, 1, 0)]
    [Arguments(@"(?:a){1<=s<=1,i<=1}b", "cab", 1, 1, 0)]
    [Arguments(@"(?:ab){1<=e<=2}c", "abxc", 0, 1, 0)]
    [Arguments(@"(?:(?:a){e<=1}){1<=e<=2}b", "aab", 0, 1, 0)]
    public void A_minimum_met_before_the_end_keeps_its_answer(
        string pattern,
        string text,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text);

        m.Success.Should().BeTrue();
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }
}
