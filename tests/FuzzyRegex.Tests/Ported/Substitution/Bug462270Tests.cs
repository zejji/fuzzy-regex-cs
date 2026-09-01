using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_462270</c> (lines 211-218).
/// </summary>
/// <remarks>
/// Only the <c>sys.version_info >= (3, 7, 0)</c> branch of upstream's version guard is ported;
/// the pre-3.7 branch produced a different empty-match count for <c>(?V0)</c> under an older
/// Python and is not a useful oracle.
/// </remarks>
public sealed class Bug462270Tests
{
    [Test]
    [Arguments("(?V0)x*", "-a-b--d-")]
    [Arguments("(?V1)x*", "-a-b--d-")]
    [Property("Upstream", "RegexTests.test_bug_462270#1-2")]
    public void Replace_with_a_star_quantifier_also_replaces_the_empty_matches_between_characters(
        string pattern,
        string expected
    ) => FuzzyRegex.Replace("abxd", pattern, "-").Should().Be(expected);

    [Test]
    [Property("Upstream", "RegexTests.test_bug_462270#3")]
    public void Replace_with_a_plus_quantifier_does_not_replace_empty_matches() =>
        FuzzyRegex.Replace("abxd", "x+", "-").Should().Be("ab-d");
}
