using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_groupdict</c> (lines 415-417).
/// </summary>
/// <remarks>
/// There is no <c>groupdict</c> equivalent on this API: each named group is asserted individually
/// via <c>Groups[name]</c> instead of comparing a whole dictionary.
/// </remarks>
public sealed class GroupDictTests
{
    [Test]
    [Skip("needs:named-groups - named group parsing is not implemented yet")]
    [Property("Upstream", "RegexTests.test_groupdict#1")]
    public void The_first_named_group_is_reachable_by_name() =>
        FuzzyRegex
            .MatchAtStart("first second", "(?P<first>first) (?P<second>second)")
            .Groups["first"]
            .Value.Should()
            .Be("first");

    [Test]
    [Skip("needs:named-groups - named group parsing is not implemented yet")]
    [Property("Upstream", "RegexTests.test_groupdict#1")]
    public void The_second_named_group_is_reachable_by_name() =>
        FuzzyRegex
            .MatchAtStart("first second", "(?P<first>first) (?P<second>second)")
            .Groups["second"]
            .Value.Should()
            .Be("second");
}
