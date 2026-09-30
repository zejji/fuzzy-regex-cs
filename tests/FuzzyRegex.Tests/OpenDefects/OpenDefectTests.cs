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
    // Queue item 9. A negative lookahead succeeds only when its body fails, so nothing its body
    // captured survives; group 1 keeps only its own capture. Upstream agrees when the body spells
    // the group out (`(?!.(?:a))` gives ['a']) but keeps an entry when the body calls it (['a', 'a']).
    [Test]
    public void A_group_call_inside_a_failed_lookaround_leaves_no_capture_behind()
    {
        Match m = new FuzzyRegex("(a)(?:(?!.(?1))|.)+?b").Match("aaab");

        m.Success.Should().BeTrue();
        m.Groups[1].Captures.Select(static c => (c.Index, c.Length)).Should().Equal((0, 1));
    }

    // D4 (the capture-dependent recursion design, section 6b). The `.*z` branch fails only after
    // reading the whole text, so the attempt's reach is already full when the first call is made,
    // no later call can show growth, and the finite left recursion `(?:|(?R)a)` is refused one
    // level down. Upstream 2026.9.10 answers (0, 2) on both rows; PCRE2 10.47 raises "nested
    // recursion at the same subject position" on the first. Without the `.*z|` branch the port
    // answers (0, 2) as well (GroupCallTests).
    [Test]
    [Arguments(@".*z|(?:|(?R)a)", "aa")]
    [Arguments(@"(?:a|(?<g>))*?(?P=g)|(?<g>)a(?R)|(?R)(?P=g)x", "ax")]
    public void A_left_recursion_is_let_through_when_the_reach_is_full_before_the_first_call(
        string pattern,
        string subject
    )
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(30)).FullMatch(subject);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 2));
    }

    // D43 (not fixed yet). A conditional's lookbehind test runs backwards, so a call in it runs its group backwards,
    // as the test written out does. DIVERGES FROM UPSTREAM, which runs the call forwards and
    // answers None on both called forms (regex 2026.9.10, 2026-09-30):
    //   search('bc(?(?<=bc)x|y)', 'bcxzzz')   (0, 3)
    //   search('bc(?(?<!bc)x|y)', 'bcyzzz')   (0, 3)
    // The 'zzz' keeps the subject longer than the call's inflated minimum width (GroupCallTests),
    // which would refuse 'bcx' before matching starts, in both engines.
    [Test]
    public void A_call_in_a_conditional_lookbehind_test_runs_backwards()
    {
        Match m = new FuzzyRegex("bc(?(?<=(?&g))x|y)(?P<g>bc)?").Match("bcxzzz");

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 3));
    }
}
