using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.UnicodeProperties;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_properties</c> (lines 1033-1084):
/// every spelling upstream accepts for a script or block property, positive and negated.
/// </summary>
/// <remarks>
/// Upstream names its subjects with Python's <c>N{...}</c> escape. They are written here as numeric
/// <c>char</c> casts so the intended code point is unambiguous in source: CYRILLIC CAPITAL LETTER A
/// is U+0410 and LATIN CAPITAL LETTER A is U+0041. Both are in the Basic Multilingual Plane, so one
/// <c>char</c> holds each and no index shifts between Python's code points and our UTF-16.
/// </remarks>
public sealed class CyrillicPropertyFormsTests
{
    /// <summary>CYRILLIC CAPITAL LETTER A, U+0410.</summary>
    private static readonly string _cyrillicA = ((char)0x0410).ToString();

    /// <summary>LATIN CAPITAL LETTER A, U+0041.</summary>
    private const string _latinA = "A";

    [Test]
    [Arguments(@"\p{Cyrillic}")]
    [Arguments(@"(?i)\p{Cyrillic}")]
    [Arguments(@"\p{IsCyrillic}")]
    [Arguments(@"\p{Script=Cyrillic}")]
    [Arguments(@"\p{InCyrillic}")]
    [Arguments(@"\p{Block=Cyrillic}")]
    [Arguments(@"[[:Cyrillic:]]")]
    [Arguments(@"[[:IsCyrillic:]]")]
    [Arguments(@"[[:Script=Cyrillic:]]")]
    [Arguments(@"[[:InCyrillic:]]")]
    [Arguments(@"[[:Block=Cyrillic:]]")]
    [Skip("needs:unicode-properties - the engine has no Unicode property tables yet")]
    [Property("Upstream", "RegexTests.test_properties#17-27")]
    public void Every_positive_spelling_of_the_Cyrillic_property_matches_a_Cyrillic_letter(string pattern) =>
        new FuzzyRegex(pattern).IsMatchAtStart(_cyrillicA).Should().BeTrue();

    [Test]
    [Arguments(@"\P{Cyrillic}")]
    [Arguments(@"\P{IsCyrillic}")]
    [Arguments(@"\P{Script=Cyrillic}")]
    [Arguments(@"\P{InCyrillic}")]
    [Arguments(@"\P{Block=Cyrillic}")]
    [Arguments(@"\p{^Cyrillic}")]
    [Arguments(@"\p{^IsCyrillic}")]
    [Arguments(@"\p{^Script=Cyrillic}")]
    [Arguments(@"\p{^InCyrillic}")]
    [Arguments(@"\p{^Block=Cyrillic}")]
    [Arguments(@"[[:^Cyrillic:]]")]
    [Arguments(@"[[:^IsCyrillic:]]")]
    [Arguments(@"[[:^Script=Cyrillic:]]")]
    [Arguments(@"[[:^InCyrillic:]]")]
    [Arguments(@"[[:^Block=Cyrillic:]]")]
    [Skip("needs:unicode-properties - the engine has no Unicode property tables yet")]
    [Property("Upstream", "RegexTests.test_properties#28-42")]
    public void Every_negated_spelling_of_the_Cyrillic_property_matches_a_Latin_letter(string pattern) =>
        new FuzzyRegex(pattern).IsMatchAtStart(_latinA).Should().BeTrue();
}
