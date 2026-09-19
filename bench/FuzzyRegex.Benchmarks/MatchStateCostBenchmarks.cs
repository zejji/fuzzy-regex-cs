using System.Text;
using BenchmarkDotNet.Attributes;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// The measurement behind S58's lazy-walk decision: what one <c>MatchState</c> costs, split into
/// the part that scales with the pattern's group count and the part that scales with the subject.
/// </summary>
/// <remarks>
/// <para>
/// <c>Iteration.Walk</c> builds one state per step rather than one per walk, deliberately - a state
/// owns rented buffers and a state held across a <c>yield return</c> is one an abandoned iterator
/// never returns. The cost is a per-step charge proportional to the subject, which is what makes a
/// full lazy walk quadratic (<see cref="WorkloadBenchmarks.EnumerateMatchesToEndDense"/> measures
/// the consequence; these measure the cause).
/// </para>
/// <para>
/// <b>Why two sweeps rather than one number, and why two classes rather than two
/// <c>[Params]</c>.</b> <c>MatchState.Create</c> allocates a <c>GroupData[]</c> and a
/// <c>RepeatData[]</c> sized by the pattern, and buffers sized by the subject; a single figure for
/// "a state" cannot be projected onto a different pattern or a different subject, so the two are
/// varied separately and the bytes read off as a slope and an intercept. Two <c>[Params]</c> on one
/// class would make BenchmarkDotNet run their cross product, paying for twenty-four combinations to
/// learn ten numbers.
/// </para>
/// <para>
/// That arithmetic is what the decision document needs: pooling a state on <c>Dispose</c> saves the
/// intercept every step, and a <c>ref struct</c> enumerator saves it without the disposal contract
/// but is not an <c>IEnumerable&lt;T&gt;</c>.
/// </para>
/// </remarks>
internal static class MatchStateCost
{
    /// <summary>The group counts both the pattern builder and the sweep use.</summary>
    internal static readonly int[] GroupCounts = [1, 2, 4, 8, 16, 32];

    /// <summary>The subject lengths both the subject builder and the sweep use.</summary>
    internal static readonly int[] SubjectLengths = [64, 1024, 16384, 262144];
}

/// <summary>
/// The group-count half of the <c>MatchState</c> cost sweep. See <see cref="MatchStateCost"/>.
/// </summary>
[MemoryDiagnoser]
public class GroupCountStateBenchmarks
{
    /// <summary>One pattern per group count in the sweep, compiled once.</summary>
    private static readonly Dictionary<int, FuzzyRegex> _byGroups = Build();

    /// <summary>How many nested capturing groups the pattern has.</summary>
    [ParamsSource(nameof(Counts))]
    public int Groups { get; set; }

    /// <summary>The group counts swept.</summary>
    public static IEnumerable<int> Counts => MatchStateCost.GroupCounts;

    /// <summary>
    /// One match with <see cref="Groups"/> capturing groups over a fixed short subject, matching at
    /// index 0: the bytes here, against the group count, are <c>GroupData[]</c>'s slope, and the
    /// intercept is everything in a state that the pattern does not size.
    /// </summary>
    /// <returns>Where the match started.</returns>
    [Benchmark]
    public int StateByGroupCount() => _byGroups[Groups].Match(Corpus.Short).Index;

    /// <summary>Compiles one nested-group pattern per group count.</summary>
    /// <returns>The patterns, keyed by group count.</returns>
    private static Dictionary<int, FuzzyRegex> Build()
    {
        Dictionary<int, FuzzyRegex> patterns = [];
        foreach (int count in MatchStateCost.GroupCounts)
        {
            // Nested rather than sequential: every group captures the same span, so the matching
            // work stays the same shape and the group count is the only thing that varies.
            StringBuilder builder = new();
            builder.Append('(', count).Append(@"\w+").Append(')', count);
            patterns[count] = new FuzzyRegex(builder.ToString());
        }

        return patterns;
    }
}

/// <summary>
/// The subject-length half of the <c>MatchState</c> cost sweep. See <see cref="MatchStateCost"/>.
/// </summary>
[MemoryDiagnoser]
public class SubjectLengthStateBenchmarks
{
    /// <summary>A single capturing group, so only the subject varies.</summary>
    private static readonly FuzzyRegex _oneGroup = new(@"(\w+)");

    /// <summary>One subject per length in the sweep, sliced once.</summary>
    private static readonly Dictionary<int, string> _bySubjectLength = Build();

    /// <summary>How long the subject is, in UTF-16 code units.</summary>
    [ParamsSource(nameof(Lengths))]
    public int SubjectLength { get; set; }

    /// <summary>The subject lengths swept.</summary>
    public static IEnumerable<int> Lengths => MatchStateCost.SubjectLengths;

    /// <summary>
    /// One match with a fixed one-group pattern over a subject of <see cref="SubjectLength"/>,
    /// matching at index 0: the bytes here, against the length, are the subject-sized buffers'
    /// slope and the vectorised pass.
    /// </summary>
    /// <returns>Where the match started.</returns>
    [Benchmark]
    public int StateBySubjectLength() => _oneGroup.Match(_bySubjectLength[SubjectLength]).Index;

    /// <summary>Slices one subject per length in the sweep.</summary>
    /// <returns>The subjects, keyed by length.</returns>
    private static Dictionary<int, string> Build()
    {
        Dictionary<int, string> subjects = [];
        foreach (int length in MatchStateCost.SubjectLengths)
        {
            subjects[length] = Corpus.Long[..length];
        }

        return subjects;
    }
}
