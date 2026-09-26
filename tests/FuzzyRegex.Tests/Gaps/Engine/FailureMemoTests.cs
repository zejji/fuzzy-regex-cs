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
    /// <summary>
    /// Long enough to rule out any doubt about the direction of a failure, short enough that the
    /// pre-memo engine is a red test in seconds rather than a stalled suite.
    /// </summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Test]
    [Arguments("(?:a|a)+c")]
    [Arguments("(a|aa)+c")]
    [Arguments("(?:a|aa)+?c")]
    [Arguments("^(?:a|a)*c")]
    public void A_repeat_whose_branches_split_a_run_many_ways_fails_fast(string pattern)
    {
        // Forty 'a's then "bc": the 'c' is there so the required-string screen cannot answer before
        // the engine runs. Every way of splitting the run reaches the 'b' and fails. With the memo,
        // the body fails once per position, so the attempt costs time linear in the run.
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout);
        string subject = new string('a', 40) + "bc";

        regex.IsMatch(subject).Should().BeFalse();
        regex.MatchAtStart(subject).Success.Should().BeFalse();
        regex.FullMatch(subject).Success.Should().BeFalse();
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
