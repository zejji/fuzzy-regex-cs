using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fuzzy_ext</c>
/// (lines 4412-4496): fuzzy quantifiers whose edit is constrained to a character set
/// (<c>{e&lt;=1:[a-z]}</c>), against literals, backreferences and case-folded sharp s, both forward
/// and with the <c>(?r)</c> reverse flag.
/// </summary>
/// <remarks>
/// U+00DF LATIN SMALL LETTER SHARP S is not distinguishable from a Greek beta by eye, so every
/// subject that contains it holds the character in <see cref="_sharpS"/> rather than a raw glyph.
/// The <em>pattern</em> text keeps upstream's own <c>\N{LATIN SMALL LETTER SHARP S}</c> spelling
/// verbatim: this port has not yet decided how <c>\N{...}</c> name escapes will be represented, so
/// the constant is used only for subject strings, never inside a pattern.
/// </remarks>
public sealed class FuzzyExtendedSyntaxTests
{
    // U+00DF LATIN SMALL LETTER SHARP S. Full-case-folds to "ss".
    private const string _sharpS = "ß";

    [Test]
    [Arguments(@"(?r)(?:a){e<=1:[a-z]}", "e", true)]
    [Arguments(@"(?:a){e<=1:[a-z]}", "e", true)]
    [Arguments(@"(?:a){e<=1:[a-z]}", "-", false)]
    [Arguments(@"(?r)(?:a){e<=1:[a-z]}", "-", false)]
    [Arguments(@"(?:a){e<=1:[a-z]}", "ae", true)]
    [Arguments(@"(?r)(?:a){e<=1:[a-z]}", "ae", true)]
    [Arguments(@"(?:a){e<=1:[a-z]}", "a-", false)]
    [Arguments(@"(?r)(?:a){e<=1:[a-z]}", "a-", false)]
    [Property("Upstream", "RegexTests.test_fuzzy_ext#1-8")]
    public void Fuzzy_literal_a_constrained_to_a_through_z_matches_only_when_the_edit_character_is_in_range(
        string pattern,
        string subject,
        bool expected
    ) => Upstream.FullMatch(subject, pattern).Success.Should().Be(expected);

    [Test]
    [Arguments(@"(?:ab){e<=1:[a-z]}", "ae", true)]
    [Arguments(@"(?r)(?:ab){e<=1:[a-z]}", "ae", true)]
    [Arguments(@"(?:ab){e<=1:[a-z]}", "a-", false)]
    [Arguments(@"(?r)(?:ab){e<=1:[a-z]}", "a-", false)]
    [Property("Upstream", "RegexTests.test_fuzzy_ext#9-12")]
    public void Fuzzy_literal_ab_constrained_to_a_through_z_matches_only_when_the_edit_character_is_in_range(
        string pattern,
        string subject,
        bool expected
    ) => Upstream.FullMatch(subject, pattern).Success.Should().Be(expected);

    [Test]
    [Arguments(@"(a)\1{e<=1:[a-z]}", "ae", true)]
    [Arguments(@"(?r)\1{e<=1:[a-z]}(a)", "ea", true)]
    [Arguments(@"(a)\1{e<=1:[a-z]}", "a-", false)]
    [Arguments(@"(?r)\1{e<=1:[a-z]}(a)", "-a", false)]
    [Property("Upstream", "RegexTests.test_fuzzy_ext#13-16")]
    public void Fuzzy_backreference_constrained_to_a_through_z_matches_only_when_the_edit_character_is_in_range(
        string pattern,
        string subject,
        bool expected
    ) => Upstream.FullMatch(subject, pattern).Success.Should().Be(expected);

    [Test]
    [Arguments(@"(?fiu)(?:\N{LATIN SMALL LETTER SHARP S}){e<=1:[a-z]}", "ts", true)]
    [Arguments(@"(?fiu)(?:\N{LATIN SMALL LETTER SHARP S}){e<=1:[a-z]}", "st", true)]
    [Arguments(@"(?firu)(?:\N{LATIN SMALL LETTER SHARP S}){e<=1:[a-z]}", "st", true)]
    [Arguments(@"(?firu)(?:\N{LATIN SMALL LETTER SHARP S}){e<=1:[a-z]}", "ts", true)]
    [Arguments(@"(?fiu)(?:\N{LATIN SMALL LETTER SHARP S}){e<=1:[a-z]}", "-s", false)]
    [Arguments(@"(?fiu)(?:\N{LATIN SMALL LETTER SHARP S}){e<=1:[a-z]}", "s-", false)]
    [Arguments(@"(?firu)(?:\N{LATIN SMALL LETTER SHARP S}){e<=1:[a-z]}", "s-", false)]
    [Arguments(@"(?firu)(?:\N{LATIN SMALL LETTER SHARP S}){e<=1:[a-z]}", "-s", false)]
    [Property("Upstream", "RegexTests.test_fuzzy_ext#17-24")]
    public void Fuzzy_sharp_s_literal_case_folds_to_ss_and_matches_only_within_the_a_through_z_constraint(
        string pattern,
        string subject,
        bool expected
    ) => Upstream.FullMatch(subject, pattern).Success.Should().Be(expected);

    [Test]
    [Arguments(@"(?fiu)(\N{LATIN SMALL LETTER SHARP S})\1{e<=1:[a-z]}", "ssst", true)]
    [Arguments(@"(?fiu)(\N{LATIN SMALL LETTER SHARP S})\1{e<=1:[a-z]}", "ssts", true)]
    [Arguments(@"(?firu)\1{e<=1:[a-z]}(\N{LATIN SMALL LETTER SHARP S})", "stss", true)]
    [Arguments(@"(?firu)\1{e<=1:[a-z]}(\N{LATIN SMALL LETTER SHARP S})", "tsss", true)]
    [Arguments(@"(?fiu)(\N{LATIN SMALL LETTER SHARP S})\1{e<=1:[a-z]}", "ss-s", false)]
    [Arguments(@"(?fiu)(\N{LATIN SMALL LETTER SHARP S})\1{e<=1:[a-z]}", "sss-", false)]
    [Arguments(@"(?firu)(\N{LATIN SMALL LETTER SHARP S})\1{e<=1:[a-z]}", "-s", false)]
    [Arguments(@"(?firu)(\N{LATIN SMALL LETTER SHARP S})\1{e<=1:[a-z]}", "s-", false)]
    [Property("Upstream", "RegexTests.test_fuzzy_ext#25-32")]
    public void Fuzzy_backreference_to_a_captured_sharp_s_matches_only_within_the_a_through_z_constraint(
        string pattern,
        string subject,
        bool expected
    ) => Upstream.FullMatch(subject, pattern).Success.Should().Be(expected);

    [Test]
    [Arguments(@"(?fiu)(ss)\1{e<=1:[a-z]}", _sharpS + "ts", true)]
    [Arguments(@"(?fiu)(ss)\1{e<=1:[a-z]}", _sharpS + "st", true)]
    [Arguments(@"(?firu)\1{e<=1:[a-z]}(ss)", "st" + _sharpS, true)]
    [Arguments(@"(?firu)\1{e<=1:[a-z]}(ss)", "ts" + _sharpS, true)]
    [Arguments(@"(?fiu)(ss)\1{e<=1:[a-z]}", _sharpS + "-s", false)]
    [Arguments(@"(?fiu)(ss)\1{e<=1:[a-z]}", _sharpS + "s-", false)]
    [Arguments(@"(?firu)(ss)\1{e<=1:[a-z]}", "s-" + _sharpS, false)]
    [Arguments(@"(?firu)(ss)\1{e<=1:[a-z]}", "-s" + _sharpS, false)]
    [Property("Upstream", "RegexTests.test_fuzzy_ext#33-40")]
    public void Fuzzy_backreference_to_a_literal_ss_group_folds_a_subject_sharp_s_within_the_constraint(
        string pattern,
        string subject,
        bool expected
    ) => Upstream.FullMatch(subject, pattern).Success.Should().Be(expected);
}
