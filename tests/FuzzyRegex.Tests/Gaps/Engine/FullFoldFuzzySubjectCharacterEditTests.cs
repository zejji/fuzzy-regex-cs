using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A subject character that expands under full case folding, such as <c>ǰ</c> (U+01F0, which
/// folds to j and U+030C), can be edited as one character inside a fuzzy string, as it can where
/// the fuzzy section covers only the pattern character it replaces.
/// </summary>
/// <remarks>
/// <para>
/// A fuzzy edit applies to one subject character. Upstream's <c>STRING_FLD</c> item compares the
/// pattern with the subject character's folding and edits that folding one folded character at a
/// time (<c>upstream/src/_regex.c</c>:10580-10633), so inside a run replacing a pattern letter
/// with <c>ǰ</c> costs two edits: <c>(?fi)(?:ssx){s&lt;=1}</c> over <c>ǰsx</c> finds nothing
/// upstream, while <c>(?fi)(?:s){s&lt;=1}sx</c> over the same text is (0, 3) with one
/// substitution. A fuzzy section that covers more of the pattern allows every error placement the
/// narrower one does, so it cannot match less. The same holds for a non-expanding character
/// (<c>(?fi)(?:ssx){s&lt;=1}</c> over <c>asx</c> is (0, 3) upstream), and it is the subject-side
/// twin of ledger entry 49, which lets a pattern <c>ß</c> be edited whole
/// (<see cref="FullFoldFuzzyCharacterEditTests"/>).
/// </para>
/// <para>
/// The port keeps upstream's folded-character edits and adds two more kinds after them, a
/// substitution and an insertion of the whole subject character, tried at the start of its
/// folding (<c>Matcher.FoldWholeSub</c>). No other engine is fuzzy, so the expected values come
/// from the argument above. Upstream's answers are regex 2026.9.10, measured 2026-09-28.
/// </para>
/// </remarks>
public sealed class FullFoldFuzzySubjectCharacterEditTests
{
    // Upstream: None for every row. One row per class of expanding subject character (ß, the
    // ligatures, ǰ, ΐ, İ, ŉ), under s, i and e limits, forwards and under (?r).
    [Test]
    [Arguments("(?fi)(?:ssx){s<=1}", "ǰsx", "search", 0, 3, 1, 0, 0)]
    [Arguments("(?fi)(?:ssx){s<=1}", "sǰx", "search", 0, 3, 1, 0, 0)]
    [Arguments("(?fi)(?:fst){s<=1}", "fǰt", "fullmatch", 0, 3, 1, 0, 0)]
    [Arguments("(?fi)(?:fst){s<=1}", "fsΐ", "fullmatch", 0, 3, 1, 0, 0)]
    [Arguments("(?fi)(?:fst){i<=1}", "fßst", "fullmatch", 0, 4, 0, 1, 0)]
    [Arguments("(?fi)(?:ssx){e<=1}", "sΐx", "fullmatch", 0, 3, 1, 0, 0)]
    [Arguments("(?fi)(?:ssx){i<=1}y", "sŉsxy", "search", 0, 5, 0, 1, 0)]
    [Arguments("(?rfi)(?:ssx){s<=1}", "ǰsx", "search", 0, 3, 1, 0, 0)]
    [Arguments("(?rfi)(?:fst){i<=1}", "fsﬁt", "fullmatch", 0, 4, 0, 1, 0)]
    // A pattern ß (ledger entry 49) next to a substituted subject ǰ.
    [Arguments("(?fi)(?:ßst){s<=1}", "ßǰt", "fullmatch", 0, 3, 1, 0, 0)]
    public void An_expanding_subject_character_in_a_fuzzy_run_can_be_edited_as_one_character(
        string pattern,
        string text,
        string operation,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    ) => ShouldMatch(pattern, text, operation, index, length, new FuzzyCounts(substitutions, insertions, deletions));

    [Test]
    public void A_dotted_capital_i_is_substituted_whole()
    {
        // Upstream gives the same (0, 3) by another route: its default tables include the Turkic
        // rows this port leaves out (docs/DIVERGENCES.md), so the İ folds to a lone i there and its
        // one folded character is the whole character. Here İ folds to i and U+0307.
        ShouldMatch("(?fi)(?:ssx){s<=1}", "sİx", "fullmatch", 0, 3, new FuzzyCounts(1, 0, 0));
    }

    // The comparison matched the start of the folding, s against the first s of ß, and the edit
    // that finds the match replaces or inserts the whole ß instead (Matcher.OfferWholeFoldedCharEdit).
    // Upstream: None for all four.
    [Test]
    [Arguments("(?fi)(?:fst){s<=1}", "fßt", 0, 3, 1, 0, 0)]
    [Arguments("(?fi)(?:fst){i<=1}", "fßst", 0, 4, 0, 1, 0)]
    [Arguments("(?rfi)(?:ssx){s<=1}", "sßx", 0, 3, 1, 0, 0)]
    [Arguments("(?rfi)(?:ssx){i<=1}", "sßsx", 0, 4, 0, 1, 0)]
    public void A_subject_character_whose_folding_begins_with_the_pattern_letter_can_still_be_edited_whole(
        string pattern,
        string text,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    ) => ShouldMatch(pattern, text, "fullmatch", index, length, new FuzzyCounts(substitutions, insertions, deletions));

    // A whole-character edit starts only at the start of a folding. Part way through ﬃ (ffi) or
    // ᾷ (U+03B1 U+0342 U+03B9), skipping the rest as one insertion would charge one edit for two
    // folded characters of a character that is not inserted: the f, or the ι, matched part of it.
    // Upstream: None for both, as here.
    [Test]
    [Arguments("(?fi)(?:fi){i<=1}", "ﬃi")]
    [Arguments("(?rfi)(?:αι){i<=1}", "αᾷ")]
    public void A_whole_character_edit_does_not_start_part_way_through_a_folding(string pattern, string text)
    {
        new FuzzyRegex(pattern).FullMatch(text).Success.Should().BeFalse();
    }

    [Test]
    public void A_whole_character_insertion_is_not_made_at_the_search_anchor()
    {
        // As for any other character, a search does not start a match with an insertion at its
        // first position, so the exact match at 1 is found. Upstream: (1, 4) with no errors.
        ShouldMatch("(?fi)(?:ssx){i<=1}", "ǰssx", "search", 1, 3, new FuzzyCounts(0, 0, 0));
    }

    // The fuzzy constraint's test sees the subject character being edited, and ǰ is not in
    // [a-z]. Upstream: None for all four.
    [Test]
    [Arguments("(?fi)(?:ssx){s<=1:[a-z]}", "ǰsx", "search")]
    [Arguments("(?rfi)(?:ssx){s<=1:[a-z]}", "ǰsx", "search")]
    [Arguments("(?fi)(?:sst){i<=1:[a-z]}", "sǰst", "fullmatch")]
    [Arguments("(?rfi)(?:sst){i<=1:[a-z]}", "sǰst", "fullmatch")]
    public void A_whole_character_edit_obeys_the_fuzzy_constraint_s_test(string pattern, string text, string operation)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "fullmatch", StringComparison.Ordinal)
            ? regex.FullMatch(text)
            : regex.Match(text);

        m.Success.Should().BeFalse();
    }

    // The whole-character edits are tried before older alternatives upstream would reach first,
    // so the first answer can hold a different mix of the same number of edits (Matcher.FoldWholeSub).
    // Upstream: (0, 3, 0), (0, 1, 1) and (0, 2, 1), with the same spans.
    [Test]
    [Arguments("(?fi)(?:ss){e<=3}", "jßsß", 0, 4, 1, 2, 0)]
    [Arguments("(?rfi)(?:fi){e<=2}", "ǰf", 0, 2, 2, 0, 0)]
    [Arguments("(?fi)(?:fi){e<=3}", "ißß", 0, 3, 2, 1, 0)]
    public void The_first_answer_can_hold_a_different_mix_of_the_same_number_of_edits(
        string pattern,
        string text,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    ) => ShouldMatch(pattern, text, "fullmatch", index, length, new FuzzyCounts(substitutions, insertions, deletions));

    // Controls, where upstream is already right: deletions alone cannot absorb a subject character,
    // and a pattern ß replaced by a subject ǰ is ledger entry 49's character reading.
    [Test]
    public void Deletions_alone_still_cannot_absorb_an_expanding_subject_character()
    {
        new FuzzyRegex("(?fi)(?:ssx){d<=1}").FullMatch("ǰsx").Success.Should().BeFalse();
    }

    [Test]
    public void A_pattern_sharp_s_is_still_replaced_by_one_subject_character()
    {
        ShouldMatch("(?fi)(?:fßt){s<=1}", "fǰt", "fullmatch", 0, 3, new FuzzyCounts(1, 0, 0));
    }

    private static void ShouldMatch(
        string pattern,
        string text,
        string operation,
        int index,
        int length,
        FuzzyCounts counts
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "fullmatch", StringComparison.Ordinal)
            ? regex.FullMatch(text)
            : regex.Match(text);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(counts);
    }
}
