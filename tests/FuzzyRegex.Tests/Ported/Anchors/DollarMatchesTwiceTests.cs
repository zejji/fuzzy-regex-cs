using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Anchors;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_dollar_matches_twice</c>
/// (lines 918-928): <c>$</c> matches at the end of the subject, and again just before a
/// terminating <c>\n</c>.
/// </summary>
public sealed class DollarMatchesTwiceTests
{
    [Test]
    [Skip("needs:anchors - the engine has no $ opcode yet")]
    [Property("Upstream", "RegexTests.test_dollar_matches_twice#1")]
    public void Dollar_replaces_before_a_trailing_newline_and_at_the_very_end() =>
        new FuzzyRegex("$").Replace("a\nb\n", "#").Should().Be("a\nb#\n#");

    [Test]
    [Skip("needs:anchors - the engine has no $ opcode yet")]
    [Property("Upstream", "RegexTests.test_dollar_matches_twice#2")]
    public void Dollar_replaces_only_at_the_end_when_the_subject_has_no_trailing_newline() =>
        new FuzzyRegex("$").Replace("a\nb\nc", "#").Should().Be("a\nb\nc#");

    [Test]
    [Skip("needs:anchors - the engine has no $ opcode yet")]
    [Property("Upstream", "RegexTests.test_dollar_matches_twice#3")]
    public void Dollar_matches_twice_in_a_bare_newline() => new FuzzyRegex("$").Replace("\n", "#").Should().Be("#\n#");

    [Test]
    [Skip("needs:anchors - the engine has no multiline $ opcode yet")]
    [Property("Upstream", "RegexTests.test_dollar_matches_twice#4")]
    public void Multiline_dollar_replaces_at_the_end_of_every_line() =>
        new FuzzyRegex("$", FuzzyRegexOptions.Multiline).Replace("a\nb\n", "#").Should().Be("a#\nb#\n#");

    [Test]
    [Skip("needs:anchors - the engine has no multiline $ opcode yet")]
    [Property("Upstream", "RegexTests.test_dollar_matches_twice#5")]
    public void Multiline_dollar_replaces_at_the_end_of_every_line_with_no_trailing_newline() =>
        new FuzzyRegex("$", FuzzyRegexOptions.Multiline).Replace("a\nb\nc", "#").Should().Be("a#\nb#\nc#");

    [Test]
    [Skip("needs:anchors - the engine has no multiline $ opcode yet")]
    [Property("Upstream", "RegexTests.test_dollar_matches_twice#6")]
    public void Multiline_dollar_matches_twice_in_a_bare_newline() =>
        new FuzzyRegex("$", FuzzyRegexOptions.Multiline).Replace("\n", "#").Should().Be("#\n#");
}
