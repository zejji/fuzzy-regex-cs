using System.Diagnostics;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// One-shot stopwatch measurements that decide how the benchmark suite and the S54 optimiser-trap
/// tests are SIZED. Not a benchmark: run it, read it, size against it.
/// </summary>
/// <remarks>
/// <para>
/// Run: <c>dotnet run -c Release --project bench/FuzzyRegex.Benchmarks -- sizing</c> (from anywhere;
/// this path does not go near BenchmarkDotNet, so the working-directory rule in
/// <c>bench/FuzzyRegex.Benchmarks.slnx</c> does not apply).
/// </para>
/// <para>
/// It answers two questions a benchmark suite cannot afford to answer by running: how expensive
/// the lazy walk's per-step state is at each subject size, and which of the two classic
/// catastrophic-backtracking shapes is actually catastrophic in this port. A case that costs tens
/// of seconds belongs in neither the suite nor the test run, and the only way to know that is to
/// time it once.
/// </para>
/// </remarks>
internal static class Sizing
{
    /// <summary>Runs every sizing measurement and prints it.</summary>
    public static void Run()
    {
        Console.WriteLine($"# Sizing run, {DateTime.Now:yyyy-MM-dd HH:mm}");
        Console.WriteLine(
            $"# LONG={Corpus.Long.Length} chars, DENSE={Corpus.Dense.Length} chars, best of 3 unless stated"
        );

        Console.WriteLine("\n## Eager Matches against lazy EnumerateMatches, walked to the end");
        var words = new FuzzyRegex(@"\w+");
        Report("Matches, 1 MB", () => words.Matches(Corpus.Long).Count);
        Report("Matches, 100 KB", () => words.Matches(Corpus.Dense).Count);
        Report("EnumerateMatches, 100 KB", () => words.EnumerateMatches(Corpus.Dense).Count());
        // ONE run, not three: if this is what the 1 MB Dry run suggested, three would cost a minute.
        Report("EnumerateMatches, 1 MB", () => words.EnumerateMatches(Corpus.Long).Count(), runs: 1);
        Report("EnumerateMatches, 1 MB, first two only", () => words.EnumerateMatches(Corpus.Long).Take(2).Count());

        Console.WriteLine("\n## The two catastrophic-backtracking shapes, over a run of 'a'");
        foreach (int n in new[] { 18, 20, 22, 24 })
        {
            string subject = new('a', n);
            Report($"(a+)+b  n={n}", () => new FuzzyRegex("(a+)+b").IsMatch(subject) ? 1 : 0);
            Report($"(a|a)*b n={n}", () => new FuzzyRegex("(a|a)*b").IsMatch(subject) ? 1 : 0);
        }

        Console.WriteLine("\n## (a+)+b at the lengths a timeout test might use");
        foreach (int n in new[] { 200, 2000 })
        {
            string subject = new('a', n);
            Report($"(a+)+b  n={n}", () => new FuzzyRegex("(a+)+b").IsMatch(subject) ? 1 : 0);
        }

        // The same shape on the built-in engine, so ReferenceBenchmarks.BacktrackingBcl's remarks
        // can say what was measured here rather than what one would expect. Instances are built
        // once, outside the timed call, exactly as the benchmarks build theirs.
        Console.WriteLine("\n## (a|a)*b on System.Text.RegularExpressions.Regex, for comparison");
        foreach (int n in new[] { 18, 24, 30, 40 })
        {
            string subject = new('a', n);
            var interpreted = new System.Text.RegularExpressions.Regex("(a|a)*b");
            var compiled = new System.Text.RegularExpressions.Regex(
                "(a|a)*b",
                System.Text.RegularExpressions.RegexOptions.Compiled
            );
            Report($"bcl (a|a)*b          n={n}", () => interpreted.IsMatch(subject) ? 1 : 0);
            Report($"bcl (a|a)*b compiled n={n}", () => compiled.IsMatch(subject) ? 1 : 0);
        }
    }

    /// <summary>Times an operation and prints the best of <paramref name="runs"/> elapsed times.</summary>
    /// <param name="label">What is being measured.</param>
    /// <param name="operation">The operation, returning a value so it cannot be optimised away.</param>
    /// <param name="runs">How many times to run it.</param>
    private static void Report(string label, Func<int> operation, int runs = 3)
    {
        double best = double.MaxValue;
        int answer = 0;
        for (int i = 0; i < runs; i++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            answer = operation();
            watch.Stop();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }

        // string.Format rather than an interpolated string: CSharpier writes an interpolation
        // alignment as `{label, -42}` and the .NET formatter demands `{label,-42}`, and IDE0055 is
        // an error here, so that syntax is unwritable (the same conflict the .editorconfig resolves
        // for `case` blocks and goto labels). Inside a format literal the alignment is just text.
        Console.WriteLine(
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0,-42} {1,12:N2} ms   (answer {2})",
                label,
                best,
                answer
            )
        );
    }
}
