using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// An empty iteration that changes a tested group is progress (upstream's rule, kept by D12 and
/// pinned by <see cref="EmptyIterationGroupProgressTests"/>) only if the state it leads to is new in
/// this run of the repeat. A body that flips a group between spans at one position therefore stops
/// once it comes back to a state it has already reached (known defect D17, fixed 2026-09-29).
/// </summary>
/// <remarks>
/// <para>
/// Upstream has no such check: every row marked "upstream: MemoryError" loops until it runs out of
/// memory (<c>regex</c> 2026.9.10, 2026-09-29). Where upstream does answer, the answer is quoted and
/// kept. The rule, and why it changes no answer upstream reaches, is in
/// <c>docs/plan/2026-09-29-d17-empty-iteration-cycle.md</c>; the check is
/// <c>Matcher.RevisitsEmptyIterationState</c>.
/// </para>
/// <para>
/// No pass of these loops reads text, so the expected answers follow from the patterns alone. For
/// the first row PCRE2 10.47 (with <c>(?J)</c>) and Perl 5.42 answer no match at once (D12 survey).
/// </para>
/// </remarks>
public sealed class EmptyIterationCycleTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Test]
    // Two states: g is (0, 1), then (0, 2), then (0, 1) again. Upstream: MemoryError.
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*$", "ab")]
    // Three states: (0, 1), (0, 2), (0, 3), (0, 1). Upstream: MemoryError.
    [Arguments(@"^(?:(?=(?P=g)c)(?=(?P<g>abc))|(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*$", "abc")]
    // The body tests g with a conditional instead of a backreference. Upstream: MemoryError.
    [Arguments(@"^(?:(?(g)(?=(?P<g>ab))|(?=(?P<g>a)))|(?=(?P<g>a)))*$", "ab")]
    // The lazy form tries the tail first, then goes round the same cycle. Upstream: MemoryError.
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*?$", "ab")]
    // A minimum above the cycle's length. Upstream: MemoryError.
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))){3,}$", "ab")]
    // The cycle inside a repeat that is itself repeated. Upstream: MemoryError.
    [Arguments(@"^(?:(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*)*$", "ab")]
    // Inside a fuzzy section: one error cannot move past both letters. Upstream: MemoryError.
    [Arguments(@"(?:^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*$){e<=1}", "ab")]
    // A fuzzy section after the loop: 'ab' is two edits from 'c'. Upstream: MemoryError.
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*(?:c){e<=1}$", "ab")]
    // Reversed, with lookbehinds; a reversed body runs right to left, so the test comes second.
    // Upstream: MemoryError.
    [Arguments(@"(?r)^(?:(?<=(?P<g>ab))(?<=a(?P=g))|(?<=(?P<g>b)))*$", "ab")]
    public void A_loop_of_empty_iterations_that_cycles_a_tested_group_ends_in_no_match(string pattern, string subject)
    {
        new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match(subject).Success.Should().BeFalse();
    }

    [Test]
    // The tail tests g with a conditional: with g set it needs 'x', so only the pass with no
    // iterations matches, and it leaves g unset. Upstream: MemoryError.
    [Arguments(@"^(?:(?=(?P<g>a))|(?=(?P<g>ab)))*(?(g)x)", "ab", 0, 0, null)]
    [Arguments(@"^(?:(?=(?P<g>a))|(?=(?P<g>ab)))*(?(g)x|ab)", "ab", 0, 2, null)]
    // Reversed and unanchored: the loop stops where it comes back to g = (1, 2), and the rest of
    // the pattern is empty, so that state is the answer. Perl's rule stops after the first pass,
    // with the same g. Upstream: MemoryError.
    [Arguments(@"(?r)(?:(?<=(?P<g>ab))(?<=a(?P=g))|(?<=(?P<g>b)))*$", "ab", 2, 2, "b")]
    [Arguments(@"(?r)(?:(?=(?P<g>ab))(?=(?P=g)b)|(?=(?P<g>a)))*^", "ab", 0, 0, "a")]
    // After the cycle stops, the alternatives still waiting are tried: pass 3 falls back to 'a',
    // then 'b' is read, and g keeps the (0, 2) that pass 2 gave it. Upstream: MemoryError.
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))|a|b)*$", "ab", 0, 2, "ab")]
    public void A_loop_that_cycles_a_tested_group_stops_and_the_rest_of_the_pattern_decides(
        string pattern,
        string subject,
        int start,
        int end,
        string? group
    )
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.Groups["g"].Success.Should().Be(group is not null);
        if (group is not null)
        {
            m.Groups["g"].Value.Should().Be(group);
        }
    }

    [Test]
    // Bounded repeats end where upstream ends them; the count keeps every state on a path apart.
    // Upstream: None, and (0, 2) with g = 'ab'.
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))){0,6}$", "ab", false)]
    [Arguments(@"^(?:(?=(?P<g>a))|(?=(?P<g>ab))|(?P=g)){0,2}$", "ab", true)]
    public void A_bounded_repeat_keeps_upstreams_answer(string pattern, string subject, bool matches)
    {
        // The second row is the witness for the count in the key. Pass 1 sets g to (0, 1) and
        // pass 2 to (0, 2), at the maximum, so nothing follows. Backtracking to pass 1 reaches
        // g = (0, 2) at count 1, where pass 2 can read 'ab' through the backreference. Keyed
        // without the count, that state looks already tried and the match is lost.
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match(subject);

        m.Success.Should().Be(matches);
        if (matches)
        {
            (m.Index, m.Length).Should().Be((0, 2));
            m.Groups["g"].Value.Should().Be("ab");
        }
    }
}
