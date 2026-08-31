using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 5 rows of the table that wait on <c>unicode-properties</c>: 5 that match and 0 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousUnicodePropertyTests
{
    [Test]
    [Arguments("\\N{LATIN SMALL LETTER A}", "a", "0", new string?[] { "a" })]
    [Arguments("\\p", "p", "0", new string?[] { "p" })]
    [Arguments("\\p{Ll}", "a", "0", new string?[] { "a" })]
    [Arguments("\\P", "P", "0", new string?[] { "P" })]
    [Arguments("\\P{Lu}", "p", "0", new string?[] { "p" })]
    [Property("Upstream", "RegexTests.test_various#209-213")]
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
