using System.Diagnostics;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// How much work the engine did on this thread, counted in a Debug build only, so that a test can
/// say "this stays polynomial" or "this memo is used" in steps rather than in seconds. Not
/// upstream's.
/// </summary>
/// <remarks>
/// <para>
/// A wall-clock bound says something about the machine as well as the engine: the failed-call memo
/// test went red on 2026-09-28 because the machine was busy, not because the memo had stopped
/// working (D13 in <c>docs/KNOWN-DEFECTS.md</c>). A step count is the same on every machine and
/// under any load, because the engine is deterministic.
/// </para>
/// <para>
/// Every writer is <see cref="ConditionalAttribute"/> on <c>DEBUG</c>, exactly like
/// <see cref="Debug.Assert(bool)"/>: a Release build compiles the calls away, so the counts stay at
/// zero and the matching loops pay nothing. <see cref="Enabled"/> tells a test which build it has.
/// The counts are per thread because the test suite runs tests in parallel, and a synchronous call
/// runs on the thread that made it.
/// </para>
/// </remarks>
internal static class WorkCounter
{
    /// <summary>
    /// Turns of the matching loop and the backtracking loop in <c>Matcher.BasicMatch</c> on this
    /// thread: one per node tried and one per backtrack entry popped.
    /// </summary>
    [field: ThreadStatic]
    internal static long Steps { get; private set; }

    /// <summary>
    /// Calls of <see cref="MatchState.Init"/> on this thread. Each is one pass over the subject, so
    /// a scan that initialised a state per match would be quadratic.
    /// </summary>
    [field: ThreadStatic]
    internal static long StatesInitialised { get; private set; }

    /// <summary>
    /// Calls of <see cref="MatchState.NextPos"/> and <see cref="MatchState.PrevPos"/> on this
    /// thread: characters walked one at a time. A position conversion that walked the subject
    /// instead of indexing it would make a long scan quadratic without adding a single loop step.
    /// </summary>
    [field: ThreadStatic]
    internal static long CharactersWalked { get; private set; }

    /// <summary>Whether this build counts at all: <see langword="true"/> in Debug only.</summary>
    internal static bool Enabled
    {
        get
        {
            bool enabled = false;
            Enable(ref enabled);
            return enabled;
        }
    }

    /// <summary>
    /// The value of <see cref="Steps"/> at which <see cref="Step"/> throws
    /// <see cref="StepLimitReachedException"/>, or 0 for no limit. A test sets it so that a runaway
    /// stops at a step count rather than at a wall-clock timeout.
    /// </summary>
    [field: ThreadStatic]
    internal static long StepLimit { get; set; }

    /// <summary>Counts one turn of a matching or backtracking loop.</summary>
    /// <exception cref="StepLimitReachedException">The count reached <see cref="StepLimit"/>.</exception>
    [Conditional("DEBUG")]
    internal static void Step()
    {
        if (++Steps == StepLimit)
        {
            throw new StepLimitReachedException();
        }
    }

    /// <summary>
    /// The value of <see cref="CharactersWalked"/> at which <see cref="CharacterWalked"/> throws
    /// <see cref="StepLimitReachedException"/>, or 0 for no limit.
    /// </summary>
    [field: ThreadStatic]
    internal static long CharacterLimit { get; set; }

    /// <summary>Counts one character walked.</summary>
    /// <exception cref="StepLimitReachedException">The count reached <see cref="CharacterLimit"/>.</exception>
    [Conditional("DEBUG")]
    internal static void CharacterWalked()
    {
        if (++CharactersWalked == CharacterLimit)
        {
            throw new StepLimitReachedException();
        }
    }

    /// <summary>Counts one <see cref="MatchState.Init"/>.</summary>
    [Conditional("DEBUG")]
    internal static void StateInitialised() => StatesInitialised++;

    [Conditional("DEBUG")]
    private static void Enable(ref bool enabled) => enabled = true;
}

/// <summary>
/// The engine reached <see cref="WorkCounter.StepLimit"/> or <see cref="WorkCounter.CharacterLimit"/>.
/// Debug builds only.
/// </summary>
internal sealed class StepLimitReachedException : Exception
{
    /// <summary>Creates the exception.</summary>
    public StepLimitReachedException()
        : base("The engine reached the step limit a test set.") { }
}
