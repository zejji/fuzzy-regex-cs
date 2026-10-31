using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D121: backtracking into a conditional put back the repeat guard lists it had saved before its
/// test, and so threw away the marks <c>END_GREEDY_REPEAT</c> had made since, saying that the body
/// of an enclosing repeat had reached its end from a position. The failure written at that position
/// when backtracking then reached the body's entry claimed the body could never get to its end from
/// there, which held only for the group values of that path, and a later path with other values was
/// refused.
/// </summary>
/// <remarks>
/// In <c>(a?)(?(?=c)c|[ab])*\1</c> over 'ac' the first path takes group 1 = 'a', and the body
/// matches the 'c' from 1. Backtracking into the conditional at 1 restores the lists without the
/// mark for 1, and the body's entry at 1 then records a failure there. The second path, group 1 =
/// '', needs the body at 1 again and is refused, so the answer was (0, 1) for (0, 2).
/// Upstream does the same for a positive test (regex 2026.9.29 gives the answer written beside each
/// row); a negative one only here, because D54 keeps a failed negative test's saved block on the
/// stack as a positive test's is kept. Every expected value is the leftmost-first answer Perl 5.42
/// and .NET 10's System.Text.RegularExpressions give (2026-10-31), and the answer this port gives
/// with every repeat guard switched off.
/// </remarks>
public sealed class ConditionalRestoreGuardTests
{
    [Test]
    // The D104 differential's row, a negative test (regex (0, 6)), and its positive twin.
    [Arguments(@"[ab]*?(?=(?P<g>[abc]*?)(?:c|cc))(?(?!c)[ab]|c)*$(?=(?P=g))", "bcccba", 0, 6, 1, 1)]
    [Arguments(@"[ab]*?(?=(?P<g>[abc]*?)(?:c|cc))(?(?=c)c|[ab])*$(?=(?P=g))", "bcccba", 0, 6, 1, 1)] // regex (1, 6)
    // The smallest forms, positive and negative.
    [Arguments(@"(a?)(?(?=c)c|[ab])*\1", "ac", 0, 2, 0, 0)] // regex (0, 1)
    [Arguments(@"(a?)(?(?!c)[ab]|c)*\1", "ac", 0, 2, 0, 0)] // regex (0, 2)
    [Arguments(@"(a*)(?(?=c)c|[ab])*\1$", "accb", 0, 4, 0, 0)] // regex (1, 4)
    // A lazy and a one-or-more repeat.
    [Arguments(@"(a?)(?(?=c)\w|\w)*?\1$", "ac", 0, 2, 0, 0)] // regex (1, 2)
    [Arguments(@"(a?)(?(?=c)\w|\w)+\1", "abc", 0, 3, 0, 0)] // regex (0, 2)
    // Found by the blind review (2026-10-31): a bounded repeat, the conditional inside an
    // alternation, and a lookbehind test.
    [Arguments(@"(a?)(?(?=c)c|[ab]){0,5}\1", "ac", 0, 2, 0, 0)] // regex (0, 1)
    [Arguments(@"(a?)(?:x|(?(?=c)c|[ab]))*\1", "ac", 0, 2, 0, 0)] // regex (0, 1)
    [Arguments(@"(a?)(?(?<=a)c|[ab])*\1", "ac", 0, 2, 0, 0)] // regex (0, 1)
    // Controls from the same review, right here before the fix too: more of the body after the
    // conditional, and a repeat inside the yes-branch. Upstream gets both wrong.
    [Arguments(@"(a?)(?:(?(?=c)c|[ab])d?)*\1", "ac", 0, 2, 0, 0)] // regex (0, 1)
    [Arguments(@"(a?)(?(?=c)(?:c)+|[ab])*\1", "acc", 0, 3, 0, 0)] // regex (0, 1)
    // Control: the same choice made by a branch saves no repeats, and was always right.
    [Arguments(@"(a?)(?:(?=c)c|[ab])*\1", "ac", 0, 2, 0, 0)] // regex (0, 2)
    // From the second blind review (2026-10-31): a reversed pattern, the repeat inside a named
    // group, a group call before the loop, and a fuzzy section around the loop. The answers are
    // upstream's branch form, and Node's where Node can write the pattern; the reversed row's
    // branch form is (?r)\1(?:c(?<=c)|[ab](?<!c))*(a?), which regex gives (0, 2), group (2, 2).
    [Arguments(@"(?r)\1(?(?<=c)c|[ab])*(a?)", "ca", 0, 2, 2, 2)] // regex (1, 2)
    [Arguments(@"(?P<g>(a?)(?(?=c)c|[ab])*\2)", "ac", 0, 2, 0, 2)] // regex (0, 1)
    [Arguments(@"(a?)(?1)?(?(?=c)c|[ab])*\1(x?)", "ac", 0, 2, 0, 0)] // regex (0, 1)
    [Arguments(@"(a?)(?:(?(?=c)c|[ab])*){e<=0}\1", "ac", 0, 2, 0, 0)] // regex (0, 1)
    // The loop inside a nested repeat. Node's answer; upstream gives (0, 0) here and in its
    // branch form too, so this one is checked against Node only.
    [Arguments(@"(a?)(?:(?:(?(?=c)c|[ab]))*)*\1", "ac", 0, 2, 0, 0)] // regex (0, 0)
    // Control: the same nested repeat with no conditional. Node's answer; upstream fails this one
    // too.
    [Arguments(@"(a?)(?:(?:[ac])*)*\1", "ac", 0, 2, 0, 0)] // regex (0, 1)
    public void A_restore_after_a_conditional_keeps_the_marks_of_bodies_that_reached_their_end(
        string pattern,
        string subject,
        int start,
        int end,
        int groupStart,
        int groupEnd
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        (m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length).Should().Be((groupStart, groupEnd));
    }

    // The nested-repeat row is checked against Node only, and its control without the conditional
    // passes, so this shows the row rests on the fix: with upstream's restore the port answers
    // (0, 1), as for the flat row. Upstream's own (0, 0) is not this divergence alone.
    [Test]
    public void The_nested_repeat_row_rests_on_the_fix()
    {
        var regex = new FuzzyRegex(@"(a?)(?:(?:(?(?=c)c|[ab]))*)*\1");
        regex.PatternObject.UpstreamConditionalRestore = true;

        Match m = regex.Match("ac");

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, 0, 1));
    }

    [Test]
    public void An_anchored_call_finds_the_match_through_the_conditional_repeat()
    {
        Match m = new FuzzyRegex(@"[ab]*?(?=(?P<g>[abc]*?)(?:c|cc))(?(?!c)[ab]|c)*$(?=(?P=g))").MatchAtStart("bcccba");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 6));
        m.Groups["g"].Value.Should().BeEmpty();
    }

    // From the second blind review (2026-10-31): the group the conditional after the loop tests
    // is set on the path that marks the body at 1 and unset on the path that needs it. The answer
    // is upstream's branch form; regex 2026.9.29 gives (0, 1).
    [Test]
    public void A_restore_keeps_the_mark_for_a_path_that_leaves_the_tested_group_unset()
    {
        Match m = new FuzzyRegex(@"(?:(a)|)(?:(?(?=c)c|[ab]))*(?(1)x)").Match("ac");

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, 0, 2));
        m.Groups[1].Success.Should().BeFalse();
    }

    // A partial call's second pass: the positive test (?=cd) runs out at the edge, its blocked end
    // lets the yes-branch and the rest run, and when the test is then given up the restore drops
    // what they marked. With group 1 = '' the answer must be the one the pattern gives with that
    // group written in, P(0, 3); the restore made it None. regex 2026.9.29 gives P(0, 3) as well,
    // with group 1 = 'a'.
    [Test]
    [Arguments(@"^(a|)a?(?:(?<=[ab])(?(?=cd)c|[ab]))*$(?<=\1)")]
    [Arguments(@"^()a(?:(?<=[ab])(?(?=cd)c|[ab]))*$(?<=\1)")] // control: one value of group 1
    public void A_restore_after_a_blocked_test_keeps_the_marks_of_the_yes_branch(string pattern)
    {
        Match m = new FuzzyRegex(pattern).Match("abc", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 3));
        m.Groups[1].Length.Should().Be(0);
    }
}
