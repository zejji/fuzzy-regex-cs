using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Gap tests for S18's <c>BRANCH</c>, <c>START_GROUP</c> and <c>END_GROUP</c>: the behaviours the
/// ported suite either does not reach at this stage of the port or reaches only through a
/// quantifier, which is S19's.
/// </summary>
/// <remarks>
/// Every expected value below was probed against <c>regex</c> 2026.7.19 on 2026-08-31 and is quoted
/// beside the assertion as Python spells it. None of these patterns holds a quantifier, so all of
/// them run on the S18 engine.
/// </remarks>
public sealed class GroupsAndAlternationTests
{
    [Test]
    public void A_group_captured_in_a_branch_that_later_fails_is_not_reported()
    {
        // Upstream: regex.match('(a)c|ab', 'ab').span(1) == (-1, -1). The first branch captures
        // group 1 and then fails on 'c', so the END_GROUP backtrack case has to undo the capture
        // before the second branch matches. A group whose spans are not restored reports 'a' here,
        // which is the classic silent engine bug this test exists to pin.
        Match m = FuzzyRegex.MatchAtStart("ab", "(a)c|ab");

        m.Value.Should().Be("ab");
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[1].Captures.Should().BeEmpty();
        m.LastGroupNumber.Should().Be(-1, "upstream lastindex is None");
    }

    [Test]
    public void A_group_that_matched_empty_is_not_the_same_as_one_that_did_not_match()
    {
        // Upstream: regex.match('(x|)a', 'a').span(1) == (0, 0) and group(1) == ''; whereas
        // regex.match('(x)a|a', 'a').span(1) == (-1, -1) and group(1) is None. same_span_as_group
        // (upstream/src/_regex.c:11639) is where the two are told apart: a group with no current
        // capture took no part in the match, where a (pos, pos) span is a real, empty capture.
        Match empty = FuzzyRegex.MatchAtStart("a", "(x|)a");

        empty.Groups[1].Success.Should().BeTrue();
        empty.Groups[1].Value.Should().BeEmpty();
        (empty.Groups[1].Index, empty.Groups[1].Length).Should().Be((0, 0));
        empty.Groups[1].Captures.Select(static c => (c.Index, c.Length)).Should().Equal((0, 0));
        empty.LastGroupNumber.Should().Be(1);

        Match absent = FuzzyRegex.MatchAtStart("a", "(x)a|a");

        absent.Groups[1].Success.Should().BeFalse();
        absent.Groups[1].Captures.Should().BeEmpty();
        absent.LastGroupNumber.Should().Be(-1);
    }

    [Test]
    public void An_empty_first_alternative_backtracks_to_the_second_and_reports_its_capture()
    {
        // Upstream: regex.match('(|a)b', 'ab').span(1) == (0, 1). The empty alternative is tried
        // first, 'b' fails at position 0, and the BRANCH backtrack case has to restore both the text
        // position and the group before the 'a' alternative is tried.
        Match m = FuzzyRegex.MatchAtStart("ab", "(|a)b");

        m.Groups[1].Value.Should().Be("a");
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 1));
    }

    [Test]
    public void Nested_groups_each_report_their_own_span()
    {
        // Upstream: regex.match('((a)(b))', 'ab') spans are (0,2), (0,2), (0,1), (1,2) as
        // (start, end), which is (0,2), (0,2), (0,1), (1,1) as (Index, Length). lastindex is 1 - the
        // group that closed last, not the highest-numbered one.
        Match m = FuzzyRegex.MatchAtStart("ab", "((a)(b))");

        m.Groups.Select(static g => (g.Index, g.Length)).Should().Equal((0, 2), (0, 2), (0, 1), (1, 1));
        m.LastGroupNumber.Should().Be(1);
        m.LastGroupName.Should().BeNull();
    }

    [Test]
    public void An_inner_group_of_an_unmatched_alternative_is_absent_too()
    {
        // Upstream: regex.match('(a(b)|c)d', 'cd') gives group 1 == 'c' and group 2 is None.
        Match m = FuzzyRegex.MatchAtStart("cd", "(a(b)|c)d");

        m.Groups[1].Value.Should().Be("c");
        m.Groups[2].Success.Should().BeFalse();
        m.LastGroupNumber.Should().Be(1);
    }

    [Test]
    public void The_leftmost_alternative_wins_even_when_a_later_one_would_match_more()
    {
        // Upstream: regex.match('a|ab', 'ab').span(0) == (0, 1). Alternation is ordered, not
        // longest-wins, so BRANCH must try next_1 before next_2.
        FuzzyRegex.MatchAtStart("ab", "a|ab").Value.Should().Be("a");
    }

    [Test]
    public void Two_matches_from_one_pattern_keep_their_own_group_spans()
    {
        // The engine reuses one state's group arrays across the whole search and the pool reuses
        // them across calls, so pattern_new_match takes a copy (copy_groups,
        // upstream/src/_regex.c:20621). A Match that shared the arrays would change under its owner
        // as soon as the next match ran.
        var regex = new FuzzyRegex("(a)b|(c)b");

        Match first = regex.MatchAtStart("ab");
        Match second = regex.MatchAtStart("cb");

        first.Groups[1].Value.Should().Be("a");
        first.Groups[2].Success.Should().BeFalse();
        first.LastGroupNumber.Should().Be(1);

        second.Groups[1].Success.Should().BeFalse();
        second.Groups[2].Value.Should().Be("c");
        second.LastGroupNumber.Should().Be(2);
    }

    [Test]
    public void A_named_group_is_reachable_by_name_and_reports_that_name()
    {
        Match m = FuzzyRegex.MatchAtStart("ab", "(?P<first>a)(?P<second>b)");

        m.Groups["first"].Value.Should().Be("a");
        m.Groups["second"].Value.Should().Be("b");
        m.Groups[1].Name.Should().Be("first");
        m.LastGroupName.Should().Be("second");

        Action unknown = () => _ = m.Groups["third"];
        unknown.Should().Throw<ArgumentOutOfRangeException>("the pattern has no group of that name");
    }

    [Test]
    public void An_unsuccessful_match_still_reports_the_groups_the_pattern_declares()
    {
        Match m = FuzzyRegex.MatchAtStart("z", "(a)(b)");

        m.Success.Should().BeFalse();
        m.Groups.Count.Should().Be(3);
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[2].Success.Should().BeFalse();
        m.LastGroupNumber.Should().Be(-1);
        m.LastGroupName.Should().BeNull();
    }

    [Test]
    public void A_group_inside_a_branch_of_each_of_two_branches_reports_both()
    {
        // Upstream: regex.match('(a|b)(c|d)', 'bd') spans are (0,2), (0,1), (1,2), lastindex 2.
        Match m = FuzzyRegex.MatchAtStart("bd", "(a|b)(c|d)");

        m.Groups.Select(static g => g.Value).Should().Equal("bd", "b", "d");
        m.LastGroupNumber.Should().Be(2);
    }
}
