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

    private static void ShouldMatch(string pattern, string text, int index, int length, FuzzyCounts counts)
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(counts);
    }
}
