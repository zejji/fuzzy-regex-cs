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
    /// D69: a one-character repeat inside a lookaround body that reaches the edge while it
    /// backtracks runs out like any other item in a body: matching goes on after the lookaround,
    /// where <c>(*FAIL)</c> fails for every continuation. The backtracking arms answered the partial
    /// at once instead: a lazy repeat's look ahead at its tail, string tail or next item, and a
    /// greedy repeat's string tail that runs into the edge ('bca' + 'b' gives 'cab'). The last
    /// three rows are controls, where what follows the lookaround can match: 'b' + 'c' completes
    /// <c>(?=b.*?c)b</c>. <c>\X</c>'s lazy <c>ANY_ALL</c> (<c>(?=\X)(*FAIL)</c> over 'b') is the same
    /// arm, but S57d's escalation answers that row until build chunk 9 part 2 deletes it
    /// (<c>OpenDefectTests</c>).
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: regex 2026.9.10 answers the partial on every row but
    // the reversed greedy row, where it answers None too.
    [Test]
    [Arguments("match", @"(?=b.*?c)(*FAIL)", "b", "None")]
    [Arguments("match", @"(?=b.*?cd)(*FAIL)", "bc", "None")]
    [Arguments("match", @"(?=a*?b)(*FAIL)", "aa", "None")]
    [Arguments("match", @"(?r)(*FAIL)(?<=c.*?b)", "b", "None")]
    [Arguments("match", @"(?=b[^a]*cab)(*FAIL)", "bca", "None")]
    [Arguments("match", @"(?=b.?cab)(*FAIL)", "bca", "None")]
    [Arguments("match", @"(?r)(*FAIL)(?<=bac[^a]*b)", "acb", "None")]
    [Arguments("match", @"(?=b.*?c)b", "b", "P(0,1)")]
    [Arguments("search", @"(?=b.*?c)bx", "b", "P(0,1)")]
    [Arguments("match", @"(?=b[^a]*cab)b", "bca", "P(0,3)")]
    public void A_lazy_repeat_that_backtracks_to_the_edge_in_a_body_runs_out_there(
        string operation,
        string pattern,
        string text,
        string expected
    )
    {
        Match m = Run(operation, pattern, text);

        (m.Success ? $"P({m.Index},{m.Index + m.Length})" : "None").Should().Be(expected);
        m.PartialMatch.Should().Be(m.Success);
    }

    /// <summary>
    /// D69 in a scan: after the empty match at 2 the next attempt may not end empty at 2, so its
    /// <c>\X</c> backtracks its lazy repeat to the edge, which answered the partial from 2. Only a
    /// start at the edge can complete a further match ('abb' + 'x' matches at 3), so the scan's
    /// partial is (3, 3): the brute-force judge's answer and upstream's (2026-10-03).
    /// </summary>
    [Test]
    public void A_scan_whose_lazy_repeat_backtracks_to_the_edge_in_a_body_reports_the_edge()
    {
        IEnumerable<string> matches = new FuzzyRegex(@"(?<=b)(?=\X)")
            .Matches("abb", partial: true)
            .Select(static m => $"{(m.PartialMatch ? 'P' : 'F')}({m.Index},{m.Index + m.Length})");

        string.Join(' ', matches).Should().Be("F(2,2) P(3,3)");
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
    /// The witness of <c>UndeterminedFuzzyRetry</c>, the entry an undetermined fuzzy test leaves
    /// below its touch. The rows above are green without it: the trailing insertion after the
    /// section, or the insertion in front of a failed lookaround, runs out at the same place. Here
    /// the path through the <c>\B</c> fails on the held text inside the section, at a condition
    /// whose test (<c>(?&lt;=^)</c>, <c>(?&lt;=a)</c>) holds there and whose yes-branch is
    /// <c>(*FAIL)</c>, so only the insertion in front of the <c>\B</c> is left. Over '' + 'aa' the
    /// <c>\B</c> fails at 0, the 'a' is inserted, and at 1 both <c>\B</c> and the no-branch hold:
    /// a match of (0, 1). Over 'a' + 'a  ' the search's match starts at 0. Each row answers None, or
    /// the later start P(1, 1), without the retry.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: regex 2026.9.10 answers None to the match and P(1, 1) to
    // the search; without partial it matches 'aa' at (0, 1) and 'a  ' at (0, 2).
    [Test]
    [Arguments("match", @"(?:\B(?(?<=^)(*FAIL))){i<=1}", "")]
    [Arguments("search", @"a(?:\B(?(?<=a)(*FAIL))){i<=1}", "a")]
    public void Only_the_failing_side_of_an_undetermined_fuzzy_test_reaches_the_insertion(
        string operation,
        string pattern,
        string text
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "match", StringComparison.Ordinal)
            ? regex.MatchAtStart(text, partial: true)
            : regex.Match(text, partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, text.Length));
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
    /// Row N8, the run-out taint on an atomic group open at the edge: 'b' + 'a' makes the atomic
    /// group's <c>\wa</c> match, so it commits and <c>(?&lt;!a)</c> then fails, which the group's
    /// empty branch would otherwise have rescued; ' b' + 'a' completes the match. Without the taint
    /// the run-out closes the group as if its first path were certain, and the second pass finds
    /// nothing (build chunk 8's witness for chunk 6's rule, shown in a Release build).
    /// </summary>
    // Upstream answers P(0, 2) too.
    [Test]
    public void A_run_out_taints_the_atomic_groups_open_in_the_body()
    {
        Match m = new FuzzyRegex(@".(?!(?>\wa|(?<!b))(?<!a))").MatchAtStart(" b", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
    }

    /// <summary>
    /// The run-out taint for a verb after the edge: in 'a' + 'x' the body's first branch reaches the
    /// verb, <c>b</c> fails, and the verb ends the verb's scope, so the negative lookahead passes and
    /// <c>a</c> matches. The body's other branch, <c>.</c>, succeeds on the held text, and without the
    /// taint that success counts as certain and fails the lookahead for every continuation: the
    /// first rows are refused (the search starts too late, at 1). Row Q3, `(?!a.(*PRUNE)b|a)a`, is
    /// no witness: the branches share their <c>a</c>, so the run-out is the branch's own test of
    /// <c>.</c> and the second branch is never reached. In the next rows the verb is inside a
    /// positive lookahead or a condition's test, which a verb unwinds straight through (ledger entry
    /// 47): the scope is the negative lookahead or the condition, and a taint on the lookahead's own
    /// frame left them refused. The last row is the control: no verb, and the held <c>.</c> success
    /// is certain.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream confines a verb to the innermost group, and
    // answers None to the rows with a positive lookahead. The expected answers are the port's own
    // non-partial answers over every continuation of up to three characters.
    [Test]
    [Arguments("match", @"(?!a.(*PRUNE)b|.)a", true, 0)]
    [Arguments("match", @"(?!a.(*SKIP)b|.)a", true, 0)]
    [Arguments("search", @"(?!a.(*PRUNE)b|.)a", true, 0)]
    [Arguments("match", @"(?!(?=a.(*PRUNE)b)x|.)a", true, 0)]
    [Arguments("search", @"(?!(?=a.(*SKIP)b)x|.)a", true, 0)]
    [Arguments("match", @"(?!(?=(?=a.(*PRUNE)b))x|.)a", true, 0)]
    [Arguments("match", @"(?(?=(?=a.(*PRUNE)b)x|.)b|a)", true, 0)]
    [Arguments("match", @"(?!a.b|.)a", false, 0)]
    public void A_run_out_taints_the_scope_of_a_verb_after_the_edge(
        string operation,
        string pattern,
        bool partial,
        int start
    )
    {
        Match m = Run(operation, pattern, "a");

        (m.Success, m.PartialMatch, m.Index).Should().Be((partial, partial, start));
    }

    /// <summary>
    /// A run-out inside a fuzzy section the body opened closes that section for the continuation,
    /// as its END_FUZZY would: the section around the lookahead is current again, with the body's
    /// errors counted in it. In the first two rows the body spends one error before the edge
    /// ('y' for 'c'), so the outer section has none left for the tail, and no continuation
    /// completes the match. Left open, the body's section governed the tail with its own limits,
    /// and the port answered P(0, 3) with two errors under a limit of one. In the third row the
    /// reported counts stay within the outer limit of two, where the open section let three
    /// through. The fourth row guards the backtracking entry that gives the body its section back:
    /// the negative lookahead's body runs out, is backtracked into, and its <c>b</c> then succeeds
    /// on the held text within the body's own limit, so the lookahead fails for every
    /// continuation; left in the outer section, the body could not afford that path and the port
    /// answered P(0, 3). The last two rows are controls: 'aby' + 'd' completes each within the
    /// limits.
    /// </summary>
    // Upstream answers None to the first two and the fourth, and P(0, 3) to the others.
    [Test]
    [Arguments(@"(?:a(?=(?:bcd){e<=2})bx){0<=e<=1}", false, 0)]
    [Arguments(@"(?:a(?=(?:(?:bcd){1<=e<=2}|b))(?:b|c){1<=e<=1}x){0<=e<=1}", false, 0)]
    [Arguments(@"(?:a(?=(?:(?:bcd){1<=e<=1}))(?:b){1<=e<=1}){1<=e<=2}", true, 2)]
    [Arguments(@"(?:a(?!(?:bcd){e<=2})b.){e<=1}", false, 0)]
    [Arguments(@"(?:a(?=(?:bcd){e<=2})b.){0<=e<=1}", true, 1)]
    [Arguments(@"(?:a(?=(?:bcd){e<=1})bzz){e<=3}", true, 2)]
    public void A_run_out_in_a_fuzzy_section_of_a_body_counts_its_errors_outside_it(
        string pattern,
        bool partial,
        int errors
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("aby", partial: true);

        m.Success.Should().Be(partial);
        if (partial)
        {
            (m.PartialMatch, m.Index, m.Length, m.FuzzyCounts.Total).Should().Be((true, 0, 3, errors));
        }
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
