#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// Answers a batch of rows with this port, one line each, for triaging oracle rows quickly.
//
// Input: a JSON-lines file of rows in the oracle's own shape (pattern, flags as upstream's int -
// ignored here except that inline flags carry them - subject, operation, namedLists, pos, endpos,
// partial). Output per row: span, fuzzy counts and group 1 captures, or the exception.
//
// Run: dotnet run tools/probes/port-probe.cs -- rows.jsonl [timeout-seconds]
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

var timeout = TimeSpan.FromSeconds(args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 5);
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
    var clock = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, timeout, lists);
        Match m = op switch
        {
            "match" => regex.MatchAtStart(subject, pos, end - pos, partial),
            "fullmatch" => regex.FullMatch(subject, pos, end - pos, partial),
            _ => regex.Match(subject, pos, end - pos, partial),
        };
        answer = Describe(m);
    }
    catch (Exception e)
    {
        // The first line of ToString() is "Type: message", which is all a triage line needs.
        answer = e.ToString().Split('\n')[0].TrimEnd('\r');
    }

    Console.WriteLine($"{op}\t{Quote(pattern)}\t{Quote(subject)}\t{answer}\t{clock.ElapsedMilliseconds}ms");
}

static string Describe(Match m)
{
    if (!m.Success)
    {
        return "None";
    }

    string partialMark = m.PartialMatch ? " partial" : "";
    string group1 = "";
    if (m.Groups.Count > 1)
    {
        group1 =
            " g1=" + string.Join(",", m.Groups[1].Captures.Select(static c => $"({c.Index},{c.Index + c.Length})"));
    }

    return $"({m.Index},{m.Index + m.Length}) {m.FuzzyCounts}{partialMark}{group1}";
}

// A JSON string literal without the reflection-based serializer, so the probe stays trim-safe. The
// relaxed encoder keeps < > + readable; the output goes to a terminal, never into HTML.
static string Quote(string s) => "\"" + JsonEncodedText.Encode(s, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\"";
