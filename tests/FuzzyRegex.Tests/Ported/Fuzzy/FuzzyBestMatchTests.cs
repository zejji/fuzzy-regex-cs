using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fuzzy</c> (lines 2612-2804).
/// </summary>
/// <remarks>
/// <para>
/// The <c>(?b)</c> assertions. Upstream's <c>BESTMATCH</c> looks for the best fuzzy match rather
/// than the first one, so each of these has an unflagged sibling elsewhere in <c>test_fuzzy</c>
/// that lands somewhere else; the sibling's index is named beside each case.
/// </para>
/// <para>
/// Spans read back from the local Python oracle on 2026-08-30.
/// </para>
/// </remarks>
public sealed class FuzzyBestMatchTests
{
    // Unflagged siblings: #12 and #16, which both settle on (0, 0).
    [Test]
    [Arguments("(?b)(fuu){i<=3,d<=3,e<=5}")]
    [Arguments("(?b)(fuu){i<=3,d<=3,e}")]
    [Property("Upstream", "RegexTests.test_fuzzy#13,17")]
    public void BestMatch_prefers_a_real_match_over_the_empty_one_at_the_start(string pattern)
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Anaconda, pattern);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((9, 10));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((9, 10));
    }

    // Unflagged sibling: #19, which settles on (0, 6).
    [Test]
    [Property("Upstream", "RegexTests.test_fuzzy#21")]
    public void BestMatch_finds_the_closest_occurrence_rather_than_the_leftmost()
    {
        Match m = FuzzyRegex.Match("xirefoabralfobarxie", "(?b)(foobar){e}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((11, 16));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((11, 16));
    }

    // Unflagged sibling: #26, which settles on (0, 6).
    [Test]
    [Property("Upstream", "RegexTests.test_fuzzy#27")]
    public void BestMatch_finds_the_closest_whole_word()
    {
        Match m = FuzzyRegex.Match("boing zfoobarz goobar woop", "(?b)\\b(foobar){e}\\b");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((15, 21));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((15, 21));
    }

    // Unflagged sibling: #43, which settles on (6, 13).
    [Test]
    [Property("Upstream", "RegexTests.test_fuzzy#44")]
    public void BestMatch_applies_under_a_weighted_cost_equation()
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Scattered, "(?b)(foobar){i<=1,d<=2,s<=3,2d+1s<4}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((34, 39));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((34, 39));
    }
}
