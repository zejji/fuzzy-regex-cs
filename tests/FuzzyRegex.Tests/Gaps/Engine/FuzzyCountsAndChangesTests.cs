using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The fuzzy counts and the change list describe the same errors, across every construct that
/// abandons a sub-attempt without backtracking through it - ledger entry 11's mechanisms C and D,
/// and ledger entry 9's remaining port-side count bug.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect class, in one sentence.</b> Upstream saves and restores the fuzzy COUNTS as a block
/// (<c>push_fuzzy_counts</c>/<c>pop_fuzzy_counts</c>, <c>:2635</c>/<c>:2652</c>) and unwinds the
/// CHANGES one item at a time (<c>record_fuzzy</c>/<c>unrecord_fuzzy</c>, <c>:9768</c>/<c>:9801</c>),
/// so anything that throws a sub-attempt's backtracking away wholesale - an atomic group, a
/// lookaround, a conditional - puts the counts back and leaves the changes standing. Ledger entry 11
/// calls that "one defect class with at least four mechanisms"; S47 fixed A and B, this file is C
/// and D.
/// </para>
/// <para>
/// <b>Every assertion here is an invariant of this port's own answer, not a comparison with
/// upstream.</b> Both engines agree on C and D, so no oracle wave can see either: a bug both
/// engines share reports as agreement. What can see it is the pair of attributes contradicting each
/// other, which is what
/// <c>OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts</c> asserts over a
/// whole wave and what these rows assert one at a time.
/// </para>
/// <para>
/// The three rows come out of the ledger with their flag bits, and <b>none of them reproduces
/// without them</b> - each was drawn by a wave at the flags written beside it.
/// </para>
/// </remarks>
public sealed class FuzzyCountsAndChangesTests
{
    /// <summary>
    /// Mechanism C, the ledger's worst measured case: a POSIX, BESTMATCH-free overlapped scan whose
    /// second match counts a DELETION and reports a SUBSTITUTION.
    /// </summary>
    /// <remarks>
    /// Measured on the committed engine, 2026-09-14, before the fix: match 2 is <c>(4, 6)</c> with
    /// counts <c>(0, 0, 1)</c> against changes <c>sub[4]</c>. The totals agree and the KINDS do not,
    /// which is the same desynchronisation the ledger wrote down as a fourteen-entry list against
    /// counts of <c>(0, 0, 1)</c> - the row has moved since it was recorded, the contradiction has
    /// not.
    /// </remarks>
    [Test]
    public void A_posix_overlapped_scan_reports_changes_of_the_kinds_it_counted()
    {
        // Flags 258 (0x102): VERSION1 | IGNORECASE.
        const string pattern =
            @"(?iV1)(?r)(?p)(?!(?:[^[\p{L}--[a-z]]]\w([\p{L}||\p{N}])){s<=1})(?:([a]+?)(?P<g3>\p{L})){1i+2d+1s<=3:[^a-z]}";

        Match[] matches = [.. new FuzzyRegex(pattern).Matches("ﬃﬃ\U00010400\U00010400\U00010400", overlapped: true)];

        matches.Should().NotBeEmpty();

        foreach (Match m in matches)
        {
            AssertChangesAgreeWithCounts(m);
        }
    }

    /// <summary>
    /// Mechanism C's twin: a BESTMATCH-and-POSIX overlapped scan whose fifth match counts two errors
    /// against an EMPTY change list.
    /// </summary>
    /// <remarks>
    /// Measured on the committed engine, 2026-09-14, before the fix: match 5 is <c>(0, 4)</c> with
    /// counts <c>(1, 0, 1)</c> and no changes at all, exactly as the ledger records it.
    /// </remarks>
    [Test]
    public void A_bestmatch_posix_overlapped_scan_reports_a_change_for_every_error_it_counted()
    {
        // Flags 16642 (0x4102): FULLCASE | VERSION1 | IGNORECASE.
        const string pattern = @"(?ifV1)(?b)(?r)(?p)(\w)(?:\s(?:([\p{L}\p{N}]{2,})){e<=2,s<=1}){1<=e<=2}";

        Match[] matches = [.. new FuzzyRegex(pattern).Matches("A\rAßß aaa", overlapped: true)];

        matches.Should().NotBeEmpty();

        foreach (Match m in matches)
        {
            AssertChangesAgreeWithCounts(m);
        }
    }

    /// <summary>
    /// Mechanism D: a lookaround under <c>(?e)</c> restores a counts block whose changes were
    /// unwound item-wise, so the match counts a SUBSTITUTION and reports a DELETION.
    /// </summary>
    /// <remarks>
    /// Measured on the committed engine, 2026-09-14, before the fix: <c>(6, 8)</c> with counts
    /// <c>(1, 0, 0)</c> against changes <c>del[7]</c>. Found by
    /// <c>OracleWaveTests.Our_own_change_positions_always_agree_with_our_own_counts</c> at seed 4242
    /// on its first run, which is what that property is for.
    /// </remarks>
    [Test]
    public void A_lookaround_under_enhancematch_reports_changes_of_the_kinds_it_counted()
    {
        // Flags 130 (0x82): ASCII | IGNORECASE.
        const string pattern = "(?ai)(?e)([abz])[a\\d]{0,}?(?<=(?:(\\d?)[A-Z]\U0001F600){s<=1,i<=1,d<=1})\\b";

        Match m = new FuzzyRegex(pattern).Match("\U0001F600\r\n\U0001F600AA");

        m.Success.Should().BeTrue();
        AssertChangesAgreeWithCounts(m);
    }

    /// <summary>
    /// Ledger entry 9's remaining port-side bug: <c>POSIX</c> plus <c>(?e)</c> reports three errors
    /// for a span this port's own flagless answer fits in two.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured on the committed engine, 2026-09-14, before the fix: POSIX gives <c>(0, 5)</c> with
    /// counts <c>(1, 1, 1)</c> where the same pattern without POSIX gives <c>(0, 5)</c> with
    /// <c>(0, 1, 1)</c>. It is self-refuting, because POSIX chooses which SPAN wins and cannot make
    /// a span cost more than this engine can fit it in - and both answers are the same span.
    /// </para>
    /// <para>
    /// Upstream answers <c>(0, 5)</c> with <c>(0, 1, 1)</c> under POSIX, measured 2026-09-14 -
    /// <c>python tools/probes/upstream-fuzzy-counts-and-changes.py</c>, whose last section is this
    /// row; <c>pwsh -File tools/probes/port-fuzzy-counts-and-changes.ps1</c> is the port half. So
    /// this port was the one that was wrong, and it is a port bug rather than a divergence.
    /// </para>
    /// <para>
    /// <b>The mechanism, established by instrumenting the walk rather than by hypothesis.</b>
    /// <c>RestoreBestMatch</c> put back <c>FuzzyCounts</c> and <c>FuzzyChanges</c> and left
    /// <c>TotalErrors</c>/<c>TotalCost</c> holding the LAST candidate's values. The enhanced walk's
    /// second run therefore restored the right counts <c>(0, 1, 1)</c> while reading
    /// <c>TotalErrors == 3</c>, so <c>TotalErrors &gt;= fewestErrors</c> cut the walk and the first
    /// run's three errors stood. Fixed by saving and restoring the two totals beside the counts;
    /// the cost-ranking RULE is untouched, as the slice's own guard requires.
    /// </para>
    /// </remarks>
    [Test]
    public void A_posix_enhancematch_fullmatch_spends_no_more_errors_than_the_same_span_needs()
    {
        const string pattern = @"(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w){s<=1,i<=1,d<=1}";
        const string subject = "+ aBA";

        Match posix = new FuzzyRegex(pattern, FuzzyRegexOptions.Posix).FullMatch(subject);
        Match plain = new FuzzyRegex(pattern).FullMatch(subject);

        posix.Success.Should().BeTrue();
        plain.Success.Should().BeTrue();

        (posix.Index, posix.Length).Should().Be((plain.Index, plain.Length));
        posix.FuzzyCounts.Should().Be(plain.FuzzyCounts);
        posix.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 1));

        AssertChangesAgreeWithCounts(posix);
    }

    /// <summary>
    /// An ATOMIC GROUP's abandoned sub-attempt does not leave its deletion behind - the position
    /// reported is the one the same pattern reports with the backtracking cut removed.
    /// </summary>
    /// <remarks>
    /// Row 74033 of the seed-20260914 6000-row gate, minimised only by dropping the flags it did not
    /// need. <b>Upstream and this port agree on everything except this one position</b>: the same
    /// span, the same counts <c>(2, 1, 1)</c>, the same two substitutions and the same insertion,
    /// and a deletion at codepoint 2 upstream against codepoint 3 here - UTF-16 4, because the
    /// subject's last two characters are astral.
    /// <para>
    /// <b>Upstream's own control is what says which is right</b>, because the two answers are both
    /// self-consistent and nothing on this side of the comparison separates them. Spell the
    /// <c>(?&gt;</c> as <c>(?:</c> - the same body, the same alternatives, the backtracking cut gone
    /// - and upstream moves its deletion to codepoint 3. Measured 2026-09-14 on regex 2026.9.10,
    /// <c>tools/probes/upstream-posix-and-atomic-free-answers.py</c>. The oracle entry
    /// <c>atomic-group-leaks-a-change-position</c> classifies the wave row on that control.
    /// </para>
    /// <para>
    /// S47's <c>leakFreeFuzzy</c> question cannot reach this: it re-asks upstream ANCHORED at the
    /// reported span, which removes an EARLIER attempt's leak, and an atomic group abandons a
    /// sub-attempt inside ONE attempt. The row's recorded <c>leakFreeFuzzy</c> agrees with
    /// upstream's drawn answer, not with this one.
    /// </para>
    /// </remarks>
    [Test]
    public void An_atomic_group_reports_the_deletion_the_cut_free_pattern_reports()
    {
        const string pattern = @"^(?:\p{Ll}\w??[a-f]){1i+2d+1s<=3}(?>(?:\p{Ll}(?:\p{L}){s<=1,i<=1,d<=1}){d<=1})$";
        string subject = "AA" + char.ConvertFromUtf32(0x1D518) + char.ConvertFromUtf32(0x1D518);

        Match m = new FuzzyRegex(pattern).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 6));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(2, 1, 1));
        m.FuzzyChanges.Substitutions.Should().Equal(0, 1);
        m.FuzzyChanges.Insertions.Should().Equal(2);

        // The whole of the divergence, and upstream's own cut-free answer: codepoint 3, which is
        // UTF-16 4 across the first astral character.
        m.FuzzyChanges.Deletions.Should().Equal(4);

        AssertChangesAgreeWithCounts(m);

        // The control itself, run here as well as in the probe: with the cut removed this port does
        // not move, which is what says the position belongs to the match rather than to the group.
        Match cutFree = new FuzzyRegex(pattern.Replace("(?>", "(?:", StringComparison.Ordinal)).Match(subject);

        cutFree.Success.Should().BeTrue();
        cutFree.FuzzyChanges.Deletions.Should().Equal(4);
    }

    /// <summary>
    /// An ATOMIC GROUP's abandoned sub-attempt does not re-KIND the changes the match reports - the
    /// substitution and the two insertions land as the cut-free pattern reports them.
    /// </summary>
    /// <remarks>
    /// Seed 7 row 74510 of the 6000-row gate of 2026-09-15, added by S52 sitting 9 as the second and
    /// stronger row of <c>atomic-group-leaks-a-change-position</c>. Where row 74033 above needs
    /// upstream's control to be judgeable at all - its list is internally consistent and only one
    /// POSITION moves - <b>this row is wrong on upstream's own terms before any control is
    /// applied</b>: upstream counts <c>(1, 2, 0)</c> and then lists TWO substitutions (codepoints 3
    /// and 4) and ONE insertion (3). <c>fuzzy_changes</c> is documented as the positions of the
    /// changes <c>fuzzy_counts</c> counts, so that one answer contradicts itself.
    /// <para>
    /// <b>Provenance of the expected values.</b> They are upstream's own cut-free answer, measured on
    /// regex 2026.9.10 on 2026-09-15 by
    /// <c>python tools/probes/upstream-posix-and-atomic-free-answers.py</c>, whose <c>control</c>
    /// line for this row reads
    /// <c>1 | span=(0, 7) g1=(0, 1) g2=(1, 2) g3=(5, 6) g4=(6, 7) g5=(7, 7) counts=(1, 2, 0)
    /// changes=([5], [3, 4], [])</c> - the same span, the same five groups and the same counts as the
    /// drawn answer, with the list re-kinded. The subject is BMP throughout, so codepoint and UTF-16
    /// positions coincide and no conversion is involved.
    /// </para>
    /// </remarks>
    [Test]
    public void An_atomic_group_reports_the_change_kinds_the_cut_free_pattern_reports()
    {
        // Flags 16394 (0x400A) - IGNORECASE | MULTILINE | FULLCASE, and no version bit, so the
        // ambient default. The row does not reproduce without them.
        const FuzzyRegexOptions options =
            FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Multiline | FuzzyRegexOptions.FullCase;
        const string pattern =
            @"^(?:(\p{Lu})([\w\s])\W){1i+2d+1s<=3}(?>(?:(\s?)(?:([^\d])){s<=1,i<=1,d<=1:\w}){2i+1d+1s<=2})([a\d]{0,})$";
        const string subject = "ﬃﬃ ﬃﬃßß";

        Match m = new FuzzyRegex(pattern, options).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 7));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 2, 0));

        // Upstream's own cut-free answer. Upstream as drawn lists substitutions at 3 and 4 and an
        // insertion at 3, which is two substitutions and one insertion under a count of one and two.
        m.FuzzyChanges.Substitutions.Should().Equal(5);
        m.FuzzyChanges.Insertions.Should().Equal(3, 4);
        m.FuzzyChanges.Deletions.Should().BeEmpty();

        AssertChangesAgreeWithCounts(m);

        // The control itself, run here as well as in the probe: with the cut removed this port does
        // not move, which is what says the kinds belong to the match rather than to the group.
        Match cutFree = new FuzzyRegex(pattern.Replace("(?>", "(?:", StringComparison.Ordinal), options).Match(subject);

        cutFree.Success.Should().BeTrue();
        cutFree.FuzzyChanges.Substitutions.Should().Equal(5);
        cutFree.FuzzyChanges.Insertions.Should().Equal(3, 4);
    }

    /// <summary>
    /// A NEGATIVE LOOKAHEAD's abandoned sub-attempt does not re-kind the changes that follow it: the
    /// match counts two insertions and reports two INSERTION positions.
    /// </summary>
    /// <remarks>
    /// Seed 7 row 74345 of the 6000-row gate of 2026-09-15, and ledger entry 11's mechanism G.
    /// <b>Upstream contradicts itself on one answer</b>: it counts <c>(0, 2, 0)</c> - two insertions
    /// and nothing else - and then lists one SUBSTITUTION (codepoint 2) and one DELETION (1) and no
    /// insertion. The totals agree, two entries for two errors, and the KINDS do not, because
    /// <c>match_fuzzy_changes</c> reports the first <c>sum(fuzzy_counts)</c> entries of the change
    /// stack (<c>upstream/src/_regex.c:20522</c>) without regard to what kind each entry is.
    /// <para>
    /// <b>Provenance of the expected values.</b> The COUNTS are upstream's own, measured on regex
    /// 2026.9.10 on 2026-09-15 by
    /// <c>python tools/probes/upstream-fuzzy-changes-of-the-wrong-kind.py</c>, whose
    /// <c>as drawn</c> line reads <c>span=(0, 3) g1=unset PARTIAL counts=(0, 2, 0)
    /// changes=([2], [], [1])</c> and whose next line reads
    /// <c>counts say 2 ins; list names 1 sub, 1 del</c>. Both engines agree on the counts, so the
    /// edit script is two insertions and two INSERTION positions are the only thing either engine may
    /// report - which is what this test asserts. <b>That the two positions are 2 and 1 is NOT
    /// established by upstream</b> and the oracle entry
    /// <c>fuzzy-changes-of-the-wrong-kind-for-their-own-counts</c> says so: every ablation that would
    /// isolate the leaking construct moves the candidate, so the KIND is settled and the positions
    /// are not. They are asserted here to pin this port's answer against drift, not as upstream's.
    /// </para>
    /// </remarks>
    [Test]
    public void A_negative_lookahead_s_abandoned_attempt_does_not_re_kind_the_changes_that_follow_it()
    {
        // Flags 264 (0x108) - VERSION1 | MULTILINE. The row does not reproduce without them.
        const FuzzyRegexOptions options = FuzzyRegexOptions.Version1 | FuzzyRegexOptions.Multiline;
        const string pattern = @"(?r)(?!(?:(?P<g1>\p{Nd}{0,2})ß){s<=1,i<=1,d<=1:\s})(?:\U00010400\U00010400){e<=2}\b";
        string subject = "ßß" + char.ConvertFromUtf32(0x10400) + "\n";

        Match m = new FuzzyRegex(pattern, options).Match(subject, partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));

        // Upstream's counts, which both engines agree on.
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 2, 0));

        // The whole of the divergence: two insertions, because two insertions is what the agreed
        // counts say. Upstream lists a substitution and a deletion and no insertion.
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
        m.FuzzyChanges.Deletions.Should().BeEmpty();
        m.FuzzyChanges.Insertions.Should().Equal(2, 1);

        AssertChangesAgreeWithCounts(m);
    }

    /// <summary>
    /// A reversed match reports a LOOKAHEAD's substitution where the lookahead tested, not at the
    /// start of the match.
    /// </summary>
    /// <remarks>
    /// The minimised reproducer behind seed-7 gate row 73463, cut to four constructs, a
    /// three-character subject and no flags at all. <b>Upstream contradicts itself here</b>: matched
    /// FORWARD it answers <c>(0, 3)</c> with the substitution at 1 - where <c>[^A]</c> was tested,
    /// one past the leading <c>A</c> - and with <c>(?r)</c> added, which picks the same candidate at
    /// the same span and the same count, it answers 0.
    /// <para>
    /// <b>The condition is a GENERAL REPEAT after the lookahead</b>, which was measured rather than
    /// assumed: with <c>A+</c> made a fixed <c>A</c> and nothing else changed, both of upstream's
    /// directions answer 1. Measured 2026-09-14 on regex 2026.9.10,
    /// <c>tools/probes/upstream-reversed-lookahead-change-position.py</c>, which carries that
    /// control and two more.
    /// </para>
    /// <para>
    /// <b>This port answered upstream's reversed answer until S48b</b>, whose <c>PopFuzzyCounts</c>
    /// truncation moved it onto upstream's own forward answer. That is why the row shows as a new
    /// divergence rather than as a fix: the oracle entry
    /// <c>reversed-lookahead-change-at-the-match-start</c> classifies the wave row, and this test
    /// pins the reproducer so the answer cannot drift back without a red suite.
    /// </para>
    /// </remarks>
    [Test]
    public void A_reversed_lookahead_reports_its_substitution_where_the_lookahead_tested()
    {
        const string pattern = @"A(?=[^A]{e<=1})A+\D";

        Match forward = new FuzzyRegex(pattern).Match("AAA");
        Match backward = new FuzzyRegex("(?r)" + pattern).Match("AAA");

        forward.Success.Should().BeTrue();
        backward.Success.Should().BeTrue();

        (forward.Index, forward.Length).Should().Be((0, 3));
        (backward.Index, backward.Length).Should().Be((0, 3));
        forward.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        backward.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));

        // Upstream's own FORWARD answer, which this port gives in both directions. Upstream reversed
        // answers 0.
        forward.FuzzyChanges.Substitutions.Should().Equal(1);
        backward.FuzzyChanges.Substitutions.Should().Equal(1);

        AssertChangesAgreeWithCounts(forward);
        AssertChangesAgreeWithCounts(backward);

        // The control that establishes the condition: a FIXED count after the lookahead, where the
        // two engines and the two directions all agree on 1.
        Match fixedCount = new FuzzyRegex(@"(?r)A(?=[^A]{e<=1})A\D").Match("AAA");

        fixedCount.Success.Should().BeTrue();
        fixedCount.FuzzyChanges.Substitutions.Should().Equal(1);
    }

    /// <summary>
    /// The invariant both mechanisms break: a match's change list holds exactly as many positions of
    /// each kind as its own counts claim.
    /// </summary>
    /// <param name="m">The match.</param>
    private static void AssertChangesAgreeWithCounts(Match m)
    {
        FuzzyCounts counts = m.FuzzyCounts;
        FuzzyChanges changes = m.FuzzyChanges;

        changes
            .Substitutions.Count.Should()
            .Be(
                counts.Substitutions,
                $"the match at ({m.Index}, {m.Index + m.Length}) counted that many substitutions"
            );
        changes
            .Insertions.Count.Should()
            .Be(counts.Insertions, $"the match at ({m.Index}, {m.Index + m.Length}) counted that many insertions");
        changes
            .Deletions.Count.Should()
            .Be(counts.Deletions, $"the match at ({m.Index}, {m.Index + m.Length}) counted that many deletions");
    }
}
