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

    // D17, found by D12's survey. An empty iteration counts as progress when it changes the span of
    // a tested group (upstream's rule, kept by D12). Here each pass flips group g between (0, 1) and
    // (0, 2), so every pass is a change and the repeat never stops. No pass reads text and '$'
    // cannot hold at 0 in 'ab', so the answer is no match, which PCRE2 10.47 and Perl 5.42 give at
    // once. Upstream: MemoryError after 1.4 s. The port exhausts its 1 GB backtrack stack.
    [Test]
    public void An_empty_iteration_that_flips_a_tested_group_between_two_spans_stops()
    {
        new FuzzyRegex(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*$", FuzzyRegexOptions.None, TimeSpan.FromSeconds(5))
            .Match("ab")
            .Success.Should()
            .BeFalse();
    }
}
