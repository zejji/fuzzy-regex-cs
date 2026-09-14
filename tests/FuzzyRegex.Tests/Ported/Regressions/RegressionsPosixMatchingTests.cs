using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about POSIX (leftmost-longest) matching.
/// </summary>
/// <remarks>
/// Every assertion uses the inline <c>(?p)</c> flag rather than the compile-time
/// <c>FuzzyRegexOptions.Posix</c> flag, copied unchanged from upstream.
/// </remarks>
public sealed class RegressionsPosixMatchingTests
{
    // Hg issue 150: Have an option for POSIX-compatible longest match of alternates.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#159")]
    public void Posix_alternation_picks_the_longest_digit_word_branch() =>
        Upstream.Match("10b12", @"(?p)\d+(\w(\d*)?|[eE]([+-]\d+))").Value.Should().Be("10b12");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#160")]
    public void Posix_alternation_picks_the_longest_exponent_branch() =>
        Upstream.Match("10E+12", @"(?p)\d+(\w(\d*)?|[eE]([+-]\d+))").Value.Should().Be("10E+12");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#161")]
    public void Posix_alternation_picks_the_longest_digraph_branch() =>
        Upstream.Match("ae", @"(?p)(\w|ae|oe|ue|ss)").Value.Should().Be("ae");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#162")]
    public void Posix_alternation_picks_the_longest_optional_suffix() =>
        Upstream.Match("oneselfsufficient", "(?p)one(self)?(selfsufficient)?").Value.Should().Be("oneselfsufficient");

    // Hg issue 180: bug of POSIX matching.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#216")]
    public void Posix_leftmost_longest_still_lets_a_lazy_group_capture_the_remainder()
    {
        Match m = Upstream.Match("aaabbb", "(?p)a*(.*?)");

        m.Value.Should().Be("aaabbb");
        m.Groups[1].Value.Should().Be("bbb");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#217")]
    public void Posix_leftmost_longest_lets_a_greedy_group_capture_the_remainder()
    {
        Match m = Upstream.Match("aaabbb", "(?p)a*(.*)");

        m.Value.Should().Be("aaabbb");
        m.Groups[1].Value.Should().Be("bbb");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#218")]
    public void Substituting_the_lazy_group_capture_under_posix_matching_leaves_the_remainder() =>
        Upstream.Replace("aaabbb", "(?p)a*(.*?)", @"\1").Should().Be("bbb");

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#219")]
    public void Substituting_the_greedy_group_capture_under_posix_matching_leaves_the_remainder() =>
        Upstream.Replace("aaabbb", "(?p)a*(.*)", @"\1").Should().Be("bbb");
}
