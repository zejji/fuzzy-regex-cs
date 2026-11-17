#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
#:property Configuration=Release
#:property RunAnalyzers=false
#:property TreatWarningsAsErrors=false
// D68, the recursive half: does a recursive call's cost grow with the number of groups in the
// pattern? Each body is matched with K extra groups that never take part, appended as (?:(x)y)?
// so the matcher only tests one character for them. A recursion copies every group at each call
// entry if D68's cause holds; the call-free control has the same groups and no calls.
// Prints median us per match (15 samples) per (body, K), and the match span to check answers.
// Run with DOTNET_TieredCompilation=0, pinned to the performance cores.
#pragma warning disable ERP022, S6966, SS033
using System.Diagnostics;
using Fuzzy.Text.RegularExpressions;

string nested = new string('(', 30) + "ab" + new string(')', 30);
string flat = string.Concat(Enumerable.Repeat("(ab)", 30));
var bodies = new (string Name, string Pattern, string Subject)[]
{
    ("recursive-depth30", @"(\((?:[^()]|(?1))*\))", nested),
    ("recursive-flat30", @"((?:\((?:[^()]|(?1))*\))+)", flat),
    ("callfree-flat30", @"((?:\((?:[^()]|\([^()]*\))*\))+)", flat),
    ("recursive-fuzzy", @"(\((?:[^()]|(?1))*\)){e<=1}", nested),
};
int[] extras = [0, 25, 100, 400];

foreach (var (name, body, subject) in bodies)
{
    var parts = new List<string>();
    foreach (int k in extras)
    {
        string pattern = body + string.Concat(Enumerable.Repeat("(?:(x)y)?", k));
        var regex = new FuzzyRegex(pattern);
        Match first = regex.Match(subject);
        string answer = first.Success ? $"({first.Index},{first.Index + first.Length})" : "None";
        for (int i = 0; i < 50; i++)
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
        parts.Add($"K={k} {samples[7]:F2}us {answer}");
    }

    Console.WriteLine($"{name}: {string.Join(" | ", parts)}");
}
