using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 10 rows of the table that wait on <c>named-groups</c>: 8 that match and 2 that do not. S18
/// delivered the named group; the 5 matching rows that also refer back to it were split out for
/// <c>backrefs</c>.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousNamedGroupTests
{
    [Test]
    [Arguments("(?P<foo_123>a)", "a", "1", new string?[] { "a" })]
    [Arguments("(?<foo_123>a)", "a", "1", new string?[] { "a" })]
    [Arguments("(?P<id>aaa)a", "aaaa", "0,id", new string?[] { "aaaa", "aaa" })]
    [Property("Upstream", "RegexTests.test_various#11,17,201")]
    public void Search_returns_the_expected_group_values(
        string pattern,
        string subject,
        string groups,
        string?[] expected
    )
    {
        Match m = Upstream.Match(subject, pattern);

        m.Success.Should().BeTrue();
        VariousTable.GroupValues(m, groups).Should().Equal(expected);
    }

    // Split out at S18: the named group itself is reachable now, but each of these rows refers back
    // to it - '(?P=name)', '\g<1>' and '\g<name>' are all backreferences, which is S21.
    [Test]
    [Arguments("(?P<foo_123>a)(?P=1)", "aa", "1", new string?[] { "a" })]
    [Arguments("(?P<foo_123>a)(?P=foo_123)", "aa", "1", new string?[] { "a" })]
    [Arguments("(?<foo_123>a)\\g<1>", "aa", "1", new string?[] { "a" })]
    [Arguments("(?<foo_123>a)\\g<foo_123>", "aa", "1", new string?[] { "a" })]
    [Arguments("(?P<id>aa)(?P=id)", "aaaa", "0,id", new string?[] { "aaaa", "aa" })]
    [Property("Upstream", "RegexTests.test_various#6,12,14,18,202")]
    public void Search_with_a_backreference_returns_the_expected_group_values(
        string pattern,
        string subject,
        string groups,
        string?[] expected
    )
    {
        Match m = Upstream.Match(subject, pattern);

        m.Success.Should().BeTrue();
        VariousTable.GroupValues(m, groups).Should().Equal(expected);
    }

    [Test]
    [Arguments("(?<foo_123>a)\\g<foo_123", "aa")]
    [Arguments("(?<foo_123>a)\\g<!>", "aa")]
    [Property("Upstream", "RegexTests.test_various#13,15")]
    public void Search_does_not_match(string pattern, string subject) =>
        Upstream.Match(subject, pattern).Success.Should().BeFalse();
}
