using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Unicode;

/// <summary>
/// What upstream 2026.9.29's move to Unicode 18.0.0 changes for a pattern: new scripts, blocks and
/// joining groups to name, new letters for <c>\w</c> and case-insensitive matching, and new
/// character names.
/// </summary>
/// <remarks>
/// The exhaustive checks are the table digests in <see cref="UnicodeTableTests"/> and
/// <see cref="UnicodePropertyTests"/>; these rows are the same change seen through the public
/// API, one per kind of data. Every expectation except the names was measured on regex 2026.9.29
/// from PyPI (2026-10-18), whose sdist is byte-identical to the tag; 2026.9.19 rejects each new
/// property name as unknown and answers false to each match. Upstream resolves <c>\N{...}</c>
/// through the host CPython's <c>unicodedata</c>, which on 3.14 is 16.0.0 and knows none of the
/// three names below; this port carries its own table at the tables' version (PORTMAP, decision B),
/// and the names are the UCD's own: <c>UnicodeData.txt</c> for the explicit one and
/// <c>extracted/DerivedName.txt</c> for the two algorithmic ranges new in 18.0.
/// </remarks>
public sealed class Unicode18Tests
{
    [Test]
    public void The_tables_are_unicode_18() => UnicodeTables.UnicodeVersion.Should().Be("18.0.0");

    [Test]
    [Arguments(@"\p{Script=Jurchen}", "\U00018E00")]
    [Arguments(@"\p{Jurc}", "\U000191A0")]
    [Arguments(@"\p{Script=Seal}", "\U0003D000")]
    [Arguments(@"\p{Block=Bengali_Supplement}", "\U00011DF0")]
    [Arguments(@"\p{Blk=MusicSup}", "\U0001D250")]
    [Arguments(@"\p{Block=Jurchen_Radicals}", "\U000191A0")]
    [Arguments(@"\p{Block=ArchaicCuneiformNumerals}", "\U00012550")]
    [Arguments(@"\p{Block=MiscArrowsExt}", "\U0001DB00")]
    [Arguments(@"\p{Joining_Group=Crown_Beh}", "\U00010ED9")]
    [Arguments(@"\p{Script=Cuneiform}", "\U00012550")]
    [Arguments(@"\p{Lu}", "꟝")]
    [Arguments(@"\p{Assigned}", "\U0001F6D9")]
    [Arguments(@"\w", "\U00018E00")]
    [Arguments(@"\w", "\U0003D000")]
    public void A_property_new_in_unicode_18_matches_its_characters(string pattern, string subject) =>
        new FuzzyRegex(pattern).IsFullMatch(subject).Should().BeTrue();

    // Proto-Cuneiform has a property value alias in 18.0 but no characters yet: the name compiles
    // and matches nothing.
    [Test]
    [Arguments(@"\p{Pcun}", "\U00012550")]
    [Arguments(@"\p{Cn}", "\U0001F6D9")]
    [Arguments(@"\p{Script=Unknown}", "\U00018E00")]
    public void A_property_unicode_18_moved_a_character_out_of_no_longer_matches_it(string pattern, string subject) =>
        new FuzzyRegex(pattern).IsFullMatch(subject).Should().BeFalse();

    // CaseFolding.txt 18.0.0: A7DD folds to 0277, A7E2 to 027C, AB6C to AB4B.
    [Test]
    [Arguments("(?i)ɷ", "꟝")]
    [Arguments("(?i)꟝", "ɷ")]
    [Arguments("(?i)ɼ", "꟢")]
    [Arguments("(?i)ꭋ", "꭬")]
    public void A_case_pair_new_in_unicode_18_matches_case_insensitively(string pattern, string subject) =>
        new FuzzyRegex(pattern).IsFullMatch(subject).Should().BeTrue();

    // U+1DF95 LATIN SMALL LIGATURE LONG S WITH DESCENDER S is the first codepoint outside the BMP
    // whose folding lands inside it: simple folding to U+00DF, full folding to "ss". So one
    // side of the match is two UTF-16 units and the other one or two, and upstream's
    // re_simple_folding_table_4 and re_expand_on_folding outgrow 16 bits for it. Spans are UTF-16
    // here; upstream's, in codepoints, are in the comment on each row.
    [Test]
    [Arguments("(?i)ß", "x\U0001DF95y", 1, 2)] // (1, 2)
    [Arguments("(?i)\U0001DF95", "xßy", 1, 1)] // (1, 2)
    [Arguments("(?i)\U0001DF95", "xẞy", 1, 1)] // (1, 2)
    [Arguments("(?fi)ss", "x\U0001DF95y", 1, 2)] // (1, 2)
    [Arguments("(?fi)SS", "x\U0001DF95y", 1, 2)] // (1, 2)
    [Arguments("(?fi)\U0001DF95", "xssy", 1, 2)] // (1, 3)
    [Arguments("(?fi)\U0001DF95", "xSSy", 1, 2)] // (1, 3)
    [Arguments("(?fi)ß", "x\U0001DF95y", 1, 2)] // (1, 2)
    [Arguments("(?fi)s\U0001DF95s", "xsssssy", 1, 4)] // (1, 5)
    [Arguments("(?i)[ß]", "x\U0001DF95y", 1, 2)] // (1, 2)
    [Arguments("(?i)[\U0001DF95]", "xßy", 1, 1)] // (1, 2)
    [Arguments("(?fi)[ß]", "x\U0001DF95y", 1, 2)] // (1, 2)
    [Arguments("(?fi)[a-z\U0001DF95]+", "xssy", 0, 4)] // (0, 4)
    [Arguments(@"(?i)[\U0001DF00-\U0001DFFF]", "xßy", 1, 1)] // (1, 2)
    [Arguments("(?fi)(?:ss){e<=1}", "\U0001DF95", 0, 2)] // (0, 1)
    [Arguments(@"(?i)\U0001DF4A", "\U0001DF4B", 0, 2)] // (0, 1)
    [Arguments("(?fi)st\U0001DF95", "stss", 0, 4)] // (0, 4)
    [Arguments("(?i)\U0001DF95{2}", "ß\U0001DF95", 0, 3)] // (0, 2)
    public void A_fold_from_outside_the_bmp_into_it_matches_like_upstream(
        string pattern,
        string subject,
        int index,
        int length
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
    }

    [Test]
    [Arguments("LIGHTHOUSE", "\U0001F6D9")]
    [Arguments("JURCHEN CHARACTER-18E00", "\U00018E00")]
    [Arguments("small seal character-3fc3f", "\U0003FC3F")]
    public void A_name_new_in_unicode_18_resolves(string name, string character) =>
        new FuzzyRegex(@"\N{" + name + "}").IsFullMatch(character).Should().BeTrue();
}
