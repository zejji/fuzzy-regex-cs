#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// 2026-09-27, for docs/plan/2026-09-27-recursion-failure-memo-design.md: how the two recursion
// rows grow with the subject, with ledger entry 42's exact-deletion retry on and off.
//
//     dotnet run -c Release tools/probes/recursion-failure-memo/timing.cs [row] [maxLength] [seconds] [V0|V1]
//
// Without -c Release the library builds Debug and runs 5-10x slower.
// Row A is a search, row B a fullmatch; each subject is a prefix of the reported one. Every run
// has a timeout (default 60 s), so a timed-out cell prints "timeout" rather than hanging.
// SkipExactDeletionRetry is internal; it is set by reflection, exactly as the oracle's ablation
// (OracleComparer.RunWithoutTheExactDeletion) sets it, but leaving the "needed" rule on.

#pragma warning disable IDE0042, S3011, MA0006, IL2075, CA1305, CA1307, MA0011, MA0076
using System.Diagnostics;
using System.Reflection;
using Fuzzy.Text.RegularExpressions;

string which = args.Length > 0 ? args[0] : "A";
int maxLength = args.Length > 1 ? int.Parse(args[1]) : 6;
double seconds = args.Length > 2 ? double.Parse(args[2]) : 60;
string prefix = args.Length > 3 ? $"(?{args[3]})" : ""; // e.g. V0, as the oracle compiles

(string Pattern, string Subject, bool Search)[] rows =
[
    (@"(|)(?:(?:(?:(?:.)+((?:(?R)){2,}|)){2<=e<=3}(?=b))){1<=s<=1,1<=d<=2}", "baxbaxbax", true),
    (@"(?b)(?:(?:.(?:(?:(?:b)+(?R)||)){1<=e<=2}(?:c)*?){2<=d<=3})", "xxaxabxx", false),
];

var row = rows[which == "A" ? 0 : 1];
PropertyInfo patternObject = typeof(FuzzyRegex).GetProperty(
    "PatternObject",
    BindingFlags.Instance | BindingFlags.NonPublic
)!;

Console.WriteLine($"row {which}: {row.Pattern}");
Console.WriteLine("length  subject    retry-on ms   retry-off ms   answer (retry on)");
for (int length = 1; length <= Math.Min(maxLength, row.Subject.Length); length++)
{
    string subject = row.Subject[..length];
    string on = Run(subject, skip: false, out string answer);
    string off = Run(subject, skip: true, out _);
    Console.WriteLine($"{length, 6}  {subject, -9}  {on, 11}  {off, 13}   {answer}");
}

string Run(string subject, bool skip, out string answer)
{
    var regex = new FuzzyRegex(prefix + row.Pattern);
    object po = patternObject.GetValue(regex)!;
    po.GetType().GetField("SkipExactDeletionRetry", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(po, skip);
    var watch = Stopwatch.StartNew();
    try
    {
        Match m = row.Search
            ? regex.Match(subject, timeout: TimeSpan.FromSeconds(seconds))
            : regex.FullMatch(subject, timeout: TimeSpan.FromSeconds(seconds));
        answer = m.Success ? $"({m.Index}, {m.Index + m.Length}) {m.FuzzyCounts}" : "no match";
        return watch.Elapsed.TotalMilliseconds.ToString("F1");
    }
    catch (Exception e) when (e is System.Text.RegularExpressions.RegexMatchTimeoutException)
    {
        answer = "timeout";
        return "timeout";
    }
    catch (Exception e)
    {
        answer = e.GetType().Name;
        return e.GetType().Name;
    }
}
