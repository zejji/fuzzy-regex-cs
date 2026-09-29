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
    /// starts a word there, and the attempt at 0 fails.
    /// </summary>
    /// <remarks>
    /// Searched, it is the partial at the edge (4, 4), a PHANTOM OF LIMIT C2, known defect D19. No
    /// text completes the pattern (<c>\m$</c> holds nowhere; the judge answers None), but the
    /// search's second pass runs a <c>\p{Lu}+?</c> out of text, and after a run-out the rest is
    /// presumed to match. The day D19 decides the rest, this row moves to None, on purpose.
    /// </remarks>
    // DIVERGES FROM UPSTREAM on the search door, as limit C2 (D19).
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

        regex.MatchAtStart("\U00010400\U0001D518", partial: true).Success.Should().BeFalse();

        Match searched = regex.Match("\U00010400\U0001D518", partial: true);

        (searched.PartialMatch, searched.Index, searched.Length).Should().Be((true, 4, 0));
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
    /// Rows 4841 and 5367 are real completions upstream refuses; row 6399 is a partial upstream
    /// answers that no text completes, because <c>\A</c> at the edge is false for every
    /// continuation. Rows 5098, 5310 and 6589 are phantoms of the design's limit C2 (known defect
    /// D19), pinned so that the day D19 is fixed they move on purpose: the brute-force judge answers
    /// None to all three.
    /// </remarks>
    // DIVERGES FROM UPSTREAM on every row, deliberately on four and as a registered limit on two.
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

        // Row 6399: upstream's partial, which no continuation completes.
        new FuzzyRegex(@"(?fi)(?r)\m(?:(?:b+?\A){e<=3}){e<=2}", FuzzyRegexOptions.Version0)
            .FullMatch("BB", partial: true)
            .Success.Should()
            .BeFalse();
        // Row 6589 searched: the attempt from inside the text to come passes `\B` and `\Z` as
        // undetermined and its `[a-f]` runs out, a phantom of limit C2 (D19; the judge answers None).
        Match m6589 = new FuzzyRegex(@"(?b)\B(?:\Z[a-f]\p{L}\Wa+){e<=2}", FuzzyRegexOptions.Version0).Match(
            "bb.aa",
            partial: true
        );

        (m6589.PartialMatch, m6589.Index, m6589.Length).Should().Be((true, 5, 0));
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
