using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 M2, build chunk 5: the cut rule for <c>(*PRUNE)</c> and <c>(*SKIP)</c>
/// (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, section 4.2, and the witnesses of section
/// 7). In a partial call's second pass a verb whose path read the text to come does nothing, and its
/// scope's frame gets a verb taint, which blocks the scope's own decision.
/// </summary>
/// <remarks>
/// Each expected answer is the brute-force judge's, <c>tools/probes/d11-brute-judge.py</c> (upstream
/// <c>regex</c> 2026.9.10 without partial, over every continuation of one to four characters),
/// measured 2026-09-29. S57d's escalation turns a failed second pass into a partial whenever a word
/// test stood at the edge, until build chunk 9 deletes it, and it clears the groups. So the
/// witnesses read the text to come through <c>$</c>, which S57d never notes, or show that the answer
/// came from a path: a group the path set, or, in a Debug build, the count of second-pass events. A
/// count is checked after the test's answers, which a Release build checks too, and a Release build
/// then skips the test with <see cref="EngineWork.DebugOnly"/> as the reason.
/// </remarks>
public sealed class PartialVerbRuleTests
{
    /// <summary>
    /// Rows V1-V3 with <c>$</c> for the read: 'a' + ' ' fails the <c>$</c>, so the verb is never
    /// reached and <c>a.</c> matches. With the verb acting, the second pass finds nothing (V1, V2) or,
    /// for the search, the skip moves the next start past 0 (V3). In the last two rows the verb is
    /// inside an unfinished atomic group, so it acts when backtracking reaches it (ledger entry 47),
    /// and the rule is asked then.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to V1 and V2 and P(1, 1) to V3,
    // and confines the verb of the last two rows to the group.
    [Test]
    [Arguments("match", @"(?:a$(*PRUNE)(?<=b)|a.)")]
    [Arguments("match", @"(?:a$(*SKIP)(?<=b)|a.)")]
    [Arguments("search", @"a$(*SKIP)(?<=b)|a.")]
    [Arguments("match", @"(?:(?>a$(*PRUNE)(?<=b))|a.)")]
    [Arguments("search", @"(?>a$(*SKIP)(?<=b))|a.")]
    public void A_verb_after_a_read_of_the_text_to_come_does_nothing(string operation, string pattern)
    {
        Match m = Run(operation, pattern, "a");

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    /// <summary>
    /// The design's rows V1-V3, whose read is a <c>\b</c>, with a group on the path that completes:
    /// S57d answers V1 and V2 with the span and no group, and V3 with P(1, 1).
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to V1 and V2 and P(1, 1) to V3.
    [Test]
    [Arguments("match", @"(?:a\b(*PRUNE)(?<=b)|(a).)")]
    [Arguments("match", @"(?:a\b(*SKIP)(?<=b)|(a).)")]
    [Arguments("search", @"a\b(*SKIP)(?<=b)|(a).")]
    public void The_design_rows_of_a_skipped_verb_are_answered_by_the_path(string operation, string pattern)
    {
        Match m = Run(operation, pattern, "a");

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
        (m.Groups[1].Success, m.Groups[1].Index).Should().Be((true, 0));
    }

    /// <summary>
    /// Row V4 and its <c>$</c> twin: the verb runs before the read, so it prunes for every
    /// continuation and nothing completes. S57d still answers V4 itself with a phantom partial until
    /// build chunk 9, so there the Debug counts are the witness, and the twin is the one a Release
    /// build checks.
    /// </summary>
    // Upstream answers None to the twin too.
    [Test]
    [Category(EngineWork.Category)]
    public void A_verb_before_the_read_prunes()
    {
        using MatchState state = RunPartial(@"(?:a(*PRUNE)\b(?<=b)|a.)", "a");

        new FuzzyRegex(@"(?:a(*PRUNE)$(?<=b)|a.)").MatchAtStart("a", partial: true).Success.Should().BeFalse();
        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(PartialEvent.VerbSkipped).Should().Be(0);
        state.PartialEventCount(PartialEvent.SuccessTurnedPartial).Should().Be(0);
    }

    /// <summary>
    /// Row N3: <c>\Z</c> at the edge is false for every continuation, so it leaves no mark, and the
    /// prune in the second branch prunes. Blocked, it would let the third branch match: a phantom.
    /// </summary>
    // Upstream answers None too.
    [Test]
    [Category(EngineWork.Category)]
    public void A_verb_after_a_decided_read_prunes()
    {
        using MatchState state = RunPartial(@"(?:a\Z(?<=b)|a(*PRUNE)(?<=b)|a)", "a");

        new FuzzyRegex(@"(?:a\Z(?<=b)|a(*PRUNE)(?<=b)|a)").MatchAtStart("a", partial: true).Success.Should().BeFalse();
        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(PartialEvent.VerbSkipped).Should().Be(0);
    }

    /// <summary>
    /// Rows Q1 and T2: the prune in the negative lookahead's body is skipped, and its taint blocks the
    /// lookahead's failure when the body's other branch, <c>a</c>, succeeds: in the continuations
    /// where the prune acted ('a' + '1' fails the <c>\b</c> of Q1's atomic group, and makes T2's
    /// <c>\B</c> true), the body fails and the assertion holds. Without the taint the second pass
    /// finds nothing, which S57d hides, so the Debug counts are the witness here, and the
    /// <c>$</c> twin below is the one a Release build checks.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to both.
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?!(?>a\b|)(?<!a)a(*PRUNE)(?<=b)|a)")]
    [Arguments(@"(?!a\B(*PRUNE)(?<=b)|a)")]
    public void A_skipped_verb_blocks_its_own_scope(string pattern)
    {
        using MatchState state = RunPartial(pattern, "a");
        Match m = new FuzzyRegex(pattern).MatchAtStart("a", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(PartialEvent.SuccessTurnedPartial).Should().Be(1);
        state.PartialEventCount(PartialEvent.NegativeUndetermined).Should().Be(1);
    }

    /// <summary>
    /// Q1 with <c>$</c> for the read, which S57d does not note: 'a' + ' ' makes the atomic group
    /// commit to <c>a$</c>, the body fails at <c>(?&lt;!a)</c>, and the assertion holds.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None.
    [Test]
    public void A_skipped_verb_blocks_its_own_scope_after_a_line_end()
    {
        Match m = new FuzzyRegex(@"(?!(?>a$|)(?<!a)a(*PRUNE)(?<=b)|a)").MatchAtStart("a", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    /// <summary>
    /// Row T1, the timeline of design section 4.2, and its <c>$</c> twin: the atomic group's cut is
    /// blocked, its empty path reaches the prune, and the prune must not discard the outer
    /// <c>|a\B</c>, which the continuations where the group committed need.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to both.
    [Test]
    [Category(EngineWork.Category)]
    public void A_verb_reached_back_through_a_skipped_cut_does_nothing()
    {
        using MatchState state = RunPartial(@"(?:(?>a\B|)(?<!a)a(*PRUNE)(?<=b)|a\B)", "a");
        Match twin = new FuzzyRegex(@"(?:(?>a$|)(?<!a)a(*PRUNE)(?<=b)|a\n)").MatchAtStart("a", partial: true);

        (twin.PartialMatch, twin.Index, twin.Length).Should().Be((true, 0, 1));
        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(PartialEvent.SuccessTurnedPartial).Should().Be(1);
    }

    /// <summary>
    /// A verb taint blocks the scope's later verbs too. The prune of the first branch is skipped (its
    /// path read the <c>$</c>), and the skip of the second branch, whose path read nothing, must not
    /// move the next start to 2: for 'abb' followed by a newline the prune acts, the next start is 1,
    /// and <c>bb\n</c> matches there. The control has no prune, and the skip moves the start.
    /// </summary>
    // Upstream answers the same, P(1, 3) and P(2, 3).
    [Test]
    [Arguments(@"abb$(*PRUNE)(*FAIL)|ab(*SKIP)(*FAIL)|bb\n", 1)]
    [Arguments(@"ab(*SKIP)(*FAIL)|bb\n", 2)]
    public void A_skipped_verb_blocks_the_later_verbs_of_its_scope(string pattern, int start)
    {
        Match m = new FuzzyRegex(pattern).Match("abb", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, start, 3 - start));
    }

    /// <summary>
    /// A verb whose scope is a condition's test and whose path read the text to come does nothing,
    /// and the test, blocked, takes both branches (build chunk 7, row T6). The first five rows are
    /// chunk 5's review's (round 1): there the test's other branch succeeds on the held text, and
    /// deciding the condition from it for every continuation would lose the branch 'a' + '\n' needs.
    /// In the last three the verb acted before chunk 7 and chose the no-branch, which fails on 'a';
    /// 'a' + '\n' fails the <c>(?!$)</c>, so the test's <c>a</c> branch holds, and the yes-branch
    /// matches. The judge answers all eight; upstream the first five, and None to the last three.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately, on the last three rows.
    [Test]
    [Arguments("match", @"(?(?=a$(*PRUNE)(?<=b)|a)x|a\n)", "a", 0)]
    [Arguments("match", @"(?(?=a$(*SKIP)(?<=b)|a)x|a\n)", "a", 0)]
    [Arguments("match", @"(?(?!a$(*PRUNE)(?<=b)|a)a\n|x)", "a", 0)]
    [Arguments("fullmatch", @"(?(?=a$(*PRUNE)(?<=b)|a)x|a\n)", "a", 0)]
    [Arguments("search", @"(?(?=a$(*PRUNE)(?<=b)|a)x|a\n)", "ba", 1)]
    [Arguments("match", @"(?(?=a(?!$)(*PRUNE)(?<=b)|a)a\n|b)", "a", 0)]
    [Arguments("match", @"(?(?=a(?!$)(*SKIP)(?<=b)|a)a\n|b)", "a", 0)]
    [Arguments("match", @"(?(?!a(?!$)(*PRUNE)(?<=b)|a)b|a\n)", "a", 0)]
    public void A_skipped_verb_in_a_condition_s_test_leaves_both_branches(
        string operation,
        string pattern,
        string text,
        int start
    )
    {
        Match m = Run(operation, pattern, text);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, start, text.Length - start));
    }

    /// <summary>
    /// An attempt whose start a touched attempt chose has touched the text to come too. The first
    /// pass and the second run the same code only from the same start, and after a touched attempt
    /// the next start is the ordinary bump-along's only when nothing can move it: a <c>(*SKIP)</c>
    /// moves it, and so does a <c>\K</c> whose entry a cut discarded. In each row the first pass
    /// jumps past start 1, the second pass reaches it, and <c>b</c> matches there on the held text:
    /// a success some continuations need, so a partial. Answered as a complete match, the port
    /// reported a match the held text does not have. The first two rows' completions are the port's
    /// own (ledger entry 45: a skip acts when backtracking reaches it): 'ab' + 'c' matches at
    /// (1, 2), where the non-partial search over 'ab' is None. The second row's truth is P(3, 3)
    /// ('aab' + 'ab'), and start 1 is a phantom of class C4: both skips of attempt 0 are blocked, so
    /// the second pass tries a start no continuation reaches. The other rows are the judge's.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers the first two rows F(1, 2) and F(1, 3)
    // (its skip moves the slice when it runs) and the last three P(2, 2), a start too late.
    [Test]
    [Arguments(@"(?=ab(*SKIP)c|a)a(*SKIP)x|b", "ab", 1, 2)]
    [Arguments(@"(?:(a)|b)(?=a(.)(*SKIP)b|a)a(*SKIP)\1|ab", "aab", 1, 3)]
    [Arguments(@"ab\Z(*SKIP)(*F)|b", "ab", 1, 2)]
    [Arguments(@"a(?=b\Z)(?>\K)x|b", "ab", 1, 2)]
    [Arguments(@"a(?=b\Z)\K(*PRUNE)x|b", "ab", 1, 2)]
    public void A_start_a_touched_attempt_chose_is_touched_too(string pattern, string text, int start, int end)
    {
        Match m = new FuzzyRegex(pattern).Match(text, partial: true);

        (m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, start, end));
    }

    /// <summary>
    /// The port grid's counter (VERIFICATION rule 11): the skipped verb of row V1, asked once when it
    /// runs, and none in a call without partial. Debug builds only.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_skipped_verb_is_counted_in_a_debug_build()
    {
        using MatchState skipped = RunPartial(@"(?:a$(*PRUNE)(?<=b)|a.)", "a");

        new FuzzyRegex(@"(?:a$(*PRUNE)(?<=b)|a.)").MatchAtStart("a").Success.Should().BeFalse();
        EngineWork.SkipUnlessCounting();
        skipped.PartialEventCount(PartialEvent.VerbSkipped).Should().Be(1);
    }

    private static Match Run(string operation, string pattern, string text) =>
        operation switch
        {
            "search" => new FuzzyRegex(pattern).Match(text, partial: true),
            "fullmatch" => new FuzzyRegex(pattern).FullMatch(text, partial: true),
            _ => new FuzzyRegex(pattern).MatchAtStart(text, partial: true),
        };

    private static MatchState RunPartial(string pattern, string text)
    {
        var regex = new FuzzyRegex(pattern);
        MatchState state = MatchState.Create(
            regex.PatternObject,
            text.AsMemory(),
            0,
            text.Length,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );
        _ = Matcher.DoMatch(state, search: false);
        return state;
    }
}
