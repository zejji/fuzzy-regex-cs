using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D115: BESTMATCH holds each run after a match to fewer errors than that match and searches to the
/// end of the slice for it. When no better match exists that search fails only at the end of the
/// text, and a finditer paid it once per match, so the scan was quadratic. The port now remembers a
/// failed search for the rest of the scan (<c>MatchState.BestWalkFailures</c>).
/// </summary>
/// <remarks>
/// Steps measured 2026-10-19 over 16,000 characters of prose (Debug, <c>WorkCounter</c>): main
/// fc878eec 8,944,794 and 6,370,408; with the memo 206,141 and 200,496. The flagless scans take
/// 127,030 and 129,706. Answers are the same with and without the memo.
/// </remarks>
public sealed class BestMatchWalkMemoTests
{
    private const string _para =
        "The quick brown fox jumps over the lazy dog while the reading group is ongoing.\r\n"
        + "Meanwhile, a second line of ordinary prose follows, with numbers like 12345 and words.\r\n"
        + "Things keep going: nothing special happens here, just text to search through.\r\n";

    // The first row is the one D115 was found on: since D109 the atomic body commits to a match
    // that spends an error, so every match costs one and none is better. The second has no exact
    // match to find at all, the same shape upstream mrab-regex is quadratic on.
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?b)(?:the\s(?>[a-z]+)\s){e<=1}", 259)]
    [Arguments(@"(?b)(?:thx\s[a-z]+\s){e<=1}", 194)]
    public void A_bestmatch_scan_with_no_better_match_reads_the_text_a_bounded_number_of_times(
        string pattern,
        int matches
    )
    {
        string text = string.Concat(Enumerable.Repeat(_para, (16_000 / _para.Length) + 1))[..16_000];
        var regex = new FuzzyRegex(pattern);
        var flagless = new FuzzyRegex(pattern.Replace("(?b)", "", StringComparison.Ordinal));

        string[] expected = [.. flagless.Matches(text).Select(static m => $"{m.Index},{m.Length},{m.FuzzyCounts}")];

        EngineWork.ShouldTakeAtMostSteps(
            () =>
                regex
                    .Matches(text)
                    .Select(static m => $"{m.Index},{m.Length},{m.FuzzyCounts}")
                    .Should()
                    .HaveCount(matches)
                    .And.Equal(expected),
            1_000_000,
            "a failed search for a better match is not repeated for every match"
        );
    }

    // D116: an exact lookbehind does not reach the search anchor, so it no longer withholds the
    // memo. Debug steps at 4,000 / 8,000 / 16,000 characters, main 627afcdf: 502,694 / 1,965,044 /
    // 7,749,390 and 338,662 / 1,285,284 / 5,001,407, quadrupling as the text doubles.
    [Test]
    [Category(EngineWork.Category)]
    [Arguments(@"(?b)(?<=\s)(?:the\s(?>[a-z]+)\s){e<=1}", 194)]
    [Arguments(@"(?b)(?<=\s)(?:thx\s[a-z]+\s){e<=1}", 130)]
    public void A_bestmatch_scan_with_an_exact_lookbehind_reads_the_text_a_bounded_number_of_times(
        string pattern,
        int matchesIn16000
    )
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, EngineWork.HangGuard);
        var flagless = new FuzzyRegex(pattern.Replace("(?b)", "", StringComparison.Ordinal));

        long Steps(int length)
        {
            string text = string.Concat(Enumerable.Repeat(_para, (length / _para.Length) + 1))[..length];
            string[] expected = [.. flagless.Matches(text).Select(static m => $"{m.Index},{m.Length},{m.FuzzyCounts}")];
            long before = WorkCounter.Steps;
            string[] actual = [.. regex.Matches(text).Select(static m => $"{m.Index},{m.Length},{m.FuzzyCounts}")];
            long steps = WorkCounter.Steps - before;
            actual.Should().Equal(expected);
            return steps;
        }

        long small = Steps(4_000);
        long middle = Steps(8_000);
        long large = Steps(16_000);
        flagless
            .Matches(string.Concat(Enumerable.Repeat(_para, (16_000 / _para.Length) + 1))[..16_000])
            .Should()
            .HaveCount(matchesIn16000);
        EngineWork.SkipUnlessCounting();

        ((double)middle / small).Should().BeLessThan(3, "the steps of a linear scan double when the text doubles");
        ((double)large / small).Should().BeLessThan(6, "the steps of a linear scan quadruple when the text quadruples");
    }

    // D115 review round 1 refuted the first memo: inside a search, the position it starts from is
    // the search anchor, where a fuzzy item may not open with an insertion. A later search starts
    // somewhere else, so its anchor bans an insertion the remembered search allowed, and where that
    // insertion decides a negative lookaround, an atomic group or a conditional, the ban widens
    // the whole pattern. Each row is main fc878eec's answer, which the first memo changed.
    // The second row is reversed and its lookahead runs against the scan; the third holds a
    // fuzzy lookbehind, which reads back across a later search's anchor; so does the fourth's
    // negative lookbehind (found by the D115 alternatives research), which a later search from 2
    // passes where a search from 0 refuses it.
    [Test]
    [Arguments(@"(?b)(?:(?!(?:ab){i<=1})x|(?:[xy]q){s<=1})", "yb.xab", "0,2,1,0,0|3,1,0,0,0")]
    [Arguments(
        @"(?br)(?:(?>x(?!x??a)(?:\B){1,2})){i<=1,d<=1}",
        "baay. x",
        "5,1,0,1,1|5,0,0,0,1|4,1,0,1,1|3,0,0,0,1|2,1,0,1,1|0,1,0,1,1"
    )]
    [Arguments(
        @"(?b)(?:xaab|(?<!x)(?<=(?:[xy]+\s?){i<=1,d<=1})){i<=1,d<=1}",
        "y aby",
        "1,0,0,0,0|2,0,0,0,0|2,1,0,1,0|3,0,0,1,0|3,1,0,1,1|5,0,0,0,0"
    )]
    [Arguments(@"(?b)(?:(?<!(?:abb){i<=1})x|(?:[aQ]Z|bbZ){s<=1})", "aQbbx", "0,2,1,0,0|4,1,0,0,0")]
    public void A_later_search_for_a_better_match_is_not_judged_by_an_earlier_one_whose_anchor_differs(
        string pattern,
        string text,
        string expected
    ) =>
        string.Join(
                '|',
                new FuzzyRegex(pattern)
                    .Matches(text)
                    .Select(static m =>
                        $"{m.Index},{m.Length},{m.FuzzyCounts.Substitutions},{m.FuzzyCounts.Insertions},{m.FuzzyCounts.Deletions}"
                    )
            )
            .Should()
            .Be(expected);

    // D116: what keeps the memo withheld for a lookaround against the scan. Rows 3 and 4 above
    // hold a fuzzy section in the body. Here the fourth and third rows' fuzzy bodies move into a
    // called group: written out by the call inliner (D101), kept as a call by recursion or by
    // upstreamCalls, and kept with upstream's call features. The last row's conditional test is
    // exact as written but compiled fuzzy by the oracle-only upstreamFuzzyConditionTest (D52).
    // Each expected answer is main 627afcdf's; with the memo forced on, every row changes
    // (the first six to the answers in the D116 design note; the last gains 8,3,0,1,1).
    [Test]
    [Arguments(
        @"(?b)(?(DEFINE)(?<g>(?:abb){i<=1}))(?:(?<!(?&g))x|(?:[aQ]Z|bbZ){s<=1})",
        "aQbbx",
        "0,2,1,0,0|4,1,0,0,0",
        false,
        false,
        false
    )]
    [Arguments(
        @"(?b)(?(DEFINE)(?<g>(?:[xy]+\s?){i<=1,d<=1}))(?:xaab|(?<!x)(?<=(?&g))){i<=1,d<=1}",
        "y aby",
        "1,0,0,0,0|2,0,0,0,0|2,1,0,1,0|3,0,0,1,0|3,1,0,1,1|5,0,0,0,0",
        false,
        false,
        false
    )]
    [Arguments(
        @"(?b)(?(DEFINE)(?<g>(?:abb){i<=1}|(?&g)z))(?:(?<!(?&g))x|(?:[aQ]Z|bbZ){s<=1})",
        "aQbbx",
        "0,2,1,0,0|4,1,0,0,0",
        false,
        false,
        false
    )]
    [Arguments(
        @"(?b)(?(DEFINE)(?<g>(?:[xy]+\s?){i<=1,d<=1}|(?&g)z))(?:xaab|(?<!x)(?<=(?&g))){i<=1,d<=1}",
        "y aby",
        "1,0,0,0,0|2,0,0,0,0|2,1,0,1,0|3,0,0,1,0|3,1,0,1,1|5,0,0,0,0",
        false,
        false,
        false
    )]
    [Arguments(
        @"(?b)(?(DEFINE)(?<g>(?:abb){i<=1}))(?:(?<!(?&g))x|(?:[aQ]Z|bbZ){s<=1})",
        "aQbbx",
        "0,2,1,0,0|4,1,0,0,0",
        true,
        false,
        false
    )]
    [Arguments(
        @"(?b)(?(DEFINE)(?<g>(?:abb){i<=1}))(?:(?<!(?&g))x|(?:[aQ]Z|bbZ){s<=1})",
        "aQbbx",
        "0,2,1,0,0|4,1,0,0,0",
        false,
        true,
        false
    )]
    [Arguments(@"(?b)(?:(?(?<=baba)b|b)x+?\s){i<=1,d<=1}", "bb  abaxbbxx", "1,2,0,0,1|5,3,0,1,1", false, false, true)]
    public void A_lookaround_against_the_scan_that_can_reach_the_anchor_keeps_the_memo_withheld(
        string pattern,
        string text,
        string expected,
        bool upstreamCalls,
        bool upstreamCallFeatures,
        bool upstreamFuzzyConditionTest
    ) =>
        string.Join(
                '|',
                FuzzyRegex
                    .WithDefaultVersion(
                        pattern,
                        FuzzyRegexOptions.None,
                        EngineWork.HangGuard,
                        null,
                        Fuzzy.Text.RegularExpressions.Parsing.PatternCompiler.DefaultVersion,
                        upstreamCallFeatures: upstreamCallFeatures,
                        upstreamFuzzyConditionTest: upstreamFuzzyConditionTest,
                        upstreamCalls: upstreamCalls
                    )
                    .Matches(text)
                    .Select(static m =>
                        $"{m.Index},{m.Length},{m.FuzzyCounts.Substitutions},{m.FuzzyCounts.Insertions},{m.FuzzyCounts.Deletions}"
                    )
            )
            .Should()
            .Be(expected);
}
