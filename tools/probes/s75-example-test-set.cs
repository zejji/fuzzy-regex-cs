#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// S75 (2026-09-20): this port's half of the test-set worked example. The upstream half is
// tools/probes/s75-example-test-set.py, and the example the demo ships claims that "color" matches
// within a budget that may only touch lowercase letters, while "col our" and "col0ur" do not.
//
// Every candidate subject is searched on its own, so a match found in one cannot be credited to
// another. The last subject is the one the example actually ships.
//
//     dotnet run tools/probes/s75-example-test-set.cs

using Fuzzy.Text.RegularExpressions;

const string pattern = "(?:colour){e<=2:[a-z]}";

string[] subjects = ["colour", "color", "col our", "col0ur", "colour, color, col our and col0ur"];

Console.WriteLine($"FuzzyRegex port. Pattern '{pattern}'");
foreach (string subject in subjects)
{
    List<string> found = [];
    foreach (Match match in FuzzyRegex.EnumerateMatches(subject, pattern))
    {
        FuzzyCounts counts = match.FuzzyCounts;
        found.Add(
            $"(({match.Index}, {match.Index + match.Length}), '{match.Value}', "
                + $"({counts.Substitutions}, {counts.Insertions}, {counts.Deletions}))"
        );
    }

    // PadRight rather than an interpolation alignment specifier: CSharpier writes those with a
    // space after the comma and IDE0055 rejects the space, so a line using one satisfies neither of
    // this repo's formatting gates (the same note is on tools/probes/aot-smoke-slow-patterns.cs).
    string quotedSubject = $"'{subject}'";
    Console.WriteLine(quotedSubject.PadRight(36) + " [" + string.Join(", ", found) + "]");
}
