using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The failure memo kept from one start position to the next within a search (Option B,
/// <c>PatternObject.KeepsFailureMemosAcrossAttempts</c>, <c>docs/plan/2026-10-11-memo-option-b.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// The witnesses are answers that change if one exclusion is deleted while the memo is kept
/// across starts: each is a pattern where a later attempt reaches a position with something the
/// earlier attempt did not have, a different capture. The pins are the cases the design argues
/// are safe (the search anchor under <c>MustAdvance</c>, a lookbehind reaching before the start,
/// <c>\K</c>, overlapped and reverse searches, partial calls): an answer that changes there is a
/// bug in the memo.
/// </para>
/// <para>
/// Every expected value is regex 2026.9.10's: <c>regex.search(pattern, subject)</c>, or the spans of
/// <c>regex.finditer</c>, measured 2026-10-11.
/// </para>
/// </remarks>
public sealed class FailureMemoAcrossAttemptsTests
{
    /// <summary>
    /// The rows that make the memo across starts pay. Attempt 0 records a body failure at every
    /// position of the run; without the memo every later attempt walks the rest of the run again,
    /// so <c>(a+)+c</c> is cubic in the run and <c>(\w+\s?)+$</c> quadratic. With it, every later
    /// attempt stops at its first check.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("(a+)+c", 1_000, 1, 3_000_000)]
    [Arguments(@"(\w+\s?)+$", 400, 5, 100_000)]
    public void A_search_keeps_what_one_start_learned_for_the_next(string pattern, int runs, int unit, long maxSteps)
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject =
            unit == 1 ? new string('a', runs) + "bc" : string.Concat(Enumerable.Repeat("aaaa ", runs)) + "!";

        EngineWork.ShouldTakeAtMostSteps(
            () => regex.IsMatch(subject).Should().BeFalse(),
            maxSteps,
            "the starts after the first stop at the memo"
        );
    }

    [Test]
    // A capture made before the repeat differs between starts, and a backreference reads it.
    // The attempt at 1 takes group 1 = 'b' and fails from the body at 2 and 3, since no 'b'
    // follows the 'a's; the attempt at 2 takes group 1 empty and matches from the body at 2.
    [Arguments(@"(b*)(?:(a))+\1c", "abaac", 2, 5)]
    [Arguments(@"(b*)(?:(a))+\1c", "baacb", 1, 4)]
    // A capture made inside the repeat on one start's path, read by a group-exists conditional.
    // Attempt 0 sets group 1 with the 'a' and wants a 'c'; attempt 1 never sets it and takes 'b'.
    [Arguments(@"(?:(a)|b)+(?(1)c|b)", "abbb", 1, 4)]
    [Arguments(@"(?:(a)|b)+(?(1)c|b)", "abbbb", 1, 5)]
    // The same, reversed: the attempts move left.
    [Arguments(@"(?r)(?:(a)|b)+(?(1)c|b)", "bbba", 0, 3)]
    public void A_pattern_that_reads_captures_resets_the_memo_at_each_start(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
    }

    [Test]
    public void A_search_from_a_later_pos_with_a_backslash_g_gives_upstreams_answer()
    {
        // \G holds only at pos 1 here; the pattern is left out of the memo across starts anyway.
        Match m = new FuzzyRegex(@"(?:\G|b)(?:a|a)+c").Match("xbaac", 1);

        (m.Index, m.Index + m.Length).Should().Be((1, 5));
    }

    [Test]
    // A lookbehind after the repeat reads text before the attempt's start.
    [Arguments(@"(?:a|b)+(?<!^a.)c", "abac", 0, 4)]
    [Arguments(@"(?:a|b)+(?<=ba)c", "abbac", 0, 5)]
    // A repeat inside a lookbehind has no memo; the one after it is plain.
    [Arguments(@"(?<=(?:a|a)+b)c", "aabc", 3, 4)]
    // A top-level \K moves the reported start only.
    [Arguments(@"(?:a|a)+\Kc", "aaac", 3, 4)]
    [Arguments(@"(?r)c(?:a|a)+", "bcaab", 1, 4)]
    [Arguments(@"(a+)+b", "aaaab", 0, 5)]
    public void A_search_that_keeps_the_memo_gives_upstreams_answer(string pattern, string subject, int start, int end)
    {
        Match m = FuzzyRegex.Match(subject, pattern);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
    }

    [Test]
    // MustAdvance after an empty match: the next search starts at the anchor and may not end there.
    [Arguments(@"(?:a|a)*", "aab", false, "0,2;2,2;3,3")]
    [Arguments(@"(?:a|a)*(?=b)", "aab", false, "0,2;2,2")]
    [Arguments(@"(?:a|b)*?", "ab", false, "0,0;0,1;1,1;1,2;2,2")]
    [Arguments(@"(?r)(?:a|a)*", "baa", false, "1,3;1,1;0,0")]
    [Arguments(@"(?r)(?:a|a)*(?<=b)", "baa", false, "1,1")]
    [Arguments(@"(?:a|a)+c", "aacaac", true, "0,3;1,3;3,6;4,6")]
    [Arguments(@"(?r)c(?:a|a)+", "caacaa", true, "3,6;3,5;0,3;0,2")]
    public void Matches_that_keep_the_memo_give_upstreams_answers(
        string pattern,
        string subject,
        bool overlapped,
        string spans
    )
    {
        MatchCollection matches = new FuzzyRegex(pattern).Matches(subject, overlapped: overlapped);

        string.Join(';', matches.Select(static m => $"{m.Index},{m.Index + m.Length}")).Should().Be(spans);
    }

    [Test]
    public void A_partial_search_gives_upstreams_answer()
    {
        Match m = new FuzzyRegex("(?:a|a)+cd").Match("aacaac", partial: true);

        (m.Index, m.Index + m.Length, m.PartialMatch).Should().Be((3, 6, true));
    }
}
