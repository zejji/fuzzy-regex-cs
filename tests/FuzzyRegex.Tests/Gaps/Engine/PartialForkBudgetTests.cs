using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 M4, build chunk 9: the per-attempt fork budget of a partial call's second pass
/// (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, section 4.4). A fork is a blocked
/// construct that keeps more than one outcome; past 256 (|t| + 1) forks in one attempt, the attempt
/// stops and the call answers the partial at its start.
/// </summary>
/// <remarks>
/// A partial is never a refusal, so the budget cannot lose a match; where it fires it answers a
/// phantom of class C5 (section 6). Counted in engine steps rather than timed (D13).
/// </remarks>
public sealed class PartialForkBudgetTests
{
    /// <summary>
    /// Row X1: every lookahead has two successes with different captures and a backreference
    /// turns the guards off, so the second pass forks at every 'a' and is exponential. The budget
    /// stops it and answers P(0, |t|), the phantom upstream 2026.9.10 answers too (the judge says
    /// None: no 'z' can come before the 'c').
    /// </summary>
    // DIVERGES FROM UPSTREAM on nothing: upstream answers P(0, 41) as well.
    [Test]
    [Category(EngineWork.Category)]
    public void An_attempt_that_forks_past_the_budget_answers_the_partial_at_its_start()
    {
        var regex = new FuzzyRegex(@"(?:(?=(.*)z|())a)*b\1\2", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = new string('a', 40) + "c";

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.MatchAtStart(subject, partial: true);

                (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 41));
                m.Groups[1].Success.Should().BeFalse();
            },
            2_000_000,
            "the budget stops the attempt after 256 (|t| + 1) forks"
        );
    }

    /// <summary>
    /// Row S4's shape: the lookahead runs out at every iteration, so the attempt from start s forks
    /// n - s times, under the budget, but the call forks about n * n / 2 times, over it. Counted
    /// per call, the budget fired in the attempt at 370 and answered P(370, 601) (Release,
    /// 2026-09-30). Counted per attempt it never fires, and the answer is the judge's: only the
    /// attempt from the edge can reach a digit. Design section 4.4's own S4, `a*(?=.*z)b`, now forks
    /// only n times in the whole call (measured, Debug), so it is no witness.
    /// </summary>
    // DIVERGES FROM UPSTREAM, as design section 1 does: upstream 2026.9.10 answers P(0, 601).
    [Test]
    [Category(EngineWork.Category)]
    public void The_budget_counts_per_attempt_so_a_search_with_many_starts_keeps_its_answer()
    {
        var regex = new FuzzyRegex(@"(?:(?=.*z)a)*\d");
        string subject = new string('a', 600) + "c";

        Match m = regex.Match(subject, partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 601, 0));
    }

    /// <summary>
    /// The fork count belongs to one call: a reused match state starts the next call's first pass
    /// with none, which assert A6 checks in a Debug build. Without the reset, the second call here
    /// trips A6 (found by the chunk 9 grid).
    /// </summary>
    [Test]
    public void A_call_after_a_forking_partial_call_starts_with_no_forks()
    {
        var regex = new FuzzyRegex("a(?!(?:b|c){1<=e<=2}$).");

        regex.MatchAtStart("abb", partial: true).PartialMatch.Should().BeTrue();
        regex.MatchAtStart("abb").Success.Should().BeFalse();
    }
}
