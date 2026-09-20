#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// How much wall-clock margin do the demo's SIZE caps have against the demo's TIME cap?
//
// S57, 2026-09-20. `DemoEngineContractTests.One_match_cannot_carry_unbounded_capture_spans` and
// `A_partial_answer_whose_capture_list_was_clipped_says_truncated` each assert that an answer
// clipped by `DemoEngine.MaxSpans` says `truncated`, and each runs the shipped path, which also
// carries `DemoEngine.MatchTimeout` - two seconds. Sitting 1 of S57 saw them fail at 2s 026ms and
// 2s 049ms and read that as machine load. It is not: this probe is the one-parameter measurement
// that settles it. The matching costs tens of milliseconds, so the plain gate has a margin of
// forty-five to seventy fold; the failures come from the ~50x slowdown of coverage instrumentation,
// which the plain `dotnet test` gate does not carry.
//
// Reaching the span cap costs 50,000 spans by definition and the engine's work is roughly one step
// per span, so there is no smaller subject that reaches the cap and dodges the clock. That is why
// the conclusion is about the coverage run - a one-off analysis tool, never a gate - and not about
// the tests.
//
// Run (a .NET 10 file-based app, no project of its own). Under a second:
//
//     dotnet run -c Release tools/probes/demo-cap-timing.cs
//
// `-c Release` is not optional; Debug is several times slower and the margin it reports is not the
// margin the gate has. Measured on 2026-09-20, Release:
//
//     nested20 x1000: 37 ms, spans=20022
//     nested20 x2600: 14 ms, spans=52022
//     nested20 x5000: 27 ms, spans=100022
//     (\w)+ partial x50010: 36 ms, spans=50013
//     (\w)+ partial x60000: 44 ms, spans=60003
//     nested20 x5000 WITH 2s budget: 20 ms
//     (\w)+ partial x60000 WITH 2s budget: 36 ms
//
// The last two rows are the same work with the demo's own per-step budget handed down, because
// that is the only difference between this probe and `DemoEngine`: polling the clock does not cost
// the seconds either.

using System.Diagnostics;
using Fuzzy.Text.RegularExpressions;

static long Time(Action work)
{
    Stopwatch clock = Stopwatch.StartNew();
    work();
    return clock.ElapsedMilliseconds;
}

// `One_match_cannot_carry_unbounded_capture_spans`' pattern and subject, plus the two sizes either
// side of the 50,000-span cap.
string nested = new string('(', 20) + @"\w" + new string(')', 20) + "+";

foreach (int n in new[] { 1_000, 2_600, 5_000 })
{
    string subject = new('a', n);
    FuzzyRegex re = new(nested);
    int spans = 0;
    long ms = Time(() =>
    {
        Match m = re.EnumerateMatches(subject).First();
        foreach (Group g in m.Groups)
        {
            spans += 1 + g.Captures.Count;
        }
    });
    Console.WriteLine($"nested20 x{n}: {ms} ms, spans={spans}");
}

// `A_partial_answer_whose_capture_list_was_clipped_says_truncated`' pattern, at the smallest
// subject that can reach the cap and at the one the test uses.
foreach (int n in new[] { 50_010, 60_000 })
{
    string subject = new('a', n);
    FuzzyRegex re = new(@"(\w)+");
    int spans = 0;
    long ms = Time(() =>
    {
        Match m = re.Match(subject, partial: true);
        foreach (Group g in m.Groups)
        {
            spans += 1 + g.Captures.Count;
        }
    });
    Console.WriteLine($"(\\w)+ partial x{n}: {ms} ms, spans={spans}");
}

TimeSpan budget = TimeSpan.FromSeconds(2);

static void Budgeted(string label, string pattern, int n, TimeSpan budget, bool partial)
{
    string subject = new('a', n);
    FuzzyRegex re = new(pattern, FuzzyRegexOptions.None, budget);
    long ms = Time(() =>
    {
        try
        {
            _ = partial
                ? re.Match(subject, partial: true, timeout: budget)
                : re.EnumerateMatches(subject, timeout: budget).First();
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            Console.WriteLine("  TIMED OUT");
        }
    });
    Console.WriteLine($"{label} WITH 2s budget: {ms} ms");
}

Budgeted("nested20 x5000", nested, 5_000, budget, partial: false);
Budgeted(@"(\w)+ partial x60000", @"(\w)+", 60_000, budget, partial: true);
