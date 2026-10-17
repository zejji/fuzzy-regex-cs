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

    // The gate, the same with partial matching on or off (D100, DECISIONS 2026-10-17). A pattern with
    // no position-dependent construct and no fuzzy minimum keeps the ban: every completed text that
    // the attempt at the anchor matches has a match from one character on, so the port's full
    // search never starts at the anchor there, and the partial contract gives upstream's answer.
    // 'x' + 'a' is (1, 2) in the port; the partial was P(0, 1) with one insertion before D100.
    [Test]
    [Arguments(@"(?:a){i<=1}", "x")]
    [Arguments(@"(?:ab){i<=1}", "x")]
    public void A_partial_search_keeps_the_ban_where_the_full_search_always_does(string pattern, string text)
    {
        Match partial = new FuzzyRegex(pattern).Match(text, partial: true);

        (partial.PartialMatch, partial.Index, partial.Length, partial.FuzzyCounts.Total).Should().Be((true, 1, 0, 0));
    }

    // A pattern whose fuzzy sections permit no insertion is gated out.
    [Test]
    public void A_section_without_insertions_is_gated_out()
    {
        new FuzzyRegex("(?:a){i<=1}").PatternObject.LiftsAnchorBan.Should().BeFalse();
        new FuzzyRegex(@"\b(?:dog){i<=1}").PatternObject.LiftsAnchorBan.Should().BeTrue();
        new FuzzyRegex(@"\b(?:dog){s<=1}").PatternObject.LiftsAnchorBan.Should().BeFalse();
        new FuzzyRegex(@"\b(?:dog){d<=1}").PatternObject.LiftsAnchorBan.Should().BeFalse();
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

    // D100: a partial reports the least start from which some continuation matches (DECISIONS
    // 2026-09-30, the partial contract), and the port's own full search decides what matches. Over
    // 'ba', the continuation 'b' gives 'bab', where this port finds (0, 3) with 'b' inserted at 0
    // (H' lifts the ban, as upstream's banned search finds nothing), so the partial starts at 0.
    // u from 1 is only a partial: its own completion needs an insertion the continuation 'b' does
    // not give. DIVERGES FROM UPSTREAM 2026.9.10, which gives P(1, 2) and None over 'bab' (the
    // mirror P(0, 1)). Judged by the port's full search over every continuation of up to 3
    // characters from 'abx ' (.scratch/s1016/d100/judge.py).
    [Test]
    [Arguments(@"(?:ab){1<=i<=2}", "ba", 0, 2, 1)]
    [Arguments(@"(?:ab){1<=i<=2}", "xa", 0, 2, 1)]
    [Arguments(@"(?:ab){2<=i<=2}", "xya", 0, 3, 2)]
    [Arguments(@"(?r)(?:ba){1<=i<=2}", "ab", 0, 2, 1)]
    [Arguments(@"(?r)(?:ba){1<=i<=2}", "ax", 0, 2, 1)]
    // u is P(2, 2), at the edge, not one character on: 'xab' is (0, 3) here, as `\b` fails at 1
    // and 2. Upstream P(2, 2). Passed on main.
    [Arguments(@"\b(?:ab){i<=1}", "xa", 0, 2, 1)]
    public void A_partial_keeps_the_anchor_when_a_continuation_lets_H_prime_lift_the_ban(
        string pattern,
        string text,
        int start,
        int end,
        int insertions
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, start, end));
        m.FuzzyCounts.Insertions.Should().Be(insertions);
    }

    // D100 over finditer: the scan's one match is the same partial.
    [Test]
    public void A_partial_scan_keeps_the_anchor_when_a_continuation_lets_H_prime_lift_the_ban()
    {
        Match[] forward = [.. new FuzzyRegex(@"(?:ab){1<=i<=2}").Matches("ba", partial: true)];
        Match[] reversed = [.. new FuzzyRegex(@"(?r)(?:ba){1<=i<=2}").Matches("ab", partial: true)];

        forward.Select(static m => (m.PartialMatch, m.Index, m.Index + m.Length)).Should().Equal((true, 0, 2));
        reversed.Select(static m => (m.PartialMatch, m.Index, m.Index + m.Length)).Should().Equal((true, 0, 2));
    }

    // D100 controls: every continuation that completes the attempt at the anchor also completes a
    // match from inside it, so the port's full search never starts at 0 and the later partial
    // stands. The partial u begins one character on, where the attempt's path less its leading
    // insertion begins, and already meets the section's minimum. Upstream agrees, and so does the
    // judge. The first three pass on main and are the rows a rule that let every partial u lose
    // would turn into P(0, ...). The last, `(?:a){i<=1}\b`, was P(0, 1) on main.
    [Test]
    [Arguments(@"(?:ab){1<=i<=2}", "xxa", 1, 3)] // 'xxab': c (0, 4) i=2, u (1, 4) i=1
    [Arguments(@"(?:ab){1<=i<=2}", "xba", 1, 3)]
    [Arguments(@"(?=x|ab)(?:ab){i<=1}", "xa", 1, 2)]
    [Arguments(@"(?:ab){i<=1}", "xa", 1, 2)]
    [Arguments(@"(?r)(?:ba){1<=i<=2}", "axx", 0, 2)]
    [Arguments(@"(?:a){i<=1}\b", "x", 1, 1)]
    public void A_partial_keeps_the_later_start_when_every_continuation_has_a_match_inside_the_attempt(
        string pattern,
        string text,
        int start,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, start, end));
    }

    // D96: H' asks where u's attempt began, not the start a `\K` reports. In each row u's attempt
    // starts inside c, so u stands, as without the `\K`. Upstream's answers (regex 2026.9.10,
    // 2026-10-17), every one exact; before the fix the port kept c, with one insertion.
    [Test]
    [Arguments(@"(?:a){i<=1}\K", "ba", 2, 2)] // without \K: (1, 2) exact on both
    [Arguments(@"(?:ab){i<=1}\K", "xab", 3, 3)] // without \K: (1, 3)
    [Arguments(@"(?e)(?:a){i<=1}\K", "ba", 2, 2)]
    [Arguments(@"(?r)\K(?:a){i<=1}", "ab", 0, 0)] // without \K: (0, 1)
    [Arguments(@"(?r)\K(?:ab){i<=1}", "abx", 0, 0)] // without \K: (0, 2)
    // u's attempt starts AT the anchor, so it is the answer; without \K, (0, 2) exact through xa.
    [Arguments(@"(?:a){i<=1}|xa\K", "xa", 2, 2)]
    // The same rows without the `\K`, and one where u's reported start already lies inside c:
    // these passed before D96's fix and must not move.
    [Arguments(@"(?:a){i<=1}", "ba", 1, 2)]
    [Arguments(@"(?:ab){i<=1}", "xab", 1, 3)]
    [Arguments(@"(?r)(?:a){i<=1}", "ab", 0, 1)]
    [Arguments(@"(?r)(?:ab){i<=1}", "abx", 0, 2)]
    [Arguments(@"(?:a){i<=1}|xa", "xa", 0, 2)]
    [Arguments(@"(?:ab){i<=1}\Kc", "xabc", 3, 4)]
    public void A_keep_does_not_change_which_attempt_wins(string pattern, string text, int start, int end)
    {
        Match m = new FuzzyRegex(pattern).Match(text);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, 0, 0));
    }

    // D96 under partial matching and finditer: upstream's answers (2026-10-17), all exact.
    [Test]
    public void A_keep_does_not_change_which_attempt_wins_in_a_scan_or_a_partial_search()
    {
        Match partial = new FuzzyRegex(@"(?:ab){i<=1}\K").Match("xab", partial: true);
        (partial.PartialMatch, partial.Index, partial.Length, partial.FuzzyCounts.Total).Should().Be((false, 3, 0, 0));

        SpansAndErrors(@"(?:ab){i<=1}\K", "xab").Should().Equal((3, 3, 0));
        SpansAndErrors(@"(?r)\K(?:ab){i<=1}", "abx").Should().Equal((0, 0, 0));
        SpansAndErrors(@"(?:ab){i<=1}|xab\K", "xab").Should().Equal((3, 3, 0));
        // The second match was (8, 8) with two insertions: c ran from 3 over the two spaces.
        SpansAndErrors(@"\b(?:dog){e<=2}\K", "dog  dog").Should().Equal((3, 3, 0), (8, 8, 0));
        SpansAndErrors(@"\b(?:dog){e<=2}", "dog  dog").Should().Equal((0, 3, 0), (5, 8, 0));

        // H' still keeps c where u's attempt begins after it, as it does without the `\K`
        // (Finditer_keeps_a_match_at_an_anchor_no_later_match_overlaps). DIVERGES FROM UPSTREAM,
        // which gives [(5, 5)] with one insertion.
        SpansAndErrors(@"(?:\ba){i<=1}\K", "xa xa").Should().Equal((2, 2, 1), (5, 5, 1));
    }

    // D96: when u wins, its attempt's start comes back with it. The overlapped scan steps on from
    // the attempt when a `\K` in a lookbehind reports a start behind it (D87). Here u begins at 2
    // and reports (1, 4); the scan kept c's attempt start, 0, stepped from the reported 1 to 2, and
    // found the attempt at 2 a second time. Each attempt now answers once: 2 with 'b' inserted, then
    // 3 exact, which is what the port gives with H' off. Upstream loops for ever here (D87).
    [Test]
    public void An_overlapped_scan_steps_on_from_the_winning_attempt()
    {
        Match[] all = [.. new FuzzyRegex(@"(?:c){i<=1}(?<=\K...)").Matches("xxbc", overlapped: true)];

        all.Select(static m => (m.Index, m.Index + m.Length, m.FuzzyCounts.Total)).Should().Equal((1, 4, 1), (1, 4, 0));
    }

    private static (int Start, int End, int Errors)[] SpansAndErrors(string pattern, string text) =>
        [
            .. new FuzzyRegex(pattern)
                .Matches(text)
                .Select(static m => (m.Index, m.Index + m.Length, m.FuzzyCounts.Total)),
        ];

    private static (int Start, int End) Span(Match m) => (m.Index, m.Index + m.Length);

    private static (int Start, int End)[] Spans(string pattern, string text) =>
        [.. new FuzzyRegex(pattern).Matches(text).Select(Span)];
}
