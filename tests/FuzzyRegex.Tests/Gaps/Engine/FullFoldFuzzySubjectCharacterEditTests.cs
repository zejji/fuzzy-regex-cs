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

    // Matcher.WholeSubstitutionRepeatsAFoldedInsertion leaves out a whole substitution only when
    // nothing can tell it from the folded insertion already tried. Each row goes red with one of
    // its checks removed (measured 2026-09-28): the first when the section's limits are not both
    // at least the error maximum and the ENHANCEMATCH check is gone too (None), the second with
    // the maximum check alone ((1, 4) found first), the third with the equal-cost check alone
    // (None), the fourth with the only-section check alone (None), and the fifth with the minimum
    // check alone (None: the folded insertion leaves no substitution to meet the minimum).
    // Upstream: None for the first, third, fourth and fifth rows; (1, 4) with (2, 0, 0) for the
    // second, a different valid match. The fifth's twins agree on one substitution in both engines:
    // `(?fi)(?:s){1<=s<=1,i<=1,e<=1}t` over 'ßt' and `(?fi)(?:st){1<=s<=1,i<=1,e<=1}` over 'xt'
    // (measured 2026-09-29). It needs `i<=1`: naming `s` sets the other maxima to 0, so without it no
    // insertion is permitted and the check is never reached. And it is spelt `1<=s<=1`, because
    // `{1<=s,e<=1}` is not a constraint at all: both engines match its braces as literal text.
    [Test]
    [Arguments("(?efi)(?:stst){s<=2,i<=1}", "ßfﬆﬁ", "fullmatch", 0, 4, 2, 1, 0)]
    [Arguments("(?fi)(?:sstt){s<=2,i<=1}", "ﬆxﬆǰx", "search", 0, 4, 2, 1, 0)]
    [Arguments("(?fi)(?:stsst){1s+2i<=4}", "ßßisß", "fullmatch", 0, 5, 4, 0, 0)]
    [Arguments("(?fi)(?:(?:st){e<=2}){1<=s<=2,e<=3}", "ßs", "fullmatch", 0, 2, 2, 0, 0)]
    [Arguments("(?fi)(?:st){1<=s<=1,i<=1,e<=1}", "ßt", "fullmatch", 0, 2, 1, 0, 0)]
    public void A_whole_substitution_is_left_out_only_where_it_repeats_a_folded_insertion(
        string pattern,
        string text,
        string operation,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    ) => ShouldMatch(pattern, text, operation, index, length, new FuzzyCounts(substitutions, insertions, deletions));

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

    // D22, the backreference twin: REF_GROUP_FLD edits the subject character's folding one folded
    // character at a time too (next_fuzzy_match_group_fld, upstream/src/_regex.c:10824-10877), so
    // a fuzzy backreference could not replace a group letter with ǰ, although it replaces it with
    // an a ('ssxas' is (0, 5) with one substitution in both engines) and the literal it stands for
    // does (the first test above). The ß rows need the edit after the first folded s matched
    // (Matcher.OfferWholeFoldedGroupCharEdit). Upstream: None for every row.
    [Test]
    [Arguments(@"(?fi)(ss)x(?:\1){s<=1}", "ssxǰs", "fullmatch", 0, 5, 1, 0, 0)]
    [Arguments(@"(?fi)(ss)x(?:\1){s<=1}", "ssxsǰ", "search", 0, 5, 1, 0, 0)]
    [Arguments(@"(?fi)(ss)x(?:\1){e<=1}", "ssxǰs", "fullmatch", 0, 5, 1, 0, 0)]
    [Arguments(@"(?fi)(ss)x(?:\1){i<=1}", "ssxsǰs", "fullmatch", 0, 6, 0, 1, 0)]
    [Arguments(@"(?fi)(fst)x(?:\1){s<=1}", "fstxfßt", "fullmatch", 0, 7, 1, 0, 0)]
    [Arguments(@"(?fi)(fst)x(?:\1){i<=1}", "fstxfßst", "fullmatch", 0, 8, 0, 1, 0)]
    [Arguments(@"(?fi)(sst)x(?:\1){i<=1}", "sstxsǰst", "fullmatch", 0, 8, 0, 1, 0)]
    [Arguments(@"(?rfi)(?:\1){s<=1}x(ss)", "ǰsxss", "fullmatch", 0, 5, 1, 0, 0)]
    [Arguments(@"(?rfi)(?:\1){s<=1}-(ssx)", "sßx-ssx", "fullmatch", 0, 7, 1, 0, 0)]
    [Arguments(@"(?rfi)(?:\1){i<=1}-(ssx)", "sßsx-ssx", "fullmatch", 0, 8, 0, 1, 0)]
    public void An_expanding_subject_character_in_a_fuzzy_backreference_can_be_edited_as_one_character(
        string pattern,
        string text,
        string operation,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    ) => ShouldMatch(pattern, text, operation, index, length, new FuzzyCounts(substitutions, insertions, deletions));

    // A whole substitution replaces a whole group character, so ǰ for a captured ß is one edit;
    // part way through the ß it is refused, because the s already matched is half of that ß and
    // 'sǰ' is two characters for one. Upstream: None for all four.
    [Test]
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1}", "ßxǰ", true)]
    [Arguments(@"(?rfi)(?:\1){s<=1}x(ß)", "ǰxß", true)]
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1}", "ßxsǰ", false)]
    [Arguments(@"(?rfi)(?:\1){s<=1}x(ß)", "ǰsxß", false)]
    public void A_whole_substitution_in_a_backreference_takes_a_whole_group_character(
        string pattern,
        string text,
        bool matches
    )
    {
        Match m = new FuzzyRegex(pattern).FullMatch(text);

        m.Success.Should().Be(matches);

        if (matches)
        {
            m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        }
    }

    [Test]
    public void A_backreference_tries_the_whole_substitution_when_the_folded_edits_fail_at_once()
    {
        // ŉ (U+0149) is a lowercase letter and folds to ʼ (U+02BC, a modifier letter) and n, so the
        // test refuses the folded substitution and passes the whole one, on the first attempt.
        // Upstream: None; its (?fi)(ss)x(?:s){s<=1:\p{Ll}}s gives (0, 5) with one substitution.
        ShouldMatch(@"(?fi)(ss)x(?:\1){s<=1:\p{Ll}}", "ssxŉs", "fullmatch", 0, 5, new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void A_backreference_makes_no_whole_character_insertion_at_the_search_anchor()
    {
        // As for a literal, a search does not start a match with an insertion. Upstream: None.
        new FuzzyRegex(@"(?fi)(?=.(ssx))(?:\1){i<=1}")
            .Match("ǰssx")
            .Success.Should()
            .BeFalse();
    }

    // The same rule when the whole insertion is reached by backtracking into an earlier edit's
    // frame, where upstream's retry spells its own insertion rule differently
    // (Matcher.RetryFuzzyMatchGroupFld): the search moves on to the exact match instead.
    // Upstream: (1, 3) and (1, 4) with no errors, as for the literal twin (?fi)(?=.*?(js))(?:js){i<=1}.
    [Test]
    [Arguments(@"(?fi)(?=.*?(js))(?:\1){i<=1}", "ǰjs", 1, 2)]
    [Arguments(@"(?fi)(?=.*?(sst))(?:\1){i<=1}", "ßsst", 1, 3)]
    public void A_retried_backreference_edit_makes_no_whole_character_insertion_at_the_search_anchor(
        string pattern,
        string text,
        int index,
        int length
    ) => ShouldMatch(pattern, text, "search", index, length, new FuzzyCounts(0, 0, 0));

    // Controls for the backreference edits, each None in both engines: the constraint's test sees
    // the whole subject character, which is not in [a-z]; an edit does not start part way through
    // ﬃ; and deletions alone cannot absorb a subject character.
    [Test]
    [Arguments(@"(?fi)(ss)x(?:\1){s<=1:[a-z]}", "ssxǰs")]
    [Arguments(@"(?rfi)(?:\1){s<=1:[a-z]}x(ss)", "ǰsxss")]
    [Arguments(@"(?fi)(sst)x(?:\1){i<=1:[a-z]}", "sstxsǰst")]
    [Arguments(@"(?fi)(fi)x(?:\1){i<=1}", "fixﬃi")]
    [Arguments(@"(?fi)(ssx)-(?:\1){d<=1}", "ssx-ǰsx")]
    public void A_fuzzy_backreference_keeps_the_limits_of_a_whole_character_edit(string pattern, string text)
    {
        new FuzzyRegex(pattern).FullMatch(text).Success.Should().BeFalse();
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
