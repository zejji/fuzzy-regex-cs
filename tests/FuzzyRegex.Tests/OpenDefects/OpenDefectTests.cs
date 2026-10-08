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

    // D4 (the capture-dependent recursion design, section 6b). The `.*z` branch fails only after
    // reading the whole text, so the attempt's reach is already full when the first call is made,
    // no later call can show growth, and the finite left recursion `(?:|(?R)a)` is refused one
    // level down. Upstream 2026.9.10 answers (0, 2) on both rows; PCRE2 10.47 raises "nested
    // recursion at the same subject position" on the first. Without the `.*z|` branch the port
    // answers (0, 2) as well (GroupCallTests).
    [Test]
    [Arguments(@".*z|(?:|(?R)a)", "aa")]
    [Arguments(@"(?:a|(?<g>))*?(?P=g)|(?<g>)a(?R)|(?R)(?P=g)x", "ax")]
    public void A_left_recursion_is_let_through_when_the_reach_is_full_before_the_first_call(
        string pattern,
        string subject
    )
    {
        Match m = new FuzzyRegex(pattern, FuzzyRegexOptions.None, TimeSpan.FromSeconds(30)).FullMatch(subject);

        (m.Success, m.Index, m.Length).Should().Be((true, 0, 2));
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
    // today; N2 and N3 guard the design. Round 3 (design section 7) moves N4-N6 to the D18 rows
    // (now the partial contract's, Gaps/Engine/PartialContractTests), because the design keeps the alternatives a blocked cut leaves, and adds X2, where the
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
    // (W1-W6, owner ruling Q2 = B, moved to PartialWindowReadTests with D11 build chunk 10.) L1 and A2 start where
    // the lookahead or the `\m` still can hold; A5, P1, P2 and P3 are zero-width attempts that a
    // word character after the truncation point completes (P1 and P2 invert
    // PartialMatchingTests.A_reverse_search_for_a_boundary_at_the_end_of_an_empty_subject_reports_the_partial_a_word_character_completes
    // and P3 inverts A_boundary_that_consumed_nothing_reports_the_partial_a_word_character_completes, see the design's open
    // questions). S3 is the search convention the slow path already follows, (1, 1) for `\ma` over
    // 'b', reached through the prefilter. Upstream, measured 2026-09-28: L1 (1, 1), A2 (2, 2), A5
    // None, P1 (0, 0), P2 None, P3 None, S3 (1, 1).
    // Review round 1 adds V1-V3, a verb reached only after a boundary the continuation decides ('ab'
    // makes the `\b` fail, so `a.` matches), and D1-D4, the path an atomic group or possessive repeat
    // takes when the continuation makes its first path fail ('xa', 'abc', 'aa', 'ab'). Upstream: None
    // on all but V3 (1, 1). V1, V2 and D1-D3 pass today only through S57d's escalation, which the
    // design deletes; they guard the design's verb and commit rules.
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
