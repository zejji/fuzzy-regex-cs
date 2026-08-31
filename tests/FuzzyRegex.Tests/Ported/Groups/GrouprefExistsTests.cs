using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_re_groupref_exists</c>
/// (lines 376-397).
/// </summary>
public sealed class GrouprefExistsTests
{
    [Test]
    // Group 1 is the optional opening paren alone, not the whole match: upstream's
    // regex.match(r'^(\()?([^()]+)(?(1)\))$', '(a)')[:] is ('(a)', '(', 'a').
    [Arguments("(", "a", "(a)")]
    [Arguments(null, "a", "a")]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#1-2")]
    public void A_conditional_on_an_optional_leading_paren_matches(string? group1, string group2, string subject)
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"^(\()?([^()]+)(?(1)\))$");

        m.Value.Should().Be(subject);
        m.Groups[1].Success.Should().Be(group1 is not null);
        if (group1 is not null)
        {
            m.Groups[1].Value.Should().Be(group1);
        }
        m.Groups[2].Value.Should().Be(group2);
    }

    [Test]
    [Arguments("a)")]
    [Arguments("(a")]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#3-4")]
    public void A_conditional_on_a_mismatched_leading_paren_does_not_match(string subject) =>
        FuzzyRegex.MatchAtStart(subject, @"^(\()?([^()]+)(?(1)\))$").Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#5")]
    public void A_conditional_choosing_between_two_branches_when_the_first_alternative_matched()
    {
        Match m = FuzzyRegex.MatchAtStart("ab", "^(?:(a)|c)((?(1)b|d))$");

        m.Value.Should().Be("ab");
        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Value.Should().Be("b");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#6")]
    public void A_conditional_choosing_between_two_branches_when_the_first_alternative_did_not_match()
    {
        Match m = FuzzyRegex.MatchAtStart("cd", "^(?:(a)|c)((?(1)b|d))$");

        m.Value.Should().Be("cd");
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[2].Value.Should().Be("d");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#7")]
    public void A_conditional_with_an_empty_yes_branch_when_the_condition_group_did_not_match()
    {
        Match m = FuzzyRegex.MatchAtStart("cd", "^(?:(a)|c)((?(1)|d))$");

        m.Value.Should().Be("cd");
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[2].Value.Should().Be("d");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#8")]
    public void A_conditional_with_an_empty_yes_branch_when_the_condition_group_matched()
    {
        Match m = FuzzyRegex.MatchAtStart("a", "^(?:(a)|c)((?(1)|d))$");

        m.Value.Should().Be("a");
        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Value.Should().Be("");
    }

    // Bug #1177831: exercise a condition group other than group 1.
    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#9")]
    public void A_conditional_on_a_named_group_other_than_the_first()
    {
        Match m = FuzzyRegex.MatchAtStart("abc", "(?P<g1>a)(?P<g2>b)?((?(g2)c|d))");

        m.Value.Should().Be("abc");
        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Value.Should().Be("b");
        m.Groups[3].Value.Should().Be("c");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#10")]
    public void A_conditional_on_a_named_group_that_did_not_participate()
    {
        Match m = FuzzyRegex.MatchAtStart("ad", "(?P<g1>a)(?P<g2>b)?((?(g2)c|d))");

        m.Value.Should().Be("ad");
        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Success.Should().BeFalse();
        m.Groups[3].Value.Should().Be("d");
    }

    [Test]
    [Arguments("abd")]
    [Arguments("ac")]
    [Property("Upstream", "RegexTests.test_re_groupref_exists#11-12")]
    public void A_conditional_on_a_named_group_rejects_the_wrong_branch(string subject) =>
        FuzzyRegex.MatchAtStart(subject, "(?P<g1>a)(?P<g2>b)?((?(g2)c|d))").Success.Should().BeFalse();
}
