using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Lookaround;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_725149</c>
/// (lines 842-847): mark_stack_base must be restored before marks are restored, when a repeated
/// group contains a lookaround.
/// </summary>
public sealed class Bug725149Tests
{
    [Test]
    [Skip("needs:lookaround - the engine has no lookahead opcodes yet")]
    [Property("Upstream", "RegexTests.test_bug_725149#1")]
    public void Repeated_group_with_positive_lookahead_leaves_the_inner_group_unset()
    {
        Match m = FuzzyRegex.MatchAtStart("abb", "(a)(?:(?=(b)*)c)*");

        m.Value.Should().Be("a");
        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Success.Should().BeFalse();
    }

    [Test]
    [Skip("needs:lookaround - the engine has no lookahead opcodes yet")]
    [Property("Upstream", "RegexTests.test_bug_725149#2")]
    public void Repeated_group_with_negative_lookahead_leaves_the_inner_groups_unset()
    {
        Match m = FuzzyRegex.MatchAtStart("abb", "(a)((?!(b)*))*");

        m.Value.Should().Be("a");
        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Success.Should().BeFalse();
        m.Groups[3].Success.Should().BeFalse();
    }
}
