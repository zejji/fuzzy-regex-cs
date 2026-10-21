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
    /// <c>(?=b.*?c)b</c>. The <c>\X</c> rows (its lazy <c>ANY_ALL</c>) were answered by S57d's
    /// escalation as well, until build chunk 9 deleted it.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: regex 2026.9.10 answers the partial on every row but
    // the two `\X` rows and the reversed greedy row, where it answers None too.
    [Test]
    [Arguments("match", @"(?=\X)(*FAIL)", "b", "None")]
    [Arguments("match", @"(?r)(*FAIL)(?<=\X)", "b", "None")]
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
    /// D69's shape in the fuzzy retry of a failed lookaround (ledger entry 50): when the inner
    /// <c>(?=b)</c> or <c>(?&lt;=b)</c> fails, the retry tries an insertion in front of it, which at
    /// the edge runs out of text. Inside an outer lookaround that is a run-out in a body like any
    /// other, so matching goes on after the outer lookaround, where <c>(*FAIL)</c> fails for every
    /// continuation and the negative form's <c>b</c> alternative needs a later start. The arm
    /// answered the partial at once instead. The reversed rows are the ones whose stale node (the
    /// <c>(*FAIL)</c>, outside the body) tripped assert A10 when the arm was first routed through
    /// the run-out; the run-out is the inner lookaround's own node. The last row is the control: 'a'
    /// + 'b' completes it.
    /// </summary>
    /// <remarks>
    /// Expected answers: the brute-force judge and the port's own non-partial judge, which agree on
    /// every row (2026-10-03).
    /// </remarks>
    // DIVERGES FROM UPSTREAM, deliberately: regex 2026.9.10 answers P(0, 1) to the first three
    // rows. It agrees on the rest.
    [Test]
    [Arguments("match", @"(?=(?:a(?=b)){e<=1})(*FAIL)", "a", "None")]
    [Arguments("fullmatch", @"(?=(?:a(?=b)){e<=1})a", "a", "None")]
    [Arguments("search", @"(?!(?:a(?=b)){e<=1})(*FAIL)|b", "a", "P(1,1)")]
    [Arguments("match", @"(?=(?:a(?<=b)){i<=1})(*FAIL)", "a", "None")]
    [Arguments("fullmatch", @"(?=(?:a(?<=b)){i<=1})a", "a", "None")]
    [Arguments("search", @"(?!(?:a(?<=b)){i<=1})(*FAIL)|b", "a", "P(1,1)")]
    [Arguments("search", @"(?r)(*FAIL)(?<=(?:a(?=b)){e<=1})", "", "None")]
    [Arguments("match", @"(?r)(*FAIL)(?<=(?:a(?=b)){e<=1})", "", "None")]
    [Arguments("match", @"(?=(?:a(?=b)){e<=1})ab", "a", "P(0,1)")]
    public void An_insertion_before_a_failed_lookaround_at_the_edge_runs_out_in_the_outer_body(
        string operation,
        string pattern,
        string text,
        string expected
    )
    {
        Match m = Run(operation, pattern, text);
        string kind = m.PartialMatch ? "P" : "F";

        (m.Success ? $"{kind}({m.Index},{m.Index + m.Length})" : "None").Should().Be(expected);
    }

    /// <summary>
    /// A pattern that starts with a lookaround gets a first-set check in front of it (upstream
    /// <c>_check_firstset</c>, <c>_regex_core.py</c>:380-409): a zero-width test of the character the
    /// pattern will read first. At the edge that test ran out of text before the lookaround's frame
    /// was pushed, so the attempt answered the partial at once instead of running out in the body,
    /// where matching goes on after the lookaround and <c>(*FAIL)</c> or a failing lookbehind fails
    /// for every continuation. The last two rows are controls: 'b' completes <c>(?=b)b</c>, and a
    /// search of 'xb' matches <c>(?&lt;=x)b</c> at 1.
    /// </summary>
    /// <remarks>
    /// Expected answers: the brute-force judge (2026-10-03). The lead was in the D11 status note.
    /// </remarks>
    // DIVERGES FROM UPSTREAM, deliberately: regex 2026.9.10 answers P(0, 0), or P(1, 1) from 'c'
    // and 'a' forward, to every row but the controls, where it agrees.
    [Test]
    [Arguments("match", @"(?=b)(*FAIL)", "", "None")]
    [Arguments("search", @"(?=b)(*FAIL)", "", "None")]
    [Arguments("search", @"(?=b)(*FAIL)", "c", "None")]
    [Arguments("search", @"(?=[bc])(*FAIL)", "a", "None")]
    [Arguments("match", @"(?<=x)b", "", "None")]
    [Arguments("fullmatch", @"(?<=x)b", "", "None")]
    [Arguments("match", @"(?r)(*FAIL)(?<=b)", "", "None")]
    [Arguments("search", @"(?r)(*FAIL)(?<=b)", "c", "None")]
    [Arguments("match", @"(?<=(?:\X\X){e<=1})b", "", "None")]
    [Arguments("fullmatch", @"(?<=(?:\X\X){e<=1})b", "", "None")]
    [Arguments("match", @"(?=b)b", "", "P(0,0)")]
    [Arguments("search", @"(?<=x)b", "", "P(0,0)")]
    public void A_first_set_check_at_the_edge_defers_to_the_lookaround_it_guards(
        string operation,
        string pattern,
        string text,
        string expected
    )
    {
        Match m = Run(operation, pattern, text);

        (m.Success ? $"P({m.Index},{m.Index + m.Length})" : "None").Should().Be(expected);
        if (m.Success)
        {
            m.PartialMatch.Should().BeTrue();
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
    /// The design rows whose read is a word test: C2, C7, N7 and G1 are second-pass successes. L7 is
    /// none, and answers None since build chunk 9 deleted S57d's escalation, which answered P(0, 1)
    /// (the judge: None, as the refused rows of the design's section 7 say).
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
    [Arguments("match", @"(?!a(?:\B(?<=b)|))a", "a", -1, "SuccessTurnedPartial", 0)]
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

        (m.PartialMatch, m.Index, m.Length).Should().Be(end < 0 ? (false, 0, 0) : (true, 0, end));
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
    // Row Q3 itself, moved from OpenDefectTests on 2026-10-18: no witness of the taint, as above,
    // but it guards the design row. It passed through S57d before D11 and
    // has held through its deletion by D11 (F54, merge 65265cfd, 2026-10-04; measured 2026-10-18).
    // Upstream answers the partial too.
    [Arguments("match", @"(?!a.(*PRUNE)b|a)a", true, 0)]
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

    /// <summary>
    /// The run-out rule at a choice point: when the test of one alternative runs out of text, the
    /// other alternatives are kept on the backtracking stack before the run-out is taken. Inside a
    /// negative lookahead the blocked end then backtracks into them, and a success of one of them
    /// on the held text fails the lookahead for every continuation. In the first four rows the
    /// tail's <c>(?&lt;=b)</c> holds after every 'ab' (F1 of the 2026-10-04 review: END_GREEDY_REPEAT
    /// took the body's run-out without the tail); then the same through GREEDY_REPEAT's body test,
    /// a bounded repeat, a one-character repeat (twice), a branch, END_LAZY_REPEAT's body test with
    /// its tail kept, and a one-character repeat that backtracks to a count whose tail runs out with
    /// a smaller count left. No row has a completion (the port's non-partial judge over every
    /// continuation of up to three characters, 2026-10-04).
    /// </summary>
    /// <remarks>
    /// Before, each row answered the partial at the attempt's start: the arm returned the partial
    /// at once or went to the run-out with its other alternatives dropped. Planted out (2026-10-04,
    /// Debug): without the kept alternative of the four multi-character arms the first six rows go
    /// red; without GREEDY_REPEAT_ONE's kept counts the next two; without the branch's kept entry
    /// the ninth; with END_LAZY_REPEAT's body test answering at once the tenth; without the
    /// backtracking arm's smaller counts the last (86 rows of a 5,824-row grid of bounded repeats
    /// before a string).
    /// </remarks>
    // DIVERGES FROM UPSTREAM on every row but the first four: regex 2026.9.10 answers the partial
    // at the start on the others, and None on the first four, where its END_GREEDY_REPEAT turns
    // the body's run-out at the end of the text into a failure.
    [Test]
    [Arguments(@"(?!(?:ab)*(?<=b))ab", "ab")]
    [Arguments(@"(?!(?:ab)*(?<=b))..", "ab")]
    [Arguments(@"(?!(?:ab)*(?<=b))", "ab")]
    [Arguments(@"(?!(?:ab)*(?<=b)).*", "ab")]
    [Arguments(@"(?!(?:ab)+)ab", "ab")]
    [Arguments(@"(?!(?:ab){1,3}(?<=b))ab", "ab")]
    [Arguments(@"(?!(?:a|b)*(?<=b))ab", "ab")]
    [Arguments(@"a(?!(?:b)*(?<=b))b", "ab")]
    [Arguments(@"(?!ab|a)", "a")]
    [Arguments(@"(?!(?:ab)*?c|ab)", "ab")]
    [Arguments(@"(?![ab]{0,2}aa)", "aa")]
    public void A_run_out_at_a_choice_point_keeps_the_other_alternatives(string pattern, string text) =>
        Run("match", pattern, text).Success.Should().BeFalse();

    /// <summary>
    /// A repeat's body or tail test that runs out of text inside a lookahead's body goes through the
    /// run-out rule like any other item: matching goes on after the lookahead, where
    /// <c>(*FAIL)</c> fails for every continuation. Each arm returned the partial at once instead
    /// (item 7 of the 2026-10-04 review): GREEDY_REPEAT's body test (the first two rows) and tail
    /// test, LAZY_REPEAT's body test (two rows) and tail test (two rows), END_GREEDY_REPEAT's tail
    /// test, END_LAZY_REPEAT's body test (two rows) and tail test. No row has a completion.
    /// </summary>
    /// <remarks>
    /// Planted out (2026-10-04, Debug): GREEDY_REPEAT's body test answering at once turns the first
    /// two rows red, END_LAZY_REPEAT's the two rows of its body test.
    /// </remarks>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 answers the partial at the start on every row but
    // the fifth, where it answers None too.
    [Test]
    [Arguments(@"(?=(?:ab)+$)(*FAIL)", "ab")]
    [Arguments(@"(?=(?:a\b)+)(*FAIL)", "a")]
    [Arguments(@"(?=x(?:ab)*cd)(*FAIL)", "xc")]
    [Arguments(@"(?=(?:ab)+?$)(*FAIL)", "ab")]
    [Arguments(@"(?=(?:.\b)+?$)(*FAIL)", "a ")]
    [Arguments(@"(?=x(?:ab)*?cd)(*FAIL)", "xc")]
    [Arguments(@"(?=(?:ab)+?(?:ab)c)(*FAIL)", "abab")]
    [Arguments(@"(?=x(?:ab)*cd)(*FAIL)", "xabc")]
    [Arguments(@"(?=(?:ab)*?c)(*FAIL)", "ab")]
    [Arguments(@"(?=(?:ab)+?c)(*FAIL)", "abab")]
    [Arguments(@"(?=x(?:ab)*?(?:ab)c)(*FAIL)", "xabab")]
    public void A_repeat_test_that_runs_out_in_a_body_goes_on_after_the_lookaround(string pattern, string text) =>
        Run("match", pattern, text).Success.Should().BeFalse();

    /// <summary>
    /// A positive lookahead whose outcome feeds a decision: its body's repeat runs out at the edge,
    /// and the tail's success on the held text then decides it, so the decision around it is taken
    /// for every continuation. Around it: a negative lookahead, a condition, an atomic group, a
    /// `(*PRUNE)` after it, and, from main's own phantoms, a branch and a one-character repeat. No
    /// row has a completion (the port's non-partial judge over continuations of up to three
    /// characters, 2026-10-04). Before, the run-out's blocked success came first, the decided
    /// success was taken as a repeat of it (the dedup), and the enclosing decision stayed open:
    /// P(0, 2) on the first five rows, where main and upstream answer None (the 2026-10-04
    /// pre-merge review, P1), and P on the last four, as on main and upstream.
    /// </summary>
    /// <remarks>
    /// The second pre-merge review (R1, R2) found the rule switched off by a group call or a
    /// capture read anywhere in the pattern; the last nine rows hold those shapes. Main and upstream
    /// answer None to them; the branch answered P(0, 2) at a48bc3cf. Planted out (Debug,
    /// 2026-10-04): with any call switching the rule off again, the five call rows go red; with the
    /// decided end's capture test taken pattern-wide again, the `(*PRUNE)` row with `(c)\1`.
    /// Three parts, each planted out (Debug): without the alternatives kept in a positive body that
    /// feeds a decision (<c>Matcher.RunOutFeedsADecision</c>) the condition row goes red; without
    /// the decided end (<c>Matcher.EndALookaroundInTheSecondPass</c>) the negative, atomic and
    /// branch rows; without the later verb acting (<c>Matcher.ActingEndsTheScopeAsTheSkippedVerbDid</c>)
    /// the `(*PRUNE)` row.
    /// </remarks>
    // DIVERGES FROM UPSTREAM on the last four rows, where regex 2026.9.10 answers the partial.
    [Test]
    [Arguments(@"(?!(?=(?:ab)*(?<=b)))ab", "ab")]
    [Arguments(@"(?!(?=(?:ab)*(?<=b)))", "ab")]
    [Arguments(@"(?(?=(?:ab)*(?<=b))x|ab)", "ab")]
    [Arguments(@"(?>(?=(?:ab)*(?<=b))|ab)x", "ab")]
    [Arguments(@"(?=(?:ab)*(?<=b))(*PRUNE)x|ab", "ab")]
    [Arguments(@"(?!(?=ab|a))a", "a")]
    [Arguments(@"(?(?=ab|a)x|a)", "a")]
    [Arguments(@"a(?!(?=b*(?<=b)))b", "ab")]
    [Arguments(@"(?!(?=[ab]*(?<=b)))ab", "ab")]
    [Arguments(@"(?!(?=(?:ab)*(?<=b)))ab|(c)(?1)", "ab")]
    [Arguments(@"(?!(?:ab)*(?<=b))ab|(c)(?1)", "ab")]
    [Arguments(@"(?>(?=(?:ab)*(?<=b))|ab)x|(c)(?1)", "ab")]
    [Arguments(@"(?(?=(?:ab)*(?<=b))x|ab)|(c)(?1)", "ab")]
    [Arguments(@"(?!(?1))ab((?=(?:ab)*(?<=b)))?", "ab")]
    [Arguments(@"(?>(?=(?:ab)*(?<=b))|ab)x|(c)\1", "ab")]
    [Arguments(@"(?!(?=(?:ab)*(?<=b)))ab|(c)\1", "ab")]
    [Arguments(@"(?=(?:ab)*(?<=b))(*PRUNE)x|ab|(c)\1", "ab")]
    [Arguments(@"(?!(?=(?:ab)*(?<=b)))ab(?(1)y)()", "ab")]
    public void A_positive_lookahead_decided_on_the_held_text_decides_what_encloses_it(string pattern, string text) =>
        Run("match", pattern, text).Success.Should().BeFalse();

    /// <summary>
    /// A lookahead in a repeat inside an atomic group (or a possessive repeat) holds at 0 for
    /// every continuation, since the tail's lookbehind succeeds on the held text, so the group
    /// commits to its first iteration and <c>x</c> then fails: no continuation completes the match
    /// (the port's non-partial judge, 2026-10-04). The run-out of the lookahead's own repeat now
    /// waits until the body's other paths have been tried (<c>Matcher.RunOutCanWaitForTheBody</c>):
    /// run out first, the repeat's second copy of the lookahead was decided while the first was
    /// still blocked, so the group never committed and its 'ab' branch answered P(0, 2) (the second
    /// pre-merge review, R3; main and upstream answer None). The last two rows have a verb outside
    /// the lookahead, which cannot reach its body's paths.
    /// </summary>
    /// <remarks>
    /// Planted out (Debug, 2026-10-04): without the waiting run-out, 5 of the first 9 rows go red
    /// (P(0, 2)); with the verb test taken pattern-wide again (as before the body's
    /// <c>HasVerbOrCall</c>), the last two.
    /// </remarks>
    // DIVERGES FROM UPSTREAM: none; regex 2026.9.10 answers None too.
    [Test]
    [Arguments(@"(?>(?:(?=(?:ab)*(?<=b))|ab)+)x")]
    [Arguments(@"(?>(?:(?=(?:ab)*(?<=b))|ab)*)x")]
    [Arguments(@"(?>(?:(?=(?:ab)*(?<=b))|ab)?)x")]
    [Arguments(@"(?>(?:(?=(?:ab)*(?<=b))|ab){2})x")]
    [Arguments(@"(?:(?=(?:ab)*(?<=b))|ab)++x")]
    [Arguments(@"(?:(?=(?:ab)*(?<=b))|ab)*+x")]
    [Arguments(@"(?:(?=(?:ab)*(?<=b))|ab)?+x")]
    [Arguments(@"(?:(?=(?:ab)*(?<=b))|ab){1,2}+x")]
    [Arguments(@"(?>(?:(?=(?:ab)*(?<!b))|ab)+)x")]
    [Arguments(@"(?>(?:(?=(?:ab)*(?<=b))|ab)+)x(*PRUNE)")]
    [Arguments(@"(?>(?:(?=(?:ab)*(?<=b))|ab)+)x|(*PRUNE)q")]
    public void A_lookahead_repeated_inside_an_atomic_group_is_decided_before_the_group_commits(string pattern) =>
        Run("match", pattern, "ab").Success.Should().BeFalse();

    /// <summary>
    /// D11-R1: a repeat's test that runs out inside a lookahead's body goes through the run-out
    /// rule, so matching goes on after the lookahead, where <c>[xy]</c> fails on 'a' for every
    /// continuation. The test returned the partial at once before the repeat run-out rule; main
    /// and upstream answer P(0, 2).
    /// </summary>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 answers P(0, 2).
    [Test]
    public void A_repeat_test_that_runs_out_in_a_lookahead_does_not_answer_for_what_follows() =>
        Run("match", @"(?=(?:(a))*b)[xy]", "aa").Success.Should().BeFalse();

    /// <summary>
    /// The sound side of the rule's limit: an atomic group around the repeat commits to the run-out's
    /// longer path in the continuations where it exists, so the held text's success after it is not
    /// certain, and the lookahead stays undetermined. Here 'ab' + 'ab' completes the match: the
    /// group takes 'abab', `(?&lt;=^ab)` then fails, so the lookahead passes. Upstream 2026.9.10 and
    /// main answer None, a refused completion. The <c>(?&lt;=b)</c> form of the same pattern, which
    /// no continuation completes, answers the same partial: limit C2, in
    /// <c>PartialContractTests.A_run_out_presumes_the_rest_of_the_pattern_can_match</c>.
    /// </summary>
    /// <remarks>
    /// The second row is the same one lookahead deeper (D11 pre-merge review, P1): a possessive
    /// repeat in a positive lookahead inside the negative one; 'ab' + 'ab' completes it too. Its
    /// <c>(?&lt;=b)</c> form is limit C2, in
    /// <c>PartialContractTests.A_run_out_presumes_the_rest_of_the_pattern_can_match</c>.
    /// </remarks>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 answers None to both rows.
    [Test]
    [Arguments(@"(?!(?>(?:ab)*)(?<=^ab))ab")]
    [Arguments(@"(?!(?=(?:ab)*+(?<=^ab)))ab")]
    public void An_atomic_group_around_a_repeat_that_runs_out_keeps_the_lookahead_undetermined(string pattern)
    {
        Match m = Run("match", pattern, "ab");

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
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
