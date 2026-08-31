using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_725106</c> (lines 824-840).
/// </summary>
/// <remarks>Capturing groups in alternatives inside a repeat.</remarks>
public sealed class Bug725106Tests
{
    [Test]
    [Arguments("^((a)|b)*", "abc", "ab", "b", "a")]
    [Arguments("^(([ab])|c)*", "abc", "abc", "c", "b")]
    [Arguments("^((d)|[ab])*", "abc", "ab", "b", null)]
    [Arguments("^((a)c|[ab])*", "abc", "ab", "b", null)]
    [Arguments("^((a)|b)*?c", "abc", "abc", "b", "a")]
    [Arguments("^(([ab])|c)*?d", "abcd", "abcd", "c", "b")]
    [Arguments("^((d)|[ab])*?c", "abc", "abc", "b", null)]
    [Arguments("^((a)c|[ab])*?c", "abc", "abc", "b", null)]
    [Property("Upstream", "RegexTests.test_bug_725106#1-8")]
    public void The_last_repeat_iteration_leaves_its_group_captures(
        string pattern,
        string subject,
        string whole,
        string group1,
        string? group2
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, pattern);

        m.Value.Should().Be(whole);
        m.Groups[1].Value.Should().Be(group1);
        m.Groups[2].Success.Should().Be(group2 is not null);
        if (group2 is not null)
        {
            m.Groups[2].Value.Should().Be(group2);
        }
    }
}
