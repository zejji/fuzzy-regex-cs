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
/// repeat is below its minimum, its edits raise an unmet minimum of an open
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

    // The rule tries one more pass of a loop at the end of the text, where upstream's end-of-text
    // stop did not, and a (*SKIP) in that pass runs. It acts only if backtracking reaches it while
    // the attempt fails (ledger entry 45): with a (*PRUNE) after it, the (*PRUNE) is reached first
    // and the next attempt starts one character on, so the first row matches at 2; without one, the
    // (*SKIP) at 3 is reached and the next attempt starts at 3, so the second finds nothing. Perl
    // 5.42.3 and PCRE2 10.47 give both answers for the same paths spelt without fuzzy matching
    // (tools/probes/skip-then-prune-perl-pcre2.py), and so does the reference matcher. Upstream
    // gives (2, 3) for both, the second because its end-of-text stop never runs that pass.
    [Test]
    public void A_skip_in_the_pass_at_the_end_of_the_text_acts_only_when_backtracking_reaches_it()
    {
        Match m = new FuzzyRegex("(?:(?:(*SKIP)a*(*PRUNE)){1<=s<=1,d<=2})*?b").Match("aab");
        m.Success.Should().BeTrue();
        (m.Index, m.Length).Should().Be((2, 1));

        new FuzzyRegex("(?:(?:(*SKIP)a*){1<=s<=1,d<=2})*?b").Match("aab").Success.Should().BeFalse();
    }

    // A lookaround in the body can substitute or insert and still leave the iteration empty, so the
    // rule asks about each kind of edit on its own. Taking the substitution for a deletion drove the
    // deletion count below zero, below every d minimum, and admitted every further iteration until
    // the backtracking stack reached its bound (blind review of 7277fb6, 2026-09-27). Upstream
    // raises MemoryError on all three. The first row is main's answer; in the others the section's
    // e minimum admits two substituting iterations, as it admits two deleting ones in
    // (?:(?:b)*){2<=d<=2}, where main's stop answered None.
    [Test]
    public void An_iteration_that_substitutes_inside_a_lookaround_is_admitted_only_while_needed()
    {
        var watch = Stopwatch.StartNew();
        ShouldMatch(
            new FuzzyRegex("(?:(?=b{e<=1})*){1<=e<=2}", FuzzyRegexOptions.None, TimeSpan.FromSeconds(10)).Match("c"),
            0,
            0,
            new FuzzyCounts(1, 0, 0)
        );
        var twoSubstitutions = new FuzzyRegex(
            "(?:(?:(?=(?:ab){1<=e<=1}))*+){2<=e<=3}",
            FuzzyRegexOptions.None,
            TimeSpan.FromSeconds(10)
        );
        ShouldMatch(twoSubstitutions.Match("ac"), 0, 0, new FuzzyCounts(2, 0, 0));
        ShouldMatch(twoSubstitutions.Match("bb"), 0, 0, new FuzzyCounts(2, 0, 0));
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and these rows pin OUR answer: an alternative written empty
    // is the exit of an optional, the same as the zero iterations of X?, so the needed rule covers
    // (?:X|) as it covers X? (ledger entry 44's addendum; option A of
    // docs/plan/2026-09-28-optional-vs-empty-alternative-ruling.md). A pass through an earlier
    // alternative that consumed no text and spent errors nothing needs fails, and the empty
    // alternative then matches with none. Upstream answers one error more on every row, and so did
    // this port until 2026-09-28; the X? spelling of each row already gave this answer.
    [Test]
    [Arguments("(?:a|){d<=1}", "", 0, 0)]
    [Arguments("(?:a|){e<=1}", "", 0, 0)]
    [Arguments("(?:(?:a|)b){d<=1}", "b", 0, 1)]
    [Arguments("(?:(?:a|)b){e<=1}", "b", 0, 1)]
    [Arguments("(?:x(?:a|){d<=1})", "x", 0, 1)]
    [Arguments("(?:a|){d<=1}b", "b", 0, 1)]
    [Arguments("(?:a||c){d<=1}", "", 0, 0)]
    [Arguments("(?:a|){d<=1}(?:b|){d<=1}", "", 0, 0)]
    [Arguments("(?:(?:a|)(?:a|)){d<=2}", "", 0, 0)]
    [Arguments("(?:aa*|){d<=1}", "", 0, 0)]
    [Arguments("(?:ab|){d<=2}", "", 0, 0)]
    [Arguments("(?:a(?:a|)|){d<=1}", "", 0, 0)]
    [Arguments("(?:cat(?:s|)){e<=1}", "cat", 0, 3)]
    [Arguments("(?:a(?:b|)){d<=1}", "a", 0, 1)]
    [Arguments("(?r)(?:(?:b|)a){d<=1}", "a", 0, 1)]
    [Arguments("(?:a|b|){d<=1}", "b", 0, 1)]
    [Arguments("(?e)(?:a|b|){d<=1}", "b", 0, 1)]
    [Arguments("(?:(?:a){d<=1}|)", "", 0, 0)]
    [Arguments("(?:a(?:(?:b){d<=1}|))", "a", 0, 1)]
    [Arguments("p?q(?:a|){d<=1}", "q", 0, 1)]
    public void An_empty_alternative_is_an_exit_that_spends_no_errors(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        ShouldMatch(new FuzzyRegex(pattern).Match(subject), start, end - start, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex(pattern).FullMatch(subject), 0, subject.Length, new FuzzyCounts(0, 0, 0));
    }

    // The same rule, on rows where only the search changes: fullmatch over 'k' is None in every
    // spelling, and in (?:a|b|) it already skipped the deleting pass, which cannot reach the end.
    [Test]
    public void An_empty_alternative_is_an_exit_for_a_search_that_stops_early()
    {
        // search('(?:[0-9]|){d<=1}', 'k')   (0, 0) (0, 0, 1)
        ShouldMatch(new FuzzyRegex("(?:[0-9]|){d<=1}").Match("k"), 0, 0, new FuzzyCounts(0, 0, 0));
        new FuzzyRegex("(?:[0-9]|){d<=1}").FullMatch("k").Success.Should().BeFalse();
    }

    // The group stays unset, as in (?:(?:(a))?){d<=1}: the pass that set it by deleting a failed.
    [Test]
    public void A_group_in_the_failed_pass_stays_unset()
    {
        // search('(?:(a)|){d<=1}', '')   (0, 0) (0, 0, 1), group 1 (0, 0)
        Match m = new FuzzyRegex("(?:(a)|){d<=1}").Match("");
        ShouldMatch(m, 0, 0, new FuzzyCounts(0, 0, 0));
        m.Groups[1].Success.Should().BeFalse();
    }

    // AGREES WITH UPSTREAM 2026.9.10 where upstream spends the errors too: something needs them. A
    // section minimum takes the one deletion it needs, not two; a conditional that tests the group
    // the deleting pass set keeps that pass.
    [Test]
    public void An_empty_alternative_leaves_the_errors_something_needs()
    {
        // search('(?:a|){1<=d<=1}', '')                (0, 0) (0, 0, 1), the same
        // search('(?:(?:a|)(?:a|)){1<=e<=2}', '')      (0, 0) (0, 0, 2)
        // search('(?:(a)|){d<=1}(?(1)x|y)', 'x')       (0, 1) (0, 0, 1), group 1 (0, 0), the same
        ShouldMatch(new FuzzyRegex("(?:a|){1<=d<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:a?){1<=d<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:(?:a|)(?:a|)){1<=e<=2}").Match(""), 0, 0, new FuzzyCounts(0, 0, 1));
        Match m = new FuzzyRegex("(?:(a)|){d<=1}(?(1)x|y)").Match("x");
        ShouldMatch(m, 0, 1, new FuzzyCounts(0, 0, 1));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 0));
    }

    // AGREES WITH UPSTREAM 2026.9.10: a choice between alternatives that are not empty stays
    // first-match. The README's own example keeps its deletion (upstream/README.rst:609,
    // upstream/regex/tests/test_regex.py:2784), and so does an empty alternative the compiler makes
    // when it factors a common prefix or suffix out of the alternatives (Branch.SplitCommonPrefix).
    // That happens only outside a fuzzy section, since nothing inside one is optimised (Fuzzy has
    // no Optimise), so it matters only where a section inside the alternatives spends the errors:
    // (?:a(?:b){d<=1}|a) compiles to the same bytecode as (?:a(?:(?:b){d<=1}|)), upstream's and
    // this port's, but it is a choice between two alternatives that are not empty.
    [Test]
    [Arguments("(?:cats|cat){e<=1}", "cat", 0, 3)]
    [Arguments("(?:ab|a){d<=1}", "a", 0, 1)]
    [Arguments("(?:ab|a){e<=1}", "a", 0, 1)]
    [Arguments("(?r)(?:ba|a){d<=1}", "a", 0, 1)]
    [Arguments("(?:ab|ac|a){d<=1}", "a", 0, 1)]
    [Arguments("(?:x|(?:ab|a)){d<=1}", "a", 0, 0)]
    [Arguments("(?:a(?:b){d<=1}|a)", "a", 0, 1)]
    [Arguments("(?r)(?:(?:b){d<=1}a|a)", "a", 0, 1)]
    public void A_choice_between_non_empty_alternatives_stays_first_match(
        string pattern,
        string subject,
        int start,
        int end
    )
    {
        ShouldMatch(new FuzzyRegex(pattern).Match(subject), start, end - start, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex(pattern).FullMatch(subject), 0, subject.Length, new FuzzyCounts(0, 0, 1));
    }

    // AGREES WITH UPSTREAM 2026.9.10: outside the rule. A nullable alternative or a lookaround is
    // not an empty alternative; a substitution or insertion consumes text; BESTMATCH and ENHANCEMATCH
    // already find the error-free answer; exact matching is untouched.
    [Test]
    public void Rows_outside_the_rule_keep_their_answer()
    {
        ShouldMatch(new FuzzyRegex("(?:a|b){d<=1}").Match("b"), 0, 0, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:c|a?){d<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:a|(?=x)){d<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:(?=x)|a){d<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 1));
        ShouldMatch(new FuzzyRegex("(?:a|){e<=1}").Match("b"), 0, 1, new FuzzyCounts(1, 0, 0));
        ShouldMatch(new FuzzyRegex("(?:a|){s<=1}").Match("x"), 0, 1, new FuzzyCounts(1, 0, 0));
        ShouldMatch(new FuzzyRegex("(?:a|){s<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?:a|){i<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?e)(?:cats|cat){e<=1}").Match("cat"), 0, 3, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?b)(?:a|){d<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?b)(?:a|b|){d<=1}").Match("b"), 0, 1, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?:a|)").Match(""), 0, 0, new FuzzyCounts(0, 0, 0));
        ShouldMatch(new FuzzyRegex("(?:|a){d<=1}").Match(""), 0, 0, new FuzzyCounts(0, 0, 0));
    }

    // A pass that consumed text, or spent no errors, stands. The first row deletes b after matching
    // a, as upstream does. In the second a* matches empty with no errors and the group it sets is
    // kept; upstream spends a deletion there, which entry 44 already removed from a*.
    [Test]
    public void A_pass_that_consumed_text_or_spent_no_errors_stands()
    {
        // search('(?:ab|){d<=1}', 'a')      (0, 1) (0, 0, 1), the same
        // search('(?:(a*)|){d<=1}', '')     (0, 0) (0, 0, 1), group 1 (0, 0)
        ShouldMatch(new FuzzyRegex("(?:ab|){d<=1}").Match("a"), 0, 1, new FuzzyCounts(0, 0, 1));
        Match m = new FuzzyRegex("(?:(a*)|){d<=1}").Match("");
        ShouldMatch(m, 0, 0, new FuzzyCounts(0, 0, 0));
        (m.Groups[1].Success, m.Groups[1].Index, m.Groups[1].Length).Should().Be((true, 0, 0));
    }

    // The deletion of an item that matched exactly and ends the alternative is left out only when the
    // pass it leaves would fail at its end anyway. Each row needs that deletion (ledger entry 42,
    // which upstream lacks, so upstream answers differently on all four): after the a matched
    // exactly, what follows the alternation fails, and deleting the a instead leaves a pass that
    // stands because of something the early exit must also see. In the first row an unmet section
    // minimum needs the deletion; in the second a tested group changed in the pass; in the third
    // the section minimum was unmet when the pass began, though the pass's deletion of the b has
    // met it since; in the fourth the pass consumed the a before deleting the [bc]. The items are
    // sets where a character would be packed into a string, which has its own deletion arm and no
    // early exit.
    [Test]
    public void The_exact_deletion_is_left_out_only_where_the_pass_would_fail()
    {
        // search('(?:a|){1<=d<=1}', 'a')             (1, 1) (0, 0, 1)
        // search('(?:()a|){d<=1}(?(1)a|z)', 'a')     None
        // search('(?:b[ac]|){1<=d<=2}[ac]', 'a')     None
        // search('(?:a[bc]|){d<=1}b', 'ab')          (1, 2) (0, 0, 0)
        ShouldMatch(new FuzzyRegex("(?:a|){1<=d<=1}").Match("a"), 0, 0, new FuzzyCounts(0, 0, 1));
        Match m = new FuzzyRegex("(?:()a|){d<=1}(?(1)a|z)").Match("a");
        ShouldMatch(m, 0, 1, new FuzzyCounts(0, 0, 1));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((0, 0));
        ShouldMatch(new FuzzyRegex("(?:b[ac]|){1<=d<=2}[ac]").Match("a"), 0, 1, new FuzzyCounts(0, 0, 2));
        ShouldMatch(new FuzzyRegex("(?:a[bc]|){d<=1}b").Match("ab"), 0, 2, new FuzzyCounts(0, 0, 1));
    }

    // A group call inside the pass re-enters the same alternation, and the inner pass must not
    // leave its start where the outer pass's end reads it. Here the outer pass deletes the c and
    // calls group 1, whose inner pass matches b? empty with no errors. The outer pass then ends
    // where it began with a deletion nothing needs, so it fails, as in the ? spelling, and the
    // empty alternative matches. Read from the inner pass's start, it looked error-free and stood.
    [Test]
    public void A_call_inside_the_pass_does_not_move_where_the_pass_began()
    {
        // search('(?:(c(?1)|b?|)){d<=1}', '')      (0, 0) (0, 0, 1), group 1 ['', '']
        // search('(?:((?:c(?1)|b?)?)){d<=1}', '')  the same
        foreach (string pattern in new[] { "(?:(c(?1)|b?|)){d<=1}", "(?:((?:c(?1)|b?)?)){d<=1}" })
        {
            Match m = new FuzzyRegex(pattern).Match("");
            ShouldMatch(m, 0, 0, new FuzzyCounts(0, 0, 0));
            m.Groups[1].Captures.Should().ContainSingle(pattern);
        }
    }

    // In a repeat, one path passes through the alternation once per iteration, and backtracking out
    // of a later iteration's pass must give the slot back to the earlier pass it re-enters. Read
    // from the later pass's start, the earlier pass's end saw fewer fuzzy changes than when that
    // pass began, which a Debug build asserts against.
    [Test]
    public void Backtracking_out_of_a_later_iteration_gives_back_where_the_earlier_pass_began()
    {
        // search('(?:(?:(?:a?b|)x)*y){e<=1}', 'bbx')   (0, 1) (1, 0, 0), the same
        ShouldMatch(new FuzzyRegex("(?:(?:(?:a?b|)x)*y){e<=1}").Match("bbx"), 0, 1, new FuzzyCounts(1, 0, 0));
    }
}
