using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_search_anchor</c>
/// (lines 1205-1206).
/// </summary>
public sealed class SearchAnchorTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_search_anchor#1")]
    public void Matches_value_for_two_char_runs_anchored_to_the_previous_match_end() =>
        FuzzyRegex.Matches("abcd ef", @"\G\w{2}").Select(m => m.Value).Should().Equal("ab", "cd");
}
