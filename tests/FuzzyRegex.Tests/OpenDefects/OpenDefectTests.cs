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
}
