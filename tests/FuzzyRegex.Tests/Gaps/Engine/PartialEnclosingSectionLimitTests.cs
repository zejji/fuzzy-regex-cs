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

    /// <summary>
    /// D129: an insertion that needs the character after the text runs out of text only if the
    /// insertion itself fits every open section. <c>^</c> fails past position 0, so after 'f' and
    /// one inserted 'x' the only step left is a second insertion, which the inner section allows
    /// and the outer one forbids. The port answered P(0, 2) with one insertion; the same path under
    /// a single <c>{i&lt;=1}</c> was already None. PCRE2 10.47 agrees once the insertions are
    /// written out: <c>f.{0,1}^</c> over 'fx' is None, <c>f.{0,2}^</c> is a partial. The rows are,
    /// in order: the insertion arm of an item; scheduled-oracle row 179705086/41658, whose
    /// insertions all count against the outer <c>{i&lt;=1}</c>; a trailing insertion at a section's
    /// end; an outer insertion cost (two units, three allowed); an outer total; and a section
    /// whose own next item is an exact character; then the trailing insertion reversed, where the
    /// section's END_FUZZY is on the left and the run-out is at the start of the text; and the
    /// inner section inside a called group, whose insertions count against the section around the
    /// call. Upstream answers None on each except the oracle row, where it reports P(0, 4)
    /// (ledger entry 81).
    /// </summary>
    [Test]
    [Arguments(@"(?:(?:f^){i<=2}){i<=1}", "fx")]
    [Arguments(@"(?:a0(?:[a-f]^){e<=2:[^i]}x){s<=1,i<=1,d<=1}", "a0fx")]
    [Arguments(@"(?:(?:f){i<=2}^){i<=1}", "fx")]
    [Arguments(@"(?:(?:f^){i<=2}){2i<=3}", "fx")]
    [Arguments(@"(?:(?:f^){i<=2}){e<=1}", "fx")]
    [Arguments(@"(?:(?:f^){i<=2}x){i<=1}", "fx")]
    [Arguments(@"(?r)(?:$(?:f){i<=2}){i<=1}", "xf")]
    [Arguments(@"(?:(?&g)){i<=1}(?(DEFINE)(?<g>(?:f^){i<=2}))", "fx")]
    public void No_partial_where_the_insertion_that_runs_out_breaks_an_enclosing_section(string pattern, string text)
    {
        new FuzzyRegex(pattern).MatchAtStart(text, partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// D129's controls: where every open section can take the second insertion, it runs out of
    /// text and the partial stands, as <c>f.{0,2}^</c> over 'fx' is a partial in PCRE2 10.47.
    /// The counts are the path so far; the insertion that runs out is not counted.
    /// </summary>
    [Test]
    [Arguments(@"(?:f){i<=2}^", "fx")]
    [Arguments(@"(?:(?:f^){i<=2}){i<=2}", "fx")]
    [Arguments(@"(?:(?:f){i<=2}^){i<=2}", "fx")]
    [Arguments(@"(?:(?:f^){i<=2}){2i<=4}", "fx")]
    [Arguments(@"(?r)(?:$(?:f){i<=2}){i<=2}", "xf")]
    [Arguments(@"(?:(?&g)){i<=2}(?(DEFINE)(?<g>(?:f^){i<=2}))", "fx")]
    public void A_run_out_insertion_every_section_allows_leaves_the_partial(string pattern, string text)
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 2));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 1, 0));
    }

    /// <summary>
    /// D130: a positive lookaround's body counts its errors against the sections around the
    /// lookaround, as a complete match does (<c>(?:(?=a(?:c){i&lt;=1})abc){i&lt;=0}</c> fullmatch 'abc'
    /// is None in both engines), so a run-out inside the body is checked against them too. In
    /// <c>(?:(?=(?:f^){i&lt;=2})f){i&lt;=1}</c> over 'fx' the body's `^` passes only after a second
    /// insertion, which the outer section forbids. The port answered P(0, 2) on each row; regex
    /// 2026.9.29 answers as here (D130-DESIGN.md, measured 2026-10-31). The rows: match; search,
    /// which moves to the attempt at 1; the same over 'af'; a lookahead nested in a lookahead; and a
    /// reversed lookbehind.
    /// </summary>
    [Test]
    [Arguments(@"(?:(?=(?:f^){i<=2})f){i<=1}", "fx", false, -1, -1)]
    [Arguments(@"(?:(?=(?:f^){i<=2})f){i<=1}", "fx", true, 1, 2)]
    [Arguments(@"(?:(?=(?:f^){i<=2})f){i<=1}", "af", true, 1, 2)]
    [Arguments(@"(?:(?=(?=(?:f^){i<=2}))f){i<=1}", "fx", false, -1, -1)]
    [Arguments(@"(?r)(?:f(?<=(?:\Zf){i<=2})){i<=1}", "xf", false, -1, -1)]
    public void A_run_out_in_a_positive_lookarounds_body_respects_the_sections_around_it(
        string pattern,
        string text,
        bool search,
        int start,
        int end
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = search ? regex.Match(text, partial: true) : regex.MatchAtStart(text, partial: true);

        if (start < 0)
        {
            m.Success.Should().BeFalse();
        }
        else
        {
            (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, start, end));
        }
    }

    /// <summary>
    /// D130's controls: an outer section with room keeps the partial; a negative lookaround's body
    /// drops its errors, so the section around it has no claim on them; an atomic group was already
    /// walked; and a complete match is unaffected. Each answer is the one before the fix.
    /// </summary>
    [Test]
    [Arguments(@"(?:(?=(?:f^){i<=2})f){i<=2}", "fx", true, true, 0, 2)]
    [Arguments(@"(?:(?!(?:abc){s<=2})x){s<=1}", "xy", true, false, 0, 1)]
    [Arguments(@"(?:(?>(?:abc){s<=2})){s<=1}", "xy", true, true, 1, 2)]
    public void A_lookaround_run_out_the_sections_allow_or_drop_is_unchanged(
        string pattern,
        string text,
        bool success,
        bool partial,
        int start,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((success, partial, start, end));
    }

    /// <summary>
    /// D132: a run-out insertion inside a positive condition test's body is checked against the
    /// sections around the condition too. The test is atomic and its body's errors count against
    /// those sections when the yes-branch runs, so an insertion they forbid fails the attempt on
    /// every text. In <c>(?:(?(?=(?:f^){i&lt;=2})f|g)){i&lt;=1}</c> over 'fx' the test's `^` passes
    /// only after a second insertion, which the outer section forbids. The port answered P(0, 2) on
    /// each match row and P(0, 2) / P(1, 3) on the search rows; regex 2026.9.29 and judge132.py both
    /// answer as here (D132-DESIGN.md, measured 2026-10-31). The rows: the witness under match,
    /// <c>(?b)</c> and <c>(?e)</c>, and over 'fxa' (None before the fix too); the condition inside a
    /// lookahead and inside an atomic group; the insertion at the inner section's end; a reversed
    /// lookbehind test; the test calling a group; an outer insertion cost; then search, which moves
    /// to the attempt at 1, and over 'afx' to 2.
    /// The last rows are the design's dangerous shapes 2 to 6 (reviews/d132-design.md F4): a
    /// positive test inside a negative lookaround, whose base stops the scan; a lookbehind test
    /// needing the start of the text; two tests in one section; and a test calling a group.
    /// </summary>
    [Test]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){i<=1}", "fx", false, "None")]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){i<=1}", "fxa", false, "None")]
    [Arguments(@"(?b)(?:(?(?=(?:f^){i<=2})f|g)){i<=1}", "fx", false, "None")]
    [Arguments(@"(?e)(?:(?(?=(?:f^){i<=2})f|g)){i<=1}", "fx", false, "None")]
    [Arguments(@"(?:(?=(?(?=(?:f^){i<=2})f|g))f){i<=1}", "fx", false, "None")]
    [Arguments(@"(?:(?>(?(?=(?:f^){i<=2})f|g))){i<=1}", "fx", false, "None")]
    [Arguments(@"(?:(?(?=(?:f){i<=2}^)f|g)){i<=1}", "fx", false, "None")]
    [Arguments(@"(?r)(?:(?(?<=(?:\Zf){i<=2})f|g)){i<=1}", "xf", false, "None")]
    [Arguments(@"(?:(?(?=(?&h))f|g)){i<=1}(?(DEFINE)(?<h>(?:f^){i<=2}))", "fx", false, "None")]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){2i<=3}", "fx", false, "None")]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){i<=1}", "fx", true, "P(1, 2)")]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){i<=1}", "afx", true, "P(2, 3)")]
    [Arguments(@"(?:(?(?=(?:f){i<=2}^)f|g)){i<=1}", "fx", true, "P(1, 2)")]
    [Arguments(@"(?:(?!(?(?=(?:f^){i<=2})f|g)x)f){i<=1}", "fx", false, "(0, 1)")]
    [Arguments(@"(?:(?!(?(?=(?:f^){i<=2})f|g)x)f){i<=1}", "fx", true, "(0, 1)")]
    [Arguments(@"(?r)(?:(?(?<=(?:^f){i<=2})f|g)){i<=1}", "xf", false, "(1, 2)")]
    [Arguments(@"(?r)(?:(?(?<=(?:^f){i<=2})f|g)){i<=1}", "xf", true, "(1, 2)")]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)(?(?=(?:f^){i<=2})f|g)){i<=2}", "fx", false, "P(0, 2)")]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)(?(?=(?:f^){i<=2})f|g)){i<=2}", "fx", true, "P(1, 2)")]
    [Arguments(@"(?:(?(?=(?&t))f|g)){i<=1}(?(DEFINE)(?<t>(?:f^){i<=2}))", "fx", true, "P(1, 2)")]
    public void A_run_out_in_a_positive_condition_test_respects_the_sections_around_it(
        string pattern,
        string text,
        bool search,
        string expected
    )
    {
        var regex = new FuzzyRegex(pattern);
        Match m = search ? regex.Match(text, partial: true) : regex.MatchAtStart(text, partial: true);

        string kind = m.PartialMatch ? "P" : "";
        string answer = m.Success ? $"{kind}({m.Index}, {m.Index + m.Length})" : "None";
        answer.Should().Be(expected);
    }

    /// <summary>
    /// D132, counts: in <c>(?:(?(?=(?:f^){i&lt;=2})f|g)f){s&lt;=1}</c> over 'f' the yes-branch path
    /// needs an insertion the outer section forbids, so the partial is the no-branch's, with 'g'
    /// substituted for 'f' ('ff' completes it). The port answered P(0, 1) with no error; regex
    /// 2026.9.29 answers P(0, 1) with counts (1, 0, 0).
    /// </summary>
    [Test]
    public void A_condition_tests_forbidden_run_out_leaves_the_no_branchs_partial()
    {
        Match m = new FuzzyRegex(@"(?:(?(?=(?:f^){i<=2})f|g)f){s<=1}").MatchAtStart("f", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 1));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
    }

    /// <summary>
    /// D132's controls, each the answer before the fix and regex 2026.9.29's: the witness over
    /// texts ending before the run-out; outer sections with room; a negative test, whose
    /// body's errors the port keeps (D54) and which the change does not walk past; a condition with
    /// no no-branch and one with equal branches; an atomic group whose test needs the end of the
    /// text; and a complete match with a substitution.
    /// </summary>
    [Test]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){i<=1}", "", true, true, 0, 0)]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){i<=1}", "f", true, true, 0, 1)]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){i<=2}", "fx", true, true, 0, 2)]
    [Arguments(@"(?r)(?:(?(?<=(?:\Zf){i<=2})f|g)){i<=2}", "xf", true, true, 0, 2)]
    [Arguments(@"(?:(?(?=(?&h))f|g)){i<=2}(?(DEFINE)(?<h>(?:f^){i<=2}))", "fx", true, true, 0, 2)]
    [Arguments(@"(?:(?(?!(?:f^){i<=2})f|g)){i<=1}", "fx", true, false, 0, 1)]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f)){i<=1}", "fx", true, false, 0, 0)]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|f)){i<=1}", "fx", true, false, 0, 1)]
    [Arguments(@"(?:(?>(?(?=(?:f\Z){i<=2})f|g))){i<=1}", "fx", true, false, 0, 1)]
    [Arguments(@"(?:(?(?=(?:f^){i<=2})f|g)){e<=1}", "fx", true, false, 0, 1)]
    public void A_condition_test_run_out_the_sections_allow_or_drop_is_unchanged(
        string pattern,
        string text,
        bool success,
        bool partial,
        int start,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern).MatchAtStart(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((success, partial, start, end));
    }
}
