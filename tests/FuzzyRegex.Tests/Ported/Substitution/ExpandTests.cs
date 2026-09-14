using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_expand</c> (lines 419-422).
/// </summary>
public sealed class ExpandTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_expand#1")]
    public void Result_expands_named_groups_by_number_and_by_name()
    {
        Match m = Upstream.MatchAtStart("first second", "(?P<first>first) (?P<second>second)");

        m.Result(@"\2 \1 \g<second> \g<first>").Should().Be("second first second first");
    }
}
