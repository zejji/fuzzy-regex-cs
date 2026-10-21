using System.Globalization;
using AwesomeAssertions;

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
    /// No continuation key where a group is open around the lookahead. Group 1 opens before the
    /// lookahead, so its start is on the save stack, not in the key, and the outer <c>a*</c>
    /// giving back one character at a time reaches the lookahead again at 2 with a longer group.
    /// With the rule taken out (<c>savedBelow != 0</c> in <c>ContinuationKeyOf</c>), the earlier
    /// recorded failure is replayed, <c>\1</c> is never tried against (0,3), and the rows answer as
    /// in the comment (36 of 90 such rows wrong, measured 2026-10-20). Upstream 2026.9.29 has the
    /// same spans and leaves group 1 unset; see DIVERGENCES, "A partial match reports a group
    /// that closes before a repeat's tail runs out of text".
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="operation">search or match.</param>
    /// <param name="expected">The guards-off answer.</param>
    [Test]
    [Arguments("aabaa", "search", "(0,5)p g1=(0,3)")] // (3,5)p g1=
    [Arguments("aabaa", "match", "(0,5)p g1=(0,3)")] // None
    [Arguments("aaabaa", "search", "(0,6)p g1=(1,4)")] // (4,6)p g1=
    public void A_group_open_around_the_lookahead_keeps_its_continuation_unrecorded(
        string subject,
        string operation,
        string expected
    )
    {
        Run(@"a*(a*(?=(?:a|b)*)b)\1", subject, operation).Should().Be(expected);
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
