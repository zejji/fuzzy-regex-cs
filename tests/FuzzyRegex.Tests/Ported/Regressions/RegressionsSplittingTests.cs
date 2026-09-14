using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about splitting.
/// </summary>
public sealed class RegressionsSplittingTests
{
    // Git issue 421: Fatal Python error: Segmentation fault.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#409")]
    public void Splitting_on_a_captured_day_or_week_alternation_keeps_the_whole_match_as_the_capture() =>
        Upstream.Compile(@"(\d+ week|\d+ days)").Split("7 days").Should().Equal("", "7 days", "");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#410")]
    public void Splitting_on_a_captured_day_or_week_alternation_keeps_the_whole_match_as_the_capture_for_a_two_digit_count() =>
        Upstream.Compile(@"(\d+ week|\d+ days)").Split("10 days").Should().Equal("", "10 days", "");
}
