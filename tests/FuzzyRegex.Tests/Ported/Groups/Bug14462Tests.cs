using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_14462</c> (lines 220-224).
/// </summary>
public sealed class Bug14462Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_14462#1")]
    public void A_non_ascii_group_name_is_usable() =>
        Upstream.Match("abc", "(?P<\u00FF>a)").Groups["\u00FF"].Value.Should().Be("a");
}
