using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Gap tests for S21's <c>REF_GROUP</c> and <c>GROUP_EXISTS</c>: the semantics the slice named as
/// "pin against the oracle, not assume", plus the UTF-16 stepping a backreference does, none of
/// which the ported suite reaches at this stage of the port.
/// </summary>
/// <remarks>
/// Every expected value below was probed against <c>regex</c> 2026.7.19 on 2026-08-31 and is quoted
/// beside the assertion as Python spells it. Python's spans are codepoint indices; ours are UTF-16
/// code units, so the two astral cases convert and say so.
/// </remarks>
public sealed class BackrefAndConditionalTests
{
    [Test]
    public void A_backreference_to_a_group_that_did_not_match_fails_rather_than_matching_empty()
    {
        // Upstream: regex.match(r'(?:(a)|b)\1', 'b') is None, but regex.match(r'(?:(a)|b)\1?', 'b')
        // spans (0, 1) with group 1 None. 'current < 0' is a failure, not an empty match
        // (upstream/src/_regex.c:14017), which is the difference between the two.
        FuzzyRegex.MatchAtStart("b", @"(?:(a)|b)\1").Success.Should().BeFalse();

        Match optional = FuzzyRegex.MatchAtStart("b", @"(?:(a)|b)\1?");

        optional.Value.Should().Be("b");
        optional.Groups[1].Success.Should().BeFalse();
    }

    [Test]
    public void A_backreference_to_a_group_that_matched_empty_matches_empty()
    {
        // Upstream: regex.match(r'(|x)\1y', 'y').span() == (0, 1) and group(1) == ''. The group has
        // a current capture, so the comparison loop runs zero times and the reference succeeds -
        // the other half of the distinction above.
        Match m = FuzzyRegex.MatchAtStart("y", @"(|x)\1y");

        m.Value.Should().Be("y");
        m.Groups[1].Success.Should().BeTrue();
        m.Groups[1].Value.Should().BeEmpty();
    }

    [Test]
    public void A_conditional_reads_the_group_as_it_stands_at_that_point_and_not_its_final_state()
    {
        // Upstream: regex.match(r'(?(1)a|b)(b)', 'bb').span() == (0, 2) with group(1) == 'b' at
        // (1, 2). Group 1 does match, but not until after the conditional has been passed, so
        // GROUP_EXISTS takes the false branch. An implementation that consulted the group's final
        // state - or the pattern's group table - would take the true branch and fail here.
        Match m = FuzzyRegex.MatchAtStart("bb", "(?(1)a|b)(b)");

        m.Value.Should().Be("bb");
        m.Groups[1].Value.Should().Be("b");
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((1, 1));
    }

    [Test]
    public void A_conditional_sees_a_group_whose_capture_was_backtracked_away_as_unmatched()
    {
        // Upstream: regex.match(r'(?:(a)x|a)(?(1)y|z)', 'az').span() == (0, 2) with group(1) None,
        // and the same pattern on 'axy' spans (0, 3) with group(1) == 'a'. On 'az' the first
        // alternative captures group 1 and then fails on 'x', so the END_GROUP backtrack case has
        // to restore 'current' to -1 before the conditional is reached.
        Match undone = FuzzyRegex.MatchAtStart("az", "(?:(a)x|a)(?(1)y|z)");

        undone.Value.Should().Be("az");
        undone.Groups[1].Success.Should().BeFalse();

        Match kept = FuzzyRegex.MatchAtStart("axy", "(?:(a)x|a)(?(1)y|z)");

        kept.Value.Should().Be("axy");
        kept.Groups[1].Value.Should().Be("a");
    }

    [Test]
    public void A_define_group_body_is_stepped_over_and_consumes_nothing()
    {
        // Upstream: regex.match(r'(?(DEFINE)(?<x>zzz))a', 'a').span() == (0, 1) with group('x')
        // None, and regex.match(r'(?(DEFINE)(?<x>zzz))zzz', 'zzz').span() == (0, 3), also with
        // group('x') None. A DEFINE compiles to a GROUP_EXISTS on group 0, whose condition never
        // holds, so the matcher always takes the second exit and the body never runs in place -
        // which is why the second case matches 'zzz' with the pattern's own 'zzz' and not with the
        // body's.
        Match noBody = FuzzyRegex.MatchAtStart("a", @"(?(DEFINE)(?<x>zzz))a");

        noBody.Value.Should().Be("a");
        noBody.Groups["x"].Success.Should().BeFalse();

        Match sameText = FuzzyRegex.MatchAtStart("zzz", @"(?(DEFINE)(?<x>zzz))zzz");

        sameText.Value.Should().Be("zzz");
        sameText.Groups["x"].Success.Should().BeFalse();
    }

    [Test]
    public void A_backreference_inside_a_repeat_reads_the_span_the_current_iteration_captured()
    {
        // Upstream: regex.match(r'(?:(\w)\1)+', 'aabb').span() == (0, 4) with group(1) == 'b' at
        // (2, 3). The second iteration's reference has to compare against 'b', the span that
        // iteration just captured, not against the 'a' the first one did.
        Match m = FuzzyRegex.MatchAtStart("aabb", @"(?:(\w)\1)+");

        m.Value.Should().Be("aabb");
        m.Groups[1].Value.Should().Be("b");
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((2, 1));
    }

    [Test]
    public void A_conditional_inside_a_repeat_reads_the_group_the_previous_iteration_captured()
    {
        // Upstream: regex.match(r'(?:(a)|b(?(1)c|d))+', 'abc').span() == (0, 3) with group(1) == 'a'
        // at (0, 1). The first iteration captures group 1; the second reaches the conditional with
        // that capture still standing and so takes 'c'.
        Match m = FuzzyRegex.MatchAtStart("abc", "(?:(a)|b(?(1)c|d))+");

        m.Value.Should().Be("abc");
        m.Groups[1].Value.Should().Be("a");
    }

    [Test]
    public void A_backreference_to_an_astral_capture_compares_and_reports_whole_codepoints()
    {
        // Upstream: regex.match(r'(.)\1', '\U0001F600\U0001F600').span() == (0, 2) with group 1 at
        // (0, 1) - codepoint indices, so (0, 4) and (0, 2) in UTF-16 code units. The comparison
        // walks both sides one codepoint at a time, so 'stringPos' lands exactly on the span's end.
        Match m = FuzzyRegex.MatchAtStart("\U0001F600\U0001F600", @"(.)\1");

        m.Value.Should().Be("\U0001F600\U0001F600");
        (m.Index, m.Length).Should().Be((0, 4));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 2));
    }

    [Test]
    public void A_backreference_spanning_an_astral_character_and_a_bmp_one_matches_the_whole_span()
    {
        // Upstream: regex.match(r'(\U0001F600.)\1', '\U0001F600a\U0001F600a').span() == (0, 4) with
        // group 1 at (0, 2) - (0, 6) and (0, 3) in UTF-16 code units. A reference whose span mixes
        // widths is where a code-unit step and a codepoint step would part company.
        Match m = FuzzyRegex.MatchAtStart("\U0001F600a\U0001F600a", "(\U0001F600.)\\1");

        (m.Index, m.Length).Should().Be((0, 6));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 3));
    }

    [Test]
    public void A_backreference_to_an_astral_capture_rejects_a_different_astral_character()
    {
        // Upstream: regex.match(r'(.)\1', '\U0001F600\U0001F601') is None. The two differ only in
        // their low surrogate, so an implementation comparing code units without decoding would
        // still get this right; one comparing only the high surrogate would not.
        FuzzyRegex.MatchAtStart("\U0001F600\U0001F601", @"(.)\1").Success.Should().BeFalse();
    }
}
