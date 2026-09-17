namespace Fuzzy.Text.RegularExpressions.Tests;

/// <summary>
/// Skips a load-sensitive test class while Stryker is mutating, and only then.
/// </summary>
/// <remarks>
/// Stryker runs the whole suite once per mutant, several mutants at a time. The classes carrying
/// this attribute measure real parallelism, wall-clock timeouts and megabyte scans, so under that
/// load they overrun Stryker's per-test coverage capture (which then treats them as covering EVERY
/// mutant) and tip mutants into "Timeout", which Stryker counts as killed. Measured 2026-09-17 on
/// the parsing chunk: 557 of 2189 mutants ended as Timeout at fourteen parallel runners. None of
/// these classes pins logic a single-line mutant changes; the ordinary suite and CI still run
/// them. <c>tools/run-stryker.ps1</c> sets <c>FUZZYREGEX_UNDER_STRYKER=1</c>; nothing else does.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipUnderStrykerAttribute()
    : SkipAttribute("Load-sensitive: skipped while Stryker mutates (FUZZYREGEX_UNDER_STRYKER=1)")
{
    /// <inheritdoc/>
    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(
            string.Equals(Environment.GetEnvironmentVariable("FUZZYREGEX_UNDER_STRYKER"), "1", StringComparison.Ordinal)
        );
}
