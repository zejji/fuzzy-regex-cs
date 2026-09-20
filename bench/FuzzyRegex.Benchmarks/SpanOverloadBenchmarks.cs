using BenchmarkDotNet.Attributes;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// The measurement behind S58's span-threading decision: what the
/// <see cref="System.ReadOnlySpan{T}"/> overloads' copy actually costs, per call and per byte.
/// </summary>
/// <remarks>
/// <para>
/// <c>FuzzyRegex.IsMatch(ReadOnlySpan&lt;char&gt;)</c> and <c>Count(ReadOnlySpan&lt;char&gt;)</c>
/// both call <c>input.ToString()</c>, because the engine indexes a <see cref="string"/> throughout
/// (<c>MatchState.Text</c>), so today they spare the caller a conversion and not the allocation.
/// The <c>ponytail:</c> notes beside both say so. This suite is the number that decides whether
/// threading a span through the engine is worth its cost, and it changes no engine code.
/// </para>
/// <para>
/// <b>The pattern matches at index 0 on purpose.</b> The question is the cost of the copy, not the
/// cost of a scan, so the engine work either side of it is made as near to nothing as a real public
/// call can be: the string and span rows then differ by the copy and by nothing else, and the
/// difference across the three sizes is the per-byte cost. A full-scan pattern would bury a
/// hundred-microsecond copy under a multi-millisecond scan, which is how this measurement gets
/// taken and reported as "free".
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class SpanOverloadBenchmarks
{
    /// <summary>Matches at index 0 of every corpus subject, so almost no engine work happens.</summary>
    private static readonly FuzzyRegex _atStart = new("the quick");

    /// <summary>The string overload on a nineteen-character subject.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark(Baseline = true)]
    public bool StringShort() => _atStart.IsMatch(Corpus.Short);

    /// <summary>The span overload on the same short subject: one copy of 63 characters.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool SpanShort() => _atStart.IsMatch(Corpus.Short.AsSpan());

    /// <summary>The string overload on a kilobyte.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool StringKilobyte() => _atStart.IsMatch(Corpus.Kilobyte);

    /// <summary>The span overload on a kilobyte: one copy of about 1,047 characters.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool SpanKilobyte() => _atStart.IsMatch(Corpus.Kilobyte.AsSpan());

    /// <summary>The string overload on a megabyte.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool StringMegabyte() => _atStart.IsMatch(Corpus.Long);

    /// <summary>The span overload on a megabyte: one copy of about a million characters.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool SpanMegabyte() => _atStart.IsMatch(Corpus.Long.AsSpan());

    /// <summary>
    /// The other span overload, over a megabyte, where the scan is real. The pair with
    /// <see cref="CountSpanMegabyte"/> is what a caller who already has a span actually pays.
    /// </summary>
    /// <returns>How many matches there were.</returns>
    [Benchmark]
    public int CountStringMegabyte() => _atStart.Count(Corpus.Long);

    /// <summary>The span form of <see cref="CountStringMegabyte"/>.</summary>
    /// <returns>How many matches there were.</returns>
    [Benchmark]
    public int CountSpanMegabyte() => _atStart.Count(Corpus.Long.AsSpan());
}
