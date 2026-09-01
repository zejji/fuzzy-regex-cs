using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.FindAll;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_re_findall</c>
/// (lines 320-343) and <c>test_bug_117612</c> (lines 345-347).
/// </summary>
/// <remarks>
/// There is no <c>findall</c> on this API; every assertion here uses
/// <see cref="FuzzyRegex.Matches(string, string, FuzzyRegexOptions, IReadOnlyDictionary{string, IReadOnlyCollection{string}})"/> instead. Upstream's
/// <c>findall</c> yields the whole match when the pattern has no groups, the text of the one
/// group when it has exactly one, and a tuple of group texts when it has two or more; a
/// tuple-valued case here becomes one assertion per group index rather than one assertion on a
/// tuple, per the group's own convention.
/// </remarks>
public sealed class FindAllTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#1")]
    public void Matches_is_empty_when_the_pattern_never_matches() =>
        FuzzyRegex.Matches("abc", ":+").Select(m => m.Value).Should().BeEmpty();

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#2")]
    public void Matches_value_is_the_whole_match_when_the_pattern_has_no_groups() =>
        FuzzyRegex.Matches("a:b::c:::d", ":+").Select(m => m.Value).Should().Equal(":", "::", ":::");

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#3")]
    public void Matches_group_one_value_is_used_when_the_pattern_has_exactly_one_group() =>
        FuzzyRegex.Matches("a:b::c:::d", "(:+)").Select(m => m.Groups[1].Value).Should().Equal(":", "::", ":::");

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#4")]
    public void Matches_group_one_value_for_a_two_group_pattern() =>
        FuzzyRegex.Matches("a:b::c:::d", "(:)(:*)").Select(m => m.Groups[1].Value).Should().Equal(":", ":", ":");

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#4")]
    public void Matches_group_two_value_for_a_two_group_pattern() =>
        FuzzyRegex.Matches("a:b::c:::d", "(:)(:*)").Select(m => m.Groups[2].Value).Should().Equal("", ":", "::");

    [Test]
    [Arguments(@"\((?P<test>.{0,5}?TEST)\)")]
    [Arguments(@"\((?P<test>.{0,3}?TEST)\)")]
    [Property("Upstream", "RegexTests.test_re_findall#5-6")]
    public void Matches_group_one_value_for_a_lazy_named_group_before_TEST(string pattern) =>
        FuzzyRegex.Matches("(MY TEST)", pattern).Select(m => m.Groups[1].Value).Should().Equal("MY TEST");

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#7")]
    public void Matches_group_one_value_for_a_lazy_named_group_before_T() =>
        FuzzyRegex.Matches("(MY T)", @"\((?P<test>.{0,3}?T)\)").Select(m => m.Groups[1].Value).Should().Equal("MY T");

    [Test]
    [Arguments(@"[^a]{2}[A-Z]", "\n  S", "  S")]
    [Arguments(@"[^a]{2,3}[A-Z]", "\n  S", "\n  S")]
    [Arguments(@"[^a]{2,3}[A-Z]", "\n   S", "   S")]
    [Property("Upstream", "RegexTests.test_re_findall#8-10")]
    public void Matches_value_for_negated_character_class_repeats(string pattern, string subject, string expected) =>
        FuzzyRegex.Matches(subject, pattern).Select(m => m.Value).Should().Equal(expected);

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#11")]
    public void Matches_group_one_value_for_a_group_repeated_one_or_two_times() =>
        FuzzyRegex
            .Matches("XYABCYPPQ\nQ DEF", @"X(Y[^Y]+?){1,2}( |Q)+DEF")
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("YPPQ\n");

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#11")]
    public void Matches_group_two_value_for_a_group_repeated_one_or_two_times() =>
        FuzzyRegex
            .Matches("XYABCYPPQ\nQ DEF", @"X(Y[^Y]+?){1,2}( |Q)+DEF")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal(" ");

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#12")]
    public void Matches_group_one_value_for_an_optional_nested_repeated_group() =>
        FuzzyRegex
            .Matches("\nTest\nxyz\nxyz\nEnd", @"(\nTest(\n+.+?){0,2}?)?\n+End")
            .Select(m => m.Groups[1].Value)
            .Should()
            .Equal("\nTest\nxyz\nxyz");

    [Test]
    [Property("Upstream", "RegexTests.test_re_findall#12")]
    public void Matches_group_two_value_for_an_optional_nested_repeated_group() =>
        FuzzyRegex
            .Matches("\nTest\nxyz\nxyz\nEnd", @"(\nTest(\n+.+?){0,2}?)?\n+End")
            .Select(m => m.Groups[2].Value)
            .Should()
            .Equal("\nxyz");

    [Test]
    [Property("Upstream", "RegexTests.test_bug_117612#1")]
    public void Matches_group_one_value_for_a_nested_alternation_group() =>
        FuzzyRegex.Matches("aba", "(a|(b))").Select(m => m.Groups[1].Value).Should().Equal("a", "b", "a");

    [Test]
    [Property("Upstream", "RegexTests.test_bug_117612#1")]
    public void Matches_group_two_value_for_a_nested_alternation_group()
    {
        // Upstream's findall substitutes '' for a group that did not participate in a match
        // (unlike Match.group(n), which is None there); this matches the built-in Regex's own
        // Group.Value, which is "" for an unsuccessful group.
        FuzzyRegex.Matches("aba", "(a|(b))").Select(m => m.Groups[2].Value).Should().Equal("", "b", "");
    }
}
