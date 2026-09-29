using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 M2, build chunk 6: the cut rule for lookarounds, the run-out inside a lookaround's body, the
/// dedup keyed on captures and fuzzy counts, and the lookaround guard rule
/// (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, section 4.2, and the witnesses of section
/// 7).
/// </summary>
/// <remarks>
/// Each expected answer is the brute-force judge's, <c>tools/probes/d11-brute-judge.py</c> (upstream
/// <c>regex</c> 2026.9.10 without partial, over every continuation of one to four characters),
/// measured 2026-09-29. S57d's escalation turns a failed second pass into a partial whenever a word
/// test stood at the edge, until build chunk 9 deletes it. Where a row reads a word test, the Debug
/// count of second-pass successes is the witness; it is checked after the answer, which a Release
/// build checks too, and a Release build then skips the test with
/// <see cref="EngineWork.DebugOnly"/> as the reason.
/// </remarks>
public sealed class PartialLookaroundRuleTests
{
    /// <summary>
    /// Rows L1, L2 and L6: a consuming item that runs out of text inside a lookaround's body no
    /// longer ends the attempt with a partial. Matching goes on after the lookaround, which holds
    /// only for some continuations: in L1 and L2 <c>[ab]</c> then fails on 'x', so the search's
    /// partial starts at the edge and the match has none; in L6 the body's other branch, <c>x</c>,
    /// matches the held text, so the assertion fails for every continuation.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers P(0, 1) to all three.
    [Test]
    [Arguments("search", @"(?=..)[ab]", "x", 1)]
    [Arguments("match", @"(?=..)[ab]", "x", -1)]
    [Arguments("match", @"(?!.a|x)x", "x", -1)]
    public void A_run_out_in_a_lookaround_body_goes_on_after_the_lookaround(
        string operation,
        string pattern,
        string text,
        int start
    )
    {
        Match m = Run(operation, pattern, text);

        if (start < 0)
        {
            m.Success.Should().BeFalse();
        }
        else
        {
            (m.PartialMatch, m.Index, m.Length).Should().Be((true, start, text.Length - start));
        }
    }

    /// <summary>
    /// A negative lookaround whose body runs out inside a string, followed by a string that needs
    /// the text to come: 'a' + 'c' completes each row. The run-out left the matcher's position in
    /// the body's string behind, and the next string then resumed from it instead of from its start
    /// (review of chunk 6, round 1).
    /// </summary>
    // Upstream answers the same.
    [Test]
    [Arguments("match", @"(?!ab)ac", "a", 0)]
    [Arguments("fullmatch", @"(?!ab)ac", "a", 0)]
    [Arguments("search", @"(?!ab)ac", "a", 0)]
    [Arguments("match", @"(?!ab)ax", "a", 0)]
    [Arguments("match", @"(?!a[b])ac", "a", 0)]
    [Arguments("match", @"(?!ab)[a]c", "a", 0)]
    [Arguments("match", @"(?!ab)a(?:c|cc)", "a", 0)]
    [Arguments("match", @"(?!ab)a\n", "a", 0)]
    [Arguments("match", @"(?i)(?!ab)ac", "a", 0)]
    [Arguments("match", @"(?:(?!ab)a)c", "a", 0)]
    [Arguments("match", @"(?r)ca(?<!ba)", "a", 0)]
    [Arguments("match", @"(?!ab)acd", "a", 0)]
    [Arguments("match", @"(?!abc)abd", "ab", 0)]
    public void A_string_after_a_body_that_ran_out_starts_from_its_first_character(
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
    /// A fuzzy zero-width test passed undetermined keeps its failing side's insertion: over '', the
    /// continuations that fail the <c>\B</c> (a word character to come) match it by inserting that
    /// character, after which <c>(?!^)</c> holds. The first row is the review's (chunk 6, round 1),
    /// which chunk 5 answered only because the inner lookahead's run-out ended the attempt.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately, on the second row: upstream answers None to it and
    // P(0, 0) to the first.
    [Test]
    [Arguments(@"(?m)(?:\B){i<=1}(*SKIP)(?!$(?!a|\Maa)|^)")]
    [Arguments(@"(?:\B){i<=1}(?!^)")]
    public void A_fuzzy_test_passed_undetermined_keeps_the_insertion_of_its_failing_side(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 0));
    }

    /// <summary>
    /// Row C2 with <c>$</c> for the read, and the double negative from chunk 5's review: a negative
    /// lookaround whose body succeeded after a read of the text to come holds for the other
    /// continuations ('True' + ' ' fails the <c>$</c>; ' ' fails the inner lookaround's <c>$</c>,
    /// its prune is never reached, and its <c>a</c> branch makes it fail), so its end is blocked.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to both.
    [Test]
    [Arguments("fullmatch", @"(?!(True|False)$)(.*)", "True")]
    [Arguments("match", @"(?!(?!a$(*PRUNE)(?<=b)|a))a", "a")]
    public void A_negative_lookaround_whose_body_read_the_text_to_come_holds_for_some_continuations(
        string operation,
        string pattern,
        string text
    )
    {
        Match m = Run(operation, pattern, text);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, text.Length));
    }

    /// <summary>
    /// Row L7 with <c>$</c>: the body's first path read the text to come and was backtracked out of,
    /// so its touch is gone, and the empty path's success fails the assertion for every
    /// continuation. A stale mark would block it, a phantom.
    /// </summary>
    // Upstream answers None too.
    [Test]
    public void A_negative_lookaround_whose_touched_path_was_backtracked_out_of_decides()
    {
        new FuzzyRegex(@"(?!a(?:$(?<=b)|))a").MatchAtStart("a", partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// The design rows whose read is a word test, which S57d answers with the same span: C2, C7, N7
    /// and G1 are second-pass successes, L7 is none.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately, on C2 (upstream: None).
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("fullmatch", @"(?!(True|False)\b)(.*)", "True", 4, "NegativeUndetermined", 1)]
    [Arguments("match", @"a(?=:a)", "a:", 2, "SuccessTurnedPartial", 1)]
    [Arguments("match", @"b(?!(?!\w.))", "b", 1, "SuccessTurnedPartial", 1)]
    [Arguments("match", @"(?:b|)(?=b*(?!b)\B)(?<!b)", "bb", 2, "SuccessTurnedPartial", 1)]
    [Arguments("match", @"(?:b|)(?=(?:b|c)*(?!b)\B)(?<!b)", "bb", 2, "SuccessTurnedPartial", 1)]
    [Arguments("match", @"(?:b|)(?=(?:bb?)*(?!b)\B)(?<!b)", "bb", 2, "SuccessTurnedPartial", 1)]
    [Arguments("match", @"(?!a(?:\B(?<=b)|))a", "a", 1, "SuccessTurnedPartial", 0)]
    public void The_design_rows_of_a_lookaround_at_the_edge(
        string operation,
        string pattern,
        string text,
        int end,
        string counted,
        int count
    )
    {
        using MatchState state = RunPartial(operation, pattern, text);
        Match m = Run(operation, pattern, text);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, end));
        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(Enum.Parse<PartialEvent>(counted)).Should().Be(count);
    }

    /// <summary>
    /// Row F1, the dedup's key. The lookahead's body succeeds first with one error (<c>ax\b</c> over
    /// 'ay'), blocked by the <c>\b</c>, and <c>az</c> then has no error left. Its second success,
    /// <c>ay\B</c>, has no error: keyed on the captures alone it would be dropped as a repeat of the
    /// first, and the second pass would find nothing.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None.
    [Test]
    [Category(EngineWork.Category)]
    public void A_lookaround_success_with_other_fuzzy_counts_is_continued()
    {
        const string pattern = @"(?:(?=(?:ax\b|ay\B){e<=1})az){e<=1}";
        using MatchState state = RunPartial("match", pattern, "ay");
        Match m = new FuzzyRegex(pattern).MatchAtStart("ay", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(PartialEvent.SuccessTurnedPartial).Should().Be(1);
        state.PartialEventCount(PartialEvent.CutBlockedLookaround).Should().BePositive();
    }

    /// <summary>
    /// A lookaround's dedup records go with its frame when an unblocked success cuts. The first
    /// lookahead's blocked success (g set, read through the <c>$</c>) is recorded; its second
    /// success, <c>(?&lt;h&gt;a)</c>, cuts unblocked. The second alternative's lookahead then opens
    /// its frame at the same stack position, and a record left behind would drop its own first
    /// success as a repeat, the one 'a' + '\n' needs (review of chunk 7, round 1).
    /// </summary>
    // Upstream answers the same.
    [Test]
    [Arguments("match", 0)]
    [Arguments("fullmatch", 0)]
    [Arguments("search", 0)]
    public void A_lookaround_s_dedup_records_go_with_its_frame(string operation, int start)
    {
        Match m = Run(operation, @"(?:(?=(?<g>a)$|(?<h>a))(*FAIL)|(?=(?<g>a)$|(?<h>a))(?(h)(*FAIL)|a\n)|z)", "a");

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, start, 1 - start));
    }

    /// <summary>
    /// A nested lookaround's dedup records go when its body is exhausted, whatever order they were
    /// made in. The outer lookahead's blocked end records its success after the inner one's, which
    /// sits at a higher frame; dropping only from the list's end stopped at the outer record and
    /// left the inner one, which then dropped the inner lookahead's later success as a repeat, the
    /// one 'a' + '\nx' needs (review of chunk 7, round 2).
    /// </summary>
    // Upstream answers the same.
    [Test]
    [Arguments("match", 0)]
    [Arguments("fullmatch", 0)]
    [Arguments("search", 0)]
    public void A_nested_lookaround_s_dedup_records_go_in_any_order(string operation, int start)
    {
        Match m = Run(operation, @"(?=(?:(?=a$)(?<p>)|(?=a(?m:$))|z))(?(p)(*FAIL)|a\nx)", "a");

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, start, 1 - start));
    }

    /// <summary>
    /// The dedup itself: each iteration's lookahead succeeds twice, once reading the text to come
    /// (<c>.*$</c>) and once not, with the same captures and counts, so the second success is not
    /// continued. Without the dedup every iteration doubles the paths: 'a' * 24 + 'c' took over
    /// 60 s (measured 2026-09-29, 5.8 s at 20), where it takes under 20,000 steps with it.
    /// </summary>
    // Upstream answers None too.
    [Test]
    [Category(EngineWork.Category)]
    public void A_lookaround_success_that_repeats_an_earlier_one_is_not_continued() =>
        EngineWork.ShouldTakeAtMostSteps(
            static () =>
                new FuzzyRegex(@"(?:(?=.*$|)a)*b", FuzzyRegexOptions.None, EngineWork.HangGuard)
                    .MatchAtStart(new string('a', 24) + "c", partial: true)
                    .Success.Should()
                    .BeFalse(),
            20_000,
            "the dedup keeps the second pass polynomial"
        );

    /// <summary>
    /// SHORTCUT until build chunk 8: a pattern that reads a capture while it matches keeps the
    /// run-out partial, because the body's captures after the edge are unknown. Going on after the
    /// lookaround with the group unset would refuse rows T3 and Q2 ('a' + 'a' completes both).
    /// </summary>
    // Upstream answers P(0, 1) to both too.
    [Test]
    [Arguments(@"(?=a(.))\1")]
    [Arguments(@"(?=a(.))(?(1)a|b)")]
    public void A_run_out_before_a_capture_is_read_keeps_the_partial(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("a", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    private static Match Run(string operation, string pattern, string text) =>
        operation switch
        {
            "search" => new FuzzyRegex(pattern).Match(text, partial: true),
            "fullmatch" => new FuzzyRegex(pattern).FullMatch(text, partial: true),
            _ => new FuzzyRegex(pattern).MatchAtStart(text, partial: true),
        };

    private static MatchState RunPartial(string operation, string pattern, string text)
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
            matchAll: string.Equals(operation, "fullmatch", StringComparison.Ordinal),
            regex.PatternLimits
        );
        _ = Matcher.DoMatch(state, search: false);
        return state;
    }
}
