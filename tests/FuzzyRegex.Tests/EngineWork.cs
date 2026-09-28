using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests;

/// <summary>
/// What a call cost the engine, in steps rather than seconds, for tests that guard "this stays
/// polynomial" or "this memo is used". See <see cref="WorkCounter"/> and D13 in
/// <c>docs/KNOWN-DEFECTS.md</c>: a wall-clock bound goes red when the machine is busy.
/// </summary>
/// <remarks>
/// The counter exists in a Debug build only, which is what the ratchet runs by default. A Release
/// run still makes the call, so what is left of the test there is its answer and the
/// <see cref="HangGuard"/>; the step bound is checked wherever a developer runs the suite.
/// </remarks>
internal static class EngineWork
{
    /// <summary>
    /// A match timeout for a test whose real bound is a step count or a correct answer: it only has
    /// to stop a runaway, so it is set far above anything a busy machine adds. Under the assembly's
    /// two-minute test timeout, which cannot interrupt a synchronous CPU-bound test.
    /// </summary>
    internal static readonly TimeSpan HangGuard = TimeSpan.FromMinutes(1);

    /// <summary>How many match states <paramref name="action"/> initialised.</summary>
    /// <param name="action">The call to measure. It runs on this thread.</param>
    /// <returns>The count, which is 0 in a Release build.</returns>
    internal static long StatesInitialisedBy(Action action)
    {
        long before = WorkCounter.StatesInitialised;
        action();
        return WorkCounter.StatesInitialised - before;
    }

    /// <summary>
    /// Runs <paramref name="action"/> and, in a Debug build, asserts it took at most
    /// <paramref name="maxSteps"/> steps of the matching loops.
    /// </summary>
    /// <param name="action">The call to measure.</param>
    /// <param name="maxSteps">
    /// The bound, set well above the measured count and far below the count without the mechanism
    /// the test guards.
    /// </param>
    /// <param name="because">What the bound guards.</param>
    /// <remarks>
    /// The engine is stopped at step <paramref name="maxSteps"/> + 1, so a regression that makes the
    /// call exponential fails at once instead of running until its match timeout.
    /// </remarks>
    internal static void ShouldTakeAtMostSteps(Action action, long maxSteps, string because) =>
        Bounded(
            action,
            maxSteps,
            because,
            "steps",
            static () => WorkCounter.Steps,
            static () => WorkCounter.StepLimit,
            static limit => WorkCounter.StepLimit = limit
        );

    /// <summary>
    /// Runs <paramref name="action"/> and, in a Debug build, asserts it walked at most
    /// <paramref name="maxCharacters"/> characters one at a time, stopping it there.
    /// </summary>
    /// <param name="action">The call to measure.</param>
    /// <param name="maxCharacters">The bound.</param>
    /// <param name="because">What the bound guards.</param>
    internal static void ShouldWalkAtMostCharacters(Action action, long maxCharacters, string because) =>
        Bounded(
            action,
            maxCharacters,
            because,
            "characters walked",
            static () => WorkCounter.CharactersWalked,
            static () => WorkCounter.CharacterLimit,
            static limit => WorkCounter.CharacterLimit = limit
        );

    private static void Bounded(
        Action action,
        long max,
        string because,
        string unit,
        Func<long> count,
        Func<long> getLimit,
        Action<long> setLimit
    )
    {
        long before = count();
        long previousLimit = getLimit();
        setLimit(before + max + 1);
        try
        {
            action();
        }
        catch (StepLimitReachedException)
        {
            Assert.Fail($"The call took more than {max:N0} {unit}: {because}.");
        }
        finally
        {
            setLimit(previousLimit);
        }

        if (WorkCounter.Enabled)
        {
            (count() - before).Should().BeLessThanOrEqualTo(max, because);
        }
    }
}
