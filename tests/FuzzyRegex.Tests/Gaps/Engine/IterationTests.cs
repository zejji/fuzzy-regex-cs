using System.Diagnostics;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// The behaviour of the scanner and the splitter that the ported suite does not pin: the complexity
/// of a scan, the overlapped step across a surrogate pair, the count and limit conventions, and
/// where <see cref="Match.NextMatch"/> resumes.
/// </summary>
/// <remarks>
/// Every expected value here was measured against <c>regex</c> 2026.7.19 on 2026-09-01 and the
/// measurement is quoted beside it, because none of them is derivable from upstream's C by reading.
/// </remarks>
public sealed class IterationTests
{
    [Test]
    [NotInParallel]
    public void A_scan_over_a_long_subject_costs_time_proportional_to_its_length()
    {
        // The complexity guard for the whole iteration surface. Since the 2026-09-01 quadratic fix
        // a MatchState costs one vectorised pass over the subject to build, so a scan that builds
        // one per match is superlinear in the subject while every correctness test stays green -
        // which is exactly why this is a test and not a comment. DECISIONS 2026-09-01.
        //
        // The assertion is the RATIO between two subject sizes, not a wall-clock ceiling, because
        // the ratio is what "proportional to its length" claims and it does not depend on how fast
        // the machine is. Measured 2026-09-01, inside the full suite and in Debug, which is what
        // the ratchet runs: 468ms for the three scans at 480,000 code units and 970ms at 960,000,
        // a ratio of 2.07. A scan that builds a state per match costs 10.8x for the same doubling
        // (Release, 2,670ms at 480,000 then 28,718ms at 960,000, and 208,922ms at 1,920,000), so
        // the threshold of 5 sits about 2.4x above the linear answer and 2.2x below the quadratic
        // one, and neither margin moves with the machine.
        //
        // [NotInParallel] because it must be: without it the suite's other 5,700 tests run
        // alongside and the same three scans took 7.5s instead of 2.1s, which is enough contention
        // to swamp any threshold.
        //
        // The engine's own MatchTimeout cannot enforce this one, which is why it is timed at all: a
        // state carries its own start time, so a scan that built a state per match would reset the
        // budget on every match and never time out however long it ran.
        TimeSpan small = TimeThreeScans(160000);
        TimeSpan large = TimeThreeScans(320000);

        (large / small)
            .Should()
            .BeLessThan(
                5,
                "doubling the subject costs a linear scan about 2.2x and a state-per-match scan "
                    + "10.8x; {0} then {1}",
                small,
                large
            );
    }

    /// <summary>
    /// How long the three scanning entry points take over a subject of <paramref name="matches"/>
    /// matches, checking on the way that each found them all.
    /// </summary>
    /// <param name="matches">How many matches the subject should hold.</param>
    /// <returns>The elapsed time.</returns>
    private static TimeSpan TimeThreeScans(int matches)
    {
        string subject = string.Concat(Enumerable.Repeat("ab ", matches));
        var pattern = new FuzzyRegex(@"\w+", FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout);

        var elapsed = Stopwatch.StartNew();

        pattern.Count(subject).Should().Be(matches);
        pattern.Matches(subject).Count.Should().Be(matches);
        pattern.Split(subject).Should().HaveCount(matches + 1);

        return elapsed.Elapsed;
    }

    [Test]
    public void An_overlapped_scan_steps_one_codepoint_from_where_the_match_started()
    {
        // Upstream's `state->text_pos = state->match_pos + step` (upstream/src/_regex.c:22472) is a
        // step of one CODEPOINT, because upstream indexes a Python str. On a subject holding
        // surrogate pairs a one-code-unit step would restart the scan inside a pair.
        //
        // Measured: regex.finditer('..', '\U0001F600\U0001F601\U0001F602', overlapped=True) gives
        // codepoint spans (0, 2) and (1, 3), which are UTF-16 (0, 4) and (2, 6).
        var pattern = new FuzzyRegex("..");

        pattern
            .Matches("\U0001F600\U0001F601\U0001F602", overlapped: true)
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((0, 4), (2, 4));

        // And backwards: regex.finditer('(?r)..', ..., overlapped=True) gives (1, 3) then (0, 2).
        new FuzzyRegex("(?r)..")
            .Matches("\U0001F600\U0001F601\U0001F602", overlapped: true)
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((2, 4), (0, 4));
    }

    [Test]
    public void An_overlapped_scan_still_refuses_two_contiguous_zero_width_matches()
    {
        // The overlapped branch clears must_advance (:22473), so what stops a zero-width match
        // repeating at one position is the step itself rather than the flag. Measured:
        // regex.finditer('a*', 'aab', overlapped=True) gives (0, 2), (1, 2), (2, 2), (3, 3) - four
        // matches, one per start position, and the scan stops when the step leaves the slice.
        new FuzzyRegex("a*")
            .Matches("aab", overlapped: true)
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((0, 2), (1, 1), (2, 0), (3, 0));

        // Without overlapped the same pattern gives (0, 2), (2, 2), (3, 3): the empty match at 2 is
        // allowed because the match before it was not empty, and the one at 2 then forces the
        // advance to 3.
        new FuzzyRegex("a*")
            .Matches("aab")
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((0, 2), (2, 0), (3, 0));
    }

    [Test]
    public void The_scan_honours_the_beginning_and_the_length_without_widening_the_slice()
    {
        // pos moves slice_start, which is what a search may not start before - but text_start stays
        // at 0, so what is before it is still readable. \B is the cheapest witness available before
        // Phase 4 brings lookbehind: it needs the character before the position, so if pos moved
        // text_start too then position 1 would look like the start of the subject and \B would fail
        // there. Measured:
        //   regex.compile(r'\Bb').finditer('abab', 1) -> spans (1, 2) and (3, 4)
        //   regex.compile(r'\Bb').finditer('abab', 2) -> (3, 4) only
        //   regex.compile('^b').finditer('abab', 1)   -> nothing
        new FuzzyRegex(@"\Bb")
            .Matches("abab", beginning: 1)
            .Select(m => m.Index)
            .Should()
            .Equal(1, 3);
        new FuzzyRegex(@"\Bb").Matches("abab", beginning: 2).Select(m => m.Index).Should().Equal(3);
        new FuzzyRegex("^b").Matches("abab", beginning: 1).Should().BeEmpty();

        // endpos moves text_end as well, so a match may not run past it. Measured:
        // regex.compile('b').finditer('abab', 0, 2) -> (1, 2) only.
        new FuzzyRegex("b")
            .Matches("abab", beginning: 0, length: 2)
            .Select(m => m.Index)
            .Should()
            .Equal(1);
    }

    [Test]
    public void NextMatch_resumes_inside_the_slice_the_match_was_found_in()
    {
        // The reason Match carries the slice and not just its own end: a fresh state built from the
        // match end alone would put slice_start there, and a \B or a lookbehind at the resumption
        // point would then read a subject that starts there too. Measured:
        // [m.span() for m in regex.finditer(r'\Bb', 'abab')] is [(1, 2), (3, 4)], and the second
        // one only survives because position 3 can still see the 'a' at position 2.
        var pattern = new FuzzyRegex(@"\Bb");

        Match first = pattern.Match("abab");
        first.Index.Should().Be(1);
        first.NextMatch().Index.Should().Be(3, "the resumed search can still read the character before it");

        // The whole walk agrees with Matches, including the overlapped variant and the zero-width
        // advance, because both go through MatchState.AdvancePastMatch.
        foreach (
            (string p, string subject, bool overlapped) in new[]
            {
                ("a*", "aab", false),
                ("a*", "aab", true),
                ("..", "abcde", true),
                ("(?r)a*", "aab", false),
                ("(?r)..", "abcde", true),
                (@"\b", "a b", false),
            }
        )
        {
            MatchCollection collection = new FuzzyRegex(p).Matches(subject, overlapped: overlapped);
            collection.Should().NotBeEmpty("every case here matches at least once");

            // The first match is the same whether or not the scan overlaps - overlapped only
            // changes where the NEXT one starts - so the walk is seeded from the collection, which
            // is also what hands NextMatch the overlapped setting to carry on with.
            List<(int, int)> byNextMatch = [];
            for (Match m = collection[0]; m.Success; m = m.NextMatch())
            {
                byNextMatch.Add((m.Index, m.Length));
            }

            byNextMatch
                .Should()
                .Equal(
                    collection.Select(m => (m.Index, m.Length)),
                    $"NextMatch must walk the same sequence as Matches for {p} over {subject} (overlapped: {overlapped})"
                );
        }
    }

    [Test]
    public void Split_reads_its_limit_the_opposite_way_round_from_upstream_at_both_ends()
    {
        // Upstream's maxsplit is 0 for "no limit" and negative for "no splits at all"; this surface
        // is -1 for no limit and 0 for none, as S01 decided. Both ends have to be translated, and
        // S24 shipped a correct port that the oracle called RED for translating only one.
        // Measured: regex.split(',', 'a,b,c', maxsplit=0) is ['a', 'b', 'c'], maxsplit=-1 is
        // ['a,b,c'], maxsplit=-2 is ['a,b,c'], maxsplit=1 is ['a', 'b,c'].
        var pattern = new FuzzyRegex(",");

        pattern.Split("a,b,c").Should().Equal("a", "b", "c");
        pattern.Split("a,b,c", maxSplits: -1).Should().Equal("a", "b", "c");
        pattern.Split("a,b,c", maxSplits: -2).Should().Equal("a", "b", "c");
        pattern.Split("a,b,c", maxSplits: 0).Should().Equal("a,b,c");
        pattern.Split("a,b,c", maxSplits: 1).Should().Equal("a", "b,c");
        pattern.Split("a,b,c", maxSplits: 9).Should().Equal("a", "b", "c");
    }

    [Test]
    public void Split_always_ends_with_the_segment_after_the_last_match_even_when_it_is_empty()
    {
        // pattern_split (upstream/src/_regex.c:22330) appends the tail unconditionally, and a split
        // on a pattern that can match at the very end therefore ends with "". Measured:
        //   regex.split('x*', 'ax')   -> ['', 'a', '', '']
        //   regex.split('a|', 'ba')   -> ['', 'b', '', '']
        //   regex.split('(a)|', 'ba') -> ['', None, 'b', 'a', '', None, '']
        //   regex.split('', '')       -> ['', '']
        //   regex.split('a', '')      -> ['']
        FuzzyRegex.Split("ax", "x*").Should().Equal("", "a", "", "");
        FuzzyRegex.Split("ba", "a|").Should().Equal("", "b", "", "");
        FuzzyRegex.Split("ba", "(a)|").Should().Equal("", null, "b", "a", "", null, "");
        FuzzyRegex.Split("", "").Should().Equal("", "");
        FuzzyRegex.Split("", "a").Should().Equal("");
    }

    [Test]
    public void A_reverse_split_walks_backwards_and_is_not_reversed_afterwards()
    {
        // Unlike pattern_subx's join list, which is reversed whole once the last match is in,
        // pattern_split leaves its list in the order the reverse scan produced. Measured:
        //   regex.split('(?r)x', 'xaxbxc')             -> ['c', 'b', 'a', '']
        //   regex.split('(?r)x', 'xaxbxc', maxsplit=1) -> ['c', 'xaxb']
        //   regex.split('(?r)x*', 'axbc')              -> ['', 'c', 'b', '', 'a', '']
        //   regex.split('(?r)(x)|(y)', 'xaxbxc')       ->
        //     ['c', 'x', None, 'b', 'x', None, 'a', 'x', None, '']
        FuzzyRegex.Split("xaxbxc", "(?r)x").Should().Equal("c", "b", "a", "");
        new FuzzyRegex("(?r)x").Split("xaxbxc", maxSplits: 1).Should().Equal("c", "xaxb");
        FuzzyRegex.Split("axbc", "(?r)x*").Should().Equal("", "c", "b", "", "a", "");
        FuzzyRegex.Split("xaxbxc", "(?r)(x)|(y)").Should().Equal("c", "x", null, "b", "x", null, "a", "x", null, "");
    }

    [Test]
    public void Split_does_not_read_the_version_flag_in_this_release()
    {
        // The slice file expected V0 to refuse a zero-width match as a split point and V1 to accept
        // it. That difference does not exist in this release: `version_0` is written by state_init
        // (upstream/src/_regex.c:18482) and read nowhere in the whole of _regex.c, exactly as S24
        // found for pattern_subx. Pinned here so the next slice does not go hunting for it.
        //
        // Measured, four pairs, every one identical under V0 and V1:
        //   split(':*', ':a:b::c') == split('(?V1):*', ':a:b::c')
        //     -> ['', '', 'a', '', 'b', '', 'c', '']
        //   split('', 'abc')  == split('(?V1)', 'abc')  -> ['', 'a', 'b', 'c', '']
        //   split(r'\b', 'a b c') == split(r'(?V1)\b', 'a b c')
        //     -> ['', 'a', ' ', 'b', ' ', 'c', '']
        //   split('x*', 'axbc') == split('(?V1)x*', 'axbc') -> ['', 'a', '', 'b', 'c', '']
        foreach (
            (string v0, string v1, string subject) in new[]
            {
                (":*", "(?V1):*", ":a:b::c"),
                ("", "(?V1)", "abc"),
                (@"\b", @"(?V1)\b", "a b c"),
                ("x*", "(?V1)x*", "axbc"),
            }
        )
        {
            FuzzyRegex
                .Split(subject, v1)
                .Should()
                .Equal(FuzzyRegex.Split(subject, v0), $"V0 and V1 split '{subject}' alike on {v0}");
        }

        FuzzyRegex.Split(":a:b::c", ":*").Should().Equal("", "", "a", "", "b", "", "c", "");
    }

    [Test]
    public void A_findall_and_a_finditer_scan_produce_the_same_sequence()
    {
        // Upstream has two loops - pattern_findall (:22415) carries a
        // slice_start <= text_pos <= slice_end guard that scanner_search_or_match (:20874) does not
        // - and this port has one. That is only sound because the two agree, which is measured
        // rather than argued: on all nine pairs below regex.findall and
        // [m[0] for m in regex.finditer(...)] are element for element identical, overlapped and
        // not, including the reverse and zero-width cases where the guard is what stops the scan.
        foreach (
            (string p, string subject) in new[]
            {
                ("..", "abcde"),
                ("", "abc"),
                ("a*", "aab"),
                ("$", "ab"),
                (@"\b", "a b"),
                ("(?r)..", "abcde"),
                ("(?r)", "abc"),
                ("(?r)a*", "aab"),
                ("(?r)^", "ab"),
            }
        )
        {
            var pattern = new FuzzyRegex(p);

            foreach (bool overlapped in new[] { false, true })
            {
                pattern
                    .Count(subject, overlapped: overlapped)
                    .Should()
                    .Be(
                        pattern.Matches(subject, overlapped: overlapped).Count,
                        $"Count and Matches run the same scan for {p} over {subject} (overlapped: {overlapped})"
                    );
            }
        }

        // The four sequences the guard decides, quoted from the measurement:
        //   finditer('a*', 'aab', overlapped=True)     -> (0,2) (1,2) (2,2) (3,3)
        //   finditer('(?r)a*', 'aab', overlapped=True) -> (3,3) (0,2) (0,1) (0,0)
        new FuzzyRegex("a*")
            .Matches("aab", overlapped: true)
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((0, 2), (1, 1), (2, 0), (3, 0));
        new FuzzyRegex("(?r)a*")
            .Matches("aab", overlapped: true)
            .Select(m => (m.Index, m.Length))
            .Should()
            .Equal((3, 0), (0, 2), (0, 1), (0, 0));
    }

    [Test]
    public void An_empty_subject_still_yields_the_one_match_a_zero_width_pattern_finds_in_it()
    {
        // Measured: regex.finditer('', '') gives one match at (0, 0), overlapped or not, and
        // regex.compile('').findall('abc', 1, 2) gives two.
        new FuzzyRegex("")
            .Matches("")
            .Select(m => m.Index)
            .Should()
            .Equal(0);
        new FuzzyRegex("").Matches("", overlapped: true).Select(m => m.Index).Should().Equal(0);
        new FuzzyRegex("").Matches("abc", beginning: 1, length: 1).Select(m => m.Index).Should().Equal(1, 2);
    }

    [Test]
    public void A_scan_asked_for_a_partial_match_yields_it_last_and_then_stops()
    {
        // S31. The slice file said upstream's finditer takes no `partial` argument; it does
        // (_main.py:351, pattern_scanner's kwlist at :21089), and the C says why the partial comes
        // last: scanner_search_or_match yields the match for a PARTIAL status like any other
        // (:20898), and the NEXT turn sees that status and ends the walk (:20886).
        //
        // Measured 2026-09-12 against regex 2026.7.19, .scratch/probe-finditer-partial.py:
        //   finditer('a', 'a xa ya', partial=True) -> (0,1)F (3,4)F (6,7)F (7,7)T
        //   finditer('ab', 'ab a ab a', partial=True) -> (0,2)F (5,7)F (8,9)T
        //   finditer('abc', 'abc xab', partial=True) -> (0,3)F (5,7)T
        Spans("a", "a xa ya").Should().Equal((0, 1, false), (3, 4, false), (6, 7, false), (7, 7, true));
        Spans("ab", "ab a ab a").Should().Equal((0, 2, false), (5, 7, false), (8, 9, true));
        Spans("abc", "abc xab").Should().Equal((0, 3, false), (5, 7, true));

        // Without asking there is no partial, and the trailing prefix is simply not a match.
        new FuzzyRegex("abc")
            .Matches("abc xab")
            .Select(m => (m.Index, m.Index + m.Length))
            .Should()
            .Equal((0, 3));

        // A reverse scan reports its partial at the LEFT end, and still last.
        //   finditer('(?r)ab', 'b xab', partial=True) -> (3,5)F (0,1)T
        Spans("(?r)ab", "b xab").Should().Equal((3, 5, false), (0, 1, true));

        // Overlapped and partial together: the overlapped step still applies to the real matches.
        //   finditer('ab', 'abab a', overlapped=True, partial=True) -> (0,2)F (2,4)F (5,6)T
        new FuzzyRegex("ab")
            .Matches("abab a", overlapped: true, partial: true)
            .Select(m => (m.Index, m.Index + m.Length, m.PartialMatch))
            .Should()
            .Equal((0, 2, false), (2, 4, false), (5, 6, true));

        // The partial is bounded by the SLICE, not by the subject - do_match forces text_pos to
        // slice_end (:18179), so a narrowed scan reports a shorter partial and never reads past it.
        //   compile('ab').finditer('xxaby', 0, 3, partial=True) -> (2,3)T
        //   compile('ab').finditer('xxaby', 0, 4, partial=True) -> (2,4)F (4,4)T
        //   compile('ab').finditer('xxaby',       partial=True) -> (2,4)F (5,5)T
        new FuzzyRegex("ab")
            .Matches("xxaby", beginning: 0, length: 3, partial: true)
            .Select(m => (m.Index, m.Index + m.Length, m.PartialMatch))
            .Should()
            .Equal((2, 3, true));
        new FuzzyRegex("ab")
            .Matches("xxaby", beginning: 0, length: 4, partial: true)
            .Select(m => (m.Index, m.Index + m.Length, m.PartialMatch))
            .Should()
            .Equal((2, 4, false), (4, 4, true));
        Spans("ab", "xxaby").Should().Equal((2, 4, false), (5, 5, true));

        // Count is NOT finditer: upstream's findall refuses `partial` outright ("unused keyword
        // argument 'partial'", measured the same day), so the counting scan has no such argument
        // and counts only complete matches.
        new FuzzyRegex("abc")
            .Count("abc xab")
            .Should()
            .Be(1);

        static IEnumerable<(int Start, int End, bool Partial)> Spans(string pattern, string subject) =>
            new FuzzyRegex(pattern)
                .Matches(subject, partial: true)
                .Select(m => (m.Index, m.Index + m.Length, m.PartialMatch));
    }
}
