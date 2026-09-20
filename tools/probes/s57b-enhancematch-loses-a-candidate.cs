#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// S57b: this port's half of `enhancematch-loses-a-candidate`, seed 20260920 row 122739 of the
// 6000-row gate and its fifteen-character minimisation.
//
//     dotnet run tools/probes/s57b-enhancematch-loses-a-candidate.cs
//
// The upstream half is tools/probes/s57b-enhancematch-loses-a-candidate.py. Read the two together:
// upstream's `(?e)` keeps a three-error fit over 'a6ZZ_' where its own `{e<=2}` finds a two-error
// one, and this port's improvement loop reaches the two-error fit. Without `(?e)` the two engines
// agree, which is what says the divergence is the loop rather than the fuzzy matcher under it.
//
// Measured 2026-09-20.
using Fuzzy.Text.RegularExpressions;

static void Show(string label, string pattern, string subject)
{
    FuzzyRegex re = new(pattern);
    Match m = re.FullMatch(subject);
    string fit = m.Success
        ? $"span=({m.Index},{m.Length}) counts=({m.FuzzyCounts.Substitutions}, "
            + $"{m.FuzzyCounts.Insertions}, {m.FuzzyCounts.Deletions})"
        : "no match";
    Console.WriteLine($"  {label, -34} {fit}");
}

Console.WriteLine("the minimised row, (?:a\\d+Z) over 'a6ZZ_'");
foreach (string prefix in new[] { "", "(?e)", "(?b)" })
{
    foreach (int budget in new[] { 2, 3, 4 })
    {
        Show($"{(prefix.Length == 0 ? "plain" : prefix)} e<={budget}", $@"{prefix}(?:a\d+Z){{e<={budget}}}", "a6ZZ_");
    }
}

Console.WriteLine();
Console.WriteLine("the control: the same pattern over 'a6Z_', where upstream's loop does improve");
Show("plain e<=3", @"(?:a\d+Z){e<=3}", "a6Z_");
Show("(?e) e<=3", @"(?e)(?:a\d+Z){e<=3}", "a6Z_");

Console.WriteLine();
Console.WriteLine("the wave row itself, seed 20260920 row 122739");
Show("(?e), as drawn", "(?e)(?:abx\\d+[\U0001f600\U0001d518]){e<=3:[^x]}", "abx6\U0001d518\U0001d518\U0001f3fb");
Show("(?e) deleted", "(?:abx\\d+[\U0001f600\U0001d518]){e<=3:[^x]}", "abx6\U0001d518\U0001d518\U0001f3fb");
