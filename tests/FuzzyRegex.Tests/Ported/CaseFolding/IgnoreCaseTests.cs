using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CaseFolding;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_ignore_case</c>
/// (lines 558-582). Upstream's <c>bytes</c> assertion (line 560) is not ported.
/// </summary>
public sealed class IgnoreCaseTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#1")]
    public void Literal_matches_case_insensitively() =>
        Upstream.MatchAtStart("ABC", "abc", FuzzyRegexOptions.IgnoreCase).Value.Should().Be("ABC");

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#3")]
    public void Negated_set_star_captures_case_insensitively() =>
        Upstream.MatchAtStart("a bb", "(a\\s[^a]*)", FuzzyRegexOptions.IgnoreCase).Groups[1].Value.Should().Be("a bb");

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#4")]
    public void Set_captures_case_insensitively() =>
        Upstream.MatchAtStart("a b", "(a\\s[abc])", FuzzyRegexOptions.IgnoreCase).Groups[1].Value.Should().Be("a b");

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#5")]
    public void Set_star_captures_case_insensitively() =>
        Upstream.MatchAtStart("a bb", "(a\\s[abc]*)", FuzzyRegexOptions.IgnoreCase).Groups[1].Value.Should().Be("a bb");

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#6")]
    public void Backreference_matches_case_insensitively() =>
        Upstream.MatchAtStart("a a", "((a)\\s\\2)", FuzzyRegexOptions.IgnoreCase).Groups[1].Value.Should().Be("a a");

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#7")]
    public void Backreference_star_matches_case_insensitively() =>
        Upstream.MatchAtStart("a aa", "((a)\\s\\2*)", FuzzyRegexOptions.IgnoreCase).Groups[1].Value.Should().Be("a aa");

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#8")]
    public void Alternation_with_backreference_captures_case_insensitively() =>
        Upstream
            .MatchAtStart("a a", "((a)\\s(abc|a))", FuzzyRegexOptions.IgnoreCase)
            .Groups[1]
            .Value.Should()
            .Be("a a");

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#9")]
    public void Alternation_star_captures_case_insensitively() =>
        Upstream
            .MatchAtStart("a aa", "((a)\\s(abc|a)*)", FuzzyRegexOptions.IgnoreCase)
            .Groups[1]
            .Value.Should()
            .Be("a aa");

    // Issue 3511: a range spanning between uppercase and lowercase (Z=0x5A to a=0x61) must not
    // become an unbounded case-insensitive range once (?i) is applied.
    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#10")]
    public void Range_between_cases_matches_a_character_between_them()
    {
        Match m = Upstream.MatchAtStart("_", "[Z-a]");

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#11")]
    public void Range_between_cases_is_unaffected_by_inline_ignore_case()
    {
        Match m = Upstream.MatchAtStart("_", "(?i)[Z-a]");

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
    }

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#12")]
    public void Inline_ignore_case_flag_matches_a_differently_cased_literal() =>
        Upstream.MatchAtStart("nAo", "(?i)nao").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#13")]
    public void Inline_ignore_case_flag_matches_a_differently_cased_accented_literal() =>
        Upstream.MatchAtStart("nÃo", "(?i)não").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#14")]
    public void Inline_ignore_case_flag_matches_a_fully_upper_cased_accented_literal() =>
        Upstream.MatchAtStart("NÃO", "(?i)não").Success.Should().BeTrue();

    [Test]
    [Property("Upstream", "RegexTests.test_ignore_case#15")]
    public void Inline_ignore_case_flag_matches_long_s_against_s() =>
        Upstream.MatchAtStart("ſ", "(?i)s").Success.Should().BeTrue();
}
