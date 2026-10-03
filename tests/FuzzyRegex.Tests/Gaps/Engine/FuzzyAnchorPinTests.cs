using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// Rule H': the search-anchor insertion ban is lifted exactly when upstream's search would return no
/// match that starts inside the candidate. Known defect D61, which replaces the ledger 19/20
/// exemption.
/// </summary>
/// <remarks>
/// <para>
/// Upstream refuses a fuzzy insertion at the position a search began
/// (<c>permit_insertion = !search || text_pos != search_anchor</c>, <c>upstream/src/_regex.c</c>:10214),
/// so that a match never swallows text where a later match could start: <c>(?:dog){e&lt;=1}</c>
/// finditer over 'dog dog dog' gives three exact matches rather than gluing the spaces on. The cost
/// is that it loses real matches when nothing can start later: <c>(?:^a){i&lt;=1}</c> over 'xa' is
/// None. Rule H' (DECISIONS 2026-09-30): at search anchor A, let u be upstream's answer from A, with
/// the ban. If u starts at A, the answer is u. Otherwise let c be the attempt at A without the ban;
/// the answer is c if it exists and u is None or starts at or after c's end, and u otherwise.
/// BESTMATCH is out of scope for now (DECISIONS 2026-10-01, scope cut): it keeps main's path, the
/// ledger 19/20 exemption and main's fix for upstream issue 564, and the BESTMATCH tests below pin
/// main's answers.
/// </para>
/// <para>
/// Every expected answer below comes from that rule, computed from upstream's own
/// <c>search</c> and <c>match</c> on <c>regex</c> 2026.9.10 on 2026-09-30. No surveyed engine
/// combines fuzzy matching with these constructs, so there is no engine survey.
/// </para>
/// </remarks>
public sealed class FuzzyAnchorPinTests
{
    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer. Upstream gives None to every
    // row: it finds no match at a later start, so c is kept.
    [Test]
    [Arguments(@"(?:^a){i<=1}", "xa", 0, 2, 1)] // as ^(?:a){i<=1}, which is (0, 2) in both
    [Arguments(@"(?:^a.){i<=2}", " xabb", 0, 4, 2)]
    [Arguments(@"(?:\Aa.){i<=2}", " xabb", 0, 4, 2)]
    [Arguments(@"(?m)(?:^a){i<=1}", "xa", 0, 2, 1)]
    [Arguments(@"(?:\ba){i<=1}", "xa", 0, 2, 1)]
    [Arguments(@"(?:\ma){i<=1}", "xa", 0, 2, 1)]
    [Arguments(@"(?:^|q)(?:a){i<=1}", "xa", 0, 2, 1)]
    [Arguments(@"(?:(?<!x)ab){i<=1}", "xab", 0, 3, 1)]
    [Arguments(@"(?:(?=x)a){i<=1}", "xa", 0, 2, 1)]
    [Arguments(@"(?:(?=^)a){i<=1}", "xa", 0, 2, 1)]
    [Arguments(@"(?=x)(?:abc){i<=1}", "xabc", 0, 4, 1)]
    // Gated in by an atomic group and by a lower bound, and H' applies.
    [Arguments(@"(?>a?)(?:a){i<=1}", " a", 0, 2, 1)]
    [Arguments(@"(?:ab){1<=i<=1}", "xab", 0, 3, 1)]
    // Reversed, the anchor is the end of the subject: the mirror image.
    [Arguments(@"(?r)(?:a$){i<=1}", "ax", 0, 2, 1)]
    [Arguments(@"(?r)(?:a\Z){i<=1}", "ax", 0, 2, 1)]
    [Arguments(@"(?r)(?:a\M){i<=1}", "ax", 0, 2, 1)]
    public void The_attempt_at_the_anchor_is_kept_when_no_later_match_exists(
        string pattern,
        string text,
        int start,
        int end,
        int insertions
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, insertions, 0));
    }

    // Upstream raises "invalid RE code" here (ledger 48); u is None and c is (0, 2).
    [Test]
    public void A_search_anchor_assertion_in_the_section_keeps_the_attempt_at_the_anchor()
    {
        Match m = new FuzzyRegex(@"(?:\Ga){i<=1}").Match("xa");

        (m.Index, m.Length, m.FuzzyCounts.Insertions).Should().Be((0, 2, 1));
    }

    // Upstream's answer in every row: u starts inside c, so u stands. The first rows are the
    // witnesses that refuted the shipped exemption and the per-path and dominance rules.
    [Test]
    public void Upstream_answers_where_its_match_starts_inside_the_candidate()
    {
        // The shipped ledger 19/20 exemption gave (3, 8) with two insertions for the second match.
        Spans(@"\b(?:dog){e<=2}", "dog  dog").Should().Equal((0, 3), (5, 8));
        Spans(@"(?:\M|)(?:dog){e<=1}", "dog dog").Should().Equal((0, 3), (4, 7));
        Spans(@"(?:\M)?(?:dog){e<=1}", "dog dog").Should().Equal((0, 3), (4, 7));
        // Upstream (0, 5), (7, 12); this port's banned search already gives (6, 12) with 'x' inserted
        // before the failing lookahead (ledger entry 50), and that starts inside the candidate (5, 12).
        Spans("(?:(?!x)stone|abc){e<=2}", "stone xstone").Should().Equal((0, 5), (6, 12));
        Spans(@"(?:\Ma|a){i<=2}", "a-xa.").Should().Equal((0, 1), (2, 4));
        Spans(@"(?<!\s)(?:dog){e<=1}", "dog dog dog").Should().Equal((0, 3), (5, 7), (9, 11));
        Spans("(?:dog){e<=1}", "dog dog dog").Should().Equal((0, 3), (4, 7), (8, 11));
        Spans(@"(?:\bdog){e<=1}", "dog dog dog").Should().Equal((0, 3), (4, 7), (8, 11));
        Spans(@"(?:(?<!\w)dog){e<=1}", "dog dog dog").Should().Equal((0, 3), (4, 7), (8, 11));
        Spans(@"(?:(?!\w)|)(?:dog){e<=1}", "dog dog").Should().Equal((0, 3), (4, 7));
        Spans("(?:ab){e<=1}", "dog dog dog").Should().BeEmpty();
        Spans("(?:^abc|b|c){i<=1}", "xabc").Should().Equal((1, 3), (3, 4));
        Spans("(?:^a|abcd){i<=1}", "xabcd").Should().Equal((1, 5));
        Spans("(?:abc){i<=1}", "xabc").Should().Equal((1, 4));
        Spans("(?m)(?:^x){i<=1}", "\nx").Should().Equal((1, 2));
        Span(new FuzzyRegex(@"(?:\Bc){i<=1}").Match("abc", 1)).Should().Be((2, 3));

        // u starts at the anchor, so it is the answer: the exact (0, 3) through `x`.
        Match exact = new FuzzyRegex("(?:^|x)(?:ab){i<=1}").Match("xab");
        (exact.Index, exact.Length, exact.FuzzyCounts.Total).Should().Be((0, 3, 0));

        // A later exact match exists, so no insertion at the anchor.
        Spans("(?:^b|)(?:a){i<=1}", "xa").Should().Equal((1, 2));
        Spans("(?:(?=^)(?!)|)(?:a){i<=1}", "xa").Should().Equal((1, 2));
        Spans("(?:(?>^b)|)(?:a){i<=1}", "xa").Should().Equal((1, 2));
        Spans("(?:(?!^b)a){i<=1}", "xa").Should().Equal((1, 2));
        new FuzzyRegex("(?:a^){i<=1}").Match("xa").Success.Should().BeFalse();

        // Reversed, the mirror: upstream's own answer.
        Spans(@"(?r)(?:dog){e<=2}\b", "dog  dog").Should().Equal((5, 8), (0, 3));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void Finditer_keeps_a_match_at_an_anchor_no_later_match_overlaps()
    {
        // finditer('(?:\ba){i<=1}', 'xa xa')     [(3, 5)]
        // finditer('(?:\ma){i<=1}', 'xa xa ya')  [(3, 5), (6, 8)]
        Spans(@"(?:\ba){i<=1}", "xa xa").Should().Equal((0, 2), (3, 5));
        Spans(@"(?:\ma){i<=1}", "xa xa ya").Should().Equal((0, 2), (3, 5), (6, 8));

        // A known effect, not a defect (DECISIONS): the extra match moves the next anchor.
        // finditer('(?:\ma){i<=2}', 'ba  aba')   [(2, 5)]
        Spans(@"(?:\ma){i<=2}", "ba  aba").Should().Equal((0, 2), (3, 5));
    }

    // BESTMATCH KEEPS MAIN'S PATH (D61 scope cut): H' is gated out, and these rows pin main's answers,
    // measured on main 747e0d6d on 2026-10-01 (with every (?b) row of the 2,500-row grid: 0 differ).
    // They also agree with what H' would give by BESTMATCH's own ranking, which is why they were
    // written; the rule inside a best-match walk is a separate question (Optimiser.FindAnchorBanLift).
    [Test]
    [Arguments(@"(?b)(?<=x)(?:ab){i<=1}", "xyab xab", 1, 6, 8, 0, 0)]
    [Arguments(@"(?b)\b(?:dog){1i+3s<=3}", "xxdog dug", 0, 0, 5, 0, 2)]
    [Arguments(@"(?b)\b(?:dog){2i+1s<=2}", "xdog dug", 0, 5, 8, 1, 0)]
    [Arguments(@"(?b)\b(?:dog){e<=1}", "xdog dug", 0, 0, 4, 0, 1)]
    [Arguments(@"(?b)(?:dog){1<=e<=2}(?=\s|$)", "\ndog\n\nzdog\n", 5, 7, 11, 0, 1)]
    public void Bestmatch_keeps_mains_answers(
        string pattern,
        string text,
        int pos,
        int start,
        int end,
        int substitutions,
        int insertions
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text, pos);

        (m.Index, m.Index + m.Length).Should().Be((start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(substitutions, insertions, 0));
    }

    // The same, over finditer: the re-review's witness, which H' inside the walk turned into
    // [(0, 3), (4, 8), (9, 11)], and upstream issue 564, which main's exemption fixes.
    [Test]
    public void Bestmatch_finditer_keeps_mains_answers()
    {
        Spans(@"(?b)(?:\mdog){1<=e<=2}", "do; xdog-dog").Should().Equal((0, 3), (8, 12));
        Spans(@"(?b)(?:(?!x)abc){1<=e<=2}", "xxabc  abc  .abc\n").Should().Equal((1, 5), (7, 11), (13, 17));
        Spans(@"(?b)\m(?:Y){1i+1d+1s<=2}\M", " XY Z").Should().Equal((1, 3), (4, 5));
    }

    // The gate. A search with partial matching on is gated in, whatever the pattern: dropping the
    // leading insertion leaves the partial (1, 1), which starts later. A pattern whose fuzzy sections
    // permit no insertion is gated out. DIVERGES FROM UPSTREAM on the partial row (upstream P(1, 1)).
    [Test]
    public void Partial_matching_is_gated_in_and_a_section_without_insertions_is_gated_out()
    {
        Match partial = new FuzzyRegex("(?:a){i<=1}").Match("x", partial: true);
        (partial.PartialMatch, partial.Index, partial.Length, partial.FuzzyCounts.Insertions)
            .Should()
            .Be((true, 0, 1, 1));

        new FuzzyRegex("(?:a){i<=1}").PatternObject.LiftsAnchorBan.Should().BeFalse();
        new FuzzyRegex(@"\b(?:dog){i<=1}").PatternObject.LiftsAnchorBan.Should().BeTrue();
        new FuzzyRegex(@"\b(?:dog){s<=1}").PatternObject.LiftsAnchorBan.Should().BeFalse();
        new FuzzyRegex(@"\b(?:dog){d<=1}").PatternObject.LiftsAnchorBan.Should().BeFalse();
        new FuzzyRegex(@"\b(?:dog){d<=1}").PatternObject.LiftsAnchorBanWhenPartial.Should().BeFalse();
    }

    // Partial matching: a full match is preferred to a partial one, so H' runs on the full pass
    // first. From pos=1 upstream answers P(2, 4); the full attempt at 1, (1, 4) with 'y' inserted,
    // has no full match after it, so it is the answer. DIVERGES FROM UPSTREAM.
    [Test]
    public void A_full_match_at_the_anchor_is_preferred_to_a_later_partial()
    {
        Match m = new FuzzyRegex(@"(?<=x)(?:ab){i<=1}|abc").Match("xyab", 1, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, false, 1, 3));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10 (None), and this test pins OUR answer: the partial pass
    // keeps the attempt at the anchor, as nothing starts later.
    [Test]
    public void A_partial_match_may_open_with_an_insertion_at_the_anchor()
    {
        Match m = new FuzzyRegex("(?:^ab){i<=1}").Match("xa", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 2));
    }

    // Upstream's answer: a partial from 1 exists, and it starts inside the candidate.
    [Test]
    public void A_partial_match_starts_at_the_leftmost_live_start()
    {
        Match m = new FuzzyRegex("(?=x|ab)(?:ab){i<=1}").Match("xa", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 1, 1));
    }

    private static (int Start, int End) Span(Match m) => (m.Index, m.Index + m.Length);

    private static (int Start, int End)[] Spans(string pattern, string text) =>
        [.. new FuzzyRegex(pattern).Matches(text).Select(Span)];
}
