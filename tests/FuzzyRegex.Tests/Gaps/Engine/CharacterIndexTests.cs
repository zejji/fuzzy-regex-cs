using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Engine;

/// <summary>
/// <see cref="CharacterIndex"/> against the walk it replaces, by exhaustive comparison.
/// </summary>
/// <remarks>
/// <para>
/// The index exists so that a subject containing astral characters converts between a character
/// count and a UTF-16 position in bounded time rather than by walking, which is what made the repeat
/// opcodes quadratic (DECISIONS 2026-09-01). It is a pure function of the subject, and the walk it
/// replaces is a correct and obvious reference implementation of that same function, so the honest
/// test is to run both over every subject, position, count and slice bound in a space small enough
/// to enumerate, and require them to agree everywhere. Nothing below is an expected value I wrote
/// down and could have written down wrongly.
/// </para>
/// <para>
/// The alphabet deliberately includes lone surrogates - halves of a pair with no partner, which a
/// <see cref="string"/> is allowed to contain and which count as one character of one code unit -
/// and the bounds sweep deliberately includes positions inside a surrogate pair, because
/// <c>beginning</c> and <c>length</c> are public API and a caller can split one. The walk has a
/// definite behaviour in both cases and the index has to reproduce it, not improve on it.
/// </para>
/// </remarks>
public sealed class CharacterIndexTests
{
    /// <summary>
    /// One symbol of each shape that changes the answer: one code unit, two code units, and each
    /// half of a pair standing alone.
    /// </summary>
    private static readonly string[] _alphabet =
    [
        "a", // ASCII
        "é", // Latin-1, still one code unit
        "中", // BMP, one code unit
        "\U0001F600", // astral, a well-formed pair, two code units
        "\U000E0041", // astral from another plane
        "\uD83D", // a lone high surrogate
        "\uDE00", // a lone low surrogate
        "\uDBFF", // the last high surrogate, which pairs with nothing here
    ];

    /// <summary>The walk <see cref="CharacterIndex.StepForward"/> replaces, verbatim.</summary>
    private static int WalkForward(MatchState state, int pos, long count, int sliceEnd)
    {
        for (long i = 0; i < count && pos < sliceEnd; i++)
        {
            pos = state.NextPos(pos);
        }

        return pos;
    }

    /// <summary>The walk <see cref="CharacterIndex.StepBackward"/> replaces, verbatim.</summary>
    private static int WalkBackward(MatchState state, int pos, long count, int sliceStart)
    {
        for (long i = 0; i < count && pos > sliceStart; i++)
        {
            pos = state.PrevPos(pos);
        }

        return pos;
    }

    /// <summary>The walk <see cref="CharacterIndex.CountBetween"/> replaces, verbatim.</summary>
    private static long WalkCount(MatchState state, int from, int to)
    {
        int pos = Math.Min(from, to);
        int end = Math.Max(from, to);
        long count = 0;

        while (pos < end)
        {
            pos = state.NextPos(pos);
            ++count;
        }

        return count;
    }

    private static MatchState StateFor(string subject, int beginning, int end)
    {
        var regex = new FuzzyRegex("x");
        return MatchState.Create(
            regex.PatternObject,
            subject,
            beginning,
            end,
            overlapped: false,
            partial: false,
            visibleCaptures: false,
            matchAll: false,
            timeout: MatchState.NoTimeout
        );
    }

    /// <summary>Every string of exactly <paramref name="length"/> symbols from the alphabet.</summary>
    /// <param name="length">How many symbols.</param>
    /// <returns>The subjects.</returns>
    private static IEnumerable<string> AllSubjectsOfLength(int length)
    {
        if (length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        foreach (string prefix in AllSubjectsOfLength(length - 1))
        {
            foreach (string symbol in _alphabet)
            {
                yield return prefix + symbol;
            }
        }
    }

    /// <summary>
    /// Compares the index against the walk at every position and count, and reports the first
    /// disagreement with enough detail to reproduce it.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="beginning">Upstream's <c>pos</c>.</param>
    /// <param name="end">Upstream's <c>endpos</c>.</param>
    /// <returns>The first disagreement, or <see langword="null"/>.</returns>
    private static string? FirstDisagreement(string subject, int beginning, int end)
    {
        using MatchState state = StateFor(subject, beginning, end);
        CharacterIndex index = state.GetCharacterIndex();

        int sliceStart = state.SliceStart;
        int sliceEnd = state.SliceEnd;

        // Only positions up to TextEnd. Above it the forward and backward walks genuinely disagree
        // about where a character starts, because NextPos stops pairing at TextEnd while PrevPos
        // pairs regardless - but the engine never holds a position above TextEnd, so no single
        // boundary set has to satisfy both, and asking for one here would be testing a case that
        // cannot arise. See the note in CharacterIndex.
        int units = state.TextEnd;

        for (int pos = 0; pos <= units; pos++)
        {
            // One past the whole subject, so the clamping at each bound is covered from outside it.
            for (long count = 0; count <= units + 1; count++)
            {
                int forward = index.StepForward(pos, count, sliceEnd);
                int walkedForward = WalkForward(state, pos, count, sliceEnd);
                if (forward != walkedForward)
                {
                    return Describe("StepForward", subject, beginning, end, pos, count, forward, walkedForward);
                }

                int backward = index.StepBackward(pos, count, sliceStart);
                int walkedBackward = WalkBackward(state, pos, count, sliceStart);
                if (backward != walkedBackward)
                {
                    return Describe("StepBackward", subject, beginning, end, pos, count, backward, walkedBackward);
                }
            }

            for (int other = 0; other <= units; other++)
            {
                long counted = index.CountBetween(pos, other);
                long walked = WalkCount(state, pos, other);
                if (counted != walked)
                {
                    return Describe("CountBetween", subject, beginning, end, pos, other, counted, walked);
                }
            }
        }

        return null;
    }

    private static string Describe(
        string operation,
        string subject,
        int beginning,
        int end,
        int pos,
        long argument,
        long got,
        long expected
    ) =>
        $"{operation}(pos: {pos}, {argument}) gave {got}, the walk gives {expected}. "
        + $"Subject U+[{string.Join(" ", subject.Select(static c => ((int)c).ToString("X4")))}], "
        + $"beginning {beginning}, end {end}.";

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    public void The_index_agrees_with_the_walk_on_every_short_subject(int length)
    {
        // Exhaustive rather than random: with eight symbols there are 4,681 subjects of four
        // characters or fewer, and every adjacency that can change an answer - a pair, a lone half,
        // a lone half touching a real pair, a pair split by a bound - occurs somewhere in that set.
        // Enumerating beats sampling whenever the space is small enough to enumerate, and it means
        // there is no seed to record and no run that is luckier than another.
        foreach (string subject in AllSubjectsOfLength(length))
        {
            FirstDisagreement(subject, 0, subject.Length).Should().BeNull();
        }
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public void The_index_agrees_with_the_walk_for_every_pair_of_slice_bounds(int length)
    {
        // 'beginning' and 'length' are public API, so a caller can start or end a search inside a
        // surrogate pair. The walk has a definite behaviour there - going forwards it can step
        // *past* the bound, because one step moves two code units - and the index has to reproduce
        // that rather than clamp to the bound.
        foreach (string subject in AllSubjectsOfLength(length))
        {
            for (int beginning = 0; beginning <= subject.Length; beginning++)
            {
                for (int end = beginning; end <= subject.Length; end++)
                {
                    FirstDisagreement(subject, beginning, end).Should().BeNull();
                }
            }
        }
    }

    [Test]
    public void The_index_agrees_with_the_walk_across_its_sampling_stride()
    {
        // The table samples every 32nd character, so a subject has to be longer than that before the
        // binary search has anything to choose between. This subject is every ordered pair of
        // alphabet symbols, concatenated and then repeated: 128 characters per cycle, four cycles,
        // so 512 characters and 17 samples - enough that the search runs several iterations and
        // lands on interior entries rather than always the first or the last. Every adjacency that
        // can change an answer is present, and there is no random number generator, so it is the
        // same subject on every run.
        string cycle = string.Concat(_alphabet.SelectMany(first => _alphabet.Select(second => first + second)));
        string subject = string.Concat(Enumerable.Repeat(cycle, 4));

        using MatchState state = StateFor(subject, 0, subject.Length);
        CharacterIndex index = state.GetCharacterIndex();

        long[] counts = [0, 1, 2, 62, 63, 64, 65, 66, 127, 128, 129, 191, 192, 193, 255, 256, 257, 1_000_000];

        for (int pos = 0; pos <= subject.Length; pos++)
        {
            foreach (long count in counts)
            {
                index
                    .StepForward(pos, count, state.SliceEnd)
                    .Should()
                    .Be(WalkForward(state, pos, count, state.SliceEnd), "StepForward({0}, {1})", pos, count);

                index
                    .StepBackward(pos, count, state.SliceStart)
                    .Should()
                    .Be(WalkBackward(state, pos, count, state.SliceStart), "StepBackward({0}, {1})", pos, count);
            }

            index.CountBetween(0, pos).Should().Be(WalkCount(state, 0, pos), "CountBetween(0, {0})", pos);
            index
                .CountBetween(pos, subject.Length)
                .Should()
                .Be(WalkCount(state, pos, subject.Length), "CountBetween({0}, end)", pos);
        }
    }
}
