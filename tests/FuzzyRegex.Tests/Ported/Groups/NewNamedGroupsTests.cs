using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_new_named_groups</c>
/// (lines 1002-1006).
/// </summary>
/// <remarks>The <c>(?P&lt;name&gt;...)</c> and <c>(?&lt;name&gt;...)</c> spellings of a named group agree.</remarks>
public sealed class NewNamedGroupsTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_new_named_groups#1")]
    public void Both_named_group_spellings_match()
    {
        Match m0 = Upstream.MatchAtStart("x", @"(?P<a>\w)");
        Match m1 = Upstream.MatchAtStart("x", @"(?<a>\w)");

        m0.Success.Should().BeTrue();
        m1.Success.Should().BeTrue();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_new_named_groups#1")]
    public void Both_named_group_spellings_produce_the_same_tuple()
    {
        Match m0 = Upstream.MatchAtStart("x", @"(?P<a>\w)");
        Match m1 = Upstream.MatchAtStart("x", @"(?<a>\w)");

        (m0.Value, m0.Groups[1].Value).Should().Be((m1.Value, m1.Groups[1].Value));
    }
}
