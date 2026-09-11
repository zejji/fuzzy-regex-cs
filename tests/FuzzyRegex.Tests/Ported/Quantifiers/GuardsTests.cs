using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Quantifiers;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_guards</c> (lines 2512-2531):
/// backtracking guard behaviour around repeated groups, an unparticipating optional group, and a
/// lazy repeat that must still reach a following literal.
/// </summary>
/// <remarks>
/// All subjects and patterns are ASCII, so <c>span</c> offsets need no UTF-16 translation. Every
/// span here was verified against the local oracle 2026-08-30 and matched upstream verbatim.
/// </remarks>
public sealed class GuardsTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_guards#1")]
    public void A_bounded_repeat_of_a_group_stops_backtracking_at_the_last_successful_repetition()
    {
        Match m = FuzzyRegex.Match("XY\nX Y\nX  Y\nXY\nXX AB:", @"(X.*?Y\s*){3}(X\s*)+AB:");

        (m.Index, m.Index + m.Length).Should().Be((3, 21));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((12, 15));
        (m.Groups[2].Index, m.Groups[2].Index + m.Groups[2].Length).Should().Be((16, 18));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_guards#2")]
    public void An_unbounded_minimum_repeat_of_a_group_still_starts_at_the_earliest_possible_match()
    {
        Match m = FuzzyRegex.Match("XY\nX Y\nX  Y\nXY\nXX AB:", @"(X.*?Y\s*){3,}(X\s*)+AB:");

        (m.Index, m.Index + m.Length).Should().Be((0, 21));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((12, 15));
        (m.Groups[2].Index, m.Groups[2].Index + m.Groups[2].Length).Should().Be((16, 18));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_guards#3")]
    public void An_optional_group_that_does_not_participate_reports_no_success()
    {
        Match m = FuzzyRegex.Match("9999XX", @"\d{4}(\s*\w)?\W*((?!\d)\w){2}");

        (m.Index, m.Index + m.Length).Should().Be((0, 6));
        m.Groups[1].Success.Should().BeFalse();
        (m.Groups[2].Index, m.Groups[2].Index + m.Groups[2].Length).Should().Be((5, 6));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_guards#4")]
    public void A_lazy_bounded_repeat_still_reaches_a_literal_that_only_appears_later()
    {
        Match m = FuzzyRegex.Match("A\n1\nS\n1 (X", @"A\s*?.*?(\n+.*?\s*?){0,2}\(X");

        (m.Index, m.Index + m.Length).Should().Be((0, 10));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((5, 8));
    }

    [Test]
    [Arguments("aaaaaa:\nDerde:", 8, 14)]
    [Arguments("aaaaa:\nDerde:", 7, 13)]
    [Property("Upstream", "RegexTests.test_guards#5-6")]
    public void A_lazy_whitespace_repeat_before_a_colon_finds_the_second_occurrence(string subject, int start, int end)
    {
        Match m = FuzzyRegex.Match(subject, @"Derde\s*:");

        (m.Index, m.Index + m.Length).Should().Be((start, end));
    }
}
