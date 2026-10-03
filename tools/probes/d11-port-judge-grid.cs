#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
#:property PublishAot=false
#:property RunAnalyzers=false
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
// D11 chunk 9. Run: dotnet run -c Debug tools/probes/d11-port-judge-grid.cs -- rows.jsonl
// Per row: the port's partial answer, the port's own non-partial judge over continuations of up to 3
// characters, and (Debug) the partial-event counts of the partial call. The standard error ends with
// the number of rows on which each event fired, every event listed, so a grid that never reaches a
// mechanism shows 0 for it (VERIFICATION.md rule 11).
//
// Operation "finditer" (a forward, non-overlapped scan): the answer is the scan's list; the judge is
// the non-partial scan of the held text, then a partial at the edge when some continuation gives
// the scan a further match from where the held text's scan ended (an empty match repeated at that
// position excluded, as the scan's must-advance rule does), starting at the earliest such start or
// the edge, whichever is first. Meant for patterns whose matches are empty, where a match of the
// held text cannot be a partial itself.
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
var advance = msType.GetMethod("AdvancePastMatch", Any)!;
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
        judge = "",
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
        static string Show(Match x) => $"{(x.PartialMatch ? "P" : "F")}({x.Index},{x.Index + x.Length})";
        bool reverse = pattern.StartsWith("(?r)");
        Match? full = op == "finditer" ? null : Run(t, false);
        if (op == "finditer")
        {
            answer = "[" + string.Join(" ", regex.Matches(t, partial: true).Select(Show)) + "]";
            var held = regex.Matches(t).ToList();
            int pos = held.Count == 0 ? 0 : held[^1].Index + held[^1].Length;
            bool lastEmpty = held.Count != 0 && held[^1].Length == 0;
            int best = int.MaxValue;
            var alpha = Alphabet.Union(pattern.Where(c => c < 128 && char.IsLetterOrDigit(c))).Distinct().ToArray();
            foreach (
                var w in alpha
                    .Select(a => a.ToString())
                    .Concat(alpha.SelectMany(a => alpha.Select(b => $"{a}{b}")))
                    .Concat(alpha.SelectMany(a => alpha.SelectMany(b => alpha.Select(c => $"{a}{b}{c}"))))
            )
            {
                foreach (Match c in regex.Matches(t + w))
                {
                    if (
                        c.Index < pos
                        || (c.Index == pos && c.Length == 0 && lastEmpty)
                        || held.Exists(h => h.Index == c.Index && h.Length == c.Length)
                    )
                        continue;
                    best = Math.Min(best, Math.Min(c.Index, t.Length));
                    break;
                }
            }
            var parts0 = held.Select(Show).ToList();
            if (best != int.MaxValue)
                parts0.Add($"P({best},{t.Length})");
            judge = "[" + string.Join(" ", parts0) + "]";
        }
        else
        {
            Match m = Run(t, true);
            answer = m.Success ? Show(m) : "None";
        }
        if (full is null) { }
        else if (full.Success)
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
        if (op == "finditer")
        {
            // Iteration.Count's loop: the counts add up over the scan's calls on one state.
            for (int turn = 0; turn <= t.Length + 2; turn++)
            {
                int st = (int)doMatch.Invoke(null, [state, true])!;
                if (st != 1)
                    break;
                advance.Invoke(state, null);
            }
        }
        else
            _ = doMatch.Invoke(null, [state, op != "match" && op != "fullmatch"]);
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
foreach (var n in names)
    Console.Error.WriteLine($"rows with {n}: {totals.GetValueOrDefault(n)}");

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
