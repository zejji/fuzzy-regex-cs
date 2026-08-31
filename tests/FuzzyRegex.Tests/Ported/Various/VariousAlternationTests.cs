using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 13 rows of the table that wait on <c>alternation</c>: 13 that match and 0 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousAlternationTests
{
    [Test]
    [Arguments("ab|cd", "abc", "0", new string?[] { "ab" })]
    [Arguments("ab|cd", "abcd", "0", new string?[] { "ab" })]
    [Arguments("a|b|c|d|e", "e", "0", new string?[] { "e" })]
    [Arguments("(a|b|c|d|e)f", "ef", "0,1", new string?[] { "ef", "e" })]
    [Arguments("(ab|cd)e", "abcde", "0,1", new string?[] { "cde", "cd" })]
    [Arguments("(abc|)ef", "abcdef", "0,1", new string?[] { "ef", "" })]
    [Arguments("(a)(b)c|ab", "ab", "0,1,2", new string?[] { "ab", null, null })]
    [Arguments("ab|cd", "abc", "0", new string?[] { "ab" })]
    [Arguments("ab|cd", "abcd", "0", new string?[] { "ab" })]
    [Arguments("a|b|c|d|e", "e", "0", new string?[] { "e" })]
    [Arguments("(a|b|c|d|e)f", "ef", "0,1", new string?[] { "ef", "e" })]
    [Arguments("(ab|cd)e", "abcde", "0,1", new string?[] { "cde", "cd" })]
    [Arguments("(abc|)ef", "abcdef", "0,1", new string?[] { "ef", "" })]
    [Property("Upstream", "RegexTests.test_various#122-123,140-141,145,148,189,269-270,300-301,305,308")]
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
