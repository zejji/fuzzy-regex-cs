using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The repeat guards used as a failure memo: once the body of a safe repeat has failed at a
/// position, it is never tried there again in the same attempt. See <c>RepeatInfo.FailureMemo</c>
/// and <c>docs/plan/2026-09-26-backtrack-memoisation-design.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The speed tests are patterns that are exponential in upstream and were exponential here: every
/// way of splitting the run of <c>a</c>s is tried before the attempt gives up. Upstream regex
/// 2026.9.10 takes 2.7 s for <c>(?:a|a)+c</c> at 24 <c>a</c>s (measured 2026-09-26), doubling with
/// every extra <c>a</c>, so at 40 it would run for about two days.
/// </para>
/// <para>
/// The witness tests are the other half. Each is an answer that changes if one of the memo's
/// exclusions is deleted, so a mutation that widens the memo goes red rather than silently changing
/// an answer. Every expected value is upstream's.
/// </para>
/// </remarks>
public sealed class FailureMemoTests
{
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("(?:a|a)+c")]
    [Arguments("(a|aa)+c")]
    [Arguments("(?:a|aa)+?c")]
    [Arguments("^(?:a|a)*c")]
    public void A_repeat_whose_branches_split_a_run_many_ways_fails_fast(string pattern)
    {
        // Forty 'a's then "bc": the 'c' is there so the required-string screen cannot answer before
        // the engine runs. Every way of splitting the run reaches the 'b' and fails. With the memo,
        // the body fails once per position, so the attempt costs time linear in the run.
        //
        // Counted in engine steps rather than timed, because a busy machine is not a slow engine
        // (D13). The four patterns took 1,230 to 15,507 steps for all three calls (Debug,
        // 2026-09-28); with the memo off the first call alone hits the bound, since every split of
        // forty 'a's is about 2^40 steps. The step bound also stops that runaway at once.
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = new string('a', 40) + "bc";

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                regex.IsMatch(subject).Should().BeFalse();
                regex.MatchAtStart(subject).Success.Should().BeFalse();
                regex.FullMatch(subject).Success.Should().BeFalse();
            },
            200_000,
            "with the memo, the body fails once per position"
        );
    }

    [Test]
    [Category(EngineWork.Category)]
    [Arguments("(?:(?(?=a)a|a)|a)*b")]
    [Arguments("(?:(?(?<=a|^)a|a)|a)*?b")]
    [Arguments("(?:(?(?=(a))a|a)|a)*b")]
    [Arguments("(?:(?(?=a)(a)|a)|a)*b")]
    public void A_conditional_does_not_forget_what_its_branches_learned(string pattern)
    {
        // D35. A conditional saves every repeat's guards before its test and used to put them all
        // back when it was undone. So at each position the path through the conditional explored
        // the rest of the run, the repeat recorded where its body failed, and backtracking out of
        // the conditional wiped those records; the plain 'a' beside it then explored the same
        // rest again. Two explorations per position is 2^n. The rows vary the kind of test and
        // where the captures are. A negative test is not among them: when it holds, its arm runs
        // after the restore, so nothing is lost. Upstream regex 2026.9.10 is exponential here too:
        // regex.match(r'(?:(?(?=a)a|a)|a)*b', 'a' * 18 + 'cb') took 89 ms and doubles with every
        // 'a' (measured 2026-09-29). The answers are upstream's: no match at the start, and a
        // search finds the final 'b' alone. The four rows took 14,114 to 17,714 steps for all
        // three calls (Debug, 2026-09-29); before the fix the first call alone hits the bound.
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = new string('a', 40) + "cb";

        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                regex.MatchAtStart(subject).Success.Should().BeFalse();
                regex.FullMatch(subject).Success.Should().BeFalse();
                Match m = regex.Match(subject);
                (m.Index, m.Length).Should().Be((41, 1));
            },
            200_000,
            "the guards a repeat records after a conditional's test survive the test being undone"
        );
    }

    [Test]
    [Arguments(@"(?:(?(?=a)(a)|a)|a)*b", "aaacaab", "search", false, 4, 7, false, "4,5;5,6")]
    [Arguments(@"(?:(?(?=(a))a|a)|a)*b", "aaacaab", "search", false, 4, 7, false, "4,5;5,6")]
    [Arguments(@"(?:(?(?=a)a|a)|a)*b", "aaa", "match", true, 0, 3, true, "")]
    [Arguments(@"(?:(?(?=a)a|a)|a)*b", "aaacb", "search", true, 4, 5, false, "")]
    [Arguments(@"(?:(?(?=a)(?(?<=a)a|b)|b)|a)*b", "aaacab", "search", false, 4, 6, false, "")]
    [Arguments(@"(?:(?(?=a)ab|a)|a)*b", "aaab", "fullmatch", false, 0, 4, false, "")]
    [Arguments(@"(?:(?(?=a)(a)|a)|b|a)+?$", "aaab", "search", false, 0, 4, false, "0,1;1,2;2,3")]
    [Arguments(@"(?:(?(?=a*b)a|b)|a){2,}b", "aab", "match", false, 0, 3, false, "")]
    [Arguments(@"(?:(?(?=a)a)|a)*\Kb", "aacab", "search", false, 4, 5, false, "")]
    [Arguments(@"(a)?(?:(?(1)a|b)|a)*c", "aabac", "search", false, 0, 5, false, "")]
    [Arguments(@"(a)?(?:(?(?=a)a|a)|a)*\1b", "aaacaab", "search", false, 4, 7, false, "4,5")]
    [Arguments(@"(?:(?(?=a)a|a)|a)*(*PRUNE)b", "aaacaab", "search", false, 4, 7, false, "")]
    public void A_conditional_that_keeps_the_guards_gives_upstreams_answer(
        string pattern,
        string subject,
        string operation,
        bool partial,
        int start,
        int end,
        bool isPartial,
        string group1
    )
    {
        // D35's edge cases: captures set in an arm or in the test, a partial match, a nested
        // conditional, a conditional with no else, a lazy repeat, a minimum, a \K after the
        // repeat, and three controls where the memo is off (a group-exists conditional, a
        // backreference, (*PRUNE)), which keep upstream's save. The first two fail at positions
        // 0 to 3 with the kept guards in use, then match at 4, so they check that a kept guard
        // changes no capture. Upstream: regex.<operation>(pattern, subject, partial=partial),
        // regex 2026.9.10, 2026-09-29.
        var regex = new FuzzyRegex(pattern);
        Match m = operation switch
        {
            "match" => regex.MatchAtStart(subject, partial: partial),
            "fullmatch" => regex.FullMatch(subject, partial: partial),
            _ => regex.Match(subject, 0, subject.Length, partial),
        };

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length, m.PartialMatch).Should().Be((start, end, isPartial));
        if (m.Groups.Count > 1)
        {
            string.Join(';', m.Groups[1].Captures.Select(static c => $"{c.Index},{c.Index + c.Length}"))
                .Should()
                .Be(group1);
        }
    }

    [Test]
    public void A_group_exists_conditional_anywhere_keeps_the_memo_off()
    {
        // Two paths reach position 1: one took the 'a' through group 1, one did not. With group 1
        // set the conditional wants 'c', so every way on from position 1 fails; without it, "ab"
        // follows and the whole of "aab" matches. A memo keyed on the position alone would skip
        // the second path and answer "ab" at 1. Upstream: regex.search(r'(?:(a)|a)+(?(1)c|b)',
        // 'aab') spans (0, 3), group 1 unset.
        Match m = FuzzyRegex.Match("aab", "(?:(a)|a)+(?(1)c|b)");

        (m.Index, m.Length).Should().Be((0, 3));
        m.Groups[1].Success.Should().BeFalse();
    }

    [Test]
    public void A_backreference_anywhere_keeps_the_memo_off()
    {
        // The same story told with a backreference. The path that takes the 'a' at position 1
        // with '.' reaches position 2 first, with group 1 unset, and nothing after it can match
        // because '\1' needs the group. The path that takes it with '(a)' reaches position 2 with
        // group 1 set, and '.', '\1' and 'c' then match. Upstream:
        // regex.search(r'a(?:.|(a)){1,}\1c', 'aabac') spans (0, 5), group 1 (1, 2).
        Match m = FuzzyRegex.Match("aabac", @"a(?:.|(a)){1,}\1c");

        (m.Index, m.Length).Should().Be((0, 5));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((1, 1));
    }

    [Test]
    [Arguments(@"(?:(?=\K)b|)+.c", "aabc", 3, 1)]
    [Arguments(@"(?:(?=\K)b|)+.c", "abc", 2, 1)]
    [Arguments(@"(?:(?>\K)b|)+.c", "abc", 2, 1)]
    [Arguments(@"(?i)(?:b|((?(?=\K)(?i:A)|\b)|\b|(?:\w|a))){1,}?\w.[^a]c", "aabc", 4, 0)]
    public void A_keep_inside_a_lookaround_or_atomic_group_keeps_the_memo_off(
        string pattern,
        string subject,
        int index,
        int length
    )
    {
        // A '\K' moves the reported start and pushes an entry that moves it back when the path
        // fails. Inside a lookaround, an atomic group or a conditional's test, that entry is thrown
        // away as soon as the construct succeeds, so the moved start outlives the path that moved
        // it. A failing path then has a lasting effect, and a memo that skips an equivalent failing
        // path skips that effect too. In the first row the path that tries 'b' at 2 moves the start
        // to 2 and fails; the path that tries it again later moves it to 3, and '.c' then matches
        // with the start at 3. Upstream: regex.search(pattern, subject).span() is (index,
        // index + length) in every row, regex 2026.9.10.
        Match m = FuzzyRegex.Match(subject, pattern);

        (m.Index, m.Length).Should().Be((index, length));
    }

    [Test]
    public void A_repeat_with_a_maximum_gets_no_memo()
    {
        // The count matters when there is a maximum. 'a' then 'b' reaches position 2 with two
        // iterations used, so the body tried there can take only one more 'b', and no 'c' follows
        // it. "ab" reaches position 2 with one iteration used, and 'b', 'b', 'c' completes the
        // match. A memo would carry the first failure over to the second path. Upstream:
        // regex.search(r'^(?:a|ab|b){0,3}c', 'abbbc') spans (0, 5).
        Match m = FuzzyRegex.Match("abbbc", "^(?:a|ab|b){0,3}c");

        (m.Index, m.Length).Should().Be((0, 5));
    }
}
