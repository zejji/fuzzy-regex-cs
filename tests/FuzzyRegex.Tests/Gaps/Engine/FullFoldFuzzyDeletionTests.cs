using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A fuzzy deletion that finishes a full-case-folded <c>STRING_FLD</c> item costs one edit.
/// </summary>
/// <remarks>
/// <para>
/// Full case folding (IgnoreCase under the default Version 1) turns a literal holding a foldable
/// pair such as fi or st into a <c>STRING_FLD</c> item. When a pattern letter fails there, the next
/// subject character's folding is already loaded. If a deletion then finishes the item, upstream's
/// leftovers code (<c>upstream/src/_regex.c:14856</c> and <c>:14874</c>) reads that loaded but
/// untouched folding as a half-matched character and charges an extra edit or backtracks. The port
/// charges it only when the folding is part-consumed: a deliberate difference, recorded in
/// <c>docs/DIVERGENCES.md</c> and ledger entry 28, and the same holds for a full-folded
/// backreference (<c>:14154</c>).
/// </para>
/// <para>
/// Upstream under V1 is the buggy engine, so every expected value below comes from <c>regex</c>
/// 2026.9.10 under <c>regex.I | regex.V0</c>, which never builds a <c>STRING_FLD</c> item, measured
/// on 2026-09-22 and quoted beside its assertion with V1's answer for contrast. Each was also
/// checked by counting edits.
/// </para>
/// </remarks>
public sealed class FullFoldFuzzyDeletionTests
{
    [Test]
    public void A_deletion_that_finishes_a_fold_item_costs_one_edit()
    {
        // V0 search('(?:fi){d<=1}', 'fe', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1: None
        Match m = new FuzzyRegex("(?i)(?:fi){d<=1}").Match("fe");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    public void A_deletion_inside_a_longer_fold_item_costs_one_edit()
    {
        // V0 search('(?:fie){d<=1}', 'fe', I): span=(0, 2) counts=(0, 0, 1) changes=([], [], [1])
        // V1: None
        Match fie = new FuzzyRegex("(?fi)(?:fie){d<=1}").Match("fe");
        fie.Success.Should().BeTrue();
        (fie.Index, fie.Length).Should().Be((0, 2));
        fie.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));

        // V0 search('(?:sta){d<=1}', 'sa', I): span=(0, 2) counts=(0, 0, 1) changes=([], [], [1])
        // V1: None
        Match sta = new FuzzyRegex("(?fi)(?:sta){d<=1}").Match("sa");
        sta.Success.Should().BeTrue();
        (sta.Index, sta.Length).Should().Be((0, 2));
        sta.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    [Test]
    public void A_reversed_fold_item_charges_a_deletion_once()
    {
        // V0 search('(?r)(?:fi){d<=1}', 'ei', I): span=(1, 2) counts=(0, 0, 1) changes=([], [], [1])
        // V1: None
        Match m = new FuzzyRegex("(?rfi)(?:fi){d<=1}").Match("ei");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((1, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    [Test]
    public void The_earliest_start_within_the_budget_is_found()
    {
        // One insertion (the space at 1) and one deletion (the i) from index 1.
        // V0 search('(?:a fie){e<=2}', 'x a fe', I): span=(1, 6) counts=(0, 1, 1) changes=([], [1], [5])
        // V1: span=(2, 6) counts=(1, 0, 1)
        Match m = new FuzzyRegex("(?i)(?:a fie){e<=2}").Match("x a fe");

        (m.Index, m.Length).Should().Be((1, 5));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 1));
        m.FuzzyChanges.Insertions.Should().Equal(1);
        m.FuzzyChanges.Deletions.Should().Equal(5);
    }

    [Test]
    public void Best_and_enhanced_matching_report_the_single_deletion()
    {
        // V0 search('(?b)(?:afie){e<=2}', ' afe', I): span=(1, 4) counts=(0, 0, 1) changes=([], [], [3])
        // V1: span=(1, 4) counts=(1, 0, 1)
        Match best = new FuzzyRegex("(?bi)(?:afie){e<=2}").Match(" afe");
        (best.Index, best.Length).Should().Be((1, 3));
        best.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        best.FuzzyChanges.Deletions.Should().Equal(3);

        // V0 search('(?e)(?:afie){e<=2}', ' afe', I): span=(1, 4) counts=(0, 0, 1) changes=([], [], [3])
        // V1: span=(1, 4) counts=(1, 0, 1)
        Match enhanced = new FuzzyRegex("(?ei)(?:afie){e<=2}").Match(" afe");
        (enhanced.Index, enhanced.Length).Should().Be((1, 3));
        enhanced.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        enhanced.FuzzyChanges.Deletions.Should().Equal(3);
    }

    [Test]
    public void A_phrase_with_two_deletions_matches_within_two_edits()
    {
        // The benchmark rows the defect was found on. Deleting E and T from "copper field studio"
        // gives "copper fild sudio".
        // V0 search(..., 'COPPER FILD SUDIO HARBOUR CANVAS FALCON 1499452310', I):
        //   span=(0, 17) counts=(0, 0, 2) changes=([], [], [9, 14])
        // V1: None
        var regex = new FuzzyRegex("(?i)(?:copper field studio){e<=2}");

        Match first = regex.Match("COPPER FILD SUDIO HARBOUR CANVAS FALCON 1499452310");
        first.Success.Should().BeTrue();
        (first.Index, first.Length).Should().Be((0, 17));
        first.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
        first.FuzzyChanges.Deletions.Should().Equal(9, 14);

        // V0 search(..., 'FALCON FALCON FALCON COPPER FELD STUDIO 59286606', I):
        //   span=(20, 39) counts=(0, 1, 1) changes=([], [20], [29])
        // V1: span=(21, 39) counts=(1, 0, 1)
        Match second = regex.Match("FALCON FALCON FALCON COPPER FELD STUDIO 59286606");
        (second.Index, second.Length).Should().Be((20, 19));
        second.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 1));
    }

    [Test]
    public void A_second_permitted_deletion_is_not_spent()
    {
        // Before the fix both engines gave an empty match at (2, 2) with two deletions.
        // V0 search('(?:fi){d<=2}', 'fe', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1: span=(2, 2) counts=(0, 0, 2)
        Match m = new FuzzyRegex("(?fi)(?:fi){d<=2}").Match("fe");

        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    [Test]
    public void A_fuzzy_backreference_ending_in_a_deletion_matches()
    {
        // REF_GROUP_FLD (:14060) has the same leftovers test, on its final 'goto backtrack' only.
        // V0 search('(fi)(?:\1){d<=1}', 'fife', I): span=(0, 3) counts=(0, 0, 1) changes=([], [], [3])
        // V1: None
        Match forward = new FuzzyRegex(@"(?i)(fi)(?:\1){d<=1}").Match("fife");
        forward.Success.Should().BeTrue();
        (forward.Index, forward.Length).Should().Be((0, 3));
        forward.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        forward.FuzzyChanges.Deletions.Should().Equal(3);

        // V0 search('(?r)(?:\1){d<=1}(fi)', 'eifi', I): span=(1, 4) counts=(0, 0, 1) changes=([], [], [1])
        // V1: None
        Match reverse = new FuzzyRegex(@"(?ri)(?:\1){d<=1}(fi)").Match("eifi");
        reverse.Success.Should().BeTrue();
        (reverse.Index, reverse.Length).Should().Be((1, 3));
        reverse.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        reverse.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    public void A_part_consumed_folding_is_still_charged()
    {
        // Pins the fix must not move: here the folding really is part-consumed, and V0 and V1 agree.
        // search('(?f)(?:fi){e<=1}', 'ﬁe', I): span=(0, 1) counts=(0, 0, 0) in both versions
        Match ligature = new FuzzyRegex("(?fi)(?:fi){e<=1}").Match("ﬁe");
        (ligature.Index, ligature.Length).Should().Be((0, 1));
        ligature.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));

        // search('(?f)(?:sst){e<=1}', '\xdfﬆ', I): span=(0, 2) counts=(0, 1, 0) changes=([], [1], [])
        // in both versions
        Match sharpS = new FuzzyRegex("(?fi)(?:sst){e<=1}").Match("ßﬆ");
        (sharpS.Index, sharpS.Length).Should().Be((0, 2));
        sharpS.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        sharpS.FuzzyChanges.Insertions.Should().Equal(1);
    }
}
