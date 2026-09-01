using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CharacterClasses;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_set</c> (lines 1664-1739).
/// </summary>
/// <remarks>
/// <c>self.assertEqual(repr(type(regex.compile(r"(?V0)([][-])"))), self.PATTERN_CLASS)</c>
/// (upstream lines 1732-1733, assertion #38) is a Python type-identity check and is not ported.
/// </remarks>
public sealed class SetTests
{
    // The 256 byte values 0x00-0xFF, built from a loop rather than pasted as literal characters
    // (many of which are unprintable control characters).
    private static readonly string _allChars = new([.. Enumerable.Range(0, 0x100).Select(c => (char)c)]);

    [Test]
    [Arguments("[a]", "a", 0, 1)]
    [Arguments("[a-b]", "a", 0, 1)]
    [Property("Upstream", "RegexTests.test_set#1,3")]
    public void Character_class_matches_a_single_char_or_a_range(string pattern, string subject, int start, int end)
    {
        Match m = FuzzyRegex.MatchAtStart(subject, pattern);

        m.Index.Should().Be(start);
        m.Length.Should().Be(end - start);
    }

    // Split from the two rows above at S17, which delivered the case-sensitive halves: these two
    // compile to CHARACTER_IGN and RANGE_IGN, which are S22's.
    [Test]
    [Arguments("(?i)[a]", "A", 0, 1)]
    [Arguments("(?i)[a-b]", "A", 0, 1)]
    [Property("Upstream", "RegexTests.test_set#2,4")]
    public void Case_insensitive_character_class_matches_a_single_char_or_a_range(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, pattern);

        m.Index.Should().Be(start);
        m.Length.Should().Be(end - start);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_set#5")]
    public void Version0_flag_allows_literal_brackets_inside_a_character_class() =>
        FuzzyRegex.Replace("a[b]c", @"(?V0)([][])", "-").Should().Be("a-b-c");

    [Test]
    [Arguments(@"[\p{Alpha}]", "a0", "a")]
    [Arguments(@"(?i)[\p{Alpha}]", "A0", "A")]
    [Property("Upstream", "RegexTests.test_set#6-7")]
    public void Alpha_property_findall_matches_only_the_letter(string pattern, string subject, string expected) =>
        FuzzyRegex.Matches(subject, pattern).Select(m => m.Value).Should().Equal(expected);

    [Test]
    [Arguments(@"[a\p{Alpha}]", "ab0", "a", "b")]
    [Arguments(@"[a\P{Alpha}]", "ab0", "a", "0")]
    [Arguments(@"(?i)[a\p{Alpha}]", "ab0", "a", "b")]
    [Arguments(@"(?i)[a\P{Alpha}]", "ab0", "a", "0")]
    [Property("Upstream", "RegexTests.test_set#8-11")]
    public void Literal_char_plus_a_property_or_its_negation_findall(
        string pattern,
        string subject,
        string first,
        string second
    ) => FuzzyRegex.Matches(subject, pattern).Select(m => m.Value).Should().Equal(first, second);

    [Test]
    [Arguments(@"[a-b\p{Alpha}]", "abC0", "a", "b", "C")]
    [Arguments(@"(?i)[a-b\p{Alpha}]", "AbC0", "A", "b", "C")]
    [Property("Upstream", "RegexTests.test_set#12-13")]
    public void Range_plus_a_property_findall(
        string pattern,
        string subject,
        string first,
        string second,
        string third
    ) => FuzzyRegex.Matches(subject, pattern).Select(m => m.Value).Should().Equal(first, second, third);

    [Test]
    [Arguments(@"[\p{Alpha}]", "a0", "a")]
    [Arguments(@"[\P{Alpha}]", "a0", "0")]
    [Arguments(@"[^\p{Alpha}]", "a0", "0")]
    [Arguments(@"[^\P{Alpha}]", "a0", "a")]
    [Property("Upstream", "RegexTests.test_set#14-17")]
    public void Alpha_property_and_its_negations_findall(string pattern, string subject, string expected) =>
        FuzzyRegex.Matches(subject, pattern).Select(m => m.Value).Should().Equal(expected);

    [Test]
    [Arguments(@"[^\d-h]", "a^b12c-h", "a^bc")]
    [Arguments(@"[^\dh]", "a^b12c-h", "a^bc-")]
    [Arguments(@"[^h\s\db]", "a^b 12c-h", "a^c-")]
    [Arguments(@"[^b\w]", "a b", " ")]
    [Arguments(@"[^b\S]", "a b", " ")]
    [Arguments(@"[^8\d]", "a 1b2", "a b")]
    [Property("Upstream", "RegexTests.test_set#18-23")]
    public void Negated_character_class_findall_joined(string pattern, string subject, string expectedJoined) =>
        string.Concat(FuzzyRegex.Matches(subject, pattern).Select(m => m.Value)).Should().Be(expectedJoined);

    [Test]
    [Arguments(@"\p{ASCII}", 128)]
    [Arguments(@"\p{Letter}", 117)]
    [Arguments(@"\p{Digit}", 10)]
    [Arguments(@"\p{HexDigit}", 22)]
    [Property("Upstream", "RegexTests.test_set#24-26,35")]
    public void Property_findall_count_over_every_byte_value(string pattern, int expectedCount) =>
        FuzzyRegex.Count(_allChars, pattern).Should().Be(expectedCount);

    [Test]
    [Property("Upstream", "RegexTests.test_set#33")]
    public void Two_properties_side_by_side_inside_one_class_is_an_implicit_union() =>
        FuzzyRegex.Count(_allChars, @"[\p{Letter}\p{Digit}]").Should().Be(127);

    [Test]
    [Arguments(@"(?V1)[\p{ASCII}&&\p{Letter}]", 52)]
    [Arguments(@"(?V1)[\p{ASCII}&&\p{Alnum}&&\p{Letter}]", 52)]
    [Arguments(@"(?V1)[\p{ASCII}&&\p{Alnum}&&\p{Digit}]", 10)]
    [Arguments(@"(?V1)[\p{ASCII}&&\p{Cc}]", 33)]
    [Arguments(@"(?V1)[\p{ASCII}&&\p{Graph}]", 94)]
    [Arguments(@"(?V1)[\p{ASCII}--\p{Cc}]", 95)]
    [Arguments(@"(?V1)[\p{Letter}||\p{Digit}]", 127)]
    [Arguments(@"(?V1)[\p{HexDigit}~~\p{Digit}]", 12)]
    [Arguments(@"(?V1)[\p{Digit}~~\p{HexDigit}]", 12)]
    [Property("Upstream", "RegexTests.test_set#27-32,34,36-37")]
    public void Set_operators_over_properties_count_over_every_byte_value(string pattern, int expectedCount) =>
        FuzzyRegex.Count(_allChars, pattern).Should().Be(expectedCount);

    [Test]
    [Arguments("(?V1)[[a-z]--[aei]]", "abc", "b", "c")]
    [Arguments("(?iV1)[[a-z]--[aei]]", "abc", "b", "c")]
    [Arguments(@"(?V1)[\w--a]", "abc", "b", "c")]
    [Arguments(@"(?iV1)[\w--a]", "abc", "b", "c")]
    [Property("Upstream", "RegexTests.test_set#39-42")]
    public void Set_difference_findall(string pattern, string subject, string first, string second) =>
        FuzzyRegex.Matches(subject, pattern).Select(m => m.Value).Should().Equal(first, second);
}
