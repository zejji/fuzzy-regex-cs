using AwesomeAssertions;
using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Tests;

/// <summary>
/// What a call cost the engine, in steps rather than seconds, for tests that guard "this stays
/// polynomial" or "this memo is used". See <see cref="WorkCounter"/> and D13 in
/// <c>docs/KNOWN-DEFECTS.md</c>: a wall-clock bound goes red when the machine is busy.
/// </summary>
/// <remarks>
/// <para>
/// The counters exist in a Debug build only. Every helper here runs the call in both builds, so
/// the answers the call asserts are checked in Release too. Then, in Release, it reports the test
/// as skipped, with <see cref="DebugOnly"/> as the reason, where it would check the count: a bound
/// on a counter that is always 0 would pass for nothing.
/// </para>
/// <para>
/// CI runs the Release suite through the ratchet, which accepts exactly that skip, and runs every
/// test of <see cref="Category"/> again in Debug, where the bounds hold (.github/workflows/ci.yml).
/// Both depend on the category, so every helper fails a test that does not carry it.
/// </para>
/// </remarks>
internal static class EngineWork
{
    /// <summary>The category of every test that bounds a <see cref="WorkCounter"/> count.</summary>
    internal const string Category = "WorkCounter";

    /// <summary>
    /// The reason a counter test gives when it is skipped in Release. <c>tools/PortTools.psm1</c>
    /// accepts this exact reason, so the ratchet does not count the skip as a regression.
    /// </summary>
    internal const string DebugOnly =
        "Debug-only work counter: WorkCounter is compiled out of a Release build, so this bound cannot be checked here";

    /// <summary>
    /// A match timeout for a test whose real bound is a step count or a correct answer: it only has
    /// to stop a runaway, so it is set far above anything a busy machine adds. Under the assembly's
    /// two-minute test timeout, which cannot interrupt a synchronous CPU-bound test.
    /// </summary>
    internal static readonly TimeSpan HangGuard = TimeSpan.FromMinutes(1);

    /// <summary>
    /// For a test that reads <see cref="WorkCounter"/> itself: fails it unless it carries
    /// <see cref="Category"/>, then skips it unless this build counts.
    /// </summary>
    internal static void SkipUnlessCounting()
    {
        RequireCategory();
        Skip.Unless(WorkCounter.Enabled, DebugOnly);
    }

    /// <summary>
    /// Runs <paramref name="action"/> and asserts it took at most <paramref name="maxSteps"/> steps
    /// of the matching loops.
    /// </summary>
    /// <param name="action">The call to measure, with its own answer assertions.</param>
    /// <param name="maxSteps">
    /// The bound, set well above the measured count and far below the count without the mechanism
    /// the test guards.
    /// </param>
    /// <param name="because">What the bound guards.</param>
    /// <remarks>
    /// In Debug the engine is stopped at step <paramref name="maxSteps"/> + 1, so a regression that
    /// makes the call exponential fails at once instead of running until its match timeout.
    /// </remarks>
    internal static void ShouldTakeAtMostSteps(Action action, long maxSteps, string because) =>
        Bounded(
            action,
            maxSteps,
            because,
            "steps",
            static () => WorkCounter.Steps,
            static limit => WorkCounter.StepLimit = limit
        );

    /// <summary>
    /// Runs <paramref name="action"/> and asserts it walked at most
    /// <paramref name="maxCharacters"/> characters one at a time, stopping it there in Debug.
    /// </summary>
    /// <param name="action">The call to measure, with its own answer assertions.</param>
    /// <param name="maxCharacters">The bound.</param>
    /// <param name="because">What the bound guards.</param>
    internal static void ShouldWalkAtMostCharacters(Action action, long maxCharacters, string because) =>
        Bounded(
            action,
            maxCharacters,
            because,
            "characters walked",
            static () => WorkCounter.CharactersWalked,
            static limit => WorkCounter.CharacterLimit = limit
        );

    /// <summary>
    /// Runs <paramref name="action"/> and asserts the fuzzy literal filter searched at most
    /// <paramref name="maxCharacters"/> characters of the subject.
    /// </summary>
    /// <param name="action">The call to measure, with its own answer assertions.</param>
    /// <param name="maxCharacters">The bound.</param>
    /// <param name="because">What the bound guards.</param>
    internal static void ShouldSearchAtMostCharacters(Action action, long maxCharacters, string because)
    {
        RequireCategory();
        long before = WorkCounter.CharactersSearched;
        action();
        SkipUnlessCounting();
        (WorkCounter.CharactersSearched - before).Should().BeLessThanOrEqualTo(maxCharacters, because);
    }

    /// <summary>
    /// Runs <paramref name="action"/> and asserts it initialised exactly <paramref name="expected"/>
    /// match states.
    /// </summary>
    /// <param name="action">The call to measure, with its own answer assertions.</param>
    /// <param name="expected">The count.</param>
    /// <param name="because">What the count guards.</param>
    internal static void ShouldInitialiseStates(Action action, long expected, string because)
    {
        RequireCategory();
        long before = WorkCounter.StatesInitialised;
        action();
        SkipUnlessCounting();
        (WorkCounter.StatesInitialised - before).Should().Be(expected, because);
    }

    private static void Bounded(
        Action action,
        long max,
        string because,
        string unit,
        Func<long> count,
        Action<long> setLimit
    )
    {
        RequireCategory();
        long before = count();
        if (WorkCounter.Enabled)
        {
            setLimit(before + max + 1);
        }

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
            setLimit(0);
        }

        SkipUnlessCounting();
        (count() - before).Should().BeLessThanOrEqualTo(max, because);
    }

    /// <summary>
    /// Fails the current test, in both builds, unless it carries <see cref="Category"/>: without
    /// it the Release ratchet would still accept its skip while the Debug CI step never ran it.
    /// </summary>
    private static void RequireCategory()
    {
        TestContext context =
            TestContext.Current ?? throw new InvalidOperationException("EngineWork is for use inside a test.");
        if (!context.Metadata.TestDetails.Categories.Contains(Category))
        {
            Assert.Fail(
                $"A test that bounds a WorkCounter count must carry [Category(EngineWork.Category)] (\"{Category}\"), "
                    + "or CI never checks its bound in Debug."
            );
        }
    }
}
