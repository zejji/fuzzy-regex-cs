#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// D68: steps of an anchored attempt at every start position, call vs written-out.
using System.Reflection;
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

#pragma warning disable S3011
Type wc = Type.GetType("Fuzzy.Text.RegularExpressions.Engine.WorkCounter, FuzzyRegex", throwOnError: true)!;
var steps = wc.GetProperty("Steps", BindingFlags.NonPublic | BindingFlags.Static)!;
foreach (var line in File.ReadLines(args[0]))
{
    using var doc = JsonDocument.Parse(line);
    string subject = doc.RootElement.GetProperty("subject").GetString()!;
    var parts = new List<string>();
    foreach (var field in new[] { "pattern", "inline" })
    {
        var regex = new FuzzyRegex(doc.RootElement.GetProperty(field).GetString()!);
        long search0 = (long)steps.GetValue(null)!;
        _ = regex.Match(subject);
        long search = (long)steps.GetValue(null)! - search0;
        long sum = 0;
        for (int p = 0; p <= subject.Length; p++)
        {
            long b = (long)steps.GetValue(null)!;
            _ = regex.MatchAtStart(subject, p, subject.Length - p);
            sum += (long)steps.GetValue(null)! - b;
        }

        parts.Add($"{field} search {search} anchored-sum {sum}");
    }

    Console.WriteLine(string.Join(" | ", parts));
}
