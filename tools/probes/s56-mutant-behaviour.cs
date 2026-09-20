#:project ../../src/FuzzyRegex/FuzzyRegex.csproj

// S56: what does the engine do when one of the queue's Timeout / RuntimeError mutants is applied?
// Driven by tools/probes/s56-mutant-behaviour.py, which applies the mutation, runs this, and
// restores the file. The watchdog is here rather than in the driver so that a hanging mutant ends
// its own process - killing `dotnet run`'s grandchild from outside is not reliable on Windows.
//
// Exit codes: 0 finished, 3 threw (type and message printed), 124 still running at the deadline.

using System.Diagnostics;
using Fuzzy.Text.RegularExpressions;

string probe = args.Length > 0 ? args[0] : "groupcall";
int deadlineSeconds = args.Length > 1 ? int.Parse(args[1]) : 60;

var done = new ManualResetEventSlim(false);
int exitCode = 0;
var clock = Stopwatch.StartNew();

var worker = new Thread(() =>
{
    try
    {
        switch (probe)
        {
            case "groupcall":
                // Reaches NodeCompiler.BuildGroupCall, the `args.Code += 2` site.
                var call = new FuzzyRegex("(?P<a>x)(?&a)");
                Console.WriteLine($"compiled; match = {call.Match("xx").Success}");
                break;

            case "optimiser":
                // Reaches Optimiser's GreedyRepeat arm, the `node.Status |= VisitedAg` site.
                var repeat = new FuzzyRegex("(a|b)*c");
                Console.WriteLine($"compiled; match = {repeat.Match("aabbc").Success}");
                break;

            case "string":
                // Reaches NodeCompiler.BuildString, the `args.Code += 3 + length` site.
                var text = new FuzzyRegex("abcdef");
                Console.WriteLine($"compiled; match = {text.Match("abcdef").Success}");
                break;

            case "bytestack":
                // Fuzzy matching saves far more state per step than a plain match, so it grows
                // the backtracking stack through ByteStack.PushBlock's doubling loop.
                var back = new FuzzyRegex("(?:abcdef){e<=3}");
                Console.WriteLine($"match = {back.Match(new string('a', 2000) + "abcdef").Success}");
                break;

            default:
                Console.WriteLine($"unknown probe '{probe}'");
                exitCode = 2;
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"threw {ex.GetType().Name}: {ex}");
        exitCode = 3;
    }

    done.Set();
})
{
    IsBackground = true,
};

worker.Start();

if (!done.Wait(TimeSpan.FromSeconds(deadlineSeconds)))
{
    Console.WriteLine($"still running after {deadlineSeconds}s - no result");
    Environment.Exit(124);
}

Console.WriteLine($"finished in {clock.ElapsedMilliseconds} ms");
Environment.Exit(exitCode);
