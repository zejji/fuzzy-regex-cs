using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Quantifiers;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_repeat_minmax</c>
/// (lines 424-459).
/// </summary>
public sealed class RepeatMinMaxTests
{
    [Test]
    [Arguments(@"^(\w){1}$", "abc")]
    [Arguments(@"^(\w){1}?$", "abc")]
    [Arguments(@"^(\w){1,2}$", "abc")]
    [Arguments(@"^(\w){1,2}?$", "abc")]
    [Arguments("^x{1}$", "xxx")]
    [Arguments("^x{1}?$", "xxx")]
    [Arguments("^x{1,2}$", "xxx")]
    [Arguments("^x{1,2}?$", "xxx")]
    [Arguments("^x{}$", "xxx")]
    [Property("Upstream", "RegexTests.test_repeat_minmax#1-4,13-16,29")]
    public void MatchAtStart_fails_when_the_repeat_count_cannot_cover_the_subject(string pattern, string subject) =>
        Upstream.MatchAtStart(subject, pattern).Success.Should().BeFalse();

    [Test]
    [Arguments(@"^(\w){3}$")]
    [Arguments(@"^(\w){1,3}$")]
    [Arguments(@"^(\w){1,4}$")]
    [Arguments(@"^(\w){3,4}?$")]
    [Arguments(@"^(\w){3}?$")]
    [Arguments(@"^(\w){1,3}?$")]
    [Arguments(@"^(\w){1,4}?$")]
    [Arguments(@"^(\w){3,4}?$")]
    [Property("Upstream", "RegexTests.test_repeat_minmax#5-12")]
    public void MatchAtStart_captures_the_last_repetition(string pattern) =>
        Upstream.MatchAtStart("abc", pattern).Groups[1].Value.Should().Be("c");

    [Test]
    [Arguments("^x{1}", "x")]
    [Arguments("^x{1}?", "x")]
    [Arguments("^x{0,1}", "x")]
    [Arguments("^x{0,1}?", "")]
    [Property("Upstream", "RegexTests.test_repeat_minmax#17-20")]
    public void MatchAtStart_value_for_open_ended_repeats(string pattern, string expectedValue) =>
        Upstream.MatchAtStart("xxx", pattern).Value.Should().Be(expectedValue);

    [Test]
    [Arguments("^x{3}$")]
    [Arguments("^x{1,3}$")]
    [Arguments("^x{1,4}$")]
    [Arguments("^x{3,4}?$")]
    [Arguments("^x{3}?$")]
    [Arguments("^x{1,3}?$")]
    [Arguments("^x{1,4}?$")]
    [Arguments("^x{3,4}?$")]
    [Property("Upstream", "RegexTests.test_repeat_minmax#21-28")]
    public void MatchAtStart_succeeds_when_the_repeat_count_covers_the_subject(string pattern) =>
        Upstream.MatchAtStart("xxx", pattern).Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_repeat_minmax#30")]
    public void MatchAtStart_treats_an_empty_brace_expression_as_a_literal() =>
        Upstream.MatchAtStart("x{}", "^x{}$").Success.Should().BeTrue();
}
