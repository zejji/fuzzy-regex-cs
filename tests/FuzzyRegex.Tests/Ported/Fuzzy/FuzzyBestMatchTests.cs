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
    //
    // THE ONE PORTED TEST THE COST RULE TURNS, AND IT ASSERTS THIS PORT'S ANSWER RATHER THAN
    // UPSTREAM'S. Upstream answers (34, 39) with one substitution and one deletion; this port
    // answers (26, 33) with two substitutions and one insertion. The owner's rule is that ranking
    // goes by COST (DECISIONS 2026-09-12), and under this pattern's own equation - '2d+1s<4', which
    // prices an insertion at nothing because it names no 'i' term - upstream's answer costs
    // 2*1 + 1*1 = 3 and this port's costs 1*2 = 2.
    //
    // UPSTREAM'S OWN ENGINE IS THE PROOF THAT THE CHEAPER MATCH IS REAL, which matters because S42's
    // first sitting recorded the opposite. Its brute force ranked the candidates 'finditer' returns,
    // and those are already error-minimised per position, so the cost-2 match was never in the set
    // it ranked. Tighten the equation until only a cost-2 match can satisfy it and upstream finds
    // exactly this span - regex 2026.7.19, 2026-09-13:
    //
    //   (foobar){i<=1,d<=2,s<=3,2d+1s<4}  search (6, 13) (3, 1, 0)   best (34, 39) (1, 0, 1)
    //   (foobar){i<=1,d<=2,s<=3,2d+1s<3}  search (26, 33) (2, 1, 0)  best (26, 33) (2, 1, 0)
    //   (foobar){i<=1,d<=2,s<=3,2d+1s<2}  search None                best None
    //
    // So cost 2 is reachable, cost 1 is not, and (26, 33) is the cheapest match in the subject.
    // Pinned as 'bestmatch-ranks-by-cost' in the oracle's ExpectedDivergences.
    [Test]
    [Property("Upstream", "RegexTests.test_fuzzy#44")]
    public void BestMatch_applies_under_a_weighted_cost_equation()
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Scattered, "(?b)(foobar){i<=1,d<=2,s<=3,2d+1s<4}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((26, 33));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((26, 33));
    }

    // The control for the case above: the same pattern with a UNIT cost equation answers what
    // upstream answers, which is what confines the divergence to weighted equations. Measured on
    // regex 2026.7.19, 2026-09-13:
    //
    //   (?b)(foobar){i<=1,d<=2,s<=3,1i+1d+1s<4}  best (34, 39) (1, 0, 1)
    //
    // The equation names all three kinds on purpose. '2d+1s<4' above and '1d+1s<4' are BOTH weighted,
    // because a term the equation omits costs NOTHING rather than one - which is why the case above
    // diverges on an insertion this port gets for free, and why a '1d+1s' control would have been no
    // control at all. Found by running the negative control for the cost bound: '1d+1s<4' took the
    // cost walk and hung with the bound removed, which a genuinely unit equation cannot do.
    [Test]
    public void BestMatch_under_a_unit_cost_equation_answers_what_upstream_answers()
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Scattered, "(?b)(foobar){i<=1,d<=2,s<=3,1i+1d+1s<4}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((34, 39));
    }
}
