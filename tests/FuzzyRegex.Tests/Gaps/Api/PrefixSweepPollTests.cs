using System.Text.RegularExpressions;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// D166: the vectorised prefix sweeps poll the budget between chunks, so a timeout or a cancelled
/// token stops a sweep that hostile text makes slow. A string search costs up to the subject's
/// length times the literal's when nearly every position is a near miss, about 0.3 s over
/// 10^7 code units (ZejPdf, 2026-11-21), and before this fix it ran as one uninterruptible call.
/// </summary>
/// <remarks>
/// <para>
/// The timeout tests use the seam <see cref="FuzzyRegex.ExpiresOnCheck"/> at 2: the first clock
/// check is the attempt's own, at <c>start_match</c>, so the second can only be a poll inside the
/// sweep. Without one the search finds nothing and the call answers <see langword="false"/>. A
/// budget of an hour on top means the real clock cannot fire, so a pass depends on neither the
/// machine nor the engine's speed.
/// </para>
/// <para>
/// The boundary tests pin what chunking can get wrong: an occurrence that straddles the end of a
/// chunk is found only if chunks overlap by the literal's length minus one.
/// </para>
/// </remarks>
[SkipUnderStryker]
public sealed class PrefixSweepPollTests
{
    /// <summary>A budget the real clock cannot spend, so only the seam can fire.</summary>
    private static readonly TimeSpan _anHour = TimeSpan.FromHours(1);

    /// <summary>
    /// A literal of 4,000 code units that nearly matches at every even position of
    /// <see cref="_hostileSubject"/>: it agrees for 3,998 units and differs at the last pair.
    /// </summary>
    private static readonly string _hostileLiteral = string.Concat(Enumerable.Repeat("ab", 1999)) + "cb";

    /// <summary>
    /// A second literal for the alternations, which the subject nearly matches at every odd
    /// position. It starts with a different character so the compiler cannot factor a common
    /// prefix out of the branches.
    /// </summary>
    private static readonly string _otherHostileLiteral = string.Concat(Enumerable.Repeat("ba", 1999)) + "da";

    /// <summary>One million code units holding neither literal; several sweep chunks long.</summary>
    private static readonly string _hostileSubject = string.Concat(Enumerable.Repeat("ab", 500_000));

    /// <summary>The literal the boundary tests place, six code units long.</summary>
    private const string _needle = "needle";

    /// <summary>
    /// Where the second chunk of a sweep from position 0 starts: the first chunk's length for a
    /// literal as short as <see cref="_needle"/>.
    /// </summary>
    private static int ChunkEnd => Matcher.SweepChunkLength;

    private static FuzzyRegex ExpiringOnSecondCheck(string pattern) =>
        new(pattern, FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout) { ExpiresOnCheck = 2 };

    /// <summary>
    /// The hostile pattern for each sweep, by name: <c>single</c> is one case-sensitive literal,
    /// which <c>StringSearch</c> finds with <c>IndexOf</c>; <c>alternation</c> is two, which the
    /// leading-literal search finds with <c>IndexOfAny(SearchValues&lt;string&gt;)</c>;
    /// <c>ignore-case</c> is the same under <see cref="StringComparison.OrdinalIgnoreCase"/>; and
    /// <c>outlier</c> holds a <c>k</c>, which the Kelvin sign also matches and .NET's rule does not,
    /// so the search for the literals runs between outliers (C6 option B3). The subject holds none,
    /// so that search is one sweep to the end.
    /// </summary>
    /// <param name="sweep">The sweep's name.</param>
    /// <returns>The pattern.</returns>
    private static string HostilePattern(string sweep) =>
        sweep switch
        {
            "single" => _hostileLiteral,
            "alternation" => $"(?:{_hostileLiteral}|{_otherHostileLiteral})",
            "ignore-case" => $"(?i)(?:{_hostileLiteral}|{_otherHostileLiteral})",
            "outlier" => $"(?i)(?:{_hostileLiteral}|{_otherHostileLiteral}k)",
            _ => throw new ArgumentOutOfRangeException(nameof(sweep), sweep, "no such sweep in this test"),
        };

    [Test]
    [Arguments("single")]
    [Arguments("alternation")]
    [Arguments("ignore-case")]
    [Arguments("outlier")]
    public void A_hostile_sweep_polls_the_budget(string sweep)
    {
        FuzzyRegex pattern = ExpiringOnSecondCheck(HostilePattern(sweep));

        Action call = () => _ = pattern.IsMatch(_hostileSubject, timeout: _anHour);

        call.Should().Throw<RegexMatchTimeoutException>();
    }

    /// <summary>
    /// The control for <see cref="A_hostile_sweep_polls_the_budget"/>: over a subject shorter than
    /// one chunk there is no poll between chunks, so the second clock check never comes and the
    /// call answers. Without it a timeout test could pass on a check made somewhere other than the
    /// sweep.
    /// </summary>
    /// <param name="sweep">The sweep's name.</param>
    [Test]
    [Arguments("single")]
    [Arguments("alternation")]
    [Arguments("ignore-case")]
    [Arguments("outlier")]
    public void Over_one_chunk_the_same_pattern_answers(string sweep)
    {
        FuzzyRegex pattern = ExpiringOnSecondCheck(HostilePattern(sweep));
        string subject = _hostileSubject[..(ChunkEnd / 2)];

        pattern.IsMatch(subject, timeout: _anHour).Should().BeFalse();
    }

    /// <summary>
    /// Every start position from the one whose occurrence ends exactly at the first chunk's end to
    /// the one that starts the second chunk, straddling positions included.
    /// </summary>
    /// <returns>The positions.</returns>
    public static IEnumerable<int> PositionsAroundTheFirstChunkEnd() =>
        Enumerable.Range(ChunkEnd - _needle.Length, _needle.Length + 1);

    [Test]
    [MethodDataSource(nameof(PositionsAroundTheFirstChunkEnd))]
    public void A_single_literal_straddling_a_chunk_end_is_found(int at)
    {
        string subject = SubjectWithNeedleAt(at);

        Match match = new FuzzyRegex(_needle).Match(subject, timeout: _anHour);

        match.Index.Should().Be(at);
    }

    [Test]
    [MethodDataSource(nameof(PositionsAroundTheFirstChunkEnd))]
    public void A_leading_literal_straddling_a_chunk_end_is_found(int at)
    {
        string subject = SubjectWithNeedleAt(at);

        Match match = new FuzzyRegex($"(?:{_needle}|pinned)").Match(subject, timeout: _anHour);

        match.Index.Should().Be(at);
    }

    /// <summary>
    /// Chunks count from where the search starts, not from 0: starting at 1,000 moves the first
    /// chunk's end to 1,000 + <see cref="ChunkEnd"/>.
    /// </summary>
    /// <param name="offset">How far past the search start the first chunk ends, minus the needle.</param>
    [Test]
    [Arguments(0)]
    [Arguments(3)]
    [Arguments(6)]
    public void Chunks_count_from_the_search_start(int offset)
    {
        const int start = 1_000;
        int at = start + ChunkEnd - _needle.Length + offset;
        string subject = SubjectWithNeedleAt(at);

        Match match = new FuzzyRegex(_needle).Match(subject, beginning: start, timeout: _anHour);

        match.Index.Should().Be(at);
    }

    /// <summary>
    /// Filler of near misses for <see cref="_needle"/> (<c>needlx</c>), with the needle at
    /// <paramref name="at"/> and two chunks of room after it.
    /// </summary>
    /// <param name="at">Where the needle starts.</param>
    /// <returns>The subject.</returns>
    private static string SubjectWithNeedleAt(int at)
    {
        char[] text = new char[at + _needle.Length + (2 * ChunkEnd)];
        for (int i = 0; i < text.Length; i++)
        {
            text[i] = "needlx"[i % 6];
        }

        _needle.CopyTo(0, text, at, _needle.Length);
        return new string(text);
    }
}
