using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// C3: the bit-parallel scan in <see cref="FuzzyLiteralFilter"/>, which skips every start position
/// from which no stretch of the subject is within <c>k</c> edits of a fuzzy literal. It stands alone
/// where the Navarro pieces would be shorter than three characters, and checks a window after each
/// piece answer where they are not. Design: <c>docs/plan/2026-10-13-fuzzy-literal-bitparallel-design.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>These pins are permanent.</b> The scan only makes searches faster, so every answer below is
/// the answer without it. Each refusal row is built so that the scan, with that refusal removed,
/// would refuse a real match.
/// </para>
/// <para>
/// <b>Provenance.</b> Upstream's answers are <c>regex 2026.9.10</c>, recorded on 2026-10-13 by
/// <c>tools/probes/c3-fuzzy-literal-upstream-claims.py</c> or a one-line search quoted beside the row.
/// Upstream prints fuzzy counts as (substitutions, insertions, deletions) and positions in code
/// points; the rows here are UTF-16. The ledger entry 49 rows are this port's own answers, pinned
/// by <see cref="FullFoldFuzzyCharacterEditTests"/>: upstream answers None for all of them.
/// </para>
/// </remarks>
public sealed class FuzzyLiteralScanTests
{
    private static string Search(
        string pattern,
        string subject,
        int beginning = 0,
        int length = -1,
        bool partial = false
    )
    {
        Match match = new FuzzyRegex(pattern).Match(subject, beginning, length, partial);
        if (!match.Success)
        {
            return "None";
        }

        string mark = match.PartialMatch ? " partial" : "";
        return $"({match.Index},{match.Index + match.Length}) {match.FuzzyCounts.Substitutions},{match.FuzzyCounts.Insertions},{match.FuzzyCounts.Deletions}{mark}";
    }

    private static PatternObject Build(string source) =>
        PatternObject.Compile(
            PatternCompiler.Compile(
                source,
                0,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                PatternCompiler.DefaultVersion
            ),
            source
        );

    // ---------------------------------------------------------------------------------------
    // Which patterns get the scan.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_literal_whose_pieces_would_be_too_short_gets_the_scan_alone()
    {
        // m = 8, k = 3: four pieces of two characters, under MinPieceLength. Before C3, no filter.
        FuzzyLiteralFilter? filter = Build("(?:haystack){e<=3}").FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter.Pieces.Should().BeEmpty();
        filter.HasScan.Should().BeTrue();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_literal_with_pieces_gets_the_scan_behind_them()
    {
        FuzzyLiteralFilter? filter = Build("(?i)(?:amber lantern works){e<=2}").FuzzyLiteralFilter;

        filter.Should().NotBeNull();
        filter.Pieces.Should().HaveCount(3);
        filter.HasScan.Should().BeTrue();
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    [Arguments("(?fi)(?:ßx){s<=1}")] // ß is one unit of two values: one edit to the engine, two to a scan
    [Arguments("(?fi)(?:ﬁx){s<=1}")]
    [Arguments("(?fi)(?:ßßx){s<=1}")]
    [Arguments("(?fi)(?:ßx){d<=1}")]
    [Arguments("(?fi)(?:amber straße works){e<=1}")] // the same unit inside a literal that has pieces
    [Arguments("(?:ab){e<=2}")] // k >= m: every position is within k
    [Arguments("(?r)(?:haystack){e<=3}")] // reverse: no mirrored scan yet
    [Arguments("(?:haystack|needles){e<=3}")] // two literals
    [Arguments("(?:abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcde){e<=1}")] // m = 65
    public void A_literal_the_scan_cannot_read_as_the_engine_does_gets_no_scan(string pattern)
    {
        (Build(pattern).FuzzyLiteralFilter?.HasScan ?? false).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // Each refusal, with the match a scan without it would lose.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "none - this port's answers (ledger entry 49); upstream None")]
    [Arguments("(?fi)(?:ßx){s<=1}", "ax", "(0,2) 1,0,0")]
    [Arguments("(?fi)(?:ﬁx){s<=1}", "ax", "(0,2) 1,0,0")]
    [Arguments("(?fi)(?:ßßx){s<=1}", "ssax", "(0,4) 1,0,0")]
    [Arguments("(?fi)(?:ßx){d<=1}", "x", "(0,1) 0,0,1")]
    public void An_expanding_character_edited_as_one_unit_is_still_found(
        string pattern,
        string subject,
        string expected
    )
    {
        // Read value by value the literals are 'ssx', 'fix', 'ssssx' and 'ssx', each two edits
        // from its subject, against a budget of one.
        Search(pattern, subject).Should().Be(expected);
    }

    [Test]
    [Property("Upstream", "(1, 4) counts (0, 1, 0) partial=True")]
    public void A_partial_match_far_from_any_whole_occurrence_is_still_found()
    {
        // 'xab' is three edits from 'abcd'. Matcher.BasicMatch withholds the filter from partial
        // searches.
        Search("(?:abcd){e<=1}", "xxab", partial: true).Should().Be("(1,4) 0,1,0 partial");
    }

    [Test]
    [Property("Upstream", "(0, 4) counts (1, 0, 0)")]
    public void A_kelvin_sign_in_the_subject_switches_the_scan_off()
    {
        // The engine pairs U+212A with 'k'; an ASCII table cannot, so it would see two edits.
        Search("(?i)(?:kabc){e<=1}", "Kabx").Should().Be("(0,4) 1,0,0");
    }

    [Test]
    [Property("Upstream", "(0, 3) counts (1, 0, 0) in code points")]
    public void An_astral_character_is_one_substitution_not_two_code_units()
    {
        Search("(?:abc){e<=1}", "a\U0001F600c").Should().Be("(0,4) 1,0,0");
    }

    [Test]
    [Property("Upstream", "(0, 4) counts (4, 0, 0); (0, 3) counts (1, 0, 1)")]
    [Arguments("(?:abcd){2i+2d+1s<=4}", "wxyz", "(0,4) 4,0,0")] // k = 4 / the cheapest kind, not 4 / 2
    [Arguments("(?:abcd){i<=1,d<=1,s<=1}", "xbd", "(0,3) 1,0,1")] // k = the sum of the kinds
    public void The_budget_is_the_most_errors_the_constraints_allow(string pattern, string subject, string expected)
    {
        Search(pattern, subject).Should().Be(expected);
    }

    // ---------------------------------------------------------------------------------------
    // Windows and bounds.
    // ---------------------------------------------------------------------------------------

    [Test]
    [Property("Upstream", "(10, 19) counts (0, 1, 0)")]
    public void An_occurrence_ending_exactly_at_the_window_limit_is_found()
    {
        // Pieces 'abcd' and 'efgh'. 'efgh' at 5 makes the first piece answer 0, which is attempted
        // unscanned and fails. The second query, from 1, is scanned: the window ends at
        // L = 1 + 2(m + k) = 19, where the only occurrence ends. A window that did not include L
        // would move on to L - m - k + 1 = 11 and lose the match at 10.
        Search("(?:abcdefgh){e<=1}", "zzzzzefghzabcdxefghzz").Should().Be("(10,19) 0,1,0");
    }

    [Test]
    [Property(
        "Upstream",
        "(3, 7) counts (1, 0, 0); search('xxabcd', 0, 5) (2, 5) counts (0, 0, 1); search('xxabc', 0, 4) None"
    )]
    [Arguments("zzzazcd", 0, -1, "(3,7) 1,0,0")] // the only occurrence ends at the slice end
    [Arguments("xxabcd", 0, 5, "(2,5) 0,0,1")] // the slice cuts the exact match short
    [Arguments("xxabc", 0, 4, "None")]
    public void The_slice_end_is_an_end_the_scan_checks_and_the_last(
        string subject,
        int beginning,
        int length,
        string expected
    )
    {
        Search("(?:abcd){e<=1}", subject, beginning, length).Should().Be(expected);
    }

    [Test]
    [Property("Upstream", "(1, 4) counts (0, 0, 0)")]
    public void No_insertion_at_the_search_anchor_only_costs_an_attempt()
    {
        // The earliest admissible start is 0 ('xabc' is one insertion away), but upstream permits
        // no insertion at the search anchor (_regex.c:10448-10450). The engine fails at 0 and the
        // next query finds 1.
        Search("(?:abc){e<=1}", "xabc").Should().Be("(1,4) 0,0,0");
    }

    [Test]
    [Property(
        "Upstream",
        "(54, 63) (0, 2, 1); (?e) and (?b) (56, 63) (0, 0, 1); (0, 4) (2, 0, 0); (?b) (3, 7) (0, 0, 0)"
    )]
    [Arguments(
        "(?:haystack){e<=3}",
        "the quick brown fox jumps over the lazy dog and finds a haystakc.",
        "(54,63) 0,2,1"
    )]
    [Arguments(
        "(?e)(?:haystack){e<=3}",
        "the quick brown fox jumps over the lazy dog and finds a haystakc.",
        "(56,63) 0,0,1"
    )]
    [Arguments(
        "(?b)(?:haystack){e<=3}",
        "the quick brown fox jumps over the lazy dog and finds a haystakc.",
        "(56,63) 0,0,1"
    )]
    [Arguments("(?:abcd){e<=2}", "abxabcd", "(0,4) 2,0,0")]
    [Arguments("(?b)(?:abcd){e<=2}", "abxabcd", "(3,7) 0,0,0")]
    public void Leftmost_first_bestmatch_and_enhancematch_answer_as_upstream_does(
        string pattern,
        string subject,
        string expected
    )
    {
        Search(pattern, subject).Should().Be(expected);
    }

    [Test]
    [Property(
        "Upstream",
        "finditer: (0, 4) (0, 0, 0), (5, 9) (1, 0, 0), (14, 19) (0, 1, 0); (1, 9) (1, 1, 1), (10, 21) (0, 3, 0), (23, 34) (0, 3, 0)"
    )]
    [Arguments("(?:abcd){e<=1}", "abcd abxd zzzz abcd", "(0,4) (5,9) (14,19)")]
    [Arguments("(?:haystack){e<=3}", "a haystakc, hay stack and haystack", "(1,9) (10,21) (23,34)")]
    public void Every_match_of_a_walk_is_found(string pattern, string subject, string expected)
    {
        string.Join(
                ' ',
                new FuzzyRegex(pattern).Matches(subject).Select(static m => $"({m.Index},{m.Index + m.Length})")
            )
            .Should()
            .Be(expected);
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void The_scan_answers_the_earliest_admissible_start_exactly()
    {
        // The scan's contract against a plain dynamic programme: the leftmost start at or after the
        // query from which some stretch ending by the slice end is within k edits. Both modes,
        // random literals, subjects, slices and queries, each with a fresh memory and a reused one.
#pragma warning disable CA5394, S2245 // A seeded sequence, so a failure replays; nothing here is a secret.
        var random = new Random(20261013);
        int checkedRows = 0;
        string[] patterns = ["(?:abcab){e<=2}", "(?:aabba){e<=1}", "(?:abcabcab){e<=2}", "(?:abcabcabcabc){e<=1}"];
        foreach (string pattern in patterns)
        {
            FuzzyLiteralFilter filter = Build(pattern).FuzzyLiteralFilter!;
            filter.HasScan.Should().BeTrue(pattern);
            string literal = pattern[3..pattern.IndexOf(')', StringComparison.Ordinal)];
            int k = filter.MaxErrors;
            for (int round = 0; round < 200; round++)
            {
                string text = string.Concat(Enumerable.Range(0, random.Next(0, 40)).Select(_ => "abc"[random.Next(3)]));
                FuzzyLiteralFilter.ScanMemory memory = Armed(filter);
                for (int step = 0; step < 20; step++)
                {
                    int sliceEnd = random.Next(text.Length + 1);
                    int at = random.Next(sliceEnd + 1);
                    int expected = AdmissibleStart(text, literal, k, at, sliceEnd);
                    filter
                        .NextStart(text, at, sliceEnd, Armed(filter))
                        .Should()
                        .Be(expected, $"{pattern} '{text}' at {at} end {sliceEnd}");
                    filter
                        .NextStart(text, at, sliceEnd, memory)
                        .Should()
                        .Be(expected, "20 queries never reach a pause");
                    checkedRows++;
                }
            }
        }

#pragma warning restore CA5394, S2245
        checkedRows.Should().Be(4 * 200 * 20);
    }

    // Behind the pieces the scan sits out a memory's first query (FuzzyLiteralFilter.NextStart).
    private static FuzzyLiteralFilter.ScanMemory Armed(FuzzyLiteralFilter filter)
    {
        FuzzyLiteralFilter.ScanMemory memory = filter.NewScanMemory();
        _ = filter.NextStart("", 0, 0, memory);
        return memory;
    }

    private static int AdmissibleStart(string text, string literal, int k, int from, int sliceEnd)
    {
        for (int s = from; s <= sliceEnd; s++)
        {
            // One row of the edit-distance table per text character, from start s.
            int m = literal.Length;
            int[] row = [.. Enumerable.Range(0, m + 1)];
            if (row[m] <= k)
            {
                return s;
            }

            for (int e = s; e < sliceEnd; e++)
            {
                int[] next = new int[m + 1];
                next[0] = row[0] + 1;
                for (int j = 1; j <= m; j++)
                {
                    next[j] = Math.Min(
                        Math.Min(row[j] + 1, next[j - 1] + 1),
                        row[j - 1] + (text[e] == literal[j - 1] ? 0 : 1)
                    );
                }

                row = next;
                if (row[m] <= k)
                {
                    return s;
                }
            }
        }

        return FuzzyLiteralFilter.NoMatch;
    }

    // ---------------------------------------------------------------------------------------
    // The work it saves, and the work it may cost. Counted, not timed (D13).
    // ---------------------------------------------------------------------------------------

    [Test]
    [Category(EngineWork.Category)]
    [Property("Upstream", "None for both")]
    // Stand-alone: without the scan, one attempt at each of 10,001 positions.
    [Arguments("(?:haystack){e<=3}", "the quick brown fox ")]
    // Behind the pieces: 'nee' at every fourth position, so without the scan about m + k + 1
    // attempts per hit; no stretch is within one edit of 'needle'.
    [Arguments("(?:needle){e<=1}", "nee ")]
    public void A_subject_with_no_occurrence_costs_no_attempts(string pattern, string unit)
    {
        string subject = string.Concat(Enumerable.Repeat(unit, 10_000 / unit.Length));
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        EngineWork.ShouldTakeAtMostSteps(
            () => regex.IsMatch(subject).Should().BeFalse(),
            200,
            "the scan refuses the subject before any attempt"
        );
    }

    [Test]
    [Category(EngineWork.Category)]
    [Property("Upstream", "len(findall) == 2500")]
    public void A_scan_that_saves_nothing_pauses_itself()
    {
        // Every position is an exact match, so every query answers its own position and skips
        // nothing. Armed, each of the 2,500 queries reads about 8 characters, 20,000 in all, on top
        // of one ASCII check of the 10,000-character subject. Paused for 256 queries after every
        // useless batch of 40, it reads about a seventh of that.
        string subject = new('a', 10_000);
        var regex = new FuzzyRegex("(?:aaaa){e<=1}", FuzzyRegexOptions.None, EngineWork.HangGuard);
        EngineWork.ShouldSearchAtMostCharacters(
            () => regex.Matches(subject).Count.Should().Be(2_500),
            2L * subject.Length,
            "a scan that skips nothing sits out most queries"
        );
    }

    [Test]
    [Property("Upstream", "none - gap test")]
    public void A_walk_whose_scan_reads_a_long_tail_is_still_cancellable()
    {
        // What this pins is the guarantee RequiredStringPrefilterTests'
        // A_scan_whose_prefilter_sweeps_a_long_tail_is_still_cancellable pins for the string sweep:
        // a token cancelled between two steps is answered. A token that expires DURING the scan is
        // what the poll at each ScanChunk boundary is for; a test that fires one is a race with the
        // machine, for the reason that test gives (every attempt passes the opening check first).
        using var cancellation = new CancellationTokenSource();
        var pattern = new FuzzyRegex("(?:haystack){e<=3}");
        string subject = "haystack" + new string('x', 200_000);

        using IEnumerator<Match> walk = pattern
            .EnumerateMatches(subject, cancellationToken: cancellation.Token)
            .GetEnumerator();

        walk.MoveNext().Should().BeTrue();
        walk.Current.Index.Should().Be(0);

        cancellation.Cancel();
        Action act = () => walk.MoveNext();

        act.Should().Throw<OperationCanceledException>();
    }
}
