using System.Diagnostics;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// <see cref="FuzzyRegex.EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
/// and <see cref="FuzzyRegex.EnumerateSplits(string, int, TimeSpan?, CancellationToken)"/>, the
/// lazy twins S53b added beside <c>Matches</c> and <c>Split</c>.
/// </summary>
/// <remarks>
/// <para>
/// The contract is "the same answer, found as it is asked for", so most of the evidence is
/// sequence equality against the eager twin - here on the edges, and in
/// <c>FuzzyRegex.OracleTests</c> over every <c>finditer</c> and <c>split</c> row of the default
/// wave, which is where a disagreement would actually show up.
/// </para>
/// <para>
/// What is left for this file is the part equality cannot see: that the walk really stops when the
/// caller does. That is checked the only way that is not a stopwatch ratio - by giving both calls
/// a time budget that two matches fit inside and a million do not, and requiring the eager one to
/// fail and the lazy one to answer.
/// </para>
/// </remarks>
public sealed class LazyEnumerationTests
{
    /// <summary>
    /// The pathological pattern <c>TimeoutAndCancellationTests</c> already uses: exponential
    /// backtracking with no way to succeed. Borrowed rather than invented, because it is the one
    /// shape in this repo already proven to make the timeout fire.
    /// </summary>
    private const string _slowPattern = @"(a|a)*(?:b|\b\B)";

    /// <summary>
    /// Two easy matches at the front, then 26 characters the pattern can only fail on
    /// exponentially. A walk that stops after the two never reaches the cliff; a walk that does
    /// not, does.
    /// </summary>
    private static readonly string _twoMatchesThenACliff = "abab" + new string('a', 26);

    [Test]
    public void EnumerateMatches_yields_what_Matches_returns()
    {
        FuzzyRegex pattern = new("(a)|(b)");
        const string subject = "xaxbxxab";

        pattern
            .EnumerateMatches(subject)
            .Select(static m => (m.Index, m.Length, m.Value))
            .Should()
            .Equal(pattern.Matches(subject).Select(static m => (m.Index, m.Length, m.Value)));
    }

    [Test]
    [Arguments("a*", "bab")] // zero-width matches, where the must-advance rule decides the answer
    [Arguments("(?r)a", "xaxax")] // reversed, so the two ends are reported the other way round
    [Arguments(@"\b", "a bc d")] // pure assertions
    [Arguments("(?<x>a)(b)?", "abaab")] // a group that sometimes takes no part
    [Arguments("", "abc")] // the empty pattern
    public void EnumerateMatches_and_Matches_agree_on_the_awkward_shapes(string pattern, string subject)
    {
        FuzzyRegex compiled = new(pattern);

        compiled
            .EnumerateMatches(subject)
            .Select(static m => (m.Index, m.Length))
            .Should()
            .Equal(compiled.Matches(subject).Select(static m => (m.Index, m.Length)));
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void EnumerateMatches_carries_the_overlapped_flag_through_the_whole_walk(bool overlapped)
    {
        FuzzyRegex pattern = new("..");
        const string subject = "abcde";

        pattern
            .EnumerateMatches(subject, overlapped: overlapped)
            .Select(static m => m.Index)
            .Should()
            .Equal(pattern.Matches(subject, overlapped: overlapped).Select(static m => m.Index));
    }

    [Test]
    public void A_trailing_partial_is_the_last_thing_yielded()
    {
        FuzzyRegex pattern = new("abc");

        Match[] found = [.. pattern.EnumerateMatches("xabcxab", partial: true)];

        found.Select(static m => (m.Value, m.PartialMatch)).Should().Equal(("abc", false), ("ab", true));
        found.Should().Equal(pattern.Matches("xabcxab", partial: true), static (a, b) => a.Index == b.Index);
    }

    [Test]
    public void EnumerateMatches_takes_the_same_slice_as_Matches()
    {
        FuzzyRegex pattern = new("a");

        pattern.EnumerateMatches("aaaaa", beginning: 1, length: 2).Select(static m => m.Index).Should().Equal(1, 2);
    }

    [Test]
    public void EnumerateSplits_yields_what_Split_returns()
    {
        FuzzyRegex pattern = new("(x)|(1)");

        // Verified against upstream 2026-09-16: list(regex.splititer('(x)|(1)', 'a1b')) and
        // regex.split('(x)|(1)', 'a1b') are both ['a', None, '1', 'b'] - the null for the group
        // that took no part is in both.
        pattern.EnumerateSplits("a1b").Should().Equal("a", null, "1", "b");
        pattern.EnumerateSplits("a1b").Should().Equal(pattern.Split("a1b"));
    }

    [Test]
    public void EnumerateSplits_keeps_a_reverse_patterns_back_to_front_order()
    {
        // Verified against upstream 2026-09-16: list(regex.compile('(?r)x').splititer('xaxbxc'))
        // is ['c', 'b', 'a', ''], the same as its split. Upstream does not reverse the list.
        FuzzyRegex pattern = new("(?r)x");

        pattern.EnumerateSplits("xaxbxc").Should().Equal("c", "b", "a", "");
        pattern.EnumerateSplits("xaxbxc").Should().Equal(pattern.Split("xaxbxc"));
    }

    [Test]
    [Arguments("(x)|(1)", "a1b", -1)]
    [Arguments(",", "a,b,c,d", 2)]
    [Arguments(",", "a,b,c,d", 0)]
    [Arguments("x*", "abxd", -1)]
    [Arguments("(?r)(x)|(1)", "a1b", -1)]
    [Arguments(@"(\d)?-", "a1-b-c", -1)]
    public void EnumerateSplits_and_Split_agree_including_the_split_limit(string pattern, string subject, int maxSplits)
    {
        FuzzyRegex compiled = new(pattern);

        compiled.EnumerateSplits(subject, maxSplits).Should().Equal(compiled.Split(subject, maxSplits));
    }

    [Test]
    public void An_early_exit_does_not_pay_for_the_rest_of_the_subject()
    {
        // Not a stopwatch ratio, which would be a statement about the machine. The subject has two
        // easy matches and then a cliff the pattern can only fail on exponentially, and both calls
        // get the same budget: the eager one has to walk past the two matches and reach the cliff,
        // so it times out, and the lazy one stops at the second match, so it answers. A lazy call
        // that secretly ran eagerly would throw here, which is what makes the eager assertion part
        // of the test rather than decoration.
        FuzzyRegex pattern = new(_slowPattern);
        TimeSpan budget = TimeSpan.FromMilliseconds(250);

        Action eager = () => pattern.Matches(_twoMatchesThenACliff, timeout: budget);
        eager.Should().Throw<System.Text.RegularExpressions.RegexMatchTimeoutException>();

        var stopwatch = Stopwatch.StartNew();
        List<int> first =
        [
            .. pattern.EnumerateMatches(_twoMatchesThenACliff, timeout: budget).Take(2).Select(static m => m.Index),
        ];
        stopwatch.Stop();

        first.Should().Equal(0, 2);
        stopwatch.Elapsed.Should().BeLessThan(budget, "two matches at the front of the subject are not the cliff");
    }

    [Test]
    public void A_cancelled_token_stops_the_walk_at_the_step_it_fires_on()
    {
        FuzzyRegex pattern = new("a");
        using CancellationTokenSource cancellation = new();

        int seen = 0;

        Action walk = () =>
        {
            foreach (Match _ in pattern.EnumerateMatches("aaaa", cancellationToken: cancellation.Token))
            {
                seen++;
                if (seen == 2)
                {
                    cancellation.Cancel();
                }
            }
        };

        walk.Should().Throw<OperationCanceledException>();
        seen.Should().Be(2, "the two matches before the cancellation were already yielded");
    }

    [Test]
    public void The_arguments_are_checked_when_the_call_is_made_not_when_it_is_walked()
    {
        // An iterator method's body does not run until the first MoveNext, so a naive
        // implementation reports a bad argument from the foreach rather than from the call. Both
        // of these have to throw before anything is enumerated.
        FuzzyRegex pattern = new("a");

        Action nullInput = () => pattern.EnumerateMatches(null!);
        Action badTimeout = () => pattern.EnumerateMatches("a", timeout: TimeSpan.Zero);
        Action nullSplitInput = () => pattern.EnumerateSplits(null!);

        nullInput.Should().Throw<ArgumentNullException>().WithParameterName("input");
        badTimeout.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("timeout");
        nullSplitInput.Should().Throw<ArgumentNullException>().WithParameterName("input");

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        Action preCancelled = () => pattern.EnumerateMatches("a", cancellationToken: cancelled.Token);
        preCancelled.Should().Throw<OperationCanceledException>();
    }

    [Test]
    public void The_static_conveniences_answer_like_the_instance_ones()
    {
        FuzzyRegex.EnumerateMatches("xaxb", "[ab]").Select(static m => m.Value).Should().Equal("a", "b");
        FuzzyRegex.EnumerateSplits("a1b", "(x)|(1)").Should().Equal("a", null, "1", "b");
    }

    [Test]
    public void A_walk_can_be_taken_twice_from_the_same_enumerable()
    {
        // An IEnumerable<Match> that could only be walked once would be a surprise on a type
        // shaped like a query; the iterator method gives a fresh walk per GetEnumerator.
        IEnumerable<Match> walk = new FuzzyRegex("a").EnumerateMatches("aba");

        walk.Select(static m => m.Index).Should().Equal(0, 2);
        walk.Select(static m => m.Index).Should().Equal(0, 2);
    }
}
