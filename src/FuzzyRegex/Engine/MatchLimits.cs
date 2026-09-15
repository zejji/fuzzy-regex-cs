namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// What bounds one matching operation: its time budget and the caller's
/// <see cref="CancellationToken"/>. Upstream passes its <c>timeout</c> keyword down the same way,
/// as an argument of <c>pattern_search_or_match</c>, <c>pattern_subx</c>, <c>pattern_split</c> and
/// <c>pattern_scanner</c> (<c>upstream/src/_regex.c</c>); the token is this port's counterpart of
/// the <c>PyErr_CheckSignals</c> half of <c>safe_check_cancel</c> (<c>:2253</c>), which is how a
/// Python caller interrupts a long match and which a .NET caller has no other way to do.
/// </summary>
/// <remarks>
/// One value rather than three loose arguments, because all three travel together through
/// <see cref="Iteration"/>, <see cref="Substitution"/> and <see cref="MatchState.Create"/>, and
/// because the choice of which exception a cancelled run raises belongs with them - see
/// <see cref="Cancelled"/>.
/// </remarks>
internal readonly struct MatchLimits
{
    /// <summary>Creates the bounds for one operation.</summary>
    /// <param name="timeoutTicks">
    /// The budget in <see cref="System.Diagnostics.Stopwatch"/> ticks, or
    /// <see cref="MatchState.NoTimeout"/>.
    /// </param>
    /// <param name="matchTimeout">The same budget as the caller expressed it, for the exception.</param>
    /// <param name="cancellation">The caller's token.</param>
    internal MatchLimits(long timeoutTicks, TimeSpan matchTimeout, CancellationToken cancellation)
    {
        TimeoutTicks = timeoutTicks;
        MatchTimeout = matchTimeout;
        Cancellation = cancellation;
    }

    /// <summary>The budget in <see cref="System.Diagnostics.Stopwatch"/> ticks.</summary>
    internal long TimeoutTicks { get; }

    /// <summary>The budget as the caller expressed it, which the exception reports.</summary>
    internal TimeSpan MatchTimeout { get; }

    /// <summary>The caller's cancellation token.</summary>
    internal CancellationToken Cancellation { get; }

    /// <summary>
    /// The exception for a run the engine abandoned - <c>MatchStatus.Cancelled</c>. Two things can
    /// produce that status and a caller has to be able to tell them apart, so the token is asked
    /// first: a cancelled token means the caller asked to stop, and the cancellation framework asks
    /// a listener to answer that with an <see cref="OperationCanceledException"/> carrying the
    /// token ("Listeners can optionally throw this exception to verify the source of the
    /// cancellation", Cancellation in Managed Threads, read 2026-09-15). Anything else is the clock.
    /// </summary>
    /// <param name="input">The subject, which the timeout exception reports.</param>
    /// <param name="pattern">The pattern, which the timeout exception reports.</param>
    /// <returns>The exception to throw.</returns>
    internal Exception Cancelled(string input, string pattern) =>
        Cancellation.IsCancellationRequested
            ? new OperationCanceledException(Cancellation)
            : new System.Text.RegularExpressions.RegexMatchTimeoutException(input, pattern, MatchTimeout);
}
