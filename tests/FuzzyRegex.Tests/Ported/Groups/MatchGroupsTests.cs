using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_re_match</c> (lines 349-374).
/// </summary>
public sealed class MatchGroupsTests
{
    [Test]
    [Skip("needs:groups - group capture is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#1")]
    public void A_pattern_with_no_groups_reports_only_the_whole_match() =>
        FuzzyRegex.MatchAtStart("a", "a").Value.Should().Be("a");

    [Test]
    [Skip("needs:groups - group capture is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#2")]
    public void A_single_group_participates_in_the_whole_tuple()
    {
        Match m = FuzzyRegex.MatchAtStart("a", "(a)");

        m.Value.Should().Be("a");
        m.Groups[1].Value.Should().Be("a");
    }

    [Test]
    [Skip("needs:groups - group capture is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#3")]
    public void Indexing_group_zero_is_the_whole_match() => FuzzyRegex.MatchAtStart("a", "(a)").Value.Should().Be("a");

    [Test]
    [Skip("needs:groups - group capture is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#4")]
    public void Indexing_group_one_is_its_capture() =>
        FuzzyRegex.MatchAtStart("a", "(a)").Groups[1].Value.Should().Be("a");

    [Test]
    [Skip("needs:groups - group capture is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#5")]
    public void Requesting_the_same_group_twice_returns_it_twice()
    {
        Match m = FuzzyRegex.MatchAtStart("a", "(a)");

        m.Groups[1].Value.Should().Be("a");
        m.Groups[1].Value.Should().Be("a");
    }

    // Assertions 9 and 10 are the same call repeated verbatim in upstream (lines 360-361); both
    // rows are kept below so the count of ported assertions matches the source.
    [Test]
    [Arguments("a", "a", "a", true, "a", false, null, false, null)]
    [Arguments("b", "b", "b", false, null, true, "b", false, null)]
    [Arguments("ac", "ac", "a", true, "a", false, null, true, "c")]
    [Arguments("bc", "bc", "b", false, null, true, "b", true, "c")]
    [Arguments("bc", "bc", "b", false, null, true, "b", true, "c")]
    [Skip("needs:groups - group capture is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#6-10")]
    public void Alternation_with_a_trailing_optional_group_reports_every_group(
        string subject,
        string whole,
        string group1,
        bool group2Participates,
        string? group2,
        bool group3Participates,
        string? group3,
        bool group4Participates,
        string? group4
    )
    {
        Match m = FuzzyRegex.MatchAtStart(subject, "((a)|(b))(c)?");

        m.Value.Should().Be(whole);
        m.Groups[1].Value.Should().Be(group1);

        m.Groups[2].Success.Should().Be(group2Participates);
        if (group2Participates)
        {
            m.Groups[2].Value.Should().Be(group2);
        }

        m.Groups[3].Success.Should().Be(group3Participates);
        if (group3Participates)
        {
            m.Groups[3].Value.Should().Be(group3);
        }

        m.Groups[4].Success.Should().Be(group4Participates);
        if (group4Participates)
        {
            m.Groups[4].Value.Should().Be(group4);
        }
    }

    [Test]
    [Skip("needs:groups - group capture is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#11-14")]
    public void Group_accessor_forms_agree_for_a_single_capturing_group()
    {
        Match m = FuzzyRegex.MatchAtStart("a", "(a)");

        m.Value.Should().Be("a"); // m.group()
        m.Value.Should().Be("a"); // m.group(0)
        m.Groups[1].Value.Should().Be("a"); // m.group(1)
        m.Groups[1].Value.Should().Be("a"); // m.group(1, 1), first element
        m.Groups[1].Value.Should().Be("a"); // m.group(1, 1), second element
    }

    [Test]
    [Skip(
        "needs:named-groups - the pattern needs named-group parsing even though this test reads the groups by number"
    )]
    [Property("Upstream", "RegexTests.test_re_match#15")]
    public void Multiple_numbered_groups_can_be_requested_together()
    {
        Match m = FuzzyRegex.MatchAtStart("a", "(?:(?P<a1>a)|(?P<b2>b))(?P<c3>c)?");

        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Success.Should().BeFalse();
        m.Groups[3].Success.Should().BeFalse();
    }

    [Test]
    [Skip("needs:named-groups - named group parsing is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#16")]
    public void Multiple_named_groups_can_be_requested_together()
    {
        Match m = FuzzyRegex.MatchAtStart("b", "(?:(?P<a1>a)|(?P<b2>b))(?P<c3>c)?");

        m.Groups["a1"].Success.Should().BeFalse();
        m.Groups["b2"].Value.Should().Be("b");
        m.Groups["c3"].Success.Should().BeFalse();
    }

    [Test]
    [Skip("needs:named-groups - named group parsing is not implemented yet")]
    [Property("Upstream", "RegexTests.test_re_match#17")]
    public void Numbered_and_named_group_requests_can_be_mixed()
    {
        Match m = FuzzyRegex.MatchAtStart("ac", "(?:(?P<a1>a)|(?P<b2>b))(?P<c3>c)?");

        m.Groups[1].Value.Should().Be("a");
        m.Groups["b2"].Success.Should().BeFalse();
        m.Groups[3].Value.Should().Be("c");
    }
}
