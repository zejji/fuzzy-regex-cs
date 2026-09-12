using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Grapheme;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_grapheme</c>
/// (lines 1532-1542).
/// </summary>
/// <remarks>
/// All five of upstream's assertions here are duplicated verbatim inside <c>test_properties</c>
/// (lines 1112-1120, assertions #64-68), so this class is the only port of them: the
/// <c>test_properties</c> copies are recorded as omitted-because-duplicated in
/// <c>docs/PORTMAP.md</c> rather than ported a second time under a second provenance.
/// Every accented letter and combining mark is
/// held in a named constant whose comment gives its code points, because precomposed and decomposed
/// forms are indistinguishable by eye in the source: U+00E0 and 'a' followed by U+0300 both render
/// as a grave-accented a, and only the constant's name says which one a test means. All are in the
/// Basic Multilingual Plane, so each code point is one <c>char</c> and no index shifts.
/// </remarks>
public sealed class GraphemeTests
{
    // U+00E0 LATIN SMALL LETTER A WITH GRAVE (precomposed).
    private const string _precomposedAWithGrave = "à";

    // 'a' + U+0300 COMBINING GRAVE ACCENT (decomposed).
    private const string _decomposedAWithGrave = "à";

    // U+00E9 LATIN SMALL LETTER E WITH ACUTE (precomposed).
    private const string _precomposedEWithAcute = "é";

    // 'e' + U+0301 COMBINING ACUTE ACCENT (decomposed).
    private const string _decomposedEWithAcute = "é";

    // U+0301 COMBINING ACUTE ACCENT on its own, with no preceding base letter.
    private const string _bareCombiningAcute = "́";

    // 'A' + U+0301 COMBINING ACUTE ACCENT (decomposed).
    private const string _decomposedCapitalAWithAcute = "Á";

    [Test]
    [Property("Upstream", "RegexTests.test_grapheme#1")]
    public void Grapheme_cluster_matches_a_single_precomposed_a_with_grave()
    {
        Match m = FuzzyRegex.MatchAtStart(_precomposedAWithGrave, @"\X");

        m.Index.Should().Be(0);
        m.Length.Should().Be(1);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_grapheme#2")]
    public void Grapheme_cluster_matches_a_base_letter_plus_combining_grave_as_one_unit()
    {
        Match m = FuzzyRegex.MatchAtStart(_decomposedAWithGrave, @"\X");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_grapheme#3")]
    public void Grapheme_cluster_splits_a_mix_of_precomposed_and_combining_letters_into_clusters()
    {
        string subject =
            "a" + _precomposedAWithGrave + _decomposedAWithGrave + "e" + _precomposedEWithAcute + _decomposedEWithAcute;

        FuzzyRegex
            .Matches(subject, @"\X")
            .Select(static m => m.Value)
            .Should()
            .Equal(
                "a",
                _precomposedAWithGrave,
                _decomposedAWithGrave,
                "e",
                _precomposedEWithAcute,
                _decomposedEWithAcute
            );
    }

    [Test]
    [Property("Upstream", "RegexTests.test_grapheme#4")]
    public void Three_grapheme_clusters_group_the_same_mix_into_two_runs()
    {
        string subject =
            "a" + _precomposedAWithGrave + _decomposedAWithGrave + "e" + _precomposedEWithAcute + _decomposedEWithAcute;

        FuzzyRegex
            .Matches(subject, @"\X{3}")
            .Select(static m => m.Value)
            .Should()
            .Equal(
                "a" + _precomposedAWithGrave + _decomposedAWithGrave,
                "e" + _precomposedEWithAcute + _decomposedEWithAcute
            );
    }

    [Test]
    [Property("Upstream", "RegexTests.test_grapheme#5")]
    public void Grapheme_cluster_treats_CR_CRLF_and_a_combining_mark_after_a_letter_as_units()
    {
        string subject = "\r\r\n" + _bareCombiningAcute + _decomposedCapitalAWithAcute;

        FuzzyRegex
            .Matches(subject, @"\X")
            .Select(static m => m.Value)
            .Should()
            .Equal("\r", "\r\n", _bareCombiningAcute, _decomposedCapitalAWithAcute);
    }
}
