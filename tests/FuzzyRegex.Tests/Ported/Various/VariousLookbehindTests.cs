using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Various;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_various</c> (lines 1741-2475).
/// </summary>
/// <remarks>
/// <para>
/// The 5 rows of the table that wait on <c>lookbehind</c>: 5 that match and 0 that do not.
/// </para>
/// <para>
/// <c>test_various</c> is a 524-row table driven by a single loop, so its <c>Upstream</c>
/// provenance counts <em>table rows</em> rather than <c>self.assert</c> lines - there is only one
/// assertion in the method. Rows are grouped here by the capability they wait on, so each method
/// carries a non-contiguous set of row numbers.
/// </para>
/// </remarks>
public sealed class VariousLookbehindTests
{
    [Test]
    [Arguments("(?<!-):(.*?)(?<!-):", "a:bc-:de:f", "1", new string?[] { "bc-:de" })]
    [Arguments("(?<!\\\\):(.*?)(?<!\\\\):", "a:bc\\:de:f", "1", new string?[] { "bc\\:de" })]
    [Arguments("(?<!\\?)'(.*?)(?<!\\?)'", "a'bc?'de'f", "1", new string?[] { "bc?'de" })]
    [Arguments("(?<!abc)(d.f)", "abcdefdof", "0", new string?[] { "dof" })]
    [Arguments("^([ab]*?)(?<!(a))c", "abc", "1,2", new string?[] { "ab", null })]
    [Property("Upstream", "RegexTests.test_various#474-476,504,521")]
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
}
