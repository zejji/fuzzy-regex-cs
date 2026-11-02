using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.OpenDefects;

/// <summary>
/// One test per known, unfixed defect whose right answer is settled. They are <c>[Explicit]</c>,
/// so the ratchet does not run them; the number that fail is the measured size of the remaining
/// correctness work. The first step of each fix moves its test into the suite proper.
/// </summary>
/// <remarks>
/// Run them with
/// <c>dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/OpenDefectTests/*"</c>.
/// Upstream's answers were measured against <c>regex</c> 2026.9.10 on 2026-09-27 and are quoted
/// beside each assertion. Where upstream is wrong the expected value is argued from principle, and
/// the argument is written above the test.
/// </remarks>
[Explicit]
public sealed class OpenDefectTests
{
    // D134 (found 2026-11-02 by the D122 blind review). D122's shape with the lookahead inside an
    // atomic group: the continuation memo is still refused while a pruning mark is open, so the
    // pass forks until the budget stops it and answers the partial at the attempt's start. The
    // argument for the expected answers is D122's (PartialContinuationMemoTests): group 2 always
    // takes part, so the last 'a' the loop takes needs a 'c' after it, and no continuation puts one
    // inside 'a' * 9 + 'b'. The grid's judge (upstream non-partial over every continuation up to 3
    // characters of 'abcdx') agrees: match None, search P(10, 10). The port answers (0, 10) partial
    // to both, as at n = 9 without the atomic group before D122's fix; at n <= 5 it is right.
    // Upstream 2026.9.29 answers (0, 10) partial.
    [Test]
    [Arguments(@"((?>(?=(?:ab|a)*(b?)a?)a))+(?(2)c|d)")]
    [Arguments(@"(?>((?=(?:ab|a)*(b?)a?)a))+(?(2)c|d)")]
    public void An_atomic_group_around_a_recorded_lookahead_still_answers_the_budget_partial(string pattern)
    {
        var regex = new FuzzyRegex(pattern);
        string subject = new string('a', 9) + "b";

        regex.MatchAtStart(subject, partial: true).Success.Should().BeFalse();
        Match m = regex.Match(subject, partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 10, 0));
    }

    // D101, what is left of it after the calls nothing can tell from their groups are written out
    // (CallInliningTests): a call that recurses stays a call, and a run-out inside one that a
    // positive condition test or any lookaround of a fuzzy pattern opened still answers the partial
    // at the attempt's start (Matcher.RunOutInALookaroundBody's SHORTCUT). Each row's expected
    // answer is its written-out form's, `(?<r>x+)` for the recursive group, and the argument above
    // it; the port's answers were measured 2026-10-24 on fix/d101.

    // The (?R) grid's verb rows: the lookahead needs an 'x' at 1 and `\Z` needs the end at 1, so no
    // text completes it (the judge, d11-brute-judge.py on regex 2026.9.29, None). The port answers
    // P(0, 1): the recursion reaches the (*PRUNE), so the run-out inside it cannot wait for the
    // body's other paths (Matcher.WaitsAcrossCalls, Node.Body.ReachesVerb) and keeps the SHORTCUT.
    [Test]
    public void A_run_out_inside_a_recursion_that_reaches_a_verb_answers_as_the_judge()
    {
        new FuzzyRegex(@"x(?=(?R)?y)(*PRUNE)\Z").MatchAtStart("x", partial: true).Success.Should().BeFalse();
    }

    // A fuzzy pattern, the recursive call in a positive lookahead. As the written-out row in
    // CallInliningTests: from 0 the section must end the text at 1, or at 2 with its one error
    // spent on an insertion, and the lookahead needs an 'x' there, so 'xy' + anything has no
    // completion from 0; 1 is a 'y', and the search's leftmost completable start is the edge
    // ('xy' + 'xx'). The port answers P(0, 2) to both; `(?<r>x+)(?:(?=x+)\Z){e<=1}` answers None
    // and P(2, 2).
    [Test]
    public void A_run_out_inside_a_recursive_call_in_a_fuzzy_pattern_answers_as_written_out()
    {
        var regex = new FuzzyRegex(@"(?<r>x(?&r)?)(?:(?=(?&r))\Z){e<=1}");

        regex.MatchAtStart("xy", partial: true).Success.Should().BeFalse();
        Match m = regex.Match("xy", partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 2, 0));
    }

    // D125. The groups close in the order g1, 2, 3, so lastindex is 3; the optional lookbehind
    // holding the call changes no group. With the recursive call kept, the port answers lastindex 1;
    // the written-out `(?P<g1>x+)(?:(?<!x+))?(a)(b)` answers 3, and so does upstream 2026.9.29's
    // written-out form (its call form loses the match, ledger entry 8).
    [Test]
    public void A_call_through_a_lookaround_the_other_way_leaves_lastindex_alone()
    {
        Match m = new FuzzyRegex("(?P<g1>x(?P>g1)?)(?:(?<!(?P>g1)))?(a)(b)").Match("xab");

        (m.Success, m.Index, m.Length, m.LastGroupNumber).Should().Be((true, 0, 3, 3));
    }

    // D126. The pattern consumes one 'x', so no text makes 'xxcw' + w a full match: None. The
    // lookahead's body is a search for a later 'y' (\X takes any character), so whatever follows
    // it cannot decide anything before the end of the pattern. The port's second pass forks once
    // per blocked \X cut, and the nested repeats multiply them (1,279 blocked atomic cuts at
    // n = 4) until the fork budget (256 (n + 1)) answers the partial at the attempt's start,
    // P(0, 4). Under the budget, at 'xxc', it answers None. The judge (d11-brute-judge.py, regex
    // 2026.9.29) answers None; upstream 2026.9.29 answers (0, 4) partial, as does main 92bf4d78.
    [Test]
    public void A_grapheme_inside_nested_repeats_under_a_negative_lookahead_stays_under_the_fork_budget()
    {
        new FuzzyRegex(@"x(?!(?:(?:\X|(?R)?)+w?)+y)").FullMatch("xxcw", partial: true).Success.Should().BeFalse();
    }

    // Not yet registered (found 2026-10-31 by the D132 blind review, F4 shape 6; reported in the
    // night log). A partial search where a condition's positive test runs out at start 0 with
    // room in every section: regex 2026.9.29 and judge132.py answer P(1, 2) (completion 'gg'),
    // the port P(0, 2), before and after the D132 fix. The one-test form
    // `(?:(?(?=(?:f^){i<=2})f|g)){i<=2}` over 'fx' differs the same way before the fix (port
    // P(0, 2), upstream P(1, 2)), so no section limit is involved.
    [Test]
    public void A_partial_search_past_a_condition_test_run_out_starts_where_upstream_does()
    {
        Match m = new FuzzyRegex(@"(?:(?(?=(?:f^){i<=2})f|g)(?(?=(?:f^){i<=2})f|g)){i<=2}").Match("fx", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, 1, 2));
    }
}
