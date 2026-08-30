using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 10 rows of the table that wait on <c>named-groups</c>: 8 that match and 2 that do not.
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
    [Arguments("(?P<foo_123>a)(?P=1)", "aa", "1", new string?[] { "a" })]
    [Arguments("(?P<foo_123>a)", "a", "1", new string?[] { "a" })]
    [Arguments("(?P<foo_123>a)(?P=foo_123)", "aa", "1", new string?[] { "a" })]
    [Arguments("(?<foo_123>a)\\g<1>", "aa", "1", new string?[] { "a" })]
    [Arguments("(?<foo_123>a)", "a", "1", new string?[] { "a" })]
    [Arguments("(?<foo_123>a)\\g<foo_123>", "aa", "1", new string?[] { "a" })]
    [Arguments("(?P<id>aaa)a", "aaaa", "0,id", new string?[] { "aaaa", "aaa" })]
    [Arguments("(?P<id>aa)(?P=id)", "aaaa", "0,id", new string?[] { "aaaa", "aa" })]
    [Skip("needs:named-groups - the parser does not read group names yet")]
    [Property("Upstream", "RegexTests.test_various#6,11-12,14,17-18,201-202")]
    public void Search_returns_the_expected_group_values(
        string pattern,
        string subject,
        string groups,
        string?[] expected
    )
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        m.Success.Should().BeTrue();
        VariousTable.GroupValues(m, groups).Should().Equal(expected);
    }

    [Test]
    [Arguments("(?<foo_123>a)\\g<foo_123", "aa")]
    [Arguments("(?<foo_123>a)\\g<!>", "aa")]
    [Skip("needs:named-groups - the parser does not read group names yet")]
    [Property("Upstream", "RegexTests.test_various#13,15")]
    public void Search_does_not_match(string pattern, string subject) =>
        FuzzyRegex.Match(subject, pattern).Success.Should().BeFalse();
}
