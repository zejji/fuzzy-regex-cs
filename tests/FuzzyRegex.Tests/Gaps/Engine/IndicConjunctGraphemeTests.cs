using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// <c>\X</c> and the Indic conjunct rule GB9c as Unicode 18.0.0 states it.
/// </summary>
/// <remarks>
/// <para>
/// UAX #29 revision 49, which is Unicode 18.0.0's, changed GB9c from
/// <c>\p{InCB=Consonant} [\p{InCB=Extend}\p{InCB=Linker}]* \p{InCB=Linker}
/// [\p{InCB=Extend}\p{InCB=Linker}]* × \p{InCB=Consonant}</c> to
/// <c>\p{InCB=Linker} \p{InCB=Extend}* × \p{InCB=Consonant}</c>: a linker keeps the next consonant in
/// its cluster whatever comes before it. Upstream 2026.9.29 moved its tables to 18.0.0 and kept the
/// old rule (<c>unicode_at_grapheme_boundary</c>, <c>upstream/src/_regex.c</c>, unchanged in that
/// release apart from type names), so it fails 10 of the 853 lines of 18.0.0's
/// <c>GraphemeBreakTest.txt</c>, these 10, and passes the rest (measured 2026-10-18). With the old
/// rule and the new data <c>\X</c> follows neither version.
/// </para>
/// <para>
/// The rows are the file's own, with its line number, each checked forwards and backwards.
/// </para>
/// </remarks>
public sealed class IndicConjunctGraphemeTests
{
    // DIVERGES FROM UPSTREAM, deliberately: upstream breaks after the linker on each row.
    [Test]
    [Property("Upstream", "none - gap test")]
    // A linker at the start of the text, then a consonant.
    [Arguments(179, new[] { "्क" })]
    [Arguments(180, new[] { "्̈क" })]
    // VEDIC SIGN JIHVAMULIYA is a linker from 18.0.
    [Arguments(659, new[] { "ᳵक" })]
    [Arguments(660, new[] { "ᳵ̈क" })]
    [Arguments(875, new[] { "ᳵক" })]
    [Arguments(876, new[] { "ᳶপ" })]
    // A linker after something that is not a consonant.
    [Arguments(859, new[] { "a्त" })]
    [Arguments(860, new[] { "?्त" })]
    // Balinese and Zanabazar Square, whose viramas are linkers from 18.0.
    [Arguments(871, new[] { "ᬅ᭄ᬳᬃ" })]
    [Arguments(879, new[] { "\U00011A3A\U00011A0B" })]
    public void A_linker_keeps_the_next_consonant_in_its_cluster(int line, string[] clusters)
    {
        _ = line;
        string subject = string.Concat(clusters);

        Values(@"\X", subject).Should().Equal(clusters);
        Values(@"(?r)\X", subject).Should().Equal(clusters.Reverse());
    }

    private static string[] Values(string pattern, string subject) =>
        [.. new FuzzyRegex(pattern).Matches(subject).Select(static m => m.Value)];
}
