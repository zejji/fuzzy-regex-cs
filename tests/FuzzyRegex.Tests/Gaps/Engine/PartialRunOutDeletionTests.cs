using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D124: a fuzzy item that runs out of text inside a negative lookaround's body, or a condition's
/// test, keeps its deletion, an error that needs no text (<c>Matcher.KeepTheDeletionOfAnItemThatRanOut</c>).
/// </summary>
/// <remarks>
/// <para>
/// A body that matches the empty string by deleting its items, such as <c>(?:x){d&lt;=1}</c>,
/// matches whatever follows the edge, so the negative lookaround fails for every continuation.
/// The run-out went on undecided and the deletion was never tried, so the run-out before the
/// lookaround was answered as a partial that no text completes: <c>(x)(?!(?:x){d&lt;=1})</c> over
/// 'xx' was P(1, 2), as upstream 2026.9.29 answers, where <c>(x)(?!x?)</c>, whose body also
/// matches empty, gives the search's edge partial P(2, 2).
/// </para>
/// <para>
/// The expected answers are the port's own: its non-partial answers over every continuation of up
/// to three characters (<c>.scratch/s1024/d124/g/judge.cs</c>), or the search's edge partial
/// (P(n, n) forward, P(0, 0) reversed) where no continuation completes, which the partial
/// contract allows. The comment on each row gives main's answer before the fix.
/// </para>
/// </remarks>
public sealed class PartialRunOutDeletionTests
{
    /// <summary>One witness per kind of item, forward and reversed.</summary>
    /// <param name="kind">The item kind the row reaches.</param>
    /// <param name="operation">search, match or fullmatch.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The text.</param>
    /// <param name="start">The partial's start, or -1 for no match.</param>
    /// <param name="end">The partial's end.</param>
    [Test]
    // The register's witness. Main: P(1, 2).
    [Arguments("character", "search", "(x)(?!(?:x){d<=1})", "xx", 2, 2)]
    // Main: P(0, 1) on every row down to the folded ones.
    [Arguments("character", "match", "(x)(?!(?:x){d<=1})", "x", -1, -1)]
    [Arguments("character", "fullmatch", "(x)(?!(?:x){d<=1})y", "x", -1, -1)]
    [Arguments("character, reversed", "search", "(?r)(?<!(?:x){d<=1})(x)", "x", 0, 0)]
    [Arguments("any", "search", "(x)(?!(?:.){d<=1})", "x", 1, 1)]
    [Arguments("set", "search", "(x)(?!(?:[xy]){d<=1})", "x", 1, 1)]
    [Arguments("property", "search", @"(x)(?!(?:\w){d<=1})", "x", 1, 1)]
    [Arguments("string", "search", "(x)(?!(?:xx){d<=2})", "x", 1, 1)]
    [Arguments("string, reversed", "search", "(?r)(?<!(?:xx){d<=2})(x)", "x", 0, 0)]
    [Arguments("string ignoring case", "match", "(?i)(x)(?!(?:xx){d<=2})", "x", -1, -1)]
    [Arguments("reference", "search", @"(x)(?!(?:\1){d<=1})", "x", 1, 1)]
    [Arguments("reference, reversed", "search", @"(?r)(?<!(?:\1){d<=1})(x)", "x", 0, 0)]
    [Arguments("condition's test", "search", "(x)(?(?!(?:x){d<=1})|(?!))", "x", 1, 1)]
    // Full case folding: the arm re-enters at the edge with an empty folding.
    [Arguments("folded string", "search", "(?fi)(x)(?!(?:ss){d<=2})", "x", 1, 1)]
    [Arguments("folded string, reversed", "search", "(?rfi)(?<!(?:ss){d<=2})(x)", "x", 0, 0)]
    [Arguments("folded string", "match", "(?fi)(x)(?!(?:ß){d<=2})", "x", -1, -1)]
    // Main: P(0, 2).
    [Arguments("folded reference", "match", @"(?fi)(ss)(?!(?:\1){d<=2})", "ss", -1, -1)]
    [Arguments("folded reference, reversed", "match", @"(?rfi)(?<!(?:\1){d<=2})(ss)", "ss", -1, -1)]
    // A captured ß is one deletion (D24's whole deletion of a group character). Main: P(1, 2) and
    // P(0, 1); 'ßß' + 'ssz' completes the search at 2.
    [Arguments("folded reference, whole character", "search", @"(?fi)(ß)(?!(?:\1){d<=1})", "ßß", 2, 2)]
    [Arguments("folded reference, whole character, reversed", "search", @"(?rfi)(?<!(?:\1){d<=1})(ß)", "ßß", 0, 0)]
    // Part way through a group character's folding: the first s of the ß deleted, then the second.
    // Main: P(1, 2).
    [Arguments("folded reference, inside a folding", "search", @"(?fi)(ß)(?!(?:\1){d<=2})", "ßß", 2, 2)]
    // The text ends after the group's ß, before its x: the x is the character the arm stands on,
    // not yet folded (the ß's folding is used up). 'ßxß' + 'xz' completes at 2. Main: P(0, 3).
    [Arguments("folded reference, between characters", "search", @"(?fi)(ßx)(?!(?:\1){d<=1})", "ßxß", 2, 3)]
    // The group's s is used up part way through the subject's ß, so the leftovers loop runs; it
    // never runs out, and a Debug build asserts that no run-out reaches the kept deletion with the
    // group used up (blind review of 9d17988f).
    [Arguments("folded reference, leftovers", "search", @"(?fi)(s)(?!(?:\1){d<=1})", "sß", 2, 2)]
    public void A_negative_body_that_deletes_its_way_to_empty_decides_at_the_edge(
        string kind,
        string operation,
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        _ = kind;
        var regex = new FuzzyRegex(pattern);
        Match m = operation switch
        {
            "match" => regex.MatchAtStart(subject, partial: true),
            "fullmatch" => regex.FullMatch(subject, partial: true),
            _ => regex.Match(subject, partial: true),
        };

        if (start < 0)
        {
            m.Success.Should().BeFalse();
            return;
        }

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, start, end));
    }

    /// <summary>
    /// Controls, which answered the same before the fix: the non-fuzzy twin, a body whose budget
    /// cannot empty it (the kept deletion spends the budget, so the text to come still decides),
    /// and the same pattern without <c>partial</c>.
    /// </summary>
    [Test]
    public void The_kept_deletion_keeps_to_the_budget_and_leaves_the_rest_alone()
    {
        // The twin without the fuzzy section, which the fixed answer now equals.
        Match twin = new FuzzyRegex("(x)(?!x?)").Match("xx", partial: true);
        (twin.Success, twin.PartialMatch, twin.Index, twin.Length).Should().Be((true, true, 2, 0));

        // One deletion leaves the y: 'x' + 'z' completes at 0.
        Match budget = new FuzzyRegex("(x)(?!(?:xy){d<=1})z").Match("x", partial: true);
        (budget.Success, budget.PartialMatch, budget.Index, budget.Length).Should().Be((true, true, 0, 1));

        // The same in a folded string: 'x' + 'z' completes at 0.
        Match folded = new FuzzyRegex("(?fi)(x)(?!(?:ss){d<=1})z").Match("x", partial: true);
        (folded.Success, folded.PartialMatch, folded.Index, folded.Length).Should().Be((true, true, 0, 1));

        new FuzzyRegex("(x)(?!(?:x){d<=1})").Match("xx").Success.Should().BeFalse();
    }

    /// <summary>
    /// In a negative lookaround or a negative test, the deletion is tried before the run-out, so
    /// a body that deletes its way to empty decides the construct without the run-out's cycle, and
    /// an answer main already had costs no more than main's steps. The bounds are main's counts,
    /// measured on a16e35a0; with the run-out first they were 32 and 26.
    /// </summary>
    /// <param name="operation">search or fullmatch.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="steps">Main's step count, which the call may not exceed.</param>
    /// <param name="start">The partial's start, or -1 for no match.</param>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("fullmatch", "(x)(?!(?:xx){d<=2})", 27, -1)]
    [Arguments("search", "(x)(?(?!(?:x){d<=1})a|b)", 25, 0)]
    public void A_negative_body_tries_its_deletion_before_the_run_out(
        string operation,
        string pattern,
        long steps,
        int start
    )
    {
        var regex = new FuzzyRegex(pattern);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = string.Equals(operation, "fullmatch", StringComparison.Ordinal)
                    ? regex.FullMatch("x", partial: true)
                    : regex.Match("x", partial: true);
                if (start < 0)
                {
                    m.Success.Should().BeFalse();
                }
                else
                {
                    (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, start, 1));
                }
            },
            steps,
            "the deletion decides the construct before the run-out is taken"
        );
    }

    /// <summary>
    /// The deletion goes first only where nothing in the body can throw away the run-out kept
    /// beneath it. In the inner negative lookahead an atomic group is open around the item that
    /// runs out ('y' at the edge). Taken first, the deletion passes the atomic group's end
    /// undisturbed, so the group commits and discards the pending run-out. The lookbehind then
    /// decides the inner body fails, and the outer lookahead fails for every continuation. But
    /// 'ax' + 'y' makes the inner body match ('xy', and the lookbehind sees 'y'), so the inner
    /// lookahead fails and the outer one holds: (0, 1) completes there. The answer is P(0, 2),
    /// the judge's. With the guard off, a mutation gives None, a miss. The blind review's own row,
    /// `a(?!(?>(?:xy){d&lt;=1})(?&lt;!x))` over 'ax', is a complete match either way, because the
    /// held text decides it in the first pass.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    [Test]
    [Arguments(@"a(?!(?!(?>(?:xy){d<=1})(?<!x)))")]
    [Arguments(@"a(?!(?!(?>(?:xy){d<=1})(?<=y)))")]
    public void A_cut_in_the_body_keeps_the_run_out_first(string pattern)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("ax", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 2));
    }

    /// <summary>
    /// The certifier: where the rest of a negative body cannot be deleted within the budget, or
    /// holds an item outside every fuzzy section past the item's own section, the deletion is not
    /// kept and the run-out is taken at once, at main's cost. The bounds are main's counts
    /// (a16e35a0); the deletion first cost 32 and 36.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="steps">Main's step count, which the call may not exceed.</param>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("(x)(?!(?:xx){d<=1})y", 25)]
    [Arguments("(x)(?!(?:x){d<=1}y)y", 28)]
    public void A_deletion_that_cannot_empty_the_body_is_not_kept(string pattern, long steps)
    {
        var regex = new FuzzyRegex(pattern);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match("x", partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 1));
            },
            steps,
            "the deletion holds no success of the body"
        );
    }

    /// <summary>
    /// A deletion that cannot pass an enclosing section of the body is not kept (OPT.md C1): the
    /// outer <c>{i&lt;=1}</c> allows no deletion, so the inner deletion fails at the outer
    /// END_FUZZY on every path, and keeping it only walked to that failure. The bound is main's
    /// count (a16e35a0); keeping it cost 116.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_deletion_an_enclosing_section_of_the_body_forbids_is_not_kept()
    {
        var regex = new FuzzyRegex(@"(?(?!(?:(?:x){d<=1}){i<=1})a|b)\Z");

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match("xxy", partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 3, 0));
            },
            110,
            "the deletion cannot pass the outer section"
        );
    }

    /// <summary>
    /// The guards of round 6's prunes (OPT.md, REVIEW-1000), each row a16e35a0's or a9082250's
    /// answer. C1: a verb between the inner and the outer END_FUZZY (the review's counterexample,
    /// which measured None even with the guard dropped), an outer section that allows a deletion,
    /// and a consuming item before the inner END_FUZZY keep the deletion. B1: inside a fuzzy
    /// section the counts bound the section, and under BESTMATCH they rank the matches, so they
    /// stay in the dedup key (the review's counterexample is the first B1 row).
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The text.</param>
    /// <param name="search">Whether the call searches, rather than matches at the start.</param>
    /// <param name="start">The partial's start, or -1 for no match.</param>
    /// <param name="end">The partial's end.</param>
    [Test]
    [Arguments(@"(?:(?(?=y(?:(?:x){d<=1}(*PRUNE)){i<=1}|y)|(?!))){1<=s<=1}", "y", false, -1, -1)]
    [Arguments(@"(x)(?!(?:(?:x){d<=1}){e<=1})", "xx", true, 2, 2)]
    [Arguments(@"(x)(?!(?:(?:x){d<=1}y){i<=1})", "xx", true, 0, 1)]
    [Arguments(@"(?:(?=zy|xy)q){s<=1}", "x", false, 0, 1)]
    [Arguments(@"(?b)x(?(?=(?:xx){e<=2})(?!)|)\Z", "x", true, 1, 1)]
    public void A_guard_row_keeps_its_answer(
        string pattern,
        string subject,
        bool search,
        int start,
        int end
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = search ? regex.Match(subject, partial: true) : regex.MatchAtStart(subject, partial: true);

        (start < 0 ? (m.Success, -1, -1) : (m.Success, m.Index, m.Index + m.Length))
            .Should()
            .Be((start >= 0, start, end));
    }

    /// <summary>
    /// The dedup ignores fuzzy counts outside every fuzzy section (OPT.md B1): a later blocked
    /// success of the same positive test, with the same captures, has the same continuation
    /// whatever errors its body spent, since nothing after the test reads top-level counts in a
    /// call without BESTMATCH or ENHANCEMATCH. The second blocked yes-run after the first deletion
    /// is skipped. The bound is a9082250's count less the skipped run (69 before).
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_blocked_success_that_differs_only_in_counts_is_not_continued_again()
    {
        var regex = new FuzzyRegex(@"x(?(?=(?:xx){e<=2})(?!)|)\Z");

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match("x", partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 1, 0));
            },
            63,
            "the second continuation differs from the first only in its counts"
        );
    }

    /// <summary>
    /// The certifier keeps a deletion it cannot rule out. The rest of this body lies in the
    /// enclosing section, whose budget deletes it (the inner deletion counts there too, so the
    /// outer section allows two), so a count against the inner budget alone would skip a choice
    /// the body needs. 'xx' + anything: the body deletes both x, the lookahead fails everywhere,
    /// and the search's answer is the edge.
    /// </summary>
    [Test]
    public void A_deletion_the_enclosing_section_completes_is_kept()
    {
        Match m = new FuzzyRegex("(x)(?!(?:(?:x){d<=1}x){d<=2})").Match("xx", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 2, 0));
    }

    /// <summary>
    /// The frame drop: in a negative lookaround a run-out only marks the body undetermined, so a
    /// pending run-out beneath a deletion whose path has already taken one (the y after
    /// <c>\b</c> runs out) is dropped. The answer is the same and the drop is counted in a Debug
    /// build.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_second_run_out_in_an_undetermined_negative_body_is_dropped()
    {
        var regex = new FuzzyRegex(@"(x)(?!(?:x){d<=1}\by)y");
        Match m = regex.Match("x", partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 1));

        using MatchState state = MatchState.Create(
            regex.PatternObject,
            "x".AsMemory(),
            0,
            1,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );
        _ = Matcher.DoMatch(state, search: true);

        EngineWork.SkipUnlessCounting();
        state.PartialEventCount(PartialEvent.RunOutPendingDropped).Should().Be(1);
    }

    /// <summary>
    /// A positive test whose decided success comes after a blocked one fails at once, without
    /// running its yes-branch again, where nothing around the test reads the decision (OPT.md (P)).
    /// The bound is main's count (a16e35a0); running the branch again cost 35.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_positive_test_decided_after_a_blocked_success_does_not_run_its_branch_again()
    {
        var regex = new FuzzyRegex("(x)(?(?=(?:x){d<=1})(?!)|)");

        EngineWork.ShouldTakeAtMostSteps(
            () => regex.FullMatch("x", partial: true).Success.Should().BeFalse(),
            33,
            "the blocked run of the yes-branch already failed"
        );
    }

    /// <summary>
    /// Each boundary of (P): the answer and counts are the order-preserving engine's (fix/d124 at
    /// 592719c9, equal to 16c77967 on the gate), and the skip fires only where the argument holds.
    /// It fires through an enclosing repeat, whose later iterations the blocked run also ran, and
    /// with a <c>\K</c> in the body, which moves only the start of a run that is never reported. It
    /// does not fire when any of these hold:
    /// <list type="bullet">
    /// <item><description>the test lies in a fuzzy section;</description></item>
    /// <item><description>an atomic group or a negative lookahead encloses it;</description></item>
    /// <item><description>its body sets a group the pattern reads;</description></item>
    /// <item><description>the pattern has a fuzzy minimum;</description></item>
    /// <item><description>the pattern has BESTMATCH or ENHANCEMATCH;</description></item>
    /// <item><description>the pattern has a verb.</description></item>
    /// </list>
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The text.</param>
    /// <param name="search">Whether the call searches, rather than matches at the start.</param>
    /// <param name="start">The partial's start, or -1 for no match.</param>
    /// <param name="end">The partial's end.</param>
    /// <param name="skipped">Whether the skip fires.</param>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?:x(?(?=(?:y){d<=1})(?!)|w))+v", "xx", true, 2, 2, true)]
    [Arguments(@"(?:x(?(?=(?:y){d<=1})(?!)|w))+v", "x", false, -1, -1, true)]
    [Arguments(@"x(?(?=(?:y){d<=1}\K)(?!)|w)", "x", true, 1, 1, true)]
    [Arguments(@"(?:x(?(?=(?:y){d<=1})(?!)|w)){e<=1}", "x", true, 0, 1, false)]
    [Arguments(@"(?>x(?(?=(?:y){d<=1})(?!)|w)v?)", "x", true, 1, 1, false)]
    [Arguments(@"x(?(?=((?:y){d<=1}))(?!)|w)\1", "x", true, 1, 1, false)]
    [Arguments(@"x(?(?=(?:y){d<=1})(?!)|w)(?:v){1<=e<=1}", "x", true, 1, 1, false)]
    [Arguments(@"(?b)(?:x(?(?=(?:y){d<=1})(?!)|w))+v", "xx", true, 2, 2, false)]
    [Arguments(@"(?e)(?:x(?(?=(?:y){d<=1})(?!)|w))+v", "xx", true, 2, 2, false)]
    [Arguments(@"x(?(?=(?:y){d<=1})(?!)|w)(*PRUNE)", "x", true, 0, 1, false)]
    public void The_decided_rerun_is_skipped_only_where_nothing_reads_it(
        string pattern,
        string subject,
        bool search,
        int start,
        int end,
        bool skipped
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = search ? regex.Match(subject, partial: true) : regex.MatchAtStart(subject, partial: true);
        if (start < 0)
        {
            m.Success.Should().BeFalse();
        }
        else
        {
            (m.Success, m.PartialMatch, m.Index, m.Index + m.Length, m.FuzzyCounts.Total)
                .Should()
                .Be((true, true, start, end, 0));
        }

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
        _ = Matcher.DoMatch(state, search);

        EngineWork.SkipUnlessCounting();
        (state.PartialEventCount(PartialEvent.DecidedRerunSkipped) != 0).Should().Be(skipped);
    }

    /// <summary>
    /// A positive lookaround whose only enclosing constructs are commit-neutral atomic groups keeps
    /// no deletion: a decided success would let the group commit, but nothing in the group outside
    /// the lookaround can leave an alternative, so the commit discards no path (OPT.md (V)). The
    /// bound is main's count (a16e35a0); the kept deletion cost 35.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_lookaround_whose_decision_nothing_reads_keeps_no_deletion()
    {
        var regex = new FuzzyRegex("(x)(?>(?=(?:x+){d<=1}))y");

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match("x", partial: true);
                (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 1));
            },
            30,
            "the atomic group's commit discards nothing"
        );
    }

    /// <summary>
    /// Each kind of node that can leave an alternative inside the atomic group makes the commit
    /// matter, so the deletion is kept there: an alternation, an optional or a lazy repeat, a
    /// possessive repeat (an atomic group around a repeat), a fuzzy item, and a second lookaround.
    /// The first row is the neutral control, where none is kept.
    /// </summary>
    /// <param name="pattern">The pattern, searched over 'x'.</param>
    /// <param name="kept">Whether the deletion is kept.</param>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("(x)(?>(?=(?:x){d<=1}))y", false)]
    [Arguments("(x)(?>(?=(?:x){d<=1})|y)z", true)]
    [Arguments("(x)(?>(?=(?:x){d<=1})x?)z", true)]
    [Arguments("(x)(?>(?=(?:x){d<=1})x*?)z", true)]
    [Arguments("(x)(?:(?=(?:x){d<=1})x?)++z", true)]
    [Arguments("(x)(?>(?=(?:x){d<=1})(?:y){e<=1})z", true)]
    [Arguments("(x)(?>(?=(?:x){d<=1})(?=y?))z", true)]
    public void A_choice_point_in_the_atomic_group_keeps_the_deletion(string pattern, bool kept)
    {
        var regex = new FuzzyRegex(pattern);
        using MatchState state = MatchState.Create(
            regex.PatternObject,
            "x".AsMemory(),
            0,
            1,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );
        _ = Matcher.DoMatch(state, search: true);

        EngineWork.SkipUnlessCounting();
        (state.PartialEventCount(PartialEvent.RunOutKeptDeletion) != 0).Should().Be(kept);
    }

    /// <summary>
    /// A reference to a capture a run-out left unknown runs out where it stands, away from the
    /// edge, and keeps no deletion: it presumes the rest of the text (build chunk 8). The lookahead
    /// at 1 runs out with <c>a+</c> wanting more, so group 1 is unknown in the negative lookahead.
    /// </summary>
    [Test]
    public void A_reference_to_an_unknown_capture_keeps_no_deletion()
    {
        Match m = new FuzzyRegex(@"x(?=(a+))(?!(?:\1){d<=1}q)b").Match("xa", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 2, 0));
    }
}
