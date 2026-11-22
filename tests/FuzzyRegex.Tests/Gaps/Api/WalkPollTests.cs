using System.Text.RegularExpressions;
using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// D167: work that grows with the subject inside one turn of the matching loop no longer holds the
/// budget poll back. A single-character repeat walks to the end of the slice in one turn
/// (<see cref="Matcher.CountOne"/>), and the fuzzy literal filter's piece searches sweep the rest
/// of the slice in one call; before this fix neither was polled, so a 10 ms timeout over
/// 10^7 code units fired after seconds (ZejPdf, 2026-11-21).
/// </summary>
/// <remarks>
/// <para>
/// The timeout tests use the seam <see cref="FuzzyRegex.ExpiresOnCheck"/> at 2, with a budget of an
/// hour so the real clock cannot fire. The walk tests are anchored and atomic, so the call is one
/// attempt of a handful of turns: its first clock check is the attempt's own, at
/// <c>start_match</c>, and a second comes only if the walk is charged to the poll counter. The
/// piece tests' subject holds no piece, so no attempt runs at all and every clock check is a poll
/// inside a piece search. Without a poll each call answers <see langword="false"/>, which the
/// controls over a short subject pin.
/// </para>
/// <para>
/// The boundary tests pin what chunking a piece search can get wrong: a piece straddling the end
/// of a chunk is found only if chunks overlap by the piece's length minus one. Their literal is
/// placed with one substitution in its second piece, so the first piece is the only one present and
/// missing it refuses the match outright.
/// </para>
/// </remarks>
[SkipUnderStryker]
public sealed class WalkPollTests
{
    /// <summary>A budget the real clock cannot spend, so only the seam can fire.</summary>
    private static readonly TimeSpan _anHour = TimeSpan.FromHours(1);

    /// <summary>One million code units of <c>ab</c>: many chunks long, and every repeat below walks all of it.</summary>
    private static readonly string _subject = string.Concat(Enumerable.Repeat("ab", 500_000));

    /// <summary>
    /// A literal of 4,000 code units whose every 20-unit stretch holds a <c>cb</c>, so neither of
    /// the two pieces <c>{e&lt;=1}</c> cuts it into occurs in <see cref="_subject"/>.
    /// </summary>
    private static readonly string _absentLiteral =
        string.Concat(Enumerable.Repeat("ababababababababcb", 222)) + "abcb";

    /// <summary>The fuzzy literal the boundary tests place: two pieces of <c>needle</c>.</summary>
    private const string _literal = "needleneedle";

    /// <summary>The literal with one substitution in its second piece, which is then absent.</summary>
    private const string _placed = "needleneedlz";

    /// <summary>A piece search's chunk for a piece as short as <c>needle</c>.</summary>
    private static int Chunk => Matcher.SweepChunkLength;

    private static FuzzyRegex ExpiringOnSecondCheck(string pattern) =>
        new(pattern, FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout) { ExpiresOnCheck = 2 };

    /// <summary>
    /// The pattern for each unpolled stretch of work, by name: <c>greedy</c> is the single-character
    /// repeat arm that calls <see cref="Matcher.CountOne"/>; <c>pieces</c>,
    /// <c>pieces-ignore-case</c> and <c>pieces-reverse</c> are the filter's forward <c>IndexOf</c>
    /// under both comparisons and its reverse <c>LastIndexOf</c>.
    /// </summary>
    /// <param name="work">The work's name.</param>
    /// <returns>The pattern.</returns>
    private static string Pattern(string work) =>
        work switch
        {
            "greedy" => @"\A(?>[ab]*)\d",
            "pieces" => $"(?:{_absentLiteral}){{e<=1}}",
            "pieces-ignore-case" => $"(?i)(?:{_absentLiteral}){{e<=1}}",
            "pieces-reverse" => $"(?r)(?:{_absentLiteral}){{e<=1}}",
            _ => throw new ArgumentOutOfRangeException(nameof(work), work, "no such work in this test"),
        };

    [Test]
    [Arguments("greedy")]
    [Arguments("pieces")]
    [Arguments("pieces-ignore-case")]
    [Arguments("pieces-reverse")]
    public void Work_that_grows_with_the_subject_polls_the_budget(string work)
    {
        FuzzyRegex pattern = ExpiringOnSecondCheck(Pattern(work));

        Action call = () => _ = pattern.IsMatch(_subject, timeout: _anHour);

        call.Should().Throw<RegexMatchTimeoutException>();
    }

    /// <summary>
    /// The control for <see cref="Work_that_grows_with_the_subject_polls_the_budget"/>: over a
    /// subject of 40 code units the walk is shorter than the 256 turns between polls and the piece
    /// search is one chunk, so the second clock check never comes and the call answers. Without it a
    /// timeout test could pass on a check made somewhere other than the work it names.
    /// </summary>
    /// <param name="work">The work's name.</param>
    [Test]
    [Arguments("greedy")]
    [Arguments("pieces")]
    [Arguments("pieces-ignore-case")]
    [Arguments("pieces-reverse")]
    public void Over_a_short_subject_the_same_pattern_answers(string work)
    {
        FuzzyRegex pattern = ExpiringOnSecondCheck(Pattern(work));

        pattern.IsMatch(_subject[..40], timeout: _anHour).Should().BeFalse();
    }

    /// <summary>
    /// Every start for the placed literal from the one whose first piece ends exactly at the first
    /// chunk's end to the one that starts the second chunk, straddling positions included.
    /// </summary>
    /// <returns>The positions.</returns>
    public static IEnumerable<int> ForwardPositionsAroundTheFirstChunkEnd() => Enumerable.Range(Chunk - 6, 7);

    [Test]
    [MethodDataSource(nameof(ForwardPositionsAroundTheFirstChunkEnd))]
    public void A_piece_straddling_a_forward_chunk_end_is_found(int at)
    {
        string subject = SubjectWithLiteralAt(at, 3 * Chunk);
        FuzzyRegex pattern = new($"(?:{_literal}){{e<=1}}");

        Match match = pattern.Match(subject, timeout: _anHour);

        match.Index.Should().Be(IndexWithinAWindow(pattern, subject, at));
    }

    /// <summary>
    /// The reverse search's chunks run back from the end of the slice: these are the starts from
    /// the one that ends the second chunk to the one whose piece starts the first, straddling
    /// positions included.
    /// </summary>
    /// <returns>The positions.</returns>
    public static IEnumerable<int> ReversePositionsAroundTheFirstChunkStart() => Enumerable.Range((2 * Chunk) - 6, 7);

    [Test]
    [MethodDataSource(nameof(ReversePositionsAroundTheFirstChunkStart))]
    public void A_piece_straddling_a_reverse_chunk_start_is_found(int at)
    {
        string subject = SubjectWithLiteralAt(at, 3 * Chunk);
        FuzzyRegex pattern = new($"(?r)(?:{_literal}){{e<=1}}");

        Match match = pattern.Match(subject, timeout: _anHour);

        match.Index.Should().Be(IndexWithinAWindow(pattern, subject, at));
    }

    /// <summary>
    /// Forward chunks count from where the search starts, not from 0: starting at 1,000 moves the
    /// first chunk's end to 1,000 + <see cref="Chunk"/>.
    /// </summary>
    /// <param name="offset">Where the literal starts relative to that end, minus the piece.</param>
    [Test]
    [Arguments(0)]
    [Arguments(3)]
    [Arguments(6)]
    public void Forward_chunks_count_from_the_search_start(int offset)
    {
        const int start = 1_000;
        int at = start + Chunk - 6 + offset;
        string subject = SubjectWithLiteralAt(at, 3 * Chunk);
        FuzzyRegex pattern = new($"(?:{_literal}){{e<=1}}");

        Match match = pattern.Match(subject, beginning: start, timeout: _anHour);

        match.Index.Should().Be(IndexWithinAWindow(pattern, subject, at));
    }

    /// <summary>
    /// The answer the same pattern gives over a short window around the literal, which no chunk
    /// boundary crosses, moved back to the subject's indices. The filler holds no match, so the
    /// whole subject's answer is the same match.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="at">Where the literal was placed.</param>
    /// <returns>The expected index.</returns>
    private static int IndexWithinAWindow(FuzzyRegex pattern, string subject, int at)
    {
        int from = at - 60;
        Match match = pattern.Match(subject[from..(at + 72)], timeout: _anHour);
        match.Success.Should().BeTrue();
        return from + match.Index;
    }

    /// <summary>
    /// Filler of near misses (<c>needlx</c>), which holds no piece and no match, with
    /// <see cref="_placed"/> at <paramref name="at"/>.
    /// </summary>
    /// <param name="at">Where the literal starts.</param>
    /// <param name="length">The subject's length.</param>
    /// <returns>The subject.</returns>
    private static string SubjectWithLiteralAt(int at, int length)
    {
        char[] text = new char[length];
        for (int i = 0; i < text.Length; i++)
        {
            text[i] = "needlx"[i % 6];
        }

        _placed.CopyTo(0, text, at, _placed.Length);
        return new string(text);
    }
}
