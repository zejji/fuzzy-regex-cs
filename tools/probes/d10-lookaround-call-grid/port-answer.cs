#:project ../../../src/FuzzyRegex/FuzzyRegex.csproj
// D10: one canonical answer line per JSON-lines row (pattern, subject, partial): span, fuzzy counts,
// every group's span and every group's capture list. upstream-answer.py prints the same shape.
using System.Text.Json;
using Fuzzy.Text.RegularExpressions;

var timeout = TimeSpan.FromSeconds(2);
bool onlyG = args.Length > 1 && string.Equals(args[1], "--only-g", StringComparison.Ordinal);
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
    bool partial = row.TryGetProperty("partial", out var p) && p.ValueKind == JsonValueKind.True;
    string answer;
    try
    {
        var regex = new FuzzyRegex(pattern, FuzzyRegexOptions.None, timeout);
        Match m = regex.Match(subject, 0, subject.Length, partial);
        if (!m.Success)
        {
            answer = "None";
        }
        else
        {
            var parts = new List<string>
            {
                $"({m.Index},{m.Index + m.Length})",
                $"{m.FuzzyCounts.Substitutions},{m.FuzzyCounts.Insertions},{m.FuzzyCounts.Deletions}",
                m.PartialMatch ? "P" : "F",
            };
            var spans = new List<string>();
            var caps = new List<string>();
            for (int g = 1; g < m.Groups.Count; g++)
            {
                var gr = m.Groups[g];
                if (onlyG && !gr.Name.StartsWith('g'))
                {
                    continue;
                }

                spans.Add(gr.Success ? $"({gr.Index},{gr.Index + gr.Length})" : "(-1,-1)");
                caps.Add(
                    "[" + string.Join("", gr.Captures.Select(static c => $"({c.Index},{c.Index + c.Length})")) + "]"
                );
            }
            parts.Add(string.Join("", spans));
            parts.Add(string.Join("", caps));
            answer = string.Join(" ", parts);
        }
    }
    catch (Exception e)
    {
        answer = "ERR " + e.GetType().Name;
    }
    Console.WriteLine(answer);
}
