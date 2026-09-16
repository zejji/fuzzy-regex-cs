using BenchmarkDotNet.Attributes;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// The v1.0 workload suite: one named benchmark per workload the <c>benchmark</c> skill and design
/// spec section 11 list, measured over the shared <see cref="Corpus"/>.
/// </summary>
/// <remarks>
/// <para>
/// These are the numbers Phase 7 regresses against, so the shapes are chosen to be the ones an
/// optimiser moves rather than the ones that read well. Backtracking-heavy work is deliberately
/// NOT repeated here: <see cref="ReferenceBenchmarks.BacktrackingPort"/> measures it on S51's exact
/// pattern and subject, and a second copy would only be a second thing to keep in step. S51's own
/// <c>MatchingBenchmarks</c> was deleted into this suite for the same reason - it was three shapes
/// written for one before/after on the cancellation check, which is done.
/// </para>
/// <para>
/// Every benchmark returns a value. BenchmarkDotNet consumes a returned value and cannot consume a
/// discarded one, so a <c>void</c> benchmark whose work has no side effect is free to be optimised
/// away entirely - which is how a benchmark suite comes to report a nanosecond for a megabyte scan.
/// </para>
/// <para>
/// Run the suite: <c>dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- --filter '*'</c>.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class WorkloadBenchmarks
{
    /// <summary>A bare literal. The corpus carries exactly one, at the end, so this is a full scan.</summary>
    private static readonly FuzzyRegex _literal = new("needle");

    /// <summary>Character-class-heavy: two classes and a counted repeat, matching densely.</summary>
    private static readonly FuzzyRegex _classHeavy = new("[a-z]{3}[^a-z]");

    /// <summary>Case-folded, with full case folding on, which is where the fold tables get hit.</summary>
    private static readonly FuzzyRegex _caseFolded = new(
        "QUICK",
        FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase
    );

    /// <summary>A reversed search that never matches, so it walks the whole subject backwards.</summary>
    private static readonly FuzzyRegex _reverse = new("zebra", FuzzyRegexOptions.RightToLeft);

    /// <summary>The pattern whose strict prefix ends <see cref="Corpus.LongPartial"/>.</summary>
    private static readonly FuzzyRegex _partial = new(@"a needle in a haystack\.");

    /// <summary>A scan pattern with many short matches, for the eager and lazy walk pair.</summary>
    private static readonly FuzzyRegex _words = new(@"\w+");

    /// <summary>A two-group template substitution.</summary>
    private static readonly FuzzyRegex _pairs = new(@"(\w+) (\w+)");

    /// <summary>One error allowed: the cheapest fuzzy shape there is.</summary>
    private static readonly FuzzyRegex _fuzzyOne = new("(?:needle){e<=1}");

    /// <summary>Three errors allowed, on a longer literal: the budget is what is varied.</summary>
    private static readonly FuzzyRegex _fuzzyThree = new("(?:haystack){e<=3}");

    /// <summary>The same budget under <c>ENHANCEMATCH</c>, which re-runs to reduce the errors.</summary>
    private static readonly FuzzyRegex _enhance = new("(?e)(?:haystack){e<=3}");

    /// <summary>
    /// The same budget under <c>BESTMATCH</c>, which explores the whole space rather than taking
    /// the first answer. The skill names it as the likeliest place to regress.
    /// </summary>
    private static readonly FuzzyRegex _best = new("(?b)(?:haystack){e<=3}");

    /// <summary>Full scan of a megabyte for a literal that occurs once, at the end.</summary>
    /// <returns>Where it was found.</returns>
    [Benchmark]
    public int LiteralMatch() => _literal.Match(Corpus.Long).Index;

    /// <summary>Counts every match of a class-heavy pattern over a megabyte.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int ClassScan() => _classHeavy.Count(Corpus.Long);

    /// <summary>Counts every case-folded match over a megabyte.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int CaseFoldedScan() => _caseFolded.Count(Corpus.Long);

    /// <summary>Walks a megabyte backwards for a literal that is not there.</summary>
    /// <returns>Whether it matched, which it does not.</returns>
    [Benchmark]
    public bool ReverseFailedScan() => _reverse.IsMatch(Corpus.Long);

    /// <summary>Scans a megabyte for a pattern only the last few characters can start.</summary>
    /// <returns>Where the partial match begins.</returns>
    [Benchmark]
    public int PartialMatch() => _partial.Match(Corpus.LongPartial, partial: true).Index;

    /// <summary>The eager walk: every match of <c>\w+</c> over a megabyte, materialised.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int MatchesToEnd() => _words.Matches(Corpus.Long).Count;

    /// <summary>The eager walk over a hundred kilobytes, the twin of <see cref="EnumerateMatchesToEndDense"/>.</summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int MatchesToEndDense() => _words.Matches(Corpus.Dense).Count;

    /// <summary>
    /// The lazy walk drained to the end - at a hundred kilobytes, not a megabyte. The per-step
    /// engine state makes this quadratic in the subject: measured 2026-09-16 with the
    /// <c>sizing</c> mode, 117 ms here and 12,643 ms over the megabyte, against 4.51 ms and 111 ms
    /// for the eager walk. A twelve-second benchmark is one nobody re-runs, so the megabyte case is
    /// measured only where the walk stops early (<see cref="EnumerateMatchesFirstTwo"/>).
    /// </summary>
    /// <returns>How many there were.</returns>
    [Benchmark]
    public int EnumerateMatchesToEndDense() => _words.EnumerateMatches(Corpus.Dense).Count();

    /// <summary>
    /// The eager walk read two matches deep. It still costs the whole megabyte, because
    /// <see cref="MatchCollection"/> is materialised before the caller sees an element; the pair
    /// with <see cref="EnumerateMatchesFirstTwo"/> is what laziness buys.
    /// </summary>
    /// <returns>Where the second match starts.</returns>
    [Benchmark]
    public int MatchesFirstTwo() => _words.Matches(Corpus.Long)[1].Index;

    /// <summary>The lazy walk read two matches deep, which should stop after two matches.</summary>
    /// <returns>Where the second match starts.</returns>
    [Benchmark]
    public int EnumerateMatchesFirstTwo() => _words.EnumerateMatches(Corpus.Long).Take(2).Last().Index;

    /// <summary>
    /// Template substitution with two group references over a megabyte. The template is upstream's
    /// <c>\2 \1</c>, which is what <c>Replace</c> takes; <c>$2 $1</c> is <c>ReplaceFormat</c>'s
    /// syntax and would be substituted here as a five-character literal, measuring the wrong work.
    /// </summary>
    /// <returns>The length of the result, which is what stops it being discarded.</returns>
    [Benchmark]
    public int ReplaceTemplate() => _pairs.Replace(Corpus.Long, @"\2 \1").Length;

    /// <summary>Splits a megabyte on whitespace.</summary>
    /// <returns>How many pieces there were.</returns>
    [Benchmark]
    public int SplitLong() => _words.Split(Corpus.Long).Length;

    /// <summary>A fuzzy match with one allowed error, on a short subject.</summary>
    /// <returns>Where it was found.</returns>
    [Benchmark]
    public int FuzzyShort() => _fuzzyOne.Match(Corpus.Short).Index;

    /// <summary>The same fuzzy pattern over a megabyte, which is the fuzzy scan cost.</summary>
    /// <returns>Where it was found.</returns>
    [Benchmark]
    public int FuzzyLong() => _fuzzyOne.Match(Corpus.Long).Index;

    /// <summary>A wider error budget on a short subject.</summary>
    /// <returns>Where it was found.</returns>
    [Benchmark]
    public int FuzzyBudgetThree() => _fuzzyThree.Match(Corpus.Short).Index;

    /// <summary>The same budget under <c>ENHANCEMATCH</c>.</summary>
    /// <returns>How many errors the answer carries.</returns>
    [Benchmark]
    public int EnhanceMatch() => _enhance.Match(Corpus.Short).FuzzyCounts.Total;

    /// <summary>The same budget under <c>BESTMATCH</c>.</summary>
    /// <returns>How many errors the answer carries.</returns>
    [Benchmark]
    public int BestMatch() => _best.Match(Corpus.Short).FuzzyCounts.Total;

    /// <summary>Compiles a large pattern from source, which is parser and compiler work only.</summary>
    /// <returns>How many capture groups it has, so the result cannot be discarded.</returns>
    [Benchmark]
    public int CompileLargePattern() => new FuzzyRegex(Corpus.LargePattern).GroupNumbers.Count;
}
