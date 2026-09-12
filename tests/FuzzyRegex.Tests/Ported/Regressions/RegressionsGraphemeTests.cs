using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about <c>\X</c> grapheme-cluster matching.
/// </summary>
public sealed class RegressionsGraphemeTests
{
    // U+1F468 MAN, a surrogate pair in UTF-16.
    private const string _man = "\U0001F468";

    // U+1F469 WOMAN, a surrogate pair in UTF-16.
    private const string _woman = "\U0001F469";

    // U+1F467 GIRL, a surrogate pair in UTF-16.
    private const string _girl = "\U0001F467";

    // U+1F466 BOY, a surrogate pair in UTF-16.
    private const string _boy = "\U0001F466";

    // U+200D ZERO WIDTH JOINER.
    private const string _zwj = "\u200D";

    // U+2103 DEGREE CELSIUS SIGN.
    private const string _degreeCelsius = "℃";

    // Hg issue 138: grapheme anchored search not working properly.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#142")]
    public void Grapheme_cluster_anchored_at_end_matches_the_final_degree_celsius_sign() =>
        FuzzyRegex.Match("ab" + _degreeCelsius, @"\X$").Value.Should().Be(_degreeCelsius);

    // Hg issue 312: \X not matching graphemes with zero-width-joins.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#373")]
    public void Grapheme_cluster_treats_a_ZWJ_joined_family_emoji_sequence_as_one_unit()
    {
        // Family emoji built from four non-BMP code points joined by ZWJ: upstream counts 7 code
        // points, .NET counts 11 UTF-16 code units, but this assertion only compares matched
        // text, so the value carries across unchanged.
        string subject = _man + _zwj + _woman + _zwj + _girl + _zwj + _boy;

        FuzzyRegex.Matches(subject, @"\X").Select(static m => m.Value).Should().Equal(subject);
    }
}
