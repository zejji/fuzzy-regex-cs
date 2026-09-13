using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Fuzzy;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_fuzzy</c> (lines 2612-2804).
/// </summary>
/// <remarks>
/// Hg issue 109, the edit distance a fuzzy match reports. Upstream's <c>fuzzy_counts</c> is the
/// tuple <c>(substitutions, insertions, deletions)</c>, which is
/// <c>FuzzyCounts</c> here, and <c>fuzzy_changes</c> is the
/// tuple of positions, which is <c>FuzzyChanges</c>. The <c>(?e)</c> rows are the point of
/// the block: ENHANCEMATCH is what drives the reported distance down to zero.
/// </remarks>
public sealed class FuzzyCountsTests
{
    [Test]
    [Arguments("(?:cats|cat){e<=1}", "cat", 0, 0, 1)]
    [Arguments("(?:cat|cats){e<=1}", "cats", 0, 1, 0)]
    [Arguments("(?:cat){e<=1} (?:cat){e<=1}", "cat cot", 1, 0, 0)]
    [Property("Upstream", "RegexTests.test_fuzzy#73, #75, #77")]
    public void A_fuzzy_match_reports_how_many_errors_of_each_kind_it_used(
        string pattern,
        string subject,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = FuzzyRegex.FullMatch(subject, pattern);

        m.Success.Should().BeTrue();
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }

    // The two '(?e)' rows of the same upstream block, split out in S40 so the three plain rows above
    // can run. They are the point of upstream's block - ENHANCEMATCH is what drives the reported
    // distance to zero - and so they wait on the capability that does it.
    [Test]
    [Skip("needs:fuzzy-enhancematch - ENHANCEMATCH ranking is not implemented yet")]
    [Arguments("(?e)(?:cats|cat){e<=1}", "cat", 0, 0, 0)]
    [Arguments("(?e)(?:cat|cats){e<=1}", "cats", 0, 0, 0)]
    [Property("Upstream", "RegexTests.test_fuzzy#74, #76")]
    public void Enhanced_mode_drives_the_reported_error_counts_to_zero(
        string pattern,
        string subject,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = FuzzyRegex.FullMatch(subject, pattern);

        m.Success.Should().BeTrue();
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }

    // Upstream's comment is "Incorrect fuzzy changes" - the assertion pins the behaviour that
    // was fixed, not a bug.
    [Test]
    [Skip("needs:fuzzy-enhancematch - ENHANCEMATCH ranking is not implemented yet")]
    [Property("Upstream", "RegexTests.test_fuzzy#78")]
    public void A_fuzzy_match_reports_where_it_spent_each_error()
    {
        Match m = FuzzyRegex.Match("ATTATTTATTTTTCATA", "(?e)(GTTTTCATTCCTCATA){i<=4,d<=4,s<=4,i+d+s<=8}");

        m.Success.Should().BeTrue();
        m.FuzzyChanges.Substitutions.Should().Equal(0, 6, 10, 11);
        m.FuzzyChanges.Insertions.Should().Equal(3);
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }
}
