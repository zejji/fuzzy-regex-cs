using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fuzzy</c> (lines 2612-2804).
/// </summary>
/// <remarks>
/// <para>
/// The <c>(?e)</c> assertions. Upstream's <c>ENHANCEMATCH</c> keeps the match it found and then
/// tries to improve its fit, which is why each of these differs from its unflagged sibling in the
/// span of the inner group rather than in whether anything matched at all.
/// </para>
/// <para>
/// Spans and lists read back from the local Python oracle on 2026-08-30.
/// </para>
/// </remarks>
public sealed class FuzzyEnhanceMatchTests
{
    // Unflagged sibling: #14, which settles on (7, 10).
    [Test]
    [Skip("needs:fuzzy-enhancematch - the engine has no ENHANCEMATCH pass yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#15")]
    public void EnhanceMatch_tightens_a_match_found_under_per_kind_caps()
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Anaconda, "(?e)(fuu){i<=2,d<=2,e<=5}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((9, 10));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((9, 10));
    }

    // Unflagged sibling: #19, which settles on (0, 6).
    [Test]
    [Skip("needs:fuzzy-enhancematch - the engine has no ENHANCEMATCH pass yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#20")]
    public void EnhanceMatch_shortens_an_unbounded_error_match()
    {
        Match m = FuzzyRegex.Match("xirefoabralfobarxie", "(?e)(foobar){e}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 3));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((0, 3));
    }

    // Unflagged sibling: #48, whose group 1 collapses to the empty span (120, 120).
    [Test]
    [Skip("needs:fuzzy-enhancematch - the engine has no ENHANCEMATCH pass yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#49")]
    public void EnhanceMatch_moves_the_inner_group_onto_the_text_it_should_have_matched()
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Hosts, "(?es)^.*(dot.org){e}.*$");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, 120));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((93, 100));
    }

    // Unflagged sibling: #52, which yields [" dog", "cot"] - the leading space is what
    // ENHANCEMATCH removes here.
    [Test]
    [Skip("needs:fuzzy-enhancematch - needs ENHANCEMATCH and named lists; the engine has neither yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#53")]
    public void EnhanceMatch_trims_a_fuzzy_named_list_match()
    {
        MatchCollection matches = FuzzyRegex.Matches(
            " book dog cot desk ",
            "(?e)\\b\\L<words>{e<=1}\\b",
            FuzzyRegexOptions.None,
            FuzzyTestData.Words
        );

        matches.Select(m => m.Value).Should().Equal("dog", "cot");
    }

    // Unflagged sibling: #54, which yields ["dog ", "cot"] - again a stray space.
    [Test]
    [Skip(
        "needs:fuzzy-enhancematch - needs ENHANCEMATCH, right-to-left search and named lists; the engine has none of them yet"
    )]
    [Property("Upstream", "RegexTests.test_fuzzy#55")]
    public void EnhanceMatch_trims_a_fuzzy_named_list_match_searching_backwards()
    {
        MatchCollection matches = FuzzyRegex.Matches(
            " book cot dog desk ",
            "(?er)\\b\\L<words>{e<=1}\\b",
            FuzzyRegexOptions.None,
            FuzzyTestData.Words
        );

        matches.Select(m => m.Value).Should().Equal("dog", "cot");
    }
}
