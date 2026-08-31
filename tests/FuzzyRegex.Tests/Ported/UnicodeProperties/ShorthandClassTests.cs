using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.UnicodeProperties;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_properties</c> (lines 1012 and
/// 1087-1110): the shorthand classes and their negations on <c>str</c> subjects, where upstream
/// defines them by Unicode property rather than by ASCII range.
/// </summary>
/// <remarks>
/// The <c>bytes</c> counterparts of these assertions - upstream's <c>(?a)</c> and <c>(?L)</c> rows
/// at lines 1009-1031 - are not ported; this port is <c>char</c>-based. They are listed in
/// <c>docs/PORTMAP.md</c>.
/// </remarks>
public sealed class ShorthandClassTests
{
    /// <summary>LATIN SMALL LETTER A WITH GRAVE, U+00E0 - a letter outside ASCII.</summary>
    private static readonly string _aGrave = ((char)0x00E0).ToString();

    [Test]
    [Property("Upstream", "RegexTests.test_properties#4")]
    public void Word_matches_a_non_ASCII_letter_because_the_default_is_Unicode() =>
        new FuzzyRegex(@"\w").IsMatchAtStart(_aGrave).Should().BeTrue();

    [Test]
    [Arguments(@"\d", "0")]
    [Arguments(@"\s", " ")]
    [Arguments(@"\w", "A")]
    [Arguments(@"\D", "?")]
    [Arguments(@"\S", "?")]
    [Arguments(@"\W", "?")]
    [Arguments(@"\w", "0")]
    [Arguments(@"\w", "a")]
    [Arguments(@"\w", "_")]
    [Property("Upstream", "RegexTests.test_properties#43-45, #52-54, #61-63")]
    public void Shorthand_class_matches_its_member(string pattern, string subject) =>
        new FuzzyRegex(pattern).IsMatchAtStart(subject).Should().BeTrue();

    [Test]
    [Arguments(@"\d", "?")]
    [Arguments(@"\s", "?")]
    [Arguments(@"\w", "?")]
    [Arguments(@"\D", "0")]
    [Arguments(@"\S", " ")]
    [Arguments(@"\W", "A")]
    [Property("Upstream", "RegexTests.test_properties#46-51")]
    public void Shorthand_class_does_not_match_a_non_member(string pattern, string subject) =>
        new FuzzyRegex(pattern).IsMatchAtStart(subject).Should().BeFalse();
}
