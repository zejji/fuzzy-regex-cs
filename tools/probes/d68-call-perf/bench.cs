#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// Run with DOTNET_TieredCompilation=0 (see docs/plan/2026-10-03-d68-call-copy-perf.md).
#pragma warning disable ERP022, S6966, SS033
// D68: per-row match time, median of 15 samples after warm-up, for 'pattern' and 'inline'.
using System.Diagnostics;
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

foreach (var line in File.ReadLines(args[0]))
{
    using var doc = JsonDocument.Parse(line);
    string subject = doc.RootElement.GetProperty("subject").GetString()!;
    var parts = new List<string>();
    foreach (var field in new[] { "pattern", "inline" })
    {
        var regex = new FuzzyRegex(doc.RootElement.GetProperty(field).GetString()!);
        string answer = regex.Match(subject) is { Success: true } m ? $"({m.Index},{m.Index + m.Length})" : "None";
        for (int i = 0; i < 200; i++)
        {
            _ = regex.Match(subject);
        }

        long t0 = Stopwatch.GetTimestamp();
        int n = 0;
        while (Stopwatch.GetElapsedTime(t0).TotalMilliseconds < 20)
        {
            _ = regex.Match(subject);
            n++;
        }

        var samples = new List<double>();
        for (int s = 0; s < 15; s++)
        {
            long t = Stopwatch.GetTimestamp();
            for (int i = 0; i < n; i++)
            {
                _ = regex.Match(subject);
            }

            samples.Add(Stopwatch.GetElapsedTime(t).TotalMicroseconds / n);
        }

        samples.Sort();
        parts.Add($"{field} {samples[7]:F2}us {answer}");
    }

    Console.WriteLine(string.Join(" | ", parts));
}
