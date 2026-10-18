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

    // D101. A run-out inside a group call that a lookaround's body or a condition's test opened,
    // where the body's outcome feeds a decision (a negative lookahead, a condition's test, a
    // lookahead inside an atomic group), still answers the partial at once (the SHORTCUT on
    // Matcher.RunOutInALookaroundBody that D98 narrowed). The written-out forms answer None, as the
    // brute-force judge does. In the first row `\Z` would need the text to end at 1, where 'xy'
    // already goes on; in the second the test passes or fails, but 'a' or 'b' is needed at 1, which
    // holds 'y'; in the third `\Z` needs the end at 1, where the lookahead needs an 'x'. Evidence
    // .scratch/s1016/d98/grid-vs-written.txt.
    [Test]
    [Arguments(@"(x)(?!y?(?1))\Z", "xy")] // port P(0, 2)
    [Arguments(@"(x)(?(?=y?(?1))a|b)", "xy")] // port P(0, 2)
    [Arguments(@"(x)(?>(?=(?1)))\Z", "x")] // port P(0, 1)
    public void A_run_out_inside_a_call_a_deciding_body_opened_is_taken_as_its_written_out_form_takes_it(
        string pattern,
        string subject
    )
    {
        Match m = Partial("match", pattern, subject);

        m.Success.Should().BeFalse();
    }

    // D100. A partial reports the least start from which this port's own full search matches some
    // continuation (DECISIONS 2026-09-30), and that search follows rule H'. When the attempt at the
    // anchor and a later partial are both live, H' compares the later partial's start with the
    // attempt's end, which for a partial is the edge, and gets these rows wrong. Too late: over
    // 'ba' the continuation 'b' gives 'bab', which is (0, 3) here (upstream None), so the partial
    // starts at 0, not 1 (upstream P(1, 2)). Too early: `(?:a){i<=1}\b` over 'x' is P(0, 1), but
    // every completion has a match from 1, so P(1, 1) (upstream's answer). No rule over the two
    // partials settles these (DECISIONS 2026-10-17); expected values are the judge's,
    // tools/probes/partial-contract-judge.py.
    [Test]
    [Arguments(@"(?:ab){1<=i<=2}", "ba", 0, 2)]
    [Arguments(@"(?:ab){1<=i<=2}", "xa", 0, 2)]
    [Arguments(@"(?:ab){2<=i<=2}", "xya", 0, 3)]
    [Arguments(@"(?r)(?:ba){1<=i<=2}", "ab", 0, 2)]
    [Arguments(@"(?r)(?:ba){1<=i<=2}", "ax", 0, 2)]
    [Arguments(@"(?:a){i<=1}\b", "x", 1, 1)]
    // Five more from the triage of the rows main and fix/d100 both got wrong
    // (.scratch/s1016/partial-44/triage.md, causes A to E), judged at continuations of up to 5.
    [Arguments(@"(?:(?=a)a){e<=1}", "b", 1, 1)] // too early: 'ba' is (1, 2)
    [Arguments(@"(?:ab){1<=d<=1,i<=1}", " ", 1, 1)] // too early: a deletion needs no more text
    [Arguments(@"(?r)(?:b){i<=2}\B", "a ", 0, 0)] // too early (reversed): 'ba ' completes inside
    [Arguments(@"(?:b){2<=i<=2}", " b", 0, 2)] // too late: ' ba' is (0, 3)
    [Arguments(@"(?r)(?:b\b){i<=2}", "xx", 0, 2)] // too late (reversed): 'bxx' is (0, 3)
    public void A_partial_starts_where_the_full_search_of_some_continuation_starts(
        string pattern,
        string text,
        int start,
        int end
    )
    {
        Match m = new FuzzyRegex(pattern).Match(text, partial: true);

        (m.Success, m.PartialMatch, m.Index, m.Index + m.Length).Should().Be((true, true, start, end));
    }

    // D100 over finditer: the scan's one match is the same partial.
    [Test]
    public void A_partial_scan_starts_where_the_full_search_of_some_continuation_starts()
    {
        Match[] forward = [.. new FuzzyRegex(@"(?:ab){1<=i<=2}").Matches("ba", partial: true)];

        forward.Select(static m => (m.PartialMatch, m.Index, m.Index + m.Length)).Should().Equal((true, 0, 2));
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
