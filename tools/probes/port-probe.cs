#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// Answers a batch of rows with this port, one line each, for triaging oracle rows quickly.
//
// Input: a JSON-lines file of rows in the oracle's own shape (pattern, flags as upstream's int,
// namedLists, subject, operation, pos, endpos, partial, and for sub, subf and split the template
// and count). Each row is asked the recorder's question: its flags are passed as options, and a
// row that names no version, in its flags or inline, gets VERSION0, the default
// tools/record-oracle.py records under (as tests/FuzzyRegex.OracleTests/OracleComparer.cs does).
// Output per row: span, fuzzy counts and change positions, and every group's captures, or the
// exception. `finditer` and `finditer-overlapped` print every match, separated by " | ". A
// substitution prints its count and text, a split its parts.
//
// Run: dotnet run -c Release tools/probes/port-probe.cs -- rows.jsonl [timeout-seconds]
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

var timeout = TimeSpan.FromSeconds(args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 5);

// The oracle's own compile, which takes the default version separately from the options so that
// an inline (?V1) still wins. Internal, so reached by reflection: a file-based app is not one of
// the assemblies the library's InternalsVisibleTo names. S3011 is disapplied for that reason: the
// bypass is the point, in a triage tool that never ships.
#pragma warning disable S3011
MethodInfo withDefaultVersion =
    typeof(FuzzyRegex).GetMethod("WithDefaultVersion", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(nameof(FuzzyRegex), "WithDefaultVersion");
#pragma warning restore S3011
ParameterInfo[] compileParameters = withDefaultVersion.GetParameters();
const int recorderDefaultVersion = (int)FuzzyRegexOptions.Version0;

int order = 0;
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
    int flags = row.TryGetProperty("flags", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0;
    bool partial = row.TryGetProperty("partial", out var p) && p.ValueKind == JsonValueKind.True;
    int? pos = row.TryGetProperty("pos", out var ps) && ps.ValueKind == JsonValueKind.Number ? ps.GetInt32() : null;
    int? endpos =
        row.TryGetProperty("endpos", out var es) && es.ValueKind == JsonValueKind.Number ? es.GetInt32() : null;

    // Upstream's (pos, endpos) as this surface's (beginning, length), as the oracle's consumer reads
    // them: both absent is the whole subject, and an endpos below pos is an empty slice.
    int beginning = pos ?? 0;
    int length = pos is null && endpos is null ? -1 : Math.Max(0, (endpos ?? subject.Length) - beginning);
    int sliceLength = length < 0 ? subject.Length - beginning : length;

    // Upstream's count: 0 is no limit, which this surface spells -1.
    int count = row.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
    int limit = count switch
    {
        0 => -1,
        < 0 => 0,
        _ => count,
    };
    var lists = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
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
        var regex = Compile(pattern, (FuzzyRegexOptions)flags, lists);
        answer = op switch
        {
            "match" => Describe(regex.MatchAtStart(subject, beginning, sliceLength, partial)),
            "fullmatch" => Describe(regex.FullMatch(subject, beginning, sliceLength, partial)),
            "finditer" or "findall" or "finditer-overlapped" => DescribeAll(
                regex.Matches(
                    subject,
                    beginning,
                    sliceLength,
                    overlapped: string.Equals(op, "finditer-overlapped", StringComparison.Ordinal),
                    partial: partial
                )
            ),
            "sub" => DescribeSub(
                regex.Replace(subject, Template(row), limit, out int made, beginning, length, timeout),
                made
            ),
            "subf" => DescribeSub(
                regex.ReplaceFormat(subject, Template(row), limit, out int madeF, beginning, length, timeout),
                madeF
            ),
            "split" => "split " + string.Join(" ", regex.Split(subject, limit, timeout).Select(DescribePart)),
            _ => Describe(regex.Match(subject, beginning, sliceLength, partial)),
        };
    }
    catch (TargetInvocationException e) when (e.InnerException is not null)
    {
        answer = FirstLine(e.InnerException);
    }
    catch (Exception e)
    {
        answer = FirstLine(e);
    }

    Console.WriteLine($"{op}\t{Quote(pattern)}\t{Quote(subject)}\t{answer}\t{clock.ElapsedMilliseconds}ms");
}

FuzzyRegex Compile(string pattern, FuzzyRegexOptions options, Dictionary<string, IReadOnlyCollection<string>> lists)
{
    // The trailing parameters are the oracle's ablation switches, all off.
    var arguments = new object?[compileParameters.Length];
    arguments[0] = pattern;
    arguments[1] = options;
    arguments[2] = timeout;
    arguments[3] = lists;
    arguments[4] = recorderDefaultVersion;
    for (int i = 5; i < arguments.Length; i++)
    {
        arguments[i] = compileParameters[i].DefaultValue;
    }

    return (FuzzyRegex)withDefaultVersion.Invoke(null, arguments)!;
}

// The collection is lazy (2026-10-14), so how it is read decides which path finds the matches.
// Rows take turns: enumeration first, the indexer first, or Count first; then the other two must
// agree with the first, and a row where they do not says so instead of answering.
string DescribeAll(MatchCollection matches)
{
    List<string> found = [];
    switch (order++ % 3)
    {
        case 0:
            found.AddRange(matches.Select(Describe));
            break;
        case 1:
            bool more = true;
            while (more)
            {
                try
                {
                    found.Add(Describe(matches[found.Count]));
                }
                catch (ArgumentOutOfRangeException)
                {
                    more = false;
                }
            }

            break;
        default:
            found.AddRange(Enumerable.Range(0, matches.Count).Select(i => Describe(matches[i])));
            break;
    }

    bool agree =
        matches.Count == found.Count
        && matches.Select(Describe).SequenceEqual(found, StringComparer.Ordinal)
        && Enumerable
            .Range(0, found.Count)
            .All(i => string.Equals(Describe(matches[i]), found[i], StringComparison.Ordinal));

    if (!agree)
    {
        return "COLLECTION READS DISAGREE";
    }

    return found.Count == 0 ? "[]" : string.Join(" | ", found);
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

static string Template(JsonElement row) => row.GetProperty("template").GetString()!;

static string DescribeSub(string text, int made) => $"sub {made} {Quote(text)}";

static string DescribePart(string? part) => part is null ? "<null>" : Quote(part);

// The first line of ToString() is "Type: message", which is all a triage line needs.
static string FirstLine(Exception e) => e.ToString().Split('\n')[0].TrimEnd('\r');

// A JSON string literal without the reflection-based serializer, so the probe stays trim-safe. The
// relaxed encoder keeps < > + readable; the output goes to a terminal, never into HTML.
static string Quote(string s) => "\"" + JsonEncodedText.Encode(s, JavaScriptEncoder.UnsafeRelaxedJsonEscaping) + "\"";
