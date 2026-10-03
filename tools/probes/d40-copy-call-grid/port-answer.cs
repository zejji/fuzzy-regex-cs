#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// D40: one answer line per JSON-lines row (pattern, subject), in upstream-answer.py's shape: span,
// fuzzy counts, then the span and capture list of every group named g<n>, in number order. Run it on
// a Debug build: a failed Debug.Assert throws, and its row answers "ERR ASSERT <message>".
using System.Globalization;
using System.Text.Json;
using D40Grid;
using Fuzzy.Text.RegularExpressions;

System.Diagnostics.Trace.Listeners.Clear();
System.Diagnostics.Trace.Listeners.Add(new AssertThrows());
if (args.Length > 1 && string.Equals(args[1], "--check-assert", StringComparison.Ordinal))
{
    System.Diagnostics.Debug.Assert(false, "the listener works");
}

// "--field inline" answers each row's written-out pattern instead; a row without one answers
// "ERR NoPattern".
string field = args.Length > 2 && string.Equals(args[1], "--field", StringComparison.Ordinal) ? args[2] : "pattern";

// "--ablate" compiles as the oracle's RunWithTheUpstreamCallFeatures does (upstream's call features
// and minimum width, D40-D49), through reflection since that switch is internal. It leaves the
// fuzzy-search fixes on, so it is for finding crashes and gross differences, not for exact answers.
bool ablate = args.Contains("--ablate", StringComparer.Ordinal);
#pragma warning disable S3011
var withDefaultVersion = typeof(FuzzyRegex).GetMethod(
    "WithDefaultVersion",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
)!;
var timeout = TimeSpan.FromSeconds(2);
foreach (var line in File.ReadLines(args[0]))
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    using var doc = JsonDocument.Parse(line);
    var row = doc.RootElement;
    string? pattern = row.GetProperty(field).GetString();
    if (pattern is null)
    {
        Console.WriteLine("ERR NoPattern");
        continue;
    }

    string subject = row.GetProperty("subject").GetString()!;
    string answer;
    try
    {
        FuzzyRegex regex = ablate
            ? Ablated(withDefaultVersion, pattern, timeout)
            : new FuzzyRegex(pattern, FuzzyRegexOptions.None, timeout);
        Match m = regex.Match(subject);
        if (!m.Success)
        {
            answer = "None";
        }
        else
        {
            var named = new SortedDictionary<int, Group>();
            for (int g = 1; g < m.Groups.Count; g++)
            {
                if (m.Groups[g].Name.StartsWith('g'))
                {
                    named[int.Parse(m.Groups[g].Name[1..], CultureInfo.InvariantCulture)] = m.Groups[g];
                }
            }

            string spans = string.Concat(
                named.Values.Select(static g => g.Success ? $"({g.Index},{g.Index + g.Length})" : "(-1,-1)")
            );
            string caps = string.Concat(
                named.Values.Select(static g =>
                    "[" + string.Concat(g.Captures.Select(static c => $"({c.Index},{c.Index + c.Length})")) + "]"
                )
            );
            answer =
                $"({m.Index},{m.Index + m.Length}) {m.FuzzyCounts.Substitutions},{m.FuzzyCounts.Insertions},{m.FuzzyCounts.Deletions} {spans} {caps}";
        }
    }
    catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
    {
        answer = "ERR Timeout";
    }
    catch (AssertFailedException e)
    {
        answer = "ERR ASSERT " + e.Message;
    }
    catch (Exception e)
    {
        answer = "ERR " + e.GetType().Name;
    }

    Console.WriteLine(answer);
}

// S3011 is disabled on purpose: this probe reaches the oracle's internal switch, which the library never sets.
#pragma warning disable S3011
static FuzzyRegex Ablated(System.Reflection.MethodInfo withDefaultVersion, string pattern, TimeSpan timeout)
{
    var regex = (FuzzyRegex)
        withDefaultVersion.Invoke(
            null,
            [pattern, FuzzyRegexOptions.None, timeout, null, (int)FuzzyRegexOptions.Version1, false, false, false, true]
        )!;
    const System.Reflection.BindingFlags Any =
        System.Reflection.BindingFlags.NonPublic
        | System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.Instance;
    object patternObject = typeof(FuzzyRegex).GetProperty("PatternObject", Any)!.GetValue(regex)!;
    Type fields = Type.GetType("Fuzzy.Text.RegularExpressions.Engine.PatternObject, FuzzyRegex", throwOnError: true)!;
    fields
        .GetField("MinWidth", Any)!
        .SetValue(patternObject, fields.GetField("UpstreamMinWidth", Any)!.GetValue(patternObject));
    return regex;
}

namespace D40Grid
{
    internal sealed class AssertThrows : System.Diagnostics.TraceListener
    {
        public override void Write(string? message) { }

        public override void WriteLine(string? message) { }

        public override void Fail(string? message, string? detailMessage) =>
            throw new AssertFailedException(message ?? "");
    }

    public sealed class AssertFailedException(string message) : Exception(message);
}
#pragma warning restore S3011
