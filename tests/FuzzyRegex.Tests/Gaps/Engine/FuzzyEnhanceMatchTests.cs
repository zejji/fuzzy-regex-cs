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
    public void Enhancematch_improves_a_fuzzy_section_inside_a_repeat()
    {
        // PORT RIGHT, KNOWN DEFECT D59 (owner ruling 2026-09-30, option (a)): ledger entry 32's stale
        // total, reached when a later fuzzy section is rejected over budget (here a repeat's second
        // iteration). Upstream's improvement pass finds (1, 5)
        // but reports the total of the section it rejected as over budget (2, above the pass's
        // max_errors of 1: _regex.c:12484-12486, traced 2026-10-01), so the loop keeps its first
        // match. Measured 2026-10-01 on regex 2026.9.10:
        //
        //   (?e)(?P<g1>(?:a+(?:ab|a)){d<=1}){1,2}   'xaaabaxa'   (1, 6) two deletions   <- upstream
        //   the same without (?e) ....................................  (1, 6) two deletions   <- both
        //   (?e), the repeat lazy ....................................  (1, 5) no errors
        //   (?e), the budget outside the repeat ......................  (1, 5) no errors
        //
        // Classified as `enhancematch-stops-on-a-stale-total`.
        Match improved = new FuzzyRegex(@"(?e)(?P<g1>(?:a+(?:ab|a)){d<=1}){1,2}").Match("xaaabaxa");
        (improved.Index, improved.Index + improved.Length).Should().Be((1, 5));
        improved.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // Near variants: without the flag there is nothing to improve and both engines agree; the
        // lazy repeat and the outer budget, which upstream improves too, give the same exact fit.
        Match plain = new FuzzyRegex(@"(?P<g1>(?:a+(?:ab|a)){d<=1}){1,2}").Match("xaaabaxa");
        (plain.Index, plain.Index + plain.Length).Should().Be((1, 6));
        plain.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
        foreach (
            string variant in new[]
            {
                @"(?e)(?P<g1>(?:a+(?:ab|a)){d<=1}){1,2}?",
                @"(?e)(?:(?P<g1>a+(?:ab|a)){1,2}){d<=1}",
            }
        )
        {
            Match m = new FuzzyRegex(variant).Match("xaaabaxa");
            (m.Index, m.Index + m.Length).Should().Be((1, 5), variant);
            m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0), variant);
        }

        // Complete-matrix row 7619, the same trace (pass 2 total 2 at max 1): a possessive repeat of
        // an i/d section over ' x aaaa' was one deletion at 0 here and two upstream. D109 moved it to
        // one deletion at 1. NOT RIGHT YET, D111: neither (0, 0) nor (1, 1) is a match of the
        // flagless pattern, whose possessive repeat commits to (0, 2) at 0 and (1, 7) at 1; the
        // refinement narrows the slice to the first match, (0, 1), and the slice end cuts the
        // repeat short. This pins the answer until D111 is fixed.
        Match possessive = new FuzzyRegex(@"(?e)(?:(?:a+){i<=1,d<=1}){1,2}+").Match(" x aaaa");
        (possessive.Index, possessive.Length).Should().Be((1, 0));
        possessive.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
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
    public void Cost_ranking_keeps_the_cheaper_match_at_a_different_span_too()
    {
        // UPSTREAM ANSWERS (2, 4) WITH TWO DELETIONS AND THIS PORT ANSWERS (0, 4) WITH THREE
        // SUBSTITUTIONS, and the port's is the cheaper: '1i+2d+1s<=4' prices two deletions at 4 and
        // three substitutions at 3. Measured on regex 2026.7.19, 2026-09-13:
        //
        //   plain search('(?:\d\wba){1i+2d+1s<=4}', 'XX8QbaY'): span=(0, 4) counts=(3, 0, 0)
        //   (?e)  search('(?e)(?:\d\wba){1i+2d+1s<=4}', 'XX8QbaY'): span=(2, 4) counts=(0, 0, 2)
        //     changes=([], [], [4, 5])
        //
        // THIS ROW IS RECORDED BECAUSE IT WAS MISREAD AS A DEFECT. S42's first sitting handed it on
        // as "a real ENHANCEMATCH defect that is NOT the cost divergence", on the strength of
        // reverting 'IsBetterFuzzyMatch' to upstream's 'errors < bestErrors' and seeing the row
        // still diverge. Re-run on 2026-09-13 with that same revert, the row AGREES and only the
        // deliberate cost test fails, so the revert cannot have been in the build that was measured.
        // It is the plain cost divergence at a DIFFERENT SPAN - the family DECISIONS 2026-09-13 says
        // 'enhancematch-ranks-by-cost' deliberately reports rather than classifies, because widening
        // the predicate to take it would also swallow what a real engine defect looks like.
        Match m = new FuzzyRegex(@"(?e)(?:\d\wba){1i+2d+1s<=4}").Match("XX8QbaY");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(3, 0, 0));
    }

    [Test]
    public void Cost_ranking_at_a_different_span_agrees_again_once_the_costs_are_equal()
    {
        // The control for the test above. Upstream, measured the same day:
        //
        //   (?e)search('(?e)(?:\d\wba){1i+1d+1s<=4}', 'XX8QbaY'): span=(2, 4) counts=(0, 0, 2)
        Match m = new FuzzyRegex(@"(?e)(?:\d\wba){1i+1d+1s<=4}").Match("XX8QbaY");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((2, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
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
    public void Enhancematch_reaches_a_two_error_fit_where_upstream_keeps_a_three_error_one()
    {
        // The improvement loop tightens its budget to one below the best count so far and re-runs
        // inside the same span, so the fit it settles on should never spend more errors than the
        // same engine can spend there. Over 'a6ZZ_' upstream settles on three substitutions, and
        // the same pattern written '{e<=2}' - a SUBSET of what '{e<=3}' permits - gives it the
        // two-error fit below. Measured 2026-09-20 on regex 2026.9.10,
        // tools/probes/s57b-enhancematch-loses-a-candidate.py:
        //
        //   plain e<=2: counts=(1, 1, 0)   e<=3: counts=(3, 0, 0)   <- upstream and this port agree
        //   (?e)  e<=2: counts=(1, 1, 0)   e<=3: counts=(3, 0, 0)   <- upstream
        //   (?e)  e<=3: counts=(1, 1, 0)                            <- this port
        //
        // Upstream's '{e<=2}' changes are ([2], [4], []): the '6' of '\d+' stands in for a 'Z' and
        // the trailing '_' is an insertion.
        //
        // The expected value is upstream's own '{e<=2}' answer, position for position. The entry is
        // `enhancematch-loses-a-candidate` in the oracle's ExpectedDivergences and ledger entry 25.
        Match m = new FuzzyRegex(@"(?e)(?:a\d+Z){e<=3}").FullMatch("a6ZZ_");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 5));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(2);
        m.FuzzyChanges.Insertions.Should().Equal(4);
    }

    // DIVERGES FROM UPSTREAM, deliberately, and this test pins OUR answer rather than upstream's.
    [Test]
    public void Enhancematch_improves_past_a_first_branch_to_a_repeat_of_deletions()
    {
        // Over 'zzzz' the first branch fits only by substituting all four characters. The second
        // branch has no 'x' to match, so every fit it has is zero-width and spends one deletion per
        // iteration - one iteration, one deletion, is its cheapest. ENHANCEMATCH is documented to
        // "improve the fit (i.e. reduce the number of errors)", so its answer is that one deletion.
        // Upstream keeps the four substitutions, and contradicts itself: every twin below has the
        // same cheapest fit or a subset of the candidates, and improves. Measured 2026-09-25 on
        // regex 2026.9.10 (search over 'zzzz'):
        //
        //   (?e)(?:(?:abcd){s<=4}|(?:(?:x){d<=1})+)      (0, 4) counts=(4, 0, 0)   <- as drawn
        //   (?e)(?:(?:abcd){s<=4}|(?:x){d<=1})           (0, 0) counts=(0, 0, 1)   <- once only
        //   (?e)(?:(?:abcd){s<=4}|(?:(?:x){d<=1})+?)     (0, 0) counts=(0, 0, 1)   <- lazy
        //   (?e)(?:(?:abcd){s<=4}|(?:(?:x){d<=1}){1,3})  (0, 0) counts=(0, 0, 2)
        //   (?e)(?:(?:abcd){s<=4}|(?:(?:x){d<=1}){1,50}) (0, 4) counts=(4, 0, 0), and (0, 0) with
        //                                                two deletions over 'zz'
        //
        // Upstream's improvement run is a re-run with the budget tightened, and the same pattern with
        // that budget written in, `(?:...){e<=3}`, runs to MemoryError upstream: the repeat goes round
        // for ever on deletions (ledger entry 33), which this port stops. Oracle pin:
        // `enhancematch-loses-a-candidate`, ledger entry 25.
        foreach (string subject in new[] { "zzzz", "zz" })
        {
            Match m = new FuzzyRegex(@"(?e)(?:(?:abcd){s<=4}|(?:(?:x){d<=1})+)").Match(subject);

            m.Success.Should().BeTrue(subject);
            (m.Index, m.Length).Should().Be((0, 0), subject);
            m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1), subject);
            m.FuzzyChanges.Deletions.Should().Equal([0], subject);
        }

        // The control: without the flag the first branch wins, as it does upstream.
        new FuzzyRegex(@"(?:(?:abcd){s<=4}|(?:(?:x){d<=1})+)")
            .Match("zzzz")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(4, 0, 0));
    }

    [Test]
    public void The_improvement_loop_is_not_what_stops_without_the_flag()
    {
        // The control for the test above, and what rules out "this port's loop simply runs where
        // upstream's does not": on the SAME pattern with the flag deleted this port answers exactly
        // what upstream answers, three substitutions. The divergence is confined to the loop.
        new FuzzyRegex(@"(?:a\d+Z){e<=3}")
            .FullMatch("a6ZZ_")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(3, 0, 0));
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

    [Test]
    public void An_enhancematch_scan_improves_the_fit_at_the_resume_position_after_an_empty_match()
    {
        // D78, the ENHANCEMATCH sibling. After the empty match at 0 the scan resumes at 0 with
        // must_advance set; the first run finds (1,1) with three deletions ('abc') and the
        // improvement re-runs from 1, which reset the search anchor to 1 and so banned the empty
        // match there - losing the one-deletion fit ('d'). Upstream (?e), regex 2026.9.10 on
        // 2026-10-10: (0,0) d:0, (1,1) d:1,2,3, (2,2) d:2,3,4. Upstream (?e) overlapped: the three
        // below, the fit the flag promises at each start.
        FuzzyBestMatchTests
            .Scan("(?e)(?:abc|d){d<=3}", "xy")
            .Should()
            .Equal("(0,0) 0,0,1 d:0", "(1,1) 0,0,1 d:1", "(2,2) 0,0,1 d:2");

        // Upstream (?er): (2,2) d:2, (1,1) d:1,2,3, (0,0) d:0,1,2. Overlapped: the three below.
        FuzzyBestMatchTests
            .Scan("(?er)(?:abc|d){d<=3}", "xy")
            .Should()
            .Equal("(2,2) 0,0,1 d:2", "(1,1) 0,0,1 d:1", "(0,0) 0,0,1 d:0");
    }

    [Test]
    public void An_enhancematch_refinement_holds_backslash_G_only_where_the_search_began()
    {
        // D79, the ENHANCEMATCH sibling: each refinement re-run calls init_match from its own start,
        // which moved \G there. Upstream (?e), regex 2026.9.10 on 2026-10-10: (1, 4) with a
        // substitution; the ^ twin (?<=^.) gives (1, 3) exact.
        Match forward = new FuzzyRegex(@"(?e)(?<=\G.)(?:bcd|bc){e<=1}").Match("abcx");
        (forward.Index, forward.Index + forward.Length).Should().Be((1, 3));
        forward.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // Upstream (?er): (0, 3) with a substitution; the $ twin (?=.$) gives (1, 3) exact.
        Match reversed = new FuzzyRegex(@"(?er)(?:dcb|cb){e<=1}(?=.\G)").Match("xcba");
        (reversed.Index, reversed.Index + reversed.Length).Should().Be((1, 3));
        reversed.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void An_enhancematch_refinement_keeps_backslash_G_at_the_search_start()
    {
        // The D79 control, green before and after: the refinement from the search start itself
        // still sees \G hold there. Upstream (?e): (0, 4) with one deletion.
        Match m = new FuzzyRegex(@"(?e)\G(?:abc|xabcd){e<=2}").Match("xabc");

        (m.Index, m.Index + m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    // D86, the ENHANCEMATCH sibling. The improvement re-runs inside the first match's span, and a
    // \K moved that span's start past where the attempt began, so the re-run could not see the
    // text before the \K. Without \K, (?e)(?:ab){e<=1}c over 'aabc' improves the insertion fit
    // (0,4) to the exact (1,4); with \K the same refinement reports (3,4) with no errors. Upstream
    // 2026.9.10 keeps the first fit: (3,4) i:1, and (?er) (0,1) i:1 (measured 2026-10-15).
    [Test]
    [Arguments(@"(?e)(?:ab){e<=1}\Kc", "aabc", 3, 4)]
    [Arguments(@"(?er)c\K(?:ab){e<=1}", "cabb", 0, 1)]
    public void Enhancematch_refines_a_match_whose_start_a_K_moved(string pattern, string subject, int start, int end)
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }
}
