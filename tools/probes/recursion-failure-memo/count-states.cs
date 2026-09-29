#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// 2026-09-27, for docs/plan/2026-09-27-recursion-failure-memo-design.md: how many of the states the
// matcher visits on the two recursion rows are repeats, under several candidate memo keys.
//
// NEEDS THE INSTRUMENTATION PATCH beside this file, which is not in the engine:
//     git apply tools/probes/recursion-failure-memo/instrumentation.patch
//     dotnet run -c Release tools/probes/recursion-failure-memo/count-states.cs A 4
//     git apply -R tools/probes/recursion-failure-memo/instrumentation.patch
//
// A "visit" is one pass of the main matching loop in Matcher.BasicMatch (one node dispatched). The
// keys are defined in the patch's ProbeCounters.Visit; the note's table explains each.

#pragma warning disable
using System.Diagnostics;
using System.Reflection;
using Fuzzy.Text.RegularExpressions;

string which = args.Length > 0 ? args[0] : "A";
int maxLength = args.Length > 1 ? int.Parse(args[1]) : 4;
bool skip = args.Length > 2 && args[2] == "off";
bool memo = args.Length > 2 && args[2].StartsWith("memo");
int keyMode = args.Length > 2 && args[2] == "memo-min" ? 1 : 0; // prototype C1, instrumentation off

(string Pattern, string Subject, bool Search)[] rows =
[
    (@"(|)(?:(?:(?:(?:.)+((?:(?R)){2,}|)){2<=e<=3}(?=b))){1<=s<=1,1<=d<=2}", "baxbaxbaxbaxbax", true),
    (@"(?b)(?:(?:.(?:(?:(?:b)+(?R)||)){1<=e<=2}(?:c)*?){2<=d<=3})", "xxaxabxxaxabxxaxab", false),
];
var row = rows[which == "A" ? 0 : 1];

Assembly lib = typeof(FuzzyRegex).Assembly;
Type probe = lib.GetType("Fuzzy.Text.RegularExpressions.Engine.ProbeCounters")!;
object Get(string name) => probe.GetField(name)!.GetValue(null)!;
int Size(string name) => (int)Get(name).GetType().GetProperty("Count")!.GetValue(Get(name))!;

Console.WriteLine($"row {which} (retry {(skip ? "off" : "on")}): {row.Pattern}");
Console.WriteLine(
    "len subject     ms        visits   (node,pos)      local       norm  norm-any-start     callee   summary   deficit  no-stack      strict  calls  call-entries  depth"
);
for (int length = 1; length <= Math.Min(maxLength, row.Subject.Length); length++)
{
    string subject = row.Subject[..length];
    var regex = new FuzzyRegex(row.Pattern);
    object po = typeof(FuzzyRegex)
        .GetProperty("PatternObject", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(regex)!;
    po.GetType().GetField("SkipExactDeletionRetry", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(po, skip);
    probe.GetMethod("Reset")!.Invoke(null, null);
    probe.GetField("On")!.SetValue(null, !memo);
    probe.GetField("Memo")!.SetValue(null, memo);
    probe.GetField("KeyMode")!.SetValue(null, keyMode);
    var watch = Stopwatch.StartNew();
    Match m = row.Search
        ? regex.Match(subject, timeout: TimeSpan.FromSeconds(60))
        : regex.FullMatch(subject, timeout: TimeSpan.FromSeconds(60));
    double ms = watch.Elapsed.TotalMilliseconds;
    probe.GetField("Memo")!.SetValue(null, false);
    if (memo)
    {
        Console.WriteLine(
            $"{length, 3} {subject, -8} {ms, 8:F1} ms  memo hits {Get("MemoHits")}  records {Get("MemoRecords")}  {(m.Success ? $"({m.Index}, {m.Index + m.Length}) {m.FuzzyCounts}" : "no match")}"
        );
        continue;
    }
    probe.GetField("On")!.SetValue(null, false);
    long visits = (long)Get("Visits");
    Console.WriteLine(
        $"{length, 3} {subject, -8} {ms, 8:F0} {visits, 12} {Size("Pos"), 12} {Size("Local"), 10} {Size("Norm"), 10} {Size("NormNoAttempt"), 15} {Size("Callee"), 10} {Size("Summary"), 9} {Size("Deficit"), 9} {Size("NoStack"), 9} {Size("Strict"), 11} {Get("CallVisits"), 6} {Size("Entries"), 13}  {Get("MaxDepth")}   {(m.Success ? "match" : "no match")}"
    );
    {
        var instances = (List<long>)Get("Instances");
        var returned = (HashSet<long>)Get("Returned");
        var seen = new HashSet<long>();
        long repeatNoExit = 0,
            repeatAny = 0;
        foreach (long k in instances)
        {
            if (!seen.Add(k))
            {
                repeatAny++;
                if (!returned.Contains(k))
                    repeatNoExit++;
            }
        }
        Console.WriteLine(
            $"    calls {instances.Count}, distinct entries {seen.Count}, entries that never returned {seen.Count(k => !returned.Contains(k))}, repeated calls {repeatAny}, of them to a never-returning entry {repeatNoExit}"
        );
    }
    if (length == Math.Min(maxLength, row.Subject.Length))
    {
        foreach (var (op, c) in ((Dictionary<string, long>)Get("Ops")).OrderByDescending(p => p.Value))
            Console.Write($"{op} {c}; ");
        Console.WriteLine();
        var byDepth = (Dictionary<int, long[]>)Get("ByDepth");
        foreach (var (depth, counts) in byDepth.OrderBy(p => p.Key))
        {
            Console.WriteLine(
                $"    call depth {depth, 2}: visits {counts[0], 12}  new strict states {counts[1], 10}  repeated {100.0 * (counts[0] - counts[1]) / counts[0], 5:F1}%"
            );
        }
    }
}
