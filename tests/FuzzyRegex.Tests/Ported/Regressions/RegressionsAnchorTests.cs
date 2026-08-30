using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertion
/// about the <c>\Z</c> end-of-string anchor.
/// </summary>
public sealed class RegressionsAnchorTests
{
    // Hg issue 59: regex.search("\\Z", "a\na\n") returns None incorrectly.
    [Test]
    [Skip("needs:anchors - \\Z does not anchor past a trailing newline")]
    [Property("Upstream", "RegexTests.test_hg_bugs#43")]
    public void End_of_string_anchor_matches_after_a_trailing_newline()
    {
        Match m = FuzzyRegex.Match("a\na\n", @"\Z");

        m.Index.Should().Be(4);
        m.Length.Should().Be(0);
    }
}
