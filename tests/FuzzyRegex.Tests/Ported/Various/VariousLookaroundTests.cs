using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 5 rows of the table that wait on <c>lookaround</c>: 5 that match and 0 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousLookaroundTests
{
    [Test]
    [Arguments("a(?!b).", "abad", "0", new string?[] { "ad" })]
    [Arguments("a(?=d).", "abad", "0", new string?[] { "ad" })]
    [Arguments("a(?=c|d).", "abad", "0", new string?[] { "ad" })]
    [Arguments("^([ab]*?)(?=(b)?)c", "abc", "1,2", new string?[] { "ab", null })]
    [Arguments("^([ab]*?)(?!(b))c", "abc", "1,2", new string?[] { "ab", null })]
    [Skip("needs:lookaround - the engine has no lookaround opcodes yet")]
    [Property("Upstream", "RegexTests.test_various#467-469,519-520")]
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
