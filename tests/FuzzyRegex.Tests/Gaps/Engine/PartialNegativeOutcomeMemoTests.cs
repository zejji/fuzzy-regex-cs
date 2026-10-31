using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D101: in a partial call's second pass, a negative lookaround whose body holds a group call is
/// decided once per position and attempt, where nothing but the position can change its outcome
/// (<c>Matcher</c>'s negative outcome memo). Since B1 such a body is searched to its end before the
/// lookaround passes undetermined, and a recursion below it reaches the same lookaround at the same
/// position again and again inside one attempt.
/// </summary>
/// <remarks>
/// Step counts are Debug <c>WorkCounter.Steps</c> of a partial search over 'x' * 80, measured
/// 2026-10-24 on fix/d101 (B1 and inlining, no memo) and with the memo's prototype. Main answered
/// both rows at the first run-out (the SHORTCUT B1 removed): 403 and 323 steps.
/// </remarks>
public sealed class PartialNegativeOutcomeMemoTests
{
    /// <summary>
    /// The double negative: 311,429 steps without the memo, 53,669 with its prototype. The single
    /// negative: 539,448 and 53,033. Both answer P(0, 80): 'x' * 80 + 'zqy' completes a match.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?!(?!(?R)?y))|z)+q")]
    [Arguments(@"(?:x(?!(?R)?y)|z)+q")]
    public void A_negative_lookaround_holding_a_recursion_is_decided_once_per_position(string pattern)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = new('x', 80);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(text, partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 80));
            },
            60_000,
            "a negative lookaround's outcome at a position is reused within the attempt"
        );
    }

    /// <summary>
    /// The capture-read exclusion's witness. The lookahead at 1 is reached twice: after
    /// <c>[ab]</c>, with group 1 unset, its body fails (a backreference to an unset group fails)
    /// and it passes; after <c>(a)</c> its body matches the second 'a' and it fails. Kept per
    /// position alone, the first outcome would let the second branch through, and the fullmatch
    /// would answer (0, 2) (measured with the exclusion removed, 2026-10-24: (0, 2) in Release, assert
    /// D11 A1 in Debug). No branch completes, so the answer is None.
    /// </summary>
    [Test]
    public void A_negative_lookaround_that_reads_a_capture_is_not_kept_per_position()
    {
        var regex = new FuzzyRegex(@"(?:[ab]|(a))(?!(?&d))(?(1)|(?!))a(?(DEFINE)(?<d>\1|w(?&d)))");

        regex.FullMatch("aa", partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// The calls' memo (<c>PatternObject.MemoisesCallOutcomes</c>): with the lookarounds decided once
    /// per position, each recursion level still ran its group to the end of the text. 53,669 steps
    /// for the double negative and 53,033 for the single with the lookaround memo alone, 28,157 and
    /// 27,521 with the calls' prototype. The same shape without calls, `(?:x(?!(?:xz?)*y)|z)+q`,
    /// costs 13,534 on main.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?!(?!(?R)?y))|z)+q")]
    [Arguments(@"(?:x(?!(?R)?y)|z)+q")]
    public void A_call_below_a_negative_lookaround_is_run_once_per_position(string pattern)
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = new('x', 80);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(text, partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 80));
            },
            30_000,
            "a call's outcome at a position is reused within the pass"
        );
    }

    /// <summary>
    /// The calls' bookkeeping at a run-out costs the same at any recursion depth: a run-out marks
    /// the innermost open call only, and a call hands what it saw to its caller when it closes.
    /// Writing every open call's track at each run-out made row 122 spend 12% of its Release time
    /// there (WPR, 2026-10-26), with 'x' * n opening n calls.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_run_out_marks_the_open_calls_in_constant_work()
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(@"(?:x(?!(?!(?R)?y))|z)+q", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string text = new('x', 400);
        long before = WorkCounter.CallTracksMarked;

        Match m = regex.Match(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 400));
        (WorkCounter.CallTracksMarked - before)
            .Should()
            .BeLessThan(4 * 400, "one track per run-out, not one per open call");
    }

    /// <summary>
    /// A call re-opened after it returned is marked touched without a run-out, so that it is never
    /// kept; it hands its caller only the run-outs it saw. Handing on its mark too kept the caller at
    /// 1 out of the failure memo, and the anchored partial match took 816 Debug steps instead of
    /// 803 (found by the generated set, row 193,980, 2026-10-26).
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_re_opened_call_hands_its_caller_only_the_run_outs_it_saw()
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(
            @"(?:x(?!(?:(?:x|(?R)?)*?w?){1,2}y)|z)*q",
            FuzzyRegexOptions.None,
            EngineWork.HangGuard
        );

        EngineWork.ShouldTakeAtMostSteps(
            () => regex.MatchAtStart("xxwq!", partial: true).Success.Should().BeFalse(),
            803,
            "a failure inside the caller is kept by the calls' memo"
        );
    }

    /// <summary>
    /// The calls' memo keeps only a call that never returned. Here the recursion inside the
    /// lookahead returns ('x' then the lookahead and the optional recursion) and then fails on the
    /// lookahead's 'q', so backtracking passes its entry after a return; kept as if it had only run
    /// out, the later call at the same position would run out instead of returning, and the
    /// fullmatch would answer P(0, 3) (measured with the condition removed, 2026-10-25). 'y' is not
    /// an 'x', so no text completes 'xxy': None.
    /// </summary>
    [Test]
    public void A_call_that_returned_is_not_kept()
    {
        new FuzzyRegex(@"x(?!(?R)?q)(?R)?").FullMatch("xxy", partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// Review finding (d101-frame-cut.md, finding 1): the frame cut's required literal can be a lone
    /// surrogate, which the pattern holds as one unit. regex 2026.9.29 answers all four P(0, 2), but
    /// no text completes a match (each branch matches exactly 'a' or the group's text; regex's
    /// non-partial fullmatch over every suffix of up to 3 of 'ayzb\ud800' finds none), so None is
    /// the answer and upstream's P is the phantom of ledger entry 67. The inliner turns these
    /// <c>(?1)</c> into the group's body, so the cut is never reached: measured 2026-10-31 on
    /// c78a073c and 184c256b, all four answer None and none throws.
    /// </summary>
    [Test]
    [Arguments(@"a(?!y?(?1))|(\ud800)", "ay")]
    [Arguments(@"a(?!(?1))|(\ud800b)", "a\ud800")]
    [Arguments(@"a(?!y?(?1))|(z)", "ay")]
    [Arguments(@"a(?!(?1))|(zb)", "az")]
    public void A_lone_surrogate_required_literal_does_not_stop_the_cut(string pattern, string text)
    {
        new FuzzyRegex(pattern).FullMatch(text, partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// Finding 1 with a recursive group, which the inliner keeps as a call. regex 2026.9.29 answers
    /// P(0, 2) (ledger entry 67 again); no suffix of up to 3 of 'ayzb\ud800' completes either, so
    /// the answer is None, and the call must not throw on the lone surrogate. Measured 2026-10-31 on
    /// c78a073c: the first row throws ArgumentOutOfRangeException from <c>char.ConvertFromUtf32</c>
    /// in <c>Matcher.LastOfTheGroupsLiteral</c> (red, the cut is not fixed here); the second answers None.
    /// </summary>
    [Test]
    [Arguments(@"a(?!y?(?1))|(\ud800(?1)?)", "ay")]
    [Arguments(@"a(?!(?1))|(\ud800b(?1)?)", "a\ud800")]
    public void A_lone_surrogate_literal_of_a_recursive_group_does_not_throw(string pattern, string text)
    {
        new FuzzyRegex(pattern).FullMatch(text, partial: true).Success.Should().BeFalse();
    }
}
