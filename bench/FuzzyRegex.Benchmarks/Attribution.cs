using Fuzzy.Text.RegularExpressions.Engine;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// The arithmetic allocation attribution S58 falls back on, measured rather than argued. Not a
/// benchmark: run it, read it, quote it.
/// </summary>
/// <remarks>
/// <para>
/// Run: <c>dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- attribution</c>.
/// </para>
/// <para>
/// <b>The three levels, and why <c>IsMatch</c> is one of them.</b> <c>Create</c> is the state
/// alone; <c>IsMatch</c> is a whole <c>Run</c> whose result is a <see cref="bool"/>; <c>Match</c>
/// is the same <c>Run</c> with the <c>Match</c> handed back. Measured 2026-09-19, the second and
/// third are equal to the byte at every group count, because <c>IsMatch</c> is
/// <c>Run(...).Success</c> (<c>FuzzyRegex.cs:444</c>) and <c>Run</c> passes
/// <c>visibleCaptures: true</c> unconditionally (<c>:568</c>) - so a predicate call pays for every
/// capture it will never be asked for. That is a Phase 7 finding, not a measurement artefact; see
/// <c>docs/plan/OPTIMISATION-NOTES.md</c>.
/// </para>
/// <para>
/// <b>Why this exists.</b> <c>PROFILING.md</c> section 7's route 2 - capture an allocation profile
/// and read it - is struck through: nothing installed can attribute a captured .NET allocation
/// trace to a source site (S58 sitting 3, <c>phase7-research/profiles/README.md</c>). The fallback
/// is <c>MemoryDiagnoser</c> plus arithmetic: vary one term of the input, subtract, and the
/// difference is that term's slope. The suite's two sweeps
/// (<see cref="GroupCountStateBenchmarks"/>, <see cref="SubjectLengthStateBenchmarks"/>) give the
/// slope of a whole <c>Match</c> call; they cannot say how much of it is
/// <c>MatchState.Create</c>'s two pattern-sized allocations, because a benchmark cannot call an
/// internal. This can - <c>FuzzyRegex.Benchmarks</c> is in <c>InternalsVisibleTo</c> - so it runs
/// the same sweeps twice, once through <c>Match</c> and once through <c>MatchState.Create</c>
/// alone, and the difference between the two slopes is everything in a match that is not the
/// state.
/// </para>
/// <para>
/// <b>Why <c>GC.GetAllocatedBytesForCurrentThread</c> and not <c>MemoryDiagnoser</c>.</b> It is the
/// same counter BenchmarkDotNet's diagnoser reads, without the harness: exact, per-thread, and
/// readable around a single call. Allocation on these paths is deterministic, which this run
/// demonstrates rather than assumes - every figure is printed as N repeats and the run says so if
/// they ever disagree.
/// </para>
/// <para>
/// <b>The pooling caveat this run exists to expose.</b> A steady-state figure of zero has two
/// explanations: nothing is allocated, or something is rented from
/// <see cref="System.Buffers.ArrayPool{T}"/> and returned. <c>MatchState.Dispose</c> returns three
/// <c>ByteStack</c> buffers, so the second explanation is live, and a sweep that reported only
/// steady figures could not tell them apart. Every measurement here is therefore printed twice:
/// its <b>first</b> call and its <b>steady</b> call.
/// </para>
/// <para>
/// <b>What "first" does and does not mean, and why only two levels report one.</b> A first-call
/// figure is only honest for the level that reaches a code path first. Within a row this run
/// measures <c>Create</c>, then <c>IsMatch</c>, then <c>Match</c>; <c>IsMatch</c> and <c>Match</c>
/// are the same <c>Run</c>, so by the time <c>Match</c> is sampled that path has already been
/// walked nine times by <see cref="Measure"/> and its "first" call is the tenth. Measured
/// 2026-09-19, the difference is not small: this run's genuinely-first <c>Run</c>, the one-group
/// row of the group sweep, costs 11,848 B against a steady 1,392 B. So <c>Create</c> and
/// <c>IsMatch</c> report a first figure - each is the first call of its own path for that pattern
/// and subject - and <c>Match</c> does not.
/// </para>
/// <para>
/// Even those two are <i>not</i> calls against an empty pool, because
/// <see cref="System.Buffers.ArrayPool{T}.Shared"/> is process-wide: only the earliest rows of the
/// first sweep see a pool this process has not already filled. So a large first figure is evidence
/// of one-time cost, and a first figure equal to the steady one is evidence there is none; the
/// shape of the first column across rows is not a per-row pooling measurement, and is not read as
/// one.
/// </para>
/// </remarks>
internal static class Attribution
{
    /// <summary>How many times each figure is re-measured to show it is deterministic.</summary>
    private const int _repeats = 5;

    /// <summary>Runs every attribution measurement and prints it.</summary>
    public static void Run()
    {
        Console.WriteLine($"# Allocation attribution, {DateTime.Now:yyyy-MM-dd HH:mm}");
        Console.WriteLine($"# {_repeats} repeats per figure; a disagreement between repeats prints a '!' line.");

        GroupCountSweep();
        SubjectLengthSweep();
    }

    /// <summary>
    /// Allocated bytes against the pattern's group count, through <c>Match</c> and through
    /// <c>MatchState.Create</c> alone. The slope of the second is
    /// <c>MatchState.Create</c>'s <c>GroupData[]</c> and <c>RepeatData[]</c>; the gap between the
    /// slopes is the rest of a match.
    /// </summary>
    private static void GroupCountSweep()
    {
        Console.WriteLine("\n## Bytes by group count, over a fixed short subject");
        Console.WriteLine("groups |     Create |    IsMatch |      Match | (first Create, first IsMatch)");

        long previousMatch = 0;
        long previousIsMatch = 0;
        long previousCreate = 0;
        int previousCount = 0;

        foreach (int count in MatchStateCost.GroupCounts)
        {
            var regex = new FuzzyRegex(NestedGroups(count));
            string subject = Corpus.Short;

            (long create, long firstCreate) = Measure(() => CreateAndDispose(regex, subject));
            (long isMatch, long firstIsMatch) = Measure(() => regex.IsMatch(subject) ? 1 : 0);
            (long match, _) = Measure(() => regex.Match(subject).Index);

            Console.WriteLine(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0,6} | {1,10:N0} | {2,10:N0} | {3,10:N0} | ({4:N0}, {5:N0})",
                    count,
                    create,
                    isMatch,
                    match,
                    firstCreate,
                    firstIsMatch
                )
            );

            if (previousCount > 0)
            {
                int step = count - previousCount;
                Console.WriteLine(
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "       | slope {0,2}->{1,-2} B/group: state {2,6:N2} + rest of Run {3,6:N2} + Match over IsMatch {4,6:N2} = {5,6:N2}",
                        previousCount,
                        count,
                        (double)(create - previousCreate) / step,
                        (double)(isMatch - previousIsMatch - (create - previousCreate)) / step,
                        (double)(match - previousMatch - (isMatch - previousIsMatch)) / step,
                        (double)(match - previousMatch) / step
                    )
                );
            }

            previousMatch = match;
            previousIsMatch = isMatch;
            previousCreate = create;
            previousCount = count;
        }
    }

    /// <summary>
    /// Allocated bytes against the subject's length, through <c>Match</c> and through
    /// <c>MatchState.Create</c> alone. A flat warm column with a cold column that is not flat is
    /// the pool at work, not an absence of allocation.
    /// </summary>
    private static void SubjectLengthSweep()
    {
        Console.WriteLine("\n## Bytes by subject length, with a fixed one-group pattern");
        Console.WriteLine(" length |     Create |    IsMatch |      Match | (first Create, first IsMatch)");

        var regex = new FuzzyRegex(@"(\w+)");

        foreach (int length in MatchStateCost.SubjectLengths)
        {
            string subject = Corpus.Long[..length];

            (long create, long firstCreate) = Measure(() => CreateAndDispose(regex, subject));
            (long isMatch, long firstIsMatch) = Measure(() => regex.IsMatch(subject) ? 1 : 0);
            (long match, _) = Measure(() => regex.Match(subject).Index);

            Console.WriteLine(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0,7} | {1,10:N0} | {2,10:N0} | {3,10:N0} | ({4:N0}, {5:N0})",
                    length,
                    create,
                    isMatch,
                    match,
                    firstCreate,
                    firstIsMatch
                )
            );
        }
    }

    /// <summary>
    /// Builds a pattern with <paramref name="count"/> nested capturing groups - the same shape
    /// <see cref="GroupCountStateBenchmarks"/> sweeps, so the two runs are comparable.
    /// </summary>
    /// <param name="count">How many nested groups.</param>
    /// <returns>The pattern source.</returns>
    private static string NestedGroups(int count)
    {
        System.Text.StringBuilder builder = new();
        builder.Append('(', count).Append(@"\w+").Append(')', count);
        return builder.ToString();
    }

    /// <summary>
    /// Creates a match state exactly as <c>FuzzyRegex.Match</c> does and disposes it, doing none of
    /// the matching. This is the isolated cost of the two allocations at
    /// <c>MatchState.cs:544</c> and <c>:554</c> plus the state object itself.
    /// </summary>
    /// <param name="regex">The compiled pattern.</param>
    /// <param name="subject">The subject.</param>
    /// <returns>The state's text length, so the call cannot be elided.</returns>
    private static int CreateAndDispose(FuzzyRegex regex, string subject)
    {
        using var state = MatchState.Create(
            regex.PatternObject,
            subject,
            0,
            subject.Length,
            overlapped: false,
            partial: false,
            visibleCaptures: true,
            matchAll: false,
            regex.PatternLimits
        );

        return state.TextLength;
    }

    /// <summary>
    /// Measures an operation's allocation on its first call and in steady state.
    /// </summary>
    /// <param name="operation">The operation, returning a value so it cannot be optimised away.</param>
    /// <returns>The steady figure and the first-call figure, in bytes.</returns>
    private static (long Steady, long First) Measure(Func<int> operation)
    {
        long first = Sample(operation);

        for (int i = 0; i < 3; i++)
        {
            operation();
        }

        long steady = Sample(operation);
        for (int i = 1; i < _repeats; i++)
        {
            long again = Sample(operation);
            if (again != steady)
            {
                // Printed rather than hidden: the arithmetic below is only meaningful if these
                // paths allocate deterministically, so a disagreement is the headline, not a nit.
                Console.WriteLine($"       ! steady figure varied: {steady:N0} then {again:N0}");
                steady = Math.Max(steady, again);
            }
        }

        return (steady, first);
    }

    /// <summary>Allocated bytes for one call of <paramref name="operation"/> on this thread.</summary>
    /// <param name="operation">The operation.</param>
    /// <returns>The bytes.</returns>
    private static long Sample(Func<int> operation)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        operation();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
