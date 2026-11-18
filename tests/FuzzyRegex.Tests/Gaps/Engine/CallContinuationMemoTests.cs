using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D99, the continuation memo: a group call fails at once when an earlier call made in an
/// equivalent state, with an equivalent stack of pending returns, was backtracked past without a
/// match (<see cref="CallContinuationMemo"/>,
/// <c>docs/plan/2026-11-17-d99-continuation-keyed-call-memo.md</c>).
/// </summary>
/// <remarks>
/// The witness tests are answers the memo would change if a part of its key were missing, from the
/// blind review of the design (2026-11-18). Their expected answers are Python regex 2026.9.29's
/// (pinned venv), and they pass with the memo off too. They switch the memos on from the first call
/// (<c>PatternObject.EagerCallMemo</c>), since subjects this short never make enough calls to switch
/// them on by themselves.
/// </remarks>
public sealed class CallContinuationMemoTests
{
    // D99's witness. D93 let a level delete its 'a' and call g again at the same position with one
    // more deletion; at each of the 13 positions the match re-enters up to the remaining budget, and
    // each chain fails the same way after the calls return. Debug steps measured 2026-11-18: 76,446
    // on main 56fcabfb.
    [Test]
    [Category(EngineWork.Category)]
    public void A_fuzzy_recursion_whose_returns_all_fail_the_same_way_is_not_searched_again()
    {
        var regex = new FuzzyRegex("(?:(?P<g>a(?&g)b|c)){d<=6}", FuzzyRegexOptions.None, EngineWork.HangGuard);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(new string('a', 12) + "c");
                (m.Index, m.Length).Should().Be((0, 5));
                m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 6));
            },
            30_000,
            "two calls with the same state and the same pending returns fail the same way"
        );
    }

    [Test]
    public void The_key_holds_the_exact_counts_of_the_sections_outside_the_call()
    {
        // Blind review F1. Both calls at 1 see the inner {e<=0} section with no errors; the first
        // came after a substituted 'q', the second after an exact 'a'. Once the inner section closes,
        // the 'c' against 'd' needs the outer {s<=1}'s substitution, which only the second path has.
        // Python regex 2026.9.29: (0, 3), counts (1, 0, 0).
        FuzzyRegex regex = WithMemo("(?:.*\\d)?(?:(?:q|a)(?:(?1)){e<=0}c|(b(?1)?)){s<=1}");

        Match m = regex.Match("abd");

        (m.Index, m.Length).Should().Be((0, 3));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    [Test]
    public void The_key_holds_where_the_call_returns_to()
    {
        // Blind review F2. Two sites call group 1 at 0 in the same state; the first returns to 'x',
        // the second to 'y'. Python regex 2026.9.29: (0, 2).
        FuzzyRegex regex = WithMemo("(?:..z|)(?:(?1)x|(?1)y|(a(?1)?)){s<=0}");

        Match m = regex.Match("ay");

        (m.Index, m.Length).Should().Be((0, 2));
    }

    [Test]
    public void The_key_holds_where_each_pending_call_returns_to()
    {
        // Found by removing the pending calls' return nodes from the key (mutation sweep,
        // 2026-11-18): calls of g at the same position whose pending calls return to different
        // places, the lazy repeat's (?&g) or g's own (?&g)?, share every other part of the key; the
        // mutated key answered one deletion. Python regex 2026.9.29: (0, 4), counts (0, 0, 2),
        // group 1 (2, 4). Compiled as the library compiles it, where the mutation found it.
        var regex = new FuzzyRegex(
            "(?:(?:x|(?&g))*?(?P<g>a(?&g)?b?)x?){1<=d<=2}",
            FuzzyRegexOptions.None,
            EngineWork.HangGuard
        );
        regex.PatternObject.EagerCallMemo = true;

        Match m = regex.FullMatch("abaa");

        (m.Index, m.Length).Should().Be((0, 4));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((2, 2));
    }

    [Test]
    public void A_hit_keeps_the_open_calls_out_of_the_failed_call_memo()
    {
        // A hit skips everything after the call, every pending return included, so an open call
        // may never return on this path although it could. Recorded by the failed-call memo as a
        // call that cannot return, it failed a later call of g with the same entry key whose return
        // led to the first match. Found by the differential on D93's call rows (2026-11-18); with
        // both memos off, and on main, group 1 is (0, 2).
        FuzzyRegex regex = WithMemo("(?:(?P<g>[ab](?&g)a+|ba)(?&g)){1<=d<=2}");

        Match m = regex.Match("aabbaa");

        (m.Index, m.Length).Should().Be((0, 6));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 2));
    }

    [Test]
    [Category(EngineWork.Category)]
    [Arguments(200)]
    [Arguments(400)]
    public void A_deep_recursion_costs_about_what_it_costs_without_the_memo(int depth)
    {
        // Blind review of the build, finding 1 (2026-11-18): every keyed call walked and copied the
        // whole stack of pending returns, so a search that recursed n deep at each start cost n^3
        // where it cost n^2 without the memo, with no hit at all. Debug, before the fix: 5,077,840 B off
        // against 61,595,856 on at depth 200, and 20,752,904 against 460,189,440 at 400. After it
        // (2026-11-18): 5,077,840 against 5,082,480, and 20,752,904 against 20,755,728; the same
        // steps both ways (162,000 and 644,000).
        string subject = new('a', depth);
        (long offSteps, long offBytes) = Cost(subject, skip: true);
        (long onSteps, long onBytes) = Cost(subject, skip: false);

        onSteps.Should().BeLessThanOrEqualTo(offSteps * 2, "the memo adds no steps where it never hits");
        onBytes.Should().BeLessThanOrEqualTo(offBytes * 3, "a key costs about the same at any depth");
    }

    [Test]
    [Category(EngineWork.Category)]
    [Arguments(200)]
    [Arguments(400)]
    public void Each_open_call_is_interned_once_however_deep_the_recursion(int depth)
    {
        // Blind review of the fix round, finding 1 (2026-11-18): the steps and bytes above cannot
        // see a key that walks every pending level again, since interning allocates nothing once a
        // tail has been seen; with the cache in LinkOf dropped the same row is cubic in Release
        // (6,317 ms against 400 ms off at n = 1000). This counts the levels interned per key built.
        // Debug, 2026-11-18: 16,384 keys and 16,269 levels interned at depth 200, 16,341 at 400; with
        // the cache dropped, 1,216,115 and 3,075,705.
        FuzzyRegex regex = GroupCallTests.WithCallsKept("(?P<g>a(?&g)?c)(?:x){s<=1}", EngineWork.HangGuard);
        regex.Match(new string('a', depth)).Success.Should().BeFalse();
        long keys = WorkCounter.CallContinuationKeys;
        long interned = WorkCounter.CallContinuationLevelsInterned;

        regex.Match(new string('a', depth)).Success.Should().BeFalse();

        keys = WorkCounter.CallContinuationKeys - keys;
        interned = WorkCounter.CallContinuationLevelsInterned - interned;
        keys.Should().BePositive("the row must build keys, or this measures nothing");
        interned.Should().BeLessThanOrEqualTo(2 * keys, "a key interns only the levels opened since the last key");
    }

    private static (long Steps, long Bytes) Cost(string subject, bool skip)
    {
        FuzzyRegex regex = GroupCallTests.WithCallsKept("(?P<g>a(?&g)?c)(?:x){s<=1}", EngineWork.HangGuard);
        regex.PatternObject.SkipContinuationMemo = skip;
        regex.Match(subject).Success.Should().BeFalse();
        long steps = WorkCounter.Steps;
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        regex.Match(subject).Success.Should().BeFalse();
        return (WorkCounter.Steps - steps, GC.GetAllocatedBytesForCurrentThread() - bytes);
    }

    /// <summary>A pattern compiled for one test, with the memos on from the first call.</summary>
    private static FuzzyRegex WithMemo(string pattern)
    {
        FuzzyRegex regex = GroupCallTests.WithCallsKept(pattern, EngineWork.HangGuard);
        regex.PatternObject.EagerCallMemo = true;
        return regex;
    }
}
