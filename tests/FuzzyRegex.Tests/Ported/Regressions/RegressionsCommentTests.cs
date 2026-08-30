using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about <c>(?#...)</c> comment groups.
/// </summary>
public sealed class RegressionsCommentTests
{
    // Hg issue 43: regex.compile("a(?#xxx)*") causes "_regex_core.error: nothing to repeat".
    [Test]
    [Skip("needs:comments - (?#...) comment groups are not stripped before quantifier parsing")]
    [Property("Upstream", "RegexTests.test_hg_bugs#22")]
    public void Quantifier_after_a_comment_group_still_repeats_the_preceding_literal() =>
        FuzzyRegex.Match("aaa", "a(?#xxx)*").Value.Should().Be("aaa");

    // Hg issue 47: regex.compile("a#comment\n*", flags=regex.X) causes "_regex_core.error:
    // nothing to repeat".
    [Test]
    [Skip("needs:comments - (?#...) comment groups are not stripped before quantifier parsing")]
    [Property("Upstream", "RegexTests.test_hg_bugs#27")]
    public void Quantifier_after_a_verbose_mode_line_comment_still_repeats_the_preceding_literal() =>
        FuzzyRegex.Match("aaa", "a#comment\n*", FuzzyRegexOptions.IgnorePatternWhitespace).Value.Should().Be("aaa");

    // Hg issue 271: Comment logic different between Re and Regex.
    [Test]
    [Skip("needs:comments - (?#...) comment groups are not stripped before quantifier parsing")]
    [Property("Upstream", "RegexTests.test_hg_bugs#309")]
    public void Escaped_close_paren_inside_a_comment_group_does_not_end_the_comment_early() =>
        FuzzyRegex.MatchAtStart("abcd", @"ab(?#comment\))cd").Success.Should().BeTrue();

    // Git issue 385: Comments in expressions.
    [Test]
    [Skip("needs:comments - (?#...) comment groups are not stripped before quantifier parsing")]
    [Property("Upstream", "RegexTests.test_hg_bugs#395")]
    public void An_empty_comment_group_compiles_on_its_own()
    {
        Action act = () => _ = new FuzzyRegex("(?#)");

        act.Should().NotThrow();
    }

    [Test]
    [Skip("needs:comments - (?#...) comment groups are not stripped before quantifier parsing")]
    [Property("Upstream", "RegexTests.test_hg_bugs#396")]
    public void An_empty_comment_group_compiles_after_a_verbose_mode_flag_group()
    {
        Action act = () => _ = new FuzzyRegex("(?x)(?#)");

        act.Should().NotThrow();
    }
}
