using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D111: an ENHANCEMATCH refinement looks for a better match inside the span of the first one, and
/// upstream does that by narrowing the slice to the span. The narrowed slice end cuts what an atomic
/// group, a possessive repeat or <c>\R</c> reads, so the refinement could report a span the pattern
/// does not match in the text. The port now keeps the caller's slice and holds the refinement's
/// matches to the span instead (<c>MatchState.RefineStart</c>).
/// </summary>
/// <remarks>
/// Every expected answer is one the flagless pattern can produce in the whole text: a flagless search
/// held to the span by a lookbehind and a lookahead outside the pattern finds it
/// (<c>.scratch/s1019/d111/probe/slice.cs</c>). Upstream regex 2026.9.10 gives the cut answers, so
/// each row is also an expected divergence.
/// </remarks>
public sealed class RankedRefinementWindowTests
{
    // Upstream and the port before D111: (1, 3) with no errors, because the slice narrowed to the
    // first match (0, 3) stops the atomic a* at 3, where in the text it takes 'aaa' and the match
    // from 1 is (1, 4). Nothing inside (0, 3) beats the first match, so it stands.
    [Test]
    [Arguments("(?e)(?:xaq|a(?>a*)){e<=1}", "xaaa", 0, 3, 1, 0, 0)]
    [Arguments("(?e)(?:xaq|aa*+){e<=1}", "xaaa", 0, 3, 1, 0, 0)]
    // \R is atomic: in the text it takes '\r\n', past the first match's end at 2.
    [Arguments("(?e)(?:bcx|a\\R){s<=1,d<=1}", "b\r\nx", 0, 2, 1, 0, 1)]
    // Reversed, the narrowed slice START cuts the atomic and possessive bodies.
    [Arguments("(?er)(?:b(?>b*)){s<=1,d<=1}", "abxbbbb", 1, 7, 1, 0, 1)]
    [Arguments("(?er)(?:aax|(?:ab|a)*+b){e<=2}", "aababax", 4, 7, 1, 0, 0)]
    [Arguments("(?er)(?:bbx|\\Rb){1s+2d+2i<=2}", "\n\r\nb\r\n\rb", 6, 7, 0, 0, 1)]
    // A (*PRUNE) or (*SKIP) commits too, but D109 refuses a ranked fuzzy pattern that holds one:
    // FuzzyBestMatchTests.A_ranked_fuzzy_pattern_with_a_backtracking_verb_is_refused.
    public void An_enhancematch_refinement_reads_the_text_past_the_first_match(
        string pattern,
        string subject,
        int start,
        int end,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }

    [Test]
    public void An_enhancematch_refinement_still_finds_a_better_match_inside_the_first()
    {
        // The control, on patterns the window applies to (an atomic group makes RefinesInAWindow
        // true): it keeps what the narrowed slice found when nothing reads past the span.
        // (?e)(?:xaq|a(?>b)){e<=1} over 'xaab': the first match (0, 3) costs a substitution, and the
        // exact 'ab' at (2, 4) ends outside it, so the refinement keeps (0, 3). (?e)(?:xabq|a(?>b))
        // over 'xaba': the first match (0, 4) costs a substitution and the exact (1, 3) is inside it
        // and wins. Upstream agrees on both.
        new FuzzyRegex("(?e)(?:xaq|a(?>b)){e<=1}")
            .PatternObject.RefinesInAWindow.Should()
            .BeTrue();

        Match outside = new FuzzyRegex("(?e)(?:xaq|a(?>b)){e<=1}").Match("xaab");
        Match inside = new FuzzyRegex("(?e)(?:xabq|a(?>b)){e<=1}").Match("xaba");

        (outside.Index, outside.Index + outside.Length).Should().Be((0, 3));
        outside.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        (inside.Index, inside.Index + inside.Length).Should().Be((1, 3));
        inside.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void An_enhancematch_scan_after_a_refinement_searches_the_whole_text_again()
    {
        // The window ends with the refinement: the scan's next step starts from the first match's end
        // and finds the matches after it, which a window left in place at (0, 3) would refuse.
        // The rest is the flagless scan's: at 3 the atomic a* commits to ' ' with a substitution
        // (D109), so (3, 5). Upstream: (1, 3), (3, 4), (4, 5), (5, 8), (8, 8).
        string[] spans =
        [
            .. new FuzzyRegex("(?e)(?:xaq|a(?>a*)){e<=1}")
                .Matches("xaaa xaq")
                .Select(static m => $"({m.Index},{m.Index + m.Length})"),
        ];

        spans.Should().Equal("(0,3)", "(3,5)", "(5,8)", "(8,8)");
    }
}
