using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A greedy repeat with no maximum stops at a fuzzy iteration that deleted its way through the
/// body without moving through the text, where upstream repeats it until it runs out of memory.
/// </summary>
/// <remarks>
/// <para>
/// A fuzzy edit bumps <c>capture_change</c> (<c>upstream/src/_regex.c:10487</c>), and the repeat
/// guards are off under fuzzy matching (<c>:9596</c>), so END_GREEDY_REPEAT (<c>:12552</c>) reads an
/// iteration that only deleted as progress. Its one fuzzy exception stops the repeat at the end of
/// the slice, and nowhere else. <c>(?:(?:x){d&lt;=1})+y</c> over <c>y</c> therefore deletes an x,
/// finds that it moved, and deletes another, until MemoryError. Ledger entry 33.
/// </para>
/// <para>
/// The port stops such a repeat once it has its minimum, the same way upstream stops at the end of
/// the slice. So the answer here is the one upstream gives where its own rule applies:
/// <c>(?:(?:x){d&lt;=1})+</c> over the empty string is (0, 0) with deletions at [0, 1]. The minimum
/// is written out as copies of the body, so <c>+</c> is one body and then a loop whose first
/// iteration counts, which is why that is two deletions and not one. A bounded repeat is unchanged:
/// upstream finishes it and the port gives the same answer. So is an iteration that leaves anything a
/// later one could see - a referenced group whose span it changed, or an edit charged to the section
/// around the repeat, whose budget then ends the loop as upstream's does. Every expected value comes
/// from <c>regex</c> 2026.9.10, measured on 2026-09-23 and 2026-09-25 and quoted beside its assertion.
/// </para>
/// </remarks>
public sealed class FuzzyEmptyIterationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

    [Test]
    public void A_repeated_fuzzy_backreference_that_can_only_delete_finds_nothing()
    {
        // V1 search(r'(?i)(x)(?:(?:\1){d<=2})+$', 'xy'): None. Only deletions are allowed, so
        // nothing can consume the y. V0 and the pattern without IgnoreCase raise MemoryError.
        foreach (string pattern in new[] { @"(?V1)(?i)(x)(?:(?:\1){d<=2})+$", @"(?V0)(?i)(x)(?:(?:\1){d<=2})+$" })
        {
            new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match("xy").Success.Should().BeFalse();
        }
    }

    [Test]
    public void A_repeated_fuzzy_literal_that_can_only_delete_finds_nothing()
    {
        // V1 search(r'(x)(?:(?:x){d<=2})+$', 'xy'): MemoryError. The backreference form under V1
        // and IgnoreCase, which reaches the same text, is None (above).
        Match m = new FuzzyRegex(@"(x)(?:(?:x){d<=2})+$", FuzzyRegexOptions.None, _timeout).Match("xy");

        m.Success.Should().BeFalse();
    }

    [Test]
    public void An_unbounded_repeat_stops_at_an_iteration_that_only_deleted()
    {
        // V1 search(r'(?:(?:x){d<=1})+y', 'y'): MemoryError.
        // V1 search(r'(?:(?:x){d<=1})+', ''): span=(0, 0) counts=(0, 0, 2) changes=([], [], [0, 1]),
        // upstream's own stop at the end of the slice.
        Match m = new FuzzyRegex(@"(?:(?:x){d<=1})+y", FuzzyRegexOptions.None, _timeout).Match("y");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
        m.FuzzyChanges.Deletions.Should().Equal(0, 1);
    }

    [Test]
    public void A_reversed_unbounded_repeat_stops_the_same_way()
    {
        // V1 search(r'(?r)y(?:(?:x){d<=1})+', 'y'): MemoryError.
        // V1 search(r'(?r)(?:(?:x){d<=1})+y', 'y'): span=(0, 1) counts=(0, 0, 2), where the repeat
        // meets the start of the slice.
        Match m = new FuzzyRegex(@"(?r)y(?:(?:x){d<=1})+", FuzzyRegexOptions.None, _timeout).Match("y");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
    }

    [Test]
    public void A_larger_minimum_is_still_met_by_iterations_that_only_delete()
    {
        // V1 search(r'(?:(?:x){d<=1}){3,}y', 'xy'): MemoryError.
        // V1 search(r'(?:(?:x){d<=1}){3,}', 'x'): span=(0, 1) counts=(0, 0, 3), the stop at the end
        // of the slice. V1 search(r'(?:(?:x){d<=1}){3,3}y', 'xy') is two deletions; the loop adds
        // one more past the written-out minimum, as '+' does above.
        Match m = new FuzzyRegex(@"(?:(?:x){d<=1}){3,}y", FuzzyRegexOptions.None, _timeout).Match("xy");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 3));
    }

    [Test]
    public void An_iteration_that_only_sets_a_capture_still_counts_as_progress()
    {
        // V1 search(r'(?:(?(1)c|z)|())*(?:d){e<=1}', 'cd'): span=(0, 2) counts=(0, 0, 0). The
        // empty iteration sets group 1, which lets the next one match c.
        Match m = new FuzzyRegex(@"(?:(?(1)c|z)|())*(?:d){e<=1}", FuzzyRegexOptions.None, _timeout).Match("cd");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void An_iteration_that_deletes_and_sets_a_capture_still_counts_as_progress()
    {
        // V1 search(r'(?:(?(1)c|z)|()(?:x){d<=1})*$', 'c'): span=(0, 1) counts=(0, 0, 1)
        // changes=([], [], [0]). V1 search(r'(?:(?(1)c|z)|(?=())(?:x){d<=1})*$', 'c') is the same,
        // with the capture set inside a lookahead.
        foreach (string pattern in new[] { @"(?:(?(1)c|z)|()(?:x){d<=1})*$", @"(?:(?(1)c|z)|(?=())(?:x){d<=1})*$" })
        {
            Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, _timeout).Match("c");

            m.Success.Should().BeTrue();
            (m.Index, m.Length).Should().Be((0, 1));
            m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 1));
            m.FuzzyChanges.Deletions.Should().Equal(0);
        }
    }

    [Test]
    public void A_repeat_inside_a_section_stops_when_the_section_charged_the_iteration_nothing()
    {
        // V1 search(r'(?:(?:(?:x){d<=1})+y){e<=5}', 'y'): MemoryError, and this port ran to its 1 GB
        // backtracking limit until 2026-09-25. END_FUZZY adds the inner section's deletions to the
        // outer counts without checking the outer limit, so the outer budget never ends the loop.
        // V1 search(r'(?:(?:(?:x){d<=1})+){e<=5}', ''): span=(0, 0) counts=(0, 0, 2) changes=([], [],
        // [0, 1]), upstream's own stop at the end of the slice, and the answer this gives before 'y'.
        Match m = new FuzzyRegex(@"(?:(?:(?:x){d<=1})+y){e<=5}", FuzzyRegexOptions.None, _timeout).Match("y");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 2));
        m.FuzzyChanges.Deletions.Should().Equal(0, 1);
    }

    [Test]
    public void A_repeat_inside_a_section_that_charges_it_keeps_upstreams_answer()
    {
        // V1 search(r'(?:(?:a(?:x){d<=1})+y){d<=9}', 'y'): span=(0, 1) counts=(0, 0, 8) changes=([],
        // [], [0, 1, 2, 3, 4, 5, 6, 7]). Each iteration deletes an 'a' from the enclosing section's own
        // budget, so the budget ends the loop and the stop must not: this is the half that keeps it
        // off. The budget is 9 because at 5 upstream's budget ends the loop at 4 deletions, which is
        // also where a wrongly applied stop would end it - `+` is one copy of the body and a loop.
        Match m = new FuzzyRegex(@"(?:(?:a(?:x){d<=1})+y){d<=9}", FuzzyRegexOptions.None, _timeout).Match("y");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 8));
        m.FuzzyChanges.Deletions.Should().Equal(0, 1, 2, 3, 4, 5, 6, 7);
    }

    [Test]
    public void An_iteration_that_sets_a_capture_to_the_span_it_already_had_stops_the_repeat()
    {
        // V1 search(r'(?:(?(1)c|z)|()(?:x){d<=1})+d', 'cd'): MemoryError, and this port ran to its
        // 1 GB limit until 2026-09-25. At 1 the first pass moves group 1 to (1, 1), which a later pass
        // could test, so it goes on; the next pass sets it to (1, 1) again and deletes, which changes
        // nothing a later pass can see, so the repeat stops there and 'd' matches. Its end-of-slice
        // form, V1 search(r'(?:(?(1)c|z)|()(?:x){d<=1})+', 'c'), is span=(0, 1) with one deletion.
        // The three deletions are at 0, 1 and 1, and a deletion is reported past the ones before it
        // (ledger entry 26's "raw" form), so the list reads 0, 2, 3.
        Match m = new FuzzyRegex(@"(?:(?(1)c|z)|()(?:x){d<=1})+d", FuzzyRegexOptions.None, _timeout).Match("cd");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 3));
        m.FuzzyChanges.Deletions.Should().Equal(0, 2, 3);
    }

    [Test]
    public void A_bounded_repeat_keeps_upstreams_answer()
    {
        // V1 search(r'(?:(?:x){d<=1}){1,3}y', 'y'): span=(0, 1) counts=(0, 0, 3)
        Match m = new FuzzyRegex(@"(?:(?:x){d<=1}){1,3}y", FuzzyRegexOptions.None, _timeout).Match("y");

        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 3));
    }
}
