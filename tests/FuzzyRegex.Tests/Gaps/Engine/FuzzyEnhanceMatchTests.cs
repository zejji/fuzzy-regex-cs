using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// <c>ENHANCEMATCH</c>: the improvement loop itself - a match it improves, one it cannot, the
/// counts and changes of an improved match, the flag with <c>(?r)</c>, and the flag inside a scan,
/// which is what the restored slice makes correct.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value was measured against <c>regex</c> 2026.7.19 on 2026-09-13 by
/// <c>tools/probes/upstream-enhancematch.py</c>, and its line is quoted beside the assertion.
/// </para>
/// <para>
/// <b>One test here asserts an answer upstream does not give.</b> Upstream ranks the runs of the
/// improvement loop by error COUNT and this port ranks them by COST (owner decision, DECISIONS
/// 2026-09-12; upstream's open issue 470). The two rules agree on every unit-cost pattern, so the
/// divergence needs a cost equation to show at all -
/// <see cref="Cost_ranking_keeps_the_cheaper_match_where_upstream_takes_the_one_with_fewer_errors"/>
/// is that pattern, and it is also an <c>ExpectedDivergences</c> entry.
/// </para>
/// </remarks>
public sealed class FuzzyEnhanceMatchTests
{
    [Test]
    public void Enhancematch_improves_a_two_error_match_into_a_one_error_match()
    {
        // A plain fullmatch('(?:x|xyq){e<=2}', 'xyz'): span=(0, 3) counts=(0, 2, 0)
        new FuzzyRegex("(?:x|xyq){e<=2}")
            .FullMatch("xyz")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(0, 2, 0));

        // A improves fullmatch('(?e)(?:x|xyq){e<=2}', 'xyz'): span=(0, 3) counts=(1, 0, 0)
        Match m = new FuzzyRegex("(?e)(?:x|xyq){e<=2}").FullMatch("xyz");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void Enhancematch_keeps_the_match_it_cannot_improve()
    {
        // B cannot match('(?e)(?:[ab][cd][ef]){e<=1}', 'acx'): span=(0, 3) counts=(1, 0, 0)
        // changes=([2], [], []), which is exactly what the same pattern answers without the flag.
        // The second run of the loop fails - one error is already the fewest this pattern can use -
        // and the point of this test is that failing to improve must not lose the match.
        Match m = new FuzzyRegex("(?e)(?:[ab][cd][ef]){e<=1}").MatchAtStart("acx");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(2);
    }

    [Test]
    public void Enhancematch_reports_the_counts_and_the_changes_of_the_improved_match()
    {
        // F plain match('(?:bc){e}', 'c'): counts=(1, 0, 1) changes=([0], [], [1])
        // F changes match('(?e)(?:bc){e}', 'c'): counts=(0, 0, 1) changes=([], [], [0])
        //
        // Both halves matter. The counts come back from 'RestoreFuzzyCounts' and the changes from
        // 'RestoreFuzzyChanges', and a match that restored one and not the other would report a
        // single deletion at a position the failing run chose.
        Match m = new FuzzyRegex("(?e)(?:bc){e}").MatchAtStart("c");

        m.Success.Should().BeTrue();
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().Equal(0);
    }

    [Test]
    public void Enhancematch_searching_backwards_improves_the_match_and_records_the_error_at_its_own_end()
    {
        // D forward fullmatch('(?e)(?:x|xyq){e<=2}', 'xyz'): counts=(1, 0, 0) changes=([2], [], [])
        // D reverse fullmatch('(?er)(?:x|xyq){e<=2}', 'xyz'): counts=(1, 0, 0) changes=([3], [], [])
        //
        // The improvement is the same and the recorded position is not: a reversed substitution is
        // recorded at the position it was tried from, before the step backwards, so it sits one past
        // the character it replaced.
        Match forward = new FuzzyRegex("(?e)(?:x|xyq){e<=2}").FullMatch("xyz");
        Match reverse = new FuzzyRegex("(?er)(?:x|xyq){e<=2}").FullMatch("xyz");

        forward.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        forward.FuzzyChanges.Substitutions.Should().Equal(2);

        reverse.Success.Should().BeTrue();
        (reverse.Index, reverse.Length).Should().Be((0, 3));
        reverse.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        reverse.FuzzyChanges.Substitutions.Should().Equal(3);
    }

    [Test]
    public void Enhancematch_puts_the_slice_back_so_the_next_match_in_a_scan_looks_at_the_whole_subject()
    {
        // E scan spans finditer('(?e)(?:x|xyq){e<=2}', 'xyzxyz'):
        //   [((0, 1), (0, 0, 0)), ((1, 2), (1, 0, 0)), ((2, 3), (1, 0, 0)), ((3, 4), (0, 0, 0)),
        //    ((4, 5), (1, 0, 0)), ((5, 6), (1, 0, 0)), ((6, 6), (0, 0, 1))]
        //
        // The improvement loop narrows 'slice_start'/'slice_end' to the span of the best run so far
        // and restores them at the exit (:17994-17995). One state serves a whole scan, so without
        // that restore every match after the first would be searched for inside the previous match's
        // span - and the rows with one error above are the ones that narrowed it.
        (int, int)[] spans =
        [
            .. new FuzzyRegex("(?e)(?:x|xyq){e<=2}").Matches("xyzxyz").Select(static m => (m.Index, m.Length)),
        ];

        spans.Should().Equal((0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1), (6, 0));
    }

    [Test]
    public void Cost_ranking_keeps_the_cheaper_match_where_upstream_takes_the_one_with_fewer_errors()
    {
        // C plain fullmatch('(?:x|xyq){1i+9s+9d<=20}', 'xyz'): counts=(0, 2, 0) changes=([], [1, 2], [])
        // C cost  fullmatch('(?e)(?:x|xyq){1i+9s+9d<=20}', 'xyz'): counts=(1, 0, 0) changes=([2], [], [])
        //
        // UPSTREAM ANSWERS (1, 0, 0) AND THIS PORT ANSWERS (0, 2, 0). The first run of the loop takes
        // the 'x' branch with two insertions - two errors costing 1 each, so 2 - and the second run,
        // held to one error, takes the 'xyq' branch with one substitution costing 9. Upstream ranks
        // by error count, so 1 < 2 and it keeps the substitution. This port ranks by cost, so 9 > 2
        // and it keeps the two insertions. See 'Matcher.IsBetterFuzzyMatch' and DECISIONS
        // 2026-09-12; the row is in the oracle's ExpectedDivergences.
        Match m = new FuzzyRegex("(?e)(?:x|xyq){1i+9s+9d<=20}").FullMatch("xyz");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0));
        m.FuzzyChanges.Insertions.Should().Equal(1, 2);
    }

    [Test]
    public void Cost_ranking_and_count_ranking_agree_once_the_costs_are_equal()
    {
        // The control for the test above: the same pattern and subject with a unit cost equation
        // answers what upstream answers, which is what confines the divergence to cost equations.
        // C cost with '{e<=2}' is row A: counts=(1, 0, 0).
        new FuzzyRegex("(?e)(?:x|xyq){1i+1s+1d<=20}")
            .FullMatch("xyz")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(1, 0, 0));
    }
}
