using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_qualified_re_sub</c>
/// (lines 203-205).
/// </summary>
public sealed class QualifiedSubTests
{
    [Test]
    [Skip("needs:substitution - Pattern.Replace is not implemented yet")]
    [Property("Upstream", "RegexTests.test_qualified_re_sub#1")]
    public void Replace_with_no_count_replaces_every_match() =>
        FuzzyRegex.Replace("aaaaa", "a", "b").Should().Be("bbbbb");

    [Test]
    [Skip("needs:substitution - the instance Replace(replacement, count) overload is not implemented yet")]
    [Property("Upstream", "RegexTests.test_qualified_re_sub#2")]
    public void Replace_with_a_count_of_one_replaces_only_the_first_match() =>
        new FuzzyRegex("a").Replace("aaaaa", "b", 1).Should().Be("baaaa");
}
