using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 M2, build chunk 7: the cut rule for a condition's test, and the <c>NoBranch</c> entry
/// (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, section 4.2, and the witnesses L4, L5
/// and T4-T6 of sections 2 and 7). Row X2 waits for a design step: once its test takes both
/// branches, its repeat body has two paths at every position, and the engine's repeat guards, which
/// are upstream's, do not bound that (a position where the body matched is never guarded, so
/// upstream's own partial <c>(?:a|a)*b</c> over 'a' * 24 + 'c' takes 2 s, measured 2026-09-29).
/// </summary>
/// <remarks>
/// Each expected answer is the brute-force judge's, <c>tools/probes/d11-brute-judge.py</c> (upstream
/// <c>regex</c> 2026.9.10 without partial, over every continuation of one to four characters),
/// measured 2026-09-29. Where a design row reads a word test, S57d's escalation answers the same span
/// until build chunk 9, so the Debug count of second-pass successes is the witness, and a twin with
/// <c>$</c> for the read is the witness in both builds.
/// </remarks>
public sealed class PartialConditionRuleTests
{
    /// <summary>
    /// Rows T4 and T5 with <c>$</c>, and more of their kind: a test whose body read the text to come
    /// holds only for some continuations, so the condition takes the branch it picks and, when that
    /// fails, the other. 'a' + 'b' fails each <c>$</c>, and 'ba' + 'b' the search's.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to the matches and P(2, 2) to the
    // search.
    [Test]
    [Arguments("match", @"(?(?=a$)(?<=b)|a)", "a", 0)]
    [Arguments("match", @"(?(?=a$)b|a)", "a", 0)]
    [Arguments("match", @"(?(?!a$)a|(?<=b))", "a", 0)]
    [Arguments("match", @"(?(?!a$)a|x)", "a", 0)]
    [Arguments("match", @"(?(?!a$|b)a|(?<=b))", "a", 0)]
    [Arguments("search", @"(?(?=a$)(?<=b)|a)", "ca", 1)]
    public void A_condition_whose_test_read_the_text_to_come_takes_both_branches(
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
    /// Rows L4 and L5: a consuming item that runs out of text inside a condition's test no longer
    /// ends the attempt with a partial. The test holds only for some continuations, so the yes-branch
    /// runs and then the no-branch: 'a' matches the yes-branch's <c>a</c>, and neither branch matches
    /// 'c'. A full match would need the text to end after the <c>a</c>, which the test's <c>..</c>
    /// forbids.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately, on the last two rows: upstream answers P(0, 1).
    [Test]
    [Arguments("match", "a", true)]
    [Arguments("match", "c", false)]
    [Arguments("fullmatch", "a", false)]
    public void A_run_out_in_a_condition_s_test_goes_on_in_its_branches(string operation, string text, bool partial)
    {
        Match m = Run(operation, @"(?(?=..)a|b)", text);

        (m.Success, m.PartialMatch).Should().Be((partial, partial));
    }

    /// <summary>
    /// A condition test's dedup records go with its frame, as a lookaround's do: when its unblocked
    /// success cuts (the END_CONDITIONAL arm), and when it is backtracked out of (the CONDITIONAL
    /// arm). The second alternative's test opens its frame at the first one's stack position, and
    /// a record left behind would drop its blocked success, the one 'a' + '\n' needs (review of
    /// chunk 7, round 1).
    /// </summary>
    // Upstream answers the same.
    [Test]
    [Arguments(@"(?:(?(?=(?<g>a)$|(?<h>a))(*FAIL))|(?(?=(?<g>a)$|(?<h>a))(?(h)(*FAIL)|a\n))|z)")]
    [Arguments(@"(?:(?(?=(?<g>a)$)(*FAIL)|(*FAIL))|(?(?=(?<g>a)$|(?<h>a))(?(h)(*FAIL)|a\n))|z)")]
    public void A_condition_s_dedup_records_go_with_its_frame(string pattern)
    {
        Match m = Run("match", pattern, "a");

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    /// <summary>
    /// The design rows whose read is a word test, which S57d answers with the same span: T4 and T5
    /// take both branches, T6 skips the <c>(*PRUNE)</c> in its test, and L4 is a second-pass success.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to T4-T6.
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?(?=a\b)(?<=b)|a)", "SuccessTurnedPartial", 1)]
    [Arguments(@"(?(?=a\b)(?<=b)|a)", "ConditionBothWays", 1)]
    [Arguments(@"(?(?!a\b)a|(?<=b))", "SuccessTurnedPartial", 1)]
    [Arguments(@"(?(?!a\b)a|(?<=b))", "CutBlockedCondition", 1)]
    [Arguments(@"(?:(?(?=a\B(*PRUNE)(?<=b))x|a\B))", "SuccessTurnedPartial", 1)]
    [Arguments(@"(?:(?(?=a\B(*PRUNE)(?<=b))x|a\B))", "VerbSkipped", 1)]
    [Arguments(@"(?(?=..)a|b)", "SuccessTurnedPartial", 1)]
    public void The_design_rows_of_a_condition_at_the_edge(string pattern, string counted, int count)
    {
        using MatchState state = RunPartial(pattern, "a");
        Match m = Run("match", pattern, "a");

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(Enum.Parse<PartialEvent>(counted)).Should().Be(count);
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
