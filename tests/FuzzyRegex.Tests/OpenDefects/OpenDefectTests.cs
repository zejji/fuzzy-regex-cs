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
    // D11 amendment 2's fuzzy rows and its review's row, re-attributed by build chunk 8 (review
    // round 1). The first six are C3: the lookahead's run-out needs 'd' (or 'c') next and the tail's
    // x needs 'x' there, and each read is judged alone. The fuzzy-free twin `a(?=byd)byx` over 'aby'
    // answers the same P(0, 3) with the same second-pass events (a run-out in the body, then one
    // outside it). The last is C4: every path reaches the (*PRUNE) and acts there, but the path
    // through the test's first branch read the edge, so the verb is skipped and the section's
    // trailing insertion goes on; its fuzzy-free twin `(?:(?=aa$|a)|a)a(*PRUNE)b` over 'aa' answers
    // the same P(0, 2) with the same skipped verbs. Upstream answers None to all of them.
    [Arguments("match", @"a(?=(?:(?:bcd){1<=e<=1}))(?:b|c){1<=e<=1}x", "aby")]
    [Arguments("search", @"a(?=(?:(?:bcd){1<=e<=1}))(?:b|c){1<=e<=1}x", "aby")]
    [Arguments("match", @"a(?=(?:b(?:cd){1<=e<=1}))(?:b|c){1<=e<=1}x", "aby")]
    [Arguments("search", @"a(?=(?:b(?:cd){1<=e<=1}))(?:b|c){1<=e<=1}x", "aby")]
    [Arguments("match", @"(?:(?(?=a$|a)|b)){e<=1}a(*PRUNE)b", "aa")]
    // D38's C4 rows (re-review of D11 chunk 9's fix round, F2). The test holds for every
    // continuation: `ab$` needs a '\n' to come, and the `a` alternative holds whatever follows, so
    // the yes-branch's `ac` fails on the held 'b' and no continuation reaches the no-branch. The
    // `a` success repeats the first one's continuation (the dedup), but the atomic group's, the inner
    // lookahead's or the skipped verb's taint lies above the test's frame, so that success counts as
    // blocked and the test's failure keeps the no-branch, where `abc` runs out: P(0, 2). The search
    // answers P(0, 2) where the edge is the leftmost completable start. Judges (the port's own and
    // tools/probes/d11-brute-judge.py) and upstream 2026.9.10: None, None, P(2, 2) (2026-10-03).
    [Arguments("match", @"(?(?=(?>ab$|a))ac|abc)", "ab")]
    [Arguments("fullmatch", @"(?(?=(?>ab$|a))ac|abc)", "ab")]
    [Arguments("search", @"(?(?=(?>ab$|a))ac|abc)", "ab")]
    [Arguments("match", @"(?(?=(?=ab$|a))ac|abc)", "ab")]
    [Arguments("fullmatch", @"(?(?=(?=ab$|a))ac|abc)", "ab")]
    [Arguments("match", @"(?(?=ab$(*PRUNE)|a)ac|abc)", "ab")]
    [Arguments("fullmatch", @"(?(?=ab$(*PRUNE)|a)ac|abc)", "ab")]
    public void A_blocked_cut_keeps_the_alternatives_made_after_the_read(string operation, string pattern, string text)
    {
        Match m = Partial(operation, pattern, text);

        // A search has a completion that starts inside the text to come; the partial must say so.
        if (string.Equals(operation, "search", StringComparison.Ordinal))
        {
            (m.PartialMatch, m.Index).Should().Be((true, text.Length));
        }
        else
        {
            m.Success.Should().BeFalse();
        }
    }

    // D19 (the design's limit C2): a run-out presumes that the rest of the pattern can match. At
    // start 2 the outer section spends its one error on 'y', and the lookahead's body and the
    // tail's (b|c) then run out, each presumed to match, though each needs another error. No start
    // completes the match (tools/probes/d11-brute-judge.py, 2026-09-29; the port's own non-partial
    // matcher agrees over every continuation of up to three characters, 2026-09-30). The match
    // row of the same pattern is right since build chunk 8 counts the body's errors in the outer
    // section (PartialLookaroundRuleTests); this one answers P(2, 3). Upstream answers None.
    [Test]
    public void A_run_out_presumes_the_rest_of_the_pattern_can_match()
    {
        Partial("search", @"(?:a(?=(?:(?:bcd){1<=e<=2}|b))(?:b|c){1<=e<=1}x){0<=e<=1}", "aby")
            .Success.Should()
            .BeFalse();
    }

    // D19 again, through a cluster or an insertion that takes text to come (D11 fix round 3,
    // review B1). After 'a' or '\r' at the edge a combining mark or an LF can extend the cluster,
    // so `\X`'s closing grapheme test is undetermined; its failing side takes the next character,
    // runs out, and the rest, which no text can match, is presumed. A trailing insertion (ledger
    // 51) runs out the same way. This is how `.+(*FAIL)|c` has always answered (port, upstream:
    // P(0, 1)); D11 build chunk 4 (bc9ab56f) gave `\X` the read, which is what makes
    // `\X(?<!a)|c` search 'a' P(0, 1) right where main and upstream answer P(1, 1)
    // (PartialCutRuleTests). Right answers: the edge is the leftmost completable start ('a' + 'c')
    // and the anchored calls complete nowhere (the port's non-partial judge over continuations of
    // up to three characters, U+0301 and '\r' included, 2026-10-04). Upstream 2026.9.10: P(1, 1),
    // P(1, 1), None, None, None; main 27b9de33: P(1, 1), P(1, 1), P(0, 1), P(0, 1), None.
    [Test]
    [Arguments("search", @"(?:\X){d<=1}(*FAIL)|c", "a", 1)]
    [Arguments("search", @"\X(*FAIL)|c", "a", 1)]
    [Arguments("match", @"\X(*FAIL)|c", "a", -1)]
    [Arguments("fullmatch", @"(?:\X){d<=1}\b", "\r", -1)]
    [Arguments("match", @"(?:ab){i<=1}(*FAIL)", "ab", -1)]
    public void A_cluster_or_insertion_that_runs_out_presumes_the_rest_of_the_pattern(
        string operation,
        string pattern,
        string text,
        int start
    )
    {
        Match m = Partial(operation, pattern, text);

        (m.Success, m.Success ? m.Index : -1).Should().Be((start >= 0, start));
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
