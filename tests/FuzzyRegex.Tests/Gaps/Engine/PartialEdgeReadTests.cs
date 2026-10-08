using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D11 M1, build chunk 2: the word, line and string tests at the edge of a partial call's second
/// pass (<c>docs/plan/2026-09-28-partial-boundary-design.md</c>, section 4.1, and the witnesses of
/// section 7). A test there is decided when every non-empty continuation of the text gives it the
/// same answer, and undetermined otherwise, when it passes and leaves a touch mark.
/// </summary>
/// <remarks>
/// A partial asks whether a longer text could make the call a match (<c>upstream/README.rst:270</c>).
/// Each expected answer below is the brute-force judge's, <c>tools/probes/d11-brute-judge.py</c>
/// (upstream <c>regex</c> 2026.9.10 without partial, over every continuation of one to four
/// characters), measured 2026-09-28 for the design's rows and 2026-09-29 for the rest. Upstream's own
/// partial answers are given where they differ. S57d's escalation is still in place until build
/// chunk 9, so a row it answers (B3, B6 and the fullmatch rows) is not a witness here.
/// </remarks>
public sealed class PartialEdgeReadTests
{
    /// <summary>
    /// A test the held text already decides needs no continuation, so no partial comes of it (rows
    /// B1, B2 and B7). With M1 removed, S57d escalates each of them to a partial.
    /// </summary>
    // DIVERGES FROM UPSTREAM on nothing: upstream answers None to every row.
    [Test]
    // `\m` after a word character: no continuation starts a word there.
    [Arguments("match", @"a\m", "a")]
    // `\M` after a non-word character: no continuation ends a word there.
    [Arguments("match", @" \M", " ")]
    // Under `(?a)` the astral letter is not a word character, so `\M` after it is decided false.
    [Arguments("match", "(?fai)^\U0001D518\\M", "\U0001D518")]
    // Reversed, `\m` before a non-word character.
    [Arguments("match", @"(?r)\m ", " ")]
    public void A_word_test_the_held_text_decides_reports_no_partial(string operation, string pattern, string text)
    {
        Partial(operation, pattern, text).Success.Should().BeFalse();
    }

    /// <summary>
    /// The seed-7 row of the 2026-09-21 oracle wave, which S57d pinned as a partial at (0, 4): its
    /// <c>\m</c> comes after an astral uppercase letter at the end of the text, so no continuation
    /// starts a word there, and the `\m` read decides nothing.
    /// </summary>
    /// <remarks>
    /// Both doors answer the partial (0, 4), limit C2 of the partial contract (was D19). No text
    /// completes the pattern (<c>\m$</c> holds nowhere; the judge answers None), but the lazy
    /// <c>\p{Lu}+?</c> backtracks to the edge and takes a character to come, and after a run-out the
    /// rest is presumed to match. Until D11's run-out fix (2026-10-04) the backtracking repeat stopped
    /// at the held text, so the anchored door answered None and the search (4, 4); the fix accepts
    /// this phantom so that a lazy repeat's real completions are not refused (`.*?(?&lt;=c)` over
    /// 'aa'). A partial promises only that more text may complete the match (owner decision
    /// 2026-10-08, <c>PartialContractTests</c>), so this is the contract's answer.
    /// </remarks>
    // DIVERGES FROM UPSTREAM (None on the anchored door, (4, 4) searched), as limit C2.
    [Test]
    public void A_word_start_after_a_word_character_at_the_end_is_decided_false()
    {
        var regex = new FuzzyRegex(
            @"^(\p{Lu}+?)(?(?<=\p{Nd})[A-Z])\m$",
            FuzzyRegexOptions.IgnoreCase
                | FuzzyRegexOptions.Multiline
                | FuzzyRegexOptions.FullCase
                | FuzzyRegexOptions.Version0
        );

        Match anchored = regex.MatchAtStart("\U00010400\U0001D518", partial: true);
        Match searched = regex.Match("\U00010400\U0001D518", partial: true);

        (anchored.PartialMatch, anchored.Index, anchored.Length).Should().Be((true, 0, 4));
        (searched.PartialMatch, searched.Index, searched.Length).Should().Be((true, 0, 4));
    }

    /// <summary>
    /// Tests that every continuation fails where the first pass, reading the text as ending, passed
    /// them: <c>\Z</c> at the end (decision 3), <c>$</c> at a final newline, and a reversed
    /// <c>\A</c> at the start (rows E1, E3, R1, N2, N3). With M1 removed each is a partial.
    /// </summary>
    // DIVERGES FROM UPSTREAM on E1 and R1, deliberately: upstream answers a partial to both.
    [Test]
    [Arguments("match", @"a\Zb", "a")]
    [Arguments("match", "a$\n\\b", "a\n")]
    [Arguments("match", @"(?r)(?<=b)\Aa", "a")]
    [Arguments("match", @"(?r)(?<=b)^a", "a")]
    [Arguments("match", @"(?>a\Z|a|)(?<!a)a", "a")]
    [Arguments("match", @"(?:a\Z(?<=b)|a(*PRUNE)(?<=b)|a)", "a")]
    public void A_test_every_continuation_fails_is_false_at_the_edge(string operation, string pattern, string text)
    {
        Partial(operation, pattern, text).Success.Should().BeFalse();
    }

    /// <summary>
    /// A decided-false test at the edge sends the path the other way, and the other way can be a
    /// match that needs the continuation (row D4: 'a' + 'a', where <c>\Z</c> fails, the atomic group
    /// takes its empty branch and <c>(?&lt;!a)a</c> matches). Upstream answers None.
    /// </summary>
    // DIVERGES FROM UPSTREAM, deliberately.
    [Test]
    public void A_decided_false_test_lets_the_path_take_its_other_branch()
    {
        Match m = Partial("match", @"(?>a\Z|)(?<!a)a", "a");

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Length).Should().Be((0, 1));
    }

    /// <summary>
    /// A test some continuations pass and others fail passes, and the path goes on to a partial
    /// (rows P1, P2, P3 and A5, the zero-width attempts; E2 and the line tests). With the
    /// undetermined answer read as the held text's, the zero-width rows are None; with it read as
    /// decided false, the line rows are None too.
    /// </summary>
    // DIVERGES FROM UPSTREAM on the zero-width rows but the reversed search, where upstream agrees.
    [Test]
    [Arguments("search", @"\b", "", 0, 0)]
    [Arguments("match", @"\b", "", 0, 0)]
    [Arguments("search", @"(?r)\b$", "", 0, 0)]
    [Arguments("match", @"(?r)\b$", "", 0, 0)]
    [Arguments("match", @"(?r)[a\d]\M", "", 0, 0)]
    // The text to come may be a newline (row E2): 'a' + '\n' holds `$` and then matches `\n`. Read
    // as decided false, `$` would refuse it.
    [Arguments("match", "a$\n", "a", 0, 1)]
    [Arguments("match", "(?m)a$\n", "a", 0, 1)]
    // Under `(?w)` a final CR is a line separator's start that a lone LF to come keeps final, so
    // `$` before it is undetermined, not false: 'a\r' + '\n' is a complete match.
    [Arguments("match", "(?w)a$\r\n", "a\r", 0, 2)]
    // Under `(?w)` a line starts after a CR unless the text to come begins with LF.
    [Arguments("match", "(?wm)a\r^b", "a\r", 0, 2)]
    public void A_test_the_continuation_decides_passes_and_the_path_reports_a_partial(
        string operation,
        string pattern,
        string text,
        int start,
        int end
    )
    {
        Match m = Partial(operation, pattern, text);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Index + m.Length).Should().Be((start, end));
    }

    /// <summary>
    /// A search whose only completion starts inside the text to come reports the partial at the
    /// end of the text, the convention of design section 1, rule 3, whichever path the search
    /// takes: the attempt from inside the text to come (Matcher.MatchFromInsideTheTextToCome).
    /// </summary>
    /// <remarks>
    /// Found by review of build chunk 2 (2026-09-29) as a regression against S57d: once <c>\Z</c>
    /// at the edge is decided false, no path reaches S57d's note, and the prefilter answered
    /// failure where the slow path answers the edge partial. The brute-force judge answers P(0, 0)
    /// for the first two and P(1, 1) for the last (design row S3).
    /// </remarks>
    // DIVERGES FROM UPSTREAM on the third row: upstream answers (1, 1) too; on the first two it
    // answers None.
    [Test]
    [Arguments(@"(?a)\Z(\W?\M)(^ +?\m)?", "", 0)]
    [Arguments(@"(?u)\Z(?:(?:\Bax|aa\b)|(\M))", "", 0)]
    [Arguments("(?i)\\mİ", "b", 1)]
    public void A_search_whose_only_completion_starts_in_the_text_to_come_reports_the_edge(
        string pattern,
        string text,
        int edge
    )
    {
        Match m = Partial("search", pattern, text);

        m.PartialMatch.Should().BeTrue();
        (m.Index, m.Length).Should().Be((edge, 0));
    }

    /// <summary>
    /// D70: the same rule for a pattern with no first set. A call (or an alternation ending in
    /// <c>$</c>, or a bare <c>\Z</c>) gives the search no first set, and before D11 the search gave
    /// up at "Can we advance?" (main <c>Matcher.cs</c>:14417-14420) before considering a start
    /// inside the text to come. Every row completes only from such a start: 'b' + 'abb' matches the
    /// first two rows at 3, '' + 'b' the third at 0, 'b' + 'ab' the last two at 3.
    /// </summary>
    /// <remarks>
    /// Expected answers: the brute-force judge (2026-10-04). Main at 8de483d7 answers None to every
    /// row (probed), so each is red there.
    /// </remarks>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 answers None to every row.
    [Test]
    [Arguments(@"(?(DEFINE)(?P<g1>b))(?<=ab)(?1)", "b", 1)]
    [Arguments(@"(?(DEFINE)(?P<g1>b))(?<=ab)(?&g1)", "x", 1)]
    [Arguments(@"(?(DEFINE)(?P<g1>b))\b(?1)", "", 0)]
    [Arguments(@"(?<=ab)(?:b|$)", "b", 1)]
    [Arguments(@"(?<=ab)\Z", "b", 1)]
    public void A_search_with_no_first_set_still_tries_a_start_in_the_text_to_come(
        string pattern,
        string text,
        int edge
    )
    {
        Match m = Partial("search", pattern, text);

        m.Success.Should().BeTrue();
        (m.PartialMatch, m.Index, m.Length).Should().Be((true, edge, 0));
    }

    /// <summary>
    /// D73: a reversed <c>$</c> in the attempt from inside the text to come. With no held text after
    /// the edge, the final newline <c>$</c> can stand before is itself in the text to come, so a
    /// position there can pass <c>$</c>: '\n' on the left of '' completes each row at (0, 0) of
    /// '\n'. Before the fix that attempt took <c>$</c> to be false, as it is when held text follows
    /// the edge, and each row answered None. The first row is the grid's witness, from D11's 8.1
    /// port grid; the last is the shortest row of the D73 grid and the documented example.
    /// </summary>
    // Upstream 2026.9.10 answers None to every row.
    [Test]
    [Arguments(@"(?wr)(?:^(*SKIP)(?<=\b$^)|(\Z(*SKIP))(*FAIL))")]
    [Arguments(@"(?wr)^(?<=\b$^)")]
    [Arguments(@"(?wr)\b$^")]
    public void A_reversed_dollar_can_stand_before_a_final_newline_in_the_text_to_come(string pattern)
    {
        Match m = Partial("search", pattern, "");

        m.Success.Should().BeTrue();
        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 0));
    }

    /// <summary>
    /// The controls: where held text follows the edge, a final newline cannot lie in the text to
    /// come, and <c>\Z</c> needs the end itself, so the attempt from inside the text to come fails
    /// at them as before. No continuation completes these (the brute-force judge, 2026-10-08).
    /// </summary>
    [Test]
    [Arguments(@"(?wr)^(?<=\b$^)", "a")]
    [Arguments(@"(?wr)^(?<=\b\Z^)", "")]
    public void A_reversed_dollar_or_end_of_string_stays_false_where_it_cannot_hold(string pattern, string text)
    {
        Partial("search", pattern, text).Success.Should().BeFalse();
    }

    /// <summary>
    /// The price of the D73 fix, pinned as contract behaviour. Once a reversed <c>$</c> can pass in
    /// the attempt from inside the text to come, these patterns report a partial at (0, 0) over ''
    /// although no text completes them: in <c>(?wr)a^$</c> the <c>^</c> needs the start of the
    /// string, with the <c>a</c> before it; in <c>(?r)$(?&lt;=\n)\A</c> nothing lies before
    /// <c>\A</c> for the lookbehind; in <c>(?wr)a\B(?=$)</c> an <c>a</c> before the end or a final
    /// newline is always a word boundary. The partial contract allows this: a partial promises only
    /// that more text may complete the match (<see cref="Match.PartialMatch"/>,
    /// <c>PartialContractTests</c>, owner decision 2026-10-08), while a miss is never allowed. On
    /// the 20,040-row reversed-anchor grid (ledger entry 72) the fix turned 327 misses into the
    /// right answer and gave 911 such partials; these three rows are from it.
    /// </summary>
    // Upstream 2026.9.10 gives the same empty partial at the start for every row, measured on
    // 2026-10-09, and the port answered None before D73.
    [Test]
    [Arguments(@"(?wr)a^$")]
    [Arguments(@"(?r)$(?<=\n)\A")]
    [Arguments(@"(?wr)a\B(?=$)")]
    public void A_reversed_dollar_in_the_text_to_come_can_give_a_partial_no_text_completes(string pattern)
    {
        Match m = Partial("search", pattern, "");

        (m.Success, m.PartialMatch, m.Index, m.Length).Should().Be((true, true, 0, 0));
    }

    /// <summary>
    /// A pattern that no text can match reports no partial on any door, however its search ends.
    /// </summary>
    /// <remarks>
    /// Chunk 2's post-repair design step (2026-09-29, the owner's delegate). At 50abbe7 a search
    /// with no start left answered the partial at the edge, and both searches below answered
    /// P(0, 0). Now the edge partial comes only from an attempt that starts inside the text to
    /// come, and a pattern no text can match fails there as everywhere. Upstream 2026.9.10 answers
    /// None on every door, as the definition requires. Under <c>(?r)</c> the <c>(?!)</c> runs
    /// first and fails, so <c>(?r)a(?!)</c> consumes nothing and is no C2 phantom either.
    /// </remarks>
    [Test]
    [Arguments("search", "(*FAIL)", "")]
    [Arguments("match", "(*FAIL)", "")]
    [Arguments("fullmatch", "(*FAIL)", "")]
    [Arguments("search", "(?r)a(?!)", "b")]
    [Arguments("match", "(?r)a(?!)", "b")]
    [Arguments("fullmatch", "(?r)a(?!)", "b")]
    public void A_pattern_no_text_can_match_reports_no_partial(string operation, string pattern, string text)
    {
        Partial(operation, pattern, text).Success.Should().BeFalse();
    }

    /// <summary>
    /// A search anchored to the start of the string, or reversed to its end, finds nothing that
    /// begins inside the text to come: no position there is the start (or the end) of the string.
    /// </summary>
    /// <remarks>
    /// Found by review of fa3880d (2026-09-29): the attempt from inside the text to come collapses
    /// the window to the edge, so the anchored-start test ahead of the search (`TextPos` against
    /// `TextStart`, or `TextEnd` reversed) passed, and every row below answered the edge partial.
    /// Upstream 2026.9.10, the brute-force judge and 11ccae7 all answer None. The review's eighth
    /// row, <c>(?r)b+$</c> over ' ', is not here: it answers P(0, 0) at 11ccae7 and upstream too
    /// (measured 2026-09-29), so it predates D11 and is not this attempt's doing.
    /// </remarks>
    [Test]
    [Arguments(@"\Ax", ":", 0, 1)]
    [Arguments(@"(?:\A)x", ":", 0, 1)]
    [Arguments(@"^x", ":", 0, 1)]
    [Arguments(@"\Ab+", " ", 0, 1)]
    [Arguments(@"\Ax", ":b", 1, 1)]
    [Arguments(@"(?r)b+\Z", " ", 0, 1)]
    [Arguments(@"(?r)^b\Z", " ", 0, 1)]
    public void An_anchored_search_finds_nothing_that_begins_in_the_text_to_come(
        string pattern,
        string text,
        int start,
        int length
    )
    {
        new FuzzyRegex(pattern).Match(text, start, length, partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// A second-pass success whose attempt read nothing of the text to come stays a success, as
    /// before D11, so that the best-match refinement around it answers as it did.
    /// </summary>
    /// <remarks>
    /// Seed 4242 row 3834 of the oracle, 2026-09-29. Under <c>(?b)</c> the first pass refines and
    /// refuses the held text's match (3, 5), which upstream's own non-partial matcher refuses too.
    /// From 5f3d184 to fa3880d M3 turned that raw success into the partial (3, 6), which depends on
    /// nothing to come. Now only a success whose attempt read the text to come is a partial (the
    /// design's assert A1), and the answer is upstream's (6, 6) with group 1 at (3, 4), whose
    /// completions put <c>\K</c> at 6 ('bba\r\na' + '\n' matches at (6, 7)).
    /// </remarks>
    [Test]
    public void A_second_pass_success_that_read_nothing_to_come_is_refined_as_before()
    {
        Match m = new FuzzyRegex(@"(?b)(?:(?P<g1>[^a])[^\d]+?([\w\s])){e<=1}\K(?:[abz][^a-f]+){d<=1}").MatchAtStart(
            "bba\r\na",
            partial: true
        );

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 6, 0));
        (m.Groups[1].Index, m.Groups[1].Length).Should().Be((3, 1));
    }

    /// <summary>
    /// Where the held text decides a line test, the continuation changes nothing. Without
    /// <c>(?w)</c> only LF ends a line, so <c>^</c> after a CR is false whatever follows.
    /// </summary>
    [Test]
    public void A_line_test_the_held_text_decides_stays_decided()
    {
        Partial("match", "(?m)a\r^b", "a\r").Success.Should().BeFalse();
    }

    /// <summary>
    /// The first pass and a call without partial read the edge as before: the complete match wins,
    /// and the answers are upstream's.
    /// </summary>
    [Test]
    public void The_first_pass_reads_the_edge_as_the_end_of_the_text()
    {
        Match complete = Partial("match", @"a\Z", "a");

        complete.PartialMatch.Should().BeFalse();
        (complete.Index, complete.Length).Should().Be((0, 1));
        new FuzzyRegex(@"(?r)\Aa").MatchAtStart("a", partial: true).PartialMatch.Should().BeFalse();
        new FuzzyRegex(@"(?m)a$").MatchAtStart("a", partial: true).PartialMatch.Should().BeFalse();
        new FuzzyRegex(@"a\m").MatchAtStart("a").Success.Should().BeFalse();
        new FuzzyRegex(@"a\B").MatchAtStart("a").Success.Should().BeFalse();
    }

    /// <summary>
    /// The port grid's counters (VERIFICATION rule 11): an undetermined read pushes one touch mark,
    /// and a decided read that differs from the first pass is counted. Debug builds only.
    /// </summary>
    [Test]
    public void The_second_pass_counts_its_edge_reads_in_a_debug_build()
    {
        using MatchState undetermined = RunPartial(@"aa\B", "aa");
        using MatchState decided = RunPartial(@"a\Zb", "a");

#if DEBUG
        const long one = 1;
#else
        const long one = 0;
#endif
        (
            undetermined.PartialEventCount(PartialEvent.UndeterminedRead),
            undetermined.PartialEventCount(PartialEvent.TouchPushed),
            decided.PartialEventCount(PartialEvent.DecidedReadDiffering),
            decided.PartialEventCount(PartialEvent.TouchPushed)
        )
            .Should()
            .Be((one, one, one, 0L));
    }

    /// <summary>
    /// The six rows of the seed-7 oracle wave of 2026-09-29 that M1 moved away from upstream, kept
    /// as the wave generated them (ExpectedDivergences <c>partial-edge-read</c>, which judges each).
    /// </summary>
    /// <remarks>
    /// Rows 4841 and 5367 are real completions upstream refuses. Rows 5098, 5310, 6399 and 6589 are
    /// partials no text completes, the design's limit C2, which the partial contract allows
    /// (<c>PartialContractTests</c>; was known defect D19): the brute-force judge answers None to
    /// all four. Rows 6399 and 6589
    /// moved in chunk 2 and are back at upstream's and 11ccae7's answers since RC5 (build chunk 3):
    /// an insertion at the edge runs out of text, which presumes the rest.
    /// </remarks>
    // DIVERGES FROM UPSTREAM on rows 4841, 5367, 5098 and 5310, deliberately on two and as a
    // registered limit on two.
    [Test]
    public void The_rows_the_seed_7_wave_found()
    {
        const FuzzyRegexOptions v1 = FuzzyRegexOptions.Version1;
        const FuzzyRegexOptions fim =
            FuzzyRegexOptions.FullCase | FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.Multiline;

        // Row 4841: '' + 'B\n' matches.
        Match m4841 = new FuzzyRegex(
            @"\b(?:[^\p{L}]{0}(*PRUNE)\D|[^a])[^a]+\B",
            fim | FuzzyRegexOptions.Version0
        ).MatchAtStart("", partial: true);

        (m4841.PartialMatch, m4841.Index, m4841.Length).Should().Be((true, 0, 0));

        // Row 5098, a phantom of limit C2.
        Match m5098 = new FuzzyRegex(
            @"(?r)^(?P<g1>\p{Nd}*)*(?P<g2>[\ ]+)(?:(?(2)(?<=(?P>g2))[^\d]))*?\M",
            fim | v1
        ).Match("     ", partial: true);

        (m5098.PartialMatch, m5098.Index, m5098.Length).Should().Be((true, 0, 0));

        // Row 5310, a phantom of limit C2: the skin-tone modifier is no word character.
        Match m5310 = new FuzzyRegex(
            "\\b(?P<g1>[\U0001F3FB]{2,}?)\\1",
            FuzzyRegexOptions.FullCase | FuzzyRegexOptions.IgnoreCase | v1
        ).FullMatch("\U0001F3FB\r\n", 3, 0, partial: true);

        (m5310.PartialMatch, m5310.Index, m5310.Length).Should().Be((true, 3, 0));

        // Row 5367: the empty slice + 'AA' is a full match.
        Match m5367 = new FuzzyRegex(@"\b([A]*){1}\1", v1).FullMatch("\r\n", 0, 0, partial: true);

        (m5367.PartialMatch, m5367.Index, m5367.Length).Should().Be((true, 0, 0));

        // Row 6399, a phantom of limit C2: an insertion runs out of text on the left.
        Match m6399 = new FuzzyRegex(@"(?fi)(?r)\m(?:(?:b+?\A){e<=3}){e<=2}", FuzzyRegexOptions.Version0).FullMatch(
            "BB",
            partial: true
        );

        (m6399.PartialMatch, m6399.Index, m6399.Length).Should().Be((true, 0, 2));

        // Row 6589 searched: the attempt at 4 runs out of text through an insertion at the edge,
        // a phantom of limit C2 (D19; the judge answers None).
        Match m6589 = new FuzzyRegex(@"(?b)\B(?:\Z[a-f]\p{L}\Wa+){e<=2}", FuzzyRegexOptions.Version0).Match(
            "bb.aa",
            partial: true
        );

        (m6589.PartialMatch, m6589.Index, m6589.Length).Should().Be((true, 4, 1));
    }

    /// <summary>
    /// A fuzzy insertion at the edge needs the character after it, so it runs out of text like any
    /// consuming item, and the path is a partial (design RC5, rows Z1 and Z2).
    /// </summary>
    /// <remarks>
    /// Brought forward from build chunk 9, because chunk 2 made it a regression. Before chunk 2 the
    /// test after the last character was read against the held text and S57d's note escalated the
    /// failed path to a partial, which hid RC5. M1 now decides <c>\m</c> after a word character and
    /// <c>\M</c> after a non-word one without that note, and the insertion that would get past them
    /// failed instead of running out. The judge answers P(0, 1) to every row ('a' + ' ' completes
    /// the first, 'a' + 'b' with the 'b' inserted the second, and ' ' + 'a' the next three), as
    /// 11ccae7 did; upstream 2026.9.10 answers None. Since build chunk 9 deleted S57d's escalation,
    /// RC5 alone answers them.
    /// </remarks>
    // DIVERGES FROM UPSTREAM, as 11ccae7 did: upstream's insertion arm has the same slip.
    [Test]
    [Arguments("match", @"(?:a\m){i<=1}", "a")]
    [Arguments("fullmatch", @"(?:a\B){i<=1}", "a")]
    [Arguments("match", @"(?: ?\M){e<=1}", " ")]
    [Arguments("fullmatch", @"(?: \M){i<=1}", " ")]
    [Arguments("fullmatch", @"(?b)(?: ?\M){e<=1}", " ")]
    [Arguments("match", @"(?r)(?:\Ma){i<=1}", "a")]
    public void An_insertion_at_the_edge_runs_out_of_text(string operation, string pattern, string text)
    {
        Match m = Partial(operation, pattern, text);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    /// <summary>
    /// A fuzzy deletion of <c>\X</c>'s character leaves its grapheme boundary tested where it
    /// started, so over an empty text the boundary sits at the edge with no character on the known
    /// side: the start of the text forward, its end reversed. Every continuation is non-empty, so
    /// the boundary there is a break (UAX #29 GB1, GB2), whatever the text to come. The test read the
    /// character before the text instead: an assert in Debug, an index out of range in Release.
    /// </summary>
    /// <remarks>
    /// Expected answers: the brute-force judge and the port's own non-partial judge, which agree
    /// (2026-10-03). A search reports the start inside the text to come as P(0, 0) ('\n' + 'ab'
    /// matches the lookbehind at 2).
    /// </remarks>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 answers None to the two searches without the b.
    [Test]
    [Arguments("match", @"(?<=(?:\X\X){e<=1})", "None")]
    [Arguments("search", @"(?<=(?:\X\X){e<=1})", "P(0,0)")]
    [Arguments("fullmatch", @"(?<=(?:\X\X){e<=1})", "None")]
    [Arguments("match", @"(?r)(?=(?:\X\X){e<=1})", "None")]
    [Arguments("search", @"(?r)(?=(?:\X\X){e<=1})", "P(0,0)")]
    [Arguments("fullmatch", @"(?r)(?=(?:\X\X){e<=1})", "None")]
    [Arguments("search", @"(?<=(?:\X\X){e<=1})b", "P(0,0)")]
    public void A_grapheme_boundary_at_the_edge_of_an_empty_text_is_a_break(
        string operation,
        string pattern,
        string expected
    )
    {
        Match m = Partial(operation, pattern, "");
        string kind = m.PartialMatch ? "P" : "F";

        (m.Success ? $"{kind}({m.Index},{m.Index + m.Length})" : "None").Should().Be(expected);
    }

    /// <summary>
    /// D26: a fuzzy zero-width test that passes undetermined at the edge still offers the insertion
    /// in front of what follows, so the path through it is not the only one. Over 'a', the
    /// <c>$</c> after the <c>a</c> passes undetermined, and the lookbehind <c>(?&lt;=:)</c> then
    /// fails on the held 'a'; the insertion of ':' that the continuation 'a:' gives is the match
    /// ((0, 2) with one insertion, here and upstream, without partial; ':a' for the reversed row).
    /// The insertion in front of the failed lookbehind (<c>InsertBeforeAFailedLookaround</c>) needs
    /// the character after the text, so it runs out; fixed by RC5 (build chunk 3, 494b35b8), which
    /// passes the position after the insertion to the run-out check. Each row is red with RC5's
    /// argument put back. The rows come from the sizing of 2026-10-03.
    /// </summary>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 answers None to the matches and P(0, 0) to the search.
    [Test]
    [Arguments("match", @"(?:a$(?<=:)){i<=1}")]
    [Arguments("match", @"(?:a(?m:$)(?<=:)){i<=1}")]
    [Arguments("match", @"(?:a$(?<=:)){e<=1}")]
    [Arguments("search", @"(?r)(?:(?=:)(?m:^)a){i<=1}")]
    public void An_undetermined_fuzzy_test_at_the_edge_still_offers_the_insertion(string operation, string pattern)
    {
        Match m = Partial(operation, pattern, "a");

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 1));
    }

    /// <summary>
    /// The trailing insertion END_FUZZY tries after a section is an insertion too: at the edge it
    /// needs the character after the text, so it runs out of text rather than failing, both where
    /// the rest of the pattern needs it (a fullmatch's end, a lookahead's last letter) and where the
    /// section's own minimum does (D41).
    /// </summary>
    /// <remarks>
    /// Each row is judged by the port's own non-partial call over the continuations (the port keeps
    /// trailing insertions, ledger entries 12 and 51; upstream refuses them at a minimum, so its
    /// judge answers None to the minimum rows). The first row was P(0, 3) before build chunk 6 only
    /// because S57d escalated the lookahead's run-out; chunk 6 made that run-out go on at the body's
    /// end, and the refused insertion then failed the path (the review of chunk 8). 'abc' + 'd'
    /// fullmatches the first with one insertion; 'b' + 'x' the second; 'ab' + 'x' the third;
    /// 'abc' + 'cd' matches the fourth at (0, 3) with one insertion in the lookahead.
    /// </remarks>
    // DIVERGES FROM UPSTREAM on the minimum rows, as ledger entry 51 does.
    [Test]
    [Arguments("fullmatch", "(?:a(?=bcd)b.){e<=1}", "abc", 3)]
    [Arguments("fullmatch", "(?:b){1<=e<=1}", "b", 1)]
    [Arguments("fullmatch", "a(?:b){1<=e<=1}", "ab", 2)]
    [Arguments("fullmatch", "(?r)(?:b){1<=e<=1}", "b", 1)]
    [Arguments("match", "(?:a(?=(?:b(?:c){1<=e<=1}d){0<=e<=1})b.){0<=e<=1}", "abc", 3)]
    public void A_trailing_insertion_at_the_edge_runs_out_of_text(
        string operation,
        string pattern,
        string text,
        int length
    )
    {
        Match m = Partial(operation, pattern, text);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, length));
    }

    /// <summary>
    /// The controls: a section that permits no insertion still fails at the edge, and a call
    /// without partial still refuses the insertion.
    /// </summary>
    [Test]
    public void A_trailing_insertion_the_section_forbids_does_not_run_out()
    {
        Partial("fullmatch", "(?:a(?=bcd)b.){s<=1}", "abc").Success.Should().BeFalse();
        new FuzzyRegex("(?:b){1<=e<=1}").FullMatch("b").Success.Should().BeFalse();
    }

    /// <summary>
    /// A one-character lazy repeat that reaches the edge by backtracking can still take a character
    /// of the text to come, so the next try runs out there, as the main loop's arm and a greedy
    /// repeat already do (D11 fix round 3's grid). The backtracking arm kept an entry only while the
    /// count was below the text held, upstream's bound, so it never ran out. 'aac' matches the first
    /// row at (0, 3) and the bounded second; 'क्क' is one cluster (GB9c), so it fullmatches the
    /// third, whose `\X` must take a consonant to come; 'caa' matches the reversed row.
    /// </summary>
    /// <remarks>
    /// Each answer is the port's own non-partial call over the continuations of up to three
    /// characters (2026-10-04). The controls: a repeat at its maximum takes no character to come,
    /// and a call without partial has no text to come.
    /// </remarks>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 answers None to every row.
    [Test]
    [Arguments("match", @".*?(?<=c)", "aa")]
    [Arguments("match", @".{0,3}?(?<=c)", "aa")]
    [Arguments("fullmatch", @"(?r)\X", "्क")]
    [Arguments("match", @"(?r)(?=c).*?", "aa")]
    public void A_lazy_repeat_that_reaches_the_edge_by_backtracking_runs_out(
        string operation,
        string pattern,
        string text
    )
    {
        Match m = Partial(operation, pattern, text);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, text.Length));
    }

    /// <summary>The controls of the test above.</summary>
    [Test]
    public void A_lazy_repeat_at_its_maximum_or_without_partial_does_not_run_out()
    {
        Partial("match", @".{0,2}?(?<=c)", "aa").Success.Should().BeFalse();
        new FuzzyRegex(@".*?(?<=c)").MatchAtStart("aa").Success.Should().BeFalse();
    }

    /// <summary>
    /// A greedy repeat's body that runs out at the end of the text stays a run-out, where upstream
    /// turns it into a failure once the minimum is reached so that the tail's complete match wins
    /// (<c>upstream/src/_regex.c:12575</c>). In the second pass every complete match is the first
    /// pass's, already refused, and the tail's success has read the text to come, so under
    /// fullmatch it backtracks and the run-out it replaced was lost. 'a b' and '1,2' fullmatch the
    /// first two rows, ' a' + 'b' reversed the third (D11 fix round 3's grid; a D11 regression,
    /// main 27b9de33 answers P(0, 2) through S57d's escalation).
    /// </summary>
    /// <remarks>
    /// Judged by the port's own non-partial call over the continuations of up to three characters
    /// (2026-10-04).
    /// </remarks>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 answers None to every row.
    [Test]
    [Arguments(@"(?:.\b)+", "a ")]
    [Arguments(@"(?:.\b)+", "1,")]
    [Arguments(@"(?r)(?:\b.)+", " a")]
    public void A_greedy_repeat_keeps_its_body_run_out_at_the_end_of_the_text(string pattern, string text)
    {
        Match m = Partial("fullmatch", pattern, text);

        (m.PartialMatch, m.Index, m.Length).Should().Be((true, 0, 2));
    }

    /// <summary>
    /// The greedy repeat's kept run-out goes through the run-out rule: inside a lookaround body
    /// matching goes on after the lookaround, where <c>(*FAIL)</c> fails for every continuation and
    /// a fullmatch's end needs the text to end. The port's judge (its non-partial call over the
    /// continuations of up to two characters) answers None to both (2026-10-04); answering the
    /// partial at once gave P(0, 2). The last row is the witness for putting the repeat's count back
    /// before the run-out, where backtracking can re-enter the body through its other branch: without
    /// it the row answered P(0, 2) (122 rows of a 22,197-row targeted grid moved away).
    /// </summary>
    [Test]
    [Arguments("match", @"(?=(?:.\b)+)(*FAIL)", "a ")]
    [Arguments("fullmatch", @"(?=((?:a(?=.))*))", "aa")]
    [Arguments("match", @"(?!(?:.\b|.){1,3}b)", "ab")]
    public void A_greedy_repeat_body_run_out_in_a_lookaround_goes_on_after_it(
        string operation,
        string pattern,
        string text
    )
    {
        Partial(operation, pattern, text).Success.Should().BeFalse();
    }

    /// <summary>
    /// When the continuation after a trailing insertion's run-out in a lookahead fails, backtracking
    /// goes on into the section's own choices and does not run out at the same entry again: the
    /// entry comes back marked. Unmarked, the row loops until the timeout. No text completes it (the
    /// 'b' at 1 is never a 'c'), so the answer is None.
    /// </summary>
    [Test]
    public void A_failed_continuation_after_a_trailing_insertion_run_out_backtracks_into_the_section()
    {
        var regex = new FuzzyRegex("a(?=(?:b){1<=e<=1})c", FuzzyRegexOptions.None, TimeSpan.FromSeconds(5));

        regex.MatchAtStart("ab", partial: true).Success.Should().BeFalse();
    }

    /// <summary>
    /// A second-pass success is returned as the partial at once, from BasicMatch, whatever the
    /// search goes on to do after a success (design section 4.3, rows M5 and M6): POSIX keeps no
    /// match to go on comparing, and best match and enhanced match get it back unrefined, as a
    /// run-out partial already was.
    /// </summary>
    /// <remarks>
    /// The judge answers P(0, 1) to both ('a' + 'a'). Upstream answers None to both. With the early
    /// return removed, POSIX saves the success as a complete match (FoundMatch) and asserts A1 and
    /// A2 fire in Debug.
    /// </remarks>
    // DIVERGES FROM UPSTREAM on both rows, as 11ccae7 did through S57d's note.
    [Test]
    [Arguments(@"(?p)a\B")]
    [Arguments(@"(?b)(?:a\B){e<=1}")]
    [Arguments(@"(?e)(?:a\B){e<=1}")]
    public void A_second_pass_success_is_the_partial_whatever_the_search_does_next(string pattern)
    {
        using MatchState state = RunPartial(pattern, "a");

#if DEBUG
        const long one = 1;
#else
        const long one = 0;
#endif
        (state.FoundMatch, state.PartialEventCount(PartialEvent.SuccessTurnedPartial)).Should().Be((false, one));
        Partial("match", pattern, "a").PartialMatch.Should().BeTrue();
    }

    /// <summary>
    /// A scan after an empty complete match keeps its trailing partial. The scan goes on from the
    /// empty match with "must advance" set, which forbids a second empty match at that position
    /// only. The attempt from inside the text to come stands for starts past the edge, never that
    /// position, so the flag must not reach it: carried in, it refused the attempt's empty success
    /// at the edge and the scan ended with no partial (D64 design review, finding 1).
    /// </summary>
    /// <remarks>
    /// Each expected list is the brute-force judge's (2026-10-03): the complete matches of the held
    /// text, then a partial at the edge exactly when some continuation of up to four characters
    /// gives the scan a further match, a repeat of the last empty match excluded. For example
    /// 'abb' + 'aa' matches <c>(?&lt;=a)(?=\X)</c> at 4, and 'a' + 'a' matches <c>(?&lt;=a)</c> at 2.
    /// The first two rows held before only because the pattern's first set ran out at the edge;
    /// <c>^</c> is the control, where no continuation gives a further match.
    /// </remarks>
    // DIVERGES FROM UPSTREAM: regex 2026.9.10 has no trailing partial on rows 3-6 (it agrees on the
    // first two rows and the control).
    [Test]
    [Arguments(@"(?<=a)(?=a)", "aab", "F(1,1) P(3,3)")]
    [Arguments(@"(?<!b)(?=[ac])", "ab", "F(0,0) P(2,2)")]
    [Arguments(@"(?<=a)(?=\X)", "abb", "F(1,1) P(3,3)")]
    [Arguments(@"(?<=a)", "ab", "F(1,1) P(2,2)")]
    [Arguments(@"(?<=a)", "a", "F(1,1) P(1,1)")]
    [Arguments(@"\b", "a", "F(0,0) F(1,1) P(1,1)")]
    [Arguments(@"^", "ab", "F(0,0)")]
    public void A_scan_after_an_empty_match_keeps_its_trailing_partial(string pattern, string text, string expected)
    {
        IEnumerable<string> matches = new FuzzyRegex(pattern)
            .Matches(text, partial: true)
            .Select(static m => $"{(m.PartialMatch ? 'P' : 'F')}({m.Index},{m.Index + m.Length})");

        string.Join(' ', matches).Should().Be(expected);
    }

    private static MatchState RunPartial(string pattern, string text)
    {
        var regex = new FuzzyRegex(pattern);
        MatchState state = MatchState.Create(
            regex.PatternObject,
            text.AsMemory(),
            0,
            text.Length,
            overlapped: false,
            partial: true,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );
        _ = Matcher.DoMatch(state, search: false);
        return state;
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
