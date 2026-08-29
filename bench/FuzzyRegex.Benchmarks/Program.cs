using BenchmarkDotNet.Running;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>Entry point for the BenchmarkDotNet suite. Driven by the <c>benchmark</c> skill.</summary>
internal static class Program
{
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
