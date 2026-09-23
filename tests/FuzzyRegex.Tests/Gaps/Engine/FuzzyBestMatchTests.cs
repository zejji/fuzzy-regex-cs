using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

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
    public void Bestmatch_answers_the_cheaper_match_where_upstream_answers_the_earlier_one()
    {
        // UPSTREAM ANSWERS (0, 6) 'voixes' AND THIS PORT ANSWERS (7, 14) 'voicees'. Upstream's own
        // open issue 470, and the owner's decision that this port ranks by cost (DECISIONS
        // 2026-09-12). Measured on regex 2026.7.19 by tools/probes/upstream-bestmatch-cost-ranking.py:
        //
        //   470 bestmatch: span=(0, 6) value='voixes' counts=(1, 0, 0) changes=([3], [], [])
        //
        // 'voixes' is one substitution, which this pattern prices at 2; 'voicees' is one insertion,
        // priced at 1. Both are ONE error, so upstream's error-count ranking cannot separate them
        // and takes the earlier. The cost budget in 'Matcher.DoBestFuzzyMatch' walk 0 is what
        // reaches the later, cheaper one - it holds the next run to a lower COST rather than to a
        // lower error count, so 'voicees' is still in play where upstream has already stopped.
        //
        // The row is in the oracle's ExpectedDivergences as 'bestmatch-ranks-by-cost'.
        Match m = new FuzzyRegex("(?b)(voices){1i+1d+2s<=2}").Match("voixes voicees");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((7, 14));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    [Test]
    public void Bestmatch_and_upstream_agree_again_once_the_costs_are_equal()
    {
        // The control for the test above, and what confines the divergence to cost equations: the
        // same pattern and subject with a unit cost equation answers what upstream answers.
        // Measured the same day:
        //
        //   unit bestmatch: span=(0, 6) value='voixes' counts=(1, 0, 0)
        Match m = new FuzzyRegex("(?b)(voices){1i+1d+1s<=2}").Match("voixes voicees");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 6));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void Bestmatch_bounds_the_cost_of_the_whole_match_not_of_one_section()
    {
        // A GROUP CALL ENTERS THE SAME FUZZY SECTION TWICE, and each entry starts from zero counts.
        // The three constraint predicates bound the cost of the section currently open, so each
        // entry passes a budget the two together break - and the cost recorded at 'END_FUZZY' is the
        // accumulated one. Without the whole-match test at 'END_FUZZY' the first walk of
        // 'DoBestFuzzyMatch' never sees a strictly cheaper run, 'start_pos' never advances, and this
        // test HANGS rather than fails. Found by S42's blind review, 2026-09-13.
        //
        // Upstream, regex 2026.7.19, measured the same day:
        //   regex.search(r'(?b)((?:a){1i+2d+1s<=1})(?1)', 'bb')
        //     -> span=(0, 2) fuzzy_counts=(2, 0, 0)
        Match m = new FuzzyRegex("(?b)((?:a){1i+2d+1s<=1})(?1)").Match("bb");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));

        // The unit-cost control, which takes no cost walk at all and always answered.
        Match unit = new FuzzyRegex("(?b)((?:a){1i+1d+1s<=1})(?1)").Match("bb");

        unit.Success.Should().BeTrue();
        unit.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
    }

    [Test]
    public void Bestmatch_ranks_on_the_live_counts_rather_than_the_end_fuzzy_snapshot()
    {
        // 'MatchState.TotalCost' and 'TotalErrors' are snapshots written at 'END_FUZZY', and a match
        // can succeed on a path whose last 'END_FUZZY' belongs to a branch that was backtracked out
        // of - so both can be STALE where 'MatchState.FuzzyCounts', the counts reported to the
        // caller, is live. Here the snapshot says four errors costing 4 and the live counts are one
        // substitution and one deletion costing 2. Ranking on the stale number scores a genuinely
        // cheaper run as equal, 'start_pos' never advances, and this test HANGS rather than fails.
        //
        // Found by S42's blind review of the fix for the group-call hang, 2026-09-13 - the second
        // hang of the same shape and a different cause, which is why 'DoBestFuzzyMatch' now takes
        // both numbers from the live counts whenever it is the one ranking.
        //
        // UPSTREAM ANSWERS (0, 2) WITH ONE SUBSTITUTION AND ONE DELETION, costing 3 + 1 = 4 under
        // this equation; this port answers (0, 1) with two deletions, costing 2. regex 2026.7.19,
        // re-run on 2026.9.10 on 2026-09-23 with the same answer:
        //   regex.search(r'(?b)((?:abc){e<=2,2i+1d+3s<=4}(?1)?)', 'bb')
        //     -> span=(0, 2) fuzzy_counts=(1, 0, 1)
        //
        // Until S87 this port answered (1, 2), also two deletions costing 2. Both spans cost the same
        // and use the same number of errors, so the owner's rule - cheapest, then fewest errors, then
        // earliest - picks (0, 1). The walk that settles the tie was reading a stale error total
        // (ledger entry 32) and lost the earlier span.
        Match m = new FuzzyRegex("(?b)((?:abc){e<=2,2i+1d+3s<=4}(?1)?)").Match("bb");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));

        // The unit-cost control: no cost walk, so upstream's answer, and it never hung.
        Match unit = new FuzzyRegex("(?b)((?:abc){e<=2,1i+1d+1s<=4}(?1)?)").Match("bb");

        unit.Success.Should().BeTrue();
        (unit.Index, unit.Index + unit.Length).Should().Be((0, 2));
        unit.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 1));
    }

    [Test]
    public void Bestmatch_keeps_a_match_that_needs_two_trailing_insertions()
    {
        // A DELIBERATE DIVERGENCE, AND AN INHERITED UPSTREAM BUG FIXED HERE - ledger entry 12, found
        // by S42 while choosing an ExpectedDivergences example row for 'bestmatch-ranks-by-cost'
        // (2026-09-13) and fixed by S46 (2026-09-14). Upstream, regex 2026.9.10:
        //
        //   regex.fullmatch(r'(?b)(?:x){e<=3}', 'xyz')  ->  None
        //   regex.fullmatch(r'(?:x){e<=3}',     'xyz')  ->  (0, 3) counts=(0, 2, 0)
        //
        // '(?b)' is documented as a ranking flag and not as a filter - "By default, fuzzy matching
        // searches for the first match that meets the given constraints ... The BESTMATCH flag will
        // make it search for the best match instead" (upstream/README.rst:592). A flag that turns a
        // match meeting the constraints into NO match contradicts that definition, and the same
        // engine answers the match the moment the flag is deleted. That self-refutation is the whole
        // judgement; no second engine implements fuzzy matching to be asked.
        //
        // The mechanism is two lines meeting. END_FUZZY's backtrack arm is the only place a TRAILING
        // insertion can come from, and it was guarded by 'total_errors(state->fuzzy_counts) +
        // total_errors(inner_counts) < state->max_errors' (:15516) - which DOUBLE-COUNTS, because
        // END_FUZZY has already merged 'inner_counts' into 'state->fuzzy_counts' (:12475-12513), so
        // the two terms are the same errors added twice. Every other 'max_errors' test in upstream's
        // file asks about ONE set of counts ('any_error_permitted' :9672, 'this_error_permitted'
        // :9690, 'insertion_permitted' :9708), and 'insertion_permitted' on the line above already
        // applies the section's own limits to 'inner_counts', so dropping the second term loses
        // nothing.
        //
        // WHY IT IS INVISIBLE OUTSIDE '(?b)' AND '(?e)': plain fuzzy matching runs with 'max_errors'
        // at PY_SSIZE_T_MAX ('do_simple_fuzzy_match' :18027), so the guard never bites and the real
        // limit is the section's own budget. 'do_best_fuzzy_match' is where 'max_errors' becomes
        // finite - the first pass finds the two-insertion match and records 'fewest_errors' as 2,
        // the second pass climbs 'max_errors' only to 2 (:17732) and the widened-slice fallback uses
        // 2 as well (:17823) - and at 2 the doubled guard refuses the second insertion.
        Match m = new FuzzyRegex("(?b)(?:x){e<=3}").FullMatch("xyz");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0));

        // The control, and what says the match is really there to be lost: upstream's own flagless
        // answer, which this port now matches under the flag as well.
        Match plain = new FuzzyRegex("(?:x){e<=3}").FullMatch("xyz");

        plain.Success.Should().BeTrue();
        (plain.Index, plain.Index + plain.Length).Should().Be((0, 3));
        plain.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0));
    }

    [Test]
    public void Bestmatch_keeps_a_match_whose_single_trailing_insertion_is_not_its_only_error()
    {
        // THE SAME DEFECT, AND IT CORRECTS THE TEST ABOVE'S HEADLINE - ledger entry 12 said two
        // trailing insertions were needed; ONE is enough when the fit spends another error too.
        // S52 sitting 16, 2026-09-15. This is the minimised, all-ASCII form of seed 523701539 row
        // 41539 of the eight-seed sweep, whose subject was 'a0' + U+1D518 + '0' + U+1F600 and
        // whose astral characters turn out to have nothing to do with it. Upstream,
        // regex 2026.9.10, block 6 of
        //   python tools/probes/upstream-bestmatch-trailing-insertions.py
        //
        //   regex.fullmatch(r'(?b)(a0)(?:(?:\1)){e<=3}', 'a0x0y')  ->  None
        //   regex.fullmatch(r'(a0)(?:(?:\1)){e<=3}',     'a0x0y')  ->  (0, 5) counts=(1, 1, 0)
        //
        // The same probe's ablations say what the fit needs: over 'a0xy' (two substitutions, no
        // insertion) and 'a0x0' (one substitution) upstream answers under the flag, so it is the
        // trailing insertion that is refused and not the error count. It also says this entry does
        // not yet know its own law - spell the section body as the literal 'a0' instead of the
        // backreference and '(?b)' answers the very same subject, and a width-2 body survives
        // trailing insertions a width-1 body does not.
        //
        // What attributes the row to THIS defect is a port-side control rather than its shape:
        // restoring upstream's doubled term in 'Matcher.cs' - the one line S46 dropped - makes this
        // port refuse this row and the three like it, and none of the other four rows the sweep
        // drew with the same signature. Those four are a different defect.
        //
        // PERMANENT, and judged in this port's favour. Classified as
        // 'bestmatch-loses-a-candidate' in tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs,
        // rows 14 to 17.
        Match m = new FuzzyRegex(@"(?b)(a0)(?:(?:\1)){e<=3}").FullMatch("a0x0y");

        m.Success.Should().BeTrue("upstream answers None here and its own flagless engine does not");
        (m.Index, m.Index + m.Length).Should().Be((0, 5));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(2);
        m.FuzzyChanges.Insertions.Should().Equal(4);

        // The control, and upstream's own answer: with the flag deleted both engines agree.
        Match plain = new FuzzyRegex(@"(a0)(?:(?:\1)){e<=3}").FullMatch("a0x0y");

        plain.Success.Should().BeTrue();
        (plain.Index, plain.Index + plain.Length).Should().Be((0, 5));
        plain.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));

        // And the ablation that says it is the trailing insertion: drop it and upstream answers
        // under the flag as well, at the same two-error cost.
        Match noInsertion = new FuzzyRegex(@"(?b)(a0)(?:(?:\1)){e<=3}").FullMatch("a0xy");

        noInsertion.Success.Should().BeTrue();
        (noInsertion.Index, noInsertion.Index + noInsertion.Length).Should().Be((0, 4));
        noInsertion.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
    }

    [Test]
    public void Bestmatch_reversed_records_the_insertion_where_its_own_flagless_answer_does()
    {
        // THE SAME DEFECT WITHOUT A LOST MATCH - the doubled term moves an error instead of
        // refusing one. S57e, 2026-09-22, row 1982 of
        //   pwsh -File tools/run-oracle.ps1 -Generator fuzzy-anchored -Count 2000 -Seeds 1234567
        // Both engines answer (0, 4) at one substitution and one insertion and both put the
        // substitution at 4. Only the insertion moves. Upstream, regex 2026.9.10:
        //
        //   regex.search(r'(?b)(?r)\m(?:.fo){e<=2}', 'x fx')  ->  (0, 4) counts=(1, 1, 0)
        //                                                          changes=([4], [2], [])
        //   regex.search(r'(?r)\m(?:.fo){e<=2}',     'x fx')  ->  (0, 4) counts=(1, 1, 0)
        //                                                          changes=([4], [1], [])
        //
        // Two alignments cost two errors here, so neither answer is the better match and the
        // question is only which one each engine reaches. What names the cause is one line of
        // source, tested from both sides:
        //   python tools/probes/s57e-double-count-moves-the-insertion.py
        // Deleting upstream's doubled term from END_FUZZY's backtrack arm
        // (upstream/src/_regex.c:15516-15519) moves upstream's insertion to 1 and recovers entry
        // 12's own lost match in the same build; restoring that term in 'Matcher.cs' moves this
        // port's insertion to 2. The flagless answer stays put in both builds, which is what says
        // the guard is doing this and not some difference in alignment order.
        //
        // PERMANENT, and judged in this port's favour: BESTMATCH is documented as a ranking flag
        // over the flagless engine's candidates (upstream/README.rst:592), so an answer the same
        // engine does not give without the flag is upstream contradicting its own definition.
        // Classified as 'bestmatch-loses-a-candidate' in
        // tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs, row 24.
        Match m = new FuzzyRegex(@"(?b)(?r)\m(?:.fo){e<=2}").Match("x fx");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(4);
        // Upstream records 2 here; its own flagless engine records 1, as this port does.
        m.FuzzyChanges.Insertions.Should().Equal(1);

        // The control: with the flag deleted both engines record the insertion at 1.
        Match plain = new FuzzyRegex(@"(?r)\m(?:.fo){e<=2}").Match("x fx");

        plain.Success.Should().BeTrue();
        (plain.Index, plain.Index + plain.Length).Should().Be((0, 4));
        plain.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        plain.FuzzyChanges.Substitutions.Should().Equal(4);
        plain.FuzzyChanges.Insertions.Should().Equal(1);

        // And the ablation that says the reverse match is part of the shape: forwards, the two
        // engines agree on a different match entirely, at two substitutions and no insertion.
        Match forwards = new FuzzyRegex(@"(?b)\m(?:.fo){e<=2}").Match("x fx");

        forwards.Success.Should().BeTrue();
        (forwards.Index, forwards.Index + forwards.Length).Should().Be((0, 3));
        forwards.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
    }

    [Test]
    public void Bestmatch_and_enhancematch_together_keep_the_match_bestmatch_alone_would_lose()
    {
        // THE SAME DEFECT WITH BOTH FLAGS ON, and the ablation that says which flag loses it - which
        // the family's own discriminator, upstream's flagless answer, cannot say. Seed 7 row 76930
        // of the 6000-row three-seed gate, S52 sitting 8. Upstream, regex 2026.9.10, measured
        // 2026-09-15, rows 12 and 13 of
        //   python tools/probes/gate-divergence-doors.py --rows tools/probes/bestmatch-loses-a-candidate-rows.jsonl
        // (the seed form of that probe reads a gate REPORT, and a judged row is no longer in one):
        //
        //   (?b)(?e)(?:[[:alpha:]][[a-f]~~[d-k]]){e<=2}\b   match 'bab_.bB'  ->  None
        //   (?e)     same pattern, (?b) deleted             same subject     ->  (0, 4) i at 2, 3
        //   (?b)     same pattern, (?e) deleted             same subject     ->  None
        //   neither  both deleted                           same subject     ->  (0, 4) i at 2, 3
        //
        // So it is '(?b)' that destroys the match and not the pair, and '(?e)' neither causes nor
        // rescues it. That matters because 'bestmatch-loses-a-candidate' keys on the FLAGLESS
        // answer, which is the same on both middle lines and therefore cannot tell the two apart.
        //
        // It is ledger entry 12's doubled 'END_FUZZY' guard again, reached through a fuzzy section
        // whose two insertions are trailing with respect to the section: one iteration of the group
        // matches 'ba' at no cost, '\b' is false at 2, and the only way to a boundary is to insert
        // through '_' to 4 - two trailing insertions, which is exactly the k >= 2 the guard refuses
        // at every budget.
        //
        // PERMANENT, and judged in this port's favour. Classified as
        // 'bestmatch-loses-a-candidate' in tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs.
        const string pattern = @"(?b)(?e)(?:[[:alpha:]][[a-f]~~[d-k]]){e<=2}\b";
        const string subject = "bab_.bB";
        const FuzzyRegexOptions options = FuzzyRegexOptions.Version1 | FuzzyRegexOptions.Multiline;

        // ANCHORED, because the row's operation is `match`. Unanchored it is not the same question
        // at all: `(?b)` searching answers (1, 4) in codepoints on both engines, having found a
        // cheaper one-insertion match further in. The defect only shows where position 0 is the
        // only start on offer and the two trailing insertions are the only way to a boundary.
        Match both = new FuzzyRegex(pattern, options).MatchAtStart(subject);

        both.Success.Should().BeTrue("upstream answers None here, under (?b) alone");
        (both.Index, both.Length).Should().Be((0, 4));
        both.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0));
        both.FuzzyChanges.Insertions.Should().Equal(2, 3);

        // The two ablations, and note what they are NOT: only the '(?b)'-deleted one is a line
        // upstream agrees with. Upstream answers the match on the two spellings without '(?b)' and
        // None on the two with it, while this port answers it on all four - so the two engines
        // differ on BOTH '(?b)' lines, the drawn one above and the '(?e)'-deleted one here, and
        // agree on both lines without it. That asymmetry is the finding; asserting it on the port's
        // side is what would go red if '(?b)' ever started destroying a match here too.
        foreach (string dropped in new[] { "(?b)", "(?e)" })
        {
            Match m = new FuzzyRegex(pattern.Replace(dropped, "", StringComparison.Ordinal), options).MatchAtStart(
                subject
            );

            m.Success.Should().BeTrue($"without {dropped}");
            (m.Index, m.Length).Should().Be((0, 4));
            m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0));
        }
    }

    [Test]
    public void Bestmatch_admits_trailing_insertions_up_to_the_sections_own_budget()
    {
        // THE BOUNDARY FOR THIS PATTERN, and it corrects ledger entry 12's first statement of the
        // symptom. The entry said n trailing insertions need 'max_errors' above 2n-1, which reads as
        // though a large enough budget buys the match. It does not: under '(?b)' the second pass
        // sets 'max_errors' to 'fewest_errors' = n itself, so on '(?:x)' the doubled guard needs
        // n > 2n-2, false for every n >= 2 AT EVERY BUDGET. Measured on regex 2026.9.10, 2026-09-14,
        // tools/probes/upstream-bestmatch-trailing-insertions.py - re-runnable from the closing notes of S46:
        //
        //   fullmatch (?:x){e<=N} over 'x' + k trailing chars     matches exactly when N >= k
        //   fullmatch (?e)(?:x){e<=N}   same subject              matches exactly when N >= k
        //   fullmatch (?b)(?:x){e<=N}   same subject              matches only for k <= 1, any N
        //
        // So this test is the (?b) row of that matrix, which must now read like the other two.
        //
        // 'k <= 1, any N' IS THIS BODY'S BOUNDARY AND NOT THE DEFECT'S (S52 sitting 16, 2026-09-15).
        // Block 7 of the same probe gives a width-2 body, '(?:xy)', surviving every k it reaches,
        // and .Bestmatch_keeps_a_match_whose_single_trailing_insertion_is_not_its_only_error below
        // loses a fit needing ONE insertion. The shape that bites is open; what attributes a row to
        // this defect is the port-side control named in that test.
        for (int k = 0; k <= 4; ++k)
        {
            string subject = "x" + "yzwvu"[..k];

            for (int budget = 0; budget <= 6; ++budget)
            {
                Match m = new FuzzyRegex($"(?b)(?:x){{e<={budget}}}").FullMatch(subject);

                m.Success.Should().Be(budget >= k, $"(?b)(?:x){{e<={budget}}} over '{subject}'");

                if (m.Success)
                {
                    m.FuzzyCounts.Should().Be(new FuzzyCounts(0, k, 0));
                }
            }
        }
    }

    [Test]
    public void Bestmatch_still_refuses_a_trailing_insertion_the_budget_cannot_afford()
    {
        // The other side of the fix, and what says the guard is still a guard. Dropping the doubled
        // term must not let the arm spend an error the whole match cannot afford: 'max_errors' is a
        // bound on the WHOLE match, so an insertion is permitted only while the merged count is
        // strictly below it.
        //
        // 'k=3 at N=2' is the cell the matrix above shows empty on all three rows, upstream
        // included: the subject needs three insertions and the section permits two.
        new FuzzyRegex("(?b)(?:x){e<=2}")
            .FullMatch("xyzw")
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("(?:x){e<=2}").FullMatch("xyzw").Success.Should().BeFalse();

        // And a second fuzzy section is where the merged count earns the word "whole": each section
        // permits one error, the match needs one from each, and the outer budget is what says two.
        // Upstream answers (0, 4) counts=(0, 2, 0) here, with the flag and without it.
        Match m = new FuzzyRegex("(?b)(?:a){i<=1}(?:b){i<=1}").FullMatch("aXbY");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0));
    }

    [Test]
    public void Bestmatch_terminates_when_an_error_kind_costs_nothing()
    {
        // A zero-priced error kind is the one shape that can drive walk 0's cost budget to -1 with
        // errors still in the match, and the run after that is a PERFECT match whose cost is not
        // strictly lower than the zero already recorded. Without the 'TotalErrors == 0' clause in
        // 'DoBestFuzzyMatch' the walk re-finds it for ever, because 'start_pos' never advances.
        //
        // This test is a TERMINATION test first and an answer second: if the clause goes, it hangs
        // rather than fails.
        //
        // The answer is also a cost divergence, for the same reason as the two tests above. Upstream,
        // regex 2026.7.19 on 2026-09-13: (?b)(?:foo){i<=2,0i+2d+2s<=4} over 'xxfoxo' -> (2, 5) with
        // one substitution, which this equation prices at 2. This port answers (2, 6) 'foxo' with one
        // insertion, which it prices at nothing.
        // THE SUBJECT NEEDS BOTH HALVES, and the first draft of this test had only one. A free
        // insertion gets 'lowestCost' to 0 with an error still in the match, which is what drives the
        // budget to -1; an EXACT match at or after that candidate is what then succeeds under it, at
        // a cost that is not strictly lower than 0. 'xxfoxo' alone gives the first half only - the
        // budget goes to -1 and the next run simply fails, so the walk ends either way and the test
        // passed with the guard deleted. 'xxfoxofoo' has the exact 'foo' as well.
        Match m = new FuzzyRegex("(?b)(?:foo){i<=2,0i+2d+2s<=4}").Match("xxfoxofoo");

        m.Success.Should().BeTrue();
        m.FuzzyCounts.Total.Should().Be(0);

        // The half-subject, kept because it is what makes the paragraph above checkable.
        Match half = new FuzzyRegex("(?b)(?:foo){i<=2,0i+2d+2s<=4}").Match("xxfoxo");

        half.Success.Should().BeTrue();
        (half.Index, half.Index + half.Length).Should().Be((2, 6));
        half.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        half.FuzzyChanges.Insertions.Should().Equal(4);
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

    [Test]
    public void Bestmatch_keeps_a_partial_that_upstreams_own_search_loses_beside_a_skip()
    {
        // UPSTREAM BUG, found by S43's composed `interactions` wave - five rows of a 6000-row
        // three-seed default wave are this family. The judgement needs no second engine, and on
        // THIS shape it takes the strongest form available: upstream contradicts itself on the same
        // compiled pattern, flag still on.
        //
        //   regex.compile(r'(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)').search('ab.', partial=True)
        //     -> None
        //   ...the same object....................................match('ab.', 2, partial=True)
        //     -> (2, 3) partial
        //
        // A search that finds nothing where its own anchored match finds something is wrong however
        // the ranking rule is defined: BESTMATCH chooses among the matches that exist, it does not
        // remove them. THAT FORM DOES NOT HOLD ON ALL FIVE WAVE ROWS - on three of them upstream
        // finds nothing from any door with the flag on, so only the weaker argument below applies
        // to those; see the probe's docstring, which keeps the two apart. Said the weaker way,
        // deleting `(?b)` gives upstream a match it refused with `(?b)` present - and THAT is the
        // answer this port gives:
        //
        //   regex.compile(r'(?:ab){e<=1}(?:\S(*SKIP)\w|\W)').search('ab.', partial=True)
        //     -> (0, 3) partial, counts (0, 0, 0)
        //
        // Four conditions, each necessary on this shape. The BESTMATCH flag, where `(?e)` in its
        // place keeps the match. The fuzzy section. The backtracking verb. And the partial. The
        // faulting function is `do_best_fuzzy_match` at upstream/src/_regex.c line 17584, though the
        // exact line inside it is not pinned, and Phase 6's upstream report owns finishing that.
        // Re-runnable as `python tools/probes/upstream-bestmatch-loses-a-partial.py`.
        Match best = new FuzzyRegex(@"(?b)(?:ab){e<=1}(?:\S(*SKIP)\w|\W)").Match("ab.", partial: true);

        best.Success.Should().BeTrue("upstream's own anchored match finds this and its search does not");
        (best.Index, best.Length).Should().Be((0, 3));
        best.PartialMatch.Should().BeTrue();
        best.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // The same pattern without `(?b)`, which upstream and this port agree on, and which is what
        // makes the answer above upstream's own rather than this port's invention.
        Match plain = new FuzzyRegex(@"(?:ab){e<=1}(?:\S(*SKIP)\w|\W)").Match("ab.", partial: true);

        (plain.Index, plain.Length).Should().Be((0, 3));
        plain.PartialMatch.Should().BeTrue();
    }

    [Test]
    public void Bestmatch_without_the_verb_keeps_its_match_on_both_engines()
    {
        // The negative control for the test above, and the reason the entry names `(*SKIP)` as a
        // condition rather than describing `(?b)` with partial matching in general. Upstream keeps
        // this one:
        //   regex.compile(r'(?b)(?:ab){e<=1}(?:\S\w|\W)').search('ab.', partial=True)
        //     -> (0, 3), NOT partial, counts (0, 0, 0)
        Match m = new FuzzyRegex(@"(?b)(?:ab){e<=1}(?:\S\w|\W)").Match("ab.", partial: true);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.PartialMatch.Should().BeFalse();
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void Bestmatch_looks_past_the_candidate_whose_own_skip_moved_the_slice()
    {
        // INHERITED BUG, FIXED HERE - ledger entry 5's sixth door, and the first one that is neither
        // a scan nor a two-pass partial. `(*SKIP)` assigns `slice_start` mid-attempt
        // (upstream/src/_regex.c:14555) and nothing puts it back, and `do_best_fuzzy_match`'s walk
        // guard at `:17625` - which holds `start_pos` between `state->slice_start` and
        // `state->slice_end` - reads the moved bound on its next turn. `start_pos` is the match this
        // candidate found, so a verb that consumed anything leaves `slice_start` ABOVE it and the
        // walk ends on its first successful candidate.
        //
        // Measured 2026-09-14 on regex 2026.9.10, `python
        // tools/probes/upstream-bestmatch-walk-truncated-by-a-skip.py`; this port answered the same
        // until this slice:
        //
        //   (?b)(?:a(*SKIP)b){e<=1}   'axab'  search    (0, 2) one substitution   <- upstream, and
        //                                                                            this port before
        //   ...the same compiled pattern............... match(2)  (2, 4) NO errors
        //   (*PRUNE) in its place...................... search    (2, 4) NO errors
        //   the verb deleted........................... search    (2, 4) NO errors
        //
        // WHAT JUDGES IT IS THE VERB'S OWN DEFINITION, not a preference between two rankings.
        // `(*SKIP)` sets a skip point: a later attempt must not start BELOW it (pcre2pattern,
        // "Verbs that act after backtracking"). Here the skip point is 1 and the candidate the walk
        // never reaches starts at 2, which the verb permits outright. And `(?b)` promises the match
        // with the fewest errors among those that exist, so a zero-error match its own anchored door
        // finds settles it without a second engine - which is as well, because PCRE2 has no fuzzy
        // matching at all (tools/probes/pcre2-has-no-fuzzy-matching.py).
        //
        // `(*PRUNE)` prunes backtracking exactly as `(*SKIP)` does and moves NO bound, so the last
        // two lines are what make the moved bound the cause rather than the pattern's meaning.
        Match best = new FuzzyRegex("(?b)(?:a(*SKIP)b){e<=1}").Match("axab");

        best.Success.Should().BeTrue();
        (best.Index, best.Index + best.Length).Should().Be((2, 4));
        best.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // The same pattern's own anchored door, which is the self-refutation stated as a test: it
        // found this answer before the fix as well, which is why the search could be called wrong
        // without appealing to upstream at all.
        Match anchored = new FuzzyRegex("(?b)(?:a(*SKIP)b){e<=1}").MatchAtStart("axab", beginning: 2);

        (anchored.Index, anchored.Index + anchored.Length).Should().Be((2, 4));
        anchored.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void Bestmatch_still_lets_a_skip_prune_a_candidates_own_alternatives()
    {
        // THE NEGATIVE CONTROL for the fix above. The restore is once PER CANDIDATE and it restores
        // the SLICE ONLY, so a `(*SKIP)` inside a candidate's attempt must still do what the verb is
        // for - cut the backtracking. This row is one where the pruning decides the answer and the
        // moved bound does not, so a fix that had reached too far would move it and the fix as
        // written must not.
        //
        // Upstream, regex 2026.9.10, measured 2026-09-14 - and the row is upstream's OWN answer, so
        // it is not a divergence and no `ExpectedDivergences` entry classifies it:
        //
        //   (?b)(?:\w(*SKIP)a|a){e<=1}   over 'axab'   (1, 3) no errors
        //   (*PRUNE) in its place .....................  (1, 3) no errors
        //   the verb deleted ..........................  (0, 1) no errors
        //
        // Both verbs agree, so the bound the `(*SKIP)` moves changes nothing here; deleting the verb
        // changes the answer, so the PRUNING is what decides it. The attempt at 0 takes `\w` = 'a',
        // the verb commits, the 'a' it then needs is 'x', and the `|a` alternative it would have
        // backtracked into is cut - so 0 can only answer by substituting, and the walk finds the
        // perfect match at 1. Position 0 is NOT barren, and the doors say so on both engines:
        //
        //   match at 0  (0, 2) one substitution      match at 2  (2, 4) one substitution
        //   match at 1  (1, 3) NO errors             match at 3  (3, 4) one deletion
        //
        // With the verb deleted, 0 answers (0, 1) with no errors and wins on being earliest, which
        // is the whole of the difference this test pins.
        //
        // It also exercises the walk more than once, which the test above does not: the fix is in
        // `DoBestFuzzyMatch` and `DoMatch2` reaches that only for a fuzzy pattern with `BESTMATCH`
        // set (Matcher.cs, the `RegexFlags.BestMatch` arm), so a control without `(?b)` would be
        // routed to `DoSimpleFuzzyMatch` and would pin nothing about this fix.
        Match pruned = new FuzzyRegex(@"(?b)(?:\w(*SKIP)a|a){e<=1}").Match("axab");

        (pruned.Index, pruned.Index + pruned.Length).Should().Be((1, 3));
        pruned.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // The verb deleted, which is what says the pruning and not the ranking is doing the work.
        Match gone = new FuzzyRegex(@"(?b)(?:\wa|a){e<=1}").Match("axab");

        (gone.Index, gone.Index + gone.Length).Should().Be((0, 1));
        gone.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    /// <summary>
    /// The minimisation of seed 7 row 76160, the eighth row of
    /// <c>bestmatch-walk-truncated-by-a-skip</c>: a <c>(*SKIP)</c> in the branch that matched costs
    /// upstream the PERFECT alternative beside it, and this port keeps it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The drawn row is a 100-character pattern whose <c>(*PRUNE)</c> door never opens - 3,092
    /// seconds on 2026-09-21 and then <c>MemoryError</c> - so S57b ran the gate's own comparer over
    /// every one-edit shortening of it, keeping any that still diverged, until nothing smaller did.
    /// What is left is nineteen characters, and every control on it answers in milliseconds.
    /// </para>
    /// <para>
    /// Measured 2026-09-21 on regex 2026.9.10 by
    /// <c>python tools/probes/s57b-bestmatch-walk-row76160.py</c>:
    /// </para>
    /// <code>
    /// as drawn             split ['', 'ß', '', None, '']   search (0, 1) ONE substitution   &lt;- upstream
    /// (*SKIP) -> (*PRUNE)  split ['', None, 'ß', None, '']  search (0, 0) NO errors          &lt;- ours
    /// the verb deleted     the same as (*PRUNE)
    /// no (?b), all three   split ['', 'ß', '', '', '']      search (0, 1) ONE substitution
    /// </code>
    /// The last line is what classifies it. With <c>(?b)</c> gone the three spellings agree, so the
    /// verb's pruning decides nothing; with <c>(?b)</c> present they part company, and the
    /// candidate upstream loses costs no errors at all where the one it keeps costs one.
    /// </remarks>
    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void Bestmatch_keeps_the_perfect_alternative_a_skip_moved_the_slice_past()
    {
        const string pattern = "(?b:(}){e}(*SKIP)|)";
        const string subject = "ß";
        const FuzzyRegexOptions drawn =
            FuzzyRegexOptions.Posix | FuzzyRegexOptions.FullCase | FuzzyRegexOptions.IgnoreCase;

        FuzzyRegex skip = new(pattern, drawn);

        skip.Split(subject).Should().Equal("", null, subject, null, "");

        Match m = skip.Match(subject);

        (m.Index, m.Length).Should().Be((0, 0));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // The control: `(*PRUNE)` prunes the same backtracking and moves no bound, and upstream
        // itself answers this port's split with it.
        new FuzzyRegex(pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal), drawn)
            .Split(subject)
            .Should()
            .Equal("", null, subject, null, "");

        // And the drawn row, which this port answers the verb-free way where upstream spends a
        // substitution: upstream's own verb-deleted spelling gives these four parts.
        FuzzyRegex drawnRow = new(
            @"(?b)(?:(?:[^\p{L}]{0,1}(?:ß){e<=1}){e<=2,s<=1}(*SKIP).|[a-f])"
                + @"(?:(?P<g1>[a\d]*?)([\w\s])[^a-f]){s<=1,i<=1,d<=1}\b",
            drawn
        );

        drawnRow.Split("\rﬀıİAﬀß").Should().Equal("\rﬀıİ", "", "ﬀ", "");
    }

    /// <summary>
    /// Row 74947 of the seed 20260921 gate: a reversed <c>fullmatch</c> where upstream loses a
    /// partial that both of its own ablations hand back, which is
    /// <c>bestmatch-loses-a-partial</c> (ledger entry 13) rather than a family of its own.
    /// </summary>
    /// <remarks>
    /// Spans in CODEPOINTS, because the subject is two astral characters.
    /// <code>
    /// as drawn             None
    /// (?b) deleted         (0, 2) partial, one substitution at 1
    /// (*SKIP) -> (*PRUNE)  (0, 2) partial, one substitution at 1     &lt;- ours, in full
    /// the verb deleted     MemoryError
    /// </code>
    /// The usual third door does not answer here: with the verb deleted the pattern exhausts
    /// memory, which is ledger entry 14's shape - a self-recursive call round a fuzzy section that
    /// can match empty - and says nothing about this row either way. The other two are enough. A
    /// flag documented to pick the BEST match cannot empty the set of matches, and <c>(*PRUNE)</c>
    /// prunes what <c>(*SKIP)</c> prunes while moving no slice bound, so the difference between
    /// them is about the bound the verb moves (<c>upstream/src/_regex.c:14553</c> reversed).
    /// Measured 2026-09-21 on regex 2026.9.10,
    /// <c>python tools/probes/s57b-row74947-two-doors.py</c>.
    /// </remarks>
    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void Bestmatch_reversed_keeps_the_partial_both_of_upstreams_doors_hand_back()
    {
        const string astral = "\U00010428";
        const string subject = astral + astral;
        string pattern =
            @"(?b)(?r)(\d*)+?(?P<g2>(?:\p{ASCII}"
            + astral
            + @"[[:alpha:]]{0,2}){e<=1}(?&g2)?)(?:(?:A"
            + astral
            + @"(?:A){e<=2,s<=1}){e<=1}(*SKIP)\p{Ll}|[[:alpha:]])";

        // The row's flag word is 8, which is MULTILINE. Nothing in the pattern is anchored, so it
        // decides nothing here; it is what the row asked and so it is what the test asks.
        Match best = new FuzzyRegex(pattern, FuzzyRegexOptions.Multiline).FullMatch(subject, partial: true);

        best.Success.Should().BeTrue("upstream answers this once either the flag or the verb goes");
        best.PartialMatch.Should().BeTrue();

        // (0, 2) and a substitution at 1 in codepoints; the subject is surrogate pairs throughout.
        (best.Index, best.Length)
            .Should()
            .Be((0, 4));
        best.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        best.FuzzyChanges.Substitutions.Should().Equal(2);
        best.Groups[1].Success.Should().BeFalse();
        best.Groups["g2"].Success.Should().BeFalse();

        // The `(*PRUNE)` door, which upstream itself answers the same way.
        Match pruned = new FuzzyRegex(
            pattern.Replace("(*SKIP)", "(*PRUNE)", StringComparison.Ordinal),
            FuzzyRegexOptions.Multiline
        ).FullMatch(subject, partial: true);

        (pruned.Index, pruned.Length).Should().Be((0, 4));
        pruned.PartialMatch.Should().BeTrue();
        pruned.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void Bestmatch_finds_the_two_error_match_upstream_settles_for_three_errors_over()
    {
        // THE SAME DEFECT KEEPING A WORSE MATCH - upstream does not lose this one, it answers a
        // costlier one. S57e, 2026-09-22, row 128947 of the 6000-row gate at seed 20260922, drawn
        // the moment 'fuzzy-anchored' joined the default generator list. Upstream, regex 2026.9.10:
        //
        //   regex.fullmatch(r'(?b)(?e)\b(?:\d+\d\s){e<=3}', '215x b')
        //       -> (0, 6) counts=(3, 0, 0) changes=([3, 4, 5], [], [])
        //
        // This port answers the same span for two errors: substitute the 'x' at 3 for the space the
        // pattern wants, then insert the trailing 'b' the section has no element for. Three errors
        // against two, under a flag upstream's own README calls a search for the best match
        // (upstream/README.rst:592), so upstream is failing its own rule.
        //
        // The flagless answer judges nothing here - it is upstream's flagged answer exactly
        // ((3, 0, 0) again), because a first-match engine returns what it reaches first and never
        // ranks. What names the cause is the guard, measured from both sides:
        //   python tools/probes/s57e-double-count-moves-the-insertion.py
        // Build upstream with the doubled term deleted from END_FUZZY's backtrack arm
        // (upstream/src/_regex.c:15516-15519) and upstream answers (0, 6) at one substitution and
        // one insertion, which is this port's answer; restore that term in 'Matcher.cs' and this
        // port answers upstream's (0, 6) at three substitutions. The trailing insertion is the one
        // the doubled term refuses, and without it three substitutions is all that fits.
        //
        // PERMANENT, and judged in this port's favour. Classified as
        // 'bestmatch-loses-a-candidate' in tests/FuzzyRegex.OracleTests/ExpectedDivergences.cs,
        // row 27.
        Match m = new FuzzyRegex(@"(?b)(?e)\b(?:\d+\d\s){e<=3}").FullMatch("215x b");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 6));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(3);
        m.FuzzyChanges.Insertions.Should().Equal(5);

        // The row carries both ranking flags, so here is which one is needed: either alone reaches
        // the two-error match in this port, and upstream reaches it under neither.
        foreach (string pattern in new[] { @"(?b)\b(?:\d+\d\s){e<=3}", @"(?e)\b(?:\d+\d\s){e<=3}" })
        {
            Match one = new FuzzyRegex(pattern).FullMatch("215x b");

            one.Success.Should().BeTrue();
            (one.Index, one.Index + one.Length).Should().Be((0, 6));
            one.FuzzyCounts.Should().Be(new FuzzyCounts(1, 1, 0), "upstream answers (3, 0, 0)");
        }

        // And with no ranking flag at all the two engines agree, which is the point of the defect
        // only biting where a budget is finite: nothing is ranking, so nothing is lost.
        Match plain = new FuzzyRegex(@"\b(?:\d+\d\s){e<=3}").FullMatch("215x b");

        plain.Success.Should().BeTrue();
        (plain.Index, plain.Index + plain.Length).Should().Be((0, 6));
        plain.FuzzyCounts.Should().Be(new FuzzyCounts(3, 0, 0));
        plain.FuzzyChanges.Substitutions.Should().Equal(3, 4, 5);
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    //
    // Row 5787 of a 6000-row `fuzzy-anchored` wave at seed 8675309, drawn by S57e's own negative
    // control. Upstream answers no match; upstream's own run without `(?b)` answers the span and the
    // counts below, so the recorder files its `bestmatch-no-worse` self-contradiction on the row.
    // The cause is ledger entry 12's doubled backtrack guard: with that term restored in
    // `Matcher.cs` the wave's one divergence goes away because this port stops finding the match
    // too. Classified as `bestmatch-loses-a-candidate`, row 28.
    [Test]
    public void Bestmatch_keeps_the_folded_match_its_own_flagless_run_finds()
    {
        // The substitution is the zero-width joiner at 1. The two insertions are the second capital
        // 'S' at 6, one 's' more than the 'ss' that folds to 'ß' needs, and the astral digit at 8,
        // whose surrogate pair is why this port's length is 10 where upstream counts 9 codepoints.
        Match m = new FuzzyRegex(@"(?b)(?e)(?fi)\b(?:straße){e<=3:\w}").FullMatch("s\u200DRaSsSe\U0001D7EE");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 10));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 2, 0), "upstream answers no match at all");
        m.FuzzyChanges.Substitutions.Should().Equal(1);
        m.FuzzyChanges.Insertions.Should().Equal(6, 8);
    }

    // UPSTREAM HANGS ON BOTH (?b) PATTERNS BELOW; the port's answer is the zero-error match the
    // `|2` branch gives, which upstream itself gives once the (?b) is removed. regex 2026.9.10 on 2026-09-23 (tools/probes/s87-stale-total-errors.py, 5 s limit per call):
    //   search('(?b)(?:(?:a(?:x+?){s<=1}){e<=2}|2)', '2y')                        -> killed at 5 s
    //   search('(?b)(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)', '2\n')          -> killed at 5 s
    //   search('(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)', '2\n')              -> (0, 1) (0, 0, 0)
    //
    // The cause is a stale error total. The fuzzy section's end sets the running total to two
    // errors, finds that over budget and backtracks without putting the old total back. The walk
    // then reads two errors off a match through `|2` whose counts are (0, 0, 0), scores it as no
    // better than the last one, and re-finds it for ever. Ledger entry 32.
    [Test]
    public void Bestmatch_does_not_read_a_stale_error_total_from_a_rejected_fuzzy_section()
    {
        Match m = new FuzzyRegex(
            @"(?b)(?:(?:a(?:x+?){s<=1}){e<=2}|2)",
            FuzzyRegexOptions.None,
            TimeSpan.FromSeconds(2)
        ).Match("2y");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        Match constrained = new FuzzyRegex(
            @"(?b)(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)",
            FuzzyRegexOptions.None,
            TimeSpan.FromSeconds(2)
        ).Match("2\n");

        constrained.Success.Should().BeTrue();
        (constrained.Index, constrained.Index + constrained.Length).Should().Be((0, 1));
        constrained.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, test pins OUR answer. ENHANCEMATCH reads the same stale
    // total: its second run finds the exact '2' at (0, 1), reads two errors off it, decides the fit
    // has stopped improving and keeps the two-substitution first run. Deleting only the nested
    // `{s<=1}` section, which is what makes the outer section's end reject, gives upstream's own
    // (?e) the exact match. regex 2026.9.10 on 2026-09-23:
    //   search('(?e)(?:(?:a(?:x+?){s<=1}){e<=2}|2)', '2y')                        -> (0, 2) (2, 0, 0)
    //   search('(?e)(?:(?:a(?:x+?)){e<=2}|2)', '2y')                              -> (0, 1) (0, 0, 0)
    //   search('(?e)(?:2|(?:a(?:x+?){s<=1}){e<=2})', '2y')                        -> (0, 1) (0, 0, 0)
    // Ledger entry 32.
    [Test]
    public void Enhancematch_does_not_stop_improving_on_a_stale_error_total()
    {
        Match m = new FuzzyRegex(@"(?e)(?:(?:a(?:x+?){s<=1}){e<=2}|2)").Match("2y");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0), "upstream answers (0, 2) with (2, 0, 0)");
    }

    // The same patterns with no ranking flag, and the constrained form under `(?e)`, never read a
    // stale total, and must not move. regex 2026.9.10 on 2026-09-23:
    //   search('(?:(?:a(?:x+?){s<=1}){e<=2}|2)', '2y')                            -> (0, 2) (2, 0, 0)
    //   search('(?e)(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)', '2\n')          -> (0, 1) (0, 0, 0)
    //   search('(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)', '2\n')              -> (0, 1) (0, 0, 0)
    [Test]
    [Arguments(@"(?:(?:a(?:x+?){s<=1}){e<=2}|2)", "2y", 2, 2)]
    [Arguments(@"(?e)(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)", "2\n", 1, 0)]
    [Arguments(@"(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)", "2\n", 1, 0)]
    public void The_stale_error_total_patterns_keep_their_answers_without_bestmatch(
        string pattern,
        string subject,
        int end,
        int substitutions
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, 0, 0));
    }

    // The snapshot END_FUZZY writes has to agree with the counts once the match is found. Driven
    // through the engine directly because the public answers above do not show the snapshot: the
    // walk's budgets often land on the right match even when it is stale.
    [Test]
    [Arguments(@"(?b)(?:(?:a(?:x+?){s<=1}){e<=2}|2)", "2y")]
    [Arguments(@"(?e)(?:(?:a(?:x+?){s<=1}){e<=2}|2)", "2y")]
    [Arguments(@"(?:(?:a(?:x+?){s<=1}){e<=2}|2)", "2y")]
    [Arguments(@"(?b)(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)", "2\n")]
    [Arguments(@"(?e)(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)", "2\n")]
    [Arguments(@"(?:(?:a(?:x+?){s<=1:\W}){s<=1,i<=1,d<=1}|2)", "2\n")]
    public void The_error_total_agrees_with_the_counts_after_a_match(string pattern, string subject)
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(2));
        MatchState state = MatchState.Create(
            regex.PatternObject,
            subject.AsMemory(),
            0,
            subject.Length,
            overlapped: false,
            partial: false,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );

        int status = Matcher.DoMatch(state, search: true);

        status.Should().Be(1);
        state.TotalErrors.Should().Be(state.FuzzyCounts.Sum());
    }

    // Oracle row 3752, generator `interactions`, seed 20260923. This test is for termination: the
    // port did not finish. Upstream answers ['', '\r\n𝔘𝔘𝔘\rAa'] only because its `(*SKIP)` leaves
    // a stale slice that ends the scan after one match (ledger 5); with `(*PRUNE)` in its place, or
    // the verb deleted, upstream hangs as well. The expected parts are upstream's own scan taken
    // one search at a time: (0, 1), then search(s, 1) gives (3, 4) with no errors, then nothing.
    // Measured on regex 2026.9.10, 2026-09-23, tools/probes/upstream-skip-carried-slice-doors.py.
    [Test]
    public void Bestmatch_split_over_a_rejected_fuzzy_section_finishes()
    {
        var pattern = new FuzzyRegex(
            "(?b)\\b\\K(?:(?:\U0001d7eea(?:[[:alpha:]]+?){s<=1:\\W}){s<=1,i<=1,d<=1}(*SKIP)\\S|\\S)",
            FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.FullCase,
            TimeSpan.FromSeconds(2)
        );

        string?[] parts = pattern.Split("\U0001d7ee\r\n\U0001d518\U0001d518\U0001d518\rAa");

        parts.Should().Equal("", "\r\n", "\U0001d518\U0001d518\rAa");
    }
}
