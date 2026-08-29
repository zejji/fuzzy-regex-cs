using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_special_escapes</c>
/// (lines 491-522): the <c>\b \B ^ $ \A \Z</c> assertions. The <c>\d \D \w \W \s \S</c> run from
/// the same method lives in <c>Ported/Escapes/SpecialEscapesCharacterClassesTests.cs</c> instead.
/// </summary>
public sealed class SpecialEscapesAnchorTests
{
    // Upstream's assertion 5 repeats assertion 1 with an explicit regex.UNICODE flag, which is
    // incidental here (str patterns are Unicode by default and the flag is not surfaced on
    // FuzzyRegexOptions); folded into one test.
    [Test]
    [Skip("needs:anchors - the engine has no word-boundary opcode yet")]
    [Property("Upstream", "RegexTests.test_special_escapes#1,5")]
    public void Word_boundary_captures_a_word_that_is_not_a_prefix_of_a_longer_one() =>
        FuzzyRegex.Match("abcd abc bcd bx", "\\b(b.)\\b").Groups[1].Value.Should().Be("bx");

    [Test]
    [Skip("needs:anchors - the engine has no non-word-boundary opcode yet")]
    [Property("Upstream", "RegexTests.test_special_escapes#2,6")]
    public void Non_word_boundary_captures_inside_a_word() =>
        FuzzyRegex.Match("abc bcd bc abxd", "\\B(b.)\\B").Groups[1].Value.Should().Be("bx");

    [Test]
    [Skip("needs:anchors - the engine has no multiline ^/$ opcodes yet")]
    [Property("Upstream", "RegexTests.test_special_escapes#7")]
    public void Caret_and_dollar_match_around_an_embedded_line_under_multiline() =>
        FuzzyRegex.Match("\nabc\n", "^abc$", FuzzyRegexOptions.Multiline).Value.Should().Be("abc");

    [Test]
    [Skip("needs:anchors - the engine has no \\A/\\Z opcodes yet")]
    [Property("Upstream", "RegexTests.test_special_escapes#8")]
    public void Caret_dollar_and_string_anchors_all_agree_on_a_bare_subject() =>
        FuzzyRegex.Match("abc", "^\\Aabc\\Z$", FuzzyRegexOptions.Multiline).Value.Should().Be("abc");

    [Test]
    [Skip("needs:anchors - the engine has no \\A/\\Z opcodes yet")]
    [Property("Upstream", "RegexTests.test_special_escapes#9")]
    public void String_anchors_reject_a_subject_with_leading_and_trailing_newlines_even_under_multiline() =>
        FuzzyRegex.Match("\nabc\n", "^\\Aabc\\Z$", FuzzyRegexOptions.Multiline).Success.Should().BeFalse();
}
