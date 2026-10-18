using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// What a partial match promises, and the answers that follow from it. A partial says only that
/// the text ran out while some path through the pattern was still alive, so more text may complete
/// the match. It does not promise that any text will. The other direction is a guarantee: where some
/// longer text completes the match, a partial call never answers no match.
/// </summary>
/// <remarks>
/// This is PCRE2's contract ("more characters may result in a complete match", pcre2partial,
/// fetched 2026-10-08). PCRE2 10.47, Java's <c>hitEnd()</c>, Boost's <c>match_partial</c> and
/// upstream 2026.9.10 all give partials that no text completes, of the kinds below (survey in
/// section 6 of the design branch <c>design/d19-runout-presumption</c>). The owner chose this
/// contract on 2026-10-08 over building a checker for the rest of the pattern (about 1,000 engine
/// lines that would still leave documented gaps). Every row here is today's answer, pinned as the
/// contract; the judge's answer, where it differs, is named beside it. The rows were known defects
/// D18, D19, D39, D53 and D74 until that decision. The designs that would remove most of them stay
/// on <c>design/d19-runout-presumption</c> in case the decision is reopened.
/// </remarks>
public sealed class PartialContractTests
{
    /// <summary>
    /// The documented example: <c>ba(*FAIL)</c> over 'b' is a partial, because the text ran out
    /// while the path still wanted an 'a'. No text completes it, since <c>(*FAIL)</c> fails
    /// whatever follows, and every longer text answers no match.
    /// </summary>
    [Test]
    [Arguments("b", 0, 1)]
    [Arguments("ba", -1, -1)]
    [Arguments("bax", -1, -1)]
    [Arguments("bb", -1, -1)]
    public void A_partial_promises_only_that_more_text_may_complete_the_match(string text, int start, int end)
    {
        ShouldAnswer("match", "ba(*FAIL)", text, start, end);
    }

    /// <summary>
    /// Tests at the edge of the text are each judged on their own (was D18; the D11 design's limits
    /// C3, C4 and C5). Two tests that no single continuation satisfies together each pass, so the
    /// call answers a partial; the judge answers None (or a partial at the edge for a search).
    /// </summary>
    /// <remarks>
    /// <c>\b</c> and <c>\B</c>, <c>\B</c> and <c>$</c>, or a lookahead and the item after it
    /// (<c>b(?=a)c</c>) each read the text to come independently. The fuzzy rows are the same: the
    /// lookahead's run-out needs 'd' (or 'c') next and the tail's <c>x</c> needs 'x' there. Where a
    /// cut or a conditional test read the edge, the alternatives made after that read are kept, so
    /// the call goes on to a path no text completes (N4-N6, T8 and D38's C4 rows,
    /// <c>docs/plan/2026-09-28-partial-boundary-design.md</c> section 7). X1 is the fork budget
    /// firing: every lookahead has two successes with different captures, and the backreferences
    /// turn the repeat guards off. Upstream 2026.9.10, measured 2026-10-08: a partial for the
    /// <c>a\b\B</c> search at (1, 1), the C4 search at (2, 2), and <c>b(?=a)c</c>, X1 and the
    /// four fuzzy rows as here; None on the rest. The rows answering None are right under any
    /// contract and guard the same code.
    /// </remarks>
    [Test]
    [Arguments("search", @"a\b\B", "a", 0, 1)]
    [Arguments("match", @"a\b\B", "a", 0, 1)]
    [Arguments("match", @"b(?=a)c", "b", 0, 1)]
    [Arguments("match", @"a\B$", "a", 0, 1)]
    [Arguments("match", @"a\b(?:(*PRUNE)(?<=b)|)", "a", 0, 1)]
    [Arguments("match", @"(?>a\b(?:|()))(?(1)|(?<=b))", "a", 0, 1)]
    [Arguments("match", @"(?=a\b(?:|()))(?(1)|(?<=b))", "a", 0, 1)]
    [Arguments("match", @"(?:(?=a\B|)(?<!a)a(*PRUNE)(?<=b)|a\B)", "a", -1, -1)]
    [Arguments("match", @"(?:(?=(.*)z|())a)*b\1\2", "aaaaaaaaaaaaaaaaaaaac", 0, 21)]
    [Arguments("match", @"a(?=(?:(?:bcd){1<=e<=1}))(?:b|c){1<=e<=1}x", "aby", 0, 3)]
    [Arguments("search", @"a(?=(?:(?:bcd){1<=e<=1}))(?:b|c){1<=e<=1}x", "aby", 0, 3)]
    [Arguments("match", @"a(?=(?:b(?:cd){1<=e<=1}))(?:b|c){1<=e<=1}x", "aby", 0, 3)]
    [Arguments("search", @"a(?=(?:b(?:cd){1<=e<=1}))(?:b|c){1<=e<=1}x", "aby", 0, 3)]
    [Arguments("match", @"(?:(?(?=a$|a)|b)){e<=1}a(*PRUNE)b", "aa", -1, -1)]
    [Arguments("match", @"(?(?=(?>ab$|a))ac|abc)", "ab", 0, 2)]
    [Arguments("fullmatch", @"(?(?=(?>ab$|a))ac|abc)", "ab", 0, 2)]
    [Arguments("search", @"(?(?=(?>ab$|a))ac|abc)", "ab", 0, 2)]
    [Arguments("match", @"(?(?=(?=ab$|a))ac|abc)", "ab", -1, -1)]
    [Arguments("fullmatch", @"(?(?=(?=ab$|a))ac|abc)", "ab", -1, -1)]
    [Arguments("match", @"(?(?=ab$(*PRUNE)|a)ac|abc)", "ab", 0, 2)]
    [Arguments("fullmatch", @"(?(?=ab$(*PRUNE)|a)ac|abc)", "ab", 0, 2)]
    public void Tests_at_the_edge_are_judged_one_at_a_time(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        ShouldAnswer(operation, pattern, text, start, end);
    }

    /// <summary>
    /// Once a path takes a character past the end of the text, the rest of the pattern is presumed
    /// to match (was D19; the D11 design's limit C2). The judge answers None on the anchored rows,
    /// and a partial at the edge, or None, on the searches.
    /// </summary>
    /// <remarks>
    /// <c>ba(*FAIL)</c> and <c>(?:a\B)+</c> are the plain cases. In the fuzzy search at start 2 the
    /// outer section spends its one error on 'y', and the lookahead's body and the tail's
    /// <c>(b|c)</c> then run out, each presumed to match, though each needs another error. After
    /// 'a' or '\r' at the edge a combining mark or a line feed can extend the cluster, so
    /// <c>\X</c>'s closing test takes the next character and the rest is presumed; a trailing
    /// insertion runs out the same way. Inside a lookahead's body the repeat's run-out leaves the
    /// rest of the body presumed, so the negative lookahead stays undetermined, though after any
    /// number of 'ab' the lookbehind holds. Upstream 2026.9.10, measured 2026-10-08: both
    /// <c>ba(*FAIL)</c> rows as here, the fuzzy search at (0, 3), the cluster searches at (1, 1)
    /// and the lookahead searches at (2, 2); None on the rest. The guarantee in the other direction is
    /// <c>PartialEdgeReadTests</c>' and <c>PartialLookaroundRuleTests</c>': <c>.*?(?&lt;=c)</c>
    /// over 'aa' and <c>(?!(?&gt;(?:ab)*)(?&lt;=^ab))ab</c> over 'ab' are partials that 'aac' and
    /// 'abab' complete.
    /// </remarks>
    [Test]
    [Arguments("match", @"ba(*FAIL)", "b", 0, 1)]
    [Arguments("search", @"ba(*FAIL)", "b", 0, 1)]
    [Arguments("fullmatch", @"(?:a\B)+", "a", 0, 1)]
    [Arguments("search", @"(?:a(?=(?:(?:bcd){1<=e<=2}|b))(?:b|c){1<=e<=1}x){0<=e<=1}", "aby", 2, 3)]
    [Arguments("search", @"(?:\X){d<=1}(*FAIL)|c", "a", 0, 1)]
    [Arguments("search", @"\X(*FAIL)|c", "a", 0, 1)]
    [Arguments("match", @"\X(*FAIL)|c", "a", 0, 1)]
    [Arguments("fullmatch", @"(?:\X){d<=1}\b", "\r", 0, 1)]
    [Arguments("match", @"(?:ab){i<=1}(*FAIL)", "ab", 0, 2)]
    [Arguments("match", @"(?!(?:ab)*+(?<=b))ab", "ab", 0, 2)]
    [Arguments("match", @"(?!(?>(?:ab)*)(?<=b))ab", "ab", 0, 2)]
    [Arguments("search", @"(?!(?:ab)*(?<=b))ab", "ab", 2, 2)]
    [Arguments("match", @"(?!(?=(?:ab)*+(?<=b)))ab", "ab", 0, 2)]
    [Arguments("search", @"(?!(?=(?:ab)*+(?<=b)))ab", "ab", 0, 2)]
    public void A_run_out_presumes_the_rest_of_the_pattern_can_match(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        ShouldAnswer(operation, pattern, text, start, end);
    }

    /// <summary>
    /// A repeated lookahead whose body can end only at the edge (was D39): the first iteration's
    /// lookahead runs out and is presumed to hold, so the call answers a partial, though a second
    /// iteration needs an 'a' after the 'a' and then the lookahead's <c>$</c> fails. The judge
    /// answers None. Upstream 2026.9.10 answers the same partial.
    /// </summary>
    [Test]
    [Arguments("match", @"(?:(?=(?:a|ab)$)a){2,}b", "a", 0, 1)]
    [Arguments("match", @"(?:x(?R)|(?=(?:a|ab)$)a)b", "xa", 0, 2)]
    public void A_repeated_lookahead_that_can_end_only_at_the_edge_answers_a_partial(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        ShouldAnswer(operation, pattern, text, start, end);
    }

    /// <summary>
    /// The skip-fail idiom <c>X(*SKIP)(*F)|Y</c> (was D53): the search reports its partial at the
    /// first start whose path was alive when the text ran out, (0, 3), though every path from there
    /// reaches <c>(*F)</c>. The leftmost start that a continuation completes is 3 ('bbb' + 'a').
    /// Upstream 2026.9.10 and PCRE2 answer the same (0, 3).
    /// </summary>
    [Test]
    public void A_skip_fail_search_reports_the_partial_where_the_path_ran_out()
    {
        ShouldAnswer("search", @"b+(*SKIP)(*F)|a", "bbb", 0, 3);
    }

    /// <summary>
    /// A lookahead the held text already decides is still treated as undetermined in two shapes
    /// (was D74): the run-out lies inside a group call that a condition's test opened (the SHORTCUT
    /// on <c>Matcher.RunOutInALookaroundBody</c>), or the lookahead's body sets a group the pattern
    /// reads later. No continuation completes either row; main before D11 and upstream 2026.9.10
    /// answer None.
    /// </summary>
    [Test]
    [Arguments(@"(?(?=(?1))x|ab)((?:ab)*(?<=b))?")]
    [Arguments(@"(?>(?=((?:ab)*)(?<=b))|ab)(?(1)x|q)")]
    public void A_decided_lookahead_inside_a_call_or_with_a_read_capture_answers_a_partial(string pattern)
    {
        ShouldAnswer("match", pattern, "ab", 0, 2);
    }

    /// <summary>
    /// A run-out inside a group call, where the body that opened the call feeds a decision (a
    /// negative lookahead, a condition's test, a lookahead inside an atomic group, or any
    /// lookaround in a fuzzy pattern), answers the partial at once (was D101; owner decision
    /// 2026-10-18). The same pattern with the call written out can answer None, so here a call
    /// does not answer as its group written out (DECISIONS, D40), which still holds for every full
    /// match.
    /// </summary>
    /// <remarks>
    /// The judge (brute force over every continuation of up to three characters, on the
    /// written-out form) answers None on the four match rows and P(2, 2) on the search: no text
    /// completes a match from 0, and 'xyxx' completes one from 2. Reporting a start earlier than
    /// the first a continuation completes is the skip-fail row's kind (D53). Upstream answers the
    /// first three rows as here; PCRE2 10.47 gives the same partials for them and for their
    /// written-out forms too (<c>.scratch/s1016/refuse-partial/NOTES.md</c>, "PCRE2 10.47"). The
    /// fix, re-opening the calls a body opened (D98's option B), is a 1.1 candidate. Evidence: D98's
    /// grid (124 of 2,640 rows, <c>.scratch/s1016/d98/grid-vs-written.txt</c>) and the fuzzy grid
    /// (421 phantoms and 188 earlier starts of 3,240 rows, no miss,
    /// <c>.scratch/s1016/refuse-partial/fz/diff.txt</c>). In a fuzzy pattern that has any group
    /// call, a run-out inside any lookaround answers at once whether or not the body opened a call
    /// (<c>Matcher.ACallOpenedInTheBodyIsOpenOrUnmodelled</c> holds for every fuzzy pattern): the
    /// last row's body opens none, no text completes it, and without the call it answers None
    /// (battery run 9, matrix k43; upstream 2026.9.10 answers P(0, 1) in both forms).
    /// </remarks>
    [Test]
    [Arguments("match", @"(?!(?:a*a){s<=1})(?P<g1>)(?1)", "a", 0, 1)]
    [Arguments("match", @"(x)(?!y?(?1))\Z", "xy", 0, 2)]
    [Arguments("match", @"(x)(?(?=y?(?1))a|b)", "xy", 0, 2)]
    [Arguments("match", @"(x)(?>(?=(?1)))\Z", "x", 0, 1)]
    [Arguments("match", @"(x)(?:(?=(?1))\Z){e<=1}", "xy", 0, 2)]
    [Arguments("search", @"(x)(?:(?=(?1))\Z){e<=1}", "xy", 0, 2)]
    public void A_run_out_inside_a_call_a_deciding_body_opened_answers_a_partial(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        ShouldAnswer(operation, pattern, text, start, end);
    }

    /// <summary>
    /// The guarantee holds for the rows above: over every text that starts with the row's text and
    /// goes on by up to three characters, the call form answers every full call as its written-out
    /// form does, and a partial call never answers None, or a different complete match, where the
    /// written-out form finds one.
    /// </summary>
    [Test]
    [Arguments(@"(x)(?!y?(?1))\Z", @"(x)(?!y?(?:x))\Z", "xy")]
    [Arguments(@"(x)(?(?=y?(?1))a|b)", @"(x)(?(?=y?(?:x))a|b)", "xy")]
    [Arguments(@"(x)(?>(?=(?1)))\Z", @"(x)(?>(?=(?:x)))\Z", "x")]
    [Arguments(@"(x)(?:(?=(?1))\Z){e<=1}", @"(x)(?:(?=(?:x))\Z){e<=1}", "xy")]
    public void A_call_a_deciding_body_opened_never_misses_a_match_its_written_out_form_completes(
        string pattern,
        string writtenOut,
        string text
    )
    {
        var call = new FuzzyRegex(pattern);
        var written = new FuzzyRegex(writtenOut);
        var texts = new List<string> { text };
        for (int i = 0; i < texts.Count && texts[i].Length < text.Length + 3; i++)
        {
            foreach (char c in "xyabq")
            {
                texts.Add(texts[i] + c);
            }
        }

        var misses = new List<string>();
        foreach (string t in texts)
        {
            foreach (var (name, run) in _calls)
            {
                string byCall = Describe(run(call, t, false));
                string byWritten = Describe(run(written, t, false));
                if (!string.Equals(byCall, byWritten, StringComparison.Ordinal))
                {
                    misses.Add($"{name} '{t}': {byCall}, written out {byWritten}");
                }

                Match partialByCall = run(call, t, true);
                Match partialByWritten = run(written, t, true);
                bool writtenCompletes = partialByWritten.Success && !partialByWritten.PartialMatch;
                if (
                    (partialByWritten.Success && !partialByCall.Success)
                    || (
                        writtenCompletes
                        && !string.Equals(Describe(partialByCall), Describe(partialByWritten), StringComparison.Ordinal)
                    )
                )
                {
                    misses.Add(
                        $"{name} partial '{t}': {Describe(partialByCall)}, written out {Describe(partialByWritten)}"
                    );
                }
            }
        }

        misses.Should().BeEmpty();
    }

    /// <summary>The search row's completion, where the judge says one exists: 'xyxx' matches at (2, 4).</summary>
    [Test]
    public void The_earlier_partial_start_still_finds_the_match_a_continuation_completes()
    {
        Describe(new FuzzyRegex(@"(x)(?:(?=(?1))\Z){e<=1}").Match("xyxx", partial: true)).Should().Be("M(2, 4)");
    }

    private static readonly (string Name, Func<FuzzyRegex, string, bool, Match> Run)[] _calls =
    [
        ("match", static (r, t, p) => r.MatchAtStart(t, partial: p)),
        ("fullmatch", static (r, t, p) => r.FullMatch(t, partial: p)),
        ("search", static (r, t, p) => r.Match(t, partial: p)),
    ];

    private static string Describe(Match m)
    {
        if (!m.Success)
        {
            return "None";
        }

        string kind = m.PartialMatch ? "P" : "M";
        return $"{kind}({m.Index}, {m.Index + m.Length})";
    }

    /// <summary>Asserts a partial call's answer: a partial over [start, end), or no match when start is -1.</summary>
    private static void ShouldAnswer(string operation, string pattern, string text, int start, int end)
    {
        var regex = new FuzzyRegex(pattern);
        Match m = operation switch
        {
            "match" => regex.MatchAtStart(text, partial: true),
            "fullmatch" => regex.FullMatch(text, partial: true),
            _ => regex.Match(text, partial: true),
        };

        bool found = start >= 0;
        (m.Success, m.PartialMatch, m.Success ? m.Index : -1, m.Success ? m.Index + m.Length : -1)
            .Should()
            .Be((found, found, start, end));
    }
}
