using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The failed-call memo: a group call whose entry key matches one that ran out of choices without
/// returning fails at once. See <c>Matcher.FailedCallKey</c>, <c>PatternObject.UseCallMemo</c>
/// and <c>docs/plan/2026-09-27-recursion-failure-memo-design.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The speed tests are the two rows that made the memo necessary. Ledger entry 42's exact-deletion
/// retry multiplies the ways into each recursive call, and every call on both rows fails, so they
/// took over a minute and 8 seconds. Upstream regex 2026.9.10 raises <c>MemoryError</c> on both,
/// so it cannot grade them; "no match" is the answer the port gives with the retry off, and with
/// the memo off on every prefix short enough to finish.
/// </para>
/// <para>
/// The witness tests are the other half. Each is an answer that changes if one of the memo's
/// safeguards is deleted, checked by deleting it (2026-09-27), so a mutation that widens the memo
/// goes red rather than silently changing an answer. They switch the memo on from the first call
/// (<c>PatternObject.EagerCallMemo</c>), since a subject this short never makes
/// enough calls to switch it on by itself, and compare with the memo off
/// (<c>PatternObject.SkipCallMemo</c>).
/// </para>
/// </remarks>
public sealed class FailedCallMemoTests
{
    /// <summary>
    /// Fifty times what the rows take on a first run, JIT included, and a small fraction of what
    /// they took without the memo, so the test is stable and a regression is still red.
    /// </summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

    private const string _rowA = "(|)(?:(?:(?:(?:.)+((?:(?R)){2,}|)){2<=e<=3}(?=b))){1<=s<=1,1<=d<=2}";

    [Test]
    public void A_fuzzy_recursion_whose_calls_all_fail_is_searched_in_milliseconds()
    {
        // 69.5 s before the memo, 889,000 calls at four characters and only 2,390 different ones.
        var regex = new FuzzyRegex(_rowA, FuzzyRegexOptions.None, _timeout);

        regex.Match("baxbax").Success.Should().BeFalse();
    }

    [Test]
    public void A_best_match_fuzzy_recursion_whose_calls_all_fail_is_fullmatched_in_milliseconds()
    {
        // 8.2 s before the memo.
        var regex = new FuzzyRegex(
            "(?b)(?:(?:.(?:(?:(?:b)+(?R)||)){1<=e<=2}(?:c)*?){2<=d<=3})",
            FuzzyRegexOptions.None,
            _timeout
        );

        regex.FullMatch("xxaxabxx").Success.Should().BeFalse();
    }

    [Test]
    public void The_failed_calls_grow_polynomially_with_the_subject()
    {
        // Without the memo each character multiplied row A's time by about 11, so 15 characters
        // would take years. With it, the number of different failed calls grows about as n^4.
        var regex = new FuzzyRegex(_rowA, FuzzyRegexOptions.None, TimeSpan.FromSeconds(5));

        Action search = () => regex.Match("baxbaxbaxbaxbax");

        search.Should().NotThrow();
    }

    [Test]
    [Arguments(@"(?b)(?:.??(?1)|z)(?:q){e<=1}(?(DEFINE)(\Ga))")]
    [Arguments(@"(?b)(?:.??(?1)|z)(?:q){e<=1}(?:|(\Ga))")]
    public void Each_best_match_pass_has_its_own_set_of_failed_calls(string pattern)
    {
        // \G and the rule against an insertion where a search began both read the search anchor,
        // which is not in the key, and every pass of the best-match walk sets it afresh. With one
        // set for the whole walk, the second pass fails the call of (\Ga) at 1 because it failed
        // in an earlier pass anchored elsewhere, and the answer was (0, 2) with a substitution.
        // Upstream: regex.search(pattern, 'zaq') spans (1, 3), fuzzy_counts (0, 0, 0), in both
        // rows, regex 2026.9.10.
        FuzzyRegex regex = WithMemo(pattern, eager: true);

        Match m = regex.Match("zaq");

        regex.PatternObject.UseCallMemo.Should().BeTrue();
        (m.Index, m.Length).Should().Be((1, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void A_partial_search_does_not_inherit_the_failed_calls_of_the_full_pass_before_it()
    {
        // A partial search first tries for a whole match, then searches again allowing one that
        // runs off the end. A call that failed in the first pass can succeed in the second, so
        // failures from the first would lose the partial match: with one set for both passes,
        // this answered (1, 2). The memo is also off in the partial pass itself, as the repeat
        // memo is, although no row that needs that has been found. Upstream:
        // regex.search(r'(((b)(?R))){i<=1}', 'ba', partial=True) spans (0, 2), partial, group 3
        // (0, 1), regex 2026.9.10.
        FuzzyRegex regex = WithMemo("(((b)(?R))){i<=1}", eager: true);

        Match m = regex.Match("ba", partial: true);

        regex.PatternObject.UseCallMemo.Should().BeTrue();
        (m.Index, m.Length, m.PartialMatch).Should().Be((0, 2, true));
        (m.Groups[3].Index, m.Groups[3].Length).Should().Be((0, 1));
    }

    [Test]
    public void A_call_inside_a_lookaround_keeps_the_memo_off()
    {
        // When a lookaround succeeds it throws away the undo entries of the captures made inside
        // it, so a capture list keeps an entry from a path that later failed. Skipping a failed
        // call leaves that entry out: with the memo forced on, group 1's list here loses an entry.
        // Spans and counts do not change, so only the capture lists can show it.
        const string pattern = "(?r)(((?R)?R(?!.(?)(?R))(.))){2<=e<3}";

        FuzzyRegex on = WithMemo(pattern, eager: true);
        FuzzyRegex off = WithMemo(pattern, eager: false);

        on.PatternObject.UseCallMemo.Should().BeFalse();
        CaptureLists(on.Match("aa")).Should().Equal(CaptureLists(off.Match("aa")));
    }

    [Test]
    public void A_call_inside_a_lookbehind_keeps_the_memo_off()
    {
        // A lookbehind runs the pattern backwards, so a call inside it can meet open calls behind
        // the position, which the key does not hold. The exclusion for a call inside any
        // lookaround covers it. This row does not go wrong with the memo forced on; it pins
        // the direction. Upstream: regex.search(r'(a)b(?<=(?1)b)', 'ab') is None, regex
        // 2026.9.10.
        FuzzyRegex regex = WithMemo("(a)b(?<=(?1)b)", eager: true);

        regex.PatternObject.UseCallMemo.Should().BeFalse();
        regex.Match("ab").Success.Should().BeFalse();
    }

    /// <summary>A pattern compiled for one test, with the memo on from the first call, or off.</summary>
    private static FuzzyRegex WithMemo(string pattern, bool eager)
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout);
        regex.PatternObject.EagerCallMemo = eager;
        regex.PatternObject.SkipCallMemo = !eager;
        return regex;
    }

    /// <summary>Every group's capture list, as (index, length) pairs.</summary>
    private static List<string> CaptureLists(Match m) =>
        [
            .. Enumerable
                .Range(0, m.Groups.Count)
                .Select(g => string.Join(" ", m.Groups[g].Captures.Select(static c => $"({c.Index},{c.Length})"))),
        ];
}
