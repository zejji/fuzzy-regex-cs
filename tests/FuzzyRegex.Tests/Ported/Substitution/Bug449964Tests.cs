using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_449964</c> (lines 113-116).
/// </summary>
public sealed class Bug449964Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_449964#1")]
    public void Replace_handles_a_group_reference_immediately_followed_by_another_escape() =>
        FuzzyRegex.Replace("xx", "(?P<unk>x)", @"\g<1>\g<1>\b").Should().Be("xx\bxx\b");
}
