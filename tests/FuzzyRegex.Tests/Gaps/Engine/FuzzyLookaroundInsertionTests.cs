using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A lookaround that fails inside a fuzzy section can be passed by inserting a text character in
/// front of it, as every other zero-width assertion can.
/// </summary>
/// <remarks>
/// <para>
/// An insertion is a text character the pattern does not account for, and it may stand between any
/// two pattern items. Upstream tries one before a failing <c>\b</c>, <c>$</c> or <c>\G</c>
/// (<c>fuzzy_match_item</c> with a step of 0, <c>upstream/src/_regex.c</c>:12060-12075), but not
/// before a failing lookaround: when a positive lookaround's body runs out of choices its backtrack
/// case just carries on backtracking (:17115-17168), and a negative lookaround whose body matched
/// goes straight to <c>backtrack</c> (:12918-13000). So <c>(?:b\b){i&lt;=1}</c> over 'bx c' is
/// (0, 2) with one insertion upstream, and <c>(?:b(?=c)){i&lt;=1}</c> over 'bxc' is None. A
/// lookaround consumes nothing, so it can be neither deleted nor substituted, and an insertion is
/// the one error that can get past it. Known defect D8, finding S3-F2; ledger entry 50.
/// </para>
/// <para>
/// Every upstream answer quoted below was measured on <c>regex</c> 2026.9.10 on 2026-09-28.
/// </para>
/// </remarks>
public sealed class FuzzyLookaroundInsertionTests
{
    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer. Upstream's answer is in the
    // last argument.
    [Test]
    [Arguments(@"(?:b(?=c)){i<=1}", "bxc", 0, 2, 1, "None")]
    [Arguments(@"(?:b(?!x)){i<=1}", "bxc", 0, 2, 1, "None")]
    [Arguments(@"(?:b(?<=x)c){i<=1}", "bxc", 0, 3, 1, "None")]
    [Arguments(@"(?:b(?<!b)c){i<=1}", "bxc", 0, 3, 1, "None")]
    [Arguments(@"(?:b(?=c)){i<=2}", "bxxc", 0, 3, 2, "None")]
    [Arguments(@"(?:b(?=c)c){i<=1}", "bxc", 0, 3, 1, "None")]
    [Arguments(@"(?:b(?=c)){i<=1}c", "bxc", 0, 3, 1, "None")]
    [Arguments(@"(?:b(?=(?=c))){i<=1}", "bxc", 0, 2, 1, "None")]
    [Arguments(@"(?:(?>b(?=c))){i<=1}", "bxc", 0, 2, 1, "None")]
    [Arguments(@"(?r)(?:(?<=c)b){i<=1}", "cxb", 1, 2, 1, "None")]
    [Arguments(@"(?r)(?:(?<!x)b){i<=1}", "cxb", 1, 2, 1, "None")]
    public void An_insertion_is_tried_before_a_failing_lookaround(
        string pattern,
        string text,
        int index,
        int length,
        int insertions,
        string upstream
    )
    {
        _ = upstream;
        Match m = new FuzzyRegex(pattern).Match(text);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, insertions, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void The_insertion_comes_before_an_error_at_a_later_start()
    {
        // search('(?:b(?=c)){e<=1}', 'bxc')  (1, 2), one substitution: a later start
        Match m = new FuzzyRegex("(?:b(?=c)){e<=1}").Match("bxc");

        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Insertions.Should().Equal(1);
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void An_anchored_match_may_insert_before_a_leading_lookaround()
    {
        // match('(?:(?=b)b){i<=1}', 'xb')  None
        Match m = new FuzzyRegex("(?:(?=b)b){i<=1}").MatchAtStart("xb");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void The_lookaround_keeps_what_it_captured_at_the_new_position()
    {
        // search('(?:b(?=(c))){i<=1}', 'bxc')  None
        // search('(?:b(?!(x))){i<=1}', 'bxc')  None
        Match positive = new FuzzyRegex("(?:b(?=(c))){i<=1}").Match("bxc");
        Match negative = new FuzzyRegex("(?:b(?!(x))){i<=1}").Match("bxc");

        (positive.Index, positive.Length).Should().Be((0, 2));
        (positive.Groups[1].Success, positive.Groups[1].Index).Should().Be((true, 2));
        (negative.Index, negative.Length).Should().Be((0, 2));
        negative.Groups[1].Success.Should().BeFalse();
    }

    // Upstream and this port agree on every row: the controls.
    [Test]
    [Arguments(@"(?:b(?=c)){s<=1}", "bxc", 1, 1, 1, 0, 0)]
    [Arguments(@"(?:b(?=c)){d<=1}", "bxc", 2, 0, 0, 0, 1)]
    [Arguments(@"(?:b(?=c)){i<=1}", "bc", 0, 1, 0, 0, 0)]
    [Arguments(@"(?:(?=b)b){i<=1}", "xb", 1, 1, 0, 0, 0)]
    [Arguments(@"(?:b\b){i<=1}", "bx c", 0, 2, 0, 1, 0)]
    public void Errors_other_than_an_insertion_cannot_pass_a_lookaround(
        string pattern,
        string text,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        // The fourth row is the search-anchor rule: no insertion at the start a search began from.
        Match m = new FuzzyRegex(pattern).Match(text);

        (m.Success, m.Index, m.Length).Should().Be((true, index, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }

    // Upstream and this port agree.
    [Test]
    [Arguments(@"(?:b(?!x)){i<=1}", "bxxc")]
    [Arguments(@"(?:b(?=c)){i<=1}", "bxxc")]
    [Arguments(@"(?:b(?=c)){e<=1}", "bx")]
    [Arguments(@"b(?=c)", "bxc")]
    public void One_insertion_too_few_still_fails(string pattern, string text)
    {
        new FuzzyRegex(pattern).Match(text).Success.Should().BeFalse();
    }
}
