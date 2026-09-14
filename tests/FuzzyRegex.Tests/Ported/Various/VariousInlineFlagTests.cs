using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 13 rows of the table that wait on <c>inline-flags</c>: 10 that match and 3 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousInlineFlagTests
{
    [Test]
    [Arguments("(?s)a.b", "a\nb", "0", new string?[] { "a\nb" })]
    [Arguments("(?s)a.*b", "acc\nccb", "0", new string?[] { "acc\nccb" })]
    [Arguments("(?s)a.{4,5}b", "acc\nccb", "0", new string?[] { "acc\nccb" })]
    [Arguments("(?x)w# comment 1\nx y\n# comment 2\nz", "wxyz", "0", new string?[] { "wxyz" })]
    [Arguments("(?m)^abc", "jkl\nabc\nxyz", "0", new string?[] { "abc" })]
    [Arguments("(?m)abc$", "jkl\nxyzabc\n123", "0", new string?[] { "abc" })]
    [Arguments("(?s)a.b", "a\nb", "0", new string?[] { "a\nb" })]
    [Arguments("(?x) foo ", "foo", "0", new string?[] { "foo" })]
    [Arguments("(?x)foo ", "foo", "0", new string?[] { "foo" })]
    [Arguments("(?ms).*?x\\s*\\Z(.*)", "xx\nx\n", "1", new string?[] { "" })]
    [Property("Upstream", "RegexTests.test_various#46,48-49,482,484-485,487,502-503,509")]
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

    [Test]
    [Arguments("a.b(?s)", "a\nb")]
    [Arguments("a.*(?s)b", "acc\nccb")]
    [Arguments(" (?x)foo ", "foo")]
    [Property("Upstream", "RegexTests.test_various#45,47,501")]
    public void Search_does_not_match(string pattern, string subject) =>
        Upstream.Match(subject, pattern).Success.Should().BeFalse();
}
