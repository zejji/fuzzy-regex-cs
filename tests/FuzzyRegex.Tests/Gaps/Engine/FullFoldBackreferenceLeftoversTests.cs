using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Two repairs to the fuzzy full-case-folded backreference (<c>REF_GROUP_FLD</c> and its reversed
/// twin): a group that runs out half-way through a subject character's folding charges the rest of
/// the folding as an edit, and a retried edit steps past a folding it finished.
/// </summary>
/// <remarks>
/// <para>
/// Under full case folding ß folds to ss, so a group holding s matches the first half of it. The
/// literal <c>STRING_FLD</c> arm offers the other s to the fuzzy machinery
/// (<c>upstream/src/_regex.c:14855</c>); upstream's <c>REF_GROUP_FLD</c> (<c>:14154</c>) and its
/// reversed twin (<c>:14255</c>) backtrack instead, so a match within the budget is lost. Ledger
/// entry 29.
/// </para>
/// <para>
/// When a fuzzy edit is retried, upstream re-enters the arm at the top and skips the two steps its
/// loop body takes after a first try, so a retried edit that used up a folding compares the same
/// character again. <c>STRING_FLD</c> takes the step (<c>:14801</c>). This one needs no ß: any
/// fuzzy backreference under IgnoreCase and V1 is a <c>REF_GROUP_FLD</c> item. Ledger entry 30.
/// </para>
/// <para>
/// Both are deliberate differences, recorded in <c>docs/DIVERGENCES.md</c>. Each expected value
/// comes from <c>regex</c> 2026.9.10, measured on 2026-09-22 and quoted beside its assertion. Where
/// upstream V1 is the buggy engine, the value comes from V0 or from the same pattern with the group
/// text written out, and the comment names which; each was also checked by counting edits. V0 folds
/// ß to itself, so it answers the ß cases only where treating ß as one character costs the same
/// edit, which is why the insertion case below is quoted from a literal instead.
/// </para>
/// </remarks>
public sealed class FullFoldBackreferenceLeftoversTests
{
    [Test]
    public void The_rest_of_a_folding_is_charged_as_a_substitution()
    {
        // V0 search('(s)(?:\1){e<=1}', 'sß', I): span=(0, 2) counts=(1, 0, 0) changes=([1], [], [])
        // The literal form, V1 search('(?:sss){e<=1}', 'ßß', I): span=(0, 2) counts=(1, 0, 0)
        // V1: None
        Match m = new FuzzyRegex(@"(?i)(s)(?:\1){e<=1}").Match("sß");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(1);
    }

    [Test]
    public void The_rest_of_a_folding_can_be_charged_as_an_insertion()
    {
        // The subject folds to sss, and the pattern's ss plus one inserted s spells it. Upstream's
        // literal arm charges the same insertion:
        // V1 search('(?:sss){i<=1}', 'ßß', I): span=(0, 2) counts=(0, 1, 0) changes=([], [1], [])
        // V1 search('(s)(?:\1){i<=1}', 'sß', I): None, the defect. V0 is None too, because there ß
        // is one character that s cannot match. Ledger entry 29.
        Match m = new FuzzyRegex(@"(?i)(s)(?:\1){i<=1}").Match("sß");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Insertions.Should().Equal(1);
    }

    [Test]
    public void A_reversed_backreference_charges_the_rest_of_a_folding()
    {
        // V0 search('(?r)(?:\1){e<=1}(s)', 'ßs', I): span=(0, 2) counts=(1, 0, 0) changes=([1], [], [])
        // V1: None
        Match m = new FuzzyRegex(@"(?ri)(?:\1){e<=1}(s)").Match("ßs");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void A_longer_group_charges_the_rest_of_its_last_folding()
    {
        // V0 search('(as)(?:\1){e<=1}', 'asaß', I): span=(0, 4) counts=(1, 0, 0) changes=([3], [], [])
        // V1: None
        Match m = new FuzzyRegex(@"(?i)(as)(?:\1){e<=1}").Match("asaß");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(3);
    }

    [Test]
    [Arguments(@"(?i)(s)(?:\1){0d+1s<=1}x", "sßy")]
    [Arguments(@"(?ri)x(?:\1){0d+1s<=1}(s)", "yßs")]
    public void A_free_deletion_cannot_charge_the_rest_of_a_folding(string pattern, string subject)
    {
        // Once the group has run out there is no group character left to delete, so a deletion
        // there must not leave the folding as it was; when deletions cost nothing, taking it again
        // and again would never end. The substitution is taken and the x fails, so the retry offers the
        // deletion, and no alignment ends at an x. S85 moved the ':[x]' rows that used to be here
        // to FullFoldDeletionAtFoldingBoundaryTests, where they match before the ß.
        // V1 search(pattern, subject, I): None for both. Upstream's literal arm has the endless
        // loop: V1 search('(?:sss){0d+1s+1i<=1:[x]}', 'ßß', I) raises MemoryError.
        new FuzzyRegex(pattern)
            .Match(subject)
            .Success.Should()
            .BeFalse();
    }

    [Test]
    public void A_deletion_of_nothing_does_not_count_towards_a_minimum()
    {
        // V1 fullmatch('(s)(?:\1){1<=d<=2,s<=1}', 'sß', I): None
        new FuzzyRegex(@"(?i)(s)(?:\1){1<=d<=2,s<=1}")
            .FullMatch("sß")
            .Success.Should()
            .BeFalse();
    }

    [Test]
    public void A_free_deletion_leaves_the_rest_of_a_folding_to_a_substitution()
    {
        // As above without the ':[x]', so the leftover s can be substituted. The literal form,
        // V1 search('(?:sss){0d+1s+1i<=1}', 'ßß', I): span=(0, 2) counts=(1, 0, 0) changes=([1], [], [])
        Match m = new FuzzyRegex(@"(?i)(s)(?:\1){0d+1s+1i<=1}").Match("sß");

        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void Matching_carries_on_after_the_charged_folding()
    {
        // V0 search('(s)(?:\1){e<=1}x', 'sßx', I): span=(0, 3) counts=(1, 0, 0) changes=([1], [], [])
        // V1: None
        Match m = new FuzzyRegex(@"(?i)(s)(?:\1){e<=1}x").Match("sßx");

        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void Best_and_enhanced_matching_find_the_one_edit_match()
    {
        // V0 search('(?b)(s)(?:\1){e<=1}', 'sß', I): span=(0, 2) counts=(1, 0, 0) changes=([1], [], [])
        // V1: None
        Match best = new FuzzyRegex(@"(?bi)(s)(?:\1){e<=1}").Match("sß");
        (best.Index, best.Length).Should().Be((0, 2));
        best.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));

        // V0 search('(?e)(s)(?:\1){e<=1}', 'sß', I): span=(0, 2) counts=(1, 0, 0) changes=([1], [], [])
        // V1: None
        Match enhanced = new FuzzyRegex(@"(?ei)(s)(?:\1){e<=1}").Match("sß");
        (enhanced.Index, enhanced.Length).Should().Be((0, 2));
        enhanced.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void The_literal_arm_already_charges_the_rest_of_a_folding()
    {
        // Pins the literal behaviour the fix copies; the port matched upstream here before S84.
        // V1 search('(?:sss){e<=1}', 'ßß', I): span=(0, 2) counts=(1, 0, 0) changes=([1], [], [])
        Match substituted = new FuzzyRegex("(?i)(?:sss){e<=1}").Match("ßß");
        (substituted.Index, substituted.Length).Should().Be((0, 2));
        substituted.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));

        // V1 search('(?:sss){i<=1}', 'ßß', I): span=(0, 2) counts=(0, 1, 0) changes=([], [1], [])
        Match inserted = new FuzzyRegex("(?i)(?:sss){i<=1}").Match("ßß");
        (inserted.Index, inserted.Length).Should().Be((0, 2));
        inserted.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        inserted.FuzzyChanges.Insertions.Should().Equal(1);
    }

    [Test]
    public void A_retried_insertion_steps_past_the_inserted_character()
    {
        // The substitution x-for-a is tried first and fails at the b; the retry inserts the x.
        // V0 search('(ab)(?:\1){e<=1}', 'abxab', I): span=(0, 5) counts=(0, 1, 0) changes=([], [2], [])
        // V1: None
        Match forward = new FuzzyRegex(@"(?i)(ab)(?:\1){e<=1}").Match("abxab");
        forward.Success.Should().BeTrue();
        (forward.Index, forward.Length).Should().Be((0, 5));
        forward.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        forward.FuzzyChanges.Insertions.Should().Equal(2);

        // V0 search('(ab)(?:\1){e<=1}c', 'abxabc', I): span=(0, 6) counts=(0, 1, 0) changes=([], [2], [])
        // V1: None
        Match followed = new FuzzyRegex(@"(?i)(ab)(?:\1){e<=1}c").Match("abxabc");
        (followed.Index, followed.Length).Should().Be((0, 6));
        followed.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));

        // V0 search('(?r)(?:\1){e<=1}(ab)', 'abxab', I): span=(0, 5) counts=(0, 1, 0) changes=([], [3], [])
        // V1: None
        Match reverse = new FuzzyRegex(@"(?ri)(?:\1){e<=1}(ab)").Match("abxab");
        reverse.Success.Should().BeTrue();
        (reverse.Index, reverse.Length).Should().Be((0, 5));
        reverse.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        reverse.FuzzyChanges.Insertions.Should().Equal(3);
    }

    [Test]
    public void A_retried_deletion_steps_past_the_deleted_group_character()
    {
        // S83's BESTMATCH case. Deleting the second s of ß's folding costs one edit. Upstream finds
        // the two-edit substitution path first, retries it as that deletion, reads ß again and so
        // keeps the two-edit match.
        // The literal form, V1 search('(?b)(?fi)(ßa)(?:ßa){s<=1,i<=1,d<=1}', 'ßasa'): span=(0, 4)
        //   counts=(0, 0, 1) changes=([], [], [3])
        // V1 search('(?b)(?fi)(ßa)(?:\1){d<=1}', 'ßasa'): span=(0, 4) counts=(0, 0, 1) changes=([], [], [3])
        // V1 search('(?b)(?fi)(ßa)(?:\1){s<=1,i<=1,d<=1}', 'ßasa'): span=(0, 4) counts=(1, 0, 1)
        Match best = new FuzzyRegex(@"(?bfi)(ßa)(?:\1){s<=1,i<=1,d<=1}").Match("ßasa");
        (best.Index, best.Length).Should().Be((0, 4));
        best.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        best.FuzzyChanges.Deletions.Should().Equal(3);
    }
}
