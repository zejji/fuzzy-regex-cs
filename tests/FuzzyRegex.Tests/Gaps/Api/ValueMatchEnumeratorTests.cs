using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S61 item 2. <see cref="FuzzyRegex.EnumerateMatches(ReadOnlySpan{char}, TimeSpan?, CancellationToken)"/>
/// runs the same loop as the string walk, reporting only where each match lies, so its answer is
/// pinned to <see cref="FuzzyRegex.Matches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>
/// rather than to hand-worked spans. Where the pool goes is <c>PoolDisciplineTests</c>' job.
/// </summary>
public sealed class ValueMatchEnumeratorTests
{
    [Test]
    [Arguments(@"\w+", "the quick brown fox")]
    [Arguments("x*", "axxbx")]
    [Arguments(@"(?r)\w+", "the quick brown fox")]
    [Arguments("(?r)x*", "axxbx")]
    [Arguments("(?:cat){e<=1}", "a cot, a cap and a dog")]
    [Arguments("(?b)(?:cat){e<=1}", "a cot, a cap and a dog")]
    [Arguments("(?e)(?:cat){e<=1}", "cta ccat caat")]
    [Arguments(".", "a\U0001F600b")]
    [Arguments(@"\X", "é\U0001F600")]
    [Arguments("(?V1)(?i)ss", "straße STRASSE")]
    [Arguments("nothing", "at all")]
    [Arguments("", "")]
    public void A_span_walk_finds_what_Matches_finds(string pattern, string subject)
    {
        FuzzyRegex regex = new(pattern);
        List<(int, int)> expected = [.. regex.Matches(subject).Select(static match => (match.Index, match.Length))];

        List<(int, int)> actual = [];
        foreach (ValueMatch match in regex.EnumerateMatches(subject.AsSpan()))
        {
            actual.Add((match.Index, match.Length));
        }

        actual.Should().Equal(expected);
    }

    [Test]
    public void A_span_walk_reads_only_the_slice_it_was_given()
    {
        // Indices are the slice's own, as the built-in Regex's span walk reports them.
        FuzzyRegex regex = new("cat");
        ReadOnlySpan<char> slice = "cat | a cat | cat".AsSpan(6, 5);

        List<(int, int)> found = [];
        foreach (ValueMatch match in regex.EnumerateMatches(slice))
        {
            found.Add((match.Index, match.Length));
        }

        found.Should().Equal((2, 3));
    }

    [Test]
    public void A_null_string_still_reaches_the_string_overload()
    {
        // A null string converts to an empty span, so if the call bound to the span overload it
        // would find nothing and say nothing. The string overload's guard is the right answer.
        FuzzyRegex regex = new("cat");

        Action call = () => _ = regex.EnumerateMatches(null!);

        call.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void A_bad_timeout_is_reported_from_the_call()
    {
        FuzzyRegex regex = new("cat");

        Action call = () => _ = regex.EnumerateMatches("cat".AsSpan(), TimeSpan.FromMilliseconds(-5));

        call.Should().Throw<ArgumentOutOfRangeException>();
    }
}
