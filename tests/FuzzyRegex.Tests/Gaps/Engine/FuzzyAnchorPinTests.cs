using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// A position assertion that holds at the search anchor, and fails one character later, lifts the
/// search-anchor insertion ban for a fuzzy item it dominates - every path from the start of the
/// pattern to the item passes it - wherever it is written. Known defect D61, the extension of ledger
/// entries 19 and 20.
/// </summary>
/// <remarks>
/// <para>
/// Upstream refuses a fuzzy insertion at the position a search began
/// (<c>permit_insertion = !search || text_pos != search_anchor</c>, <c>upstream/src/_regex.c</c>:10214),
/// because "it's better just to start searching one character later". That is true only while the
/// pattern still fits one character later. An assertion every path to the item passes, which holds
/// at the anchor and fails one character on, says it does not: any start one character later fails
/// at the assertion. S57c applied that to an assertion before the section; the owner's ruling of
/// 2026-09-30 (option B, <c>docs/plan/DECISIONS.md</c>) applies it wherever the assertion is written,
/// and the review that refuted a per-path reading narrowed "the path passed it" to "it dominates the
/// item": with another route to the item, a later start need not pass the assertion at all. The ban
/// itself stays, so a pattern with no such assertion answers as upstream does, and finditer still
/// does not glue a separator onto the next match.
/// </para>
/// <para>
/// The expected answers come from that rule: each red row is the match the pattern makes at the
/// anchor with a leading insertion, where a dominating assertion forbids every later start. No surveyed engine combines fuzzy matching with these
/// assertions, so there is no engine survey. Every upstream answer quoted was measured on
/// <c>regex</c> 2026.9.10 on 2026-09-30.
/// </para>
/// </remarks>
public sealed class FuzzyAnchorPinTests
{
    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer. Upstream's answer is the
    // last argument.
    [Test]
    [Arguments(@"(?:^a){i<=1}", "xa", 0, 2, 1, "None")] // as ^(?:a){i<=1}, which is (0, 2) in both
    [Arguments(@"(?:^a.){i<=2}", " xabb", 0, 4, 2, "None")]
    [Arguments(@"(?:\Aa.){i<=2}", " xabb", 0, 4, 2, "None")]
    [Arguments(@"(?m)(?:^a){i<=1}", "xa", 0, 2, 1, "None")]
    [Arguments(@"(?:\ba){i<=1}", "xa", 0, 2, 1, "None")]
    [Arguments(@"(?:\ma){i<=1}", "xa", 0, 2, 1, "None")]
    [Arguments(@"(?:\Ga){i<=1}", "xa", 0, 2, 1, "RuntimeError: invalid RE code")] // ledger 48
    // A lookaround whose body is one character test is an assertion about the position too.
    [Arguments(@"(?:(?<!x)ab){i<=1}", "xab", 0, 3, 1, "None")]
    [Arguments(@"(?:(?=x)a){i<=1}", "xa", 0, 2, 1, "None")]
    // An assertion on the branch that holds the item dominates it (the `abcd` branch has none).
    [Arguments(@"(?:^a|abcd){i<=1}", "xabcd", 0, 2, 1, "(1, 5), no errors")]
    // Reversed, the anchor is the end of the subject and "one character later" is one to the left.
    [Arguments(@"(?r)(?:a$){i<=1}", "ax", 0, 2, 1, "None")]
    [Arguments(@"(?r)(?:a\Z){i<=1}", "ax", 0, 2, 1, "None")]
    [Arguments(@"(?r)(?:a\M){i<=1}", "ax", 0, 2, 1, "None")]
    public void An_assertion_that_pins_the_anchor_lifts_the_ban_wherever_it_is_written(
        string pattern,
        string text,
        int start,
        int end,
        int insertions,
        string upstream
    )
    {
        _ = upstream;
        Match m = new FuzzyRegex(pattern).Match(text);

        (m.Success, m.Index, m.Index + m.Length).Should().Be((true, start, end));
        m.FuzzyCounts.Should().Be(new FuzzyCounts(0, insertions, 0));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void A_partial_match_may_open_with_an_insertion_at_a_pinned_anchor()
    {
        // search('(?:^ab){i<=1}', 'xa', partial=True)  None. 'xab' is a complete match with the x
        // inserted, so 'xa' is a prefix of one.
        Match m = new FuzzyRegex("(?:^ab){i<=1}").Match("xa", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 2));
    }

    // Upstream's answer: the lookahead's body is an alternation, which does not pin (see
    // Optimiser.FindAnchorPins), and the leftmost start from which a continuation can match is 1.
    [Test]
    public void A_partial_match_starts_at_the_leftmost_live_start()
    {
        // search('(?=x|ab)(?:ab){i<=1}', 'xa', partial=True)  P(1, 2)
        Match m = new FuzzyRegex("(?=x|ab)(?:ab){i<=1}").Match("xa", partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 1, 1));
    }

    // DIVERGES FROM UPSTREAM 2026.9.10, and this test pins OUR answer.
    [Test]
    public void Finditer_keeps_the_match_at_a_pinned_anchor_and_every_later_one()
    {
        // finditer('(?:\ba){i<=1}', 'xa xa')     [(3, 5)]
        // finditer('(?:\ma){i<=1}', 'xa xa ya')  [(3, 5), (6, 8)]
        // The second search starts at the space, where \b holds one character on too, so the ban
        // still keeps the space out of the second match.
        Spans(@"(?:\ba){i<=1}", "xa xa").Should().Equal((0, 2), (3, 5));
        Spans(@"(?:\ma){i<=1}", "xa xa ya").Should().Equal((0, 2), (3, 5), (6, 8));

        // A search can move to an earlier, shorter match, which then covers less text: upstream
        // gives (1, 5) and [(1, 5)] here, so 'bcd' is no longer in any match. That is the answer
        // upstream and this port already give to the written-before form ^(?:a|abcd){i<=1}, and
        // the ordinary leftmost-first answer of (?:xa|abcd).
        Spans("(?:^a|abcd){i<=1}", "xabcd").Should().Equal((0, 2));
        Spans("^(?:a|abcd){i<=1}", "xabcd").Should().Equal((0, 2));
    }

    // Upstream's answer in every row, and this port's before and after D61.
    [Test]
    public void The_ban_stays_where_no_assertion_on_the_path_pins_the_anchor()
    {
        // No assertion: upstream's rule doing its job. The separator stays out of each match.
        Spans("(?:dog){e<=1}", "dog dog dog").Should().Equal((0, 3), (4, 7), (8, 11));
        Spans("(?:abc){i<=1}", "xabc").Should().Equal((1, 4));

        // An assertion that holds one character later too pins nothing.
        Spans(@"(?:\bdog){e<=1}", "dog dog dog").Should().Equal((0, 3), (4, 7), (8, 11));
        Spans(@"(?:(?<!\w)dog){e<=1}", "dog dog dog").Should().Equal((0, 3), (4, 7), (8, 11));
        Spans("(?m)(?:^x){i<=1}", "\nx").Should().Equal((1, 2));
        Span(new FuzzyRegex(@"(?:\Bc){i<=1}").Match("abc", 1)).Should().Be((2, 3));

        // An assertion that does not dominate the item pins nothing, because a later start can reach
        // the item another way (the review's refutation of the per-path reading, 2026-09-30). The
        // first three rows were (0, 2), (0, 2) and (0, 4) under that reading and are upstream's
        // answer now; the next three glued the space onto the second 'dog', and the last took a
        // needless insertion, where upstream and `(?:dog){e<=1}` give exact matches.
        new FuzzyRegex("(?:^|q)(?:a){i<=1}")
            .Match("xa")
            .Success.Should()
            .BeFalse();
        new FuzzyRegex("(?:q|^)(?:a){i<=1}").Match("xa").Success.Should().BeFalse();
        new FuzzyRegex(@"(?:\b|q)(?:abc){i<=1}").Match("xabc").Success.Should().BeFalse();
        Spans(@"(?:\M|)(?:dog){e<=1}", "dog dog").Should().Equal((0, 3), (4, 7));
        Spans(@"(?:\M)?(?:dog){e<=1}", "dog dog").Should().Equal((0, 3), (4, 7));
        Spans(@"(?:(?!\w)|)(?:dog){e<=1}", "dog dog").Should().Equal((0, 3), (4, 7));
        Match exact = new FuzzyRegex("(?:^|x)(?:ab){i<=1}").Match("xab");
        (exact.Index, exact.Length, exact.FuzzyCounts.Total).Should().Be((0, 3, 0));

        // An assertion inside a lookaround's body is tested wherever the body has got to, so it is
        // not counted, and a zero-width body is not one character test, so this lookaround does not
        // pin either. It was (0, 2) under the per-path reading; upstream gives None.
        new FuzzyRegex("(?:(?=^)a){i<=1}")
            .Match("xa")
            .Success.Should()
            .BeFalse();

        // The assertion held only on a path the engine abandoned: a failed alternative, a lookahead
        // whose continuation failed, an atomic group that failed, or a negative lookaround's body.
        Spans("(?:^b|)(?:a){i<=1}", "xa").Should().Equal((1, 2));
        Spans("(?:(?=^)(?!)|)(?:a){i<=1}", "xa").Should().Equal((1, 2));
        Spans("(?:(?>^b)|)(?:a){i<=1}", "xa").Should().Equal((1, 2));
        Spans("(?:(?!^b)a){i<=1}", "xa").Should().Equal((1, 2));

        // An assertion after the first character is not passed at the anchor.
        new FuzzyRegex("(?:a^){i<=1}")
            .Match("xa")
            .Success.Should()
            .BeFalse();
    }

    // The planted-bug control for the dominance analysis. The oracle entry
    // `fuzzy-insertion-at-an-anchor-pinned-on-the-path` is keyed on switching the pins off, which a
    // wrong analysis passes too (switched off, upstream's answer; live, an insertion at the anchor),
    // so the oracle would classify that bug as the fix. These rows are what catch it: joining paths
    // by union instead of intersection pins an assertion that only one path to the item passes.
    [Test]
    [Arguments("(?:^b|)(?:a){i<=1}", "xa", 1, 2, 0, 2)]
    [Arguments("(?:^b)?(?:a){i<=1}", "xa", 1, 2, 0, 2)]
    [Arguments(@"(?:\bb)*(?:a){i<=1}", "xa", 1, 2, 0, 2)]
    [Arguments("(?:^|x)(?:ab){i<=1}", "xab", 0, 3, 0, 3)]
    public void An_assertion_on_one_path_only_is_caught_if_the_analysis_pins_it(
        string pattern,
        string text,
        int start,
        int end,
        int plantedStart,
        int plantedEnd
    )
    {
        var sound = new FuzzyRegex(pattern);
        var planted = new FuzzyRegex(pattern);
        Fuzzy.Text.RegularExpressions.Engine.Optimiser.FindAnchorPins(planted.PatternObject, unionAtMerges: true);

        Match right = sound.Match(text);
        Match wrong = planted.Match(text);

        (Span(right), right.FuzzyCounts.Insertions).Should().Be(((start, end), 0));
        (Span(wrong), wrong.FuzzyCounts.Insertions)
            .Should()
            .Be(((plantedStart, plantedEnd), 1), "the planted bug must be one these rows see");
    }

    private static (int Start, int End) Span(Match m) => (m.Index, m.Index + m.Length);

    private static (int Start, int End)[] Spans(string pattern, string text) =>
        [.. new FuzzyRegex(pattern).Matches(text).Select(Span)];
}
