using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Docs;

/// <summary>
/// Pins every runnable code example in <c>docs/GUIDE.md</c> against the exact values their
/// <c>Console.WriteLine</c> calls would print, so a library regression that changes one of those
/// values fails here, in the ordinary test run, rather than only in
/// <c>tools/check-doc-examples.ps1</c> (still the CI gate that catches the sample's SOURCE
/// drifting from the guide's own markdown, by actually extracting and running it - this file does
/// not re-derive its assertions from the markdown text, for the same Native-AOT reason given in
/// <see cref="ComparisonSamples"/>). Each test calls the library directly rather than redirecting
/// <see cref="Console"/> itself, exactly as <see cref="ReadmeSamples"/> does.
/// </summary>
public sealed class GuideSamples
{
    /// <summary>"What this library is for", the first example.</summary>
    [Test]
    public void First_fuzzy_match()
    {
        Match match = FuzzyRegex.MatchAtStart("hallo", @"(?:hello){e<=1}");

        match.Value.Should().Be("hallo");
        match.FuzzyCounts.Substitutions.Should().Be(1);
    }

    /// <summary>"Choosing an entry point", the `MatchEvaluator` example.</summary>
    [Test]
    public void Match_evaluator_rewrites_only_what_it_chooses_to()
    {
        const string input = "room 12b, room 7";
        string output = FuzzyRegex.Replace(input, @"\d+", static m => "#" + m.Value);

        output.Should().Be("room #12b, room #7");
    }

    /// <summary>"Every flag in one table", the `Options` readback example.</summary>
    [Test]
    public void Options_reads_back_every_flag_actually_in_effect()
    {
        var regex = new FuzzyRegex(@"x");

        regex.Options.Should().Be(FuzzyRegexOptions.Unicode | FuzzyRegexOptions.Version1 | FuzzyRegexOptions.FullCase);
    }

    /// <summary>"Every flag in one table", the `Ascii` vs `Unicode` example.</summary>
    [Test]
    public void Ascii_restricts_word_characters_to_ascii()
    {
        var ascii = new FuzzyRegex(@"\w+", FuzzyRegexOptions.Ascii);
        var unicode = new FuzzyRegex(@"\w+");

        ascii.Match("café").Length.Should().Be(3);
        unicode.Match("café").Length.Should().Be(4);
    }

    /// <summary>"Every flag in one table", the `Posix` example.</summary>
    [Test]
    public void Posix_takes_the_longer_match_at_the_same_start()
    {
        var posix = new FuzzyRegex(@"a|ab", FuzzyRegexOptions.Posix);
        var leftmostFirst = new FuzzyRegex(@"a|ab");

        posix.Match("ab").Length.Should().Be(2);
        leftmostFirst.Match("ab").Length.Should().Be(1);
    }

    /// <summary>"Timeouts and cancellation", the pre-cancelled `CancellationToken` example.</summary>
    [Test]
    public void A_cancelled_token_stops_the_match_before_it_starts()
    {
        var regex = new FuzzyRegex(@"(a+)+b");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Action act = () => regex.Replace("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaac", "x", cancellationToken: cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    /// <summary>"Reading a result", the `FuzzyCounts`/`FuzzyChanges` example.</summary>
    [Test]
    public void An_exact_match_has_every_fuzzy_count_at_zero()
    {
        Match match = FuzzyRegex.MatchAtStart("fuzzy", @"(?:fuzzy){e<=2}");

        match.FuzzyCounts.Total.Should().Be(0);
        match.FuzzyChanges.Substitutions.Count.Should().Be(0);
    }
}
