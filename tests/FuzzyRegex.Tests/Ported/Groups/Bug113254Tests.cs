using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_113254</c> (lines 753-756).
/// </summary>
/// <remarks>
/// Upstream reports <c>-1</c> from <c>start</c>, <c>end</c> and <c>span</c> for a group that did
/// not participate; this API instead exposes that as <c>Groups[n].Success == false</c>, so all
/// three assertions collapse onto the same check.
/// </remarks>
public sealed class Bug113254Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_113254#1")]
    public void Start_reports_a_group_that_did_not_participate() =>
        Upstream.MatchAtStart("b", "(a)|(b)").Groups[1].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_bug_113254#2")]
    public void End_reports_a_group_that_did_not_participate() =>
        Upstream.MatchAtStart("b", "(a)|(b)").Groups[1].Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_bug_113254#3")]
    public void Span_reports_a_group_that_did_not_participate() =>
        Upstream.MatchAtStart("b", "(a)|(b)").Groups[1].Success.Should().BeFalse();
}
