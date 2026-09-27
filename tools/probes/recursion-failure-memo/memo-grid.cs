#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// 2026-09-27, for docs/plan/2026-09-27-recursion-failure-memo-design.md: does the prototype
// "call with no exit" memo change any answer? Random small patterns that mix fuzzy sections with
// recursion and the constructs the note names (captures, backreferences, conditionals, lookaround,
// atomic groups, verbs, (?b), (?e), (?r)), every subject over {a, b, x} up to length 3 plus a few
// longer ones, four modes; each answer compared with the memo off, the full key and the minimal key.
//
// NEEDS THE INSTRUMENTATION PATCH beside this file (see count-states.cs for how to apply it).
//     dotnet run -c Release tools/probes/recursion-failure-memo/memo-grid.cs [seed] [patterns] [minutes]

#pragma warning disable
using System.Diagnostics;
using System.Text;
using Fuzzy.Text.RegularExpressions;

int seed = args.Length > 0 ? int.Parse(args[0]) : 1;
int patternCount = args.Length > 1 ? int.Parse(args[1]) : 2000;
double minutes = args.Length > 2 ? double.Parse(args[2]) : 8;
var rng = new Random(seed);
Type probe = typeof(FuzzyRegex).Assembly.GetType("Fuzzy.Text.RegularExpressions.Engine.ProbeCounters")!;

string[] fuzzy = ["{e<=1}", "{1<=e<=2}", "{d<=1}", "{1<=d<=2}", "{s<=1,d<=1}", "{i<=1}", "{e<=2}", "{2<=e<=3}"];
string[] quants = ["", "", "+", "*", "?", "{2,}", "*?", "+?", "{1,2}"];

string Atom(int depth)
{
    int r = rng.Next(depth > 2 ? 6 : 22);
    return r switch
    {
        0 => "a",
        1 => "b",
        2 => ".",
        3 => "(?R)",
        4 => "(?1)",
        5 => "",
        6 or 7 => "(?:" + Expr(depth + 1) + ")" + fuzzy[rng.Next(fuzzy.Length)],
        8 or 9 => "(?:" + Expr(depth + 1) + ")" + quants[rng.Next(quants.Length)],
        10 => "(" + Expr(depth + 1) + ")",
        11 => "(?=" + Expr(depth + 1) + ")",
        12 => "(?!" + Expr(depth + 1) + ")",
        13 => "(?>" + Expr(depth + 1) + ")",
        14 => "\\1",
        15 => "(?(1)" + Expr(depth + 1) + "|" + Expr(depth + 1) + ")",
        16 => "(*PRUNE)",
        17 => "(*SKIP)",
        18 => "(?<=a)",
        19 => "(?:" + Expr(depth + 1) + "|" + Expr(depth + 1) + ")",
        20 => "(?:" + Expr(depth + 1) + ")" + fuzzy[rng.Next(fuzzy.Length)] + quants[rng.Next(quants.Length)],
        _ => "(?:(?R)|)",
    };
}

string Expr(int depth)
{
    var sb = new StringBuilder();
    int n = 1 + rng.Next(3);
    for (int i = 0; i < n; i++)
        sb.Append(Atom(depth));
    return sb.ToString();
}

string Pattern()
{
    string[] flags = ["", "", "", "(?b)", "(?e)", "(?r)"];
    // Group 1 first so \1, (?1) and (?(1)...) always compile; a fuzzy section with a call inside.
    return flags[rng.Next(flags.Length)]
        + "("
        + Expr(1)
        + ")"
        + "(?:"
        + Expr(1)
        + "(?R)?"
        + Expr(1)
        + ")"
        + fuzzy[rng.Next(fuzzy.Length)];
}

var subjects = new List<string> { "" };
for (int len = 1; len <= 3; len++)
{
    foreach (string s in subjects.Where(s => s.Length == len - 1).ToList())
    foreach (char c in "abx")
        subjects.Add(s + c);
}
subjects.AddRange(["abab", "baxba", "aabba", "xabax"]);

string Answer(FuzzyRegex regex, string subject, int mode, bool memo, int keyMode)
{
    probe.GetMethod("Reset")!.Invoke(null, null);
    probe.GetField("KeyMode")!.SetValue(null, keyMode);
    probe.GetField("Memo")!.SetValue(null, memo);
    try
    {
        var t = TimeSpan.FromMilliseconds(250);
        Match m = mode switch
        {
            0 => regex.Match(subject, timeout: t),
            1 => regex.MatchAtStart(subject, timeout: t),
            2 => regex.FullMatch(subject, timeout: t),
            _ => regex.Match(subject, partial: true, timeout: t),
        };
        if (!m.Success)
            return "none";
        var sb = new StringBuilder();
        for (int g = 0; g < m.Groups.Count; g++)
        {
            var grp = m.Groups[g];
            sb.Append(grp.Success ? $"({grp.Index},{grp.Index + grp.Length})" : "(-)");
        }
        sb.Append(
            $" {m.FuzzyCounts.Substitutions}s{m.FuzzyCounts.Insertions}i{m.FuzzyCounts.Deletions}d {m.PartialMatch} {string.Join(",", m.FuzzyChanges.Substitutions)}|{string.Join(",", m.FuzzyChanges.Insertions)}|{string.Join(",", m.FuzzyChanges.Deletions)}"
        );
        sb.Append(" ##");
        for (int g = 0; g < m.Groups.Count; g++)
        {
            foreach (var cap in m.Groups[g].Captures)
                sb.Append($"[{cap.Index},{cap.Length}]");
            sb.Append(';');
        }
        return sb.ToString();
    }
    catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
    {
        return "TIMEOUT";
    }
    catch (Exception e)
    {
        return "EXC " + e.GetType().Name;
    }
    finally
    {
        probe.GetField("Memo")!.SetValue(null, false);
    }
}

string Features(string p)
{
    var f = new List<string>();
    if (p.Contains("(*"))
        f.Add("verb");
    if (p.Contains("(?<="))
        f.Add("lookbehind");
    if (p.StartsWith("(?r)"))
        f.Add("reverse");
    if (p.StartsWith("(?b)"))
        f.Add("bestmatch");
    if (p.StartsWith("(?e)"))
        f.Add("enhancematch");
    if (p.Contains(@"\1"))
        f.Add("backref");
    if (p.Contains("(?(1)"))
        f.Add("conditional");
    if (p.Contains("(?>"))
        f.Add("atomic");
    if (p.Contains("(?=") || p.Contains("(?!"))
        f.Add("lookahead");
    return string.Join("+", f);
}

var watch = Stopwatch.StartNew();
long rows = 0,
    skipped = 0,
    hitRows = 0,
    compileErrors = 0,
    core = 0,
    capturesOnly = 0,
    partialDiffs = 0;
var coreByFeatures = new Dictionary<string, long>();
var capByFeatures = new Dictionary<string, long>();
var shown = new List<string>();
long offTimeouts = 0,
    bothTimeouts = 0;
var slowShapes = new List<string>();
var capExamples = new List<(int, string)>();
int done = 0;
for (int p = 0; p < patternCount && watch.Elapsed.TotalMinutes < minutes; p++, done++)
{
    string pattern = Pattern();
    FuzzyRegex regex;
    try
    {
        regex = new FuzzyRegex(pattern);
    }
    catch
    {
        compileErrors++;
        continue;
    }
    foreach (string subject in subjects)
    {
        for (int mode = 0; mode < 4; mode++)
        {
            string off = Answer(regex, subject, mode, false, 1);
            if (off == "TIMEOUT" && mode != 3)
            {
                offTimeouts++;
                string onT = Answer(regex, subject, mode, true, 1);
                if (onT == "TIMEOUT")
                {
                    bothTimeouts++;
                    if (slowShapes.Count < 6 && !slowShapes.Contains(pattern))
                        slowShapes.Add(pattern);
                }
            }
            if (off == "TIMEOUT" || off.StartsWith("EXC"))
            {
                skipped++;
                continue;
            }
            rows++;
            string on = Answer(regex, subject, mode, true, 1);
            if ((long)probe.GetField("MemoHits")!.GetValue(null)! > 0)
                hitRows++;
            if (on == "TIMEOUT" || on == off)
                continue;
            if (mode == 3)
            {
                partialDiffs++;
                continue;
            }
            string offCore = off.Split(" ##")[0],
                onCore = on.Split(" ##")[0];
            string f = Features(pattern);
            if (offCore != onCore)
            {
                core++;
                coreByFeatures[f] = coreByFeatures.GetValueOrDefault(f) + 1;
                if (shown.Count < 12 && !shown.Any(x => x.Contains(pattern)))
                    shown.Add($"mode {mode} {pattern} '{subject}': off {offCore} | on {onCore}");
            }
            else
            {
                capturesOnly++;
                capByFeatures[f] = capByFeatures.GetValueOrDefault(f) + 1;
                capExamples.Add((pattern.Length, $"mode {mode} {pattern} '{subject}': off {off} | on {on}"));
            }
        }
    }
}

Console.WriteLine(
    $"seed {seed}: {done} patterns ({compileErrors} did not compile), {rows} rows, {skipped} skipped (timeout or exception with the memo off), {hitRows} rows where the memo fired ({watch.Elapsed.TotalSeconds:F0} s)"
);
Console.WriteLine(
    $"minimal key: span/group/fuzzy answer changed {core}; only the capture lists changed {capturesOnly}; partial-search rows changed {partialDiffs}"
);
Console.WriteLine(
    $"non-partial rows over 250 ms with the memo off {offTimeouts}, still over 250 ms with it on {bothTimeouts}"
);
foreach (string x in slowShapes)
    Console.WriteLine("  still slow: " + x);
Console.WriteLine(
    "core changes by feature set: "
        + string.Join("; ", coreByFeatures.OrderByDescending(p => p.Value).Select(p => $"[{p.Key}] {p.Value}"))
);
Console.WriteLine(
    "capture-only changes by feature set: "
        + string.Join("; ", capByFeatures.OrderByDescending(p => p.Value).Take(8).Select(p => $"[{p.Key}] {p.Value}"))
);
foreach (var (_, x) in capExamples.OrderBy(e => e.Item1).Take(3))
    Console.WriteLine("  capture-only: " + (x.Length > 400 ? x[..400] : x));
foreach (string x in shown)
    Console.WriteLine("  " + (x.Length > 260 ? x[..260] : x));
