using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The runtime re-entry guard and the failed-call memo, reached by patterns the compile-time
/// left-recursion check lets through (D4). The guard's capture-keyed part (<c>Matcher.TryOpenCall</c>,
/// <c>Matcher.AnOpenCallSawThisReadState</c>, <c>Matcher.SameRead</c> and the memo's key) lost
/// every test when the left-recursive patterns that used to reach it were refused; these restore
/// them (blind review of the D4 static refusal, finding 5, 2026-10-08).
/// </summary>
/// <remarks>
/// <para>
/// Two shapes reach a call at the position it is already open at without the check seeing it. In
/// a fuzzy section, <c>(?:q){d&lt;=1}(?R)</c> deletes the q, which the check reads as text. In an
/// exact pattern, <c>a(?&lt;=(?=(?R))a)</c> reads an <c>a</c>, steps back over it and calls the
/// pattern where it started. Each row is one of the removed <c>GroupCallTests</c> rows rewritten in
/// one of those forms.
/// </para>
/// <para>
/// The answers are the engine's, measured 2026-10-09. Upstream 2026.9.10 answers three rows, and
/// agrees on all three: <c>(?:\1x|\2()(?:q){d&lt;=1}(?R)|()(?:q){d&lt;=1}(?R))</c> and its reversed
/// form over 'x' give (0, 1), and the lookbehind conditional row over 'x' gives (0, 0). It raises
/// MemoryError or times out on the rest; the D4 reference matcher (<c>tools/probes/d4/order.py</c>)
/// does not read lookarounds round a call, so it cannot judge them.
/// </para>
/// </remarks>
public sealed class CallGuardWitnessTests
{
    [Test]
    // GroupCallTests.A_call_that_reaches_nothing_new_but_sees_a_new_capture_is_let_through: a group
    // set between two calls at one position makes the inner call new. Upstream agrees on the first two.
    [Arguments(@"(?:\1x|\2()(?:q){d<=1}(?R)|()(?:q){d<=1}(?R))", "x", "(0,1) s0i0d2")]
    [Arguments(@"(?r)(?:x\1|(?R)(?:q){d<=1}\2()|(?R)(?:q){d<=1}())", "x", "(0,1) s0i0d2")]
    [Arguments(@"(?:\1x|\2()a(?<=(?=(?R))a)|()a(?<=(?=(?R))a))", "ax", "None")]
    // GroupCallTests.A_call_guard_keyed_on_what_is_read_stays_polynomial, the matching rows.
    [Arguments(@"(?:()|()|a)(?:q){d<=1}(?R)|\1\2x", "aax", "(0,3) s0i0d8")]
    [Arguments(@"(?:()|()|a)(?:q){d<=1}(?R)|\1\2x", "aay", "None")]
    [Arguments(@"(?:(?=(a*))|a)(?:q){d<=1}(?R)|\1x", "aaaay", "None")]
    [Arguments(@"(?:(?=(a*))|a)(?:q){d<=1}(?R)|(?(1)x)", "aaaay", "(0,4) s0i0d4")]
    [Arguments(@"(?:()|()|()|()|a)(?:q){d<=1}(?R)|\3\4x", "aax", "(0,3) s0i0d9")]
    [Arguments(@"(?:()|())a(?<=(?=(?R))a)|\1\2x", "ax", "None")]
    [Arguments(@"(?:()|a)a(?<=(?=(?R))a)|\1x", "aax", "None")]
    // GroupCallTests.A_capture_taken_inside_a_lookahead_is_part_of_the_call_guard_key: a capture
    // taken in a lookahead or lookbehind is part of the key. Upstream agrees on the 'x' row.
    [Arguments(@"(?|(?=(a))|(?=(aa)))(?:q){d<=1}(?R)|(?(1)x)", "aax", "(0,0)")]
    [Arguments(@"(?:(?<=(a))|a)(?:q){d<=1}(?R)|\1x", "aax", "(0,3) s0i0d2")]
    [Arguments(@"(?|(?=(a))|(?=(aa)))a(?<=(?=(?R))a)|(?(1)x)", "aax", "(0,0)")]
    [Arguments(@"(?|(?=(a))|(?=(aa)))a(?<=(?=(?R))a)|(?(1)x)", "x", "(0,0)")]
    [Arguments(@"(?:(?=(a))|(?=(aa)))a(?<=(?=(?R))a)|(?(1)x)|(?(2)x)", "aax", "(0,1)")]
    public void A_call_reentered_where_the_check_cannot_see_it_answers_as_measured(
        string pattern,
        string subject,
        string expected
    )
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard).Match(subject);

        Answer(m).Should().Be(expected);
    }

    /// <summary>
    /// SHORTCUT (known defect D33, the perf phase): the k-group rows of the removed
    /// A_call_guard_keyed_on_what_is_read_stays_polynomial, in the forms the check lets through,
    /// are exponential in k. The exact rows stayed polynomial because the failed-call memo cut
    /// them; with the call in a lookaround (which turns the memo off for the whole pattern) or
    /// after a fuzzy deletion, the memo does not cut them, as measured, and each order of setting
    /// the k empty groups is walked again. Measured in Debug on 2026-10-09
    /// over 'aay', the fuzzy form takes 3,145, 16,853, 88,995, 461,539 and 2,343,621 steps for
    /// k = 2 to 6, and the lookbehind forms grow the same way (24,074 at k = 4, 1,384,276 at
    /// k = 6). This pins today's cost at k = 4, so a fix shows as a drop and a regression as a
    /// failure. Upgrade path: decide UseCallMemo per call site (D33).
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_guard_keyed_on_what_is_read_is_exponential_where_the_memo_is_off()
    {
        EngineWork.ShouldTakeAtMostSteps(
            static () =>
            {
                var regex = new FuzzyRegex(
                    @"(?:()|()|()|()|a)(?:q){d<=1}(?R)|\1\2\3\4x",
                    FuzzyRegexOptions.None,
                    EngineWork.HangGuard
                );
                regex.Match("aay").Success.Should().BeFalse();
            },
            100_000,
            "today's cost at k = 4 (88,995 steps); it grows about five times with each group"
        );
    }

    private static string Answer(Match match)
    {
        if (!match.Success)
        {
            return "None";
        }

        string span = $"({match.Index},{match.Index + match.Length})";
        FuzzyCounts counts = match.FuzzyCounts;
        return counts == default ? span : $"{span} s{counts.Substitutions}i{counts.Insertions}d{counts.Deletions}";
    }
}
