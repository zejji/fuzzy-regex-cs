using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Groups;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_re_groupref</c> (lines 399-413).
/// </summary>
public sealed class GrouprefTests
{
    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref#1")]
    public void A_backreference_to_a_leading_optional_delimiter_requires_the_matching_close()
    {
        Match m = Upstream.MatchAtStart("|a|", @"^(\|)?([^()]+)\1$");

        m.Value.Should().Be("|a|");
        m.Groups[1].Value.Should().Be("|");
        m.Groups[2].Value.Should().Be("a");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref#2")]
    public void An_optional_backreference_may_be_absent_on_both_sides()
    {
        Match m = Upstream.MatchAtStart("a", @"^(\|)?([^()]+)\1?$");

        m.Value.Should().Be("a");
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[2].Value.Should().Be("a");
    }

    [Test]
    [Arguments("a|")]
    [Arguments("|a")]
    [Property("Upstream", "RegexTests.test_re_groupref#3-4")]
    public void A_backreference_rejects_a_one_sided_delimiter(string subject) =>
        Upstream.MatchAtStart(subject, @"^(\|)?([^()]+)\1$").Success.Should().BeFalse();

    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref#5")]
    public void A_backreference_to_the_taken_alternative_matches()
    {
        Match m = Upstream.MatchAtStart("aa", @"^(?:(a)|c)(\1)$");

        m.Value.Should().Be("aa");
        m.Groups[1].Value.Should().Be("a");
        m.Groups[2].Value.Should().Be("a");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_re_groupref#6")]
    public void An_optional_backreference_to_a_group_that_did_not_participate_may_be_absent()
    {
        Match m = Upstream.MatchAtStart("c", @"^(?:(a)|c)(\1)?$");

        m.Value.Should().Be("c");
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[2].Success.Should().BeFalse();
    }

    [Test]
    // S21 delivered the backreference; what is left is 'Matches', which is S25.
    [Property("Upstream", "RegexTests.test_re_groupref#7")]
    public void Findall_resolves_a_backreference_to_an_earlier_group()
    {
        MatchCollection matches = Upstream.Matches(
            "TEST, BEST; LEST ; Lest 123 Test, Best",
            @"(?i)(.{1,40}?),(.{1,40}?)(?:;)+(.{1,80}).{1,40}?\3(\ |;)+(.{1,80}?)\1"
        );

        matches.Should().HaveCount(1);
        Match m = matches[0];
        m.Groups[1].Value.Should().Be("TEST");
        m.Groups[2].Value.Should().Be(" BEST");
        m.Groups[3].Value.Should().Be(" LEST");
        m.Groups[4].Value.Should().Be(" ");
        m.Groups[5].Value.Should().Be("123 ");
    }
}
