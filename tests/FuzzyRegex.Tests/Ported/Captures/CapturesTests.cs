using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Captures;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_captures</c>
/// (lines 2492-2511): the <c>Match.captures(...)</c> method, which upstream's <c>Match</c> has
/// and the built-in <see cref="System.Text.RegularExpressions.Match"/> does not - here it is
/// <c>Groups[n].Captures</c>.
/// </summary>
/// <remarks>
/// Every value here is a plain ASCII string comparison, so no UTF-16 index translation applies.
/// All eight results were verified against the local oracle 2026-08-30 and matched upstream
/// verbatim.
/// </remarks>
public sealed class CapturesTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_captures#1")]
    public void A_repeated_group_reports_one_capture_per_repetition()
    {
        Match m = FuzzyRegex.Match("abc", @"(\w)+");

        m.Groups[1].Captures.Select(c => c.Value).Should().Equal("a", "b", "c");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_captures#2")]
    public void The_whole_match_and_a_repeated_group_each_report_their_own_captures()
    {
        Match m = FuzzyRegex.Match("abcdef", @"(\w{3})+");

        m.Groups[0].Captures.Select(c => c.Value).Should().Equal("abcdef");
        m.Groups[1].Captures.Select(c => c.Value).Should().Equal("abc", "def");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_captures#3")]
    public void An_ipv4_address_reports_one_capture_for_the_first_octet_and_three_for_the_rest()
    {
        Match m = FuzzyRegex.Match("192.168.0.1", @"^(\d{1,3})(?:\.(\d{1,3})){3}$");

        m.Groups[1].Captures.Select(c => c.Value).Should().Equal("192");
        m.Groups[2].Captures.Select(c => c.Value).Should().Equal("168", "0", "1");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_captures#4")]
    public void Two_interleaved_repeated_groups_each_report_their_own_captures_in_order()
    {
        Match m = FuzzyRegex.MatchAtStart("3FB52A0C a2c4g3k9d3", @"^([0-9A-F]{2}){4} ([a-z]\d){5}$");

        m.Groups[1].Captures.Select(c => c.Value).Should().Equal("3F", "B5", "2A", "0C");
        m.Groups[2].Captures.Select(c => c.Value).Should().Equal("a2", "c4", "g3", "k9", "d3");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_captures#5")]
    public void A_group_before_a_repeated_group_and_one_after_it_each_report_their_own_captures()
    {
        Match m = FuzzyRegex.MatchAtStart("aWbXcXdXeXfY", "([a-z]W)([a-z]X)+([a-z]Y)");

        m.Groups[1].Captures.Select(c => c.Value).Should().Equal("aW");
        m.Groups[2].Captures.Select(c => c.Value).Should().Equal("bX", "cX", "dX", "eX");
        m.Groups[3].Captures.Select(c => c.Value).Should().Equal("fY");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_captures#6")]
    public void A_group_repeated_only_inside_a_lookahead_still_reports_its_captures()
    {
        Match m = FuzzyRegex.Match("ab", @".*?(?=(.)+)b");

        m.Groups[1].Captures.Select(c => c.Value).Should().Equal("b");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_captures#7")]
    public void A_group_repeated_inside_an_atomic_group_reports_every_repetition_it_made()
    {
        Match m = FuzzyRegex.Match("abcd", @".*?(?>(.){0,2})d");

        m.Groups[1].Captures.Select(c => c.Value).Should().Equal("b", "c");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_captures#8")]
    public void A_group_repeated_exactly_once_still_reports_one_capture()
    {
        Match m = FuzzyRegex.Match("a", @"(.)+");

        m.Groups[1].Captures.Select(c => c.Value).Should().Equal("a");
    }
}
