using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Regressions;

/// <summary>
/// Ported from <c>upstream/regex/tests/test_regex.py</c> <c>test_hg_bugs</c>, the assertions
/// about group capture.
/// </summary>
public sealed class RegressionsGroupTests
{
    // Hg issue 296: Group references are not taken into account when group is reporting the last
    // match.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#326-327")]
    [Skip("needs:recursion - '(?P<x>.)*(?&x)' needs a group call as well as the repeat")]
    public void Subroutine_call_captures_every_iteration_but_group_reports_the_last()
    {
        Match m = FuzzyRegex.FullMatch("abc", "(?P<x>.)*(?&x)");

        m.Groups["x"].Captures.Select(c => c.Value).Should().Equal("a", "b", "c");
        m.Groups["x"].Value.Should().Be("b");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#328-329")]
    public void Three_groups_sharing_one_name_capture_all_three_and_group_reports_the_last()
    {
        Match m = FuzzyRegex.FullMatch("abc", "(?P<x>.)(?P<x>.)(?P<x>.)");

        m.Groups["x"].Captures.Select(c => c.Value).Should().Equal("a", "b", "c");
        m.Groups["x"].Value.Should().Be("c");
    }

    // Git issue 474: regex has no equivalent to `re.Match.groups()` for captures.
    // Upstream's `allcaptures()`/`allspans()` have no counterpart on our `Match`; expressed here
    // through `Groups`/`Captures` rather than ported as new API.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#435-436")]
    [Skip("needs:quantifiers - '(.)+' repeats a capture group")]
    public void All_captures_and_spans_of_a_repeated_group_are_available_via_groups_and_captures()
    {
        Match m = FuzzyRegex.MatchAtStart("abc", @"(.)+");
        Group[] groups = [.. m.Groups];

        groups.Should().HaveCount(2);
        groups[0].Captures.Select(c => c.Value).Should().Equal("abc");
        groups[1].Captures.Select(c => c.Value).Should().Equal("a", "b", "c");
        groups[0].Captures.Select(c => (c.Index, c.Length)).Should().Equal((0, 3));
        // Upstream allspans() reports (start, end); Capture exposes (Index, Length), so the
        // three one-character captures are (0, 1), (1, 1), (2, 1), not the raw span pairs.
        groups[1].Captures.Select(c => (c.Index, c.Length)).Should().Equal((0, 1), (1, 1), (2, 1));
    }

    // Hg issue 100: strange results from regex.search.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#71")]
    [Skip("needs:quantifiers - '^([^z]*(?:WWWi|W))?$' has a repeat and an optional group")]
    public void Optional_group_around_an_uppercase_alternative_captures_the_whole_subject()
    {
        Match m = FuzzyRegex.Match("WWWi", "^([^z]*(?:WWWi|W))?$");

        m.Groups.Skip(1).Select(g => g.Success ? g.Value : null).Should().Equal("WWWi");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#72")]
    [Skip("needs:quantifiers - '^([^z]*(?:WWWi|w))?$' has a repeat and an optional group")]
    public void Optional_group_around_a_lowercase_alternative_captures_the_whole_subject()
    {
        Match m = FuzzyRegex.Match("WWWi", "^([^z]*(?:WWWi|w))?$");

        m.Groups.Skip(1).Select(g => g.Success ? g.Value : null).Should().Equal("WWWi");
    }

    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#73")]
    [Skip("needs:quantifiers - '^([^z]*?(?:WWWi|W))?$' has a lazy repeat and an optional group")]
    public void Optional_group_with_a_lazy_star_still_captures_the_whole_subject()
    {
        Match m = FuzzyRegex.Match("WWWi", "^([^z]*?(?:WWWi|W))?$");

        m.Groups.Skip(1).Select(g => g.Success ? g.Value : null).Should().Equal("WWWi");
    }

    // Hg issue 220: Misbehavior of group capture with OR operand.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#264")]
    [Skip(
        @"needs:quantifiers - '\w*(ea)\w*|\w*e(?!a)\w*' repeats '\w'; if the second arm is ever entered it needs a negative lookahead too"
    )]
    public void Alternation_picks_the_branch_that_captures_the_group()
    {
        Match m = FuzzyRegex.MatchAtStart("easier", @"\w*(ea)\w*|\w*e(?!a)\w*");

        m.Groups.Skip(1).Select(g => g.Success ? g.Value : null).Should().Equal("ea");
    }

    // Hg issue 87: Allow duplicate names of groups.
    [Test]
    [Property("Upstream", "RegexTests.test_hg_bugs#60")]
    public void Two_groups_sharing_one_name_report_both_spans_inner_first()
    {
        Match m = FuzzyRegex.MatchAtStart("ab", "(?<x>a(?<x>b))");

        // Upstream spans("x") is [(1, 2), (0, 2)] as (start, end). As (Index, Length) the inner
        // group captured "b", one character at index 1, and the outer captured "ab".
        m.Groups["x"].Captures.Select(c => (c.Index, c.Length)).Should().Equal((1, 1), (0, 2));
    }
}
