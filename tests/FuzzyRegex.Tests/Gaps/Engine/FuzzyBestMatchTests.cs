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
        //     -> [((0, 2), 'B-', (0, 0, 0)), ((2, 2), '', (0, 0, 2))]   (the D78 skip - see below)
        //     subf '<>' count=2 -> '<><>\U0001D518'
        // Those are CODEPOINT spans, and both happen to be below the astral character, so they are
        // also the UTF-16 ones. The port's answer with '+= 1' was '<>\uD835<>' - a lone high
        // surrogate, because the second match started one code unit into the pair.
        const string subject = "B-\U0001D518";
        FuzzyRegex pattern = new(@"(?b)(?fi)(?:(?:b\W){e:0}){1i+2d+1s<=4}");

        (int, int)[] spans = [.. pattern.Matches(subject).Select(static m => (m.Index, m.Index + m.Length))];

        // D78 adds (4, 4), the empty match at the end that upstream's (?b) scan skips. Upstream on
        // 2026-10-10 (regex 2026.9.10): plain finditer gives (0, 2), (2, 2), (3, 3) in codepoints,
        // and the (?b) overlapped scan ends with the same (2, 2), (3, 3); codepoint 3 is UTF-16 4.
        spans.Should().Equal((0, 2), (2, 2), (4, 4));

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
    public void Bestmatch_without_the_verb_keeps_its_match_on_both_engines()
    {
        // The verb-free twin of a row D109 now refuses, and the reason the entry named `(*SKIP)` as a
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
    public void Without_bestmatch_a_skip_before_a_fuzzy_repeat_keeps_the_first_fit()
    {
        // With no `(?b)` there is no walk (D109 refuses the flag with a verb), and this port answers
        // the plain first match upstream answers, (0, 5) with a substitution at 4 (measured
        // 2026-10-01 on regex 2026.9.10). So the exact fit above is BESTMATCH's doing.
        Match plain = new FuzzyRegex(@"x(*SKIP)(?:ba+){e<=1}").MatchAtStart("xbaabbbx");

        (plain.Index, plain.Index + plain.Length).Should().Be((0, 5));
        plain.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
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

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    //
    // Ledger entry 12's plainest shape yet, found by S89 while minimising STATE finding 3: a fuzzy
    // section with nothing in it, so every character is a trailing insertion. Upstream answers no
    // match to both patterns below; without `(?b)` it answers (0, 3) at three insertions, which
    // is this port's answer. Restoring the doubled term (`PatternObject.DoubleCountTrailingInsertions`)
    // makes this port answer no match too. Recorded 2026-09-23 by `python tools/record-oracle.py
    // --rows tools/probes/s89-bestmatch-rows.jsonl`, rows 4 and 5; classified as
    // `bestmatch-loses-a-candidate`, rows 30 and 31.
    [Test]
    public void Bestmatch_keeps_a_match_that_is_all_trailing_insertions()
    {
        Match empty = new FuzzyRegex("(?b)(?:){e<=3}").FullMatch("znz");

        empty.Success.Should().BeTrue();
        (empty.Index, empty.Index + empty.Length).Should().Be((0, 3));
        empty.FuzzyCounts.Should().Be(new FuzzyCounts(0, 3, 0), "upstream answers no match at all");
        empty.FuzzyChanges.Insertions.Should().Equal(0, 1, 2);

        // The same through a named list whose first phrase is empty, reversed, which is how the
        // oracle first drew it.
        Dictionary<string, IReadOnlyCollection<string>> lists = new(StringComparer.Ordinal)
        {
            ["phrases"] = ["", "amber lantern"],
        };
        Match listed = new FuzzyRegex(@"(?b)(?r)(?:\L<phrases>){e<=3}", FuzzyRegexOptions.None, lists).FullMatch("znz");

        listed.Success.Should().BeTrue();
        (listed.Index, listed.Index + listed.Length).Should().Be((0, 3));
        listed.FuzzyCounts.Should().Be(new FuzzyCounts(0, 3, 0), "upstream answers no match at all");
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    //
    // STATE finding 3's first row. Upstream answers no match under `(?b)(?r)`, and (0, 11) at seven
    // insertions both without `(?b)` and without `(?r)`. This port answers (0, 11) at seven
    // insertions, and answers no match once the doubled term of ledger entry 12 is restored. Recorded
    // 2026-09-23, `tools/probes/s89-bestmatch-rows.jsonl` row 3; `bestmatch-loses-a-candidate` row 29.
    [Test]
    public void Bestmatch_reversed_keeps_the_seven_insertion_match()
    {
        Match m = new FuzzyRegex("(?b)(?e)(?fi)(?r)(?:fine){e<=7}").FullMatch("oelFin becf");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 11));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 7, 0), "upstream answers no match at all");
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    //
    // Reported on 2026-09-28 as a possible new bug in BESTMATCH's retries under fullmatch, and it is
    // ledger entry 12 in the shape of that entry's own reproduction: a one-character body with two
    // trailing insertions. Upstream, measured by tools/probes/bestmatch-fullmatch-trailing-insertions.py:
    //
    //   regex.fullmatch(r'(?b)(?:b){e<=2}', 'bba')       ->  None     ({i<=2} and {e<=9} too)
    //   regex.fullmatch(r'(?:b){e<=2}', 'bba')           ->  (0, 3) counts=(0, 2, 0), insertions [1, 2]
    //   regex.fullmatch(r'(?e)(?:b){e<=2}', 'bba')       ->  the same
    //   regex.match(r'(?b)(?:b){e<=2}$', 'bba')          ->  None, so fullmatch itself is not the cause
    //
    // The door is not the cause: '$' under plain match loses the fit the same way, and so does (?r).
    // What every lost row needs is a second trailing insertion, which only END_FUZZY's backtrack arm
    // can make, and that arm's guard counts the section's errors twice (_regex.c:15516-15517). The
    // control below puts that doubled term back and gets upstream's None.
    [Test]
    public void Bestmatch_keeps_a_one_character_fit_whose_two_trailing_insertions_reach_the_end()
    {
        foreach (string pattern in new[] { "(?b)(?:b){e<=2}", "(?b)(?:b){i<=2}", "(?b)(?r)(?:b){e<=2}" })
        {
            Match m = new FuzzyRegex(pattern).FullMatch("bba");

            m.Success.Should().BeTrue($"{pattern} - upstream answers no match at all");
            (m.Index, m.Index + m.Length).Should().Be((0, 3), pattern);
            m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0), pattern);
        }

        Match anchored = new FuzzyRegex("(?b)(?:b){e<=2}$").MatchAtStart("bba");

        anchored.Success.Should().BeTrue("upstream answers no match at all");
        (anchored.Index, anchored.Index + anchored.Length).Should().Be((0, 3));
        anchored.FuzzyChanges.Insertions.Should().Equal(1, 2);

        // The attribution control: upstream's doubled term restored, and nothing else changed.
        FuzzyRegex doubled = new("(?b)(?:b){e<=2}");
        doubled.PatternObject.DoubleCountTrailingInsertions = true;

        doubled.FullMatch("bba").Success.Should().BeFalse("upstream's guard refuses the second insertion");
        doubled.FullMatch("bb").Success.Should().BeTrue("one trailing insertion survives upstream too");
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    //
    // Known defect D46: ENHANCEMATCH alone shows ledger entry 12's doubled count of trailing
    // insertions, with no `(?b)` in the pattern. The enhance loop tightens `max_errors` to the
    // total minus one (_regex.c:17978), so END_FUZZY's guard (:15516) meets a bounded budget and
    // counts the section's errors twice. Upstream, measured on regex 2026.9.10 on 2026-09-30:
    //
    //   regex.fullmatch(r'(?e)(?:ob+?a){e<=3}', 'bab')      ->  (0, 3) counts=(3, 0, 0), substitutions [0, 1, 2]
    //   regex.fullmatch(r'(?e)(?r)(?:ob+?a){e<=3}', 'aob')  ->  (0, 3) counts=(3, 0, 0), substitutions [3, 2, 1]
    //   regex.fullmatch(r'(?e)(?:ob+?a){e<=2}', 'bab')      ->  (0, 3) counts=(0, 1, 1)
    //
    // The port answers (0, 1, 1) for both {e<=3} rows, which is upstream's own answer under {e<=2}
    // and costs two errors, not three. The control puts the doubled term back and gets (3, 0, 0).
    [Test]
    public void Enhancematch_keeps_the_two_error_fit_upstream_trades_for_three_substitutions()
    {
        foreach (
            (string pattern, string subject) in new[]
            {
                ("(?e)(?:ob+?a){e<=3}", "bab"),
                ("(?e)(?r)(?:ob+?a){e<=3}", "aob"),
            }
        )
        {
            Match m = new FuzzyRegex(pattern).FullMatch(subject);

            m.Success.Should().BeTrue(pattern);
            (m.Index, m.Index + m.Length).Should().Be((0, 3), pattern);
            m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 1), $"{pattern} - upstream answers (3, 0, 0)");

            FuzzyRegex doubled = new(pattern);
            doubled.PatternObject.DoubleCountTrailingInsertions = true;

            doubled
                .FullMatch(subject)
                .FuzzyCounts.Should()
                .Be(new FuzzyCounts(3, 0, 0), $"{pattern} - upstream's doubled guard");
        }
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    //
    // STATE finding 4, seed 20260923 row 3821 of a 2000-row `fuzzy-literal,fuzzy-anchored` wave.
    // Upstream's `subf` gives '<><>😀': its first match is (0, 2) at a substitution and a deletion,
    // because the doubled term refuses the trailing insertion the (0, 3) fit needs. That fit costs
    // the same two errors (an insertion and a deletion) and covers the whole first run, so without
    // it the second emoji is left over. Without `(?b)` upstream gives '<><>', which is this port's answer,
    // and restoring the doubled term makes this port give '<><>😀'. Recorded 2026-09-23,
    // `tools/probes/s89-bestmatch-rows.jsonl` row 6; `bestmatch-loses-a-candidate` row 32.
    [Test]
    public void Bestmatch_reversed_replaces_the_whole_emoji_run()
    {
        string result = new FuzzyRegex(@"(?b)(?r)\m(?:😀\d😀){e:[a-z]}").ReplaceFormat("b😀😀", "<>");

        result.Should().Be("<><>", "upstream gives '<><>😀'");
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

    // S42-2G, tools/controls.json: the whole-match cost bound on END_FUZZY's trailing-insertion
    // retry ('Matcher.cs', the third test beside the 'ponytail:' note). Found by a blind review's
    // 686-candidate sweep of the '(?1)$' self-recursive-call family (2026-09-28); this is the
    // smallest of the 56 that fired. Dropping the bound's 'TotalCost(state.FuzzyCounts, innerNode)'
    // term (leaving only the trailing insertion's own cost) lets the retry re-enter the section on
    // every walk-0 step without ever spending down the budget, and the match times out rather than
    // answering wrong - so this test is the control's whole signal; the ratchet's normal answer
    // never sees a wrong value to assert against. Upstream, regex 2026.9.10, checked 2026-09-28:
    // search('(?b)((?:a){1s+2i<=2})(?1)$', 'bab') -> (1, 3) counts=(1, 0, 0).
    [Test]
    public void Bestmatch_bounds_the_whole_match_cost_of_a_trailing_insertion()
    {
        Match m = new FuzzyRegex("(?b)((?:a){1s+2i<=2})(?1)$", FuzzyRegexOptions.None, TimeSpan.FromSeconds(5)).Match(
            "bab"
        );

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((1, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    // D45's other side: a first match with no errors is perfect, and BESTMATCH and ENHANCEMATCH
    // stop at it. The discarded lookahead body's errors made it look imperfect, the walk went on
    // under a tighter budget, the lookahead body could no longer match, and the empty branch took
    // over at (0, 0). Upstream answers (0, 0) on both rows; without a flag both engines answer
    // (0, 1), which is the control.
    [Test]
    [Arguments("(?e)(?:(?!(?:bc){e<=2})|c)", "cbdbc")]
    [Arguments("(?b)(?:(?!(?:q){e<=1})|c)", "c")]
    public void A_first_match_with_no_errors_is_kept_although_a_discarded_lookaround_spent_some(
        string pattern,
        string subject
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        Match plain = new FuzzyRegex(pattern[4..]).Match(subject);
        (plain.Index, plain.Index + plain.Length).Should().Be((0, 1));
    }

    // D102: the budget BESTMATCH and ENHANCEMATCH hold a run to is a search device, and it must not
    // decide a test. A negative lookaround's body, and a conditional's test, run with the budget
    // lifted, so a body that matches with errors still matches. Before the fix, at 0 in 'x' the
    // lookahead spent one deletion, the budget then had no room for the body's substitution, the
    // body failed, and `(?!a{s<=1})` passed although `a{s<=1}` matches 'x'. Each expected answer
    // is a match of the flagless pattern, ranked by the flag. ENHANCEMATCH refines inside the span
    // of the first match it finds and may end on a later start, as both engines answer
    // `(?e)(?:x(?:bb){d<=2}|(?:c){d<=1}$)` over 'x' with (1, 1). `regex` 2026.9.10 runs away on
    // the BESTMATCH rows here (ledger entry 32) and answers (0, 0), not a match, on the plain
    // ENHANCEMATCH row; on the three ENHANCEMATCH rows with a lookahead in front it stops at the
    // first fit, (0, 1) (.scratch/s1016/d102/NOTES.md).
    [Test]
    [Arguments("(?b)(?=a{d<=1})\\w??(?!a{s<=1})", "x", 0, 1, 0, 0, 1)]
    [Arguments("(?b)(?:b){d<=1}x??(?!a{s<=1})", "xx", 1, 2, 0, 0, 1)]
    [Arguments("(?b)(?r)(?<!a{s<=1})\\w??(?<=a{d<=1})", "xx", 0, 1, 0, 0, 1)]
    [Arguments("(?b)(?:(?:yy){d<=2}^|x(?=a{d<=1})\\w??(?<!a{s<=1}))", "xx", 0, 0, 0, 0, 2)]
    [Arguments("(?b)(?=a{d<=1})(?:x(?:b){d<=1}|(?!a{s<=1}))", "x", 1, 1, 0, 0, 1)]
    [Arguments("(?e)(?:x(?:b){d<=1}|(?!a{s<=1}))", "x", 1, 1, 0, 0, 0)]
    [Arguments("(?e)(?=a{d<=1})(?:x(?:b){d<=1}|(?!a{s<=1}))", "x", 1, 1, 0, 0, 1)]
    [Arguments("(?b)(?=a{d<=1})(?(?!a{s<=1})\\w??|x)", "x", 1, 1, 0, 0, 1)]
    [Arguments("(?b)(?=a{d<=1})(?(?=a{s<=1})x|\\w??)", "x", 1, 1, 0, 0, 1)]
    [Arguments("(?e)(?=a{d<=1})(?(?!a{s<=1})\\w??|x)", "x", 1, 1, 0, 0, 1)]
    [Arguments("(?e)(?=a{d<=1})(?(?=a{s<=1})x|\\w??)", "x", 1, 1, 0, 0, 1)]
    public void The_ranking_budget_does_not_stop_a_test_s_body_from_matching(
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

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length, m.FuzzyCounts)
            .Should()
            .Be((start, end, new FuzzyCounts(substitutions, insertions, deletions)));
    }

    // D102's other half. A conditional's test keeps its errors in the match (D54), and with the
    // budget lifted inside the test nothing held them to it: the next run at the same start took
    // the same one-substitution path, did no better, and the walk never ended. END_CONDITIONAL now
    // holds the test's errors to the budget as END_FUZZY holds a section's. (1, 1) is exact: at 1
    // the test's body cannot match, so the empty no-branch is taken.
    [Test]
    [Arguments("(?b)(?(?=(?:ab){s<=1})x)")]
    [Arguments("(?b)(?(?!(?:ab){s<=1})|x)")]
    public void A_conditional_s_test_is_held_to_the_budget_when_it_ends(string pattern)
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(5)).Match("xb");

        (m.Index, m.Index + m.Length, m.FuzzyCounts).Should().Be((1, 1, new FuzzyCounts(0, 0, 0)));
    }

    // D102 in a partial search's second pass. A test's body that reads the text to come ends
    // blocked: the continuation goes on in the branch with the body closed, and REOPEN opens the
    // body again for its other paths. Each must close or reopen the lifted budget with the body, or
    // the budget stays lifted outside it or is restored inside it (a Debug assertion in
    // MatchState.CloseABoundlessBody and in Matcher.BasicMatch catches either). The first three
    // rows reach the blocked end only, the last four REOPEN as well. The answers are the flagless
    // pattern's, which nothing here should move.
    [Test]
    [Arguments("(?b)(?(?=(?:ab){s<=1})x|y)", "x", 0, 1, 1, 0, 0)]
    [Arguments("(?b)(?(?=a{s<=1}b?)xb|y)", "x", 0, 1, 1, 0, 0)]
    [Arguments("(?b)(?:c){d<=1}(?(?=a{s<=1}b?)xc|y)", "x", 0, 1, 1, 0, 1)]
    [Arguments("(?b)(?(?=a{s<=1}\\w?)z|y)", "x", 1, 1, 0, 0, 0)]
    [Arguments("(?b)(?(?=a{s<=1}b?)z|x)", "x", 1, 1, 0, 0, 0)]
    [Arguments("(?b)(?:c){d<=1}(?(?=a{s<=1}\\w?)z|x)", "x", 1, 1, 0, 0, 0)]
    [Arguments("(?b)(?(?=(?:a\\w?){s<=1})z|x)", "xy", 2, 2, 0, 0, 0)]
    public void A_partial_bestmatch_search_closes_and_reopens_the_budget_with_a_blocked_test(
        string pattern,
        string subject,
        int start,
        int end,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject, partial: true);
        Match plain = new FuzzyRegex(pattern[4..]).Match(subject, partial: true);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length, m.FuzzyCounts)
            .Should()
            .Be((start, end, new FuzzyCounts(substitutions, insertions, deletions)));
        (plain.Index, plain.Index + plain.Length, plain.FuzzyCounts)
            .Should()
            .Be((m.Index, m.Index + m.Length, m.FuzzyCounts));
    }

    // D109 reverses D102's round 2. A conditional's test, like a lookaround, commits to its body's
    // first match, and that match's errors stay in the match (D54). Round 2 let the body go on
    // under the budget for a later match that fitted, so `(?b)(?(?=a{s<=1}|x)x|y)` over 'x'
    // answered (0, 1) exact, but the flagless test at 0 takes `a{s<=1}` with a substitution and
    // never tries 'x', so no flagless path reaches (0, 1) without an error. With a capture in the
    // body the later match was a span the flagless pattern does not match at all (D109's
    // `(?b)(?(?=(a{s<=1}|xy))\1|z)` over 'xy', (0, 2)). Each row now answers what the flagless
    // pattern does, with its substitution, except the last: at 0 the flagless test takes 'b' for
    // `a` with a substitution, and the better match is the exact empty one at 1, where the test
    // fails and the empty no-branch is taken. `regex` 2026.9.10, and main before D109, answer
    // (0, 1) exact on every row.
    [Test]
    [Arguments("(?b)(?(?=a{s<=1}|x)x|y)", "x", 0, 1, 1)]
    [Arguments("(?e)(?(?=a{s<=1}|x)x|y)", "x", 0, 1, 1)]
    [Arguments("(?b)(?(?=(?:ab|xb){s<=1})x|y)", "xb", 0, 1, 1)]
    [Arguments("(?b)(?(?!a{s<=1}|x)y|x)", "x", 0, 1, 1)]
    [Arguments("(?e)(?(?!a{s<=1}|x)y|x)", "x", 0, 1, 1)]
    [Arguments("(?b)(?(?=(?:a|b){s<=1}c)\\w)", "bc", 1, 1, 0)]
    public void A_conditional_s_test_keeps_the_errors_of_the_match_it_commits_to(
        string pattern,
        string subject,
        int start,
        int end,
        int substitutions
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Index + m.Length, m.FuzzyCounts).Should().Be((start, end, new FuzzyCounts(substitutions, 0, 0)));
    }

    // The same rows through fullmatch and a scan.
    [Test]
    public void A_conditional_s_test_keeps_the_errors_of_the_match_it_commits_to_in_every_operation()
    {
        Match full = new FuzzyRegex("(?b)(?(?=a{s<=1}|x)x|y)").FullMatch("x");
        (full.Index, full.Index + full.Length, full.FuzzyCounts).Should().Be((0, 1, new FuzzyCounts(1, 0, 0)));

        Match negative = new FuzzyRegex("(?b)(?(?!a{s<=1}|x)y|x)").FullMatch("x");
        (negative.Index, negative.Index + negative.Length, negative.FuzzyCounts)
            .Should()
            .Be((0, 1, new FuzzyCounts(1, 0, 0)));

        Scan("(?b)(?(?=a{s<=1}|x)x|y)", "xx").Should().Equal("(0,1) 1,0,0 d:", "(1,2) 1,0,0 d:");
    }

    // D102's witness through each operation, with the flagless pattern as the control: (0, 0) is
    // not a match, because `a{s<=1}` matches 'x' at 0.
    [Test]
    public void A_bestmatch_negative_lookahead_whose_body_needs_an_error_fails_in_every_operation()
    {
        const string pattern = "(?b)(?=a{d<=1})\\w??(?!a{s<=1})";
        var regex = new FuzzyRegex(pattern);

        Match matched = regex.MatchAtStart("x");
        (matched.Index, matched.Index + matched.Length).Should().Be((0, 1));

        Match full = regex.FullMatch("x");
        (full.Index, full.Index + full.Length).Should().Be((0, 1));

        Scan(pattern, "xx").Should().Equal("(1,2) 0,0,1 d:1", "(2,2) 0,0,1 d:2");
        Scan(pattern[4..], "xx").Should().Equal("(1,2) 0,0,1 d:1", "(2,2) 0,0,1 d:2");
    }

    // The controls D102's fix must not move: the flagless witness and its ENHANCEMATCH twin, which
    // never tightened the budget before the body ran, and a positive lookahead, whose body's errors
    // belong to the match, so the budget does bound them. `regex` 2026.9.10 answers those three
    // rows the same. The fourth spends no error before the body, so the budget never reached it;
    // `regex` answers (1, 1) there, which is not a match (`a{s<=1}` matches the 'x' before 1).
    [Test]
    [Arguments("(?=a{d<=1})\\w??(?!a{s<=1})", "x", 0, 1, 0, 0, 1)]
    [Arguments("(?e)(?=a{d<=1})\\w??(?!a{s<=1})", "x", 0, 1, 0, 0, 1)]
    [Arguments("(?b)(?=a{d<=1})\\w??(?=a{s<=1})", "x", 0, 0, 1, 0, 1)]
    [Arguments("(?b)(?r)(?<!a{s<=1})\\w??", "x", 0, 1, 0, 0, 0)]
    public void A_test_s_body_is_unchanged_where_the_budget_never_reached_it(
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

        (m.Index, m.Index + m.Length, m.FuzzyCounts)
            .Should()
            .Be((start, end, new FuzzyCounts(substitutions, insertions, deletions)));
    }

    // D102's oracle row (battery run 6, R1): a reversed, POSIX, overlapped BESTMATCH scan whose
    // negative lookbehind's body matches 'AA\n\n' with one \W substitution, so (8, 12), the first
    // match the port reported, is not a match. Every match costs one deletion, so the flag ranks
    // nothing and the answer is the flagless scan's, which `regex` 2026.9.10 gives too.
    [Test]
    public void The_battery_row_with_a_fuzzy_negative_lookbehind_reports_only_matches()
    {
        const string pattern =
            "(?b)(?r)(?p)(?<!(?:([\\p{L}\\p{N}]{3,}?)(?:[^\\d]){s<=1}){s<=1:\\W})[\\w--[0-9]]{1,2}?"
            + "(?<=(?:[[a-z]--[aei]]+[\\w--[0-9]]){d<=1})";
        const FuzzyRegexOptions options =
            FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.Version1;

        string[] spans =
        [
            .. new FuzzyRegex(pattern, options)
                .Matches("\U0001D518\U0001D518AA\n\n\U0001D518\U0001D518\U0001F600", overlapped: true)
                .Select(static m => $"({m.Index},{m.Index + m.Length}) {m.FuzzyCounts.Deletions}"),
        ];

        spans.Should().Equal("(10,12) 1", "(4,6) 1", "(2,5) 1", "(0,4) 1", "(0,2) 1");
    }

    // D78: AFTER AN EMPTY MATCH, A BESTMATCH SCAN EXCLUDES ONLY THE EMPTY MATCH AT THE RESUME
    // POSITION. Upstream's ban on a second empty match (`_regex.c:15164`) compares with the search
    // anchor, and every candidate re-run of `do_best_fuzzy_match` resets the anchor to its own start
    // while keeping `must_advance`, so the ban becomes "no empty match at ANY candidate's start" and
    // the scan skips positions. The expected answers are upstream's own plain and overlapped scans
    // (regex 2026.9.10, 2026-10-10): at equal cost the flag only chooses the fit at each start, so
    // the starts are plain finditer's. Upstream's (?b) answers are quoted beside each for contrast.

    [Test]
    public void A_bestmatch_scan_finds_an_empty_match_at_every_position_after_an_empty_match()
    {
        // Upstream (?b): (0,1), (1,1) d:1, (3,3) d:2 - position 2 skipped and a stale change list.
        // Upstream plain and (?b) overlapped: the five below.
        Scan(@"(?b)(?:\ ){d<=1}", " xyz")
            .Should()
            .Equal("(0,1) 0,0,0 d:", "(1,1) 0,0,1 d:1", "(2,2) 0,0,1 d:2", "(3,3) 0,0,1 d:3", "(4,4) 0,0,1 d:4");
    }

    [Test]
    public void A_bestmatch_scan_of_a_pattern_that_only_matches_empty_finds_every_position()
    {
        // Upstream (?b): (0,0), (2,2) - every other position skipped. Plain and overlapped: four.
        const string pattern = "(?b)(?:ab){d<=2}";
        Scan(pattern, "xyz")
            .Should()
            .Equal("(0,0) 0,0,2 d:0,1", "(1,1) 0,0,2 d:1,2", "(2,2) 0,0,2 d:2,3", "(3,3) 0,0,2 d:3,4");

        // Upstream (?b) findall: ['', '']; plain: four.
        new FuzzyRegex(pattern)
            .Matches("xyz")
            .Select(static m => m.Value)
            .Should()
            .Equal("", "", "", "");
    }

    [Test]
    public void A_reversed_bestmatch_scan_finds_an_empty_match_at_every_position_after_an_empty_match()
    {
        // Upstream (?br): (3,4), (3,3) d:3, (1,1) d:2. Plain (?r) and (?br) overlapped: the five below.
        Scan(@"(?br)(?:\ ){d<=1}", "xyz ")
            .Should()
            .Equal("(3,4) 0,0,0 d:", "(3,3) 0,0,1 d:3", "(2,2) 0,0,1 d:2", "(1,1) 0,0,1 d:1", "(0,0) 0,0,1 d:0");

        // Upstream (?br): (3,3), (1,1). Plain (?r) and overlapped: four.
        Scan("(?br)(?:ab){d<=2}", "xyz")
            .Should()
            .Equal("(3,3) 0,0,2 d:3,4", "(2,2) 0,0,2 d:2,3", "(1,1) 0,0,2 d:1,2", "(0,0) 0,0,2 d:0,1");
    }

    [Test]
    public void A_bestmatch_scan_keeps_the_best_fit_at_each_start_after_an_empty_match()
    {
        // The walk's first run finds (0,0) with three deletions ('abc'); its re-run from the same
        // start finds one deletion ('d'). Upstream (?b): (0,0) d:0 and nothing more. (?b)
        // overlapped: the three below. Plain gives the same starts with three deletions each.
        Scan("(?b)(?:abc|d){d<=3}", "xy").Should().Equal("(0,0) 0,0,1 d:0", "(1,1) 0,0,1 d:1", "(2,2) 0,0,1 d:2");
    }

    [Test]
    public void A_bestmatch_scan_reaches_the_end_after_an_empty_match_beside_an_optional_group()
    {
        // Upstream (?b): (0,0), (1,2), (2,2) - the empty match at 3 lost. Plain and overlapped: four.
        Scan("(?b)(a)?(?:b){d<=1}", "xax")
            .Should()
            .Equal("(0,0) 0,0,1 d:0", "(1,2) 0,0,1 d:2", "(2,2) 0,0,1 d:2", "(3,3) 0,0,1 d:3");
    }

    [Test]
    public void A_bestmatch_scan_reaches_the_end_after_an_empty_match_between_better_ones()
    {
        // Upstream (?b): (1,2), (3,4), (4,4) - the empty match at 5 lost. (?b) overlapped: four.
        Scan("(?b)(?:abc){d<=3}", "xaybz")
            .Should()
            .Equal("(1,2) 0,0,2 d:2,3", "(3,4) 0,0,2 d:3,5", "(4,4) 0,0,3 d:4,5,6", "(5,5) 0,0,3 d:5,6,7");
    }

    [Test]
    public void A_weighted_bestmatch_scan_finds_an_empty_match_at_every_position_after_an_empty_match()
    {
        // The cost walk (walk 0) re-runs candidates too. Upstream (?b) skips position 2, as above.
        Scan(@"(?b)(?:\ ){s<=0,i<=0,1i+2d+1s<=2}", " xyz")
            .Should()
            .Equal("(0,1) 0,0,0 d:", "(1,1) 0,0,1 d:1", "(2,2) 0,0,1 d:2", "(3,3) 0,0,1 d:3", "(4,4) 0,0,1 d:4");
    }

    [Test]
    public void A_partial_bestmatch_scan_finds_every_complete_match_before_the_partial()
    {
        // Upstream (?b) partial: (0,1), (1,1), (3,3), (4,4) partial. Plain partial: the six below.
        Scan(@"(?b)(?:\ ){d<=1}", " xyz", partial: true)
            .Should()
            .Equal(
                "(0,1) 0,0,0 d:",
                "(1,1) 0,0,1 d:1",
                "(2,2) 0,0,1 d:2",
                "(3,3) 0,0,1 d:3",
                "(4,4) 0,0,1 d:4",
                "(4,4) 0,0,0 d: partial"
            );
    }

    [Test]
    public void A_bestmatch_replace_and_split_see_every_empty_match()
    {
        // Upstream (?b): sub '<>xy<>z', split ['', 'xy', 'z']. Plain: the two below.
        FuzzyRegex pattern = new("(?b)(?:ab){d<=2}");

        pattern.Replace("xyz", "<>").Should().Be("<>x<>y<>z<>");
        pattern.Split("xyz").Should().Equal("", "x", "y", "z", "");
    }

    [Test]
    public void A_bestmatch_scan_still_bans_a_second_empty_match_at_the_resume_position()
    {
        // Controls, green before and after D78: the one exclusion that stays. Upstream (?b), plain
        // and overlapped agree on each; the partial form adds the partial at the end, as plain does.
        Scan("(?b)(?:b){e<=1}", "xyz")
            .Should()
            .Equal("(0,1) 1,0,0 d:", "(1,2) 1,0,0 d:", "(2,3) 1,0,0 d:", "(3,3) 0,0,1 d:3");
        Scan("(?b)(?:b){e<=1}", "xyz", partial: true)
            .Should()
            .Equal("(0,1) 1,0,0 d:", "(1,2) 1,0,0 d:", "(2,3) 1,0,0 d:", "(3,3) 0,0,1 d:3", "(3,3) 0,0,0 d: partial");
        Scan("(?b)(?:ab){e<=2}", "xyz").Should().Equal("(0,2) 2,0,0 d:", "(2,3) 1,0,1 d:3", "(3,3) 0,0,2 d:3,4");
        Scan("(?b)(?:cat){e<=1}", "cat dog bat").Should().Equal("(0,3) 0,0,0 d:", "(8,11) 1,0,0 d:");
        Scan("(?b)(?:ab){e<=1}", "ab ab xb").Should().Equal("(0,2) 0,0,0 d:", "(3,5) 0,0,0 d:", "(6,8) 1,0,0 d:");
    }

    [Test]
    public void An_overlapped_bestmatch_scan_has_no_exclusion_to_lose()
    {
        // Controls, green before and after D78: an overlapped scan clears must_advance, so its
        // answers were never short. Upstream (?b) overlapped, each the same as plain overlapped.
        Scan(@"(?b)(?:\ ){d<=1}", " xyz", overlapped: true)
            .Should()
            .Equal("(0,1) 0,0,0 d:", "(1,1) 0,0,1 d:1", "(2,2) 0,0,1 d:2", "(3,3) 0,0,1 d:3", "(4,4) 0,0,1 d:4");
        Scan("(?b)(?:ab){d<=2}", "xyz", overlapped: true)
            .Should()
            .Equal("(0,0) 0,0,2 d:0,1", "(1,1) 0,0,2 d:1,2", "(2,2) 0,0,2 d:2,3", "(3,3) 0,0,2 d:3,4");
        Scan(@"(?br)(?:\ ){d<=1}", "xyz ", overlapped: true)
            .Should()
            .Equal("(3,4) 0,0,0 d:", "(3,3) 0,0,1 d:3", "(2,2) 0,0,1 d:2", "(1,1) 0,0,1 d:1", "(0,0) 0,0,1 d:0");
        Scan("(?br)(?:ab){d<=2}", "xyz", overlapped: true)
            .Should()
            .Equal("(3,3) 0,0,2 d:3,4", "(2,2) 0,0,2 d:2,3", "(1,1) 0,0,2 d:1,2", "(0,0) 0,0,2 d:0,1");
    }

    // D79: \G MEANS WHERE THE MATCHING OPERATION BEGAN, NOT WHERE THE CURRENT CANDIDATE RUN BEGAN.
    // Upstream's \G reads `search_anchor` (`_regex.c:7288`), which `init_match` sets (`:3410`), and
    // every candidate re-run of the BESTMATCH walk calls `init_match` from its own start, so \G
    // holds at each candidate's start and a search that began at 0 returns a match at 1. The
    // expected answers are upstream's own spellings without the bug, measured on regex 2026.9.10 on
    // 2026-10-10: the anchored match from the search start for a leading \G, or the ^ twin where
    // the search start is 0. Upstream's (?b) search answers are quoted for contrast.

    [Test]
    [Arguments(@"(?b)\G(?:abc){e<=2}", "xabc", 0, 0, 4)] // upstream (1, 4) exact; its match (0, 4) i:1
    [Arguments(@"(?be)\G(?:abc){e<=2}", "xabc", 0, 0, 4)] // upstream (1, 4) exact
    [Arguments(@"(?b)\G(?:abc){e<=2}", "zxabc", 1, 1, 5)] // upstream (2, 5) exact; match pos=1 (1, 5)
    [Arguments(@"(?br)(?:abc){e<=2}\G", "abcx", 0, 0, 4)] // upstream (0, 3) exact; (?br) match (0, 4)
    [Arguments(@"(?b)\G(?:abc){i<=1}", "xabc", 0, 0, 4)] // upstream None; the ^ twin (0, 4) i:1
    public void A_bestmatch_search_holds_backslash_G_only_where_the_search_began(
        string pattern,
        string subject,
        int beginning,
        int start,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject, beginning);

        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    [Test]
    public void A_bestmatch_scan_holds_backslash_G_only_where_each_search_began()
    {
        // Upstream (?b): (0,3), (4,7) - the second search began at 3 and \G held at 4.
        Scan(@"(?b)\G(?:abc){e<=2}", "abcxabc").Should().Equal("(0,3) 0,0,0 d:", "(3,7) 0,1,0 d:");

        // Upstream (?b): (0,2) alone, because it also bans the insertion where a search began; the
        // ^ twin of each step (match pos=2, (?br) match endpos=3) gives the second match below.
        Scan(@"(?b)\G(?:ab){e<=1}", "abxab").Should().Equal("(0,2) 0,0,0 d:", "(2,5) 0,1,0 d:");
        Scan(@"(?br)(?:ab){e<=1}\G", "abxab").Should().Equal("(3,5) 0,0,0 d:", "(0,3) 0,1,0 d:");
    }

    [Test]
    public void A_bestmatch_partial_search_holds_backslash_G_only_where_the_search_began()
    {
        // Upstream (?b): the partial P(0, 4) (1, 1, 0). The ^ twin and the anchored partial match
        // both give the full match (0, 4) (0, 1, 1), which the full pass finds first.
        Match m = new FuzzyRegex(@"(?b)\G(?:abcd){e<=2}").Match("xabc", partial: true);

        (m.Index, m.Index + m.Length, m.PartialMatch).Should().Be((0, 4, false));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 1));
    }

    [Test]
    public void A_bestmatch_lookbehind_holds_backslash_G_only_where_the_search_began()
    {
        // Upstream (?b): (2, 3) with a deletion; the ^ twin (?<=^.) gives (1, 3) exact.
        Match m = new FuzzyRegex(@"(?b)(?<=\G.)(?:bcd|bc){e<=1}").Match("abcx");

        (m.Index, m.Index + m.Length).Should().Be((1, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    [Arguments(@"(?b)(?:.??(?1)|z)(?:q){e<=1}(?(DEFINE)(\Ga))")]
    [Arguments(@"(?b)(?:.??(?1)|z)(?:q){e<=1}(?:|(\Ga))")]
    public void A_bestmatch_group_call_holds_backslash_G_only_where_the_search_began(string pattern)
    {
        // Upstream (?b): (1, 3) exact, where (\Ga) is called at the second candidate's start. The
        // ^ twin and upstream's own anchored match at 0 both give (0, 2) with the 'a' at 1
        // substituted for 'q'. These rows pinned the failed-call memo's per-pass reset until D79
        // (FailedCallMemoTests has its new witness).
        Match m = new FuzzyRegex(pattern).Match("zaq");

        (m.Index, m.Index + m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1);
        m.Groups[1].Success.Should().BeFalse();
    }

    [Test]
    public void Backslash_G_controls_that_D79_does_not_move()
    {
        // Green before and after D79. No \G: the walk keeps the exact match at 1.
        Match plain = new FuzzyRegex("(?b)(?:abc){e<=2}").Match("xabc");
        (plain.Index, plain.Index + plain.Length, plain.FuzzyCounts).Should().Be((1, 4, new FuzzyCounts(0, 0, 0)));

        // \G where the exact match already starts at the search start.
        Match atStart = new FuzzyRegex(@"(?b)\G(?:abc){e<=2}").Match("abcx");
        (atStart.Index, atStart.Index + atStart.Length).Should().Be((0, 3));

        // \G as one alternative of a choice still holds only at the start: upstream None.
        new FuzzyRegex(@"(?b)(?:x|\G)(?:abc){e<=1}")
            .Match("yabc")
            .Success.Should()
            .BeFalse();

        // Substitutions alone cannot shift 'abc' onto 'xabc' from 0: upstream None.
        new FuzzyRegex(@"(?b)\G(?:abc){s<=2}")
            .Match("xabc")
            .Success.Should()
            .BeFalse();

        // The insertion rule still reads each candidate's own start (D61 scope cut, ledger 19/20):
        // a \m guard pins the insertion at the candidate the walk restarts at, under both budgets.
        Scan(@"(?b)\m(?:Y){1i+1d+1s<=1}\M", " XY Z").Should().Equal("(1,3) 0,1,0 d:", "(4,5) 1,0,0 d:");
        Scan(@"(?b)\m(?:Y){1i+1d+1s<=2}\M", " XY Z").Should().Equal("(1,3) 0,1,0 d:", "(4,5) 1,0,0 d:");
        Scan(@"(?b)\m(?:Y){i}\M", "XY YX").Should().Equal("(0,2) 0,1,0 d:", "(3,5) 0,1,0 d:");
    }

    [Test]
    public void Bestmatch_breaks_a_tie_by_where_the_attempt_began_not_the_start_a_K_reported()
    {
        // Flag-free: (2,4) s:1, the attempt at 0 through the first alternative. The second pass
        // also re-runs from 1, where 'a' gives (1,4) s:1: the same error count, and an earlier
        // reported start, but a later attempt, so the attempt at 0 keeps its place. Upstream: None.
        Match m = new FuzzyRegex(@"(?b)(?:xa\K|a)(?:By){e<=1}").Match("xaCy");

        (m.Index, m.Index + m.Length).Should().Be((2, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void Bestmatch_with_a_cost_equation_falls_back_without_throwing_when_K_moved_the_start()
    {
        // A single fuzzy section with a cost equation ranks by cost. Anchored at 0 with a \K, this
        // threw ArgumentOutOfRangeException before D86 was fixed (differential row, 2026-10-15).
        // The answer is the flag-free one, (2,2) s:0,1. Upstream 2026.9.10 answers (6,6) with one
        // substitution, which no attempt at 0 can make: 'ab' with one insertion spans at most 3.
        Match m = new FuzzyRegex(@"(?b)(?:ab){2i+2d+1s<=3}\K").MatchAtStart("-Bay\U0001D49Cba");

        (m.Index, m.Index + m.Length).Should().Be((2, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
    }

    [Test]
    public void Bestmatch_scans_keep_K_moved_candidates()
    {
        // Plain scan: (1,3) s:1 and (4,6) s:1, and BESTMATCH finds no better. Upstream: [] both.
        Scan(@"(?b)a\K(?:By){e<=1}", "aCyaBx").Should().Equal("(1,3) 1,0,0 d:", "(4,6) 1,0,0 d:");
        Scan(@"(?b)a\K(?:By){e<=1}", "aCyaBx", overlapped: true).Should().Equal("(1,3) 1,0,0 d:", "(4,6) 1,0,0 d:");

        // The must-advance ban: the first step's best is the substitution at 0, which \K reports
        // as the empty match at 1. Upstream skips it and answers only the one at 2.
        Scan(@"(?b)(?:a){e<=1}\K", "bb").Should().Equal("(1,1) 1,0,0 d:", "(2,2) 1,0,0 d:");
    }

    [Test]
    public void Bestmatch_partial_prefers_the_complete_K_moved_match()
    {
        // Without \K, (?b) partial over 'aCy' answers the complete (0,3) s:1 (upstream agrees), so
        // with \K the answer is the same match reported from 1. Upstream: (3,3) partial, s:0.
        Match m = new FuzzyRegex(@"(?b)a\K(?:By){e<=1}").Match("aCy", 0, 3, partial: true);

        m.PartialMatch.Should().BeFalse();
        (m.Index, m.Index + m.Length).Should().Be((1, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    // D109: D102's fault reached through commitment. An atomic group, a possessive repeat (and \R,
    // which is one), a lookaround and a conditional's test commit to the first match of their body,
    // and a (*PRUNE) or (*SKIP) acts when backtracking reaches it. The budget BESTMATCH holds a run
    // to made the body fail where the flagless engine's first match needs more errors than the
    // budget, so the construct committed to a later match, or a path never reached the verb, and
    // the run returned a span the flagless pattern does not match. In the first row, at 1 the
    // flagless `(?>ab|a)` takes 'ac' with a substitution and the match fails there; with the
    // budget it took 'a' and answered (1, 3) exact. Each expected answer here is the flagless
    // pattern's match with the fewest errors, worked by hand; before the fix the port and `regex`
    // 2026.9.10 gave the span in the comment.
    [Test]
    [Arguments("(?b)(?:(?>ab|a)c){s<=1}", "aacbc", 0, 3, 1, 0, 0)] // was (1, 3) exact
    [Arguments("(?b)(?>(?:c){d<=1}|b)", "ab", 0, 0, 0, 0, 1)] // was (1, 2)
    [Arguments("(?br)(?:(?>b|c)){d<=1}", "xbcaxx", 1, 2, 0, 0, 0)] // was (2, 3)
    [Arguments("(?b)(?:a?+){e<=2}b", "bbbxcc", 0, 2, 1, 0, 0)] // was (0, 1)
    [Arguments("(?b)(?:(?:a|ab)*+c){s<=1}", "abc", 0, 3, 1, 0, 0)] // was (2, 3)
    [Arguments("(?b)(?:\\Rb){e<=2}", "ccabc", 1, 4, 2, 0, 0)] // was (2, 4) 1 substitution
    [Arguments("(?b)(?=(a{s<=1}|xy))\\1", "xy", 0, 1, 1, 0, 0)] // was (0, 2) exact
    [Arguments("(?b)(?(?=(a{s<=1}|xy))\\1|z)", "xy", 0, 1, 1, 0, 0)] // was (0, 2) exact
    public void The_ranking_budget_does_not_change_what_a_construct_commits_to(
        string pattern,
        string subject,
        int start,
        int end,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(5)).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length, m.FuzzyCounts)
            .Should()
            .Be((start, end, new FuzzyCounts(substitutions, insertions, deletions)));
    }

    // D109 over the shapes the scope probe found (.scratch/s1019/d109/probe), BESTMATCH and
    // ENHANCEMATCH, forwards and reversed: whatever the flag answers must be a match of the flagless
    // pattern. Asked twice, as either question alone has a blind spot: the flagless pattern matches
    // the span as a slice (which hides the text around it from a lookaround), or a flagless search
    // of the whole text held to the span by a lookbehind and a lookahead finds it.
    [Test]
    [Arguments("(?e)(?:(?>ab|a)){e<=2}c", "xccac")]
    [Arguments("(?e)(?>(?:ab){s<=1}|c)b", "ccxxcbb")]
    [Arguments("(?er)(?:b(?>ca|c)){1s+2d+2i<=2}", "bcxxx")]
    [Arguments("(?br)(?:ca++){s<=1}", "cxxcac")]
    [Arguments("(?e)(?:a?+){e<=2}b", "bbbxcc")]
    [Arguments("(?er)(?:ca++){s<=1}", "cxxcac")]
    [Arguments("(?e)(?:\\Rb){e<=2}", "ccabc")]
    [Arguments("(?br)(?:c\\R){1s+2d+2i<=2}", "bcbaba")]
    [Arguments("(?er)(?:c\\R){1s+2d+2i<=2}", "bcbaba")]
    [Arguments("(?b)(?:(?=((?:a){d<=1}|b))\\1){d<=1}", "xb")]
    [Arguments("(?b)(?:(?=((?:c){e<=1}|aca))\\1){e<=1}", "acaxbc")]
    public void A_ranked_answer_is_a_match_of_the_flagless_pattern(string pattern, string subject)
    {
        bool reversed = pattern[3] == 'r';
        string body = pattern[(reversed ? 5 : 4)..];
        string flagless = (reversed ? "(?r)" : "") + body;
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(5)).Match(subject);

        m.Success.Should().BeTrue();
        int start = m.Index;
        int end = m.Index + m.Length;
        bool asASlice = new FuzzyRegex(flagless).FullMatch(subject, start, end - start).Success;
        string held =
            (reversed ? "(?r)" : "") + $"(?<=\\A(?s:.{{{start}}}))(?:{body})(?=(?s:.{{{subject.Length - end}}})\\Z)";
        bool heldToTheSpan = new FuzzyRegex(held).Match(subject).Success;

        (asASlice || heldToTheSpan).Should().BeTrue($"{pattern} answered ({start}, {end}) over '{subject}'");
    }

    // D109's verb half, refused (DECISIONS 2026-10-19). A (*PRUNE) or (*SKIP) acts when backtracking
    // reaches it, so the budget the two flags search with decided where it acted: upstream answers
    // `(?b)(?:(?:a){s<=1}(*PRUNE)c|b)` over 'ccab' with (3, 4), which the pattern does not match.
    // Lifting the budget for the whole run fixed the answers but cost up to 110 times the steps on
    // prose and could lose an exact match a (*SKIP) then fired past, so a fuzzy pattern that ranks
    // its matches and holds such a verb is refused when it is compiled. The rows are the D109
    // witnesses and the patterns of the tests the refusal retired (the walk a (*SKIP) truncated,
    // the slice it carried, and an ArgumentOutOfRangeException a (*SKIP) row threw).
    [Test]
    [Arguments("(?b)(?:(?:a){s<=1}(*PRUNE)c|b)", "BESTMATCH", "PRUNE", 18)]
    [Arguments("(?b)(?:(?:a){s<=1}(*SKIP)c|b)", "BESTMATCH", "SKIP", 18)]
    [Arguments("(?e)(?:(?:b){e<=2}(*SKIP)a|a)", "ENHANCEMATCH", "SKIP", 18)]
    [Arguments("(?br)(?:(?:a(*PRUNE)c|b)){s<=1}", "BESTMATCH", "PRUNE", 12)]
    [Arguments("(?b:(}){e}(*SKIP)|)", "BESTMATCH", "SKIP", 10)]
    [Arguments("(?b)(?:a(*SKIP)b){e<=1}", "BESTMATCH", "SKIP", 8)]
    [Arguments("(?b)(?:(?:ba){1s+2d+2i<=2}(*SKIP)b|ac)", "BESTMATCH", "SKIP", 26)]
    [Arguments("(?b)(?e)(?:[^a](*SKIP)[[:digit:]]|\\w)(?:a){e<=1}", "BESTMATCH", "SKIP", 15)]
    [Arguments("(?b)a(*SKIP)\\K(?:By){e<=1}", "BESTMATCH", "SKIP", 5)]
    [Arguments("(?b)(?:ab){e<=1}(*PRUNE)(*SKIP)", "BESTMATCH", "PRUNE", 16)]
    public void A_ranked_fuzzy_pattern_with_a_backtracking_verb_is_refused(
        string pattern,
        string flag,
        string verb,
        int position
    )
    {
        Action compile = () => _ = new FuzzyRegex(pattern);

        FuzzyRegexParseException error = compile.Should().Throw<FuzzyRegexParseException>().Which;
        error
            .Message.Should()
            .Contain(
                $"{flag} cannot rank a fuzzy pattern that holds (*{verb}): (*PRUNE) and (*SKIP) depend on the order "
                    + $"the search tries paths, which {flag} changes; use the pattern without the flag or without the verb"
            );
        error.Offset.Should().Be(position);
    }

    // The refusal's edges: the flag by option, and the patterns it does not reach - no flag, no
    // fuzzy section, a section that allows no error, and (*FAIL), which only fails.
    [Test]
    public void The_verb_refusal_covers_the_option_and_nothing_else()
    {
        Action byOption = static () => _ = new FuzzyRegex(@"(?:ab){e<=1}(*SKIP)c", FuzzyRegexOptions.EnhanceMatch);
        byOption
            .Should()
            .Throw<FuzzyRegexParseException>()
            .Which.Message.Should()
            .Contain(
                "ENHANCEMATCH cannot rank a fuzzy pattern that holds (*SKIP): (*PRUNE) and (*SKIP) depend on the order "
                    + "the search tries paths, which ENHANCEMATCH changes; use the pattern without the flag or without the verb"
            );

        new FuzzyRegex(@"(?:(?:a){s<=1}(*PRUNE)c|b)").Match("ccab").Index.Should().Be(0);
        new FuzzyRegex(@"(?b)(?:a(*PRUNE)c|b)").Match("ccab").Index.Should().Be(3);
        new FuzzyRegex(@"(?b)(?:a{e<=0}(*SKIP)c|b)").Match("ccab").Index.Should().Be(3);
        new FuzzyRegex(@"(?b)(?:(?:a){s<=1}(*F)|b)").Match("ccab").Index.Should().Be(3);
    }

    /// <summary>
    /// A scan as one line per match: span, counts as substitutions, insertions, deletions, the
    /// deletion positions, and "partial" when it is one.
    /// </summary>
    internal static string[] Scan(string pattern, string subject, bool overlapped = false, bool partial = false) =>
        [
            .. new FuzzyRegex(pattern)
                .Matches(subject, overlapped: overlapped, partial: partial)
                .Select(static m =>
                    $"({m.Index},{m.Index + m.Length}) {m.FuzzyCounts.Substitutions},{m.FuzzyCounts.Insertions},"
                    + $"{m.FuzzyCounts.Deletions} d:{string.Join(",", m.FuzzyChanges.Deletions)}"
                    + (m.PartialMatch ? " partial" : "")
                ),
        ];
}
