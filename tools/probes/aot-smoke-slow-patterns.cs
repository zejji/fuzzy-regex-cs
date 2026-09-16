#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// How long does this port take to answer some classic catastrophic-backtracking patterns, with no
// budget at all?
//
// S53. The AOT smoke app asserts that MatchTimeout fires, and that assertion is only meaningful if
// the work it interrupts really does exceed the budget. The textbook pattern does not: this port
// answers `(a+)+$` in tens of milliseconds, so a 50 ms budget over it would pass or fail by luck.
// This probe is how the smoke app's pattern was chosen, and re-running it is how to re-choose one
// if the engine gets faster - which Phase 7 intends.
//
// Run (a .NET 10 file-based app, no project of its own). About 14 s:
//
//     dotnet run -c Release tools/probes/aot-smoke-slow-patterns.cs
//
// `-c Release` is NOT optional and the timings below are not reproducible without it. A file-based
// app defaults to Debug, where the same rows come out several times slower - 24,312 ms and
// 103,251 ms for the last two, against roughly 2,500 and 12,000 here - and the whole probe takes over two
// minutes instead of fourteen seconds. The smoke app's budget was chosen against the Release
// figures because Release is what CI publishes.
//
// Measured 2026-09-16, Release, this machine, against the committed code. **Quote the RANGES, not
// a single run.** This is wall clock on a loaded developer machine and the spread is wider than it
// first looked: S53's independent verifier re-ran the probe and got 3,280 ms for the `(a|aa)+$`
// row against the 2,531 ms first recorded here, 30% out, which is why the two slow rows are given
// as ranges over five and four observations respectively:
//
//     (a+)+$              len=41       42-47 ms  matched=False
//     (a+)+$              len=61       10-14 ms  matched=False
//     (a|aa)+$            len=33   2400-3300 ms  matched=False   <- what the smoke app uses
//     ^(a|a?)+$           len=31           0 ms  matched=False
//     (x+x+)+y            len=30         1-5 ms  matched=False
//     (?:a{0,10}){0,10}b  len=30  11500-13150 ms  matched=False
//
// The ORDERS of magnitude are what the smoke app's 50 ms budget rests on, not the digits: the
// slowest-to-fire row is at worst 48x the budget and at best 66x.
//
// The smoke app wants the middle ground: comfortably over a 50 ms budget, and still bounded, so a
// regression that stopped the timeout firing ends in seconds instead of hanging CI.
//
// The `len=` column counts the whole subject, so `len=33` is 32 a's plus the trailing 'b'. The
// smoke app asserts that exact subject.
//
// One case is behind a flag because it takes SIX MINUTES and would make this probe useless as a
// quick check. `dotnet run -c Release tools/probes/aot-smoke-slow-patterns.cs -- --all` adds it:
//
//     (a|a)*$             len=31  379561 ms  matched=True
//
// That is why "pick the slowest" is the wrong rule for the smoke app, and it is not a bug: an
// unanchored alternation of two identical branches under a star is the shape MatchTimeout exists
// for. Recorded here rather than in the ledger for that reason.

using System.Diagnostics;
using System.Globalization;
using Fuzzy.Text.RegularExpressions;

bool all = args.Contains("--all", StringComparer.Ordinal);

(string Pattern, string Subject)[] candidates =
[
    (@"(a+)+$", new string('a', 40) + "b"),
    (@"(a+)+$", new string('a', 60) + "b"),
    (@"(a|aa)+$", new string('a', 32) + "b"),
    (@"^(a|a?)+$", new string('a', 30) + "b"),
    (@"(x+x+)+y", new string('x', 30)),
    (@"(?:a{0,10}){0,10}b", new string('a', 30)),
    .. all ? new[] { (@"(a|a)*$", new string('a', 30) + "b") } : [],
];

foreach ((string pattern, string subject) in candidates)
{
    FuzzyRegex compiled = new(pattern);
    var clock = Stopwatch.StartNew();
    bool matched = compiled.Match(subject).Success;
    clock.Stop();

    // PadRight/PadLeft rather than interpolation alignment specifiers: CSharpier writes those
    // with a space after the comma and IDE0055 rejects the space, so a line using one cannot
    // satisfy both of this repo's formatting gates.
    string length = subject.Length.ToString(CultureInfo.InvariantCulture);
    string ms = clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture);

    Console.WriteLine(
        pattern.PadRight(22) + " len=" + length.PadRight(3) + " " + ms.PadLeft(8) + " ms  matched=" + matched
    );
}
