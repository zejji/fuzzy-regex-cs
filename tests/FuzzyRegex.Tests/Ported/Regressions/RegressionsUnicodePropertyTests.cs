using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions about
/// Unicode property matching (<c>\p{...}</c>, script extensions, and vertical/horizontal space).
/// </summary>
/// <remarks>
/// The Bengali, Arabic, and Devanagari subjects are single BMP code points that are not readable
/// Latin script, so each is held in a named constant giving its code point. The horizontal- and
/// vertical-space subjects are built entirely from <c>\u</c> escapes rather than pasted glyphs,
/// since most of those code points render as invisible or near-identical blanks. The "smiling cat
/// face with open mouth" emoji is outside the Basic Multilingual Plane (U+1F63A), so it is built
/// with <see cref="char.ConvertFromUtf32(int)"/> rather than pasted as a literal, and its span
/// assertion is recomputed from Python's codepoint count to .NET's UTF-16 code units.
/// </remarks>
public sealed class RegressionsUnicodePropertyTests
{
    // U+09EF BENGALI DIGIT NINE.
    private const string _bengaliDigitNine = "৯";

    // U+062A ARABIC LETTER TEH.
    private const string _arabicLetterTeh = "ت";

    // U+091C DEVANAGARI LETTER JA.
    private const string _devanagariLetterJa = "ज";

    // U+1F63A SMILING CAT FACE WITH OPEN MOUTH. Outside the BMP: one Python codepoint, but a
    // UTF-16 surrogate pair (2 chars) in .NET.
    private static readonly string _smilingCatFaceWithOpenMouth = char.ConvertFromUtf32(0x1F63A);

    // Every code point regex.HorizSpace treats as horizontal whitespace: TAB, SPACE, NBSP,
    // OGHAM SPACE MARK, MONGOLIAN VOWEL SEPARATOR, EN QUAD .. HAIR SPACE, NARROW NBSP, MEDIUM
    // MATHEMATICAL SPACE, IDEOGRAPHIC SPACE.
    private const string _everyHorizontalSpaceCharacter =
        "\t \u00A0\u1680\u180E\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u202F\u205F\u3000";

    // Every code point regex.VertSpace treats as vertical whitespace: LF, VT, FF, CR, NEL,
    // LINE SEPARATOR, PARAGRAPH SEPARATOR.
    private const string _everyVerticalSpaceCharacter = "\n\v\f\r\u0085\u2028\u2029";

    // Hg issue 291: Include Script Extensions as a supported Unicode property.
    [Test]
    [Arguments(@"(?u)\p{Script:Beng}")]
    [Arguments(@"(?u)\p{Script:Bengali}")]
    [Arguments(@"(?u)\p{Script_Extensions:Bengali}")]
    [Arguments(@"(?u)\p{Script_Extensions:Beng}")]
    [Arguments(@"(?u)\p{Script_Extensions:Cakm}")]
    [Arguments(@"(?u)\p{Script_Extensions:Sylo}")]
    [Property("Upstream", "RegexTests.test_hg_bugs#312-317")]
    public void Script_and_script_extensions_spellings_all_match_a_shared_bengali_digit(string pattern) =>
        FuzzyRegex.MatchAtStart(_bengaliDigitNine, pattern).Success.Should().BeTrue();

    // Hg issue #293: scx (Script Extensions) property currently matches incorrectly.
    [Test]
    [Arguments(@"(?u)\p{scx:Latin}", "P", true)]
    [Arguments(@"(?u)\p{scx:Ahom}", "P", false)]
    [Arguments(@"(?u)\p{scx:Common}", "4", true)]
    [Arguments(@"(?u)\p{scx:Caucasian_Albanian}", "4", false)]
    [Arguments(@"(?u)\p{scx:Arabic}", _arabicLetterTeh, true)]
    [Arguments(@"(?u)\p{scx:Balinese}", _arabicLetterTeh, false)]
    [Arguments(@"(?u)\p{scx:Devanagari}", _devanagariLetterJa, true)]
    [Arguments(@"(?u)\p{scx:Batak}", _devanagariLetterJa, false)]
    [Property("Upstream", "RegexTests.test_hg_bugs#318-325")]
    public void Scx_short_form_matches_only_the_scripts_the_character_actually_belongs_to(
        string pattern,
        string subject,
        bool expected
    ) => FuzzyRegex.MatchAtStart(subject, pattern).Success.Should().Be(expected);

    // Git issue 473: Emoji classified as letter.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#433")]
    public void Letter_or_titlecase_class_does_not_match_a_cat_face_emoji() =>
        FuzzyRegex.MatchAtStart(_smilingCatFaceWithOpenMouth, @"^\p{LC}+$").Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#434")]
    public void Other_symbol_class_matches_the_whole_cat_face_emoji_as_one_surrogate_pair()
    {
        // Upstream expects (0, 1) in codepoints; U+1F63A is a surrogate pair, so UTF-16 gives (0, 2).
        Match m = FuzzyRegex.MatchAtStart(_smilingCatFaceWithOpenMouth, @"^\p{So}+$");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }

    // Git issue 477: \v for vertical spacing.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#437")]
    public void HorizSpace_property_fullmatches_every_horizontal_space_character() =>
        FuzzyRegex.FullMatch(_everyHorizontalSpaceCharacter, @"\p{HorizSpace}+").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#438")]
    public void VertSpace_property_fullmatches_every_vertical_space_character() =>
        FuzzyRegex.FullMatch(_everyVerticalSpaceCharacter, @"\p{VertSpace}+").Success.Should().BeTrue();

    // Git issue 580: Regression in v2025.7.31: \P{L} no longer matches in simple patterns.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#495")]
    public void Optional_non_letter_followed_by_a_letter_matches_at_the_start_of_a_word() =>
        FuzzyRegex.MatchAtStart("hello,", @"\A\P{L}?\p{L}").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#496")]
    public void Non_letter_runs_around_a_named_letter_group_fullmatch_the_whole_word() =>
        FuzzyRegex.FullMatch("hello,", @"\A\P{L}*(?P<w>\p{L}+)\P{L}*\Z").Success.Should().BeTrue();
}
