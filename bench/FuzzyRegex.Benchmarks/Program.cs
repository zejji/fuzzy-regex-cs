using BenchmarkDotNet.Running;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>Entry point for the BenchmarkDotNet suite. Driven by the <c>benchmark</c> skill.</summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        // `sizing` is not a benchmark filter: it is the one-shot stopwatch pass that decides how
        // this suite and the S54 trap tests are sized. See Sizing.cs.
        if (args is ["sizing"])
        {
            Sizing.Run();
            return;
        }

        // `attribution` is not a benchmark filter either: it is the one-shot allocation split that
        // stands in for the allocation profiler nothing installed can read. See Attribution.cs.
        if (args is ["attribution"])
        {
            Attribution.Run();
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
