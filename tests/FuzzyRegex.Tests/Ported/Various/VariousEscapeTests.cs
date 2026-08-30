using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 17 rows of the table that wait on <c>escapes</c>: 13 that match and 4 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousEscapeTests
{
    [Test]
    [Arguments("\\09", "\u00009", "0", new string?[] { "\u00009" })]
    [Arguments("\\0", "\u0000", "0", new string?[] { "\u0000" })]
    [Arguments("\\xff", "\u00FF", "0", new string?[] { "\u00FF" })]
    [Arguments("\\t\\n\\v\\r\\f\\a\\g", "\t\n\v\r\f\ag", "0", new string?[] { "\t\n\v\r\f\ag" })]
    [Arguments("\t\n\v\r\f\a\\g", "\t\n\v\r\f\ag", "0", new string?[] { "\t\n\v\r\f\ag" })]
    [Arguments("\\t\\n\\v\\r\\f\\a", "\t\n\v\r\f\a", "0", new string?[] { "\t\n\v\r\f\a" })]
    [Arguments("\\g", "g", "0", new string?[] { "g" })]
    [Arguments("\\N", "N", "0", new string?[] { "N" })]
    [Arguments("\\w+", "--ab_cd0123--", "0", new string?[] { "ab_cd0123" })]
    [Arguments("\\D+", "1234abc5678", "0", new string?[] { "abc" })]
    [Arguments("(\\s*)(\\S*)(\\s*)", " testing!1972", "3,2,1", new string?[] { "", "testing!1972", " " })]
    [Arguments(".*?\\S *:", "xx:", "0", new string?[] { "xx:" })]
    [Arguments("\\w", "\u00C4", "0", new string?[] { "\u00C4" })]
    [Skip("needs:escapes - the parser does not decode escapes yet")]
    [Property("Upstream", "RegexTests.test_various#21,24,30,35-37,204,208,488,490,494,506,524")]
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
    [Arguments("\\x00ffffffffffffff", "\u00FF")]
    [Arguments("\\x00f", "\u000F")]
    [Arguments("\\x00fe", "\u00FE")]
    [Arguments("\\x00ff", "\u00FF")]
    [Skip("needs:escapes - the parser does not decode escapes yet")]
    [Property("Upstream", "RegexTests.test_various#31-34")]
    public void Search_does_not_match(string pattern, string subject) =>
        FuzzyRegex.Match(subject, pattern).Success.Should().BeFalse();
}
