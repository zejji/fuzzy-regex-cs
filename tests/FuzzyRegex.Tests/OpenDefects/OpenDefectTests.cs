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
    // Matrix wave triage (docs/plan/2026-09-27-matrix-wave-triage.md), seed 4242 row 1719,
    // minimised. The grammar G -> '' | G 'a' generates 'a', 'aa', 'aaa' and so on, so both subjects
    // are in its language. Matching 'aa' needs a call of G nested inside another call of G at the
    // same text position, and the innermost call takes the empty branch, so the path is finite.
    // Upstream finds (0, 2) for all three, and so does PCRE2 10.47 for the named-group form
    // (`^(?<g>|(?&g)a)$` over 'aa', 'aaa', 'aaaa'). The port refuses any call of a group at a
    // position where a call of it is already open (Matcher.cs, Opcode.GroupCall) and answers None,
    // None and (1, 2). The third row is the reversed form, which is how the wave reached it.
    [Test]
    [Arguments("(?:|(?R)a)", false)]
    [Arguments("(?r)(?:|a(?R))", false)]
    [Arguments("(?P<g>|(?&g)a)$", true)]
    public void A_left_recursive_call_nested_at_one_position_still_finds_its_match(string pattern, bool search)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = search ? regex.Match("aa") : regex.FullMatch("aa");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
    }
}
