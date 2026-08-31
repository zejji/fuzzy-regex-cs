using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_bug_527371</c> (lines 758-765).
/// </summary>
public sealed class Bug527371Tests
{
    [Test]
    [Property("Upstream", "RegexTests.test_bug_527371#1")]
    [Skip(
        "needs:quantifiers - LastGroupNumber itself answers from S18, but '(a)?a' is an optional group and only a repeat can make a group backtrack out"
    )]
    public void LastGroupNumber_is_absent_when_the_only_group_backtracked_out() =>
        FuzzyRegex.MatchAtStart("a", "(a)?a").LastGroupNumber.Should().Be(-1);

    [Test]
    [Property("Upstream", "RegexTests.test_bug_527371#2")]
    [Skip("needs:quantifiers - '(a)(b)?b' has an optional group")]
    public void LastGroupNumber_is_the_group_that_participated_when_a_later_one_backtracked_out() =>
        FuzzyRegex.MatchAtStart("ab", "(a)(b)?b").LastGroupNumber.Should().Be(1);

    [Test]
    [Property("Upstream", "RegexTests.test_bug_527371#3")]
    [Skip("needs:quantifiers - '(?P<a>a)(?P<b>b)?b' has an optional group")]
    public void LastGroupName_is_the_named_group_that_participated_when_a_later_one_backtracked_out() =>
        FuzzyRegex.MatchAtStart("ab", "(?P<a>a)(?P<b>b)?b").LastGroupName.Should().Be("a");

    [Test]
    [Property("Upstream", "RegexTests.test_bug_527371#4")]
    public void LastGroupName_is_the_last_named_group_even_when_an_unnamed_group_matched_later() =>
        FuzzyRegex.MatchAtStart("ab", "(?P<a>a(b))").LastGroupName.Should().Be("a");

    [Test]
    [Property("Upstream", "RegexTests.test_bug_527371#5")]
    public void LastGroupNumber_is_the_outer_group_even_though_both_groups_span_the_same_text() =>
        FuzzyRegex.MatchAtStart("a", "((a))").LastGroupNumber.Should().Be(1);
}
