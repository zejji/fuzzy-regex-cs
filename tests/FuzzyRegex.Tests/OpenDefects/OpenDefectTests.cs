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

    // Blind review of the refined call guard, 2026-09-28 (ledger entry 14, "The guard refined").
    // Each pattern needs two calls of itself open at 0 at once. The second reaches no new text, but
    // a group set between the two calls changes what it does: a conditional or backreference reads
    // that group, so the inner call is not a repeat of the outer one and the path is finite.
    // Upstream 2026.9.10 answers (0, 1) for every row, search and fullmatch alike (measured
    // 2026-09-28); PCRE2 10.47 raises "nested recursion at the same subject position" on the first.
    // The port answers None, because the guard's key holds no capture state.
    [Test]
    [Arguments(@"(?(a)(?(b)x|(?<b>)(?R))|(?<a>)(?R))")]
    [Arguments(@"(?:\1x|\2()(?R)|()(?R))")]
    [Arguments(@"(?:(?P=b)x|(?P=a)(?<b>)(?R)|(?<a>)(?R))")]
    public void A_call_that_reaches_nothing_new_but_sees_a_new_capture_is_let_through(string pattern)
    {
        var regex = new FuzzyRegex(pattern);

        Match search = regex.Match("x");
        Match full = regex.FullMatch("x");

        (search.Success, search.Index, search.Length).Should().Be((true, 0, 1));
        (full.Success, full.Index, full.Length).Should().Be((true, 0, 1));
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
