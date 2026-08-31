using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_issue_18468</c>
/// (lines 2955-3030), the <c>regex.match(...).groups()</c>/<c>.group(n)</c> assertions.
/// </summary>
/// <remarks>
/// <para>
/// Upstream loops <c>for string in 'a', StrSubclass('a'):</c>; both iterations run the same
/// source line, so they share one assertion index each rather than getting two - the
/// <c>StrSubclass</c> iteration is not separately ported. The following <c>bytes</c>/
/// <c>bytearray</c>/<c>memoryview</c>/<c>BytesSubclass</c> loop (assertions #28-32) has no
/// equivalent in this char-based engine and is not ported.
/// </para>
/// <para>
/// Upstream's <c>.groups()</c> excludes group 0, so <c>groups() == ()</c> (#23) means the match
/// has group 0 and nothing else. <c>GroupCollection</c> includes group 0, which makes that a
/// count of one.
/// </para>
/// </remarks>
public sealed class Issue18468Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#23")]
    public void A_pattern_with_no_capturing_group_reports_only_group_zero() =>
        FuzzyRegex.MatchAtStart("a", "a").Groups.Count.Should().Be(1);

    [Test]
    [Property("Upstream", "RegexTests.test_issue_18468#24-27")]
    public void Group_accessor_forms_agree_for_a_single_capturing_group()
    {
        Match m = FuzzyRegex.MatchAtStart("a", "(a)");

        m.Groups[1].Value.Should().Be("a"); // .groups() == ('a',)
        m.Value.Should().Be("a"); // .group(0)
        m.Groups[1].Value.Should().Be("a"); // .group(1)
        m.Groups[1].Value.Should().Be("a"); // .group(1, 1), both elements identical
    }
}
