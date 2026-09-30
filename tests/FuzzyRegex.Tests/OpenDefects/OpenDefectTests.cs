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

    // D38. Over 'aba' this pattern matches (0, 3) with one insertion, here and upstream, so 'ab' is
    // a partial match at start 0, and MatchAtStart there says so. Search reports start 1 instead: the
    // attempt at 0 fails with only the end-of-text flag set (the `\b` in the condition's test is
    // decided at the end), and DoMatch reads that flag only when every start has failed, so a real
    // partial at start 1 wins. The leftmost start wins in a search. Upstream 2026.9.10 answers
    // (1, 2) for the search and None for match(partial=True).
    [Test]
    public void A_partial_search_reports_the_leftmost_start_that_the_anchored_matcher_finds()
    {
        var regex = new FuzzyRegex(@"(?:(?(?=a\b|a)a|b)){i<=1}a");

        Match m = regex.Match("ab", 0, 2, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 2));
    }
}
