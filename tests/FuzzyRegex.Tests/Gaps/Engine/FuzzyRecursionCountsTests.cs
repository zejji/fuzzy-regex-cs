using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Known defect D37: the errors a recursive instance of a fuzzy section spends are in the match's
/// counts and change list, and they share the section's one budget.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> In <c>(?:z(?R)|){e&lt;=1}</c> the pattern as a whole is the fuzzy section. The
/// compiler read "is the pattern a fuzzy section" before optimising, when the pattern was still a
/// one-item sequence, and the group-call check read it after, when it was the section itself. The
/// two disagreed, so the <c>(?R)</c> got neither a copy of the pattern nor a return at the end of
/// it: the call ran on to SUCCESS with the call and the outer section still open. The outer
/// instance's errors were never merged back, and its limits were never checked. Upstream reads the
/// two at the same two places (<c>upstream/regex/_main.py:577</c> and <c>_regex_core.py:4436</c>)
/// and raises <c>MemoryError</c> on every row here.
/// </para>
/// <para>
/// <b>The rule.</b> A recursive instance is the same section entered again, so it nests like any
/// inner section: it counts its own errors from zero, and its END_FUZZY adds them to the instance
/// that called it. Each instance's limits apply to what it matched, nested instances included. So
/// the outermost instance's maximum is the budget for the whole recursion, and a minimum is asked
/// of every instance. This is what upstream's C does for any nested section (the FUZZY arm saves
/// the outer counts, END_FUZZY merges them, <c>_regex.c:13132</c> and <c>:12448</c>), and a group
/// call does not save or reset the counts (<c>:13394</c>), so a called group spends the caller's
/// budget. One <c>{e&lt;=1}</c> written once in the pattern is one budget: a reader of
/// <c>(?:z(?R)|){e&lt;=1}</c> expects at most one error in the match, however deep it recurses.
/// </para>
/// </remarks>
public sealed class FuzzyRecursionCountsTests
{
    [Test]
    public void A_whole_pattern_recursion_counts_the_substitution_made_before_the_call()
    {
        // Before the fix: (0, 2) with no errors, though 'b' can only match 'z' as a substitution.
        Match m = new FuzzyRegex("(?:z(?R)|){e<=1}").Match("bz");

        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(0);
    }

    [Test]
    public void A_recursive_instance_shares_the_sections_budget()
    {
        // Before the fix: (0, 1) with a substitution and a deletion, two errors under e<=1.
        Match m = new FuzzyRegex("(?:z(?R)?){e<=1}").Match("b");

        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(0);
        m.FuzzyChanges.Deletions.Should().BeEmpty();
    }

    [Test]
    [Arguments("b", 0, 1, 0)]
    [Arguments("bb", 0, 1, 0)]
    [Arguments("zbz", 0, 3, 1)]
    public void An_optional_item_before_the_recursion_reports_its_substitution(
        string text,
        int start,
        int end,
        int substituted
    )
    {
        // Before the fix 'b' gave the right answer by the wrong path (the outer section never
        // closed, which only the Debug build's SUCCESS assert sees); 'bb' gave (0, 2) with one
        // error for two changed characters, and 'zbz' gave (0, 3) with none.
        Match m = new FuzzyRegex("(?:z?(?R)?){e<=1}").Match(text);

        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(substituted);
    }

    [Test]
    [Arguments("(?:z(?R)|){e<=1}", 1, new[] { 0 })]
    [Arguments("(?:z(?R)|){e<=2}", 2, new[] { 0, 1 })]
    public void The_outermost_maximum_bounds_every_level_of_the_recursion(string pattern, int end, int[] substituted)
    {
        // Before the fix both gave (0, 2) with one error: under e<=1 the second 'b' was a free
        // substitution, and under e<=2 the count lost it.
        Match m = new FuzzyRegex(pattern).Match("bb");

        (m.Index, m.Length).Should().Be((0, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substituted.Length, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(substituted);
    }

    [Test]
    [Arguments("(?:(?:z(?R)|){e<=1})")]
    [Arguments("(?:z(?0)|){e<=1}")]
    [Arguments("(?:(?:(?:z(?R)|){e<=1}))")]
    public void The_spelling_of_the_whole_pattern_call_does_not_change_the_counts(string pattern)
    {
        Match m = new FuzzyRegex(pattern).Match("bz");

        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(0);
    }

    [Test]
    [Arguments("((?:z(?1)|){e<=1})")]
    [Arguments("(?:(z(?1)|)){e<=1}")]
    public void A_numbered_call_into_the_section_shares_its_budget_too(string pattern)
    {
        // Controls: these were right before the fix, because a numbered group is compiled with its
        // own return. They pin the same rule on the path D37 did not break.
        Match m = new FuzzyRegex(pattern).Match("bb");

        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        m.FuzzyChanges.Substitutions.Should().Equal(0);
    }

    [Test]
    [Arguments(FuzzyRegexOptions.BestMatch)]
    [Arguments(FuzzyRegexOptions.EnhanceMatch)]
    public void Bestmatch_and_enhancematch_do_not_report_a_phantom_error_free_match(FuzzyRegexOptions options)
    {
        // Before the fix both answered (0, 2) with no errors, a match that needs one. The best
        // match is the empty one at 0, which needs none.
        Match m = new FuzzyRegex("(?:z(?R)|){e<=1}", options).Match("bz");

        (m.Index, m.Length).Should().Be((0, 0));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void Bestmatch_finds_the_error_free_match_that_the_recursion_hid()
    {
        // Before the fix (0, 3) with no errors; 'b' at 1 cannot match 'z' for free.
        Match m = new FuzzyRegex("(?:z?(?R)?){e<=1}", FuzzyRegexOptions.BestMatch).Match("zbz");

        (m.Index, m.Length).Should().Be((0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    [Test]
    public void A_minimum_is_met_by_every_instance_of_the_recursion()
    {
        // A control, the same before the fix. Each instance's END_FUZZY checks its minimum against
        // what it matched, nested instances included, so the one insertion in the inner instance
        // meets the minimum of the instance that called it as well.
        Match m = new FuzzyRegex("(?:z(?R)|){1<=e<=1}").Match("zz");

        (m.Index, m.Length).Should().Be((0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Insertions.Should().Equal(1);
    }
}
