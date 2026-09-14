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
