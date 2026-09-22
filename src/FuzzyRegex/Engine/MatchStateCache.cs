using System.Buffers;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// One <see cref="MatchState"/> a compiled pattern keeps between calls, so that a call on a warm
/// pattern builds nothing. The counterpart of upstream's <c>groups_storage</c>,
/// <c>repeats_storage</c> and <c>stack_storage</c> (<c>upstream/src/_regex.c</c> lines 577-579),
/// which <c>state_init_2</c> takes from the pattern and <c>state_fini</c> hands back.
/// </summary>
/// <remarks>
/// <para>
/// <b>One slot, taken and put back with <see cref="Interlocked.Exchange{T}(ref T, T)"/></b>, which
/// is the shape of the built-in <c>Regex._runner</c>. A call that finds the slot empty, because
/// another thread holds the state, builds its own and offers it back
/// afterwards; whichever state arrives last is the one kept, and the other is left to the
/// collector. So two threads never share a state, and the slot is the only field any call writes.
/// Upstream guards its storage with the pattern's lock instead (<c>acquire_state_lock</c>,
/// <c>:20847</c>), which this port does not have.
/// </para>
/// <para>
/// A state that comes back has already let go of the call's subject and cancellation token and
/// returned its stack buffers to the pool (<see cref="MatchState.Release"/>). What it keeps is what
/// upstream keeps: the group and repeat blocks, grown to the largest match seen so far.
/// </para>
/// </remarks>
internal sealed class MatchStateCache
{
    private MatchState? _state;

    /// <summary>
    /// A state ready to match, the kept one when it is free and a new one otherwise. The arguments
    /// are <see cref="MatchState.Create"/>'s.
    /// </summary>
    /// <param name="pattern">The compiled pattern, which must be the one every call on this cache passes.</param>
    /// <param name="text">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="overlapped">Whether a search may start inside the previous match.</param>
    /// <param name="partial">Whether a partial match is wanted.</param>
    /// <param name="visibleCaptures">Whether the caller will read the capture lists.</param>
    /// <param name="matchAll">Whether the match must cover the whole slice.</param>
    /// <param name="limits">The time budget and cancellation token bounding this operation.</param>
    /// <param name="oneUnitPerCharacter">What a previous state already knows about the subject.</param>
    /// <param name="pool">
    /// A pool other than the shared one, which only <c>PoolDisciplineTests</c> passes. It gets a
    /// state of its own that is never kept, so the kept state never rents from a test's pool.
    /// </param>
    /// <returns>
    /// The state, which the caller disposes, or hands back with <see cref="Return"/>, exactly once.
    /// </returns>
    internal MatchState Rent(
        PatternObject pattern,
        string text,
        int start,
        int end,
        bool overlapped,
        bool partial,
        bool visibleCaptures,
        bool matchAll,
        MatchLimits limits,
        bool? oneUnitPerCharacter = null,
        ArrayPool<byte>? pool = null
    )
    {
        if (pool is not null)
        {
            return MatchState.Create(
                pattern,
                text,
                start,
                end,
                overlapped,
                partial,
                visibleCaptures,
                matchAll,
                limits,
                pool,
                oneUnitPerCharacter
            );
        }

        MatchState? state = Interlocked.Exchange(ref _state, null);
        if (state is null)
        {
            return MatchState.Create(
                pattern,
                text,
                start,
                end,
                overlapped,
                partial,
                visibleCaptures,
                matchAll,
                limits,
                oneUnitPerCharacter: oneUnitPerCharacter,
                cache: this
            );
        }

        state.Init(text, start, end, overlapped, partial, visibleCaptures, matchAll, limits, oneUnitPerCharacter);
        return state;
    }

    /// <summary>
    /// Hands a state back, whatever the match left in it. <see cref="MatchState.Dispose"/> calls this
    /// for a state this cache built.
    /// </summary>
    /// <param name="state">A state <see cref="Rent"/> gave out, which the caller no longer reads.</param>
    internal void Return(MatchState state)
    {
        state.Release();
        Volatile.Write(ref _state, state);
    }
}
