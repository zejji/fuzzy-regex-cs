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

    // ---------------------------------------------------------------------------------------
    // S44, the Phase 6 upstream sync. Issue 611, commit 1c90270, released 2026.8.30:
    // `LookAroundConditional.is_empty()` read `a and b or c`, which Python groups as
    // `(a and b) or c`, so a lookaround conditional with an EMPTY NO-BRANCH called itself empty
    // whatever its test and yes-branch were. Upstream titles it "Heap out-of-bounds write at
    // compile time"; in Python it surfaces as a dropped quantifier, a dropped conditional branch
    // and, on the fourth case below, a MemoryError.
    //
    // Both sides of every assertion were measured on 2026-09-13 with
    // `tools/probes/upstream-lookaround-conditional-is-empty.py`, run against 2026.7.19 (which is
    // the old pin 2026.8.12 byte for byte under `src/` and `regex/`) and against 2026.9.10. The
    // probe carries the four `_regex_core.py` call sites each case reaches.
    // ---------------------------------------------------------------------------------------

    [Test]
    public void A_quantified_lookaround_conditional_with_an_empty_no_branch_keeps_its_quantifier()
    {
        // The conditional is NOT zero-width - its yes-branch matches 'b' - so `parse_quantifier`
        // (upstream/regex/_regex_core.py:585) must keep the `*`. With the quantifier kept, zero
        // repetitions match at 0.
        //
        // 2026.9.10: regex.search(r'(?(?=a)b|)*', 'ab').span() == (0, 0)
        // 2026.7.19: (1, 1) - the quantifier was dropped, so position 0 could only try the
        //            yes-branch, which needs a 'b' and finds an 'a'.
        Match m = FuzzyRegex.Match("ab", "(?(?=a)b|)*");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 0));
    }

    [Test]
    public void A_scan_of_a_quantified_lookaround_conditional_matches_at_every_position()
    {
        // The same defect across a whole scan.
        //
        // 2026.9.10: [m.span() for m in regex.finditer(r'(?(?=a)b|)*', 'aab')]
        //            == [(0, 0), (1, 1), (2, 2), (3, 3)]
        // 2026.7.19: [(2, 2), (3, 3)] - the two positions where the lookahead fails.
        new FuzzyRegex("(?(?=a)b|)*")
            .Matches("aab")
            .Select(static m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((0, 0), (1, 1), (2, 2), (3, 3));
    }

    [Test]
    public void A_group_conditional_holding_a_lookaround_conditional_is_not_dropped()
    {
        // `parse_conditional` (:1045) returns an empty Sequence when BOTH branches are empty, so
        // the wrong `is_empty()` deleted the whole conditional. Kept, the yes-branch runs: group 1
        // matched, so at position 1 the inner test `(?=a)` succeeds and demands a 'b' that is not
        // there.
        //
        // 2026.9.10: regex.search(r'(x)(?(1)(?(?=a)b|)|)', 'xa') is None
        // 2026.7.19: (0, 1) - the conditional was gone, leaving just `(x)`.
        FuzzyRegex.Match("xa", "(x)(?(1)(?(?=a)b|)|)").Success.Should().BeFalse();
    }

    [Test]
    public void A_group_inside_a_dropped_conditional_branch_does_not_desync_the_group_count()
    {
        // The same shape with a CAPTURE inside the branch that was being dropped, which is the
        // group-count desync upstream reports as a heap out-of-bounds WRITE. It reaches Python as
        // an allocation failure rather than a wrong answer.
        //
        // 2026.9.10: [m.span() for m in regex.finditer(r'(x)(?(1)(?(?=a)(b)|)|)', 'xa')] == []
        // 2026.7.19: MemoryError.
        var pattern = new FuzzyRegex("(x)(?(1)(?(?=a)(b)|)|)");

        pattern.Matches("xa").Should().BeEmpty();
        pattern.GroupCount.Should().Be(2);
    }

    [Test]
    public void A_positive_lookaround_holding_a_lookaround_conditional_does_not_collapse()
    {
        // `LookAround.optimise` (:3164) replaces a POSITIVE lookaround with its subpattern when the
        // subpattern is empty, so the wrong `is_empty()` turned a zero-width test into a consuming
        // one: `(?=X)a` became `Xa`.
        //
        // 2026.9.10: regex.search(r'(?=(?(?=)b|))a', 'abab') is None
        // 2026.7.19: (1, 3) - 'ba', the collapsed pattern's match.
        FuzzyRegex.Match("abab", "(?=(?(?=)b|))a").Success.Should().BeFalse();
    }

    [Test]
    public void A_bounded_quantifier_on_a_lookaround_conditional_is_kept()
    {
        // :585 again with a bounded repeat, where dropping the quantifier changes the minimum count
        // rather than just allowing zero.
        //
        // 2026.9.10: regex.search(r'(?(?=)b|){2,3}', 'b') is None - one 'b' cannot satisfy {2,3}.
        // 2026.7.19: (0, 1) - the quantifier was dropped, so one 'b' was enough.
        FuzzyRegex.Match("b", "(?(?=)b|){2,3}").Success.Should().BeFalse();
    }

    [Test]
    public void An_atomic_group_holding_a_lookaround_conditional_keeps_its_atomicity()
    {
        // `Atomic.optimise` (:2085) returns the subpattern in place of the atomic group when the
        // subpattern is empty, so the wrong `is_empty()` threw the ATOMICITY away: `(?>X)` became
        // `X`, and a repeat inside it that upstream must not backtrack could backtrack again.
        //
        // The fourth call site, and the one the first six tests here did not reach - found by S44's
        // blind review, not by the port. There is no quantifier on the atomic group, so `:585` is
        // not involved and this is `:2085` alone.
        //
        // 2026.9.10: regex.search(r'(?>(?(?=b)b*|))b', 'bbb') is None - 'b*' takes all three 'b's
        //            atomically and the trailing 'b' has nothing left.
        // Pre-1c90270: (0, 3) - the collapsed 'b*' gives one back.
        //
        // Measured 2026-09-13, tools/probes/upstream-lookaround-conditional-is-empty.py. That
        // "before" figure is the one measurement in this class taken by monkeypatching the old
        // expression onto 2026.9.10 rather than by running 2026.7.19, which is not installed on
        // this machine. The probe's docstring says so and shows all three rows.
        FuzzyRegex.Match("bbb", "(?>(?(?=b)b*|))b").Success.Should().BeFalse();
        FuzzyRegex.Match("bb", "(?>(?(?=b)b*|))b").Success.Should().BeFalse();
        FuzzyRegex.Match("aaa", "(?>(?(?=a)a*|))a").Success.Should().BeFalse();
    }
}
