using BenchmarkDotNet.Attributes;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// The engine's hot loop, measured. The first benchmarks in this project: S51 needed a before/after
/// on the matching loop because it added a second read to <c>Matcher.SafeCheckCancel</c>, and
/// nothing here measured anything yet. S54 owns the real baseline set and the v1.0 gate; these are
/// deliberately few and deliberately about the loop rather than about a workload anyone runs.
/// </summary>
/// <remarks>
/// <para>
/// <b>What makes these the right shapes for a cancellation-check benchmark.</b> The check is gated
/// on <c>state.Iterations == 0</c>, a <see cref="ushort"/> stepped by <c>0x100</c>, so it is read
/// once per 256 turns of the matching or backtracking loop. A benchmark that matches quickly never
/// reaches it; what exercises it is a pattern that spends millions of turns, which is what
/// <see cref="BacktrackingFailure"/> is. <see cref="LiteralScan"/> and <see cref="FuzzyScan"/> are
/// the control: ordinary work, where the check is a rounding error and should stay one.
/// </para>
/// <para>
/// Run one: <c>dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter "*MatchingBenchmarks*"</c>.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class MatchingBenchmarks
{
    /// <summary>
    /// Exponential backtracking with no way to succeed. Eighteen characters rather than a rounder
    /// number because the cost doubles per character and the run has to stay short enough to take
    /// the default job's iteration count, which is what makes the before/after readable at all: at
    /// 20 characters and <c>--job short</c> the error bar was 21% of the mean.
    /// </summary>
    private static readonly FuzzyRegex _backtracking = new("(a|a)*b");

    /// <summary>The subject for <see cref="_backtracking"/>, with no <c>b</c> to find.</summary>
    private static readonly string _backtrackingSubject = new('a', 18);

    /// <summary>An ordinary scan: many short matches over a long subject.</summary>
    private static readonly FuzzyRegex _literal = new(@"\w+");

    /// <summary>A fuzzy scan, which is the engine this library exists for.</summary>
    private static readonly FuzzyRegex _fuzzy = new("(?:needle){e<=1}");

    /// <summary>A subject long enough that per-call overhead is not what is being measured.</summary>
    private static readonly string _prose = string.Join(
        ' ',
        Enumerable.Repeat("the quick brown fox jumps over the lazy dog and finds a needle", 200)
    );

    /// <summary>Drives the backtracking loop to exhaustion.</summary>
    /// <returns>Whether it matched, which it does not.</returns>
    [Benchmark]
    public bool BacktrackingFailure() => _backtracking.IsMatch(_backtrackingSubject);

    /// <summary>Scans a long subject for many short matches.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int LiteralScan() => _literal.Count(_prose);

    /// <summary>Scans the same subject with a fuzzy pattern.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int FuzzyScan() => _fuzzy.Count(_prose);
}
