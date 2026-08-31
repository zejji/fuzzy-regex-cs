using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about atomic groups (<c>(?&gt;...)</c>).
/// </summary>
public sealed class RegressionsAtomicTests
{
    // Hg issue 213: Segmentation Fault. Not a raw Python string, so each `\xNN` here is four
    // literal characters (backslash, x, and two hex digits), not a decoded byte.
    private const string _hgIssue213Subject =
        "\"\\xF9\\x80\\xAEqdz\\x95L\\xA7\\x89[\\xFE \\x91)\\xF9]\\xDB'\\x99\\x09=\\x00\\xFD\\x98\\x22\\xDD\\xF1\\xB6\\xC3 Z\\xB6gv\\xA5x\\x93P\\xE1r\\x14\\x8Cv\\x0C\\xC0w\\x15r\\xFFc%\" ";

    private const string _hgIssue213Pattern =
        @"(?P<http_referer>((?>(?<!\\)(?>""(?>\\.|[^\\""]+)+""|""""|(?>'(?>\\.|[^\\']+)+')|''|(?>`(?>\\.|[^\\`]+)+`)|``)))) (?P<useragent>((?>(?<!\\)(?>""(?>\\.|[^\\""]+)+""|""""|(?>'(?>\\.|[^\\']+)+')|''|(?>`(?>\\.|[^\\`]+)+`)|``))))";

    // Hg issue 154: Segmentation fault 11 when working with an atomic group.
    private const string _hgIssue154Subject =
        "June 30, December 31, 2013 2012\nsome words follow:\nmore words and numbers 1,234,567 9,876,542\nmore words and numbers 1,234,567 9,876,542";

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#1")]
    public void Atomic_group_around_a_single_literal_compiles()
    {
        Action act = () => _ = new FuzzyRegex("(?>b)", FuzzyRegexOptions.Version1);

        act.Should().NotThrow();
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#2")]
    public void Repeated_alternation_of_two_atomic_groups_compiles()
    {
        Action act = () => _ = new FuzzyRegex(@"^((?>\w+)|(?>\s+))*$", FuzzyRegexOptions.Version1);

        act.Should().NotThrow();
    }

    // Hg issue 38: regex.search("(?>.*/)b", "a/b") returns None.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#16")]
    public void Atomic_group_over_dot_star_still_lets_the_rest_of_the_pattern_match() =>
        FuzzyRegex.Match("a/b", "(?>.*/)b").Value.Should().Be("a/b");

    [Test]
    [Skip("needs:lookaround - the atomic group works from S20; the (?<!...) lookbehind and FuzzyRegex.Matches do not")]
    [Property("Upstream", "RegexTests.test_hg_bugs#206")]
    public void Negative_lookbehind_before_an_atomic_alternation_finds_exactly_one_match() =>
        FuzzyRegex.Matches(_hgIssue154Subject, @"(?<!\d)(?>2014|2013 ?2012)").Should().HaveCount(1);

    // Hg issue 156: regression on atomic grouping.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#207")]
    public void Atomic_group_after_a_literal_still_matches_at_the_start()
    {
        Match m = FuzzyRegex.MatchAtStart("12", "1(?>2)");

        m.Index.Should().Be(0);
        m.Length.Should().Be(2);
    }

    // Hg issue 213: Segmentation Fault.
    [Test]
    [Skip("needs:lookaround - the atomic groups work from S20; the (?=...) lookahead has no opcode yet")]
    [Property("Upstream", "RegexTests.test_hg_bugs#258")]
    public void Nested_atomic_alternation_over_quoted_strings_does_not_match_a_non_conforming_subject() =>
        FuzzyRegex.Match(_hgIssue213Subject, _hgIssue213Pattern).Success.Should().BeFalse();
}
