using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.UnicodeProperties;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_properties</c> (lines 1123-1184):
/// the table-driven block that runs each class over a fixed subject and joins what it finds, plus
/// the three <c>NumericValue</c> assertions that close the method.
/// </summary>
/// <remarks>
/// <para>
/// Upstream drives the table with a loop that calls <c>self.fail</c>, so there are no numbered
/// assertions inside it; the whole table is provenance <c>#73</c>, one C# case per row.
/// </para>
/// <para>
/// Only the 18 <c>str</c> rows port. The 28 <c>bytes</c> rows - upstream's <c>(?L)</c> and
/// <c>(?a)</c> blocks at lines 1146-1176 - are omitted because this port is <c>char</c>-based, and
/// are recorded in <c>docs/PORTMAP.md</c>. Upstream's <c>word_set</c> local (line 1125) is dead
/// code there and has nothing to port.
/// </para>
/// <para>
/// The non-ASCII characters are written literally: GREEK CAPITAL LETTER GAMMA is U+0393, GREEK
/// SMALL LETTER GAMMA is U+03B3 and LATIN SMALL LETTER A WITH ACUTE is U+00E1. All three are in
/// the Basic Multilingual Plane, so each is one <c>char</c> and no index shifts.
/// </para>
/// </remarks>
public sealed class PosixAndPropertyClassTableTests
{
    /// <summary>Upstream's <c>chars_u</c>: hyphen, 0, 9, A, Z, a, z, underscore, U+0393, U+03B3.</summary>
    private const string _charsU = "-09AZaz_Γγ";

    /// <summary>Upstream's second subject: <c>a</c> followed by U+00E1.</summary>
    private const string _latinPair = "aá";

    [Test]
    [Arguments(@"\w", _charsU, "09AZaz_Γγ")]
    [Arguments("[[:word:]]", _charsU, "09AZaz_Γγ")]
    [Arguments(@"\W", _charsU, "-")]
    [Arguments("[[:^word:]]", _charsU, "-")]
    [Arguments(@"\d", _charsU, "09")]
    [Arguments("[[:digit:]]", _charsU, "09")]
    [Arguments(@"\D", _charsU, "-AZaz_Γγ")]
    [Arguments("[[:^digit:]]", _charsU, "-AZaz_Γγ")]
    [Arguments("[[:alpha:]]", _charsU, "AZazΓγ")]
    [Arguments("[[:^alpha:]]", _charsU, "-09_")]
    [Arguments("[[:alnum:]]", _charsU, "09AZazΓγ")]
    [Arguments("[[:^alnum:]]", _charsU, "-_")]
    [Arguments("[[:xdigit:]]", _charsU, "09Aa")]
    [Arguments("[[:^xdigit:]]", _charsU, "-Zz_Γγ")]
    [Arguments(@"\p{InBasicLatin}", _latinPair, "a")]
    [Arguments(@"\P{InBasicLatin}", _latinPair, "á")]
    [Arguments(@"(?i)\p{InBasicLatin}", _latinPair, "a")]
    [Arguments(@"(?i)\P{InBasicLatin}", _latinPair, "á")]
    [Skip("needs:unicode-properties - the engine has no Unicode property tables yet")]
    [Property("Upstream", "RegexTests.test_properties#73")]
    public void Class_finds_exactly_its_members_in_the_subject(string pattern, string subject, string expected) =>
        string.Concat(FuzzyRegex.Matches(subject, pattern).Select(m => m.Value)).Should().Be(expected);

    [Test]
    [Skip("needs:unicode-properties - the engine has no Unicode property tables yet")]
    [Property("Upstream", "RegexTests.test_properties#70")]
    public void Numeric_value_zero_matches_the_digit_zero() =>
        new FuzzyRegex(@"\p{NumericValue=0}").IsMatchAtStart("0").Should().BeTrue();

    /// <remarks>
    /// The subject is VULGAR FRACTION ONE HALF, U+00BD. Upstream asserts the same character
    /// matches the value written as a fraction and as a decimal.
    /// </remarks>
    [Test]
    [Arguments(@"\p{NumericValue=1/2}")]
    [Arguments(@"\p{NumericValue=0.5}")]
    [Skip("needs:unicode-properties - the engine has no Unicode property tables yet")]
    [Property("Upstream", "RegexTests.test_properties#71-72")]
    public void Numeric_value_one_half_matches_the_vulgar_fraction(string pattern) =>
        new FuzzyRegex(pattern).IsMatchAtStart(((char)0x00BD).ToString()).Should().BeTrue();
}
