#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// 2026-09-27, for docs/plan/2026-09-27-recursion-failure-memo-design.md: what the prototype
// "call with no exit" memo costs on recursive patterns that do not blow up. Median of 7 runs after
// 3 warm-ups, memo off and on (minimal key). NEEDS THE INSTRUMENTATION PATCH (see count-states.cs).
//     dotnet run -c Release tools/probes/recursion-failure-memo/overhead.cs

#pragma warning disable
using System.Diagnostics;
using Fuzzy.Text.RegularExpressions;

Type probe = typeof(FuzzyRegex).Assembly.GetType("Fuzzy.Text.RegularExpressions.Engine.ProbeCounters")!;
string nested = new string('(', 300) + "x" + new string(')', 300);
string flat = string.Concat(Enumerable.Repeat("(ab)(c)d", 1500));
string ab = new string('a', 60) + new string('b', 60);
string pal = "abcdefghijjihgfedcba";
(string Name, string Pattern, string Subject, bool Full)[] cases =
[
    ("balanced, nested 300 deep", @"\((?:[^()]++|(?R))*\)", nested, false),
    ("balanced, 12k flat", @"\((?:[^()]++|(?R))*\)", flat, false),
    ("balanced, no match 12k", @"\((?:[^()]++|(?R))*\)z", flat, false),
    ("fuzzy a^n b^n, n=60", @"(?:a(?R)?b){e<=1}", ab, true),
    ("fuzzy a^n b^n fails", @"(?:a(?R)?b){e<=1}c", ab, false),
    ("palindrome with backref", @"^((.)(?:(?1)|.?)\2)$", pal, false),
    ("row A, baxba", @"(|)(?:(?:(?:(?:.)+((?:(?R)){2,}|)){2<=e<=3}(?=b))){1<=s<=1,1<=d<=2}", "baxba", false),
    ("row B, xxaxabxx", @"(?b)(?:(?:.(?:(?:(?:b)+(?R)||)){1<=e<=2}(?:c)*?){2<=d<=3})", "xxaxabxx", true),
];

probe.GetField("KeyMode")!.SetValue(null, 1);
foreach (var c in cases)
{
    var regex = new FuzzyRegex(c.Pattern);
    string Time(bool memo, out string answer)
    {
        var times = new List<double>();
        answer = "";
        int runs = c.Name.StartsWith("row") && !memo ? 1 : 10;
        for (int i = 0; i < runs; i++)
        {
            probe.GetMethod("Reset")!.Invoke(null, null);
            probe.GetField("Memo")!.SetValue(null, memo);
            var w = Stopwatch.StartNew();
            Match m = c.Full
                ? regex.FullMatch(c.Subject, timeout: TimeSpan.FromSeconds(60))
                : regex.Match(c.Subject, timeout: TimeSpan.FromSeconds(60));
            double ms = w.Elapsed.TotalMilliseconds;
            probe.GetField("Memo")!.SetValue(null, false);
            answer = m.Success ? $"({m.Index},{m.Index + m.Length}) {m.FuzzyCounts.Total}e" : "none";
            if (runs == 1 || i >= 3)
                times.Add(ms);
        }
        times.Sort();
        return $"{times[times.Count / 2], 9:F3} ms";
    }
    string off = Time(false, out string a1);
    string on = Time(true, out string a2);
    Console.WriteLine(
        $"{c.Name, -28} off {off}  on {on}  hits {probe.GetField("MemoHits")!.GetValue(null), 8}  records {probe.GetField("MemoRecords")!.GetValue(null), 7}  {(a1 == a2 ? "same answer " + a1 : "DIFFERENT " + a1 + " / " + a2)}"
    );
}
