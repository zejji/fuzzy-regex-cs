using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about backreferences.
/// </summary>
public sealed class RegressionsBackrefTests
{
    // Hg issue 36: regex.search("^(a|)\1{2}b", "b") returns None.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#14")]
    public void Backreference_to_an_empty_optional_group_repeated_still_matches()
    {
        Match m = Upstream.Match("b", @"^(a|)\1{2}b");

        m.Value.Should().Be("b");
        m.Groups[1].Value.Should().Be("");
    }

    // Hg issue 52: regex.search("(\\1xx|){6}", "xx", flags=regex.V1).span(0,1) returns incorrect
    // value.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#38")]
    public void Backreference_to_the_groups_own_capture_under_V1_reports_the_final_span()
    {
        Match m = Upstream.Match("xx", @"(?V1)(\1xx|){6}");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
        m.Groups[1].Index.Should().Be(2);
        m.Groups[1].Length.Should().Be(0);
    }

    // Hg issue 115: Infinite loop when processing backreferences.
    [Test]
    // S21 delivered the backreference; what is left is 'Matches', which is S25.
    [Property("Upstream", "RegexTests.test_hg_bugs#104")]
    public void Backreference_that_cannot_match_finds_nothing_without_looping_forever() =>
        Upstream
            .Matches("To make use of one of these modules", @"\bof ([a-z]+) of \1\b")
            .Select(static m => m.Value)
            .Should()
            .BeEmpty();

    // Hg issue 212: Unexpected matching difference with .*? between re and regex.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#256")]
    public void Lazy_dot_star_followed_by_a_backreference_finds_the_shortest_span()
    {
        Match m = Upstream.MatchAtStart("x  |y| z|", @"x.*? (.).*\1(.*)\1");

        m.Index.Should().Be(0);
        m.Length.Should().Be(9);
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#257")]
    public void Lazy_group_followed_by_two_more_backreferences_finds_the_whole_span()
    {
        Match m = Upstream.MatchAtStart(".sr  h |<nw>|<span class=\"locked\">|", @"\.sr (.*?) (.)(.*)\2(.*)\2(.*)");

        m.Index.Should().Be(0);
        m.Length.Should().Be(35);
    }

    // Git issue 408: regex fails with a quantified backreference but succeeds with repeated
    // backref.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#400")]
    public void Backreference_repeated_three_times_explicitly_matches() =>
        Upstream.MatchAtStart("xxxxx", @"(?:(x*)\1\1\1)*x$").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#401")]
    public void Backreference_with_an_explicit_quantifier_of_three_matches_the_same_way() =>
        Upstream.MatchAtStart("xxxxx", @"(?:(x*)\1{3})*x$").Success.Should().BeTrue();

    // Git issue 494: Backtracking failure matching regex ^a?(a?)b?c\1$ against string abca.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#441")]
    public void Optional_groups_around_a_backreference_backtrack_to_find_the_full_span()
    {
        Match m = Upstream.Match("abca", @"^a?(a?)b?c\1$");

        m.Index.Should().Be(0);
        m.Length.Should().Be(4);
    }
}
