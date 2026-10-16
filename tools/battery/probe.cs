#:project ../src/FuzzyRegex/FuzzyRegex.csproj
#:property PublishAot=false
#:property RunAnalyzers=false
#:property TreatWarningsAsErrors=false
#:property EnforceCodeStyleInBuild=false
#:property Nullable=enable
// The battery's port probe: tools/probes/port-probe.cs with two changes. A trace listener turns a
// failed Debug.Assert into an exception, so a Debug build reports the row as
// "AssertFailedException: <message>" instead of ending the process (a failed assert calls
// Environment.FailFast without a listener; measured 2026-10-16, exit 35, "Process terminated").
// And each row is compiled with its own `flags` and the recorder's default version, V0, as upstream
// and Python re use when the flags name none (FuzzyRegex.WithDefaultVersion, which the oracle and
// tools/matrix/port-runner.cs call the same way). Run 20261016-1 compiled the bed under the port's
// V1 default, where an unescaped `[` inside a set opens a nested set: 589 rows threw.
//
// battery.py copies this file to <worktree>/.battery/probe.cs, so the #:project path above is
// relative to that folder, not to tools/battery/.
//
// Input: JSON lines (pattern, flags, subject, operation, partial, pos, endpos, namedLists). Output,
// one line per row:
//   op \t "pattern" \t "subject" \t answer \t <n>ms
// where answer is port-probe.cs's Describe text, "None", or "Type: message".
//
// Run: dotnet run -c Debug .battery/probe.cs -- rows.jsonl [timeout-seconds]
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

Trace.Listeners.Clear();
Trace.Listeners.Add(new ThrowListener());

MethodInfo withDefaultVersion = typeof(FuzzyRegex).GetMethod(
    "WithDefaultVersion",
    BindingFlags.NonPublic | BindingFlags.Static
)!;
var timeout = TimeSpan.FromSeconds(args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 5);
using var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = false, NewLine = "\n" };
int n = 0;
foreach (var line in File.ReadLines(args[0]))
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    using var doc = JsonDocument.Parse(line);
    var row = doc.RootElement;
    string pattern = row.GetProperty("pattern").GetString()!;
    string subject = row.GetProperty("subject").GetString()!;
    string op = row.GetProperty("operation").GetString()!;
    var flags = (FuzzyRegexOptions)(
        row.TryGetProperty("flags", out var fl) && fl.ValueKind == JsonValueKind.Number ? fl.GetInt32() : 0
    );
    bool partial = row.TryGetProperty("partial", out var p) && p.ValueKind == JsonValueKind.True;
    int pos = row.TryGetProperty("pos", out var ps) && ps.ValueKind == JsonValueKind.Number ? ps.GetInt32() : 0;
    int end =
        row.TryGetProperty("endpos", out var es) && es.ValueKind == JsonValueKind.Number
            ? es.GetInt32()
            : subject.Length;
    var lists = new Dictionary<string, IReadOnlyCollection<string>>();
    if (row.TryGetProperty("namedLists", out var nl) && nl.ValueKind == JsonValueKind.Object)
    {
        foreach (var entry in nl.EnumerateObject())
        {
            lists[entry.Name] = [.. entry.Value.EnumerateArray().Select(static v => v.GetString()!)];
        }
    }

    string answer;
    var clock = Stopwatch.StartNew();
    try
    {
        var regex = Compile(pattern, flags, timeout, lists);
        answer = op switch
        {
            "match" => Describe(regex.MatchAtStart(subject, pos, end - pos, partial)),
            "fullmatch" => Describe(regex.FullMatch(subject, pos, end - pos, partial)),
            "finditer" or "findall" or "finditer-overlapped" => DescribeAll(
                regex.Matches(
                    subject,
                    pos,
                    end - pos,
                    overlapped: string.Equals(op, "finditer-overlapped", StringComparison.Ordinal),
                    partial: partial
                )
            ),
            _ => Describe(regex.Match(subject, pos, end - pos, partial)),
        };
    }
    catch (Exception e)
    {
        answer = e.ToString().Split('\n')[0].TrimEnd('\r');
    }

    stdout.WriteLine($"{op}\t{Quote(pattern)}\t{Quote(subject)}\t{answer}\t{clock.ElapsedMilliseconds}ms");
    if (++n % 200 == 0)
    {
        stdout.Flush();
    }
}

stdout.Flush();

FuzzyRegex Compile(
    string pattern,
    FuzzyRegexOptions flags,
    TimeSpan limit,
    Dictionary<string, IReadOnlyCollection<string>> lists
)
{
    try
    {
        return (FuzzyRegex)
            withDefaultVersion.Invoke(
                null,
                [
                    pattern,
                    flags,
                    limit,
                    lists,
                    (int)FuzzyRegexOptions.Version0,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                ]
            )!;
    }
    catch (TargetInvocationException e) when (e.InnerException is not null)
    {
        // The row's answer names the parse error, as a direct constructor call would.
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(e.InnerException);
        throw;
    }
}

static string Describe(Match m)
{
    if (!m.Success)
    {
        return "None";
    }

    string partialMark = m.PartialMatch ? " partial" : "";
    FuzzyCounts counts = m.FuzzyCounts;
    FuzzyChanges changes = m.FuzzyChanges;
    string groups = string.Concat(
        Enumerable
            .Range(1, m.Groups.Count - 1)
            .Select(i =>
                $" g{i}="
                + string.Join(",", m.Groups[i].Captures.Select(static c => $"({c.Index},{c.Index + c.Length})"))
            )
    );

    return $"({m.Index},{m.Index + m.Length}) ({counts.Substitutions},{counts.Insertions},{counts.Deletions})"
        + $" s:{string.Join(",", changes.Substitutions)} i:{string.Join(",", changes.Insertions)}"
        + $" d:{string.Join(",", changes.Deletions)}{partialMark}{groups}";
}

// Unlike port-probe.cs, one read order only (enumeration): the battery asks about matching, and
// port-probe.cs's three-way collection check belongs to the collection's own tests.
static string DescribeAll(MatchCollection matches)
{
    List<string> found = [.. matches.Select(Describe)];
    return found.Count == 0 ? "[]" : string.Join(" | ", found);
}

static string Quote(string s) => "\"" + JsonEncodedText.Encode(s, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\"";

internal sealed class ThrowListener : TraceListener
{
    public override void Write(string? message) { }

    public override void WriteLine(string? message) { }

    public override void Fail(string? message, string? detailMessage) => throw new AssertFailedException(message ?? "");
}

internal sealed class AssertFailedException(string message) : Exception(message);
