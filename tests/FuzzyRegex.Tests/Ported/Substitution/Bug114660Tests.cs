using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_114660</c> (lines 207-209).
/// </summary>
public sealed class Bug114660Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_114660#1")]
    public void Replace_collapses_whitespace_between_two_captured_non_space_characters() =>
        FuzzyRegex.Replace("hello  there", @"(\S)\s+(\S)", @"\1 \2").Should().Be("hello there");
}
