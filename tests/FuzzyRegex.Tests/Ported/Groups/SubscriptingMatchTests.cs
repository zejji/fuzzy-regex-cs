using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_subscripting_match</c>
/// (lines 991-1000).
/// </summary>
public sealed class SubscriptingMatchTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_subscripting_match#1")]
    public void The_match_succeeds() => Upstream.MatchAtStart("xy", @"(?<a>\w)").Success.Should().BeTrue();

    // NOT PORTED: upstream's second assertion checks that `m[0] == m.group(0)` and
    // `m[1] == m.group(1)` - that Python's subscript operator agrees with its `.group()` method.
    // This API has only one accessor for each (`.Value` and `Groups[n].Value`), so the two Python
    // expressions collapse onto the identical C# expression and there is nothing distinct left to
    // assert.

    // Assertion 4 (a second "if not m" check) duplicates assertion 1 above.

    [Test]
    [Property("Upstream", "RegexTests.test_subscripting_match#5")]
    public void The_whole_tuple_is_the_match_and_its_single_named_group()
    {
        Match m = Upstream.MatchAtStart("xy", @"(?<a>\w)");

        m.Value.Should().Be("x");
        m.Groups[1].Value.Should().Be("x");
    }
}
