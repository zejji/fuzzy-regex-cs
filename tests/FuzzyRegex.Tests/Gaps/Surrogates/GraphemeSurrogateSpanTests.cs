using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Surrogates;

/// <summary>
/// Pins the UTF-16 view of a non-BMP grapheme cluster: a family emoji built from four
/// surrogate-pair code points joined by ZWJ is 7 code points in Python but 11 <c>char</c> code
/// units in .NET, so a correct <c>\X</c> implementation must report an 11-<c>char</c> match here,
/// not 7.
/// </summary>
/// <remarks>
/// This is a gap test, not a ported one: it pins our own UTF-16 semantics rather than an upstream
/// assertion, so it does not count towards the parity percentage. See
/// <c>RegressionsGraphemeTests.Grapheme_cluster_treats_a_ZWJ_joined_family_emoji_sequence_as_one_unit</c>
/// for the ported assertion (Hg issue 312) this subject also serves.
/// </remarks>
public sealed class GraphemeSurrogateSpanTests
{
    // U+1F468 MAN, a surrogate pair in UTF-16.
    private const string _man = "\U0001F468";

    // U+1F469 WOMAN, a surrogate pair in UTF-16.
    private const string _woman = "\U0001F469";

    // U+1F467 GIRL, a surrogate pair in UTF-16.
    private const string _girl = "\U0001F467";

    // U+1F466 BOY, a surrogate pair in UTF-16.
    private const string _boy = "\U0001F466";

    // U+200D ZERO WIDTH JOINER. Written as an escape because the character itself is invisible,
    // so a literal here would be indistinguishable from an empty string by eye.
    private const string _zwj = "\u200D";

    [Test]
    [Skip("needs:grapheme - \\X has no grapheme-cluster opcode yet")]
    [Property("Upstream", "none - gap test")]
    public void ZWJ_joined_family_emoji_is_one_grapheme_cluster_spanning_11_UTF16_code_units()
    {
        string subject = _man + _zwj + _woman + _zwj + _girl + _zwj + _boy;

        Match m = FuzzyRegex.MatchAtStart(subject, @"\X");

        m.Index.Should().Be(0);
        m.Length.Should().Be(11);
    }
}
