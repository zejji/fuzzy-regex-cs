#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
#:property PublishAot=false
#:property RunAnalyzers=false
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
// D11 chunk 9. Run: dotnet run -c Debug tools/probes/d11-port-judge-grid.cs -- rows.jsonl
// Per row: the port's partial answer, the port's own non-partial judge over continuations of up to 3
// characters, and (Debug) the partial-event counts of the partial call.
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

Trace.Listeners.Clear();
Trace.Listeners.Add(new ThrowListener());
const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
var asm = typeof(FuzzyRegex).Assembly;
var msType = asm.GetType("Fuzzy.Text.RegularExpressions.Engine.MatchState")!;
var matcher = asm.GetType("Fuzzy.Text.RegularExpressions.Engine.Matcher")!;
var evType = asm.GetType("Fuzzy.Text.RegularExpressions.Engine.PartialEvent")!;
var create = msType.GetMethod("Create", Any)!;
var doMatch = matcher.GetMethod("DoMatch", Any)!;
var count = msType.GetMethod("PartialEventCount", Any)!;
var names = Enum.GetNames(evType);
var totals = new Dictionary<string, long>();
var timeout = TimeSpan.FromSeconds(2);
const string Alphabet = "ab1א :\"\n";
foreach (var line in File.ReadLines(args[0]))
{
    if (string.IsNullOrWhiteSpace(line))
        continue;
    using var doc = JsonDocument.Parse(line);
    var r = doc.RootElement;
    string pattern = r.GetProperty("pattern").GetString()!,
        t = r.GetProperty("subject").GetString()!,
        op = r.GetProperty("operation").GetString()!;
    string answer,
        judge,
        events = "";
    try
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, timeout);
        Match Run(string s, bool partial) =>
            op switch
            {
                "match" => regex.MatchAtStart(s, partial: partial),
                "fullmatch" => regex.FullMatch(s, partial: partial),
                _ => regex.Match(s, partial: partial),
            };
        Match m = Run(t, true);
        answer = m.Success ? $"{(m.PartialMatch ? "P" : "F")}({m.Index},{m.Index + m.Length})" : "None";
        bool reverse = pattern.StartsWith("(?r)");
        Match full = Run(t, false);
        if (full.Success)
            judge = $"F({full.Index},{full.Index + full.Length})";
        else
        {
            var alpha = Alphabet.Union(pattern.Where(c => c < 128 && char.IsLetterOrDigit(c))).Distinct().ToArray();
            int best = op == "search" ? (reverse ? -1 : int.MaxValue) : -1;
            bool found = false;
            IEnumerable<string> Words()
            {
                foreach (var a in alpha)
                    yield return a.ToString();
                foreach (var a in alpha)
                foreach (var b in alpha)
                    yield return $"{a}{b}";
                foreach (var a in alpha)
                foreach (var b in alpha)
                foreach (var c in alpha)
                    yield return $"{a}{b}{c}";
            }
            foreach (var w in Words())
            {
                string text = reverse ? w + t : t + w;
                Match c = Run(text, false);
                if (!c.Success)
                    continue;
                found = true;
                if (op != "search")
                    break;
                if (reverse)
                    best = Math.Max(best, Math.Max(c.Index + c.Length - w.Length, 0));
                else
                    best = Math.Min(best, Math.Min(c.Index, t.Length));
            }
            judge =
                !found ? "None"
                : op != "search" ? $"P(0,{t.Length})"
                : reverse ? $"P(0,{best})"
                : $"P({best},{t.Length})";
        }
        // Debug events of the partial call.
        var po = typeof(FuzzyRegex).GetProperty("PatternObject", Any)!.GetValue(regex);
        var limits = typeof(FuzzyRegex).GetProperty("PatternLimits", Any)!.GetValue(regex);
        var parms = create.GetParameters();
        var a2 = new object?[parms.Length];
        a2[0] = po;
        a2[1] = t.AsMemory();
        a2[2] = 0;
        a2[3] = t.Length;
        a2[4] = false;
        a2[5] = true;
        a2[6] = true;
        a2[7] = op == "fullmatch";
        a2[8] = limits;
        for (int i = 9; i < a2.Length; i++)
            a2[i] = Type.Missing;
        var state = create.Invoke(null, BindingFlags.OptionalParamBinding, null, a2, null)!;
        _ = doMatch.Invoke(null, [state, op == "search"]);
        var parts = new List<string>();
        foreach (var n in names)
        {
            long c = (long)count.Invoke(state, [Enum.Parse(evType, n)])!;
            if (c != 0)
            {
                parts.Add($"{n}={c}");
                totals[n] = totals.GetValueOrDefault(n) + 1;
            }
        }
        events = string.Join(",", parts);
        (state as IDisposable)?.Dispose();
    }
    catch (Exception e)
    {
        var x = e is TargetInvocationException { InnerException: { } ie } ? ie : e;
        answer = "ERR:" + x.GetType().Name + ":" + x.Message.Split('\n')[0];
        judge = "?";
    }
    Console.WriteLine(
        $"{op}\t{JsonSerializer.Serialize(pattern, Opt.J)}\t{JsonSerializer.Serialize(t, Opt.J)}\t{answer}\t{judge}\t{events}"
    );
}
foreach (var kv in totals.OrderBy(k => k.Key))
    Console.Error.WriteLine($"rows with {kv.Key}: {kv.Value}");

sealed class ThrowListener : TraceListener
{
    public override void Fail(string? message, string? detail) =>
        throw new InvalidOperationException("ASSERT: " + message);

    public override void Write(string? message) { }

    public override void WriteLine(string? message) { }
}

static class Opt
{
    public static readonly JsonSerializerOptions J = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
