using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// C2: what one match costs once the engine has found it - the <see cref="Match"/> and its
/// groups, the list behind <see cref="MatchCollection"/>, and the pieces of a <c>Replace</c> or a
/// <c>Split</c> - and the answers those paths give, which the cheaper shapes must keep.
/// </summary>
/// <remarks>
/// <para>
/// The allocation tests use <see cref="AllocationTests.AllocatedBy"/>, which counts this thread's
/// allocations and repeats a run a collection paused. Where a call also pays a fixed cost (a
/// compiled template, an enumerator), the test measures two subject sizes and bounds the
/// difference, so only the part that grows with the matches is judged.
/// </para>
/// <para>
/// Every expected answer is upstream's, recorded with regex 2026.9.10 on CPython 3.14.7 on
/// 2026-10-13 and quoted beside the assertion in Python's <c>(start, end)</c> spans.
/// </para>
/// </remarks>
[SkipUnderStryker]
public sealed class PerMatchCostTests
{
    // --- Replace: the join list -------------------------------------------------------------

    [Test]
    public void A_warm_Replace_allocates_its_answer_and_nothing_else()
    {
        // The pieces are stretches of the subject and of the template, copied once into the
        // answer. Before C2 each piece was a substring of its own and the list of them grew, about
        // 300 bytes over this subject on top of the answer.
        const string subject = "call 0123 456 7890 after six";
        string answer = "";

        long allocated = AllocationTests.AllocatedBy(() =>
        {
            FuzzyRegex regex = new(@"\d+");
            _ = regex.Replace(subject, "#");
            return () => answer = regex.Replace(subject, "#");
        });

        // regex.sub(r'\d+', '#', 'call 0123 456 7890 after six') == 'call # # # after six'
        answer.Should().Be("call # # # after six");
        allocated.Should().BeLessThanOrEqualTo(StringBytes(answer.Length));
    }

    [Test]
    public void A_warm_Replace_that_replaces_nothing_hands_back_the_subject_and_allocates_nothing()
    {
        // Upstream answers with the subject itself: regex.sub(r'\d+', '#', s) is s, for a s with no
        // digits in it. The join list used to cost 88 bytes to say so.
        string subject = string.Concat("no digits ", "here");
        string answer = "";

        long allocated = AllocationTests.AllocatedBy(() =>
        {
            FuzzyRegex regex = new(@"\d+");
            _ = regex.Replace(subject, "#");
            return () => answer = regex.Replace(subject, "#");
        });

        answer.Should().BeSameAs(subject);
        allocated.Should().Be(0);
    }

    [Test]
    public void A_template_Replace_grows_by_its_answer_alone()
    {
        // Each pair "ab cd " becomes "cd ab ", six characters, so a thousand more pairs make the
        // answer 12,000 bytes longer and should cost nothing else. The template is compiled once a
        // call, which the difference cancels. Before C2: about 150 bytes a pair.
        (long shorter, string shorterAnswer) = Measure(1_000);
        (long longer, string longerAnswer) = Measure(2_000);

        shorterAnswer.Should().Be(string.Concat(Enumerable.Repeat("cd ab ", 1_000)));
        longerAnswer.Should().Be(string.Concat(Enumerable.Repeat("cd ab ", 2_000)));
        (longer - shorter).Should().BeLessThanOrEqualTo((1_000 * 6 * sizeof(char)) + 64);

        static (long Allocated, string Answer) Measure(int pairs)
        {
            string subject = string.Concat(Enumerable.Repeat("ab cd ", pairs));
            string answer = "";

            long allocated = AllocationTests.AllocatedBy(() =>
            {
                FuzzyRegex regex = new(@"(\w+) (\w+)");
                _ = regex.Replace(subject, @"\2 \1");
                return () => answer = regex.Replace(subject, @"\2 \1");
            });

            return (allocated, answer);
        }
    }

    [Test]
    // regex.sub(r'\d+', '#', 'call 0123 456 7890 after six')
    [Arguments(@"\d+", "#", "call 0123 456 7890 after six", "call # # # after six")]
    // regex.sub(r'(\w+) (\w+)', r'\2 \1', 'ab cd ef gh ij')
    [Arguments(@"(\w+) (\w+)", @"\2 \1", "ab cd ef gh ij", "cd ab gh ef ij")]
    // regex.sub(r'(a)|(b)', r'[\1\2]', 'abc'): a group that took no part joins as nothing.
    [Arguments(@"(a)|(b)", @"[\1\2]", "abc", "[a][b]c")]
    // regex.sub(r'\w+', r'<\g<0>>', 'ab cd')
    [Arguments(@"\w+", @"<\g<0>>", "ab cd", "<ab> <cd>")]
    // regex.sub(r'(?r)(\w)(\w)', r'\2\1', 'abcde'): pieces added back to front, joined in order.
    [Arguments(@"(?r)(\w)(\w)", @"\2\1", "abcde", "acbed")]
    // regex.sub(r'(?r)\d', '#', 'a1b22c')
    [Arguments(@"(?r)\d", "#", "a1b22c", "a#b##c")]
    // regex.sub(r'x*', '-', 'abc'): empty matches, empty pieces between them.
    [Arguments("x*", "-", "abc", "-a-b-c-")]
    // regex.sub(r'x*', '-', '')
    [Arguments("x*", "-", "", "-")]
    // regex.sub(r'\d', '', 'a1b2'): an empty literal adds nothing.
    [Arguments(@"\d", "", "a1b2", "ab")]
    public void Replace_gives_upstreams_answer(string pattern, string replacement, string subject, string expected) =>
        new FuzzyRegex(pattern).Replace(subject, replacement).Should().Be(expected);

    [Test]
    public void Replace_keeps_the_slice_the_count_and_the_evaluators_answers()
    {
        FuzzyRegex digit = new(@"\d");

        // regex.compile(r'\d').sub('#', 'a1b2c3', pos=2, endpos=4) == 'a1b#c3'
        digit.Replace("a1b2c3", "#", beginning: 2, length: 2).Should().Be("a1b#c3");

        // regex.subn(r'\d', 'X', 'a1b2c3', count=2) == ('aXbXc3', 2)
        digit.Replace("a1b2c3", "X", 2, out int replacements).Should().Be("aXbXc3");
        replacements.Should().Be(2);

        // regex.sub(r'\d', lambda m: None, 'a1b2') == 'ab'
        digit.Replace("a1b2", static _ => null!).Should().Be("ab");

        // regex.sub(r'\d', lambda m: m.group() * 2, 'a1b2') == 'a11b22'
        digit.Replace("a1b2", static m => m.Value + m.Value).Should().Be("a11b22");

        // regex.subf(r'(\w+) (?P<x>\w+)', '{x}-{1}', 'ab cd ef gh') == 'cd-ab gh-ef'
        new FuzzyRegex(@"(\w+) (?P<x>\w+)")
            .ReplaceFormat("ab cd ef gh", "{x}-{1}")
            .Should()
            .Be("cd-ab gh-ef");
    }

    [Test]
    public void A_Replace_stopped_part_way_leaves_the_next_call_its_full_answer()
    {
        // The join list's rented array goes back to the pool in a finally, whatever stops the call.
        FuzzyRegex digit = new(@"\d");
        string subject = string.Concat(Enumerable.Repeat("a1", 100));
        int calls = 0;

        Action throwing = () =>
            digit.Replace(subject, _ => ++calls < 50 ? "#" : throw new InvalidOperationException("stop part way"));
        throwing.Should().Throw<InvalidOperationException>();

        using CancellationTokenSource source = new();
        source.Cancel();
        Action cancelled = () => digit.Replace(subject, "#", cancellationToken: source.Token);
        cancelled.Should().Throw<OperationCanceledException>();

        digit.Replace(subject, "#").Should().Be(string.Concat(Enumerable.Repeat("a#", 100)));
    }

    // --- Split: shared one-character pieces --------------------------------------------------

    [Test]
    public void Split_hands_out_one_shared_string_per_Latin1_character()
    {
        // regex.split(r'\w+', 'ab cd')[1] is regex.split(r'\w+', 'x y')[1] -> True: CPython keeps
        // one string per Latin-1 character. Measured the same way: a U+00E9 piece is shared and a
        // U+0101 piece is not.
        FuzzyRegex words = new(@"\w+");
        FuzzyRegex letters = new("[a-d]+");

        words.Split("ab cd")[1].Should().BeSameAs(words.Split("x y")[1]);
        letters.Split("abécd")[1].Should().BeSameAs(letters.Split("cdéab")[1]);

        // A group's text goes out the same way.
        FuzzyRegex separator = new(@"(\W)");
        separator.Split("a,b")[1].Should().BeSameAs(separator.Split("c,d")[1]);
    }

    [Test]
    public void Split_gives_upstreams_answers()
    {
        // regex.split(r'\w+', 'ab cd') == ['', ' ', '']
        new FuzzyRegex(@"\w+")
            .Split("ab cd")
            .Should()
            .Equal("", " ", "");

        // regex.split(r'(\W)(x)?', 'a b,c') == ['a', ' ', None, 'b', ',', None, 'c']
        new FuzzyRegex(@"(\W)(x)?")
            .Split("a b,c")
            .Should()
            .Equal("a", " ", null, "b", ",", null, "c");

        // regex.split(r'[a-d]+', 'abécdāab') == ['', '\xe9', 'ā', '']
        new FuzzyRegex("[a-d]+")
            .Split("abécdāab")
            .Should()
            .Equal("", "é", "ā", "");

        // regex.split(',', 'a,b,c', maxsplit=1) == ['a', 'b,c']
        new FuzzyRegex(",")
            .Split("a,b,c", maxSplits: 1)
            .Should()
            .Equal("a", "b,c");

        // regex.split(r'(?r)\w+', 'ab cd,ef') == ['', ',', ' ', '']
        new FuzzyRegex(@"(?r)\w+")
            .Split("ab cd,ef")
            .Should()
            .Equal("", ",", " ", "");
    }

    // --- Match: the packed groups -------------------------------------------------------------

    [Test]
    public void A_match_keeps_its_groups_in_one_array()
    {
        // Three groups with one capture each: a header of three numbers per group and two per
        // span, fifteen ints in one array, where they were seven objects (240 bytes) before C2.
        // The same line through the same pattern without groups builds the same Match, so the
        // difference is what the groups cost.
        const string line = "2026-10-13 INFO request served in 12 ms";
        long withGroups = Allocated(@"^(?<date>\d{4}-\d\d-\d\d) (?<level>[A-Z]+) (?<msg>.*)$", line);
        long without = Allocated(@"^\d{4}-\d\d-\d\d [A-Z]+ .*$", line);

        (withGroups - without).Should().BeLessThanOrEqualTo(IntArrayBytes((3 * 3) + (3 * 2)));

        static long Allocated(string pattern, string subject)
        {
            bool matched = false;
            long allocated = AllocationTests.AllocatedBy(() =>
            {
                FuzzyRegex regex = new(pattern);
                _ = regex.Match(subject);
                return () => matched = regex.Match(subject).Success;
            });

            matched.Should().BeTrue();
            return allocated;
        }
    }

    [Test]
    public void Groups_captures_and_last_group_read_as_upstreams()
    {
        // m = regex.match(r'(?:(\w)\s?)+', 'a b c'): captures(1) == ['a', 'b', 'c'],
        // span(1) == (4, 5), lastindex == 1
        Match repeated = new FuzzyRegex(@"(?:(\w)\s?)+").MatchAtStart("a b c");
        repeated.Groups[1].Captures.Select(static c => c.Value).Should().Equal("a", "b", "c");
        Span(repeated.Groups[1]).Should().Be((4, 5));
        repeated.LastGroupNumber.Should().Be(1);

        // m = regex.match(r'(?<a>x)(?<b>y)?(z)', 'xz'): span('a') == (0, 1), group('b') is None,
        // captures('b') == [], span(3) == (1, 2), lastindex == 3, lastgroup == 'a'
        Match named = new FuzzyRegex("(?<a>x)(?<b>y)?(z)").MatchAtStart("xz");
        Span(named.Groups["a"]).Should().Be((0, 1));
        named.Groups["a"].Name.Should().Be("a");
        named.Groups["b"].Success.Should().BeFalse();
        named.Groups["b"].Captures.Should().BeEmpty();
        named.Groups["b"].Name.Should().Be("b");
        Span(named.Groups[3]).Should().Be((1, 2));
        named.Groups[3].Name.Should().Be("3");
        named.Groups[0].Name.Should().Be("0");
        named.LastGroupNumber.Should().Be(3);
        named.LastGroupName.Should().Be("a");

        // m = regex.match(r'(?&routine)(?(DEFINE)(?<routine>.))', 'a'): group('routine') is None,
        // span() == (0, 1). Upstream's captures('routine') is ['a']; this port's is empty, a pinned
        // divergence (D51, RegressionsDefineTests.Group_called_only_from_inside_define_records_no_capture).
        Match routine = new FuzzyRegex("(?&routine)(?(DEFINE)(?<routine>.))").MatchAtStart("a");
        Span(routine).Should().Be((0, 1));
        routine.Groups["routine"].Success.Should().BeFalse();
        routine.Groups["routine"].Captures.Should().BeEmpty();

        // m = regex.match(r'(?P<a>a(b))', 'ab'): lastindex == 1, lastgroup == 'a'
        Match nested = new FuzzyRegex("(?P<a>a(b))").MatchAtStart("ab");
        nested.LastGroupNumber.Should().Be(1);
        nested.LastGroupName.Should().Be("a");

        // A miss: every group absent, none with a capture.
        Match miss = new FuzzyRegex("(?<a>x)(y)").Match("nothing here");
        miss.Success.Should().BeFalse();
        miss.Groups.Count.Should().Be(3);
        miss.Groups["a"].Success.Should().BeFalse();
        miss.Groups["a"].Captures.Should().BeEmpty();
        miss.Groups["a"].Name.Should().Be("a");
        miss.Groups[2].Value.Should().Be("");
    }

    [Test]
    public void Fuzzy_partial_overlapped_and_reverse_matches_read_as_upstreams()
    {
        // m = regex.search(r'(?:kitten){e<=2}', 'sitting'): span() == (0, 6),
        // fuzzy_counts == (2, 0, 0), fuzzy_changes == ([0, 4], [], [])
        Match fuzzy = new FuzzyRegex("(?:kitten){e<=2}").Match("sitting");
        Span(fuzzy).Should().Be((0, 6));
        fuzzy.FuzzyCounts.Should().Be(new FuzzyCounts(2, 0, 0));
        fuzzy.FuzzyChanges.Substitutions.Should().Equal(0, 4);
        fuzzy.FuzzyChanges.Insertions.Should().BeEmpty();
        fuzzy.FuzzyChanges.Deletions.Should().BeEmpty();

        // m = regex.search(r'(?:(\w)(\w)){e<=1}', 'a-'): span() == (0, 2), span(1) == (0, 1),
        // span(2) == (1, 2), fuzzy_counts == (1, 0, 0), captures(1) == ['a'], captures(2) == ['-']
        Match fuzzyGroups = new FuzzyRegex(@"(?:(\w)(\w)){e<=1}").Match("a-");
        Span(fuzzyGroups).Should().Be((0, 2));
        Span(fuzzyGroups.Groups[1]).Should().Be((0, 1));
        Span(fuzzyGroups.Groups[2]).Should().Be((1, 2));
        fuzzyGroups.FuzzyCounts.Should().Be(new FuzzyCounts(1, 0, 0));
        fuzzyGroups.Groups[2].Captures.Select(static c => c.Value).Should().Equal("-");

        // m = regex.match(r'(a)(b)(c)', 'ab', partial=True): span() == (0, 2), partial,
        // span(1) == (0, 1), span(2) == (1, 2), span(3) == (-1, -1), captures(3) == []
        Match partial = new FuzzyRegex("(a)(b)(c)").MatchAtStart("ab", partial: true);
        Span(partial).Should().Be((0, 2));
        partial.PartialMatch.Should().BeTrue();
        Span(partial.Groups[1]).Should().Be((0, 1));
        Span(partial.Groups[2]).Should().Be((1, 2));
        partial.Groups[3].Success.Should().BeFalse();
        partial.Groups[3].Captures.Should().BeEmpty();

        // [(m.span(), m.span(1), m.captures(2)) for m in regex.finditer(r'(\w)(\w)', 'abcd',
        // overlapped=True)] == [((0, 2), (0, 1), ['b']), ((1, 3), (1, 2), ['c']), ((2, 4), (2, 3), ['d'])]
        new FuzzyRegex(@"(\w)(\w)")
            .Matches("abcd", overlapped: true)
            .Select(static m => (Span(m), Span(m.Groups[1]), m.Groups[2].Captures.Single().Value))
            .Should()
            .Equal(((0, 2), (0, 1), "b"), ((1, 3), (1, 2), "c"), ((2, 4), (2, 3), "d"));

        // m = regex.search(r'(?r)(\w)(\w)', 'abc d'): span() == (1, 3), span(1) == (1, 2),
        // span(2) == (2, 3)
        Match reverse = new FuzzyRegex(@"(?r)(\w)(\w)").Match("abc d");
        Span(reverse).Should().Be((1, 3));
        Span(reverse.Groups[1]).Should().Be((1, 2));
        Span(reverse.Groups[2]).Should().Be((2, 3));

        // [(m.span(), m.captures(1)) for m in regex.finditer(r'(?r)(\w)+', 'ab cd')]
        // == [((3, 5), ['d', 'c']), ((0, 2), ['b', 'a'])]
        new FuzzyRegex(@"(?r)(\w)+")
            .Matches("ab cd")
            .Select(static m => (Span(m), string.Join(",", m.Groups[1].Captures.Select(static c => c.Value))))
            .Should()
            .Equal(((3, 5), "d,c"), ((0, 2), "b,a"));
    }

    [Test]
    public void Templates_and_walks_read_the_packed_groups_as_upstream()
    {
        // m = regex.match(r'(\w+) (\w+)', 'ab cd'): expand(r'\2-\1') == 'cd-ab',
        // expandf('{2}-{1}-{0}') == 'cd-ab-ab cd'
        Match pair = new FuzzyRegex(@"(\w+) (\w+)").MatchAtStart("ab cd");
        pair.Result(@"\2-\1").Should().Be("cd-ab");
        pair.ResultFormat("{2}-{1}-{0}").Should().Be("cd-ab-ab cd");

        // regex.match(r'(\w)(x)?', 'ab').expand(r'[\2]') == '[]'
        new FuzzyRegex(@"(\w)(x)?")
            .MatchAtStart("ab")
            .Result(@"[\2]")
            .Should()
            .Be("[]");

        // [(m.span(), m.span(1), m.captures(1)) for m in regex.finditer(r'(?:(\d)|x)+', '12x3 x 45')]
        // == [((0, 4), (3, 4), ['1', '2', '3']), ((5, 6), (-1, -1), []), ((7, 9), (8, 9), ['4', '5'])]
        FuzzyRegex digits = new(@"(?:(\d)|x)+");
        MatchCollection all = digits.Matches("12x3 x 45");
        all.Select(static m =>
                (Span(m), m.Groups[1].Success, string.Concat(m.Groups[1].Captures.Select(static c => c.Value)))
            )
            .Should()
            .Equal(((0, 4), true, "123"), ((5, 6), false, ""), ((7, 9), true, "45"));
        Span(all[0].Groups[1]).Should().Be((3, 4));
        Span(all[2].Groups[1]).Should().Be((8, 9));

        // A NextMatch walk gives the same matches as Matches.
        List<(int, int)> walked = [];
        for (Match m = digits.Match("12x3 x 45"); m.Success; m = m.NextMatch())
        {
            walked.Add(Span(m.Groups[1].Success ? m.Groups[1] : m));
        }

        walked.Should().Equal((3, 4), (5, 6), (8, 9));
    }

    // --- Matches: the chunked list ------------------------------------------------------------

    [Test]
    public void Matches_stores_each_match_in_one_reference()
    {
        // Matches and EnumerateMatches build the same Match objects, so what Matches allocates
        // beyond the lazy walk is the list that holds them. A List<T> that doubles allocated about
        // 16 bytes a match for it; chunks of 1,024 allocate one reference, 8 bytes, and a little.
        long shorter = Storage(4_000);
        long longer = Storage(8_000);

        ((longer - shorter) / 4_000).Should().BeLessThanOrEqualTo(12);

        static long Storage(int words)
        {
            string subject = string.Concat(Enumerable.Repeat("ab ", words));
            int eager = 0;
            int lazy = 0;

            long allocatedEager = AllocationTests.AllocatedBy(() =>
            {
                FuzzyRegex regex = new(@"\w+");
                _ = regex.Matches(subject).Count;
                return () => eager = regex.Matches(subject).Count;
            });

            long allocatedLazy = AllocationTests.AllocatedBy(() =>
            {
                FuzzyRegex regex = new(@"\w+");
                _ = regex.EnumerateMatches(subject).Count();
                return () => lazy = regex.EnumerateMatches(subject).Count();
            });

            eager.Should().Be(words);
            lazy.Should().Be(words);
            return allocatedEager - allocatedLazy;
        }
    }

    [Test]
    public void A_collection_across_its_chunks_answers_every_position_in_order()
    {
        // 2,500 matches: the first chunk, a whole second chunk and part of a third.
        string subject = string.Concat(Enumerable.Repeat("ab ", 2_500));
        MatchCollection matches = new FuzzyRegex(@"\w+").Matches(subject);

        matches.Count.Should().Be(2_500);
        foreach (int i in (int[])[0, 1, 1_023, 1_024, 1_025, 2_047, 2_048, 2_499])
        {
            matches[i].Index.Should().Be(3 * i);
        }

        matches.Select(static m => m.Index).Should().Equal(Enumerable.Range(0, 2_500).Select(static i => 3 * i));
        new FuzzyRegex(@"\w+")
            .EnumerateMatches(subject)
            .Select(static m => m.Index)
            .Should()
            .Equal(matches.Select(static m => m.Index));

        Action pastTheEnd = () => _ = matches[2_500];
        Action beforeTheStart = () => _ = matches[-1];
        pastTheEnd.Should().Throw<ArgumentOutOfRangeException>();
        beforeTheStart.Should().Throw<ArgumentOutOfRangeException>();

        // Read from many threads at once, a finished collection gives every reader the same answer.
        int wrong = 0;
        Parallel.For(
            0,
            2_500,
            i =>
            {
                if (matches[i].Index != 3 * i || !string.Equals(matches[i].Value, "ab", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref wrong);
                }
            }
        );
        wrong.Should().Be(0);

        // An empty and a short scan.
        new FuzzyRegex(@"\d")
            .Matches(subject)
            .Should()
            .BeEmpty();
        new FuzzyRegex(@"\w+").Matches("ab cd").Select(static m => m.Value).Should().Equal("ab", "cd");
    }

    /// <summary>A capture as Python's <c>(start, end)</c> span.</summary>
    /// <param name="capture">The capture.</param>
    /// <returns>Its start and end.</returns>
    private static (int Start, int End) Span(Capture capture) => (capture.Index, capture.Index + capture.Length);

    /// <summary>What a string of that many characters occupies on a 64-bit runtime.</summary>
    /// <param name="length">The number of characters.</param>
    /// <returns>Header, method table, length and the characters with their terminator, rounded to 8.</returns>
    private static long StringBytes(int length) => (22 + (2L * length) + 7) & ~7L;

    /// <summary>What an <c>int[]</c> of that length occupies on a 64-bit runtime.</summary>
    /// <param name="length">The number of elements.</param>
    /// <returns>Header, method table, padded length and the elements, rounded to 8.</returns>
    private static long IntArrayBytes(int length) => (24 + (4L * length) + 7) & ~7L;
}
