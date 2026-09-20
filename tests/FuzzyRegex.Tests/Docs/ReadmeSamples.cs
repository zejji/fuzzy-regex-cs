using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Docs;

/// <summary>
/// Pins the three code samples in <c>README.md</c>'s "Quick start" section against the exact
/// values their <c>Console.WriteLine</c> calls would print, so a library regression that changes
/// one of those values fails here, in the ordinary test run, rather than only in
/// <c>tools/check-doc-examples.ps1</c> (still the CI gate, `ci.yml`, that catches the sample's
/// SOURCE drifting from the README's own markdown, by actually extracting and running it - this
/// file does not re-derive its assertions from the README text, since doing so at test time would
/// need either a subprocess build or Roslyn scripting, and the latter is not Native-AOT-safe,
/// which the whole suite is published as, per `tools/run-aot-tests.ps1`). Each test calls the
/// library directly rather than redirecting <see cref="Console"/> itself - TUnit's own log
/// correlation walks the async context through <see cref="Console.Out"/>, and it publishes no
/// documented way to read a test's own captured output back for an assertion (checked tunit.dev's
/// Test Context and Logging pages, 2026-09-18).
/// </summary>
public sealed class ReadmeSamples
{
    /// <summary>"Exact match with named groups".</summary>
    [Test]
    public void Exact_match_with_named_groups()
    {
        Match match = FuzzyRegex.Match("2026-09-16", @"(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})");

        IEnumerable<string> lines = match.Groups.Keys.Select(key => $"{key}: {match.Groups[key].Value}");

        lines.Should().Equal("0: 2026-09-16", "year: 2026", "month: 09", "day: 16");
    }

    /// <summary>"Fuzzy match with an error budget".</summary>
    [Test]
    public void Fuzzy_match_with_an_error_budget()
    {
        var regex = new FuzzyRegex(@"(?:foo){e<=2}");
        Match match = regex.MatchAtStart("fou");

        (int substitutions, int insertions, int deletions) = match.FuzzyCounts;

        match.Value.Should().Be("fou");
        $"{substitutions} substitution(s), {insertions} insertion(s), {deletions} deletion(s)"
            .Should()
            .Be("1 substitution(s), 0 insertion(s), 0 deletion(s)");
    }

    /// <summary>"Enumerating matches with a timeout".</summary>
    [Test]
    public void Enumerating_matches_with_a_timeout()
    {
        var regex = new FuzzyRegex(@"\w+");

        IEnumerable<string> values = regex
            .EnumerateMatches("one two three", timeout: TimeSpan.FromSeconds(1))
            .Select(static match => match.Value);

        values.Should().Equal("one", "two", "three");
    }
}
