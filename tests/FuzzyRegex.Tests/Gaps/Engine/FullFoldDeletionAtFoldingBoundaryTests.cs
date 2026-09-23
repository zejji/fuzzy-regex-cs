using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A fuzzy full-case-folded item that runs out half-way through a subject character's folding can
/// end before that character, charging each pattern character it compared there as a deletion.
/// </summary>
/// <remarks>
/// <para>
/// Under full case folding ß folds to ss, so the literal <c>sss</c> over <c>ßß</c> matches the
/// whole first ß and the first half of the second. The item cannot end there, and upstream's
/// leftovers loop (<c>upstream/src/_regex.c:14856</c>) can only finish the folding with an insertion
/// or a substitution. Its deletion moves nothing: it charges an edit, leaves the folding as it was,
/// and with free deletions repeats for ever. So with only deletions allowed the match is lost, and
/// a match upstream finds over <c>ß</c> disappears when another ß follows. Here a deletion in the
/// leftovers takes back the last comparison into the part-used folding instead, so the pattern's
/// last s counts as deleted and the item ends before the second ß. The same applies to
/// <c>STRING_FLD_REV</c>, <c>REF_GROUP_FLD</c> and <c>REF_GROUP_FLD_REV</c>. Ledger entry 31.
/// </para>
/// <para>
/// Upstream finds these matches nowhere, so each expected value is quoted from a
/// <c>regex</c> 2026.9.10 call that gives the same alignment where upstream is not defective: the
/// same pattern over the subject cut at the character boundary, or the backreference written as a
/// literal that compiles to whole-character comparison. Each was also checked by counting edits.
/// Measured 2026-09-23 with <c>regex.I | regex.V1</c>.
/// </para>
/// </remarks>
public sealed class FullFoldDeletionAtFoldingBoundaryTests
{
    [Test]
    public void A_following_character_does_not_lose_a_one_deletion_match()
    {
        // V1 search('(?:sss){d<=1}', 'ß', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1 search('(?:sss){d<=1}', 'ßß', I): span=(1, 2), the defect.
        Match m = new FuzzyRegex("(?i)(?:sss){d<=1}").Match("ßß");

        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    public void A_reversed_string_ends_before_the_part_used_character()
    {
        // V1 search('(?r)(?:sss){d<=1}', 'ß', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [0])
        // The deletion is recorded at the match's left edge.
        // V1 search('(?r)(?:sss){d<=1}', 'ßß', I): span=(0, 1), the defect.
        Match m = new FuzzyRegex("(?ri)(?:sss){d<=1}").Match("ßß");

        (m.Index, m.Length).Should().Be((1, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    public void Two_comparisons_into_a_three_character_folding_become_two_deletions()
    {
        // ﬃ folds to ffi. The pattern's ff matches its first two characters, and ending before
        // it costs both.
        // V1 search('(?:ff){d<=2}', '', I): span=(0, 0) counts=(0, 0, 2) changes=([], [], [0, 1])
        // V1 search('(?:ff){d<=2}', 'ﬃ', I): span=(1, 1), the defect.
        Match m = new FuzzyRegex("(?i)(?:ff){d<=2}").Match("ﬃ");

        (m.Index, m.Length).Should().Be((0, 0));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
        m.FuzzyChanges.Deletions.Should().Equal(0, 1);

        // V1 search('(?r)(?:fi){d<=2}', '', I): span=(0, 0) counts=(0, 0, 2) changes=([], [], [0, 1])
        // V1 search('(?r)(?:fi){d<=2}', 'ﬃ', I): span=(0, 0), the defect, found after moving left.
        Match reversed = new FuzzyRegex("(?ri)(?:fi){d<=2}").Match("ﬃ");

        (reversed.Index, reversed.Length).Should().Be((1, 0));
        reversed.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
    }

    [Test]
    public void A_deletion_before_the_folding_and_one_into_it_are_both_charged()
    {
        // V1 search('(?:fffx){d<=2}', 'ﬀ', I): span=(0, 1) counts=(0, 0, 2) changes=([], [], [1, 2])
        // V1 search('(?:fffx){d<=2}', 'ﬀﬃ', I): None, the defect.
        Match m = new FuzzyRegex("(?i)(?:fffx){d<=2}").Match("ﬀﬃ");

        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
    }

    [Test]
    [Arguments(@"(?i)(s)(?:\1){d<=1}", "sß", 0, 1, 1)]
    [Arguments(@"(?i)(s)(?:\1){d<=1}", "sßs", 0, 1, 1)]
    [Arguments(@"(?i)(as)(?:\1){d<=1}", "asaß", 0, 3, 3)]
    [Arguments(@"(?ri)(?:\1){d<=1}(s)", "ßs", 1, 1, 1)]
    [Arguments(@"(?ri)(?:\1){d<=1}(sa)", "ßasa", 1, 3, 1)]
    public void A_backreference_ends_before_the_part_used_character(
        string pattern,
        string subject,
        int index,
        int length,
        int deletedAt
    )
    {
        // The literal forms compile to whole-character comparison, where s against ß is a mismatch:
        // V1 search('(s)(?:s){d<=1}', 'sß', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1 search('(s)(?:s){d<=1}', 'sßs', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1 search('(as)(?:as){d<=1}', 'asaß', I): span=(0, 3) counts=(0, 0, 1) changes=([], [], [3])
        // V1 search('(?r)(?:s){d<=1}(s)', 'ßs', I): span=(1, 2) counts=(0, 0, 1) changes=([], [], [1])
        // V1 search('(?r)(?:sa){d<=1}(sa)', 'ßasa', I): span=(1, 4) counts=(0, 0, 1) changes=([], [], [1])
        // With the backreference, V1 gives None for all but 'sßs', where it gives (2, 3).
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(deletedAt);
    }

    [Test]
    [Arguments("(?i)(?:sss){0d+1s+1i<=1:[x]}", "ßß", 0)]
    [Arguments("(?ri)(?:sss){0d+1s+1i<=1:[x]}", "ßß", 1)]
    [Arguments(@"(?i)(s)(?:\1){0d+1s+1i<=1:[x]}", "sß", 0)]
    [Arguments(@"(?ri)(?:\1){0d+1s+1i<=1:[x]}(s)", "ßs", 1)]
    public void A_free_deletion_ends_the_item_before_the_part_used_character(string pattern, string subject, int index)
    {
        // ':[x]' forbids the substitution and insertion that could finish the folding. Upstream's
        // deletion there deletes nothing, so free deletions never end: V1 search on the first two
        // rows raises MemoryError. S84 guarded the last two, which then found nothing.
        // V1 search('(?:sss){0d+1s+1i<=1:[x]}', 'ß', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1 search('(?r)(?:sss){0d+1s+1i<=1:[x]}', 'ß', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [0])
        // V1 search('(s)(?:s){0d+1s+1i<=1:[x]}', 'sß', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1 search('(?r)(?:s){0d+1s+1i<=1:[x]}(s)', 'ßs', I): span=(1, 2) counts=(0, 0, 1) changes=([], [], [1])
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Length).Should().Be((index, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    [Test]
    [Arguments("(?i)(?:sss){0d+1s<=1}x", "ßßy")]
    [Arguments("(?ri)x(?:sss){0d+1s<=1}", "yßß")]
    public void A_retried_free_deletion_in_a_string_ends(string pattern, string subject)
    {
        // The substitution that finishes the second ß is taken first and fails at the x; the retry
        // offers the deletion, which must make progress too. The reversed row passes with
        // SkipLeftoverTakeBack set as well (measured 2026-09-23), so it only pins the answer.
        // V1 search(pattern, subject, I): None for both.
        new FuzzyRegex(pattern)
            .Match(subject)
            .Success.Should()
            .BeFalse();
    }

    [Test]
    [Arguments(@"(?i)(s)(?:\1){s<=1,d<=1}", "sﬀ", 0)]
    [Arguments(@"(?ri)(?:\1){s<=1,d<=1}(s)", "ﬀs", 1)]
    [Arguments(@"(?i)(x)(?:\1){i<=1,d<=2}", "xﬀ", 0)]
    public void A_folding_an_edit_was_made_in_is_not_taken_back(string pattern, string subject, int index)
    {
        // The backreference's s is substituted for ﬀ's first f, or its x is inserted before it,
        // and the group runs out. Taking back that comparison would leave the substitution or
        // insertion charged for a character outside the match, so the engine backtracks and
        // deletes the pattern's character instead.
        // V1 search('(s)(?:s){s<=1,d<=1}', 's', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1 search('(?r)(?:s){s<=1,d<=1}(s)', 's', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [0])
        // V1 search('(x)(?:x){i<=1,d<=2}', 'x', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // With the backreference over the full subject, V1 gives None for all three.
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Length).Should().Be((index, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
        m.FuzzyChanges.Deletions.Should().Equal(1);
    }

    [Test]
    [Arguments(@"(?i)(s)(?=(?:x){s<=1})(?:\1){d<=1}", "sß", 0, 1, 1)]
    [Arguments(@"(?ri)(?:\1){d<=1}(?<=(?:x){s<=1})(s)", "ßs", 1, 1, 1)]
    [Arguments("(?i)(?=(?:x){s<=1})(?:ff){d<=2}", "ﬃ", 0, 0, 2)]
    [Arguments("(?ri)(?:fi){d<=2}(?<=(?:x){s<=1})", "ﬃ", 1, 0, 2)]
    public void An_edit_made_before_the_item_does_not_stop_a_take_back(
        string pattern,
        string subject,
        int index,
        int length,
        int deletions
    )
    {
        // The lookaround substitutes the subject character the item then starts on, so its edit is
        // recorded at the same position as the item's. It is not an edit inside the folding.
        // V1 search('(s)(?=(?:x){s<=1})(?:s){d<=1}', 'sß', I): span=(0, 1) counts=(1, 0, 1) changes=([1], [], [1])
        // V1 search('(?r)(?:s){d<=1}(?<=(?:x){s<=1})(s)', 'ßs', I): span=(1, 2) counts=(1, 0, 1) changes=([1], [], [1])
        // y and z fold singly, so these give the literals' alignment with the pattern deleted
        // before the subject character:
        // V1 search('(?=(?:x){s<=1})(?:yz){d<=2}', 'a', I): span=(0, 0) counts=(1, 0, 2) changes=([0], [], [0, 1])
        // V1 search('(?r)(?:yz){d<=2}(?<=(?:x){s<=1})', 'a', I): span=(1, 1) counts=(1, 0, 2) changes=([1], [], [1, 2])
        // Over the subjects here V1 gives None for all four.
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, deletions));
    }

    [Test]
    public void Best_and_enhanced_matching_find_the_first_character()
    {
        // V1 search('(?b)(?:sss){d<=1}', 'ß', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // V1 search('(?e)(?:sss){d<=1}', 'ß', I): span=(0, 1) counts=(0, 0, 1) changes=([], [], [1])
        // Over 'ßß' both give (1, 2), the defect.
        Match best = new FuzzyRegex("(?bi)(?:sss){d<=1}").Match("ßß");
        (best.Index, best.Length).Should().Be((0, 1));
        best.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));

        Match enhanced = new FuzzyRegex("(?ei)(?:sss){d<=1}").Match("ßß");
        (enhanced.Index, enhanced.Length).Should().Be((0, 1));
        enhanced.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
    }

    [Test]
    public void An_insertion_is_still_preferred_to_taking_a_comparison_back()
    {
        // Upstream tries a substitution, then an insertion, then a deletion; the new deletion keeps
        // that place in the order.
        // V1 search('(?:sss){d<=1,i<=1}', 'ßß', I): span=(0, 2) counts=(0, 1, 0) changes=([], [1], [])
        Match m = new FuzzyRegex("(?i)(?:sss){d<=1,i<=1}").Match("ßß");

        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }
}
