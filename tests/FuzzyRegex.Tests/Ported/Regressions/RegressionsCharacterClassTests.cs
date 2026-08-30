using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about POSIX bracket classes (<c>[[:alpha:]]</c> etc.) and nested character-set operations
/// under <c>(?V1)</c>.
/// </summary>
/// <remarks>
/// Assertions #114-127 wrap both sides of the comparison in Python's <c>ascii(...)</c>, which only
/// makes a failed assertion's message printable and has no C# counterpart, so it is dropped here.
/// </remarks>
public sealed class RegressionsCharacterClassTests
{
    // U+212A KELVIN SIGN, rendered identically to Latin capital K in most fonts.
    private const string _kelvinSign = "\u212A";

    // Every BMP code point (including lone surrogates, as Python's `chr(c) for c in
    // range(0x10000)` also produces), used to compare a POSIX bracket class against its
    // `\p{...}` equivalent over the same subject.
    private static readonly string _bmpCodePoints = new([.. Enumerable.Range(0, 0x10000).Select(c => (char)c)]);

    // Hg issue 63: regex.search("[[:ascii:]]", "\N{KELVIN SIGN}", flags=regex.I|regex.V1) doesn't
    // return None.
    [Test]
    [Skip("needs:character-classes - POSIX bracket classes like [[:ascii:]] are not parsed yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#46")]
    public void Case_insensitive_ascii_posix_class_does_not_match_the_kelvin_sign() =>
        FuzzyRegex.Match(_kelvinSign, "(?i)[[:ascii:]]").Success.Should().BeFalse();

    // Hg issue 137: Posix character class :punct: does not seem to be supported. Posix
    // compatibility as recommended in https://www.unicode.org/reports/tr18/#Compatibility_Properties.
    [Test]
    [Skip("needs:character-classes - POSIX bracket classes like [[:alpha:]] are not parsed yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#114-127")]
    [Arguments(@"[[:alnum:]]+", @"[\p{Alpha}\p{PosixDigit}]+")]
    [Arguments(@"[[:alpha:]]+", @"\p{Alpha}+")]
    [Arguments(@"[[:ascii:]]+", @"[\p{InBasicLatin}]+")]
    [Arguments(@"[[:blank:]]+", @"[\p{gc=Space_Separator}\t]+")]
    [Arguments(@"[[:cntrl:]]+", @"\p{gc=Control}+")]
    [Arguments(@"[[:digit:]]+", @"[0-9]+")]
    [Arguments(@"[[:graph:]]+", @"[^\p{Space}\p{gc=Control}\p{gc=Surrogate}\p{gc=Unassigned}]+")]
    [Arguments(@"[[:lower:]]+", @"\p{Lower}+")]
    [Arguments(@"[[:print:]]+", @"(?V1)[\p{Graph}\p{Blank}--\p{Cntrl}]+")]
    [Arguments(@"[[:punct:]]+", @"(?V1)[\p{gc=Punctuation}\p{gc=Symbol}--\p{Alpha}]+")]
    [Arguments(@"[[:space:]]+", @"\p{Whitespace}+")]
    [Arguments(@"[[:upper:]]+", @"\p{Upper}+")]
    [Arguments(@"[[:word:]]+", @"[\p{Alpha}\p{gc=Mark}\p{Digit}\p{gc=Connector_Punctuation}\p{Join_Control}]+")]
    [Arguments(@"[[:xdigit:]]+", @"[0-9A-Fa-f]+")]
    public void Posix_bracket_class_selects_the_same_characters_as_its_property_equivalent(
        string posixPattern,
        string propertyPattern
    )
    {
        string posixMatches = string.Concat(FuzzyRegex.Matches(_bmpCodePoints, posixPattern).Select(m => m.Value));
        string propertyMatches = string.Concat(
            FuzzyRegex.Matches(_bmpCodePoints, propertyPattern).Select(m => m.Value)
        );

        posixMatches.Should().Be(propertyMatches);
    }

    // Git issue 584: AttributeError: 'AnyAll' object has no attribute 'positive'.
    [Test]
    [Skip("needs:character-classes - alternating character classes like (\\s|\\S) fail to compile")]
    [Property("Upstream", "RegexTests.test_hg_bugs#497")]
    public void Pattern_alternating_two_character_classes_compiles()
    {
        Action act = () => _ = new FuzzyRegex(@"(\s|\S)");

        act.Should().NotThrow();
    }

    // Git PR 585: Fix AttributeError: 'AnyAll' object has no attribute '_key'.
    [Test]
    [Skip("needs:character-classes - alternating character classes like [\\S\\s] fail to compile")]
    [Property("Upstream", "RegexTests.test_hg_bugs#498")]
    public void Pattern_alternating_a_negated_and_a_positive_character_class_group_compiles()
    {
        Action act = () => _ = new FuzzyRegex(@"(?:[\S\s]|[A-D][M-Z])");

        act.Should().NotThrow();
    }

    // Hg issue 131: nested sets behaviour.
    [Test]
    [Skip("needs:set-operations - the (?V1) nested-set \"--\" difference operator is not implemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#107-110")]
    [Arguments(@"(?V1)[[b-e]--cd]")]
    [Arguments(@"(?V1)[b-e--cd]")]
    [Arguments(@"(?V1)[[bcde]--cd]")]
    [Arguments(@"(?V1)[bcde--cd]")]
    public void Nested_set_difference_syntax_excludes_the_subtracted_characters(string pattern) =>
        FuzzyRegex.Matches("abcdef", pattern).Select(m => m.Value).Should().Equal("b", "e");

    // Git issue 551: a single "-" inside a nested set is a literal character, only "--" performs
    // set difference, and the operand order of "--" matters.
    [Test]
    [Skip("needs:set-operations - the (?V1) nested-set \"--\" difference operator is not implemented")]
    [Property("Upstream", "RegexTests.test_hg_bugs#467-473")]
    [Arguments(@"(?V1)[[\s\S]]", true)]
    [Arguments(@"(?V1)[[\s\S]-a]", true)]
    [Arguments(@"(?V1)[[\s\S]--a]", false)]
    [Arguments(@"(?V1)[[a-z]--b]", true)]
    [Arguments(@"(?V1)[[\s\S]--b]", true)]
    [Arguments(@"(?V1)[a-[\s\S]]", true)]
    [Arguments(@"(?V1)[a--[\s\S]]", false)]
    public void Nested_set_operand_order_and_single_versus_double_dash_change_the_match(
        string pattern,
        bool expectedMatch
    ) => FuzzyRegex.MatchAtStart("a", pattern).Success.Should().Be(expectedMatch);
}
