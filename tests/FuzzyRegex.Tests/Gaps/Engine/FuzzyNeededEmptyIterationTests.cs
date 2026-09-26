using System.Diagnostics;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The "needed" rule for a repeat iteration that consumed no text and spent fuzzy errors, and the
/// repeat memo that keeps it fast.
/// </summary>
/// <remarks>
/// <para>
/// Upstream counts every fuzzy edit as progress in a repeat (<c>upstream/src/_regex.c</c>:12550-12557
/// with :10487), including edits since undone, and stops only at the end of the text, so how many
/// deleting iterations a loop takes depends on what follows it, and where a section inside the body
/// restarts its budget it loops until MemoryError. The port admits such an iteration only if the
/// repeat is below its minimum, its deletions raise an unmet <c>d</c> or <c>e</c> minimum of an open
/// section, or it changed a group a backreference or conditional tests; and it drops a path whose
/// state after an iteration an earlier path through the same run of the repeat already reached.
/// Evidence and the four blind reviews: <c>docs/plan/2026-09-26-empty-iteration-survey.md</c>. Every
/// answer below equals <c>tools/probes/fuzzy-reference-matcher.py</c> in mode "needed"; the upstream
/// answers were measured on <c>regex</c> 2026.9.10 on 2026-09-26.
/// </para>
/// </remarks>
public sealed class FuzzyNeededEmptyIterationTests
{
    private static void ShouldMatch(Match m, int index, int length, FuzzyCounts counts)
    {
        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((index, length));
        m.FuzzyCounts.Should().Be(counts);
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void An_error_nothing_needs_is_not_spent()
    {
        // search('(?:[0-9]+){d<=2}', '42kg')              (0, 2) (0, 0, 2)
        // search('(?:(?:[0-9]+,){d<=1})+end', '12,end')   (0, 6) (0, 0, 1)
        // match('(?:ac+){d<=2}', 'a')                     (0, 1) (0, 0, 2)
        // fullmatch('(?:b+){d<=1}', 'bb')                 (0, 2) (0, 0, 0), the same
        ShouldMatch(new FuzzyRegex("(?:[0-9]+){d<=2}").Match("42kg"), 0, 2, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?:[0-9]+){d<=2}").Match("42"), 0, 2, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?:(?:[0-9]+,){d<=1})+end").Match("12,end"), 0, 6, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?:ac+){d<=2}").MatchAtStart("a"), 0, 1, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:b+){d<=1}").FullMatch("bb"), 0, 2, new FuzzyCounts(0, 0, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10 (MemoryError there), and this test pins OUR answer.
    [Test]
    public void A_required_iteration_is_deleted_once()
    {
        // search('(?:(?:[0-9]+,){d<=3})+end', 'end')   MemoryError
        ShouldMatch(new FuzzyRegex("(?:(?:[0-9]+,){d<=3})+end").Match("end"), 0, 3, new FuzzyCounts(0, 0, 2));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer: rule (b), a deletion that an
    // unmet minimum error count needs.
    [Test]
    public void A_section_minimum_admits_the_deletions_it_needs()
    {
        // search('(?:[0-9]+){1<=d<=2}', '42kg')         (0, 2) (0, 0, 2)
        // search('(?:[0-9]+){1<=d<=2}', '42')           (1, 2) (0, 0, 1)
        // match('(?:b*){1<=d<=2}', 'bba')               (0, 2) (0, 0, 2)
        // fullmatch('(?:(?:^z|aa)*){1<=d<=1}', 'aa')    (0, 2) (0, 0, 1), the same (third review)
        ShouldMatch(new FuzzyRegex("(?:[0-9]+){1<=d<=2}").Match("42kg"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:[0-9]+){1<=d<=2}").Match("42"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:b*){1<=d<=2}").MatchAtStart("bba"), 0, 2, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:(?:^z|aa)*){1<=d<=1}").FullMatch("aa"), 0, 2, new FuzzyCounts(0, 0, 1));
    }

    // AGREES WITH UPSTREAM 2026.9.10: a minimum deletions cannot raise admits nothing, so the
    // unbounded deletion budget does not loop.
    [Test]
    public void A_minimum_deletions_cannot_raise_admits_nothing()
    {
        // fullmatch('(?:b*){1<=s<=1,d}', '')   None
        var watch = Stopwatch.StartNew();
        new FuzzyRegex("(?:b*){1<=s<=1,d}", FuzzyRegexOptions.None, TimeSpan.FromSeconds(10))
            .FullMatch("")
            .Success.Should()
            .BeFalse();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    // Rule (c): an iteration that deletes and changes a tested group is progress. Entry 33's case.
    // DIVERGES FROM UPSTREAM 2026.9.10 in the count: search('(?:(?(1)c|z)|()(?:x){d<=1})*$', 'c')
    // is (0, 1) (0, 0, 1) there; after the c, a pass at the end deletes x and moves group 1 from
    // (0, 0) to (1, 1), a change to a tested group, which upstream skips only because it is at the
    // end of the text.
    [Test]
    public void A_deletion_that_changes_a_tested_group_is_progress()
    {
        Match m = new FuzzyRegex(@"(?:(?(1)c|z)|()(?:x){d<=1})*$").Match("c");
        ShouldMatch(m, 0, 1, new FuzzyCounts(0, 0, 2));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((1, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10 (MemoryError there), and this test pins OUR answer.
    [Test]
    public void A_backreference_to_a_group_set_by_a_deleting_iteration_is_found()
    {
        // search('(?:(b?)(?:x){d<=1})*\1c', 'bc')   MemoryError
        ShouldMatch(new FuzzyRegex(@"(?:(b?)(?:x){d<=1})*\1c").Match("bc"), 0, 2, new FuzzyCounts(0, 0, 2));
    }

    // AGREES WITH UPSTREAM 2026.9.10: an error-free empty iteration keeps upstream's rule, where a
    // changed tested group is progress (the open question in the survey note).
    [Test]
    public void An_error_free_empty_iteration_keeps_upstreams_rule()
    {
        // search('^(?:(?(1)c|z)|())*$', 'c')   (0, 1)
        ShouldMatch(new FuzzyRegex(@"^(?:(?(1)c|z)|())*$").Match("c"), 0, 1, new FuzzyCounts(0, 0, 0));
    }

    // Upstream raises MemoryError on both; main (b70ba32, 2d6249b) threw InvalidOperationException
    // after about 2 seconds when the backtracking stack reached its bound.
    [Test]
    public void Two_shapes_that_exhausted_the_stack_answer_at_once()
    {
        var watch = Stopwatch.StartNew();
        new FuzzyRegex("(?:(?:b?)*){d<=1}", FuzzyRegexOptions.None, TimeSpan.FromSeconds(10))
            .FullMatch("a")
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("(?fi)(?:(?:a){e<=1})+?(?=c)", FuzzyRegexOptions.None, TimeSpan.FromSeconds(10))
            .Match("σ")
            .Success.Should()
            .BeFalse();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    // The repeat memo: each empty iteration can delete a, b, c or d, and all four lead to the same
    // state, so without it the search explores 4^n paths. Upstream takes 1.28 s at n = 11.
    [Test]
    public void Paths_that_reach_the_same_state_are_explored_once()
    {
        var watch = Stopwatch.StartNew();
        new FuzzyRegex("(?:(?:a|b|c|d)*){12<=d<=12}", FuzzyRegexOptions.None, TimeSpan.FromSeconds(10))
            .FullMatch("x")
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("(?:(?:(?:a|b|c|d)*)*){3<=d<=3}", FuzzyRegexOptions.None, TimeSpan.FromSeconds(10))
            .FullMatch(new string('a', 32) + "x")
            .Success.Should()
            .BeFalse();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    // Rule (b) through a nested section: the outer section's minimum counts the inner section's
    // deletions, which END_FUZZY adds to it.
    [Test]
    public void An_enclosing_sections_minimum_admits_an_inner_deletion()
    {
        // fullmatch('(?:(?:(?:b){d<=1})*){1<=d<=1}', '')   (0, 0) (0, 0, 1), the same
        ShouldMatch(new FuzzyRegex("(?:(?:(?:b){d<=1})*){1<=d<=1}").FullMatch(""), 0, 0, new FuzzyCounts(0, 0, 1));
    }

    // The walk out through the enclosing sections that rule (b) and the narrowing make read each
    // entry from the section's own frame on the structure stack. It looped for ever, ignoring the
    // match timeout, when a verb dropped the frames that restored a per-section record, or a
    // recursive call entered a section inside itself (blind review of af59be7, 2026-09-26). The
    // verb rows answer None: the second iteration's empty deletion is not needed, it fails, and
    // backtracking into its (*PRUNE) or (*SKIP) ends the attempt at every start, which is also the
    // reference matcher's answer. Upstream raises MemoryError on the recursion rows.
    [Test]
    public async Task Verbs_and_recursion_in_sections_with_a_minimum_answer_in_bounded_time()
    {
        foreach (
            string pattern in new[]
            {
                "(?:(?:(*PRUNE)a){1<=d<=1})+",
                "(?:(?:(*SKIP)a){1<=e<=2})+",
                "(?:(?:(*SKIP)a){1<=e<=2}){2,}",
            }
        )
        {
            (await Answer(pattern, "c").ConfigureAwait(false)).Should().BeNull(pattern);
        }

        (await Answer("(?:b(?R)?){d<=1}(?:a){1<=d<=1}", "bb").ConfigureAwait(false)).Should().NotBeNull();
        (await Answer("(?:b(?R)?(?:a){1<=d<=2}){e<=3}", "bb").ConfigureAwait(false)).Should().NotBeNull();
    }

    /// <summary>
    /// Searches on a worker thread and throws <see cref="TimeoutException"/> if it does not finish in
    /// five seconds, so a loop that ignores the match timeout turns the test red rather than hanging
    /// the suite.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <returns>The match, or <see langword="null"/> for none.</returns>
    private static async Task<Match?> Answer(string pattern, string subject)
    {
        Match m = await Task.Run(() =>
                new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(2)).Match(subject)
            )
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        return m.Success ? m : null;
    }
}
