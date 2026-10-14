using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

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
    /// <c>(a+)+c</c> over a run of <c>a</c>s. The compiler copies the first iteration out of a
    /// repeat whose body holds a repeat (<c>NodeCompiler.BuildRepeat</c>), so the pattern runs as
    /// <c>(a+)(a+)*c</c>, and every start walks the run again through the copy's <c>a+</c>. What the
    /// memo removes is the work behind each character given back: without it the memo repeat
    /// explores the rest of the run again at every one, so the search is cubic in the run; with it
    /// that is one guard lookup, and the search is quadratic.
    /// </summary>
    /// <remarks>
    /// Counted in guard lookups, not steps: the copy gives characters back inside one step, asking
    /// the guards once per character. Doubling the run multiplied the lookups by 3.9 with the memo
    /// (15,643 to 61,293) and by 7.3 without it (201,402 to 1,472,802; Debug, 2026-10-11, runs of
    /// 100 and 200), so the bound of 5.5 sits between them with a wide margin either way.
    /// </remarks>
    [Test]
    [Category(EngineWork.Category)]
    public void A_nested_repeat_search_is_quadratic_in_the_run()
    {
        var regex = new FuzzyRegex("(a+)+c", FuzzyRegexOptions.None, EngineWork.HangGuard);

        long Lookups(int run)
        {
            string subject = new string('a', run) + "bc";
            long before = WorkCounter.GuardChecks;
            regex.IsMatch(subject).Should().BeFalse();
            return WorkCounter.GuardChecks - before;
        }

        long small = Lookups(100);
        long large = Lookups(200);
        EngineWork.SkipUnlessCounting();

        ((double)large / small).Should().BeLessThan(5.5, "a cubic search grows eightfold when the run doubles");
    }

    /// <summary>
    /// <c>(\w+\s?)+$</c> over words and a <c>!</c>. Attempt 0 records a body failure at every
    /// position, since an iteration can start after any letter; with the memo every later attempt
    /// stops at its first check, so the search is linear in the subject rather than quadratic. It
    /// took 57,193 steps at 400 words with the memo and 15,071,604 without (Debug, 2026-10-11).
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_search_keeps_what_one_start_learned_for_the_next()
    {
        var regex = new FuzzyRegex(@"(\w+\s?)+$", FuzzyRegexOptions.None, EngineWork.HangGuard);
        string subject = string.Concat(Enumerable.Repeat("aaaa ", 400)) + "!";

        EngineWork.ShouldTakeAtMostSteps(
            () => regex.IsMatch(subject).Should().BeFalse(),
            200_000,
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
    // C5: the copy starts with a single-character repeat, so the FAILURE arm's leading-repeat skip
    // runs after a skipped start as well.
    [Arguments("(?:a*b)+c", "abaabxaabc", 6, 10)]
    [Arguments("(?:a*b)+c", "babbc", 0, 5)]
    [Arguments("(?:a?b?)+c", "abxabc", 3, 6)]
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
    // C5: patterns that start with a copied first iteration.
    [Arguments("(a*)+", "aab", false, "0,2;2,2;3,3")]
    [Arguments("(?:a+)+c", "aacaac", true, "0,3;1,3;3,6;4,6")]
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

    // ---------------------------------------------------------------------------------------
    // C5: the copied first iteration (S2d) and the guarded-span jump (G2).
    // docs/plan/2026-10-13-linear-nested-repeats-design.md
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// S2d. <c>(a+)+c</c> over a run of <c>a</c>s: attempt 0 leaves R's body guarded at every
    /// position, and every later attempt starts with the copy of R's first iteration, so it is
    /// failed before it starts. Without that, each later attempt walks the run again through the
    /// copy, a step per character, and the steps grow fourfold when the run doubles.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    [Arguments("(a+)+c", "a", "bc")]
    [Arguments("(x+x+)+y", "x", "z")]
    public void A_search_skips_the_starts_the_first_iteration_already_failed_from(
        string pattern,
        string unit,
        string tail
    )
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);

        long Steps(int run)
        {
            string subject = string.Concat(Enumerable.Repeat(unit, run)) + tail;
            long before = WorkCounter.Steps;
            regex.IsMatch(subject).Should().BeFalse();
            return WorkCounter.Steps - before;
        }

        long small = Steps(200);
        long large = Steps(400);
        EngineWork.SkipUnlessCounting();

        ((double)large / small).Should().BeLessThan(2.6, "the steps of a linear search double when the run doubles");
    }

    /// <summary>
    /// G2. <c>\A(a+)+c</c> makes one attempt. Each entry into R's body walks the inner <c>a+</c>
    /// back over positions the earlier entries guarded; one span holds them all, so the walk is
    /// one lookup per entry rather than one per character, and the lookups double, not quadruple,
    /// when the run doubles.
    /// </summary>
    [Test]
    [Category(EngineWork.Category)]
    public void A_repeat_walks_back_over_a_guarded_span_in_one_move()
    {
        var regex = new FuzzyRegex(@"\A(a+)+c", FuzzyRegexOptions.None, EngineWork.HangGuard);

        long Lookups(int run)
        {
            string subject = new string('a', run) + "bc";
            long before = WorkCounter.GuardChecks;
            regex.IsMatch(subject).Should().BeFalse();
            return WorkCounter.GuardChecks - before;
        }

        long small = Lookups(200);
        long large = Lookups(400);
        EngineWork.SkipUnlessCounting();

        ((double)large / small).Should().BeLessThan(2.6, "one lookup per entry into the body is linear in the run");
    }

    [Test]
    // The copy is the attempt's first node; R's body guards from earlier starts decide later ones.
    [Arguments("(a+)+c", "aaabaac", 0, 4, 7, "aa")]
    [Arguments("(a+)+c", "aaabaac", 1, 4, 7, "aa")]
    // An empty first iteration: the copy at 3 matches empty and R's body runs at 3 again.
    [Arguments("(a*)+c", "aabc", 0, 3, 4, ",")]
    // Two copies: the pattern starts with the first, which is not R's first iteration.
    [Arguments("(a+){2,}c", "aaxaaac", 0, 3, 7, "aa,a")]
    [Arguments("(a+)+?c", "aaxaac", 0, 3, 6, "aa")]
    [Arguments("(?r)c(a+)+", "xcaaba", 0, 1, 4, "aa")]
    [Arguments("(x+x+)+y", "xxxzxxxy", 0, 4, 8, "xxx")]
    [Arguments(@"(\w+\s?)+$", "ab cd!ef gh", 0, 6, 11, "ef ,gh")]
    // A surrogate pair per character: guards two units apart never merge into one span.
    [Arguments("(\U0001F600+)+c", "\U0001F600\U0001F600b\U0001F600c", 0, 5, 8, "\U0001F600")]
    public void A_search_that_starts_with_a_first_iteration_gives_upstreams_answer(
        string pattern,
        string subject,
        int beginning,
        int start,
        int end,
        string captures
    )
    {
        Match m = new FuzzyRegex(pattern).Match(subject, beginning);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        string.Join(',', m.Groups[1].Captures.Select(static c => c.Value)).Should().Be(captures);
    }

    [Test]
    public void A_partial_search_that_starts_with_a_first_iteration_gives_upstreams_answer()
    {
        // regex.search('(a+)+c', 'aaab', partial=True): (4, 4), partial. A partial call keeps no
        // guards across starts, so it skips none.
        Match m = new FuzzyRegex("(a+)+c").Match("aaab", partial: true);

        (m.Index, m.Index + m.Length, m.PartialMatch).Should().Be((4, 4, true));
    }
}
