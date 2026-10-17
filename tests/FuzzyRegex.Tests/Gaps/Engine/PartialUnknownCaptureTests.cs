using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 M2, build chunk 8: unknown captures (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>,
/// section 4.2, the run-out row, and the witnesses Q2 and T3 of section 7). After a run-out inside a
/// lookaround body or condition test, a group inside the body may hold anything or nothing: a
/// condition on it takes both branches, a backreference to it runs out, backtracking takes it back,
/// and a partial reports it unset. Before chunk 8 a pattern that read a capture while it matched
/// answered the run-out partial at once, whatever followed.
/// </summary>
/// <remarks>
/// Each expected answer is the brute-force judge's, <c>tools/probes/d11-brute-judge.py</c> (upstream
/// <c>regex</c> 2026.9.10 without partial, over every continuation of one to four characters),
/// measured 2026-09-30. The grid is <c>tools/probes/d11-unknown-capture-grid.py</c>: 640 rows, 100
/// phantoms and 23 wrong spans before, 61 and 9 after, no miss either way, and no row the judge
/// agreed with changes.
/// </remarks>
public sealed class PartialUnknownCaptureTests
{
    /// <summary>
    /// Rows Q2 and T3: 'a' + 'a' completes each. The group is unknown, so the condition's yes-branch
    /// and the backreference hold for some continuations, and the partial reports group 1 unset.
    /// Without the unknown captures both are refused; without the report group 1 holds the unknown
    /// span.
    /// </summary>
    // Upstream answers P(0, 1) too.
    [Test]
    [Arguments(@"(?=a(.))(?(1)a|b)")]
    [Arguments(@"(?=a(.))\1")]
    public void A_capture_made_after_the_edge_is_unknown_and_reported_unset(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("a", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
        m.Groups[1].Success.Should().BeFalse();
        m.Groups[1].Captures.Should().BeEmpty();
    }

    /// <summary>
    /// Only a group the body can still reach after the edge is unknown. In each row the body runs out
    /// at <c>b</c>, and group 1 closed before the edge, so every completion ('aa' + 'b') gives it the
    /// span the held text gave it, and the partial reports that. Made unknown, the group was reported
    /// with <c>Success</c> false and its capture still listed.
    /// </summary>
    // Upstream answers the same spans and groups.
    [Test]
    [Arguments(@"(?=a(.)b)\1", "match", 1)]
    [Arguments(@"(?=a(.)b)\1", "search", 1)]
    [Arguments(@"(?=(a)(?:(.)|x)b)\1", "match", 0)]
    [Arguments(@"(?=(a)(.)b)\1\2", "match", 0)]
    public void A_group_the_body_closed_before_the_edge_is_reported(string pattern, string operation, int start)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "search", StringComparison.Ordinal)
            ? regex.Match("aa", partial: true)
            : regex.MatchAtStart("aa", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
        (m.Groups[1].Success, m.Groups[1].Index, m.Groups[1].Length).Should().Be((true, start, 1));
        m.Groups[1].Captures.Select(static c => (c.Index, c.Length)).Should().Equal((start, 1));
    }

    /// <summary>
    /// A group that held a capture before the edge and that the body can capture again after it is
    /// unknown, and reported unset with no captures listed: 'aa' + 'b' leaves group 1 at (1, 2) and
    /// 'aa' + 'ab' moves it to (2, 3). The run-out is the <c>x?</c> after the second <c>(a)</c>,
    /// which the repeat's end leads back to <c>(a)</c> from. A capture
    /// made after an unknown one is reported with the unknown one left out of the list: 'a' + 'a'
    /// captures (1, 2) inside the lookahead, then (0, 1).
    /// </summary>
    // Upstream lists the held captures of the first two rows and reports group 1 unset in the third.
    [Test]
    [Arguments(@"(?=(?:(a)x?)*b)\1", "aa", false, 0, 0)]
    [Arguments(@"(?=(?:(a)x?)*b)(?(1)a|b)", "aa", false, 0, 0)]
    [Arguments(@"(?=a(?P<n>.))(?P<n>a)(?P=n)", "a", true, 0, 1)]
    public void A_group_the_body_can_still_capture_is_reported_consistently(
        string pattern,
        string text,
        bool success,
        int index,
        int length
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, text.Length));
        (m.Groups[1].Success, m.Groups[1].Index, m.Groups[1].Length).Should().Be((success, index, length));
        m.Groups[1].Captures.Select(static c => (c.Index, c.Length)).Should().Equal(success ? [(index, length)] : []);
    }

    /// <summary>
    /// A backreference to an unknown capture presumes the rest, as a run-out does: 'ab' + 'a'
    /// matches, with group 1 the 'a' to come and the backreference the held 'a'. Compared as an
    /// empty span the reference leaves <c>b</c> to fail on the held 'a'.
    /// </summary>
    // Upstream answers P(0, 2) too.
    [Test]
    [Arguments("match")]
    [Arguments("search")]
    public void A_backreference_to_an_unknown_capture_runs_out(string operation)
    {
        var regex = new FuzzyRegex(@"(?=..(.))\1b");
        Match m = string.Equals(operation, "search", StringComparison.Ordinal)
            ? regex.Match("ab", partial: true)
            : regex.MatchAtStart("ab", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
    }

    /// <summary>
    /// A condition on an unknown capture takes both branches: 'a' + 'b' leaves the group
    /// unset, and the no-branch's <c>a</c> matches the held text; the yes-branch's <c>x</c> never
    /// can. The answer is S57d's as well until build chunk 9, so the Debug counts are the witness.
    /// </summary>
    // Upstream answers P(0, 1) too.
    [Test]
    [Category(EngineWork.Category)]
    public void A_condition_on_an_unknown_capture_takes_both_branches()
    {
        const string pattern = @"(?=a(?:(.)|)b)(?(1)x|a)";
        var regex = new FuzzyRegex(pattern);
        Match m = regex.MatchAtStart("a", partial: true);
        using MatchState state = MatchState.Create(
            regex.PatternObject,
            "a".AsMemory(),
            0,
            1,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );
        _ = Matcher.DoMatch(state, search: false);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(PartialEvent.SuccessTurnedPartial).Should().Be(1);
        state.PartialEventCount(PartialEvent.UnknownCaptureSet).Should().BePositive();
    }

    /// <summary>
    /// Backtracking past the run-out takes the unknown capture back. The body's first path runs out
    /// at <c>(.)</c>; its second, <c>(?(1)x|a)</c> then a character, must see group 1 unset again, so
    /// its success is certain and the negative lookahead fails for every continuation. Left unknown,
    /// the condition takes both branches, the success counts as reading the text to come, and the
    /// negative lookahead passes: a phantom P(0, 2).
    /// </summary>
    // Upstream answers None too.
    [Test]
    [Arguments(@"(?!(?:..(.)|(?(1)x|a).))a")]
    [Arguments(@"(?!(?:..(.)|(?(1)x|a)b))a")]
    public void Backtracking_past_a_run_out_takes_its_unknown_capture_back(string pattern)
    {
        new FuzzyRegex(pattern).MatchAtStart("ab", partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// Phantoms the run-out partial answered before chunk 8, because the pattern reads a capture: no
    /// continuation completes any row. The first two are D36 (found by D11's X2 design step): the
    /// test's <c>.*z</c> runs out, and <c>b</c> then fails on the held 'c'.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers the partial the port answered before.
    [Test]
    [Arguments(@"(x)?(?(?=.*z)a|a)b(?(1)y)", "ac")]
    [Arguments(@"(x)?(?(?=.*z)a|a)b\1", "ac")]
    [Arguments(@"(?=a(.)b)(?(1)x|y)", "a")]
    [Arguments(@"(?=a(.)b)a(?(1)b|c)", "ax")]
    [Arguments(@"(?!a(.)b)(?(1)a|b)", "a")]
    [Arguments(@"(?!a(.)b)a\1", "a")]
    [Arguments(@"(?(?=a(.)b)a|b)(?(1)x|y)", "ab")]
    [Arguments(@"(?=(?:a(.))+b)(?(1)x|y)", "a")]
    [Arguments(@"(?=a(?>(.)b))(?(1)x|y)", "a")]
    public void A_run_out_in_a_pattern_that_reads_captures_no_longer_ends_the_attempt(string pattern, string text) =>
        new FuzzyRegex(pattern).MatchAtStart(text, partial: true).Success.Should().BeFalse();

    /// <summary>
    /// D97: a group call anywhere in the pattern, outside the lookaround that runs out, leaves the
    /// run-out rule on for a pattern that reads captures, so each row answers as its written-out
    /// form does. In <c>(x)(?1)(?=\1)\Z</c> over 'xx' the lookahead needs an 'x' at 2 and <c>\Z</c>
    /// needs the text to end there, so no continuation completes it. Until the fix any call made
    /// the run-out end the attempt as a partial, P(0, 2). The rows cover <c>(?R)</c>, a named call,
    /// the call before the group and after the lookaround, a call to a group the lookaround does
    /// not read, a lookahead inside a lookbehind, a call in the body after the read, and
    /// <c>fullmatch</c> standing in for <c>\Z</c>.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers the partial at the run-out (D11).
    [Test]
    [Arguments(@"(x)(?1)(?=\1)\Z", "xx", "match")]
    [Arguments(@"y(?R)|(x)x(?=\1)\Z", "xx", "match")]
    [Arguments(@"(?P<a>x)(?&a)(?=(?P=a))\Z", "xx", "match")]
    [Arguments(@"(?1)(x)(?=\1)\Z", "xx", "match")]
    [Arguments(@"(x)x(?=\1)\Z(?1)?", "xx", "match")]
    [Arguments(@"(x)(y)(?2)(?=\1)\Z", "xyy", "match")]
    [Arguments(@"(x)(?1)(?<=x(?=\1))\Z", "xx", "match")]
    [Arguments(@"(x)(?=\1(?1))\Z", "x", "match")]
    [Arguments(@"(?P<b>x)(?&b)(?=(?P=b))", "xx", "fullmatch")]
    public void A_group_call_leaves_the_run_out_rule_on_for_a_backreference_in_a_lookaround(
        string pattern,
        string text,
        string operation
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "fullmatch", StringComparison.Ordinal)
            ? regex.FullMatch(text, partial: true)
            : regex.MatchAtStart(text, partial: true);

        m.Success.Should().BeFalse();
    }

    /// <summary>
    /// D97, the scanning forms: the attempt at 0 is refused as in the anchored row, and the attempt
    /// at 1 runs out at the call outside any body and answers P(1, 2), as the written-out
    /// <c>(x)x(?=\1)\Z</c> does. Reversed, the attempt at 2 is refused and the one at 1 answers
    /// P(0, 1), as <c>(?r)\A(?&lt;=\1)x(x)</c> does.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers P(0, 2) for each.
    [Test]
    [Arguments(@"(x)(?1)(?=\1)\Z", "search", 1, 1)]
    [Arguments(@"(x)(?1)(?=\1)\Z", "finditer", 1, 1)]
    [Arguments(@"(?r)\A(?<=\1)(?1)(x)", "search", 0, 0)]
    public void A_scan_with_a_group_call_refuses_the_attempt_the_lookaround_rules_out(
        string pattern,
        string operation,
        int index,
        int groupIndex
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match[] found = string.Equals(operation, "finditer", StringComparison.Ordinal)
            ? [.. regex.Matches("xx", partial: true)]
            : [regex.Match("xx", partial: true)];

        found
            .Select(static m => (m.PartialMatch, m.Index, m.Length, m.Groups[1].Index))
            .Should()
            .Equal((true, index, 1, groupIndex));
    }

    /// <summary>
    /// D97's controls, which the fix leaves as they were: a literal or a capture in the lookahead
    /// in place of the backreference, a longer text, and a lookahead without <c>\Z</c> (refused or
    /// completed by 'xx' + 'x'); a backreference outside any lookaround, which 'xx' + 'x' completes.
    /// A run-out inside a call the body opened still answers at once (the SHORTCUT on
    /// <c>Matcher.RunOutInALookaroundBody</c>), so <c>(x)(?=(?1)\1)\Z</c> is not among them.
    /// </summary>
    // Upstream answers the same.
    [Test]
    [Arguments(@"(x)(?1)(?=x)\Z", "xx", false)]
    [Arguments(@"(x)(?1)(?=(x))\Z", "xx", false)]
    [Arguments(@"(x)(?1)(?=\1)\Z", "xxx", false)]
    [Arguments(@"(x)x(?=\1)\Z", "xx", false)]
    [Arguments(@"(x)(?1)(?=\1)", "xx", true)]
    [Arguments(@"(x)(?1)\1", "xx", true)]
    public void A_group_call_pattern_s_other_partials_are_unchanged(string pattern, string text, bool partial)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.Success, m.PartialMatch).Should().Be((partial, partial));
        if (partial)
        {
            (m.Index, m.Length).Should().Be((0, text.Length));
        }
    }

    /// <summary>
    /// D98: a run-out inside a group call that the lookaround's body opened is taken as any other
    /// run-out in the body, so each row answers as its written-out form does. In
    /// <c>(x)(?=(?1)\1)\Z</c> over 'x' the lookahead needs 'xx' at 1 and <c>\Z</c> needs the text to
    /// end there, so no continuation completes it; until the fix the run-out inside <c>(?1)</c>
    /// answered P(0, 1) at once. The rows cover a numbered, a named and a nested call,
    /// <c>(?R)</c>, a lookahead inside a lookbehind and <c>fullmatch</c> standing in for <c>\Z</c>.
    /// Over 'xx' the run-out is at <c>\1</c>, after the call has returned (D97 fixed that one).
    /// Each None is the brute-force judge's (<c>.scratch/s1016/d98/judge-out.tsv</c>).
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers the partial at the run-out (D11).
    [Test]
    [Arguments(@"(x)(?=(?1)\1)\Z", "x", "match")]
    [Arguments(@"(x)(?=(?1)\1)\Z", "xx", "match")]
    [Arguments(@"(x)(?=(?1)y)\Z", "x", "match")]
    [Arguments(@"(?P<a>x)(?=(?&a)(?P=a))\Z", "x", "match")]
    [Arguments(@"x(?=(?R)?y)\Z", "x", "match")]
    [Arguments(@"(x)(?=(?&b)\1)\Z(?(DEFINE)(?<b>y(?1)))", "x", "match")]
    [Arguments(@"(x)(?=(?&b)\1)\Z(?(DEFINE)(?<b>y(?1)))", "xy", "match")]
    [Arguments(@"(x)(?<=x(?=(?1)\1))\Z", "x", "match")]
    [Arguments(@"(x)(?=(?1)\1)", "x", "fullmatch")]
    public void A_run_out_inside_a_call_the_lookaround_opened_is_taken_as_a_run_out_in_the_body(
        string pattern,
        string text,
        string operation
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "fullmatch", StringComparison.Ordinal)
            ? regex.FullMatch(text, partial: true)
            : regex.MatchAtStart(text, partial: true);

        (m.Success, m.PartialMatch).Should().Be((false, false));
    }

    /// <summary>
    /// D98, the scanning forms: every attempt that starts inside the text is refused as in the
    /// anchored rows, and the attempt at the text's end runs out outside any body, as the
    /// written-out forms (<c>(x)(?=x\1)\Z</c>, <c>x(?=x?y)\Z</c>) answer.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers the partial at the first attempt's run-out.
    [Test]
    [Arguments(@"(x)(?=(?1)\1)\Z", "x", "search", 1)]
    [Arguments(@"(x)(?=(?1)\1)\Z", "xx", "search", 2)]
    [Arguments(@"(x)(?=(?1)\1)\Z", "x", "finditer", 1)]
    [Arguments(@"(x)(?=(?1)\1)\Z", "xx", "finditer", 2)]
    [Arguments(@"x(?=(?R)?y)\Z", "x", "search", 1)]
    [Arguments(@"x(?=(?R)?y)\Z", "x", "finditer", 1)]
    public void A_scan_refuses_the_attempts_a_run_out_inside_a_called_group_rules_out(
        string pattern,
        string text,
        string operation,
        int index
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match[] found = string.Equals(operation, "finditer", StringComparison.Ordinal)
            ? [.. regex.Matches(text, partial: true)]
            : [regex.Match(text, partial: true)];

        found.Select(static m => (m.PartialMatch, m.Index, m.Length)).Should().Equal((true, index, 0));
    }

    /// <summary>
    /// D98's controls, which the fix leaves as they were: a run-out inside the called group where a
    /// continuation completes the match ('xx' after <c>(x)(?=(?1)\1)</c>; 'b' for the condition's
    /// no-branch) or where the run-out after the lookaround decides it (<c>z</c>), so each stays a
    /// partial; and negative lookaheads whose body fails on the held text, which match in full.
    /// </summary>
    // Upstream answers the same.
    [Test]
    [Arguments(@"(x)(?=(?1)\1)", "x", true)]
    [Arguments(@"(x)(?=(?1)y)z", "x", true)]
    [Arguments(@"(x)(?(?=(?1)\1)a|b)", "x", true)]
    [Arguments(@"(x)(?!(?1)\1)\Z", "x", false)]
    [Arguments(@"(x)(?!(?1)y)", "x", false)]
    public void A_run_out_inside_a_called_group_that_a_continuation_completes_stays_a_partial(
        string pattern,
        string text,
        bool partial
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, partial, 0, text.Length));
    }

    /// <summary>
    /// D98's limit, pinned as it stands: where the body's outcome feeds a decision (a negative
    /// lookahead, a condition's test, a lookahead inside an atomic group) a run-out inside a call
    /// the body opened still answers the partial at once, a phantom: the written-out forms and the
    /// judge answer None. D101 in KNOWN-DEFECTS; its red test is in <c>OpenDefectTests</c>.
    /// </summary>
    // Upstream answers the same partial.
    [Test]
    [Arguments(@"(x)(?!y?(?1))\Z", "xy")]
    [Arguments(@"(x)(?(?=y?(?1))a|b)", "xy")]
    [Arguments(@"(x)(?>(?=(?1)))\Z", "x")]
    public void A_run_out_inside_a_call_a_deciding_body_opened_still_answers_at_once(string pattern, string text)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, text.Length));
    }
}
