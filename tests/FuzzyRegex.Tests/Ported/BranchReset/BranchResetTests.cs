using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.BranchReset;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_branch_reset</c>
/// (lines 1610-1662): the <c>(?|...)</c> branch-reset construct, where each alternative reuses
/// the same group numbers, plus Hg issue 87's relaxation allowing the same group name to be
/// declared more than once across the alternatives.
/// </summary>
public sealed class BranchResetTests
{
    [Test]
    [Arguments("ac", "a", null)]
    [Arguments("bc", null, "b")]
    [Skip("needs:branch-reset - the parser has no alternation-group support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#1-2")]
    public void Unnamed_alternation_reports_the_matched_side_and_null_for_the_other(
        string subject,
        string? groupA,
        string? groupB
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(?:(a)|(b))(c)");

        m.Groups[1].Success.Should().Be(groupA is not null);
        if (groupA is not null)
        {
            m.Groups[1].Value.Should().Be(groupA);
        }
        m.Groups[2].Success.Should().Be(groupB is not null);
        if (groupB is not null)
        {
            m.Groups[2].Value.Should().Be(groupB);
        }
        m.Groups[3].Value.Should().Be("c");
    }

    [Test]
    [Arguments("ac", "a", null)]
    [Arguments("bc", null, "b")]
    [Skip("needs:branch-reset - the parser has no alternation-group support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#3-4")]
    public void Named_alternation_reports_the_matched_side_and_null_for_the_other(
        string subject,
        string? groupA,
        string? groupB
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(?:(?<a>a)|(?<b>b))(?<c>c)");

        m.Groups[1].Success.Should().Be(groupA is not null);
        if (groupA is not null)
        {
            m.Groups[1].Value.Should().Be(groupA);
        }
        m.Groups[2].Success.Should().Be(groupB is not null);
        if (groupB is not null)
        {
            m.Groups[2].Value.Should().Be(groupB);
        }
        m.Groups[3].Value.Should().Be("c");
    }

    [Test]
    [Arguments("abd", "b", null)]
    [Arguments("acd", null, "c")]
    [Skip("needs:branch-reset - the parser has no alternation-group support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#5-6")]
    public void Named_alternation_between_two_fixed_named_groups_reports_null_for_the_untaken_branch(
        string subject,
        string? groupB,
        string? groupC
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(?<a>a)(?:(?<b>b)|(?<c>c))(?<d>d)");

        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Success.Should().Be(groupB is not null);
        if (groupB is not null)
        {
            m.Groups[2].Value.Should().Be(groupB);
        }
        m.Groups[3].Success.Should().Be(groupC is not null);
        if (groupC is not null)
        {
            m.Groups[3].Value.Should().Be(groupC);
        }
        m.Groups[4].Value.Should().Be("d");
    }

    [Test]
    [Arguments("abd", "b", null)]
    [Arguments("acd", null, "c")]
    [Skip("needs:branch-reset - the parser has no alternation-group support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#7-8")]
    public void Unnamed_alternation_between_two_fixed_groups_reports_null_for_the_untaken_branch(
        string subject,
        string? groupB,
        string? groupC
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(a)(?:(b)|(c))(d)");

        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Success.Should().Be(groupB is not null);
        if (groupB is not null)
        {
            m.Groups[2].Value.Should().Be(groupB);
        }
        m.Groups[3].Success.Should().Be(groupC is not null);
        if (groupC is not null)
        {
            m.Groups[3].Value.Should().Be(groupC);
        }
        m.Groups[4].Value.Should().Be("d");
    }

    [Test]
    [Skip("needs:branch-reset - the parser has no (?|...) branch-reset support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#9")]
    public void Branch_reset_merges_two_identical_unnamed_alternatives_into_one_group_number()
    {
        Match m = FuzzyRegex.MatchAtStart("abd", @"(a)(?|(b)|(b))(d)");

        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Value.Should().Be("b");
        m.Groups[3].Value.Should().Be("d");
    }

    [Test]
    [Arguments("ac", "a", null)]
    [Arguments("bc", null, "b")]
    [Skip("needs:branch-reset - the parser has no (?|...) branch-reset support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#10-11")]
    public void Branch_reset_with_differently_named_alternatives_keeps_separate_group_numbers(
        string subject,
        string? groupA,
        string? groupB
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(?|(?<a>a)|(?<b>b))(c)");

        m.Groups[1].Success.Should().Be(groupA is not null);
        if (groupA is not null)
        {
            m.Groups[1].Value.Should().Be(groupA);
        }
        m.Groups[2].Success.Should().Be(groupB is not null);
        if (groupB is not null)
        {
            m.Groups[2].Value.Should().Be(groupB);
        }
        m.Groups[3].Value.Should().Be("c");
    }

    // Hg issue 87: the same name in both alternatives merges to a single group number, so this
    // pattern has only two groups, not three.
    [Test]
    [Arguments("ac", "a")]
    [Arguments("bc", "b")]
    [Skip("needs:branch-reset - the parser has no (?|...) branch-reset support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#12-13")]
    public void Branch_reset_with_the_same_name_in_both_alternatives_merges_to_one_group(string subject, string groupA)
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(?|(?<a>a)|(?<a>b))(c)");

        m.Groups[1].Value.Should().Be(groupA);
        m.Groups[2].Value.Should().Be("c");
    }

    [Test]
    [Arguments("abe", "a", "b")]
    [Arguments("cde", "d", "c")]
    [Skip("needs:branch-reset - the parser has no (?|...) branch-reset support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#14-15")]
    public void Branch_reset_group_numbers_follow_the_name_not_the_position_when_both_sides_are_named(
        string subject,
        string groupA,
        string groupB
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(?|(?<a>a)(?<b>b)|(?<b>c)(?<a>d))(e)");

        m.Groups[1].Value.Should().Be(groupA);
        m.Groups[2].Value.Should().Be(groupB);
        m.Groups[3].Value.Should().Be("e");
    }

    [Test]
    [Arguments("abe", "a", "b")]
    [Arguments("cde", "d", "c")]
    [Skip("needs:branch-reset - the parser has no (?|...) branch-reset support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#16-17")]
    public void Branch_reset_group_numbers_follow_the_name_when_only_the_second_side_reuses_it(
        string subject,
        string groupA,
        string groupB
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(?|(?<a>a)(?<b>b)|(?<b>c)(d))(e)");

        m.Groups[1].Value.Should().Be(groupA);
        m.Groups[2].Value.Should().Be(groupB);
        m.Groups[3].Value.Should().Be("e");
    }

    [Test]
    [Arguments("abe", "a", "b")]
    [Arguments("cde", "c", "d")]
    [Skip("needs:branch-reset - the parser has no (?|...) branch-reset support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#18-19")]
    public void Branch_reset_with_a_fully_unnamed_second_alternative_reverts_to_positional_numbering(
        string subject,
        string groupA,
        string groupB
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, @"(?|(?<a>a)(?<b>b)|(c)(d))(e)");

        m.Groups[1].Value.Should().Be(groupA);
        m.Groups[2].Value.Should().Be(groupB);
        m.Groups[3].Value.Should().Be("e");
    }

    // Hg issue 87: the second alternative's unnamed first group shares group 1 with the first
    // alternative's <a> positionally, and its own <a> also writes to group 1 by name, so a single
    // match leaves two captures behind the one name "a" even though only one alternative ran.
    [Test]
    [Skip("needs:branch-reset - the parser has no (?|...) branch-reset support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#20-21")]
    public void Branch_reset_duplicate_name_group_reports_the_first_alternatives_single_capture()
    {
        Match m = FuzzyRegex.MatchAtStart("abe", @"(?|(?<a>a)(?<b>b)|(c)(?<a>d))(e)");

        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Value.Should().Be("b");
        m.Groups[3].Value.Should().Be("e");
        m.Groups["a"].Captures.Select(c => c.Value).Should().Equal("a");
        m.Groups["b"].Captures.Select(c => c.Value).Should().Equal("b");
    }

    [Test]
    [Skip("needs:branch-reset - the parser has no (?|...) branch-reset support yet")]
    [Property("Upstream", "RegexTests.test_branch_reset#22-23")]
    public void Branch_reset_duplicate_name_group_reports_both_of_the_second_alternatives_captures()
    {
        Match m = FuzzyRegex.MatchAtStart("cde", @"(?|(?<a>a)(?<b>b)|(c)(?<a>d))(e)");

        m.Groups[1].Value.Should().Be("d");
        m.Groups[2].Success.Should().BeFalse();
        m.Groups[3].Value.Should().Be("e");
        m.Groups["a"].Captures.Select(c => c.Value).Should().Equal("c", "d");
        m.Groups["b"].Captures.Should().BeEmpty();
    }
}
