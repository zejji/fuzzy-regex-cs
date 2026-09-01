using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Substitution;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_449000</c> (lines 118-127).
/// </summary>
public sealed class Bug449000Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_449000#1")]
    public void Replace_a_verbatim_crlf_pattern_with_a_verbatim_newline_template() =>
        FuzzyRegex.Replace("abc\r\ndef\r\n", @"\r\n", @"\n").Should().Be("abc\ndef\n");

    [Test]
    [Property("Upstream", "RegexTests.test_bug_449000#2")]
    public void Replace_a_literal_crlf_pattern_with_a_verbatim_newline_template() =>
        FuzzyRegex.Replace("abc\r\ndef\r\n", "\r\n", @"\n").Should().Be("abc\ndef\n");

    [Test]
    [Property("Upstream", "RegexTests.test_bug_449000#3")]
    public void Replace_a_verbatim_crlf_pattern_with_a_literal_newline_template() =>
        FuzzyRegex.Replace("abc\r\ndef\r\n", @"\r\n", "\n").Should().Be("abc\ndef\n");

    [Test]
    [Property("Upstream", "RegexTests.test_bug_449000#4")]
    public void Replace_a_literal_crlf_pattern_with_a_literal_newline_template() =>
        FuzzyRegex.Replace("abc\r\ndef\r\n", "\r\n", "\n").Should().Be("abc\ndef\n");
}
