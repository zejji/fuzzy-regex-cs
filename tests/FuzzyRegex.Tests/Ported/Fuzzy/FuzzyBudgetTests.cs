using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fuzzy</c> (lines 2612-2804).
/// </summary>
/// <remarks>
/// <para>
/// The assertions where the constraint itself is what is under test: per-kind caps
/// (<c>i&lt;=2,d&lt;=2</c>), weighted cost equations (<c>1i+1d&lt;2</c>, <c>2d+1s&lt;4</c>) and the
/// two-sided form <c>{0&lt;e&lt;5}</c>, which requires at least one error.
/// </para>
/// <para>
/// Every span here was read back from the local Python oracle on 2026-08-30, not copied from the
/// upstream source.
/// </para>
/// </remarks>
public sealed class FuzzyBudgetTests
{
    [Test]
    [Skip("needs:fuzzy-budget - the engine has no fuzzy cost accounting yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#7")]
    public void A_cost_equation_that_permits_no_errors_rejects_an_inexact_match() =>
        FuzzyRegex.Match(FuzzyTestData.Molasses, "(znacnda){s<=1,e<=3,1i+1d<1}").Success.Should().BeFalse();

    [Test]
    [Skip("needs:fuzzy-budget - the engine has no fuzzy cost accounting yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#8")]
    public void Loosening_the_cost_equation_by_one_admits_the_match()
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Molasses, "(znacnda){s<=1,e<=3,1i+1d<2}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((9, 17));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((9, 17));
    }

    [Test]
    [Skip("needs:fuzzy-budget - the engine has no fuzzy cost accounting yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#9")]
    public void A_cost_equation_alone_can_rule_out_every_position() =>
        FuzzyRegex.Match(FuzzyTestData.Molasses, "(ananda){1i+1d<2}").Success.Should().BeFalse();

    // The subject holds no "fuu", so what these three pin down is which position the per-kind
    // caps allow the engine to settle on. Oracle fuzzy_counts for #14 is (0, 2, 2).
    [Test]
    [Arguments("(fuu){i<=3,d<=3,e<=5}", 0, 0)]
    [Arguments("(fuu){i<=2,d<=2,e<=5}", 7, 10)]
    [Arguments("(fuu){i<=3,d<=3,e}", 0, 0)]
    [Skip("needs:fuzzy-budget - the engine has no fuzzy cost accounting yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#12,14,16")]
    public void Per_kind_caps_choose_the_match_position(string pattern, int start, int end)
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Anaconda, pattern);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((start, end));
    }

    // At most two inserts or substitutions, and at most two errors in total.
    [Test]
    [Skip("needs:fuzzy-budget - the engine has no fuzzy cost accounting yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#24")]
    public void A_total_cap_bounds_the_per_kind_caps()
    {
        Match m = FuzzyRegex.Match("oobargoobaploowap", "(foobar){i<=2,s<=2,e<=2}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((5, 11));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((5, 11));
    }

    // At most one insert, two deletes and three substitutions, where a delete costs two and a
    // substitution one, and the total cost must come to less than four.
    [Test]
    [Skip("needs:fuzzy-budget - the engine has no fuzzy cost accounting yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#43")]
    public void A_weighted_cost_equation_prices_deletes_above_substitutions()
    {
        Match m = FuzzyRegex.Match(FuzzyTestData.Scattered, "(foobar){i<=1,d<=2,s<=3,2d+1s<4}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((6, 13));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((6, 13));
    }

    // Hg issue 41: the two-sided form, which demands at least one error and fewer than five.
    [Test]
    [Arguments("servic detection", 16)]
    [Arguments("service detect", 14)]
    [Arguments("service detecti", 15)]
    [Arguments("in service detection", 20)]
    [Skip("needs:fuzzy-budget - the engine has no fuzzy cost accounting yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#68-70,72")]
    public void A_two_sided_constraint_matches_when_there_is_at_least_one_error(string subject, int end)
    {
        Match m = FuzzyRegex.MatchAtStart(subject, "(?:service detection){0<e<5}");

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((0, end));
    }

    [Test]
    [Skip("needs:fuzzy-budget - the engine has no fuzzy cost accounting yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#71")]
    public void A_two_sided_constraint_rejects_an_exact_match() =>
        FuzzyRegex.MatchAtStart("service detection", "(?:service detection){0<e<5}").Success.Should().BeFalse();
}
