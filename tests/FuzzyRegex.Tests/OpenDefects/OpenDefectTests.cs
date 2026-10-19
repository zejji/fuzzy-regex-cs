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
    /// <summary>
    /// D104: since the D90 fix a repeat inside a blocked lookahead keeps no record of the
    /// continuation failing, so where the lookahead captures a group that a group condition reads,
    /// the second pass explores until the fork budget stops it and answers the budget's partial at
    /// the attempt start, (0, 10). The expected answers are the guards-off build's with no budget,
    /// which main before D90 also gave. Records kept under their instance
    /// (<c>MatchState.CutBodyKeyOf</c>) bring back 30 of the 40 grid rows and the register's
    /// witness (<c>PartialBlockedEndGuardTests</c>); these 10 still exhaust the budget. A likely
    /// cause, not yet tested: <c>(b?)</c> captures an empty span at every position the inner repeat
    /// can stop at, and the key holds that exact span where a later capture could equal it (the
    /// capture-change counter reads it), so each lookahead hands its continuation one state per
    /// position.
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
    public void D104_a_lookahead_capture_read_by_a_condition_keeps_the_exact_partial(
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
}
