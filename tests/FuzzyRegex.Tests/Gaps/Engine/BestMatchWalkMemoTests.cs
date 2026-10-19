using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// D115: BESTMATCH holds each run after a match to fewer errors than that match and searches to the
/// end of the slice for it. When no better match exists that search fails only at the end of the
/// text, and a finditer paid it once per match, so the scan was quadratic. The port now remembers a
/// failed search for the rest of the scan (<c>MatchState.BestWalkNoMatchFrom</c>).
/// </summary>
/// <remarks>
/// Steps measured 2026-10-19 over 16,000 characters of prose (Debug, <c>WorkCounter</c>): main
/// fc878eec 8,944,794 and 6,370,408; with the memo 205,109 and 199,724. The flagless scans take
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
}
