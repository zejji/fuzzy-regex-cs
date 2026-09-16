using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// This port beside <see cref="Regex"/>, interpreted and <see cref="RegexOptions.Compiled"/>, on
/// the exact subset of the workload suite the built-in engine can express.
/// </summary>
/// <remarks>
/// <para>
/// Not part of the v1.0 gate - that is measured against Python <c>regex</c> - but it is the number
/// a .NET user will actually compare against, so it is measured and committed with the rest.
/// Four workloads, three engines each, same patterns and the same <see cref="Corpus"/>.
/// </para>
/// <para>
/// <b>This is a speed comparison and not a parity claim.</b> <c>\w</c> means different things to
/// the two engines and the corpus is ASCII precisely so that the difference cannot change how much
/// work either of them does. Nothing here asserts an answer; the assertions live in the test suite.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class ReferenceBenchmarks
{
    /// <summary>The literal that occurs once, at the end of the corpus.</summary>
    private const string _literalPattern = "needle";

    /// <summary>The class-heavy pattern.</summary>
    private const string _classPattern = "[a-z]{3}[^a-z]";

    /// <summary>The template substitution's pattern.</summary>
    private const string _pairsPattern = @"(\w+) (\w+)";

    /// <summary>The exponential-backtracking pattern, on a subject with no <c>b</c>.</summary>
    private const string _backtrackingPattern = "(a|a)*b";

    /// <summary>The subject for <see cref="_backtrackingPattern"/>: eighteen characters, no match.</summary>
    private static readonly string _backtrackingSubject = new('a', 18);

    private static readonly FuzzyRegex _portLiteral = new(_literalPattern);
    private static readonly FuzzyRegex _portClass = new(_classPattern);
    private static readonly FuzzyRegex _portPairs = new(_pairsPattern);
    private static readonly FuzzyRegex _portBacktracking = new(_backtrackingPattern);

    private static readonly Regex _bclLiteral = new(_literalPattern, RegexOptions.None);
    private static readonly Regex _bclClass = new(_classPattern, RegexOptions.None);
    private static readonly Regex _bclPairs = new(_pairsPattern, RegexOptions.None);
    private static readonly Regex _bclBacktracking = new(_backtrackingPattern, RegexOptions.None);

    private static readonly Regex _compiledLiteral = new(_literalPattern, RegexOptions.Compiled);
    private static readonly Regex _compiledClass = new(_classPattern, RegexOptions.Compiled);
    private static readonly Regex _compiledPairs = new(_pairsPattern, RegexOptions.Compiled);
    private static readonly Regex _compiledBacktracking = new(_backtrackingPattern, RegexOptions.Compiled);

    /// <summary>Literal search over a megabyte, this port.</summary>
    /// <returns>Where it was found.</returns>
    [Benchmark]
    public int LiteralPort() => _portLiteral.Match(Corpus.Long).Index;

    /// <summary>Literal search over a megabyte, interpreted <see cref="Regex"/>.</summary>
    /// <returns>Where it was found.</returns>
    [Benchmark]
    public int LiteralBcl() => _bclLiteral.Match(Corpus.Long).Index;

    /// <summary>Literal search over a megabyte, compiled <see cref="Regex"/>.</summary>
    /// <returns>Where it was found.</returns>
    [Benchmark]
    public int LiteralBclCompiled() => _compiledLiteral.Match(Corpus.Long).Index;

    /// <summary>Class-heavy scan over a megabyte, this port.</summary>
    /// <returns>How many matches there were.</returns>
    [Benchmark]
    public int ClassScanPort() => _portClass.Count(Corpus.Long);

    /// <summary>Class-heavy scan over a megabyte, interpreted <see cref="Regex"/>.</summary>
    /// <returns>How many matches there were.</returns>
    [Benchmark]
    public int ClassScanBcl() => _bclClass.Count(Corpus.Long);

    /// <summary>Class-heavy scan over a megabyte, compiled <see cref="Regex"/>.</summary>
    /// <returns>How many matches there were.</returns>
    [Benchmark]
    public int ClassScanBclCompiled() => _compiledClass.Count(Corpus.Long);

    /// <summary>
    /// Template substitution over a megabyte, this port. The template differs from the two below
    /// because the syntaxes do: <c>Replace</c> takes upstream's <c>\2 \1</c> and <see cref="Regex"/>
    /// takes .NET's <c>$2 $1</c>. Same pattern, same subject, same swap, same output length.
    /// </summary>
    /// <returns>The length of the result.</returns>
    [Benchmark]
    public int ReplacePort() => _portPairs.Replace(Corpus.Long, @"\2 \1").Length;

    /// <summary>Template substitution over a megabyte, interpreted <see cref="Regex"/>.</summary>
    /// <returns>The length of the result.</returns>
    [Benchmark]
    public int ReplaceBcl() => _bclPairs.Replace(Corpus.Long, "$2 $1").Length;

    /// <summary>Template substitution over a megabyte, compiled <see cref="Regex"/>.</summary>
    /// <returns>The length of the result.</returns>
    [Benchmark]
    public int ReplaceBclCompiled() => _compiledPairs.Replace(Corpus.Long, "$2 $1").Length;

    /// <summary>Exponential backtracking to exhaustion, this port.</summary>
    /// <returns>Whether it matched, which it does not.</returns>
    [Benchmark]
    public bool BacktrackingPort() => _portBacktracking.IsMatch(_backtrackingSubject);

    /// <summary>Exponential backtracking to exhaustion, interpreted <see cref="Regex"/>.</summary>
    /// <returns>Whether it matched, which it does not.</returns>
    [Benchmark]
    public bool BacktrackingBcl() => _bclBacktracking.IsMatch(_backtrackingSubject);

    /// <summary>Exponential backtracking to exhaustion, compiled <see cref="Regex"/>.</summary>
    /// <returns>Whether it matched, which it does not.</returns>
    [Benchmark]
    public bool BacktrackingBclCompiled() => _compiledBacktracking.IsMatch(_backtrackingSubject);
}
