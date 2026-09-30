#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
#:property PublishAot=false
#:property RunAnalyzers=false
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property Nullable=enable
// The complete matrix's port half: every answer the port gives a row, for checks C2-C6.
//
//     dotnet run -c Debug tools/matrix/port-runner.cs -- ROWS.jsonl OUT.jsonl [--start N] [--ablate NAME]
//
// RUN IT ON A DEBUG BUILD (check C3): a failed Debug.Assert throws here and the row answers
// "ASSERT <message>". Every call has a step cap (WorkCounter.StepLimit, Debug only) and a 2 s
// timeout, and a row stops asking once it has spent 20 s; set DOTNET_GCHeapHardLimit on the process
// so a runaway row fails with OutOfMemory rather than taking the machine (a capless Debug probe
// reached 14 GB on 2026-09-29).
//
// Per row it writes one JSON line: `base` (the row's question), `off` and `on` (check C5: the
// failed-call memo and the repeat failure memo forced off; the failed-call memo on from the first
// call), `wo` (check C2: each written-out form, by depth), and `judge` (check C4, partial rows only:
// the port's own non-partial answers over every continuation of up to 3 characters, the approach of
// d11-port-judge-grid.cs on maint/d11-partial-boundary).
//
// --ablate switches one known bug back on, through the same internal switches the oracle's
// ablations use, so the validation controls can prove each check fires: d37 (upstream's
// whole-pattern call, WITHOUT the exemption from the closing assert), callfeatures (D40's
// upstream call features, on the base answer only), exactdeletion (ledger entry 42 off), and
// unsafememo (the failed-call memo forced on where the compiler turned it off, on `on` only).
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

Trace.Listeners.Clear();
Trace.Listeners.Add(new MatrixRunner.ThrowListener());

string input = args[0];
string output = args[1];
int start = 0;
string ablate = "none",
    runAblate = "none";
for (int a = 2; a + 1 < args.Length; a += 2)
{
    if (args[a] == "--start")
        start = int.Parse(args[a + 1], CultureInfo.InvariantCulture);
    else if (args[a] == "--ablate")
        runAblate = args[a + 1];
}

const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
Assembly asm = typeof(FuzzyRegex).Assembly;
MethodInfo withDefaultVersion = typeof(FuzzyRegex).GetMethod("WithDefaultVersion", Any)!;
PropertyInfo patternObjectProperty = typeof(FuzzyRegex).GetProperty("PatternObject", Any)!;
Type po = patternObjectProperty.PropertyType;
FieldInfo F(string name) => po.GetField(name, Any) ?? throw new MissingFieldException(po.Name, name);
FieldInfo skipCallMemo = F("SkipCallMemo"),
    eagerCallMemo = F("EagerCallMemo"),
    useCallMemo = F("UseCallMemo");
FieldInfo repeatInfoList = F("RepeatInfoList");
FieldInfo skipExactDeletion = F("SkipExactDeletionRetry"),
    upstreamEmpty = F("UpstreamEmptyIterations");
FieldInfo minimumOrder = F("CheckMinimumBeforeTrailingInsertions");
FieldInfo minWidth = F("MinWidth"),
    upstreamMinWidth = F("UpstreamMinWidth");
FieldInfo skipLookaroundInsertion = F("SkipLookaroundInsertion"),
    anchorGuards = F("AnchorGuards"),
    upstreamDefaultBoundary = F("UpstreamDefaultBoundary"),
    skipTiming = F("SkipMovesTheSliceWhenItRuns"),
    verbScope = F("VerbsAreConfinedToTheInnermostGroup"),
    doubledInsertions = F("DoubleCountTrailingInsertions");

// Check C1x's pinned divergences: the fuzzy ablations OracleComparer's RunWith.../RunWithout...
// helpers apply for ExpectedDivergences, one variant each. A C1x row whose difference from upstream
// one of these takes away is accounted for, as check C1 accounts for it (matrix triage 2026-09-30).
string[] pinned =
[
    "x42",
    "x44",
    "x51",
    "x50",
    "xanchor",
    "xboundary",
    "xskip",
    "xverbscope",
    "xcallfeatures",
    "xdoubled",
];
Type repeatInfo = asm.GetType("Fuzzy.Text.RegularExpressions.Engine.RepeatInfo", throwOnError: true)!;
FieldInfo failureMemo = repeatInfo.GetField("FailureMemo", Any)!;
Type workCounter = asm.GetType("Fuzzy.Text.RegularExpressions.Engine.WorkCounter", throwOnError: true)!;
PropertyInfo steps = workCounter.GetProperty("Steps", Any)!;
PropertyInfo stepLimit = workCounter.GetProperty("StepLimit", Any)!;
int version0 = (int)FuzzyRegexOptions.Version0;

// Proof the listener throws, and that this is a Debug build (the step counter only counts there).
try
{
    Debug.Assert(false, "listener check");
    Console.Error.WriteLine("WARNING: Debug.Assert did not throw - not a Debug build; C3 is blind");
}
catch (MatrixRunner.AssertFailedException) { }

var timeout = TimeSpan.FromSeconds(2);
const long StepCap = 20_000_000;
const double RowBudgetSeconds = 20;
const string Alphabet = "ab1א :\"\n";
var json = new JsonSerializerOptions
{
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

// Per-row state the local functions below read.
FuzzyRegexOptions options = FuzzyRegexOptions.None;
var named = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
var clock = new Stopwatch();

using var writer = new StreamWriter(output, append: start > 0, new UTF8Encoding(false))
{
    NewLine = "\n",
    AutoFlush = true,
};
int index = -1;
foreach (string line in File.ReadLines(input))
{
    if (string.IsNullOrWhiteSpace(line))
        continue;
    index++;
    if (index < start)
        continue;
    using JsonDocument doc = JsonDocument.Parse(line);
    JsonElement row = doc.RootElement;
    clock.Restart();
    // A validation control names its own ablation; every other row takes the run's.
    ablate = row.TryGetProperty("ablate", out JsonElement ab) ? ab.GetString()! : runAblate;
    string pattern = row.GetProperty("pattern").GetString()!;
    options = (FuzzyRegexOptions)row.GetProperty("flags").GetInt32();
    string subject = row.GetProperty("subject").GetString()!;
    string op = row.GetProperty("operation").GetString()!;
    bool partial = row.TryGetProperty("partial", out JsonElement p) && p.GetBoolean();
    int? pos =
        row.TryGetProperty("pos", out JsonElement pe) && pe.ValueKind == JsonValueKind.Number ? pe.GetInt32() : null;
    int? endpos =
        row.TryGetProperty("endpos", out JsonElement ee) && ee.ValueKind == JsonValueKind.Number ? ee.GetInt32() : null;
    named = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
    if (row.TryGetProperty("namedLists", out JsonElement nl) && nl.ValueKind == JsonValueKind.Object)
        foreach (JsonProperty list in nl.EnumerateObject())
            named[list.Name] = list.Value.EnumerateArray().Select(static v => v.GetString()!).ToArray();

    var result = new Dictionary<string, object?>
    {
        ["id"] = row.TryGetProperty("id", out JsonElement id) ? id.GetInt32() : index,
    };
    bool Spent() => clock.Elapsed.TotalSeconds > RowBudgetSeconds;

    result["base"] = Answer(pattern, subject, op, partial, pos, endpos, "base");
    result["off"] = Spent() ? "ERR RowBudget" : Answer(pattern, subject, op, partial, pos, endpos, "off");
    result["on"] = Spent() ? "ERR RowBudget" : Answer(pattern, subject, op, partial, pos, endpos, "on");
    if (row.TryGetProperty("writtenOut", out JsonElement wo) && wo.ValueKind == JsonValueKind.Object)
    {
        var forms = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty form in wo.EnumerateObject())
            forms[form.Name] = Spent()
                ? "ERR RowBudget"
                : Answer(form.Value.GetString()!, subject, op, partial, pos, endpos, "wo");
        result["wo"] = forms;
    }
    // A row may also ask for them itself (`askPinned`), for triaging a C1 row by hand.
    if (
        (op == "finditer" && (partial || pos is not null || endpos is not null))
        || (row.TryGetProperty("askPinned", out JsonElement ap) && ap.GetBoolean())
    )
    {
        var forms = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string v in pinned)
            forms[v] = Spent() ? "ERR RowBudget" : Answer(pattern, subject, op, partial, pos, endpos, v);
        result["pinned"] = forms;
    }
    if (partial && op != "finditer" && pos is null && endpos is null)
        result["judge"] = Spent() ? "ERR RowBudget" : Judge(pattern, subject, op);
    result["ms"] = (int)clock.ElapsedMilliseconds;
    writer.WriteLine(JsonSerializer.Serialize(result, json));
}

FuzzyRegex Compile(string pattern, string variant)
{
    bool d37 = ablate == "d37" && variant != "wo";
    bool callFeatures = (ablate == "callfeatures" && variant == "base") || variant == "xcallfeatures";
    var regex = (FuzzyRegex)
        withDefaultVersion.Invoke(
            null,
            [
                pattern,
                options,
                FuzzyRegex.InfiniteMatchTimeout,
                named.Count > 0 ? named : null,
                version0,
                false,
                false,
                d37,
                callFeatures,
            ]
        )!;
    object pobj = patternObjectProperty.GetValue(regex)!;
    if (d37 || callFeatures || (ablate == "exactdeletion" && variant != "wo"))
    {
        // As the oracle's named ablations do: ledger entries 42 and 44 off, entry 51's order kept.
        skipExactDeletion.SetValue(pobj, true);
        upstreamEmpty.SetValue(pobj, true);
        minimumOrder.SetValue(pobj, false);
    }
    if (callFeatures)
        minWidth.SetValue(pobj, upstreamMinWidth.GetValue(pobj));
    if (Array.IndexOf(pinned, variant) >= 0)
    {
        // OracleComparer.Run's withoutTheFuzzySearchFixes: entry 42 off always; entry 44 off except
        // for RunWithoutTheExactDeletion; entry 51 off except for it and RunWithUpstreamEmptyIterations.
        skipExactDeletion.SetValue(pobj, true);
        upstreamEmpty.SetValue(pobj, variant != "x42");
        minimumOrder.SetValue(pobj, variant is not ("x42" or "x44"));
        if (variant is "x51" or "x50")
            skipLookaroundInsertion.SetValue(pobj, true); // RunWithTheUpstreamMinimumOrder sets both
        if (variant == "xanchor")
            anchorGuards.SetValue(pobj, null);
        if (variant == "xboundary")
            upstreamDefaultBoundary.SetValue(pobj, true);
        if (variant == "xskip")
            skipTiming.SetValue(pobj, true); // RunWithTheUpstreamSkipTiming
        if (variant == "xverbscope")
            verbScope.SetValue(pobj, true); // RunWithTheUpstreamVerbScope
        if (variant == "xdoubled")
            doubledInsertions.SetValue(pobj, true); // RunWithTheDoubledInsertionGuard
    }
    if (variant == "off")
    {
        skipCallMemo.SetValue(pobj, true);
        foreach (object info in (System.Collections.IEnumerable)repeatInfoList.GetValue(pobj)!)
            failureMemo.SetValue(info, false);
    }
    else if (variant == "on")
    {
        eagerCallMemo.SetValue(pobj, true);
        if (ablate == "unsafememo")
            useCallMemo.SetValue(pobj, true);
        // Every repeat's failure memo on, including where Optimiser.KeepFailureMemosSound withdrew
        // it (backreferences, calls, fuzzy, verbs, POSIX).
        if (ablate == "unsaferepeatmemo")
            foreach (object info in (System.Collections.IEnumerable)repeatInfoList.GetValue(pobj)!)
                failureMemo.SetValue(info, true);
    }
    return regex;
}

string Answer(string pattern, string subject, string op, bool partial, int? pos, int? endpos, string variant)
{
    FuzzyRegex regex;
    try
    {
        regex = Compile(pattern, variant);
    }
    catch (TargetInvocationException e) when (e.InnerException is { } inner)
    {
        return Failure(inner, compiling: true);
    }
    catch (Exception e)
    {
        return Failure(e, compiling: true);
    }
    int beginning = pos ?? 0;
    int length = pos is null && endpos is null ? -1 : Math.Max(0, (endpos ?? subject.Length) - beginning);
    try
    {
        Capped();
        if (op == "finditer")
        {
            var parts = regex
                .Matches(subject, beginning, length, overlapped: false, partial: partial, timeout: timeout)
                .Select(Describe)
                .ToList();
            return "[" + string.Join(" ; ", parts) + "]";
        }
        Match m = op switch
        {
            "search" => regex.Match(subject, beginning, length, partial, timeout),
            "match" => regex.MatchAtStart(subject, beginning, length, partial, timeout),
            _ => regex.FullMatch(subject, beginning, length, partial, timeout),
        };
        return m.Success ? Describe(m) : "None";
    }
    catch (Exception e)
    {
        return Failure(e, compiling: false);
    }
}

string Judge(string pattern, string t, string op)
{
    // d11-port-judge-grid.cs's judge: the port's non-partial answer, or the best partial any
    // continuation of up to 3 characters completes, in upstream's partial conventions.
    try
    {
        FuzzyRegex regex = Compile(pattern, "base");
        bool reverse =
            (options & FuzzyRegexOptions.RightToLeft) != 0 || pattern.StartsWith("(?r)", StringComparison.Ordinal);
        Match Run(string s) =>
            op switch
            {
                "match" => regex.MatchAtStart(s, timeout: timeout),
                "fullmatch" => regex.FullMatch(s, timeout: timeout),
                _ => regex.Match(s, timeout: timeout),
            };
        Capped();
        Match full = Run(t);
        if (full.Success)
            return $"F({full.Index},{full.Index + full.Length})";
        char[] alpha = Alphabet
            .Union(pattern.Where(static c => c < 128 && char.IsLetterOrDigit(c)))
            .Distinct()
            .ToArray();
        if (op == "search" && (regex.Options & (FuzzyRegexOptions.BestMatch | FuzzyRegexOptions.EnhanceMatch)) != 0)
            return LeftmostLive(regex, t, reverse, alpha);
        int best = op == "search" ? (reverse ? -1 : int.MaxValue) : -1;
        bool found = false;
        foreach (string w in Words(alpha))
        {
            if (clock.Elapsed.TotalSeconds > RowBudgetSeconds)
                return "ERR RowBudget";
            Capped();
            string text = reverse ? w + t : t + w;
            Match c = Run(text);
            if (!c.Success)
                continue;
            found = true;
            if (op != "search")
                break;
            best = reverse
                ? Math.Max(best, Math.Max(c.Index + c.Length - w.Length, 0))
                : Math.Min(best, Math.Min(c.Index, t.Length));
        }
        return !found ? "None"
            : op != "search" ? $"P(0,{t.Length})"
            : reverse ? $"P(0,{best})"
            : $"P({best},{t.Length})";
    }
    catch (Exception e)
    {
        return Failure(e is TargetInvocationException { InnerException: { } i } ? i : e, compiling: false);
    }
}

// The partial start under BESTMATCH or ENHANCEMATCH (owner ruling 2026-09-30, option A): the least
// start (greatest end, reversed) from which some continuation matches, asked as an anchored match at
// each start, as d11-brute-judge.py's leftmost_live asks upstream. Those flags make `search` rank
// matches rather than return the leftmost, so the least start of `search(t + w)` is not the rule.
// SHORTCUT: reversed, the end is fixed by the slice, which hides a lookahead's view past it; see the
// Python judge for the ceiling and the upgrade.
string LeftmostLive(FuzzyRegex regex, string t, bool reverse, char[] alpha)
{
    string[] words = [.. Words(alpha)];
    if (!reverse)
    {
        for (int s = 0; s <= t.Length; s++)
            foreach (string w in words)
            {
                if (clock.Elapsed.TotalSeconds > RowBudgetSeconds)
                    return "ERR RowBudget";
                Capped();
                string text = t + w;
                Match m =
                    s == t.Length
                        ? regex.Match(text, s, text.Length - s, timeout: timeout)
                        : regex.MatchAtStart(text, s, text.Length - s, timeout: timeout);
                if (m.Success)
                    return $"P({s},{t.Length})";
            }
        return "None";
    }
    for (int e = t.Length; e >= 0; e--)
        foreach (string w in words)
        {
            if (clock.Elapsed.TotalSeconds > RowBudgetSeconds)
                return "ERR RowBudget";
            Capped();
            string text = w + t;
            Match m =
                e == 0
                    ? regex.Match(text, 0, w.Length, timeout: timeout)
                    : regex.MatchAtStart(text, 0, w.Length + e, timeout: timeout);
            if (m.Success)
                return $"P(0,{e})";
        }
    return "None";
}

static IEnumerable<string> Words(char[] alpha)
{
    foreach (char a in alpha)
        yield return a.ToString();
    foreach (char a in alpha)
    foreach (char b in alpha)
        yield return string.Concat(a, b);
    foreach (char a in alpha)
    foreach (char b in alpha)
    foreach (char c in alpha)
        yield return string.Concat(a, b, c);
}

void Capped() => stepLimit.SetValue(null, (long)steps.GetValue(null)! + StepCap);

static string Describe(Match m)
{
    var sb = new StringBuilder();
    sb.Append(CultureInfo.InvariantCulture, $"({m.Index},{m.Index + m.Length})");
    if (m.PartialMatch)
        sb.Append('P');
    FuzzyCounts c = m.FuzzyCounts;
    sb.Append(CultureInfo.InvariantCulture, $" {c.Substitutions},{c.Insertions},{c.Deletions}");
    FuzzyChanges ch = m.FuzzyChanges;
    sb.Append(" [")
        .Append(string.Join(",", ch.Substitutions))
        .Append('|')
        .Append(string.Join(",", ch.Insertions))
        .Append('|')
        .Append(string.Join(",", ch.Deletions))
        .Append(']');
    sb.Append(" g");
    for (int g = 1; g < m.Groups.Count; g++)
    {
        Group grp = m.Groups[g];
        sb.Append(grp.Success ? $"({grp.Index},{grp.Index + grp.Length})" : "(-)");
    }
    sb.Append(" c");
    for (int g = 1; g < m.Groups.Count; g++)
    {
        sb.Append('[');
        foreach (Capture cap in m.Groups[g].Captures)
            sb.Append(CultureInfo.InvariantCulture, $"({cap.Index},{cap.Index + cap.Length})");
        sb.Append(']');
    }
    return sb.ToString();
}

static string Failure(Exception e, bool compiling)
{
    Exception x = e is TargetInvocationException { InnerException: { } inner } ? inner : e;
    string first = x.Message.Split('\n')[0];
    if (first.Length > 200)
        first = first[..200];
    return x switch
    {
        MatrixRunner.AssertFailedException => "ASSERT " + first,
        System.Text.RegularExpressions.RegexMatchTimeoutException => "ERR Timeout",
        _ when x.GetType().Name == "StepLimitReachedException" => "ERR StepLimit",
        // A rejection is an answer (C1 compares it with upstream's); anything else from the compiler
        // is a crash.
        ArgumentException when compiling => "REJECT " + x.GetType().Name,
        _ => "EXC " + x.GetType().Name + (compiling ? " (compile) " : " ") + first + " @ " + Where(x),
    };
}

static string Where(Exception x)
{
    string? frame = x
        .StackTrace?.Split('\n')
        .FirstOrDefault(static f => f.Contains("FuzzyRegex", StringComparison.Ordinal));
    return frame?.Trim() ?? "?";
}

namespace MatrixRunner
{
    internal sealed class ThrowListener : TraceListener
    {
        public override void Write(string? message) { }

        public override void WriteLine(string? message) { }

        public override void Fail(string? message, string? detailMessage) =>
            throw new AssertFailedException(message ?? "");
    }

    public sealed class AssertFailedException(string message) : Exception(message);
}
