using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The scoped ASCII and Unicode flags at match time: a <c>PROPERTY</c> node compiled inside
/// <c>(?a:...)</c> or <c>(?u:...)</c> carries its own encoding in its status word and ignores the
/// pattern's, which is upstream's <c>ENCODING_KIND</c> (<c>upstream/src/_regex.c</c> line 167).
/// </summary>
/// <remarks>
/// <para>
/// Upstream's own suite pins <c>(?a:\d)</c> alone (<c>test_hg_bugs</c> #476); every other
/// combination it has reaches the answer through <c>findall</c>, which is S25. Without these the
/// engine could read the encoding bits for the one case upstream happens to test and ignore them
/// everywhere else, and no test would say so.
/// </para>
/// <para>
/// Every expected value is upstream's, recorded against regex 2026.7.19 on 2026-08-31 with
/// <c>regex.match(pattern, subject)</c> and quoted beside the assertion.
/// </para>
/// </remarks>
public sealed class ScopedEncodingTests
{
    /// <summary>U+FF19 FULLWIDTH DIGIT NINE: <c>Nd</c>, and above <c>RE_ASCII_MAX</c>.</summary>
    private const string _fullwidthNine = "\uFF19";

    /// <summary>U+00E9 LATIN SMALL LETTER E WITH ACUTE: a letter, and above <c>RE_ASCII_MAX</c>.</summary>
    private const string _eAcute = "\u00E9";

    /// <summary>U+212A KELVIN SIGN: <c>Lu</c>, and well above it.</summary>
    private const string _kelvinSign = "\u212A";

    [Test]
    // upstream: regex.match(r'(?a:\d)', '\uff19') is None
    [Arguments(@"(?a:\d)", _fullwidthNine, false)]
    // upstream: regex.match(r'(?u:\d)', '\uff19').span() == (0, 1)
    [Arguments(@"(?u:\d)", _fullwidthNine, true)]
    // A scoped flag wins over the pattern's, in both directions.
    // upstream: regex.match(r'(?a)(?u:\d)', '\uff19').span() == (0, 1)
    [Arguments(@"(?a)(?u:\d)", _fullwidthNine, true)]
    // upstream: regex.match(r'(?u)(?a:\d)', '\uff19') is None
    [Arguments(@"(?u)(?a:\d)", _fullwidthNine, false)]
    // upstream: regex.match(r'(?a)\d', '\uff19') is None
    [Arguments(@"(?a)\d", _fullwidthNine, false)]
    // The same node inside a set, so matches_member's own ENCODING_KIND switch is the one under
    // test rather than matches_PROPERTY's.
    // upstream: regex.match(r'(?a:[\d])', '\uff19') is None
    [Arguments(@"(?a:[\d])", _fullwidthNine, false)]
    // upstream: regex.match(r'(?a:[^\d])', '\uff19').span() == (0, 1)
    [Arguments(@"(?a:[^\d])", _fullwidthNine, true)]
    // upstream: regex.match(r'(?a:\p{L})', '\u00e9') is None
    [Arguments(@"(?a:\p{L})", _eAcute, false)]
    // upstream: regex.match(r'(?u:\p{L})', '\u00e9').span() == (0, 1)
    [Arguments(@"(?u:\p{L})", _eAcute, true)]
    // upstream: regex.match(r'(?a)[[:alpha:]]', '\u00e9') is None
    [Arguments("(?a)[[:alpha:]]", _eAcute, false)]
    // upstream: regex.match(r'(?a)\p{L}', '\u212a') is None
    [Arguments(@"(?a)\p{L}", _kelvinSign, false)]
    public void The_encoding_a_property_node_was_compiled_under_decides_its_answer(
        string pattern,
        string subject,
        bool expected
    ) => FuzzyRegex.MatchAtStart(subject, pattern).Success.Should().Be(expected);
}
