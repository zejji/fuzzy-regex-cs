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
    // Each pass also deletes the 'x' of a fuzzy section inside the body, so the error count rises
    // on every lap while the groups cycle. Upstream: MemoryError for all five.
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))(?:x){d<=1})*$", "ab")]
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))(?:x){d})*$", "ab")]
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))(?:x){i<=1,d<=1})*$", "ab")]
    [Arguments(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))(?:x){d<=1}|(?=(?P<g>a)))*$", "ab")]
    [Arguments(@"(?b)^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))(?:x){d<=1})*$", "ab")]
    // The same inside a section around the loop, whose counts take the inner deletions in.
    // Upstream: MemoryError.
    [Arguments(@"(?:^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a))(?:x){d<=1})*$){e<=1}", "ab")]
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

    [Test]
    public void A_state_is_keyed_by_the_position_it_was_reached_at()
    {
        // Witness for the position in the key. Pass 1 sets g to (0, 1) at position 0 and fails
        // later; its sibling reads 'a' and sets g to (0, 0), and at position 1 the lookbehind sets g
        // back to (0, 1). Only from there can the backreference read the second 'a'. Keyed without
        // the position, that state looks like the one pass 1 reached, and the match is lost.
        // Upstream: (0, 2), g = (1, 2).
        Match m = new FuzzyRegex(
            @"^(?:(?=(?P<g>a)a)|^(?P<g>)a|(?<=(?P<g>a))|(?<=a)(?P=g))*$",
            FuzzyRegexOptions.None,
            _timeout
        ).Match("aa");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        (m.Groups["g"].Index, m.Groups["g"].Length).Should().Be((1, 1));
    }

    [Test]
    [Arguments(@"(?:(?:(?:b|(?(g)a)(?P=g))*(?P<g>)){0,2}(?=(?P<g>b))$){e<=1}", "abb", 3)]
    // A minimum makes an error useful, so a count cannot be cut for being higher than another.
    [Arguments(@"(?:(?:(?:b|(?(g)a)(?P=g))*(?P<g>)){0,2}(?=(?P<g>b))$){1<=e<=1}", "abb", 3)]
    [Arguments(@"(?:(?:(?:b|(?(g)a)(?P=g))*(?P<g>)){0,2}(?=(?P<g>b))$){1<=e<=1}", "abbb", 4)]
    // The loop inside an unbounded section, inside a bounded one: only the outer limit tells the
    // states apart, so the cap takes every section's limits (review round 2). No answer is known
    // to depend on it; these rows keep upstream's answer either way.
    [Arguments(@"(?:(?:(?:(?:b|(?(g)a)(?P=g))*(?P<g>)){0,2}){i}(?=(?P<g>b))$){i<=1}", "abb", 3)]
    [Arguments(@"(?:(?:(?:(?:b|(?(g)a)(?P=g))*(?P<g>)){0,2}){i}(?=(?P<g>b))$){i<=1}", "abbb", 4)]
    public void A_state_is_keyed_by_the_errors_the_open_section_has_made(string pattern, string subject, int end)
    {
        // Witness for the fuzzy counts in the key. The inner repeat reaches the same position and
        // spans with the section's one error spent and with it unspent. Keyed without the counts,
        // the two look alike, the one reached second is taken as already tried, and the first match
        // starts at 1 instead of 0. Upstream: (0, end) with one insertion, g = (end - 1, end).
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match(subject);

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, end));
        (m.FuzzyCounts.Insertions, m.FuzzyCounts.Total).Should().Be((1, 1));
        (m.Groups["g"].Index, m.Groups["g"].Length).Should().Be((end - 1, 1));
    }

    [Test]
    public void A_state_one_run_of_a_repeat_recorded_is_not_read_by_another()
    {
        // Witness for the run in the key. The inner repeat is entered once for each pass of the
        // outer one, at the same position with the same g, but the outer count differs, and so does
        // what can follow. Keyed without the run, the second entry reads the first entry's states as
        // already tried and the match is lost. Upstream: (0, 3), g = 'a'.
        Match m = new FuzzyRegex(@"^(?:(?:(?P=g)|(?=(?P<g>a)))*(?(g)b)){2}b$", FuzzyRegexOptions.None, _timeout).Match(
            "abb"
        );

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 3));
        m.Groups["g"].Value.Should().Be("a");
    }
}
