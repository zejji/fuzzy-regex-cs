using System.Buffers;
using System.Runtime.InteropServices;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// Where one match lies in the subject, and nothing else. Shaped after
/// <see cref="System.Text.RegularExpressions.ValueMatch"/>, which the built-in <c>Regex</c>'s
/// span walk yields.
/// </summary>
/// <remarks>
/// <see cref="Index"/> and <see cref="Length"/> are UTF-16 code units, as on <see cref="Capture"/>.
/// Take the text with <c>input.Slice(match.Index, match.Length)</c>.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly ref struct ValueMatch
{
    /// <summary>Creates the value from the engine's two ends.</summary>
    /// <param name="index">Where the match starts, in UTF-16 code units.</param>
    /// <param name="length">How many UTF-16 code units it covers.</param>
    internal ValueMatch(int index, int length)
    {
        Index = index;
        Length = length;
    }

    /// <summary>Where the match starts in the subject, in UTF-16 code units.</summary>
    public int Index { get; }

    /// <summary>How many UTF-16 code units the match covers.</summary>
    public int Length { get; }
}

/// <summary>
/// The matches of a pattern in a span, found one at a time as <see cref="MoveNext"/> asks for
/// them. What <see cref="FuzzyRegex.EnumerateMatches(ReadOnlySpan{char}, TimeSpan?, CancellationToken)"/>
/// returns; shaped after <see cref="System.Text.RegularExpressions.Regex.ValueMatchEnumerator"/>.
/// </summary>
/// <remarks>
/// <para>
/// The walk borrows two things for as long as it runs: a copy of the span, in a buffer rented from
/// <see cref="ArrayPool{T}.Shared"/>, and the pattern's kept engine state. Both go back when
/// <see cref="MoveNext"/> returns <see langword="false"/> or throws, or when <see cref="Dispose"/>
/// runs, which a <c>foreach</c> does for you when it ends or breaks. A walk abandoned without
/// either leaves the buffer to the garbage collector, and the pattern builds a new state for its
/// next call.
/// </para>
/// <para>
/// The copy is needed because the engine keeps the subject between steps and a span cannot be
/// kept. The built-in enumerator reads the span in place. The timeout, as on
/// <see cref="FuzzyRegex.EnumerateMatches(string, int, int, bool, bool, TimeSpan?, CancellationToken)"/>,
/// bounds each step and not the walk.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public ref struct ValueMatchEnumerator
{
    private readonly FuzzyRegex _regex;
    private readonly ArrayPool<char> _pool;
    private readonly MatchLimits _limits;
    private readonly int _length;

    /// <summary>
    /// The state's <see cref="MatchState.Lease"/> when this walk rented it. A copy of this struct
    /// shares the state, so a second <see cref="Dispose"/> reads a different lease and does
    /// nothing, where handing the state back twice would give it to two callers at once.
    /// </summary>
    private readonly int _lease;

    private char[]? _copy;
    private MatchState? _state;
    private bool _started;
    private int _index;
    private int _matchLength;

    /// <summary>Copies the subject and rents the state the walk runs on.</summary>
    /// <param name="regex">The pattern to walk with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="limits">The per-step time budget and the caller's token, already checked.</param>
    /// <param name="pool">Where the copy of <paramref name="input"/> is rented from.</param>
    internal ValueMatchEnumerator(FuzzyRegex regex, ReadOnlySpan<char> input, MatchLimits limits, ArrayPool<char> pool)
    {
        _regex = regex;
        _pool = pool;
        _limits = limits;
        _length = input.Length;
        _copy = pool.Rent(input.Length);

        try
        {
            input.CopyTo(_copy);

            // Count's arguments: nothing reads the capture lists (Iteration.Scan).
            _state = regex.StateCache.Rent(
                regex.PatternObject,
                new ReadOnlyMemory<char>(_copy, 0, _length),
                0,
                _length,
                overlapped: false,
                partial: false,
                visibleCaptures: false,
                matchAll: false,
                limits
            );
        }
        catch
        {
            pool.Return(_copy);
            throw;
        }

        _lease = _state.Lease;
    }

    /// <summary>The match <see cref="MoveNext"/> last found.</summary>
    public readonly ValueMatch Current => new(_index, _matchLength);

    /// <summary>Lets <c>foreach</c> walk the matches.</summary>
    /// <returns>This enumerator.</returns>
    public readonly ValueMatchEnumerator GetEnumerator() => this;

    /// <summary>Finds the next match.</summary>
    /// <returns>
    /// <see langword="true"/> if there is one, now in <see cref="Current"/>; <see langword="false"/>
    /// once the walk has ended or been disposed.
    /// </returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// This step ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException">The caller's token was cancelled.</exception>
    public bool MoveNext()
    {
        if (_state is not { } state || state.Lease != _lease)
        {
            return false;
        }

        // Iteration.Enumerate's loop, one turn per call: step past the previous match, then search.
        if (_started)
        {
            state.AdvancePastMatch();
        }

        _started = true;
        state.RestartClock();

        int status = Matcher.DoMatch(state, search: true);
        if (status == MatchStatus.Cancelled)
        {
            // Built before Dispose hands the buffer back; the timeout exception reports the subject.
            Exception exception = _limits.Cancelled(new string(_copy!, 0, _length), _regex.Pattern);
            Dispose();
            throw exception;
        }

        // No Partial: the walk is not partial, so do_match answers success or nothing.
        if (status != MatchStatus.Success)
        {
            Dispose();
            return false;
        }

        // Upstream's rule that a reverse match reports its two ends the other way round (:20795).
        int start = state.Reverse ? state.TextPos : state.MatchPos;
        int end = state.Reverse ? state.MatchPos : state.TextPos;
        _index = start;
        _matchLength = end - start;
        return true;
    }

    /// <summary>
    /// Hands back the copy of the subject and the engine state. Safe to call more than once, and
    /// on a copy of this enumerator.
    /// </summary>
    public void Dispose()
    {
        if (_state is null || _state.Lease != _lease)
        {
            _state = null;
            return;
        }

        // The state first: its Release drops the subject, so nothing the pattern keeps still points
        // into the buffer once the pool has it back.
        _state.Dispose();
        _state = null;
        _pool.Return(_copy!);
        _copy = null;
    }
}
