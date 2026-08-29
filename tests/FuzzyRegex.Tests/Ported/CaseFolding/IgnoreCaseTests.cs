using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.CaseFolding;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_ignore_case</c>
/// (lines 558-582). Upstream's <c>bytes</c> assertion (line 560) is not ported.
/// </summary>
public sealed class IgnoreCaseTests
{
    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#1")]
    public void Literal_matches_case_insensitively() =>
        FuzzyRegex.MatchAtStart("ABC", "abc", FuzzyRegexOptions.IgnoreCase).Value.Should().Be("ABC");

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#3")]
    public void Negated_set_star_captures_case_insensitively() =>
        FuzzyRegex
            .MatchAtStart("a bb", "(a\\s[^a]*)", FuzzyRegexOptions.IgnoreCase)
            .Groups[1]
            .Value.Should()
            .Be("a bb");

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#4")]
    public void Set_captures_case_insensitively() =>
        FuzzyRegex.MatchAtStart("a b", "(a\\s[abc])", FuzzyRegexOptions.IgnoreCase).Groups[1].Value.Should().Be("a b");

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#5")]
    public void Set_star_captures_case_insensitively() =>
        FuzzyRegex
            .MatchAtStart("a bb", "(a\\s[abc]*)", FuzzyRegexOptions.IgnoreCase)
            .Groups[1]
            .Value.Should()
            .Be("a bb");

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive backreference matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#6")]
    public void Backreference_matches_case_insensitively() =>
        FuzzyRegex.MatchAtStart("a a", "((a)\\s\\2)", FuzzyRegexOptions.IgnoreCase).Groups[1].Value.Should().Be("a a");

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive backreference matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#7")]
    public void Backreference_star_matches_case_insensitively() =>
        FuzzyRegex
            .MatchAtStart("a aa", "((a)\\s\\2*)", FuzzyRegexOptions.IgnoreCase)
            .Groups[1]
            .Value.Should()
            .Be("a aa");

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#8")]
    public void Alternation_with_backreference_captures_case_insensitively() =>
        FuzzyRegex
            .MatchAtStart("a a", "((a)\\s(abc|a))", FuzzyRegexOptions.IgnoreCase)
            .Groups[1]
            .Value.Should()
            .Be("a a");

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#9")]
    public void Alternation_star_captures_case_insensitively() =>
        FuzzyRegex
            .MatchAtStart("a aa", "((a)\\s(abc|a)*)", FuzzyRegexOptions.IgnoreCase)
            .Groups[1]
            .Value.Should()
            .Be("a aa");

    // Issue 3511: a range spanning between uppercase and lowercase (Z=0x5A to a=0x61) must not
    // become an unbounded case-insensitive range once (?i) is applied.
    [Test]
    [Skip("needs:character-classes - the engine has no range set matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#10")]
    public void Range_between_cases_matches_a_character_between_them()
    {
        Match m = FuzzyRegex.MatchAtStart("_", "[Z-a]");

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
    }

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#11")]
    public void Range_between_cases_is_unaffected_by_inline_ignore_case()
    {
        Match m = FuzzyRegex.MatchAtStart("_", "(?i)[Z-a]");

        (m.Index, m.Index + m.Length).Should().Be((0, 1));
    }

    [Test]
    [Skip("needs:ignore-case - the engine has no inline (?i) flag yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#12")]
    public void Inline_ignore_case_flag_matches_a_differently_cased_literal() =>
        FuzzyRegex.MatchAtStart("nAo", "(?i)nao").Success.Should().BeTrue();

    [Test]
    [Skip("needs:ignore-case - the engine has no inline (?i) flag yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#13")]
    public void Inline_ignore_case_flag_matches_a_differently_cased_accented_literal() =>
        FuzzyRegex.MatchAtStart("nÃo", "(?i)não").Success.Should().BeTrue();

    [Test]
    [Skip("needs:ignore-case - the engine has no inline (?i) flag yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#14")]
    public void Inline_ignore_case_flag_matches_a_fully_upper_cased_accented_literal() =>
        FuzzyRegex.MatchAtStart("NÃO", "(?i)não").Success.Should().BeTrue();

    [Test]
    [Skip("needs:ignore-case - the engine has no case-insensitive matching yet")]
    [Property("Upstream", "RegexTests.test_ignore_case#15")]
    public void Inline_ignore_case_flag_matches_long_s_against_s() =>
        FuzzyRegex.MatchAtStart("ſ", "(?i)s").Success.Should().BeTrue();
}
