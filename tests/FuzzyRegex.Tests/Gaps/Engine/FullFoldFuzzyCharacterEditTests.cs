using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A character that expands under full case folding, such as <c>ß</c> (which folds to <c>ss</c>),
/// can be edited as one character inside a fuzzy string, as it can on its own.
/// </summary>
/// <remarks>
/// <para>
/// On its own, <c>(?fi)ß</c> compiles to a choice between the character and its folding
/// (<c>Character._compile</c>, <c>upstream/regex/_regex_core.py:2629-2632</c>), so one substitution
/// can replace the whole <c>ß</c>: <c>(?fi)(?:ß){s&lt;=1}</c> over <c>a</c> is (0, 1) upstream.
/// Next to another literal, <c>Sequence.pack_characters</c> packs both into one <c>STRING_FLD</c>
/// item holding only the folding, <c>ssx</c>, whose edits are one folded letter each. Replacing the
/// <c>ß</c> then costs a substitution and a deletion, so <c>(?fi)(?:ßx){s&lt;=1}</c> over <c>ax</c>
/// finds nothing upstream although <c>(?fi)(?:ß){s&lt;=1}x</c> over the same text matches.
/// </para>
/// <para>
/// A fuzzy section that covers more of the pattern allows every error placement the narrower one
/// does, so it cannot match less. The port keeps the packed string, whose exact matches (including
/// a subject <c>ß</c> spanning two pattern letters) are unchanged, and adds the character-by-character
/// reading as a second alternative when the section is fuzzy. PCRE2 10.47, Python <c>re</c>, .NET
/// and JavaScript do not fold <c>ß</c> to <c>ss</c> at all; Perl 5.42 does and agrees on every exact
/// row here. None of them is fuzzy, so the expected values come from the argument above. Upstream's
/// answers are regex 2026.9.10, measured 2026-09-28. Ledger entry 49.
/// </para>
/// </remarks>
public sealed class FullFoldFuzzyCharacterEditTests
{
    // Upstream: None for every row. Its split forms, such as (?fi)(?:ß){s<=1}x over 'ax' and
    // (?fi)(?:ß){d<=1}x over 'x', give the answers expected here.
    [Test]
    [Arguments("(?fi)(?:ßx){s<=1}", "ax", 0, 2, 1, 0, 0)]
    [Arguments("(?fi)(?:ßx){e<=1}", "ax", 0, 2, 1, 0, 0)]
    [Arguments("(?fi)(?:ßx){s<=1}", "sx", 0, 2, 1, 0, 0)]
    [Arguments("(?fi)(?:ßx){s<=1}", "axx", 0, 2, 1, 0, 0)]
    [Arguments("(?fi)(?:ßx){d<=1}", "x", 0, 1, 0, 0, 1)]
    [Arguments("(?fi)(?:ﬁx){s<=1}", "ax", 0, 2, 1, 0, 0)]
    [Arguments("(?rfi)(?:ßx){s<=1}", "ax", 0, 2, 1, 0, 0)]
    [Arguments("(?fi)(?:ßßx){s<=1}", "ssax", 0, 4, 1, 0, 0)]
    [Arguments("(?fi)(?:ssßx){s<=1}", "ßax", 0, 3, 1, 0, 0)]
    public void An_expanding_character_in_a_fuzzy_string_can_be_edited_as_one_character(
        string pattern,
        string text,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    ) => ShouldMatch(pattern, text, index, length, new FuzzyCounts(substitutions, insertions, deletions));

    [Test]
    public void The_best_match_replaces_the_expanding_character_with_one_substitution()
    {
        // Upstream: (0, 2) with counts (1, 0, 1), a substitution and a deletion for one character.
        Match m = new FuzzyRegex("(?bfi)(?:ßx){e<=2}").Match("ax");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    // Controls, where upstream is already right: exact matches through the folding, including a
    // subject ß that answers for two pattern letters on either side of the pattern's own ß, and an
    // edit to one letter of the folding.
    [Test]
    [Arguments("(?fi)(?:ßx){s<=1}", "ssx", 0, 3, 0, 0, 0)]
    [Arguments("(?fi)(?:ßx){s<=1}", "ẞx", 0, 2, 0, 0, 0)]
    [Arguments("(?fi)(?:sß){e<=1}", "ßs", 0, 2, 0, 0, 0)]
    [Arguments("(?fi)(?:ßx){s<=1}", "sax", 0, 3, 1, 0, 0)]
    [Arguments("(?fi)(?:ßx){s<=1}", "ẞy", 0, 2, 1, 0, 0)]
    [Arguments("(?fi)(?:ß){s<=1}x", "ax", 0, 2, 1, 0, 0)]
    public void A_fuzzy_full_fold_string_keeps_the_matches_its_folding_gives(
        string pattern,
        string text,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    ) => ShouldMatch(pattern, text, index, length, new FuzzyCounts(substitutions, insertions, deletions));

    [Test]
    public void Two_literal_letters_are_still_two_characters()
    {
        // Only a character that expands gets the one-character reading: 'ss' written out is two
        // pattern characters, and replacing both with one text character is a substitution and a
        // deletion. Upstream: None.
        new FuzzyRegex("(?fi)(?:ssx){s<=1}")
            .Match("ax")
            .Success.Should()
            .BeFalse();
    }

    // Known defect D24, the backreference twin. REF_GROUP_FLD folds the captured text as it goes
    // and edits the group's folding one folded character at a time (next_fuzzy_match_group_fld,
    // upstream/src/_regex.c:10824-10877), so a captured ß cost two edits to replace or delete,
    // although the literal ß it stands for costs one (the first test above, and the split forms
    // (?fi)(ß)x(?:ß){s<=1} over 'ßxa', (0, 3) with one substitution upstream, and
    // (?fi)(ß)x(?:ß){d<=1} over 'ßx', (0, 2) with one deletion). The 'ßxs', 'ßx-sx', 'xs-xß' and
    // 'ßs-s' rows need the edit after the first folded s of the group's ß matched
    // (Matcher.OfferWholeFoldedGroupCharEdit). Upstream: None for every row.
    [Test]
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1}", "ßxa", "fullmatch", 0, 3, 1, 0, 0)]
    [Arguments(@"(?fi)(ß)x(?:\1){e<=1}", "ßxa", "fullmatch", 0, 3, 1, 0, 0)]
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1}", "ßxs", "fullmatch", 0, 3, 1, 0, 0)]
    [Arguments(@"(?fi)(ß)x(?:\1){d<=1}", "ßx", "fullmatch", 0, 2, 0, 0, 1)]
    [Arguments(@"(?fi)(ß)(?:\1){s<=1}", "ßa", "search", 0, 2, 1, 0, 0)]
    [Arguments(@"(?fi)(ßx)-(?:\1){s<=1}", "ßx-ax", "fullmatch", 0, 5, 1, 0, 0)]
    [Arguments(@"(?fi)(ßx)-(?:\1){s<=1}", "ßx-sx", "fullmatch", 0, 5, 1, 0, 0)]
    [Arguments(@"(?fi)(ßx)-(?:\1){d<=1}", "ßx-x", "fullmatch", 0, 4, 0, 0, 1)]
    [Arguments(@"(?fi)(ﬁx)-(?:\1){s<=1}", "ﬁx-ax", "fullmatch", 0, 5, 1, 0, 0)]
    [Arguments(@"(?fi)(aßb)-(?:\1){s<=1}", "aßb-acb", "fullmatch", 0, 7, 1, 0, 0)]
    [Arguments(@"(?fi)(aßb)-(?:\1){d<=1}", "aßb-ab", "fullmatch", 0, 6, 0, 0, 1)]
    [Arguments(@"(?fi)(ßs)-(?:\1){d<=1}", "ßs-s", "fullmatch", 0, 4, 0, 0, 1)]
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1:[a-z]}", "ßxa", "fullmatch", 0, 3, 1, 0, 0)]
    // U+0345 is a combining mark (Mn) that folds to a letter, ι, so the test refuses the folded
    // substitution and passes the whole one, on the first attempt (Matcher.GroupFoldKinds). The
    // literal (?fi)(ß)x(?:ß){s<=1:\p{Mn}} gives the same upstream.
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1:\p{Mn}}", "ßxͅ", "fullmatch", 0, 3, 1, 0, 0)]
    [Arguments(@"(?rfi)(?:\1){s<=1}-(ßx)", "ax-ßx", "fullmatch", 0, 5, 1, 0, 0)]
    [Arguments(@"(?rfi)(?:\1){s<=1}-(xß)", "xs-xß", "fullmatch", 0, 5, 1, 0, 0)]
    [Arguments(@"(?rfi)(?:\1){d<=1}-(ßx)", "x-ßx", "fullmatch", 0, 4, 0, 0, 1)]
    public void An_expanding_group_character_in_a_fuzzy_backreference_can_be_edited_as_one_character(
        string pattern,
        string text,
        string operation,
        int index,
        int length,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "fullmatch", StringComparison.Ordinal)
            ? regex.FullMatch(text)
            : regex.Match(text);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }

    // Controls, None in both engines. Two captured letters are two characters. The constraint's
    // test sees the subject character: é is not in [a-z], nor a in \p{Mn}. A whole edit takes a
    // whole character on each side it touches: part way through the subject's ﬀ, the group's ß
    // cannot be substituted for the second f; part way through the group's ﬃ, the fi left over is
    // not one character to delete, with or without an expanding subject ß next; at the end of the
    // subject no character is left to substitute for the ß; and part way through the subject's ﬃ,
    // the fi left over is not one character to insert.
    [Test]
    [Arguments(@"(?fi)(ss)x(?:\1){s<=1}", "ssxa")]
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1:[a-z]}", "ßxé")]
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1:\p{Mn}}", "ßxa")]
    [Arguments(@"(?fi)(fß)-(?:\1){s<=1}", "fß-ﬀ")]
    [Arguments(@"(?fi)(ﬃ)x(?:\1){d<=1}", "ﬃxf")]
    [Arguments(@"(?fi)(ﬃ)x(?:\1){d<=1}ß", "ﬃxfß")]
    [Arguments(@"(?fi)(ß)x(?:\1){s<=1}", "ßx")]
    [Arguments(@"(?fi)(fß)-(?:\1){i<=1}", "fß-ﬃß")]
    public void A_fuzzy_backreference_edits_a_group_character_whole_only_within_its_limits(string pattern, string text)
    {
        new FuzzyRegex(pattern).FullMatch(text).Success.Should().BeFalse();
    }

    private static void ShouldMatch(string pattern, string text, int index, int length, FuzzyCounts counts)
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(counts);
    }
}
