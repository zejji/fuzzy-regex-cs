using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Surrogates;

/// <summary>
/// Pins the UTF-16 view of a non-BMP character matched by a Unicode property class. U+1F63A
/// SMILING CAT FACE WITH OPEN MOUTH is one code point to Python and one <c>\p{So}</c> match, but
/// two <c>char</c> code units in .NET, so a correct implementation must report a match of
/// <c>Length</c> 2 here, not 1.
/// </summary>
/// <remarks>
/// This is a gap test, not a ported one: it pins our own UTF-16 semantics rather than an upstream
/// assertion, so it does not count towards the parity percentage. The ported assertion it sits
/// beside is <c>RegressionsUnicodePropertyTests</c>, <c>RegexTests.test_hg_bugs#434</c> (Git issue
/// 473), where upstream's codepoint span <c>(0, 1)</c> becomes <c>(0, 2)</c> in UTF-16.
/// Verified against the local oracle on 2026-08-30:
/// <c>regex.match(r'^\p{So}+$', '\N{SMILING CAT FACE WITH OPEN MOUTH}').span()</c> is
/// <c>(0, 1)</c> over one code point, and that code point encodes to two UTF-16 code units.
/// </remarks>
public sealed class UnicodePropertySurrogateSpanTests
{
    // U+1F63A SMILING CAT FACE WITH OPEN MOUTH. Built with ConvertFromUtf32 rather than written
    // as a literal, because an astral emoji does not survive every editor and tool path intact.
    private static readonly string _smilingCatFaceWithOpenMouth = char.ConvertFromUtf32(0x1F63A);

    [Test]
    [Property("Upstream", "none - gap test")]
    public void Other_symbol_property_matches_a_non_BMP_emoji_as_two_UTF16_code_units()
    {
        _smilingCatFaceWithOpenMouth.Length.Should().Be(2);

        Match m = FuzzyRegex.MatchAtStart(_smilingCatFaceWithOpenMouth, @"^\p{So}+$");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }
}
