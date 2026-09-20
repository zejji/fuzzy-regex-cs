#:project ../../src/FuzzyRegex/FuzzyRegex.csproj
// S75 (2026-09-20): the port's half of the owner's `(foobar){e}` case. The upstream half, and what
// the case is about, is in tools/probes/s75-stacked-deletions.py.
//
//     pattern  (foobar){e}
//     subject  xirefoabralfobarxie
//
// Two columns, because the demo does not draw the library's own deletion positions. A deletion is
// reported where the missing character would sit in a string that had every deletion put back, so
// the i-th deletion of a match is i past the place in the subject it happened at; DemoEngine.Edits
// subtracts that i so the page has a position in the string it slices (DemoEngine.cs, the
// `changes.Deletions[i] - i`). "library" below is what upstream is compared against, "demo" is what
// reaches the highlight, and the stacking the slice fixes is only visible in the second.
//
//     dotnet run tools/probes/s75-stacked-deletions.cs

using Fuzzy.Text.RegularExpressions;

const string pattern = "(foobar){e}";
const string subject = "xirefoabralfobarxie";

Console.WriteLine($"pattern {pattern}  subject {subject}");
Console.WriteLine();
Console.WriteLine(
    $"{"#", 3}  {"span", 9}  {"text", -10}  {"s", 2} {"i", 2} {"d", 2}  library deletions -> demo deletions"
);

int number = 0;
foreach (Match match in FuzzyRegex.EnumerateMatches(subject, pattern))
{
    number++;
    FuzzyCounts counts = match.FuzzyCounts;
    FuzzyChanges changes = match.FuzzyChanges;

    int[] library = [.. changes.Deletions];
    int[] demo = [.. library.Select(static (at, i) => at - i)];

    string span = $"({match.Index},{match.Index + match.Length})";
    Console.WriteLine(
        $"{number, 3}  {span, 9}  {$"'{match.Value}'", -10}  "
            + $"{counts.Substitutions, 2} {counts.Insertions, 2} {counts.Deletions, 2}  "
            + $"[{string.Join(", ", library)}] -> [{string.Join(", ", demo)}]"
    );
}
