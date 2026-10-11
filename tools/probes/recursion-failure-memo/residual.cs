#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// 2026-09-27, for docs/plan/2026-09-27-recursion-failure-memo-design.md: the grid's shapes that stay
// slow with the prototype memo. Is the residue the exact-deletion retry's (ledger 42) or older?
// Each cell is one search, 5 s cap. NEEDS THE INSTRUMENTATION PATCH (see count-states.cs).
//     dotnet run -c Release tools/probes/recursion-failure-memo/residual.cs

#pragma warning disable
using System.Diagnostics;
using System.Reflection;
using Fuzzy.Text.RegularExpressions;

Type probe = typeof(FuzzyRegex).Assembly.GetType("Fuzzy.Text.RegularExpressions.Engine.ProbeCounters")!;
probe.GetField("KeyMode")!.SetValue(null, 1);
string[] patterns =
[
    @"(?b)((?:(?R)|))(?:\1b(?R)?(?(1)(?:(?R)|)|(?:(?1)a(?R)|(?R))(?:|(?R)))(?:(?!(?R)(?1)))(?<=a)){e<=2}",
    @"((?:(?R)|))(?:(?:(?:(?R)|)(?:(?1)){d<=1}(?:){i<=1}){1,2}(?R)?(?:(?!(?R)(?1))(?:b|a)){2<=e<=3}){e<=2}",
    @"(?r)((?:(?:(?R)|)(?:b.)+(?:(?R)|)){1<=e<=2})(?:(?:(?:b)*?)(?R)?.(?:(?:b(?1)|)(?!a.)((?1)a)|(?=(?1)))){e<=2}",
];
string[] subjects = ["ab", "aba", "abab", "ababa"];
foreach (string pattern in patterns)
{
    Console.WriteLine(pattern);
    foreach (string subject in subjects)
    {
        string Cell(bool skip, bool memo)
        {
            var regex = new FuzzyRegex(pattern);
            object po = typeof(FuzzyRegex)
                .GetProperty("PatternObject", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(regex)!;
            po.GetType()
                .GetField("SkipExactDeletionRetry", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(po, skip);
            probe.GetMethod("Reset")!.Invoke(null, null);
            probe.GetField("Memo")!.SetValue(null, memo);
            var w = Stopwatch.StartNew();
            try
            {
                regex.Match(subject, timeout: TimeSpan.FromSeconds(5));
                return $"{w.Elapsed.TotalMilliseconds, 8:F1}";
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                return "   >5000";
            }
            finally
            {
                probe.GetField("Memo")!.SetValue(null, false);
            }
        }
        Console.WriteLine(
            $"  {subject, -6} retry on: {Cell(false, false)} ms, +memo {Cell(false, true)} ms | retry off: {Cell(true, false)} ms, +memo {Cell(true, true)} ms"
        );
    }
}
