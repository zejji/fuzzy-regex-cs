using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.OpenDefects;

/// <summary>
/// One test per known, unfixed defect whose right answer is settled. They are <c>[Explicit]</c>,
/// so the ratchet does not run them; the number that fail is the measured size of the remaining
/// correctness work. The first step of each fix moves its test into the suite proper.
/// </summary>
/// <remarks>
/// Run them with
/// <c>dotnet run --project tests/FuzzyRegex.Tests -- --treenode-filter "/*/*/OpenDefectTests/*"</c>.
/// Upstream's answers were measured against <c>regex</c> 2026.9.10 on 2026-09-27 and are quoted
/// beside each assertion. Where upstream is wrong the expected value is argued from principle, and
/// the argument is written above the test.
/// </remarks>
[Explicit]
public sealed class OpenDefectTests
{
    // Queue item 9. A negative lookahead succeeds only when its body fails, so nothing its body
    // captured survives; group 1 keeps only its own capture. Upstream agrees when the body spells
    // the group out (`(?!.(?:a))` gives ['a']) but keeps an entry when the body calls it (['a', 'a']).
    [Test]
    public void A_group_call_inside_a_failed_lookaround_leaves_no_capture_behind()
    {
        Match m = new FuzzyRegex("(a)(?:(?!.(?1))|.)+?b").Match("aaab");

        m.Success.Should().BeTrue();
        m.Groups[1].Captures.Select(static c => (c.Index, c.Length)).Should().Equal((0, 1));
    }

    // Blind review of the refined call guard, 2026-09-28 (ledger entry 14, "The guard refined").
    // Each pattern needs two calls of itself open at 0 at once. The second reaches no new text, but
    // a group set between the two calls changes what it does: a conditional or backreference reads
    // that group, so the inner call is not a repeat of the outer one and the path is finite.
    // Upstream 2026.9.10 answers (0, 1) for every row, search and fullmatch alike (measured
    // 2026-09-28); PCRE2 10.47 raises "nested recursion at the same subject position" on the first.
    // The port answers None, because the guard's key holds no capture state.
    [Test]
    [Arguments(@"(?(a)(?(b)x|(?<b>)(?R))|(?<a>)(?R))")]
    [Arguments(@"(?:\1x|\2()(?R)|()(?R))")]
    [Arguments(@"(?:(?P=b)x|(?P=a)(?<b>)(?R)|(?<a>)(?R))")]
    public void A_call_that_reaches_nothing_new_but_sees_a_new_capture_is_let_through(string pattern)
    {
        var regex = new FuzzyRegex(pattern);

        Match search = regex.Match("x");
        Match full = regex.FullMatch("x");

        (search.Success, search.Index, search.Length).Should().Be((true, 0, 1));
        (full.Success, full.Index, full.Length).Should().Be((true, 0, 1));
    }

    // D17, found by D12's survey. An empty iteration counts as progress when it changes the span of
    // a tested group (upstream's rule, kept by D12). Here each pass flips group g between (0, 1) and
    // (0, 2), so every pass is a change and the repeat never stops. No pass reads text and '$'
    // cannot hold at 0 in 'ab', so the answer is no match, which PCRE2 10.47 and Perl 5.42 give at
    // once. Upstream: MemoryError after 1.4 s. The port exhausts its 1 GB backtrack stack.
    [Test]
    public void An_empty_iteration_that_flips_a_tested_group_between_two_spans_stops()
    {
        new FuzzyRegex(@"^(?:(?=(?P=g)b)(?=(?P<g>ab))|(?=(?P<g>a)))*$", FuzzyRegexOptions.None, TimeSpan.FromSeconds(5))
            .Match("ab")
            .Success.Should()
            .BeFalse();
    }

    // D11 (docs/plan/2026-09-28-partial-boundary-design.md; the ids are its table's). A partial
    // means some longer text completes the match (upstream/README.rst:270). In each row no
    // continuation can: the held text already decides the boundary (B1, B2, B7), a later item
    // fails on held text (B6, L2, L5), a full match would need the text to end where the pattern
    // stops (B3, B5), the negative lookahead's body matches the held text whatever follows (L6,
    // L7), or `\Z` needs the text to end before `b` (E1). Measured 2026-09-28 by
    // tools/probes/d11-partial-boundary.py: upstream None on every row except L2, L5, L6 and E1,
    // where upstream and PCRE2 both answer a partial; the port answers a partial on every row.
    // Review round 1 adds V4, where the (*PRUNE) runs before the boundary and so prunes for every
    // continuation, R1, R2, where a reversed `\A` or `^` at the left edge is false once any text is
    // added there (upstream and the port: partial), and E3, where `$` at a final newline is false
    // once text follows the newline (upstream None, port partial). Review round 2 adds N1-N6: a
    // cut must stop at a decision about the text to come, and at nothing else. In N1 and N2 the
    // atomic group's second path commits once it is reached. In N3 the end-of-string test is
    // decided, so the prune verb prunes. In N4-N6 the alternatives made after the word boundary go
    // with the cut. Upstream: None on all six. The port answers a partial on N1, N4, N5 and N6
    // today; N2 and N3 guard the design. Round 3 (design section 7) moves N4-N6 to the D18 test
    // below, because the design keeps the alternatives a blocked cut leaves, and adds X2, where the
    // design keeps the repeat guards on a path that read the text to come: `(?:(?(?=.*z)a|a))*b`
    // over 'a' * 20 + 'c' has no 'b' to find whatever follows, but upstream and the port answer
    // P(0, 21).
    [Test]
    [Arguments("match", @"a\m", "a")]
    [Arguments("match", @" \M", " ")]
    [Arguments("fullmatch", @"a\B", "a")]
    [Arguments("fullmatch", @"(?fV1mi)(?:\p{Ll}(*SKIP)[^\d]|\p{Ll})([[:alpha:]])\B$", "aaA")]
    [Arguments("match", @"a\b(?<=b)", "a")]
    [Arguments("match", "(?fai)^\U0001D518\\M", "\U0001D518")]
    [Arguments("match", @"(?=..)[ab]", "x")]
    [Arguments("match", @"(?(?=..)a|b)", "c")]
    [Arguments("match", @"(?!.a|x)x", "x")]
    [Arguments("match", @"(?!a(?:\B(?<=b)|))a", "a")]
    [Arguments("match", @"a\Zb", "a")]
    [Arguments("match", @"(?:a(*PRUNE)\b(?<=b)|a.)", "a")]
    [Arguments("match", @"(?r)(?<=b)\Aa", "a")]
    [Arguments("match", @"(?r)(?<=b)^a", "a")]
    [Arguments("match", "a$\n\\b", "a\n")]
    [Arguments("match", @"(?>x\b|x|)(?<!x)x", "x")]
    [Arguments("match", @"(?>a\Z|a|)(?<!a)a", "a")]
    [Arguments("match", @"(?:a\Z(?<=b)|a(*PRUNE)(?<=b)|a)", "a")]
    [Arguments("match", @"(?:(?(?=.*z)a|a))*b", "aaaaaaaaaaaaaaaaaaaac")]
    public void A_partial_is_refused_where_no_longer_text_can_complete_the_match(
        string operation,
        string pattern,
        string text
    )
    {
        Partial(operation, pattern, text).Success.Should().BeFalse();
    }

    // D11, the other direction: some continuation completes each row, so the answer is a partial
    // spanning from the leftmost start that a continuation completes to the end of the text.
    // W1-W5 are owner ruling Q2 = B, a complete match whose word boundary read past the end ('a:'
    // followed by 'a' keeps 'a:a' one word under UAX #29 WB6, WB4 and WB12). L1 and A2 start where
    // the lookahead or the `\m` still can hold; A5, P1, P2 and P3 are zero-width attempts that a
    // word character after the truncation point completes (P1 and P2 invert
    // PartialMatchingTests.A_reverse_search_for_a_boundary_at_the_end_of_an_empty_subject_reports_the_partial_a_word_character_completes
    // and P3 inverts A_boundary_that_consumed_nothing_reports_the_partial_a_word_character_completes, see the design's open
    // questions). S3 is the search convention the slow path already follows, (1, 1) for `\ma` over
    // 'b', reached through the prefilter. Upstream, measured 2026-09-28: W1 None, W2 (3, 3), W3 None,
    // W5 None, L1 (1, 1), A2 (2, 2), A5 None, P1 (0, 0), P2 None, P3 None, S3 (1, 1).
    // Review round 1 adds V1-V3, a verb reached only after a boundary the continuation decides ('ab'
    // makes the `\b` fail, so `a.` matches), and D1-D4, the path an atomic group or possessive repeat
    // takes when the continuation makes its first path fail ('xa', 'abc', 'aa', 'ab'). Upstream: None
    // on all but V3 (1, 1). V1, V2 and D1-D3 pass today only through S57d's escalation, which the
    // design deletes; they guard the design's verb and commit rules. W6 is WB7b: a Hebrew letter,
    // a double quote and a Hebrew letter to come stay one word (upstream and the port: None).
    // N7 and N8 came from tools/probes/d11-universe-model.py's grid, and each broke a draft of the
    // round-2 invariant: N7 ('b' + 'aa') needs the outer lookahead's frame kept for the decision's
    // other world, N8 (' b' + 'a') needs a run-out inside a lookaround to mean also "takes the
    // text and fails after the atomic group commits". Upstream and the port: partial today.
    // Round 3 (design section 7) adds its guard rows, which the port answers today only through
    // S57d's escalation or a run-out ending the whole match, both of which the design removes:
    // Q1 and T2 (a skipped verb blocks its own scope), Q2 and T3 (a capture made after the edge is
    // unknown), Q3 (a run-out taints the verb after the edge), T1 (a path reached back through a
    // skipped cut), T4-T6 (a condition whose test read the text to come), G1 (no repeat guard
    // inside a lookaround body while a taint lives) and F1 (the dedup key holds the fuzzy counts).
    // Upstream 2026.9.10, measured 2026-09-28: Q2, Q3, T3 and G1 partial, the others None.
    [Test]
    [Arguments("match", @"(?w)a\B", "a:", 0, 2)]
    [Arguments("search", @"(?w)a\B", "xa:", 1, 3)]
    [Arguments("match", "(?w)a\\B", "a:\u0301", 0, 3)]
    [Arguments("match", @"(?w)1\B", "1,", 0, 2)]
    [Arguments("search", @"(?=..)[ab]", "x", 1, 1)]
    [Arguments("search", "(?fi)\\m\u0130(?(?<!\\w)[^a-f]|\\p{Ll})$", "\uFB01 ", 2, 2)]
    [Arguments("match", @"(?r)[a\d]\M", "", 0, 0)]
    [Arguments("search", @"(?r)\b$", "", 0, 0)]
    [Arguments("match", @"(?r)\b$", "", 0, 0)]
    [Arguments("search", @"\b", "", 0, 0)]
    [Arguments("search", "(?i)\\m\u0130", "b", 1, 1)]
    [Arguments("match", @"(?:a\b(*PRUNE)(?<=b)|a.)", "a", 0, 1)]
    [Arguments("match", @"(?:a\b(*SKIP)(?<=b)|a.)", "a", 0, 1)]
    [Arguments("search", @"a\b(*SKIP)(?<=b)|a.", "a", 0, 1)]
    [Arguments("match", @"(?>x\b|)(?<!x)x", "x", 0, 1)]
    [Arguments("match", @"(?>ab\b|a)(?<=a)b", "ab", 0, 2)]
    [Arguments("match", @"(?:a\b)?+(?<!a)a", "a", 0, 1)]
    [Arguments("match", @"(?>a\Z|)(?<!a)a", "a", 0, 1)]
    [Arguments("match", "(?w)א\\B", "א\"", 0, 2)]
    [Arguments("match", @"b(?!(?!\w.))", "b", 0, 1)]
    [Arguments("match", @".(?!(?>\wa|(?<!b))(?<!a))", " b", 0, 2)]
    [Arguments("match", @"(?!(?>a\b|)(?<!a)a(*PRUNE)(?<=b)|a)", "a", 0, 1)]
    [Arguments("match", @"(?=a(.))(?(1)a|b)", "a", 0, 1)]
    [Arguments("match", @"(?!a.(*PRUNE)b|a)a", "a", 0, 1)]
    [Arguments("match", @"(?:(?>a\B|)(?<!a)a(*PRUNE)(?<=b)|a\B)", "a", 0, 1)]
    [Arguments("match", @"(?!a\B(*PRUNE)(?<=b)|a)", "a", 0, 1)]
    [Arguments("match", @"(?=a(.))\1", "a", 0, 1)]
    [Arguments("match", @"(?(?=a\b)(?<=b)|a)", "a", 0, 1)]
    [Arguments("match", @"(?(?!a\b)a|(?<=b))", "a", 0, 1)]
    [Arguments("match", @"(?:(?(?=a\B(*PRUNE)(?<=b))x|a\B))", "a", 0, 1)]
    [Arguments("match", @"(?:b|)(?=b*(?!b)\B)(?<!b)", "bb", 0, 2)]
    [Arguments("match", @"(?:(?=(?:ax\b|ay\B){e<=1})az){e<=1}", "ay", 0, 2)]
    public void A_partial_is_reported_where_longer_text_completes_the_match(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        Match m = Partial(operation, pattern, text);

        m.Success.Should().BeTrue();
        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
    }

    // D11 round 3, S4: in the second pass every start's lookahead reads the text to come, so every
    // start but the edge fails on 'c' and the leftmost completable start is the edge itself
    // ('...c' + 'bz' matches at 1001). Upstream and the port answer P(0, 1001) today, from the
    // first start's run-out. The design's memo and per-attempt budget keep this search at the first
    // pass's order (section 9).
    [Test]
    public void A_search_whose_lookahead_reads_the_text_to_come_from_every_start_reports_the_edge()
    {
        Match m = Partial("search", @"a*(?=.*z)b", new string('a', 1000) + "c");

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((1001, 1001));
    }

    // D18 (the design's limits C3, C4 and C5, one SHORTCUT). No continuation completes any row, but
    // the D11 design answers a partial on each, as the port does today (measured 2026-09-28):
    // J1 and J2 take two tests at the edge independently (`\b` and `\B`, `\B` and `$`), which no
    // single continuation satisfies together; N4, N5, N6 and T8 keep the alternatives made after
    // the read that blocked a cut; X1 is the fork budget firing, where every lookahead has two
    // successes with different captures and a backreference turns the repeat guards off. This
    // stays red until D18 is built, after D11.
    [Test]
    [Arguments("match", @"a\b\B", "a")]
    [Arguments("match", @"a\B$", "a")]
    [Arguments("match", @"a\b(?:(*PRUNE)(?<=b)|)", "a")]
    [Arguments("match", @"(?>a\b(?:|()))(?(1)|(?<=b))", "a")]
    [Arguments("match", @"(?=a\b(?:|()))(?(1)|(?<=b))", "a")]
    [Arguments("match", @"(?:(?=a\B|)(?<!a)a(*PRUNE)(?<=b)|a\B)", "a")]
    [Arguments("match", @"(?:(?=(.*)z|())a)*b\1\2", "aaaaaaaaaaaaaaaaaaaac")]
    public void A_blocked_cut_keeps_the_alternatives_made_after_the_read(string operation, string pattern, string text)
    {
        Partial(operation, pattern, text).Success.Should().BeFalse();
    }

    private static Match Partial(string operation, string pattern, string text)
    {
        var regex = new FuzzyRegex(pattern);

        return operation switch
        {
            "match" => regex.MatchAtStart(text, partial: true),
            "fullmatch" => regex.FullMatch(text, partial: true),
            _ => regex.Match(text, partial: true),
        };
    }
}
