namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// Converts between a character count and a UTF-16 position in constant time, for a subject where
/// the two are not the same number.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not an upstream type.</b> Upstream indexes the subject by codepoint
/// (<c>PyUnicode_KIND</c>/<c>PyUnicode_DATA</c>, <c>upstream/src/_regex.c</c> lines 18230-18232), so
/// the k-th character is at index k and <c>text_pos + count * step</c> is the only conversion it
/// needs. This port indexes by UTF-16 code unit, where an astral character is one character and two
/// units, so the same conversion is a question that has to be answered.
/// </para>
/// <para>
/// <see cref="MatchState.OneUnitPerCharacter"/> answers it by arithmetic for a subject with no
/// surrogate pair in it, which is nearly all of them. This type answers it for the rest. Without
/// either, <see cref="Matcher.StepBy"/> and <see cref="Matcher.CountBetween"/> walk the subject one
/// character at a time, and because the repeat opcodes' backtrack arms call them once per repeat
/// position, a walk makes the whole scan quadratic - measured at exactly 3n^2 walk steps before this
/// existed (DECISIONS 2026-09-01).
/// </para>
/// <para>
/// <b>The structure</b> is a sampled position table: the UTF-16 position of every
/// <see cref="_stride"/>-th character boundary. Answering a query means one array lookup or one
/// binary search over that table, and then at most <see cref="_stride"/> - 1 steps of exactly the
/// walk this type exists to avoid. So the work per query is bounded by a constant instead of by the
/// length of the subject, which is the whole point, and the last few steps reuse
/// <see cref="MatchState.NextPos"/> rather than reimplementing it.
/// </para>
/// <para>
/// A rank-and-select bitmap would make the queries truly O(1) rather than O(log n) plus a bounded
/// walk, and would cost about half as much memory. It is the upgrade path if a measurement ever asks
/// for one, and it would be no harder to trust: like this type it is a pure function of the subject,
/// so the same exhaustive comparison against the walk in <c>CharacterIndexTests</c> would check it
/// just as completely. It was not chosen because it is more code doing bit arithmetic to buy
/// something nothing currently needs - the stride below is already past the point where less walking
/// shows up on a clock.
/// </para>
/// <para>
/// <b>Boundaries.</b> A <i>character boundary</i> is a position the walk from 0 visits: 0, then
/// repeatedly <see cref="MatchState.NextPos"/>, up to and including the end of the subject. Position
/// b(k) is the k-th of them. A position that is the low half of a surrogate pair is not a boundary,
/// which matters because the public API lets a caller start or end a search there.
/// </para>
/// </remarks>
internal sealed class CharacterIndex
{
    /// <summary>
    /// Characters between one sampled position and the next.
    /// </summary>
    /// <remarks>
    /// A power of two so the division and remainder in <see cref="PositionOf"/> are a shift and a
    /// mask, and 32 by measurement. Halving the stride halves the residual walk exactly, and doubles
    /// the table: on a 48,002 character astral subject the walk costs 96.5 steps per character at a
    /// stride of 64, 48.5 at 32, 24.5 at 16 and 12.5 at 8. Below 32 the wall clock stops improving,
    /// because the binary search and the call overhead dominate what is left of the walk, so the
    /// extra memory buys nothing. At 32 the table is one <see cref="int"/> per 32 characters, which
    /// for an all-astral subject is about 3% of what the subject itself occupies.
    /// </remarks>
    private const int _stride = 32;

    private readonly MatchState _state;

    /// <summary>
    /// <c>_samples[i]</c> is the position of character boundary <c>i * _stride</c>. Always starts
    /// with 0, and always has at least one entry.
    /// </summary>
    private readonly int[] _samples;

    /// <summary>How many characters the whole subject holds, so <c>b(CharacterCount)</c> is its end.</summary>
    private readonly long _characterCount;

    /// <summary>
    /// Walks the subject once and records every <see cref="_stride"/>-th boundary.
    /// </summary>
    /// <param name="state">The state whose subject is being indexed.</param>
    internal CharacterIndex(MatchState state)
    {
        _state = state;

        // Only as far as TextEnd, which is where the search stops and therefore the highest
        // position the engine can ever ask about. Indexing the whole string instead would make
        // Match(hugeSubject, 0, 10) walk the whole subject to answer questions about ten units of
        // it. Upper bound on the sample count: one per _stride characters, and there are at most
        // TextEnd characters. The '+ 1' is the entry for boundary zero.
        var samples = new int[(state.TextEnd / _stride) + 1];
        int sampleCount = 0;
        long characters = 0;
        int pos = 0;

        while (true)
        {
            if (characters % _stride == 0)
            {
                samples[sampleCount++] = pos;
            }

            if (pos >= state.TextEnd)
            {
                break;
            }

            pos = state.NextPos(pos);
            ++characters;
        }

        _samples = sampleCount == samples.Length ? samples : samples[..sampleCount];
        _characterCount = characters;
    }

    /// <summary>The position of the <paramref name="k"/>-th character boundary, <c>b(k)</c>.</summary>
    /// <param name="k">Which boundary, clamped into range.</param>
    /// <returns>Its UTF-16 position.</returns>
    private int PositionOf(long k)
    {
        if (k <= 0)
        {
            return 0;
        }

        if (k >= _characterCount)
        {
            return _state.TextEnd;
        }

        int pos = _samples[(int)(k / _stride)];
        for (long i = k % _stride; i > 0; i--)
        {
            pos = _state.NextPos(pos);
        }

        return pos;
    }

    /// <summary>
    /// The rank of the greatest character boundary at or before <paramref name="pos"/>: the <c>k</c>
    /// with <c>b(k) &lt;= pos &lt; b(k+1)</c>.
    /// </summary>
    /// <param name="pos">A UTF-16 position, which need not be a boundary.</param>
    /// <returns>The rank.</returns>
    private long RankFloor(int pos)
    {
        if (pos <= 0)
        {
            return 0;
        }

        if (pos >= _state.TextEnd)
        {
            return _characterCount;
        }

        // The greatest sample at or before pos. Its boundary is at most _stride - 1 characters
        // before the answer, because the next sample sits after pos.
        int low = 0;
        int high = _samples.Length - 1;
        while (low < high)
        {
            int mid = low + ((high - low + 1) / 2);
            if (_samples[mid] <= pos)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        long rank = (long)low * _stride;
        int walk = _samples[low];
        while (walk < pos)
        {
            int next = _state.NextPos(walk);
            if (next > pos)
            {
                // 'pos' is inside this character, so the greatest boundary at or before it is the
                // one the walk is standing on. This is the low half of a surrogate pair.
                break;
            }

            walk = next;
            ++rank;
        }

        return rank;
    }

    /// <summary>
    /// The rank of the least character boundary at or after <paramref name="pos"/>.
    /// </summary>
    /// <param name="pos">A UTF-16 position, which need not be a boundary.</param>
    /// <returns>The rank.</returns>
    private long RankCeiling(int pos)
    {
        long floor = RankFloor(pos);
        return PositionOf(floor) == pos ? floor : floor + 1;
    }

    /// <summary>
    /// <see cref="Matcher.CountBetween"/>'s answer: how many characters lie between two positions.
    /// </summary>
    /// <param name="from">One position.</param>
    /// <param name="to">The other.</param>
    /// <returns>The count.</returns>
    internal long CountBetween(int from, int to)
    {
        if (from == to)
        {
            // The walk's loop condition fails immediately. Worth its own arm rather than falling out
            // of the arithmetic below, which would answer 1 for a position inside a surrogate pair:
            // the floor of such a position is the boundary before it and the ceiling the boundary
            // after, so the two ranks differ even though no character lies between a position and
            // itself.
            return 0;
        }

        int low = Math.Min(from, to);
        int high = Math.Max(from, to);

        // The walk starts at 'low' and steps until it reaches or passes 'high', so a position inside
        // a character at either end still costs a whole step. RankFloor at the near end and
        // RankCeiling at the far end say exactly that.
        return RankCeiling(high) - RankFloor(low);
    }

    /// <summary>
    /// <see cref="Matcher.StepBy"/>'s answer going forwards: <paramref name="count"/> characters
    /// after <paramref name="pos"/>, stopping at <paramref name="sliceEnd"/>.
    /// </summary>
    /// <param name="pos">Where to start.</param>
    /// <param name="count">How many characters to step.</param>
    /// <param name="sliceEnd">The bound the walk stops at.</param>
    /// <returns>The stepped position.</returns>
    internal int StepForward(int pos, long count, int sliceEnd)
    {
        if (count <= 0 || pos >= sliceEnd)
        {
            // The walk's loop condition fails before its first step, so it returns where it started
            // - which may not be a character boundary, and so must not go through PositionOf.
            return pos;
        }

        long start = RankFloor(pos);
        long available = RankCeiling(sliceEnd) - start;
        return PositionOf(start + Math.Min(count, available));
    }

    /// <summary>
    /// <see cref="Matcher.StepBy"/>'s answer going backwards.
    /// </summary>
    /// <param name="pos">Where to start.</param>
    /// <param name="count">How many characters to step.</param>
    /// <param name="sliceStart">The bound the walk stops at.</param>
    /// <returns>The stepped position.</returns>
    internal int StepBackward(int pos, long count, int sliceStart)
    {
        if (count <= 0 || pos <= sliceStart)
        {
            return pos;
        }

        // Going backwards from inside a character, the first step lands on that character's start,
        // so the position counts as the boundary *after* it - which is what RankCeiling gives.
        long start = RankCeiling(pos);
        long available = start - RankFloor(sliceStart);
        return PositionOf(start - Math.Min(count, available));
    }
}
