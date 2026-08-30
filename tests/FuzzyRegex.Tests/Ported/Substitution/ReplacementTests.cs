using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_replacement</c>
/// (lines 2476-2484).
/// </summary>
/// <remarks>
/// Every replacement string here is a plain (non-verbatim) C# literal because upstream's
/// backslash escapes (<c>\?</c>, <c>\.</c>, <c>\a</c>, <c>\\1</c>) need to reach the replacement
/// engine as actual characters, not as regex syntax; C#'s own <c>\a</c> and <c>\\</c> escapes
/// happen to line up with Python's, so the literal is copied verbatim. Verified against the local
/// oracle 2026-08-30: all five results matched upstream's expected values exactly.
/// </remarks>
public sealed class ReplacementTests
{
    [Test]
    [Skip("needs:substitution - regex.sub is not implemented yet")]
    [Property("Upstream", "RegexTests.test_replacement#1")]
    public void Sub_leaves_unrecognised_backslash_escapes_and_control_characters_untouched() =>
        FuzzyRegex.Replace("test?", @"test\?", "result\\?\\.\a\n").Should().Be("result\\?\\.\a\n");

    [Test]
    [Skip("needs:substitution - regex.sub is not implemented yet")]
    [Property("Upstream", "RegexTests.test_replacement#2")]
    public void Sub_expands_a_backreference_in_the_replacement_template() =>
        FuzzyRegex.Replace("x", "(.)", "\\1\\1").Should().Be("xx");

    [Test]
    [Skip("needs:substitution - regex.sub is not implemented yet")]
    [Property("Upstream", "RegexTests.test_replacement#3")]
    public void Sub_treats_an_escaped_replacement_template_as_literal_text() =>
        FuzzyRegex.Replace("x", "(.)", FuzzyRegex.Escape("\\1\\1")).Should().Be("\\1\\1");

    [Test]
    [Skip("needs:substitution - regex.sub is not implemented yet")]
    [Property("Upstream", "RegexTests.test_replacement#4")]
    public void Sub_treats_a_doubled_backslash_before_a_digit_as_a_literal_backslash_and_digit() =>
        FuzzyRegex.Replace("x", "(.)", "\\\\1\\\\1").Should().Be("\\1\\1");

    [Test]
    [Skip("needs:substitution - regex.sub is not implemented yet")]
    [Property("Upstream", "RegexTests.test_replacement#5")]
    public void Sub_with_an_evaluator_uses_its_return_value_literally_with_no_further_expansion() =>
        FuzzyRegex.Replace("x", "(.)", _ => "\\1\\1").Should().Be("\\1\\1");
}
