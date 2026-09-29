using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 M2, build chunk 4: the cut rule for atomic groups, possessive repeats and <c>\X</c>
/// (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, section 4.2, and the witnesses of section
/// 7). In a partial call's second pass such a group cuts only when no mark lies above its frame;
/// otherwise it keeps its body's other paths, taints its frame and leaves a <c>Reopen</c> entry.
/// </summary>
/// <remarks>
/// Each expected answer is the brute-force judge's, <c>tools/probes/d11-brute-judge.py</c> (upstream
/// <c>regex</c> 2026.9.10 without partial, over every continuation of one to four characters),
/// measured 2026-09-29, except where a remark says otherwise. S57d's escalation turns a failed second
/// pass into a partial whenever a word or grapheme test stood at the edge, until build chunk 9
/// deletes it. So the witnesses here read the text to come through <c>$</c>, which S57d never notes,
/// or show that the answer came from a path: a group the path set, which S57d clears, or, in a
/// Debug build, the count of second-pass successes.
/// </remarks>
public sealed class PartialCutRuleTests
{
    /// <summary>
    /// Rows D1-D3 with <c>$</c> for the read: 'x' + ' ' fails the <c>$</c>, the group then takes
    /// its other path, and the match completes. With the group's cut taken, the second pass finds
    /// nothing.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to all three.
    [Test]
    [Arguments(@"(?>x$|)(?<!x)x", "x", 1)]
    [Arguments(@"(?>ab$|a)(?<=a)b", "ab", 2)]
    [Arguments(@"(?:a$)?+(?<!a)a", "a", 1)]
    public void A_cut_after_a_read_of_the_text_to_come_keeps_the_other_paths(string pattern, string text, int end)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, end));
    }

    /// <summary>
    /// The design's own rows D1-D3, whose read is a <c>\b</c>. S57d answers them with the same span
    /// without the cut rule; with it, the second pass reaches the end of the pattern.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to all three.
    [Test]
    [Arguments(@"(?>x\b|)(?<!x)x", "x", 1)]
    [Arguments(@"(?>ab\b|a)(?<=a)b", "ab", 2)]
    [Arguments(@"(?:a\b)?+(?<!a)a", "a", 1)]
    public void The_design_rows_of_a_blocked_cut_are_second_pass_successes(string pattern, string text, int end)
    {
        using MatchState state = RunPartial(pattern, text);
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        state.PartialEventCount(PartialEvent.SuccessTurnedPartial).Should().Be(_one);
        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, end));
    }

    /// <summary>
    /// Row C3: after 'a' at the edge a combining mark can extend the cluster, so the grapheme test
    /// that ends <c>\X</c> is undetermined, the group's cut is blocked, and <c>\X</c> goes on to run
    /// out of text ('a' + U+0301 matches). A CR at the edge can be joined by an LF (GB3).
    /// </summary>
    /// <remarks>
    /// The group the path set is the witness: S57d answers the same span with no groups. The judge's
    /// alphabet holds no combining mark, so row C3 was measured by hand: upstream matches 'á'
    /// at (0, 2) and answers None to the partial.
    /// </remarks>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None to every row.
    [Test]
    [Arguments(@"()\X(?<!a)", "a")]
    [Arguments(@"()\X(?<!\r)", "\r")]
    public void A_grapheme_test_at_the_edge_blocks_the_cut_of_its_cluster(string pattern, string text)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
        (m.Groups[1].Success, m.Groups[1].Index).Should().Be((true, 0));
    }

    /// <summary>
    /// The reversed twin of row C3's CR case: an LF at the left edge can be joined by a CR to come.
    /// </summary>
    /// <remarks>
    /// The judge answers None, because upstream's reversed <c>\X</c> takes one codepoint (ledger
    /// entry 43); this port's own matcher matches '\r\n' at (0, 2), which is the completion.
    /// </remarks>
    [Test]
    public void A_reversed_grapheme_test_at_the_edge_blocks_the_cut_of_its_cluster()
    {
        using MatchState state = RunPartial(@"(?r)(?!\n)\X", "\n");
        Match m = new FuzzyRegex(@"(?r)(?!\n)\X").MatchAtStart("\n", partial: true);

        new FuzzyRegex(@"(?r)(?!\n)\X").MatchAtStart("\r\n").Length.Should().Be(2);
        state.PartialEventCount(PartialEvent.CutBlockedAtomic).Should().Be(_one);
        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    /// <summary>
    /// A grapheme test at the edge that every continuation decides the same way: after an LF, and
    /// under <c>(?r)</c> before a CR, a cluster always ends (GB4, GB5). The cut is taken, and no
    /// partial comes of it. Read as undetermined, each row would run <c>\X</c> out of text.
    /// </summary>
    [Test]
    [Arguments(@"()\X(?<!\n)", "\n")]
    [Arguments(@"(?r)(?!\r)\X", "\r")]
    public void A_grapheme_test_the_held_text_decides_takes_the_cut(string pattern, string text)
    {
        new FuzzyRegex(pattern).MatchAtStart(text, partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// Row N1 with <c>$</c>: once the group's first path has been backtracked out of, the second,
    /// <c>x</c>, reads nothing to come, so the group commits to it, and <c>(?&lt;!x)</c> fails for
    /// every continuation. The taint the blocked first end left at the frame must not block this
    /// cut; if it did, the empty path would run and the answer would be a phantom partial.
    /// </summary>
    // Upstream answers None too.
    [Test]
    public void A_group_whose_own_frame_is_tainted_still_cuts()
    {
        new FuzzyRegex(@"(?>x$|x|)(?<!x)x").MatchAtStart("x", partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// Row N1 itself, whose read is a <c>\b</c>: the second pass fails, as the judge's None says.
    /// S57d still escalates the failure to a partial until build chunk 9, so the Debug count of
    /// second-pass successes is the witness.
    /// </summary>
    [Test]
    public void The_design_row_of_a_tainted_frame_is_no_second_pass_success()
    {
        using MatchState state = RunPartial(@"(?>x\b|x|)(?<!x)x", "x");

        state.PartialEventCount(PartialEvent.SuccessTurnedPartial).Should().Be(0);
        state.PartialEventCount(PartialEvent.CutBlockedAtomic).Should().Be(_one);
    }

    /// <summary>
    /// The cut taint, T1's mechanism, in a shape the cut rule of this chunk decides alone: the inner
    /// group's first end is blocked, its empty path then ends unblocked, and the taint moves to its
    /// end entry. The outer group, reached back through the skipped cut, is then blocked too, and
    /// its second path runs out of text ('a' + '\n'). Counting reads alone would let the outer group
    /// cut, and the second pass would find nothing.
    /// </summary>
    /// <remarks>
    /// Row T1 itself needs a skipped <c>(*PRUNE)</c>, build chunk 5's: see
    /// <see cref="PartialVerbRuleTests.A_verb_reached_back_through_a_skipped_cut_does_nothing"/>.
    /// </remarks>
    // DIVERGES FROM UPSTREAM, deliberately: upstream answers None.
    [Test]
    public void A_path_reached_back_through_a_skipped_cut_blocks_the_next_cut()
    {
        Match m = new FuzzyRegex(@"(?>(?>a$|)(?<!a)a|a\n)(?<!a)").MatchAtStart("a", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    /// <summary>
    /// The port grid's counter (VERIFICATION rule 11): one blocked cut, and none in a call without
    /// partial. Debug builds only.
    /// </summary>
    [Test]
    public void A_blocked_cut_is_counted_in_a_debug_build()
    {
        using MatchState blocked = RunPartial(@"(?>x$|)(?<!x)x", "x");

        blocked.PartialEventCount(PartialEvent.CutBlockedAtomic).Should().Be(_one);
        new FuzzyRegex(@"(?>x$|)(?<!x)x").MatchAtStart("x").Success.Should().BeFalse();
    }

#if DEBUG
    private const long _one = 1;
#else
    private const long _one = 0;
#endif

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
