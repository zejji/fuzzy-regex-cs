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
    // D121. Start 0 matches: `[ab]*?` takes the 'b', the lookahead at 1 sets g to '' before a 'c',
    // the conditional repeat takes 'cccba', and `(?=(?P=g))` matches the empty g at the end. The
    // port reports the match from start 1; regex 2026.9.10 answers (0, 6), and so does the port
    // with every repeat guard off.
    [Test]
    public void A_search_finds_the_leftmost_start_through_a_conditional_repeat_after_a_lookahead()
    {
        Match m = new FuzzyRegex(@"[ab]*?(?=(?P<g>[abc]*?)(?:c|cc))(?(?!c)[ab]|c)*$(?=(?P=g))").Match("bcccba");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 6));
        m.Groups["g"].Value.Should().BeEmpty();
    }

    // D122. Group 2 always takes part, so the conditional needs a 'c' after the last 'a' the
    // repeat takes, and no text after 'a' * 9 + 'b' puts one there: the 'b' is fixed. The anchored
    // calls have no completion, and the search's leftmost completable start is the edge. The port
    // forks until the budget stops it and answers the partial at the start; upstream 2026.9.29
    // answers (0, 10) as well. The expected answers are argued, and the port with every repeat
    // guard and the fork budget off gives the search's (10, 10) (blind review, 2026-10-20).
    [Test]
    public void A_group_open_around_a_recorded_lookahead_still_answers_the_budget_partial()
    {
        var regex = new FuzzyRegex(@"((?=(?:ab|a)*(b?)a?)a)+(?(2)c|d)");
        string subject = new string('a', 9) + "b";

        regex.MatchAtStart(subject, partial: true).Success.Should().BeFalse();
        Match m = regex.Match(subject, partial: true);
        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 10, 0));
    }

    // D131. Without partial, search finds no match over 'b' (both engines). So a partial call's
    // answer must be a partial or none: a complete match would be one the plain search finds. The
    // port answers a complete (0, 0) with one deletion, and in Debug its second pass trips design
    // assert A1 ("a second-pass success read the text to come"), on main 4205ad63 as on this
    // branch. regex 2026.9.29 answers the partial (0, 1) with one substitution (measured
    // 2026-10-31). Which partial is right is not settled here; that it is not complete is.
    [Test]
    public void A_partial_call_does_not_answer_a_complete_match_the_plain_search_does_not_find()
    {
        var regex = new FuzzyRegex(@"(?:(?=(?:ab){e<=2})){d<=1}");
        regex.Match("b").Success.Should().BeFalse();

        Match m = regex.Match("b", partial: true);
        (!m.Success || m.PartialMatch).Should().BeTrue();
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
