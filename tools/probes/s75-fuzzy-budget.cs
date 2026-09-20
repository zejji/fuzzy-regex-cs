#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// S75 (2026-09-20): this port's half of the unbounded-budget table. The upstream half is
// tools/probes/s75-fuzzy-budget.py, and the demo decides what to say from the pattern text alone
// (demo/web/src/lib/budget.ts), so the two have to agree about which budgets run away.
//
// A budget with no bound matches a subject that shares nothing with the pattern. A bounded one does
// not. Same cases, same subjects, same verdicts as the Python probe.
//
//     dotnet run tools/probes/s75-fuzzy-budget.cs

using Fuzzy.Text.RegularExpressions;

const string far = "zzzzzzzzz";
const string padded = "czozlzozuzzr";

(string Pattern, string Subject)[] cases =
[
    ("(?:colour){e}", far),
    ("(?:colour){s}", far),
    ("(?:colour){i}", padded),
    ("(?:colour){d}", far),
    ("(?:colour){s<=1,e}", far),
    ("(?:colour){s<=1,e}", "colouur"),
    ("(?:colour){e<=2}", far),
    ("(?:colour){e<2}", far),
    ("(?:colour){s<=1,i<=1,d<=1}", far),
    ("(?:colour){1i+1d<3}", far),
    ("(?:colour){e<=2:[a-z]}", far),
    ("(?:colour){e:[a-z]}", far),
    ("(?:colour){1<=e<=3}", far),
    ("(?:colour){2i+2d+1s<=4}", far),
    ("colou?r", far),
    ("a{2}", far),
    ("[{e}]", far),
    (@"colour\{e\}", far),
    ("[]{e}]x", "]x"),
    ("[]{e}]x", "}x"),
    ("(?:colour){e<=1,e}", far),
    ("(?:colour){e<=1,e}", "colour{e<=1,e}"),
];

foreach ((string pattern, string subject) in cases)
{
    string verdict;
    try
    {
        Match match = FuzzyRegex.Match(subject, pattern);
        FuzzyCounts counts = match.FuzzyCounts;
        verdict = match.Success
            ? $"match ({match.Index}, {match.Index + match.Length}) '{match.Value}' "
                + $"fuzzy_counts=({counts.Substitutions}, {counts.Insertions}, {counts.Deletions})"
            : "no match";
    }
    catch (FuzzyRegexParseException error)
    {
        verdict = $"ERROR {error.Message}";
    }

    string quotedPattern = $"'{pattern}'";
    string quotedSubject = $"'{subject}'";
    Console.WriteLine($"{quotedPattern, -30} {quotedSubject, -16} {verdict}");
}
