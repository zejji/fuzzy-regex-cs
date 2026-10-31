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
}
