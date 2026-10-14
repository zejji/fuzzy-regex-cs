using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// <see cref="MatchCollection"/> finds its matches as they are asked for, as the built-in
/// <c>System.Text.RegularExpressions.MatchCollection</c> does (owner decision, DECISIONS
/// 2026-10-14; design in <c>docs/plan/2026-10-14-lazy-matches.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// Laziness is shown the way <see cref="LazyEnumerationTests"/> shows it, without a stopwatch: a
/// subject with two easy matches and then a cliff the pattern can only fail on exponentially, and a
/// budget the two fit inside and the cliff does not. A collection that reads two matches and stops
/// answers; one that scans on construction, or past what was asked for, times out.
/// </para>
/// <para>
/// The answers themselves are pinned against
/// <see cref="FuzzyRegex.EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>,
/// the lazy walk the oracle already holds equal to upstream's <c>finditer</c>, read in every order
/// a caller can read a collection in.
/// </para>
/// </remarks>
public sealed class LazyMatchCollectionTests
{
    /// <summary>Exponential backtracking with no way to succeed; see <see cref="LazyEnumerationTests"/>.</summary>
    private const string _slowPattern = @"(a|a)*\1?(?:b|\b\B)";

    /// <summary>Two easy matches, at 0 and 2, then 26 characters the pattern can only fail on exponentially.</summary>
    private static readonly string _twoMatchesThenACliff = "abab" + new string('a', 26);

    /// <summary>A budget the two easy matches fit inside many times over and the cliff does not.</summary>
    private static readonly TimeSpan _budget = TimeSpan.FromMilliseconds(250);

    [Test]
    [Category(EngineWork.Category)]
    public void Reading_two_matches_by_index_does_not_scan_the_rest_of_the_subject()
    {
        FuzzyRegex pattern = new(_slowPattern);

        // 36 steps for the two matches (Debug, 2026-09-28, the EnumerateMatches twin of this test),
        // where the cliff is about 2^26.
        EngineWork.ShouldTakeAtMostSteps(
            () =>
            {
                MatchCollection matches = pattern.Matches(_twoMatchesThenACliff, timeout: _budget);
                matches[0].Index.Should().Be(0);
                matches[1].Index.Should().Be(2);
            },
            10_000,
            "two matches at the front of the subject are not the cliff"
        );
    }

    [Test]
    [Category(EngineWork.Category)]
    public void Enumerating_two_matches_and_stopping_does_not_scan_the_rest_of_the_subject()
    {
        FuzzyRegex pattern = new(_slowPattern);

        EngineWork.ShouldTakeAtMostSteps(
            () =>
                pattern
                    .Matches(_twoMatchesThenACliff, timeout: _budget)
                    .Take(2)
                    .Select(static m => m.Index)
                    .Should()
                    .Equal(0, 2),
            10_000,
            "two matches at the front of the subject are not the cliff"
        );
    }

    [Test]
    public void A_timeout_surfaces_from_Count_and_the_matches_before_it_stay_readable()
    {
        FuzzyRegex pattern = new(_slowPattern);

        MatchCollection matches = pattern.Matches(_twoMatchesThenACliff, timeout: _budget);

        Action count = () => _ = matches.Count;
        RegexMatchTimeoutException thrown = count.Should().Throw<RegexMatchTimeoutException>().Which;

        matches[0].Index.Should().Be(0);
        matches[1].Index.Should().Be(2);

        // Asked again, the collection does not search again: the walk that timed out is gone, and
        // its exception is what every read past the found matches gets.
        Action again = () => _ = matches.Count;
        Action third = () => _ = matches[2];
        again.Should().Throw<RegexMatchTimeoutException>().Which.Should().BeSameAs(thrown);
        third.Should().Throw<RegexMatchTimeoutException>().Which.Should().BeSameAs(thrown);
    }

    [Test]
    public void A_timeout_surfaces_from_the_indexer_that_needs_the_match_past_the_cliff()
    {
        FuzzyRegex pattern = new(_slowPattern);

        MatchCollection matches = pattern.Matches(_twoMatchesThenACliff, timeout: _budget);

        matches[1].Index.Should().Be(2);
        Action third = () => _ = matches[2];
        third.Should().Throw<RegexMatchTimeoutException>();
    }

    [Test]
    public void A_timeout_surfaces_from_enumeration_after_the_matches_before_it_were_yielded()
    {
        FuzzyRegex pattern = new(_slowPattern);
        List<int> seen = [];

        Action walk = () =>
        {
            foreach (Match match in pattern.Matches(_twoMatchesThenACliff, timeout: _budget))
            {
                seen.Add(match.Index);
            }
        };

        walk.Should().Throw<RegexMatchTimeoutException>();
        seen.Should().Equal(0, 2);
    }

    [Test]
    public void A_token_cancelled_after_the_call_surfaces_from_the_first_read_that_has_to_match()
    {
        FuzzyRegex pattern = new("a");
        using CancellationTokenSource cancellation = new();

        MatchCollection matches = pattern.Matches("aaaa", cancellationToken: cancellation.Token);
        matches[1].Index.Should().Be(1);

        cancellation.Cancel();

        matches[0].Index.Should().Be(0, "a match already found needs no matching");
        Action count = () => _ = matches.Count;
        count.Should().Throw<OperationCanceledException>().Which.CancellationToken.Should().Be(cancellation.Token);
    }

    /// <summary>
    /// Each row is a scan where a rule carried from one match to the next decides the answer, so a
    /// collection that rebuilt its engine state between matches, rather than keeping the walk's
    /// own, would be caught here.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="beginning">Where the slice starts.</param>
    /// <param name="length">The slice's length, or -1 for the rest.</param>
    /// <param name="overlapped">Whether matches may overlap.</param>
    /// <param name="partial">Whether the scan may end with a partial match.</param>
    [Test]
    [Arguments("a*", "baaab", 0, -1, false, false)] // zero-width matches: the must-advance rule
    [Arguments("(?r)a*", "baaab", 0, -1, false, false)] // the same rule walking backwards
    [Arguments("..", "\U0001F600\U0001F601\U0001F602", 0, -1, true, false)] // overlapped steps a codepoint
    [Arguments("[A-Z]*(*SKIP)_", "__BB__B", 0, -1, true, false)] // (*SKIP) moves the slice start for the rest of the scan
    [Arguments(@"\bab", "xab ab abab", 1, 8, false, false)] // a slice: \b reads before pos
    [Arguments("abc", "abcxabcab", 0, -1, false, true)] // a trailing partial is the last match
    [Arguments("(?b)(?:cat){e<=1}", "cot cat cut", 0, -1, false, false)] // BESTMATCH
    [Arguments("(?e)(?:cat){e<=1}", "cot cat cut", 0, -1, false, false)] // ENHANCEMATCH
    [Arguments("(?:cat){e<=1}", "xcaxcatcxt", 0, -1, true, false)] // fuzzy, overlapped
    [Arguments("(a)|(b)", "xaxbxxab", 0, -1, false, false)] // groups that sometimes take no part
    public void Every_way_of_reading_the_collection_gives_the_walks_answer(
        string pattern,
        string subject,
        int beginning,
        int length,
        bool overlapped,
        bool partial
    )
    {
        FuzzyRegex compiled = new(pattern);
        List<string> expected =
        [
            .. compiled.EnumerateMatches(subject, beginning, length, overlapped, partial).Select(Describe),
        ];
        expected.Should().NotBeEmpty("every row has matches to read");

        // Count first, then the indexer.
        MatchCollection counted = compiled.Matches(subject, beginning, length, overlapped, partial);
        counted.Count.Should().Be(expected.Count);
        Enumerable.Range(0, counted.Count).Select(i => Describe(counted[i])).Should().Equal(expected);

        // The indexer first, ascending until it runs out, then Count.
        MatchCollection indexed = compiled.Matches(subject, beginning, length, overlapped, partial);
        List<string> byIndex = [];
        bool more = true;
        while (more)
        {
            try
            {
                byIndex.Add(Describe(indexed[byIndex.Count]));
            }
            catch (ArgumentOutOfRangeException)
            {
                more = false;
            }
        }

        byIndex.Should().Equal(expected);
        indexed.Count.Should().Be(expected.Count);

        // The last match first, which has to find every match before it.
        MatchCollection lastFirst = compiled.Matches(subject, beginning, length, overlapped, partial);
        Describe(lastFirst[expected.Count - 1]).Should().Be(expected[^1]);
        lastFirst.Select(Describe).Should().Equal(expected);

        // Enumeration first, part way, then all of it, twice.
        MatchCollection enumerated = compiled.Matches(subject, beginning, length, overlapped, partial);
        enumerated.Take(1).Select(Describe).Should().Equal(expected.Take(1));
        enumerated.Select(Describe).Should().Equal(expected);
        enumerated.Select(Describe).Should().Equal(expected);
        enumerated.Count.Should().Be(expected.Count);
    }

    [Test]
    public void The_indexer_rejects_a_position_outside_the_matches_as_List_does()
    {
        MatchCollection matches = new FuzzyRegex(@"\w+").Matches("ab cd");

        Action negative = () => _ = matches[-1];
        Action pastTheEnd = () => _ = matches[2];

        negative.Should().Throw<ArgumentOutOfRangeException>();
        pastTheEnd.Should().Throw<ArgumentOutOfRangeException>();
        matches.Count.Should().Be(2, "looking past the end has found every match");
    }

    [Test]
    public void Two_enumerations_interleaved_on_one_collection_each_see_every_match()
    {
        MatchCollection matches = new FuzzyRegex(@"\w+").Matches("one two three four");

        using IEnumerator<Match> first = matches.GetEnumerator();
        using IEnumerator<Match> second = matches.GetEnumerator();
        List<string> a = [];
        List<string> b = [];

        while (true)
        {
            bool movedFirst = first.MoveNext();
            if (movedFirst)
            {
                a.Add(first.Current.Value);
            }

            // The second skips one each turn, so it runs ahead of the first and then waits for it.
            bool movedSecond = second.MoveNext();
            movedSecond = movedSecond && second.MoveNext();
            if (movedSecond)
            {
                b.Add(second.Current.Value);
            }

            if (!movedFirst && !movedSecond)
            {
                break;
            }
        }

        a.Should().Equal("one", "two", "three", "four");
        b.Should().Equal("two", "four");
    }

    [Test]
    public void Collections_read_part_way_on_one_pattern_do_not_disturb_each_other_or_its_other_calls()
    {
        // Each collection part way through holds an engine state of its own. Interleaving two of
        // them with ordinary calls on the same pattern is the case where a state handed back too
        // early, or twice, would show as a wrong answer somewhere else.
        FuzzyRegex pattern = new(@"\w+");
        MatchCollection left = pattern.Matches("a b c d");
        MatchCollection right = pattern.Matches("w x y z");

        left[0].Value.Should().Be("a");
        right[1].Value.Should().Be("x");
        pattern.Match("  q").Value.Should().Be("q");
        left[2].Value.Should().Be("c");
        pattern.Matches("m n").Select(static m => m.Value).Should().Equal("m", "n");
        right.Select(static m => m.Value).Should().Equal("w", "x", "y", "z");
        left.Select(static m => m.Value).Should().Equal("a", "b", "c", "d");
        pattern.Match("  r").Value.Should().Be("r");
    }

    [Test]
    public void Many_threads_reading_one_fresh_collection_each_get_every_answer_right()
    {
        // The README promises a MatchCollection can be read from several threads at once, each
        // with its own foreach - stronger than the built-in Regex, which asks callers to serialise
        // (learn.microsoft.com, "Thread safety in regular expressions"). With a lazy collection the
        // readers are also the ones driving the walk, so this starts them together on a collection
        // nobody has read yet: some ascending by index, some from the far end, some enumerating.
        const int words = 5_000;
        string subject = string.Concat(Enumerable.Repeat("ab ", words));
        FuzzyRegex pattern = new(@"\w+");

        for (int round = 0; round < 20; round++)
        {
            MatchCollection matches = pattern.Matches(subject);
            using Barrier start = new(8);
            int wrong = 0;

            Parallel.For(
                0,
                8,
                new ParallelOptions { MaxDegreeOfParallelism = 8 },
                reader =>
                {
                    start.SignalAndWait();
                    switch (reader % 4)
                    {
                        case 0:
                            for (int i = 0; i < words; i++)
                            {
                                Check(matches[i], i);
                            }

                            break;
                        case 1:
                            for (int i = words - 1; i >= 0; i -= 7)
                            {
                                Check(matches[i], i);
                            }

                            break;
                        case 2:
                            int index = 0;
                            foreach (Match match in matches)
                            {
                                Check(match, index++);
                            }

                            if (index != words)
                            {
                                Interlocked.Increment(ref wrong);
                            }

                            break;
                        default:
                            if (matches.Count != words)
                            {
                                Interlocked.Increment(ref wrong);
                            }

                            break;
                    }
                }
            );

            wrong.Should().Be(0, $"every reader should see match i at 3 * i (round {round})");

            void Check(Match match, int i)
            {
                if (match.Index != 3 * i || !string.Equals(match.Value, "ab", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref wrong);
                }
            }
        }

        // And the pattern's own state is still sound afterwards.
        pattern.Matches("x yy").Select(static m => m.Value).Should().Equal("x", "yy");
    }

    /// <summary>A match as its span, its partial flag and every group's captures.</summary>
    /// <param name="match">The match.</param>
    /// <returns>A one-line description that differs whenever anything a caller can read differs.</returns>
    private static string Describe(Match match) =>
        $"({match.Index},{match.Index + match.Length}){(match.PartialMatch ? " partial" : "")}"
        + $" {match.FuzzyCounts.Substitutions},{match.FuzzyCounts.Insertions},{match.FuzzyCounts.Deletions}"
        + string.Concat(
            match
                .Groups.Values.Skip(1)
                .Select(static g =>
                    " [" + string.Join(",", g.Captures.Select(static c => $"{c.Index}:{c.Length}")) + "]"
                )
        );
}
