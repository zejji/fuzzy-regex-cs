using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// <c>BESTMATCH</c>: the two passes of <c>Matcher.DoBestFuzzyMatch</c> - a first match that is not
/// the best one, equal-best candidates where the earliest wins, the flag with <c>(?r)</c>, the flag
/// inside a scan, the flag with a partial match, and the widened-slice fallback that
/// <c>RE_MAX_ERRORS</c> is what reaches.
/// </summary>
/// <remarks>
/// Every expected value was measured against <c>regex</c> 2026.7.19 on 2026-09-13 by
/// <c>tools/probes/upstream-bestmatch.py</c>, and its line is quoted beside the assertion.
/// </remarks>
public sealed class FuzzyBestMatchTests
{
    [Test]
    public void Bestmatch_passes_over_a_near_match_for_the_exact_one_further_on()
    {
        // first-not-best     : span=(0, 6) value='fxxbar' counts=(2, 0, 0)
        new FuzzyRegex("(foobar){e<=3}")
            .Match("fxxbar foobar")
            .FuzzyCounts.Should()
            .Be(new FuzzyCounts(2, 0, 0));

        // first-not-best (?b): span=(7, 13) value='foobar' counts=(0, 0, 0)
        Match m = new FuzzyRegex("(?b)(foobar){e<=3}").Match("fxxbar foobar");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((7, 13));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void Bestmatch_keeps_the_earliest_of_two_equally_good_candidates()
    {
        // equal-best (?b): span=(0, 3) value='cbt' counts=(1, 0, 0) changes=([1], [], [])
        //
        // 'cbt' and 'cet' are both one substitution, so the best list holds two entries and nothing
        // in the second pass can better either of them. The earliest is the answer, which is the
        // 'better' test's tie-break at ':17746' and the only thing that decides between them.
        Match m = new FuzzyRegex("(?b)(cat){e<=1}").Match("cbt xxx cet");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1);
    }

    [Test]
    public void Bestmatch_reversed_walks_the_slice_the_other_way_and_finds_the_same_best_match()
    {
        // reverse (?b): span=(7, 13) value='foobar' counts=(0, 0, 0)
        //
        // The step is -1 and 'max_offset' is measured from 'slice_start' rather than 'slice_end'
        // (':17715'), so getting either end wrong loses the exact match.
        Match m = new FuzzyRegex("(?br)(foobar){e<=3}").Match("fxxbar foobar");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((7, 13));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void Bestmatch_in_a_scan_restores_the_slice_between_matches()
    {
        // scan (?b): [((4, 7), 'cat', (0, 0, 0)), ((8, 11), 'cet', (1, 0, 0))]
        //
        // 'cbt' at (0, 3) is never a match at all: the first scan step searches the whole subject and
        // the exact 'cat' at 4 beats it. The second step then starts at 7, and the widened-slice
        // fallback is the only thing in this method that writes 'slice_start'/'slice_end' - so a
        // missing restore would search step two inside step one's span.
        (int, int, FuzzyCounts)[] found =
        [
            .. new FuzzyRegex("(?b)(cat){e<=1}")
                .Matches("cbt cat cet")
                .Select(static m => (m.Index, m.Index + m.Length, m.FuzzyCounts)),
        ];

        found.Should().Equal((4, 7, new FuzzyCounts(0, 0, 0)), (8, 11, new FuzzyCounts(1, 0, 0)));
    }

    [Test]
    public void Bestmatch_reports_a_partial_match_when_the_subject_runs_out()
    {
        // partial (?b): span=(3, 8) value='xfoob' counts=(0, 1, 0) changes=([], [3], [])
        //
        // A partial is a negative status, which the first pass returns straight out rather than
        // treating as a candidate - so this is also the test that the second pass never sees one.
        Match m = new FuzzyRegex("(?b)(foobar){e<=1}").Match("xxxxfoob", partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((3, 8));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    [Test]
    public void Bestmatch_still_prefers_a_complete_match_when_partials_are_permitted()
    {
        // partial full(?b): span=(2, 8) value='foobar' counts=(0, 0, 0)
        Match m = new FuzzyRegex("(?b)(foobar){e<=1}").Match("xxfoobarxx", partial: true);

        m.PartialMatch.Should().BeFalse();
        (m.Index, m.Index + m.Length).Should().Be((2, 8));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void A_best_match_needing_more_errors_than_the_limit_falls_back_to_the_widened_slice()
    {
        // max-errors 12 (?b): span=(0, 24) counts=(0, 12, 0)
        //                     changes=([], [12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23], [])
        //
        // THIS IS THE FALLBACK PATH, and 'RE_MAX_ERRORS' is what reaches it. The best match uses 12
        // errors; 'error_limit' is clamped to RE_MAX_ERRORS = 10 (':17699'), so the second pass's
        // inner climb from 1 to 10 never succeeds, 'best_groups' stays NULL and the else branch at
        // ':17792' re-runs entry 0 inside a slice widened by 'fewest_errors' at each end.
        //
        // The changes then come from 'best_changes_list.lists[0]' and NOT from that re-run
        // (':17831-17834'), which is why they are asserted here: a fallback that forgot to copy them
        // back would still give the right span and the right counts.
        Match m = new FuzzyRegex(@"(?b)(^\d{12}$){i<=12}").MatchAtStart("123456789012" + new string('x', 12));

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 24));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 12, 0));
        m.FuzzyChanges.Insertions.Should().Equal(12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23);
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    [Test]
    public void A_best_match_inside_the_error_limit_takes_the_improved_candidate_instead()
    {
        // fallback2 (?b): span=(0, 11) counts=(0, 8, 0) changes=([], [0, 1, 2, 3, 4, 5, 6, 7], [])
        //
        // The control for the test above: eight errors is inside RE_MAX_ERRORS, so the second pass
        // does succeed, 'best_groups' is set and the widened-slice branch is not reached. The two
        // together are what say the limit is 10 rather than "some number".
        Match m = new FuzzyRegex("(?b)(^123$){s,i,d}").MatchAtStart("xxxxxxxx123");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 11));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 8, 0));
        m.FuzzyChanges.Insertions.Should().Equal(0, 1, 2, 3, 4, 5, 6, 7);
    }

    [Test]
    public void Bestmatch_still_agrees_with_upstream_on_the_issue_470_example()
    {
        // 470 bestmatch: span=(0, 6) value='voixes' counts=(1, 0, 0) changes=([3], [], [])
        //
        // UPSTREAM'S ANSWER, AND THIS PORT'S, AND THE COST RULE SAYS IT SHOULD NOT BE. 'voixes' is
        // one substitution costing 2; 'voicees' at (7, 14) is one insertion costing 1, so the
        // cheaper match is the later one. Making the second pass rank by cost (see
        // 'DoBestFuzzyMatch') cannot help, because 'voicees' never reaches the best list: the FIRST
        // pass holds
        // the next run to FEWER ERRORS than the one it has (':17675'), both are one error, so the
        // search stops at 'voixes'. Reaching it needs a cost BOUND inside 'basic_match', which is
        // what releases up to 2015.09.28 had and the 2015.11.5 issue 165 hang fix removed.
        //
        // This test exists so that change cannot land silently: it is the line that turns red when
        // the bound goes in, and its replacement is the pinned divergence. S42's second sitting.
        Match m = new FuzzyRegex("(?b)(voices){1i+1d+2s<=2}").Match("voixes voicees");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 6));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void A_bestmatch_second_pass_steps_whole_characters_not_code_units()
    {
        // The second pass walks 'start_pos' away from each candidate one CHARACTER at a time
        // (':17776'), and upstream can write that as '+= step' because its indexes are codepoints.
        // A '+= 1' here lands inside a surrogate pair and the match that starts there splits the
        // character - which in a substitution is a lone high surrogate in the OUTPUT.
        //
        // Found by the differential oracle, 2026-09-13, seed 7 row 1773 of the default 2000-row
        // 'fuzzy' wave. The row is kept whole rather than minimised: the shapes it reduces to -
        // dropping '(?fi)', the '\W', the '{e:0}' test or the nesting - all stop reaching the
        // stepped position, so the smaller pattern passes either way and pins nothing. Verified by
        // re-running this test against 'start_pos += step', 2026-09-13.
        //
        // Upstream, measured the same day (tools/probes/upstream-bestmatch.py's sibling run):
        //   (?b)(?fi)(?:(?:b\W){e:0}){1i+2d+1s<=4} over 'B-\U0001D518'
        //     -> [((0, 2), 'B-', (0, 0, 0)), ((2, 2), '', (0, 0, 2))]
        //     subf '<>' count=2 -> '<><>\U0001D518'
        // Those are CODEPOINT spans, and both happen to be below the astral character, so they are
        // also the UTF-16 ones. The port's answer with '+= 1' was '<>\uD835<>' - a lone high
        // surrogate, because the second match started one code unit into the pair.
        const string subject = "B-\U0001D518";
        FuzzyRegex pattern = new(@"(?b)(?fi)(?:(?:b\W){e:0}){1i+2d+1s<=4}");

        (int, int)[] spans = [.. pattern.Matches(subject).Select(static m => (m.Index, m.Index + m.Length))];

        spans.Should().Equal((0, 2), (2, 2));

        // The output the wave actually compared, and the one that showed the lone surrogate.
        pattern.Replace(subject, "<>", 2).Should().Be("<><>\U0001D518");
    }

    [Test]
    public void A_perfect_best_match_reports_no_error_positions()
    {
        // The 'fewest_errors == 0' arm (':17841'), which clears the change list rather than running
        // the second pass at all. Upstream: search('(?b)(cat){e<=1}', 'xxcatxx') is (2, 5), (0,0,0).
        Match m = new FuzzyRegex("(?b)(cat){e<=1}").Match("xxcatxx");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((2, 5));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }
}
