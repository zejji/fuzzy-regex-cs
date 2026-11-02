using System.Globalization;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D104, the cost of the D90 fix: in a partial call's second pass, a lookahead in a repeat whose
/// capture a later condition reads has several ways out at the end of the text, and each re-ran
/// the continuation, which holds every later lookahead. The search was exponential until the fork
/// budget stopped it, and the budget's partial at the attempt start is a phantom. Two changes on
/// top of option G (records kept under their instance, <c>PartialBlockedEndGuardTests</c>) end
/// it: the records stay for the whole pass instead of one start, and a continuation that failed
/// is itself recorded under a key of everything it reads (<c>MatchState.ContinuationKeyOf</c>).
/// </summary>
/// <remarks>
/// The expected answers are the same build's with every repeat guard off and no fork budget: guards
/// only prune, so that answer is the reference (<c>.scratch/s1019/d104-fast/REPORT.md</c>,
/// <c>.scratch/s1020/d104</c>). Steps are counted in Debug builds (D13), never timed. The answer is
/// written as <c>(start,end)</c>, a <c>p</c> for a partial match, then each group's captures.
/// </remarks>
public sealed class PartialContinuationMemoTests
{
    /// <summary>
    /// The shape option G could not reach. The third way out of the body is a real success, after
    /// which the lookahead is atomic and its body's entries are discarded, so no record is ever
    /// written inside it, and the outer repeat's body guard is off (D85). Only a record of the
    /// continuation stops the re-runs. Moved from <c>OpenDefectTests</c>: red before the
    /// continuation memo (each answered the budget's (0, 10), or (0, 10) for (9, 10)).
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="start">The partial match's expected start.</param>
    /// <param name="end">Its expected end.</param>
    [Test]
    [Arguments(@"(?:(?=(?:ab|a)*(b?)a?)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"(?:(?=(?:ab|a)*(b?)a?)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"(?:(?=(?:ab|a)*(b?)a?)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"a*(?:(?=(?:ab|a)*(b?)a?)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"b?(?:(?=(?:ab|a)*(b?)a?)a)*(?(1)c|d)", "aaaaaaaaab", 9, 10)]
    [Arguments(@"b?(?:(?=(?:ab|a)*(b?)a?)a)+(?(1)c|d)", "aaaaaaaaab", 9, 10)]
    [Arguments(@"b?(?:(?=(?:ab|a)*(b?)a?)a){2,}(?(1)c|d)", "aaaaaaaaab", 9, 10)]
    [Arguments(@"y?(?:(?=(?:ab|a)*(b?)a?)a)*(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"y?(?:(?=(?:ab|a)*(b?)a?)a)+(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"y?(?:(?=(?:ab|a)*(b?)a?)a){2,}(?(1)c|d)", "aaaaaaaaab", 10, 10)]
    [Arguments(@"(?:(?=(?:ab|a)*(b?)a?)(a))+(?(1)c|d)", "aaaaaaaaab", 10, 10)] // a group after it, not open around it (D122)
    public void A_lookahead_whose_real_success_discards_its_records_keeps_the_exact_partial(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject, partial: true);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length)
            .Should()
            .Be((start, end), "the fork budget's partial at the attempt start is the D104 phantom");
    }

    /// <summary>
    /// D122: the same shape with a capture group open around the lookahead, in every placement the
    /// 2026-10-31 grid found (<c>.scratch/s1024/d122</c>): closing after the 'a' or straight after
    /// the lookahead, nested, with <c>*</c>, <c>{2,}</c>, a lazy <c>+?</c>, an <c>a*</c> or a
    /// <c>b?</c> prefix, at 50 characters as at 9, and the review's W3, which opens the group before
    /// a variable prefix. The group's start sat on the save stack outside the key, so the memo was
    /// off, the pass forked until the budget stopped it, and every row answered the budget's partial
    /// from the attempt's start (moved from <c>OpenDefectTests</c>). The key now holds the start.
    /// </summary>
    /// <remarks>
    /// The expected answers are argued, and hold for every n: q always takes part, so the last 'a'
    /// the loop takes must be followed by a 'c', and every position the loop can stop at inside
    /// 'a' * n + 'b' holds an 'a' or the 'b'. So no anchored call has a completion, and the search's
    /// least completable start is the edge (text to come 'ac'), or the 'b' when <c>b?</c> can take
    /// it. A zero-iteration <c>*</c> needs a 'd' where the text has an 'a', so it adds no start. The
    /// grid's brute-force judge agrees, as does the port with every guard and the fork budget off.
    /// Upstream 2026.9.29 answers (0, n + 1) partial on every row and on the no-group controls too.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="n">How many 'a's come before the 'b'.</param>
    /// <param name="searchStart">The search's expected partial start; it ends at the edge.</param>
    [Test]
    [Arguments(@"((?=(?:ab|a)*(b?)a?)a)+(?(2)c|d)", 9, 10)]
    [Arguments(@"((?=(?:ab|a)*(?P<q>b?)a?)a)*(?(q)c|d)", 9, 10)]
    [Arguments(@"((?=(?:ab|a)*(?P<q>b?)a?)a){2,}(?(q)c|d)", 9, 10)]
    [Arguments(@"((?=(?:ab|a)*(?P<q>b?)a?)a)+?(?(q)c|d)", 9, 10)]
    [Arguments(@"(?:((?=(?:ab|a)*(?P<q>b?)a?))a)+(?(q)c|d)", 9, 10)]
    [Arguments(@"(?:((?=(?:ab|a)*(?P<q>b?)a?)a))+(?(q)c|d)", 9, 10)]
    [Arguments(@"(((?=(?:ab|a)*(?P<q>b?)a?)a))+(?(q)c|d)", 9, 10)]
    [Arguments(@"(a?(?=(?:ab|a)*(?P<q>b?)a?)a)+(?(q)c|d)", 9, 10)]
    [Arguments(@"a*((?=(?:ab|a)*(?P<q>b?)a?)a)+(?(q)c|d)", 9, 10)]
    [Arguments(@"b?((?=(?:ab|a)*(?P<q>b?)a?)a)+(?(q)c|d)", 9, 9)]
    [Arguments(@"((?=(?:ab|a)*(?P<q>b?)a?)a)+(?(q)c|d)", 50, 51)]
    [Arguments(@"b?(?:((?=(?:ab|a)*(?P<q>b?)a?)a))*(?(q)c|d)", 9, 9)]
    [Arguments(@"a*(?:((?=(?:ab|a)*(?P<q>b?)a?)a)){2,}(?(q)c|d)", 9, 10)]
    [Arguments(@"b?(((?=(?:ab|a)*(?P<q>b?)a?)a)){2,}(?(q)c|d)", 9, 9)]
    [Arguments(@"a*(((?=(?:ab|a)*(?P<q>b?)a?)a))*(?(q)c|d)", 9, 10)]
    [Arguments(@"b?(a?(?=(?:ab|a)*(?P<q>b?)a?)a)*(?(q)c|d)", 9, 9)]
    [Arguments(@"a*(a?(?=(?:ab|a)*(?P<q>b?)a?)a){2,}(?(q)c|d)", 9, 10)]
    [Arguments(@"a*(a*(?:(?=(?:ab|a)*(?P<q>b?)a?)a)+)(?(q)c|d)", 9, 10)] // review W3: opens before a variable prefix
    [Arguments(@"(?|((?=(?:ab|a)*(b?)a?)a)|x)+(?(2)c|d)", 9, 10)] // blind review: branch reset around the group
    [Arguments(@"((?=(?:ab|a)*(b?)a?)a)+(?=(?:ab|a)*b?a?)a*(?(2)c|d)", 9, 10)] // two keyed lookaheads, one with no group open
    [Arguments(@"(a(?=(?:ab|a)*(b?)a?)(?:a|ab))+(?(2)c|d)", 9, 1)] // the lookahead's body ends again after a retry; right without the fix too
    public void A_group_open_around_the_lookahead_keys_its_start_and_keeps_the_exact_partial(
        string pattern,
        int n,
        int searchStart
    )
    {
        var regex = new FuzzyRegex(pattern);
        string subject = new string('a', n) + "b";

        regex.MatchAtStart(subject, partial: true).Success.Should().BeFalse();
        Match m = regex.Match(subject, partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, searchStart, n + 1));
    }

    /// <summary>
    /// The same shape at 50 characters: each (lookahead, position, key) continuation fails once
    /// per pass. 102,173 steps (Debug, 2026-10-20); main and option G alone run to the budget and
    /// answer (0, 51), at 298,586 and 385,822 steps.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_recorded_continuation_keeps_the_real_success_shape_polynomial()
    {
        var regex = new FuzzyRegex(@"(?:(?=(?:ab|a)*(b?)a?)a)+(?(1)c|d)", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = new string('a', 50) + "b";

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(subject, partial: true);

                (m.PartialMatch, m.Index, m.Length).Should().Be((true, 51, 0));
            },
            300_000,
            "each continuation fails once per pass"
        );
    }

    /// <summary>
    /// D122: with a group open around the lookahead, the same order of work as without one, as the
    /// group's start is the lookahead's position, already in the key. 109,250 steps (Debug,
    /// 2026-11-01), against 102,173 without the group; before D122 the memo was off and the pass
    /// ran to the budget, 444,157 steps for the phantom (0, 51).
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_group_open_around_the_lookahead_keeps_the_real_success_shape_polynomial()
    {
        var regex = new FuzzyRegex(@"((?=(?:ab|a)*(b?)a?)a)+(?(2)c|d)", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = new string('a', 50) + "b";

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(subject, partial: true);

                (m.PartialMatch, m.Index, m.Length).Should().Be((true, 51, 0));
            },
            150_000,
            "each continuation fails once per pass, keyed by the group's start"
        );
    }

    /// <summary>
    /// D122, the review's cap question: the key that holds a group's start multiplies the keys
    /// where the start can take many values, as in W3, whose group opens before an <c>a*</c>, and
    /// the keys share <c>MatchState.CutBodyKeyCap</c> with the repeat records. At 20 characters W3
    /// peaks at 962 keys in 373,465 steps; at 64, 8,706 keys (13% of the cap) and no write skipped,
    /// in 38,658,877 steps against 35,569,057 for the phantom before (Debug, 2026-11-01). No write
    /// must be skipped here, or the cap is crowding out records (the peak is in
    /// <c>WorkCounter.CutBodyKeysPeak</c>, a per-thread maximum, so it is measured by probe, not here).
    /// Without the fix no key is written at all, so only the answer can go red then.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_group_opened_before_a_variable_prefix_stays_far_below_the_key_cap()
    {
        EngineWork.SkipUnlessCounting();
        var regex = new FuzzyRegex(
            @"a*(a*(?:(?=(?:ab|a)*(b?)a?)a)+)(?(2)c|d)",
            FuzzyRegexOptions.None,
            EngineWork.HangGuard
        );
        long skippedBefore = WorkCounter.CutBodyWritesSkipped;

        Match m = regex.Match(new string('a', 20) + "b", partial: true);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 21, 0));
        (WorkCounter.CutBodyWritesSkipped - skippedBefore).Should().Be(0);
    }

    /// <summary>
    /// The register's witness at 200 characters. There are about 2.6 n^2 distinct (key, position)
    /// states; emptied at every start, as upstream's <c>reset_guards</c> does, option G proved
    /// each again from each of the n starts, 23,628,606 steps. Kept for the pass: 671,969 steps
    /// (Debug, 2026-10-20), where main answers the budget's phantom (0, 201) in 1,113,322.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void Records_kept_for_the_whole_pass_keep_the_register_witness_quadratic()
    {
        var regex = new FuzzyRegex(@"(?:(?=(?:a|(b))*y)a)+(?(1)c|d)", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = new string('a', 200) + "b";

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                Match m = regex.Match(subject, partial: true);

                (m.PartialMatch, m.Index, m.Length).Should().Be((true, 201, 0));
            },
            1_500_000,
            "each state is proved once per pass, not once per start"
        );
    }

    /// <summary>
    /// The prototype's blind review, round 1 (2026-10-19): a hit on a recorded continuation
    /// skipped what the cut path would have done, so entries pushed before it popped as clean and
    /// wrote plain records that belonged to the instance (the D90 shape). A hit now marks a
    /// blocked end, as a hit in the cut-body store does. With that line taken out, the rows answer
    /// as in the comment (measured 2026-10-20); the others are the review's neighbours.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="operation">search or match.</param>
    /// <param name="expected">The guards-off answer.</param>
    [Test]
    [Arguments(@"(?:a|aa)(?=(?=[ab]*c)(a|aa)*a?)b", "aaab", "search", "(1,4)p g1=")]
    [Arguments(@"(?:a?a?(?=(?=[ab]*c)(?:a|aa)*(?<=a)))+(?<!a)", "ab", "match", "(0,2)p")] // None
    [Arguments(@"(?:a?a?(?=(?=[ab]*c)(a|aa)*(?<=a)))+(?<!a)", "aab", "search", "(0,3)p g1=(0,1),(1,2),(0,1),(1,2)")] // g1=(0,1),(1,2)
    [Arguments(@"a?a?(?:(?=(?=[ab]*c)(a|aa)*a?)a){1,3}aa", "aaab", "search", "(0,4)p g1=(0,1),(1,2),(2,3)")]
    [Arguments(@"(?:a?a?(?=(?=[ab]*c)(a|aa)*(?=a)?))+\1", "aab", "match", "(0,3)p g1=(1,2),(1,2)")] // g1=(1,2)
    public void A_hit_on_a_recorded_continuation_marks_a_blocked_end(
        string pattern,
        string subject,
        string operation,
        string expected
    )
    {
        Run(pattern, subject, operation).Should().Be(expected);
    }

    /// <summary>
    /// A group open around the lookahead keys its parked start (D122). Group 1 opens before the
    /// lookahead, so its start is on the save stack, and the outer <c>a*</c> giving back one
    /// character at a time reaches the lookahead again at 2 with a longer group. Before D122 the
    /// memo was off here. With the start left out of the key (the memo on and no start, measured
    /// 2026-10-20 and again 2026-11-01), the earlier recorded failure is replayed, <c>\1</c> is
    /// never tried against (0,3), and the rows answer as in the comment. Upstream 2026.9.29 has the
    /// same spans and leaves group 1 unset; see DIVERGENCES, "A partial match reports a group that
    /// closes before a repeat's tail runs out of text".
    /// </summary>
    /// <remarks>
    /// The nested rows put groups between group 1 and the lookahead. The key borrowed from the
    /// lookahead's repeat reads group 1's save-stack slot counted from the top (the D104 review's
    /// point 3); with six groups in between that read lands on a parked start below the
    /// lookahead's parked state, which must still key the arrivals apart. Expected answers are the
    /// port's with the memo off where a group is open (before D122), which these small rows reach
    /// within the budget. So these rows pass with the fix reverted too (the memo is then off): they
    /// guard against a key too coarse, not for the fix; the rows above go red without it.
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="operation">search or match.</param>
    /// <param name="expected">The guards-off answer.</param>
    [Test]
    [Arguments(@"a*(a*(?=(?:a|b)*)b)\1", "aabaa", "search", "(0,5)p g1=(0,3)")] // (3,5)p g1=
    [Arguments(@"a*(a*(?=(?:a|b)*)b)\1", "aabaa", "match", "(0,5)p g1=(0,3)")] // None
    [Arguments(@"a*(a*(?=(?:a|b)*)b)\1", "aaabaa", "search", "(0,6)p g1=(1,4)")] // (4,6)p g1=
    [Arguments(
        @"a*(a*(((((((?=(?:a|b)*)))))))b)\1",
        "aabaa",
        "search",
        "(0,5)p g1=(0,3) g2=(2,2) g3=(2,2) g4=(2,2) g5=(2,2) g6=(2,2) g7=(2,2)"
    )]
    [Arguments(
        @"a*(a*(((((((?=(?:a|b)*)))))))b)\1",
        "aabaa",
        "match",
        "(0,5)p g1=(0,3) g2=(2,2) g3=(2,2) g4=(2,2) g5=(2,2) g6=(2,2) g7=(2,2)"
    )]
    [Arguments(
        @"a*(a*(((((((?=(?:a|b)*)))))))b)\1",
        "aaabaa",
        "search",
        "(0,6)p g1=(1,4) g2=(3,3) g3=(3,3) g4=(3,3) g5=(3,3) g6=(3,3) g7=(3,3)"
    )]
    public void A_group_open_around_the_lookahead_keys_its_start_apart(
        string pattern,
        string subject,
        string operation,
        string expected
    )
    {
        Run(pattern, subject, operation).Should().Be(expected);
    }

    /// <summary>
    /// The first pattern without partial matching, where the memo is off: no start has a group
    /// that <c>\1</c> can repeat before the end. Upstream 2026.9.29 answers None (D122 review W2),
    /// so a fix that keys the group's start must leave the full-match answer alone.
    /// </summary>
    [Test]
    public void A_group_open_around_the_lookahead_finds_no_full_match()
    {
        var regex = new FuzzyRegex(@"a*(a*(?=(?:a|b)*)b)\1");

        (regex.Match("aabaa").Success, regex.MatchAtStart("aabaa").Success).Should().Be((false, false));
    }

    private static string Run(string pattern, string subject, string operation)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = string.Equals(operation, "match", StringComparison.Ordinal)
            ? regex.MatchAtStart(subject, partial: true)
            : regex.Match(subject, partial: true);
        if (!m.Success)
        {
            return "None";
        }

        string groups = string.Concat(
            Enumerable
                .Range(1, m.Groups.Count - 1)
                .Select(i =>
                    " g"
                    + i.ToString(CultureInfo.InvariantCulture)
                    + "="
                    + string.Join(
                        ",",
                        m.Groups[i]
                            .Captures.Select(static c =>
                                string.Create(CultureInfo.InvariantCulture, $"({c.Index},{c.Index + c.Length})")
                            )
                    )
                )
        );
        return string.Create(
            CultureInfo.InvariantCulture,
            $"({m.Index},{m.Index + m.Length}){(m.PartialMatch ? "p" : "")}{groups}"
        );
    }
}
