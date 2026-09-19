using BenchmarkDotNet.Attributes;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// What S59's pattern cache is worth: the static conveniences used to compile their pattern on
/// every call, and now look it up.
/// </summary>
/// <remarks>
/// <para>
/// <b>The before figures come from this run, not from a second one.</b> Each cached row has an
/// <c>Uncached</c> partner whose body is the one the static convenience had at commit b6e82db -
/// quoted, with its line number, on each of those three rows - so the pair is measured in one
/// process, on one machine, in one session, minutes rather than an hour apart. A two-tree run
/// would have measured the same two bodies with a whole build and a whole set of run-to-run
/// conditions between them.
/// </para>
/// <para>
/// Nothing here names <c>FuzzyRegex.CacheSize</c> or any other member S59 added, so the file also
/// compiles against the tree before it; the miss row reaches its worst case by cycling more
/// distinct patterns than the default bound rather than by setting the bound.
/// </para>
/// <para>
/// The subjects are short and the patterns match early on purpose. The question is what a CALL
/// costs, not what a scan costs, and a megabyte subject would bury the whole compile under it -
/// which is exactly how an optimisation this size gets measured and reported as noise. The
/// instance rows are the control: S59 changed no engine code and no constructor, so a move in one
/// of those is the machine moving, not the cache.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class PatternCacheBenchmarks
{
    /// <summary>
    /// The one pattern the hit rows use. Ordinary rather than trivial - a group, a class and a
    /// quantifier - because a one-literal pattern compiles so fast that the row measures the
    /// dictionary lookup against almost nothing.
    /// </summary>
    private const string _pattern = @"(quick|slow)\s+(?<colour>[a-z]+)\s+fox";

    /// <summary>A fuzzy pattern, whose compile does more work than the literal one's.</summary>
    private const string _fuzzyPattern = @"(?:needle){e<=1}";

    /// <summary>
    /// Twenty distinct patterns, cycled one per call. Twenty because the cache holds fifteen, and a
    /// cycle longer than the bound is the case an MRU cache is worst at: every call evicts the
    /// entry it is about to ask for next, so every call misses and every call compiles. This row is
    /// the cost of the cache when it never helps, and it is the row a regression would show in.
    /// </summary>
    private static readonly string[] _cycle =
    [
        .. Enumerable.Range(0, 20).Select(static index => $@"(quick|slow)\s+(?<c{index}>[a-z]+)\s+fox"),
    ];

    /// <summary>The instance the control rows use, compiled once outside the measurement.</summary>
    private static readonly FuzzyRegex _compiled = new(_pattern);

    /// <summary>The fuzzy instance, likewise.</summary>
    private static readonly FuzzyRegex _compiledFuzzy = new(_fuzzyPattern);

    /// <summary>
    /// The named lists a convenience can carry. One entry, because the row is about which path the
    /// call takes and not about the dictionary's size.
    /// </summary>
    private static readonly Dictionary<string, IReadOnlyCollection<string>> _namedLists = new(StringComparer.Ordinal)
    {
        ["colour"] = ["brown", "grey"],
    };

    /// <summary>Which pattern of <see cref="_cycle"/> the next miss row call takes.</summary>
    private int _next;

    /// <summary>
    /// The row the slice exists for: one static call, the same pattern every time. Before S59 this
    /// compiled the pattern; after it, it hashes a key and matches.
    /// </summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark(Baseline = true)]
    public bool StaticIsMatch() => FuzzyRegex.IsMatch(Corpus.Short, _pattern);

    /// <summary>
    /// The SAME CALL AS IT WAS BEFORE S59, so the before and after figures come out of one run of
    /// one process rather than two runs of two trees. The body is the one commit b6e82db compiled,
    /// copied out of <c>git show HEAD:src/FuzzyRegex/FuzzyRegex.cs</c> at line 1497:
    /// <code>
    /// ) => new FuzzyRegex(pattern, options).IsMatch(input, timeout: timeout, cancellationToken: cancellationToken);
    /// </code>
    /// with the two arguments that were <see langword="null"/> and <see langword="default"/> left
    /// off, because the overload's defaults are exactly those. <see cref="StaticIsMatch"/> divided
    /// by this row is what the cache bought on this workload.
    /// </summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool UncachedIsMatch() => new FuzzyRegex(_pattern).IsMatch(Corpus.Short);

    /// <summary>The same call on an instance, which S59 did not touch. The control.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool InstanceIsMatch() => _compiled.IsMatch(Corpus.Short);

    /// <summary>The same question for a fuzzy pattern, whose compile costs more.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool StaticFuzzyIsMatch() => FuzzyRegex.IsMatch(Corpus.Short, _fuzzyPattern);

    /// <summary>The fuzzy pattern's before row, the same shape as <see cref="UncachedIsMatch"/>.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool UncachedFuzzyIsMatch() => new FuzzyRegex(_fuzzyPattern).IsMatch(Corpus.Short);

    /// <summary>The fuzzy control.</summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool InstanceFuzzyIsMatch() => _compiledFuzzy.IsMatch(Corpus.Short);

    /// <summary>
    /// A static call that returns a <see cref="Match"/> rather than a flag, so the row includes the
    /// work the conveniences do after the lookup as well as the lookup.
    /// </summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool StaticMatch() => FuzzyRegex.Match(Corpus.Short, _pattern).Success;

    /// <summary>
    /// <see cref="StaticMatch"/>'s before row. HEAD's body at line 1518 was
    /// <c>new FuzzyRegex(pattern, options, InfiniteMatchTimeout, namedLists).Match(...)</c>, and
    /// with no named lists that is this.
    /// </summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool UncachedMatch() =>
        new FuzzyRegex(_pattern, FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, null)
            .Match(Corpus.Short)
            .Success;

    /// <summary>
    /// The miss row: twenty patterns cycled past a fifteen-entry cache, so nothing is ever found.
    /// A cache that costs nothing when it misses leaves this row on its before figure.
    /// </summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool StaticIsMatchAlwaysMissing()
    {
        _next = (_next + 1) % _cycle.Length;
        return FuzzyRegex.IsMatch(Corpus.Short, _cycle[_next]);
    }

    /// <summary>
    /// A convenience carrying a caller-supplied named-lists dictionary, which S59 deliberately
    /// routes AROUND the cache - the dictionary is the caller's and it may change it between calls.
    /// This row must not move: it still compiles per call, before and after. It is
    /// <c>Match</c> rather than <c>IsMatch</c> because <c>IsMatch</c> has no named-lists overload.
    /// </summary>
    /// <returns>Whether it matched.</returns>
    [Benchmark]
    public bool StaticMatchWithNamedLists() =>
        FuzzyRegex.Match(Corpus.Short, @"(quick|slow)\s+\L<colour>\s+fox", FuzzyRegexOptions.None, _namedLists).Success;
}
