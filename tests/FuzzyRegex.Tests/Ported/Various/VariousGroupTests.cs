using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 13 rows of the table that wait on <c>groups</c>: 13 that match and 0 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousGroupTests
{
    [Test]
    [Arguments("()ef", "def", "0,1", new string?[] { "ef", "" })]
    [Arguments("a\\(b", "a(b", "*", new string?[] { "a(b" })]
    [Arguments("((a))", "abc", "0,1,2", new string?[] { "a", "a", "a" })]
    [Arguments("(a)b(c)", "abc", "0,1,2", new string?[] { "abc", "a", "c" })]
    [Arguments("((a)(b)c)(d)", "abcd", "1,2,3,4", new string?[] { "abc", "a", "b", "d" })]
    [Arguments("(((((((((a)))))))))", "a", "0", new string?[] { "a" })]
    [Arguments("()ef", "def", "0,1", new string?[] { "ef", "" })]
    [Arguments("a\\(b", "a(b", "*", new string?[] { "a(b" })]
    [Arguments("((a))", "abc", "0,1,2", new string?[] { "a", "a", "a" })]
    [Arguments("(a)b(c)", "abc", "0,1,2", new string?[] { "abc", "a", "c" })]
    [Arguments("((a)(b)c)(d)", "abcd", "1,2,3,4", new string?[] { "abc", "a", "b", "d" })]
    [Arguments("((((((((((a))))))))))", "a", "10", new string?[] { "a" })]
    [Arguments("(((((((((a)))))))))", "a", "0", new string?[] { "a" })]
    [Skip("needs:groups - the engine does not capture groups yet")]
    [Property("Upstream", "RegexTests.test_various#124,126,130-131,158,166,271,276,282-283,318,326,330")]
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
}
