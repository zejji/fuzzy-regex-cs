using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Splitting;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_splititer</c>
/// (lines 1527-1530).
/// </summary>
public sealed class SplititerTests
{
    [Test]
    [Skip("needs:splitting - the instance Split(input, maxSplits) overload is not implemented yet")]
    [Property("Upstream", "RegexTests.test_splititer#1")]
    public void Split_on_a_comma_yields_empty_strings_for_adjacent_and_trailing_delimiters() =>
        new FuzzyRegex(",").Split("a,b,,c,").Should().Equal("a", "b", "", "c", "");

    // NOT PORTED: assertion #2 (lines 1529-1530) uses regex.splititer, which is deferred; Split
    // covers the same pieces (see #1 above).
}
