using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

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
    /// A budget partial reports no capture even with a group call open, as the written-out pattern
    /// does (<c>(x)(?:(a)|a){12}(?!\b)(*FAIL)\2</c> answers the same span with every group unset).
    /// The budget stands for no path, so D62's rule, which reports what every completion of the
    /// partial's path would report, has no path to read: putting back the caller's groups from the
    /// open call's frame reported group 1 as (0, 1) under the default capture rule, and tripped
    /// the assert that a call only adds captures (chunk 9 review, finding 2).
    /// </summary>
    // Upstream 2026.9.10 answers P(0, 13) with group 1 set, its escalation's answer; the judge says
    // None, since (*FAIL) fails every continuation.
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void A_budget_partial_inside_an_open_call_reports_no_capture(bool keepCallCaptures)
    {
        var regex = new FuzzyRegex(
            @"(x)(?&c)(?(DEFINE)(?<c>(?:(a)|a){12}(?!\b)(*FAIL)\3))",
            FuzzyRegexOptions.None,
            EngineWork.HangGuard
        );
        regex.PatternObject.KeepCallCaptures = keepCallCaptures;

        Match m = regex.MatchAtStart("x" + new string('a', 12), partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 13));
        m.Groups[1].Captures.Should().BeEmpty();
        m.Groups[2].Captures.Should().BeEmpty();
        m.Groups[3].Captures.Should().BeEmpty();
    }

    /// <summary>
    /// Each kind of fork is counted once per construct that keeps more than one outcome (design
    /// section 4.4), at one site each; every row is the witness of the site it names (rule 12). A
    /// negative lookaround or test is one fork however many of its body's successes were blocked,
    /// counted when it passes undetermined; a positive one forks at each blocked end that goes on,
    /// and its no-branch adds none. Before the chunk 9 review's repair, the negative lookaround and
    /// both conditions counted 2 (finding 3), so the budget fired at half the designed count.
    /// </summary>
    [Test]
    [Arguments(@"a(?>\b|)(*FAIL)", "atomic group's blocked cut (BlockAtomicCut)", 1)]
    // 4 since D90: the tail guard that the first continuation's failure wrote cut two later paths
    // to the blocked end; a guard written after a blocked end is no longer kept, since in general
    // it records the continuation, not the position (MatchState.BlockedEndMark).
    [Arguments(@"a(?:\b|)++(*FAIL)", "possessive repeat's blocked cuts (BlockAtomicCut)", 4)]
    [Arguments(@"a\b(*PRUNE)(*FAIL)", "skipped verb (SkipABlockedVerb)", 1)]
    [Arguments(@"a(?=\b)(*FAIL)", "positive lookaround's blocked end (EndALookaroundInTheSecondPass)", 1)]
    [Arguments(@"a(?<=a\b)(*FAIL)", "lookbehind's blocked end (EndALookaroundInTheSecondPass)", 1)]
    [Arguments(@"a(?!\b)(*FAIL)", "negative lookaround passed undetermined (the LOOKAROUND arm)", 1)]
    [Arguments(@"a(?(?=\b)(*FAIL)|(*FAIL))", "positive test's blocked end (EndALookaroundInTheSecondPass)", 1)]
    [Arguments(@"a(?(?!\b)(*FAIL)|(*FAIL))", "negative test, both branches (the CONDITIONAL arm)", 1)]
    [Arguments(@"(?=a(.))(?(1)(*FAIL)|(*FAIL))", "lookahead's end, then group-exists on an unknown capture", 2)]
    public void Each_fork_is_counted_once(string pattern, string site, long forks)
    {
        var regex = new FuzzyRegex(pattern);
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

        state.Forks.Should().Be(forks, site);
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

    /// <summary>
    /// Row X1's cost: every iteration's lookahead ends blocked and leaves a dedup entry for its
    /// frame, so up to n entries are live at once, and the dedup scanned all of them at every
    /// look-up and every drop. The call ran in time quadratic in n up to the budget (2.5 s at
    /// n = 1000, Release, 2026-10-03; a profile put 72% of it in those scans). Kept in frame order,
    /// a look-up reads only its own frame's entries and a drop only the entries it drops. The count
    /// is kept in every build, and the test asks for a non-zero count as well, so it cannot pass
    /// for nothing in the Release suite CI runs (review L1, 2026-10-04: the count was Debug-only).
    /// </summary>
    [Test]
    public void The_dedup_examines_a_bounded_number_of_entries_per_fork()
    {
        const int n = 400;
        var regex = new FuzzyRegex(@"(?:(?=(.*)z|())a)*b\1\2");
        string subject = new string('a', n) + "c";
        using MatchState state = MatchState.Create(
            regex.PatternObject,
            subject.AsMemory(),
            0,
            subject.Length,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );

        _ = Matcher.DoMatch(state, search: false);

        state.DedupEntriesExamined.Should().BeInRange(1, 8L * 256 * (n + 1));
    }

    /// <summary>
    /// The second pass's stack budget: a partial call whose second pass keeps more bytes than the
    /// budget answers the budget's partial at the attempt's start, where it threw at the
    /// backtracking stack's 1 GB limit. Three lookaheads over repeats nested in repeats keep every
    /// blocked body's entries, so the stacks grow as the cube of the text; at 'ab' * 500 they passed
    /// the limit (2026-10-04). No continuation is refused: 'c' after the held text completes the
    /// match, so the partial is right here, and wherever the budget fires it is a phantom at worst
    /// (class C5).
    /// </summary>
    // Upstream 2026.9.10 answers P(0, 1000) too, at once (its lookaround run-out ends the match).
    [Test]
    public void A_second_pass_that_keeps_too_many_bytes_answers_the_budget_partial()
    {
        string text = string.Concat(Enumerable.Repeat("ab", 500));
        var regex = new FuzzyRegex(
            @"(?:(?=(?:(?=(?:ab)*(?<=b))ab)*)ab)*c",
            FuzzyRegexOptions.None,
            EngineWork.HangGuard
        );

        Match m = regex.MatchAtStart(text, partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1000));
    }
}
