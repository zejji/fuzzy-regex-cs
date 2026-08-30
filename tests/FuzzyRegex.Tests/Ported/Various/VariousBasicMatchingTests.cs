using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 29 rows of the table that wait on <c>basic-matching</c>: 17 that match and 12 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousBasicMatchingTests
{
    [Test]
    [Arguments("a.b", "acb", "0", new string?[] { "acb" })]
    [Arguments("a.b", "a\rb", "0", new string?[] { "a\rb" })]
    [Arguments("", "", "0", new string?[] { "" })]
    [Arguments("abc", "abc", "0", new string?[] { "abc" })]
    [Arguments("abc", "xabcy", "0", new string?[] { "abc" })]
    [Arguments("abc", "ababc", "0", new string?[] { "abc" })]
    [Arguments("a.c", "abc", "0", new string?[] { "abc" })]
    [Arguments("a.c", "axc", "0", new string?[] { "axc" })]
    [Arguments("a]", "a]", "0", new string?[] { "a]" })]
    [Arguments("multiple words", "multiple words, yeah", "0", new string?[] { "multiple words" })]
    [Arguments("abc", "abc", "0", new string?[] { "abc" })]
    [Arguments("abc", "xabcy", "0", new string?[] { "abc" })]
    [Arguments("abc", "ababc", "0", new string?[] { "abc" })]
    [Arguments("a.c", "abc", "0", new string?[] { "abc" })]
    [Arguments("a.c", "axc", "0", new string?[] { "axc" })]
    [Arguments("a]", "a]", "0", new string?[] { "a]" })]
    [Arguments("multiple words", "multiple words, yeah", "0", new string?[] { "multiple words" })]
    [Skip("needs:basic-matching - the engine cannot match a literal yet")]
    [Property("Upstream", "RegexTests.test_various#40,44,51-52,56-57,77-78,94,168,214,218-219,247-248,261,332")]
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
    [Arguments("a.b", "a\nb")]
    [Arguments("abc", "xbc")]
    [Arguments("abc", "axc")]
    [Arguments("abc", "abx")]
    [Arguments("abc", "")]
    [Arguments("multiple words of text", "uh-uh")]
    [Arguments("abc", "xbc")]
    [Arguments("abc", "axc")]
    [Arguments("abc", "abx")]
    [Arguments("abc", "")]
    [Arguments("multiple words of text", "uh-uh")]
    [Arguments("a.b", "a\nb")]
    [Skip("needs:basic-matching - the engine cannot match a literal yet")]
    [Property("Upstream", "RegexTests.test_various#41,53-55,138,167,215-217,296,331,486")]
    public void Search_does_not_match(string pattern, string subject) =>
        FuzzyRegex.Match(subject, pattern).Success.Should().BeFalse();
}
