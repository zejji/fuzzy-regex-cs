using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A partial match's fuzzy counts describe a path every open fuzzy section allows (was D81). A
/// run-out answers the partial before the sections around it close, and a section checks its own
/// limits only when it closes, so the run-out checks the upper limits of every open section against
/// the counts so far. Counts only grow with more text, so a limit already broken stays broken: such a
/// path is treated as failing and backtracking finds a legal one, or none.
/// </summary>
/// <remarks>
/// <c>{s&lt;=1}</c> alone allows only substitutions, and <c>{i&lt;=1}</c> only insertions: a kind a
/// section does not name has a limit of 0. An inner section's errors count against every section
/// around it (END_FUZZY adds them in). Upstream 2026.9.10 answers the legal path on the first
/// witness and reports a forbidden one on the others, measured 2026-10-11.
/// </remarks>
public sealed class PartialEnclosingSectionLimitTests
{
    /// <summary>
    /// The first witness, and the same shape inside a lookahead's body, reversed, and under
    /// <c>(?b)</c>. The inner section may substitute 'c' for 'b', but the outer <c>{i&lt;=1}</c>
    /// forbids a substitution; the legal path inserts the 'c' and runs out at 'b', which 'cbx'
    /// completes. The port answered the substitution. Upstream answers as here.
    /// </summary>
    [Test]
    [Arguments(@"(?:(?:b){e<=2}){i<=1}x", 0)]
    [Arguments(@"(?=(?:(?:b){e<=2}){i<=1}x)", 0)]
    [Arguments(@"(?r)x(?:(?:b){e<=2}){i<=1}", 1)]
    [Arguments(@"(?b)(?:(?:b){e<=2}){i<=1}x", 0)]
    [Arguments(@"(?e)(?:(?:b){e<=2}){i<=1}x", 0)]
    public void A_partial_takes_the_path_the_enclosing_section_allows(string pattern, int insertedAt)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart("c", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
        m.FuzzyChanges.Insertions.Should().Equal(insertedAt);
        m.FuzzyChanges.Substitutions.Should().BeEmpty();
    }

    /// <summary>
    /// The second witness, and a two-character twin: every path to the edge breaks the outer
    /// section, so no text completes the match and there is no partial. In
    /// <c>(?:a(?:b){i&lt;=1,s&lt;=1}){s&lt;=1}c</c> over 'xy', 'x' must be a substitution for 'a',
    /// and 'y' is then a second substitution or an insertion, both of which the outer
    /// <c>{s&lt;=1}</c> forbids. The port answered P(0, 2) with two substitutions; upstream answers
    /// P(0, 2) with counts (0, 1, 0) against a substitution in its change list, and None for
    /// every completion it was given ('xyc', 'xybc').
    /// </summary>
    [Test]
    [Arguments(@"(?:a(?:b){i<=1,s<=1}){s<=1}c", "xy")]
    [Arguments(@"(?:(?:bb){e<=2}){i<=1}x", "cc")]
    public void No_partial_where_every_path_breaks_an_enclosing_section(string pattern, string text)
    {
        new FuzzyRegex(pattern).MatchAtStart(text, partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// A minimum is not a limit a run-out can break: more text can still add the errors it needs.
    /// In the first row the outer section needs exactly one insertion and forbids the substitution
    /// the port answered before; the insertion path meets the minimum. In the second the outer
    /// minimum is unmet when 'x' runs out ('b' + 'cx' completes it), and in the third the inner
    /// section's ('b' + 'dx' completes it), and both stay partials with no errors. Upstream answers
    /// the first and third rows as here and None on the second, where it checks the minimum before
    /// trying the trailing insertion (ledger entry 51).
    /// </summary>
    [Test]
    [Arguments(@"(?:(?:b){e<=2}){1<=i<=1}x", "c", 0, 1, 0, 1, 0)]
    [Arguments(@"(?:(?:b){e<=2}){1<=i<=2}x", "b", 0, 1, 0, 0, 0)]
    [Arguments(@"(?:(?:bc){1<=e<=2}){e<=2}x", "b", 0, 1, 0, 0, 0)]
    public void An_unmet_minimum_leaves_the_partial(
        string pattern,
        string text,
        int start,
        int end,
        int substitutions,
        int insertions,
        int deletions
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, deletions));
    }
}
