using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Fuzzy.Text.RegularExpressions.Tests.TestThreadsLimit>]

namespace Fuzzy.Text.RegularExpressions.Tests;

/// <summary>
/// How many tests run at once in this assembly: <c>FUZZYREGEX_TEST_THREADS</c> when set to a
/// positive number, otherwise the processor count, which is TUnit's own default.
/// </summary>
/// <remarks>
/// Only <c>tools/run-stryker.ps1</c> sets the variable. Stryker runs several copies of this suite
/// at once, and each copy at the default would use every core, so four runners took the machine
/// to 100% (owner, 2026-09-17). Four runners times four threads leaves half the machine free.
/// </remarks>
public sealed class TestThreadsLimit : IParallelLimit
{
    /// <inheritdoc/>
    public int Limit =>
        int.TryParse(Environment.GetEnvironmentVariable("FUZZYREGEX_TEST_THREADS"), out int threads) && threads > 0
            ? threads
            : Environment.ProcessorCount;
}
